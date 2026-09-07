#!/usr/bin/env bash

# Read-only validation for the Mesa Topgear Mix team release project.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
RELEASE_PROJECT="${VLN_MESA_TOPGEAR_RELEASE_PROJECT:-$VLN_ROOT/UnityProjects/VLN_MesaTopgear_TeamRelease}"
TARGET_SCENE="Assets/VLN/Scenes/VLNMesaTopgearMixVehicleScale4p0WorldCandidate.unity"
PYTHON_BIN="${PYTHON_BIN:-python3}"

fail_count=0
warn_count=0

pass() { printf '[PASS] %s\n' "$1"; }
warn() { printf '[WARN] %s\n' "$1"; warn_count=$((warn_count + 1)); }
fail() { printf '[FAIL] %s\n' "$1"; fail_count=$((fail_count + 1)); }

echo "== Mesa Topgear Mix 团队发布工程检查 =="
echo "release_project=$RELEASE_PROJECT"

release_manifest="$RELEASE_PROJECT/VLN_MESA_TOPGEAR_TEAM_RELEASE_MANIFEST.json"
release_mode="unknown"
if [[ -f "$release_manifest" ]]; then
  if grep -q 'full_fidelity\|full_assets_packages_projectsettings' "$release_manifest"; then
    release_mode="full_fidelity"
  elif grep -q 'scene_guid_dependencies_plus_vln_runtime_tooling' "$release_manifest"; then
    release_mode="trimmed_dependency"
  fi
fi
echo "release_mode=$release_mode"

if [[ -d "$RELEASE_PROJECT" ]]; then
  pass "发布工程目录存在。"
else
  fail "发布工程目录不存在。请先运行 scripts/prepare_mesa_topgear_team_release_project.sh，或放入团队发布资产包。"
fi

required_paths=(
  "$RELEASE_PROJECT/$TARGET_SCENE"
  "$RELEASE_PROJECT/Assets/VLN/Editor"
  "$RELEASE_PROJECT/Assets/VLN/Scripts"
  "$RELEASE_PROJECT/Assets/VLN/LiDARScanPatterns"
  "$RELEASE_PROJECT/Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven/Surfaces/aerial_sand/aerial_sand_diff_4k.jpg"
  "$RELEASE_PROJECT/Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven/Surfaces/aerial_sand/aerial_sand_nor_gl_4k.jpg"
  "$RELEASE_PROJECT/Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven/Surfaces/aerial_ground_rock/aerial_ground_rock_diff_4k.jpg"
  "$RELEASE_PROJECT/Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven/Surfaces/aerial_ground_rock/aerial_ground_rock_nor_gl_4k.jpg"
  "$RELEASE_PROJECT/Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven/Surfaces/cliff_side/cliff_side_diff_4k.jpg"
  "$RELEASE_PROJECT/Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven/Surfaces/cliff_side/cliff_side_nor_gl_4k.jpg"
  "$RELEASE_PROJECT/Assets/Resources"
  "$RELEASE_PROJECT/Packages/manifest.json"
  "$RELEASE_PROJECT/ProjectSettings/ProjectVersion.txt"
  "$RELEASE_PROJECT/VLN_MESA_TOPGEAR_TEAM_RELEASE_MANIFEST.json"
)

for path in "${required_paths[@]}"; do
  if [[ -e "$path" ]]; then
    pass "存在：${path#$RELEASE_PROJECT/}"
  else
    fail "缺少：${path#$RELEASE_PROJECT/}"
  fi
done

if [[ -e "$RELEASE_PROJECT/Temp" ]]; then
  fail "发布工程不应包含：Temp"
fi

if [[ "$release_mode" == "trimmed_dependency" || "$release_mode" == "unknown" ]]; then
  excluded_asset_paths=(
    "$RELEASE_PROJECT/Assets/BK/PureNature_Oasis"
    "$RELEASE_PROJECT/Assets/VLN/Scenes/VLNMesaTopgearMixWorldScale0p8WorldCandidate.unity"
    "$RELEASE_PROJECT/Assets/VLN/Scenes/VLNMesaTopgearMixWorldScale0p9WorldCandidate.unity"
  )

  for path in "${excluded_asset_paths[@]}"; do
    if [[ -e "$path" ]]; then
      fail "精简发布工程不应包含：${path#$RELEASE_PROJECT/}"
    fi
  done
