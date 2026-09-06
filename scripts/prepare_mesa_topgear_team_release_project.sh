#!/usr/bin/env bash

# Build a clean team handoff Unity project for the accepted Mesa Topgear Mix path.
# It copies the approved scene dependency graph plus the small VLN runtime/editor tooling.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
SOURCE_PROJECT="${VLN_LARGE_ASSET_PROJECT:-$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox}"
RELEASE_PROJECT="${VLN_MESA_TOPGEAR_RELEASE_PROJECT:-$VLN_ROOT/UnityProjects/VLN_MesaTopgear_TeamRelease}"
BACKUP_ROOT="$VLN_ROOT/UnityProjects/_TeamReleaseBackups"
TARGET_SCENE="Assets/VLN/Scenes/VLNMesaTopgearMixVehicleScale4p0WorldCandidate.unity"
TEAM_SCENE_KEY="mesa_topgear_mix"

usage() {
  cat <<'EOF'
用法：
  ./scripts/prepare_mesa_topgear_team_release_project.sh
  ./scripts/prepare_mesa_topgear_team_release_project.sh --refresh

用途：
  从已验证的大资产副本工程中生成干净团队发布版 Unity 工程：
  UnityProjects/VLN_MesaTopgear_TeamRelease

发布版用于打开：
  ./scripts/open_high_precision_world_model.sh --scene mesa_topgear_mix
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

required_paths=(
  "$SOURCE_PROJECT/Packages"
  "$SOURCE_PROJECT/ProjectSettings"
  "$SOURCE_PROJECT/$TARGET_SCENE"
  "$SOURCE_PROJECT/Assets/VLN/Editor"
  "$SOURCE_PROJECT/Assets/VLN/Scripts"
  "$VLN_ROOT/scripts/audit_mesa_topgear_team_release_assets.py"
)

for path in "${required_paths[@]}"; do
  if [[ ! -e "$path" ]]; then
    echo "缺少主线发布所需路径：$path"
    echo "请先确认 mesa_topgear_mix 已在大资产副本工程中验收通过。"
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
cp -a "$SOURCE_PROJECT/Packages" "$SOURCE_PROJECT/ProjectSettings" "$RELEASE_PROJECT/"

ASSET_AUDIT_REPORT="$VLN_ROOT/.runtime/release_asset_audit/mesa_topgear_mix_prepare_$(date +%Y%m%d_%H%M%S).json"
python3 "$VLN_ROOT/scripts/audit_mesa_topgear_team_release_assets.py" \
  --source-project "$SOURCE_PROJECT" \
  --scene "$TARGET_SCENE" \
  --report "$ASSET_AUDIT_REPORT" \
  --copy-to "$RELEASE_PROJECT"

if [[ -d "$RELEASE_PROJECT/Assets/VLN/Scenes" ]]; then
  unexpected_scene_count=$(find "$RELEASE_PROJECT/Assets/VLN/Scenes" -maxdepth 1 -type f -name '*.unity' ! -name 'VLNMesaTopgearMixVehicleScale4p0WorldCandidate.unity' | wc -l)
  if [[ "$unexpected_scene_count" != "0" ]]; then
    echo "依赖复制后发现额外场景文件，拒绝继续："
    find "$RELEASE_PROJECT/Assets/VLN/Scenes" -maxdepth 1 -type f -name '*.unity' ! -name 'VLNMesaTopgearMixVehicleScale4p0WorldCandidate.unity' | sort
    exit 1
  fi
fi

cat > "$RELEASE_PROJECT/VLN_MESA_TOPGEAR_TEAM_RELEASE.md" <<EOF
# VLN Mesa Topgear Mix Team Release

Generated at: $(date -Iseconds)

Target scene:

\`\`\`text
$TARGET_SCENE
\`\`\`

Open from repository root:

\`\`\`bash
./scripts/open_high_precision_world_model.sh --scene mesa_topgear_mix
\`\`\`

This folder is a local/team asset deliverable. Do not commit it to normal Git history.
EOF

python3 - <<PY
from pathlib import Path
import hashlib, json, os, time
root = Path(r"$RELEASE_PROJECT")
target_scene = root / r"$TARGET_SCENE"
manifest_path = root / "VLN_MESA_TOPGEAR_TEAM_RELEASE_MANIFEST.json"
def file_count_size(path: Path):
    count = 0
    size = 0
    for file in path.rglob('*'):
        if file.is_file():
            count += 1
            size += file.stat().st_size
    return count, size
count, size = file_count_size(root)
if manifest_path.exists():
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
else:
    manifest = {"schema": "vln_mesa_topgear_mix_team_release_manifest_v2"}
manifest.update({
    "generated_at_unix": int(time.time()),
    "source_project": r"$SOURCE_PROJECT",
    "release_project": r"$RELEASE_PROJECT",
    "target_scene": r"$TARGET_SCENE",
    "release_file_count": count,
    "release_size_bytes": size,
    "target_scene_size_bytes": target_scene.stat().st_size,
    "target_scene_sha256": hashlib.sha256(target_scene.read_bytes()).hexdigest(),
    "asset_audit_report": r"$ASSET_AUDIT_REPORT",
    "copy_mode": "scene_guid_dependencies_plus_vln_runtime_tooling",
    "team_open_command": "./scripts/open_high_precision_world_model.sh --scene $TEAM_SCENE_KEY"
})
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("release_file_count=" + str(count))
print("release_size_bytes=" + str(size))
print("target_scene_sha256=" + manifest["target_scene_sha256"])
PY

echo "VLN_MESA_TOPGEAR_TEAM_RELEASE_READY"
echo "release_project=$RELEASE_PROJECT"
echo "target_scene=$TARGET_SCENE"
echo "next_check=$VLN_ROOT/scripts/check_mesa_topgear_team_release_project.sh"
echo "next_open=$VLN_ROOT/scripts/open_high_precision_world_model.sh --scene $TEAM_SCENE_KEY"
