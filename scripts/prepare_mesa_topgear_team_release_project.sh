#!/usr/bin/env bash

# Build a clean team handoff Unity project for the accepted Mesa Topgear path.
# It copies only the Mesa desert world, VLN vehicle/sensor assets, Packages,
# ProjectSettings, and Resources. Other imported worlds remain excluded.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
SOURCE_PROJECT="${VLN_LARGE_ASSET_PROJECT:-$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox}"
RELEASE_PROJECT="${VLN_MESA_TOPGEAR_RELEASE_PROJECT:-$VLN_ROOT/UnityProjects/VLN_MesaTopgear_TeamRelease}"
BACKUP_ROOT="$VLN_ROOT/UnityProjects/_TeamReleaseBackups"
TARGET_SCENE="Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity"

usage() {
  cat <<'EOF'
用法：
  ./scripts/prepare_mesa_topgear_team_release_project.sh
  ./scripts/prepare_mesa_topgear_team_release_project.sh --refresh

用途：
  从已验证的大资产副本工程中生成干净团队发布版 Unity 工程：
  UnityProjects/VLN_MesaTopgear_TeamRelease

发布版只包含：
  - Pure Nature Mesa Desert 资产
  - Pure_Common 共享 shader/script
  - VLN 小车、传感器、控制、配置脚本与场景
  - Unity Packages、ProjectSettings、Resources

发布版不包含：
  - Oasis / Meadow / ForestLake
  - Unity Library / Temp / Logs / UserSettings
  - 原始 .unitypackage、rosbag、截图缓存
EOF
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

if [[ "${1:-}" != "" && "${1:-}" != "--refresh" ]]; then
  echo "未知参数：$1"
  usage
  exit 2
fi

if ! command -v rsync >/dev/null 2>&1; then
  echo "缺少 rsync，不能安全生成团队发布工程。"
  exit 1
fi

required_paths=(
  "$SOURCE_PROJECT/Assets/BK/PureNature_MesaDesert"
  "$SOURCE_PROJECT/Assets/BK/Pure_Common"
  "$SOURCE_PROJECT/Assets/VLN"
  "$SOURCE_PROJECT/Assets/Resources"
  "$SOURCE_PROJECT/Packages"
  "$SOURCE_PROJECT/ProjectSettings"
  "$SOURCE_PROJECT/$TARGET_SCENE"
)

for path in "${required_paths[@]}"; do
  if [[ ! -e "$path" ]]; then
    echo "缺少主线发布所需路径：$path"
    echo "请先确认 mesa_topgear 已在大资产副本工程中验收通过。"
    exit 1
  fi
done

if [[ -e "$RELEASE_PROJECT" ]]; then
  if [[ "${1:-}" != "--refresh" ]]; then
    echo "VLN_MESA_TOPGEAR_TEAM_RELEASE_EXISTS"
    echo "release_project=$RELEASE_PROJECT"
    echo "如需重新生成，运行：$0 --refresh"
    exit 0
  fi
  mkdir -p "$BACKUP_ROOT"
  backup_path="$BACKUP_ROOT/VLN_MesaTopgear_TeamRelease_$(date +%Y%m%d_%H%M%S)"
  mv "$RELEASE_PROJECT" "$backup_path"
  echo "已备份旧发布工程：$backup_path"
fi

mkdir -p "$RELEASE_PROJECT/Assets/BK"

rsync -a \
  --exclude='Library/' \
  --exclude='Temp/' \
  --exclude='Obj/' \
  --exclude='Logs/' \
  --exclude='Build/' \
  --exclude='Builds/' \
  --exclude='UserSettings/' \
  --exclude='Recordings/' \
  "$SOURCE_PROJECT/Packages" \
  "$SOURCE_PROJECT/ProjectSettings" \
  "$RELEASE_PROJECT/"

if [[ -f "$SOURCE_PROJECT/Assets/BK.meta" ]]; then
  rsync -a "$SOURCE_PROJECT/Assets/BK.meta" "$RELEASE_PROJECT/Assets/"
fi

rsync -a "$SOURCE_PROJECT/Assets/BK/PureNature_MesaDesert" "$RELEASE_PROJECT/Assets/BK/"
if [[ -f "$SOURCE_PROJECT/Assets/BK/PureNature_MesaDesert.meta" ]]; then
  rsync -a "$SOURCE_PROJECT/Assets/BK/PureNature_MesaDesert.meta" "$RELEASE_PROJECT/Assets/BK/"
fi
rsync -a "$SOURCE_PROJECT/Assets/BK/Pure_Common" "$RELEASE_PROJECT/Assets/BK/"
if [[ -f "$SOURCE_PROJECT/Assets/BK/Pure_Common.meta" ]]; then
  rsync -a "$SOURCE_PROJECT/Assets/BK/Pure_Common.meta" "$RELEASE_PROJECT/Assets/BK/"
fi

rsync -a "$SOURCE_PROJECT/Assets/VLN" "$RELEASE_PROJECT/Assets/"
if [[ -f "$SOURCE_PROJECT/Assets/VLN.meta" ]]; then
  rsync -a "$SOURCE_PROJECT/Assets/VLN.meta" "$RELEASE_PROJECT/Assets/"
fi
rsync -a "$SOURCE_PROJECT/Assets/Resources" "$RELEASE_PROJECT/Assets/"
if [[ -f "$SOURCE_PROJECT/Assets/Resources.meta" ]]; then
  rsync -a "$SOURCE_PROJECT/Assets/Resources.meta" "$RELEASE_PROJECT/Assets/"
fi

cat > "$RELEASE_PROJECT/VLN_MESA_TOPGEAR_TEAM_RELEASE.md" <<EOF
# VLN Mesa Topgear Team Release

Generated at: $(date -Iseconds)

Target scene:

\`\`\`text
$TARGET_SCENE
\`\`\`

Open from repository root:

\`\`\`bash
./scripts/open_mesa_topgear_team_release_project.sh
\`\`\`

This folder is a local/team asset deliverable. Do not commit it to normal Git history.
EOF

python3 - <<PY
from pathlib import Path
import hashlib, json, os, time
root = Path(r"$RELEASE_PROJECT")
target_scene = root / r"$TARGET_SCENE"
def file_count_size(path: Path):
    count = 0
    size = 0
    for file in path.rglob('*'):
        if file.is_file():
            count += 1
            size += file.stat().st_size
    return count, size
count, size = file_count_size(root)
manifest = {
    "schema": "vln_mesa_topgear_team_release_v1",
    "generated_at_unix": int(time.time()),
    "source_project": r"$SOURCE_PROJECT",
    "release_project": r"$RELEASE_PROJECT",
    "target_scene": r"$TARGET_SCENE",
    "file_count": count,
    "size_bytes": size,
    "target_scene_size_bytes": target_scene.stat().st_size,
    "target_scene_sha256": hashlib.sha256(target_scene.read_bytes()).hexdigest(),
    "included_asset_roots": [
        "Assets/BK/PureNature_MesaDesert",
        "Assets/BK/Pure_Common",
        "Assets/VLN",
        "Assets/Resources",
        "Packages",
        "ProjectSettings"
    ],
    "excluded_worlds": ["PureNature_Oasis", "Meadow Environment Dynamic Nature", "ForestLake"]
}
(root / "VLN_MESA_TOPGEAR_TEAM_RELEASE_MANIFEST.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("release_file_count=" + str(count))
print("release_size_bytes=" + str(size))
print("target_scene_sha256=" + manifest["target_scene_sha256"])
PY

echo "VLN_MESA_TOPGEAR_TEAM_RELEASE_READY"
echo "release_project=$RELEASE_PROJECT"
echo "target_scene=$TARGET_SCENE"
echo "next_check=$VLN_ROOT/scripts/check_mesa_topgear_team_release_project.sh"
echo "next_open=$VLN_ROOT/scripts/open_mesa_topgear_team_release_project.sh"