else
  pass "全保真发布模式允许保留源工程完整资产目录。"
fi

generated_editor_paths=(
  "$RELEASE_PROJECT/Library"
  "$RELEASE_PROJECT/Logs"
  "$RELEASE_PROJECT/UserSettings"
)

for path in "${generated_editor_paths[@]}"; do
  if [[ -e "$path" ]]; then
    warn "Unity 打开工程后会生成：${path#$RELEASE_PROJECT/}；这不是资产缺失，不影响运行。"
  fi
done

if [[ -d "$RELEASE_PROJECT/Assets/VLN/Scenes" ]]; then
  unexpected_scenes=()
  while IFS= read -r scene_path; do
    scene_name="$(basename "$scene_path")"
    if [[ "$scene_name" != "VLNMesaTopgearMixVehicleScale4p0WorldCandidate.unity" ]]; then
      unexpected_scenes+=("$scene_name")
    fi
  done < <(find "$RELEASE_PROJECT/Assets/VLN/Scenes" -maxdepth 1 -type f -name '*.unity' | sort)

  if [[ "$release_mode" == "full_fidelity" ]]; then
    pass "全保真发布模式允许保留源工程其它候选场景文件。"
  elif (( ${#unexpected_scenes[@]} == 0 )); then
    pass "发布工程只包含 Mesa Topgear Mix 主场景。"
  else
    fail "发布工程包含额外场景文件："
    printf '  %s\n' "${unexpected_scenes[@]}"
  fi
fi

if [[ -f "$RELEASE_PROJECT/ProjectSettings/ProjectVersion.txt" ]] && grep -q '2022.3.62f1' "$RELEASE_PROJECT/ProjectSettings/ProjectVersion.txt"; then
  pass "Unity 版本为 2022.3.62f1。"
else
  fail "Unity 版本不是 2022.3.62f1 或版本文件缺失。"
fi

if [[ -f "$RELEASE_PROJECT/Packages/manifest.json" ]] \
  && grep -q 'com.unity.robotics.ros-tcp-connector' "$RELEASE_PROJECT/Packages/manifest.json" \
  && grep -q 'com.frj.unity-sensors' "$RELEASE_PROJECT/Packages/manifest.json" \
  && grep -q 'com.frj.unity-sensors-ros' "$RELEASE_PROJECT/Packages/manifest.json"; then
  pass "Unity ROS/传感器依赖已写入 manifest。"
else
  fail "manifest 缺少 ROS-TCP-Connector 或 UnitySensors 依赖。"
fi

if [[ -f "$release_manifest" ]] \
  && grep -q 'VLNMesaTopgearMixVehicleScale4p0WorldCandidate.unity' "$release_manifest" \
  && [[ "$release_mode" != "unknown" ]]; then
  if [[ "$release_mode" == "full_fidelity" ]]; then
    pass "发布工程 manifest 记录了全保真资产复制模式。"
  else
    pass "发布工程 manifest 记录了 Mix 主场景依赖清单复制模式。"
  fi
else
  fail "发布工程 manifest 缺少有效复制模式或目标场景记录。"
fi

if [[ -d "$RELEASE_PROJECT/Assets/VLN/Terrain" ]]; then
  terrain_guid_report="$($PYTHON_BIN - "$RELEASE_PROJECT" <<'PY'
from pathlib import Path
import re
import sys

project = Path(sys.argv[1])
guid_re = re.compile(r"guid:\s*([0-9a-fA-F]{32})")
guid_map = {}

for meta in (project / "Assets").rglob("*.meta"):
    try:
        text = meta.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        continue
    match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", text, flags=re.MULTILINE)
    if match:
        guid_map[match.group(1).lower()] = str(meta.with_suffix("").relative_to(project))

missing = []
terrain_root = project / "Assets" / "VLN" / "Terrain"
for terrain_layer in terrain_root.glob("*.terrainlayer"):
    text = terrain_layer.read_text(encoding="utf-8", errors="ignore")
    for guid in sorted({m.group(1).lower() for m in guid_re.finditer(text)}):
        if guid not in guid_map:
            missing.append(f"{terrain_layer.relative_to(project)} -> {guid}")

if missing:
    print("FAIL")
    for item in missing:
        print(item)
else:
    print("OK")
PY
)"
  if [[ "$terrain_guid_report" == "OK" ]]; then
    pass "TerrainLayer 引用的地表贴图 GUID 均可解析。"
  else
    fail "TerrainLayer 存在无法解析的地表贴图 GUID："
    printf '%s\n' "$terrain_guid_report" | sed '1d;s/^/  /'
  fi
fi

if [[ -d "$RELEASE_PROJECT/Assets" && -f "$RELEASE_PROJECT/$TARGET_SCENE" ]]; then
  asset_reference_report="$($PYTHON_BIN - "$RELEASE_PROJECT" "$TARGET_SCENE" <<'PY'
from collections import Counter, deque
from pathlib import Path
import re
import sys

project = Path(sys.argv[1])
target_scene = sys.argv[2]
assets = project / "Assets"
meta_guid_re = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
ref_re = re.compile(rb"guid:\s*([0-9a-fA-F]{32})")
shader_re = re.compile(r"m_Shader:\s*\{fileID:\s*[-0-9]+,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*\d+\}")
texture_re = re.compile(r"m_Texture:\s*\{fileID:\s*[-0-9]+,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*\d+\}")
scan_exts = {
    ".unity", ".prefab", ".mat", ".asset", ".terrainlayer", ".controller",
    ".overridecontroller", ".anim", ".mask", ".rendertexture", ".cubemap",
    ".physicsmaterial2d", ".physicsmaterial", ".playable", ".preset",
    ".asmdef", ".shadergraph", ".shadersubgraph", ".compute", ".shader",
    ".inputactions", ".spriteatlas", ".guiskin", ".fontsettings", ".lighting", ".meta",
}


def is_ignored_guid(guid: str) -> bool:
    guid = guid.lower()
    return guid == "0" * 32 or guid.startswith("0000000000000000") or guid.startswith("abc000000000")


guid_map = {}
duplicate_guids = []
for meta in assets.rglob("*.meta"):
    try:
        text = meta.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        continue
    match = meta_guid_re.search(text)
    if not match:
        continue
    guid = match.group(1).lower()
    asset_rel = meta.with_suffix("").relative_to(project).as_posix()
    previous = guid_map.get(guid)
    if previous and previous != asset_rel:
        duplicate_guids.append(f"{guid}: {previous} | {asset_rel}")
    guid_map[guid] = asset_rel

missing_meta = []
orphan_meta = []
for path in assets.rglob("*"):
    if path.name.endswith(".meta"):
        base = Path(str(path)[:-5])
        if not base.exists():
            orphan_meta.append(path.relative_to(project).as_posix())
        continue
    if path.is_file() and not (path.parent / (path.name + ".meta")).exists():
        missing_meta.append(path.relative_to(project).as_posix())

closure = set()
queue = deque([target_scene])
while queue:
    rel_path = queue.popleft()
    if rel_path in closure:
        continue
    closure.add(rel_path)
    path = project / rel_path
    if not path.is_file():
        continue
    scan_paths = [path]
    meta = path.parent / (path.name + ".meta")
    if meta.is_file():
        scan_paths.append(meta)
    for scan_path in scan_paths:
        ext = ".meta" if scan_path.name.endswith(".meta") else scan_path.suffix.lower()
        if ext not in scan_exts:
            continue
        try:
            data = scan_path.read_bytes()
        except OSError:
            continue
        for match in ref_re.finditer(data):
            guid = match.group(1).decode("ascii").lower()
            if is_ignored_guid(guid):
                continue
            dependency = guid_map.get(guid)
            if dependency and dependency not in closure:
                queue.append(dependency)

material_shader_missing = []
material_texture_missing = []
for rel_path in sorted(path for path in closure if path.endswith(".mat")):
    path = project / rel_path
    if not path.is_file():
        continue
    text = path.read_text(encoding="utf-8", errors="ignore")
    for guid in shader_re.findall(text):
        guid = guid.lower()
        if not is_ignored_guid(guid) and guid not in guid_map:
            material_shader_missing.append(f"{rel_path} -> shader {guid}")
    for guid in texture_re.findall(text):
        guid = guid.lower()
        if not is_ignored_guid(guid) and guid not in guid_map:
            material_texture_missing.append(f"{rel_path} -> texture {guid}")

failures = []
if missing_meta:
    failures.append("资产文件缺少 .meta: " + str(len(missing_meta)))
if orphan_meta:
    failures.append("存在孤立 .meta: " + str(len(orphan_meta)))
if duplicate_guids:
    failures.append("存在重复 GUID: " + str(len(duplicate_guids)))
if material_shader_missing:
    failures.append("当前主场景材质存在无法解析的 shader GUID: " + str(len(material_shader_missing)))
if material_texture_missing:
    failures.append("当前主场景材质存在无法解析的 texture GUID: " + str(len(material_texture_missing)))

if failures:
    print("FAIL")
    print("主场景依赖文件数=" + str(len(closure)))
    for failure in failures:
        print(failure)
    for title, items in (
        ("missing_meta", missing_meta),
        ("orphan_meta", orphan_meta),
        ("duplicate_guid", duplicate_guids),
        ("material_shader_missing", material_shader_missing),
        ("material_texture_missing", material_texture_missing),
    ):
        if items:
            print(title + ":")
            for item in items[:40]:
                print("  " + item)
else:
    print("OK")
    print("主场景依赖文件数=" + str(len(closure)))
    print("材质文件数=" + str(sum(1 for path in closure if path.endswith(".mat"))))
PY
)"
  asset_reference_status="$(printf '%s\n' "$asset_reference_report" | sed -n '1p')"
  if [[ "$asset_reference_status" == "OK" ]]; then
    pass "主场景资产 .meta/GUID 与材质贴图引用均可解析。"
  else
    fail "主场景资产引用完整性检查失败："
    printf '%s\n' "$asset_reference_report" | sed '1d;s/^/  /'
  fi
fi

if [[ -f "$VLN_ROOT/config/topgear_sensor_pose_user_locked.json" \
  && -f "$VLN_ROOT/config/topgear_upper_assembly_user_locked.json" \
  && -f "$VLN_ROOT/config/topgear_camera_data_pose_user_locked.json" ]]; then
  pass "仓库根目录包含 Topgear 传感器/上装/真实相机锁定配置。"
else
  fail "仓库根目录缺少 Topgear 锁定配置。"
fi

if [[ -d "$RELEASE_PROJECT" ]]; then
  nested_packages=()
  while IFS= read -r package_path; do
    nested_packages+=("${package_path#$RELEASE_PROJECT/}")
  done < <(find "$RELEASE_PROJECT/Assets" -type f \( -name '*.unitypackage' -o -name '*.assetpackage' \) 2>/dev/null | sort)

  if (( ${#nested_packages[@]} == 0 )); then
    pass "发布工程未包含嵌套 Unity 原始资产包。"
  else
    fail "发布工程不应包含嵌套 Unity 原始资产包："
    printf '  %s\n' "${nested_packages[@]}"
  fi

  echo "== 发布工程体量 =="
  du -sh "$RELEASE_PROJECT" 2>/dev/null || true
  find "$RELEASE_PROJECT" -type f -size +95M -printf '%s %p\n' 2>/dev/null | sort -nr | sed -n '1,40p'
fi

echo "summary: failures=$fail_count warnings=$warn_count"
if (( fail_count > 0 )); then
  echo "VLN_MESA_TOPGEAR_TEAM_RELEASE_CHECK_FAILED"
  exit 1
fi

echo "VLN_MESA_TOPGEAR_TEAM_RELEASE_CHECK_OK"
