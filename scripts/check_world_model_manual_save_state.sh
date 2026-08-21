#!/usr/bin/env bash

# 检查 Unity 菜单“VLN -> 更改世界模型 -> 保存本次世界”是否真的写入磁盘。
# 只读校验：不打开 Unity，不修改场景，不启动 ROS2。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
MANIFEST="$VLN_ROOT/config/world_model_current_save.json"
DEFAULT_SCENE="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox/Assets/VLN/Scenes/VLNMesaOasisStitchedRouteCandidate.unity"

python3 - "$MANIFEST" "$DEFAULT_SCENE" <<'PY'
import hashlib
import json
import pathlib
import sys

manifest_path = pathlib.Path(sys.argv[1])
default_scene_path = pathlib.Path(sys.argv[2])

def fail(message: str) -> None:
    print("VLN_WORLD_MODEL_MANUAL_SAVE_CHECK_FAIL")
    print(message)
    raise SystemExit(1)

def sha256_file(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()

def file_contains(path: pathlib.Path, needle: str) -> bool:
    needle_bytes = needle.encode("utf-8")
    overlap = max(len(needle_bytes) - 1, 0)
    previous = b""
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            data = previous + chunk
            if needle_bytes in data:
                return True
            previous = data[-overlap:] if overlap else b""
    return False

if not manifest_path.exists():
    fail(f"未找到保存记录：{manifest_path}\n请先在 Unity Edit 模式点击 VLN -> 更改世界模型 -> 保存本次世界。")

try:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
except Exception as exc:
    fail(f"保存记录不是有效 JSON：{manifest_path}\n{exc}")

scene_path = pathlib.Path(manifest.get("absolute_scene_path") or default_scene_path)
marker = manifest.get("save_marker")
expected_sha = manifest.get("scene_sha256")

if not scene_path.exists():
    fail(f"保存记录指向的场景不存在：{scene_path}")
if not marker:
    fail("保存记录缺少 save_marker。")
if not expected_sha:
    fail("保存记录缺少 scene_sha256。")
if not file_contains(scene_path, marker):
    fail(f"场景文件中找不到保存 marker：{marker}\n这说明上次保存记录和当前 .unity 场景不一致。")

actual_sha = sha256_file(scene_path)
if actual_sha != expected_sha:
    fail(
        "场景 SHA256 与保存记录不一致。\n"
        f"recorded={expected_sha}\n"
        f"actual={actual_sha}\n"
        "如果你又手工改过场景，请回 Unity 再点击一次“保存本次世界”。"
    )

print("VLN_WORLD_MODEL_MANUAL_SAVE_CHECK_PASS")
print(f"scene={scene_path}")
print(f"manifest={manifest_path}")
print(f"marker={marker}")
print(f"sha256={actual_sha}")
print("下次使用 ./scripts/open_pure_nature_mesa_oasis_stitched_scene.sh 会打开这份已保存场景；普通重建脚本不会自动覆盖它。")
PY
