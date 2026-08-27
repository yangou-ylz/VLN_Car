#!/usr/bin/env bash

# Package the clean Mesa Topgear team release project for external delivery.
# The archive is written under VLN_ASSETS_CACHE and must not be committed to Git.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
RELEASE_PROJECT="${VLN_MESA_TOPGEAR_RELEASE_PROJECT:-$VLN_ROOT/UnityProjects/VLN_MesaTopgear_TeamRelease}"
OUTPUT_DIR="${VLN_TEAM_RELEASE_OUTPUT_DIR:-$VLN_ROOT/VLN_ASSETS_CACHE/team_release_packages}"
PACKAGE_BASENAME="VLN_MesaTopgear_TeamRelease_$(date +%Y%m%d_%H%M%S)"
SPLIT_SIZE=""

usage() {
  cat <<'EOF'
用法：
  ./scripts/package_mesa_topgear_team_release_project.sh
  ./scripts/package_mesa_topgear_team_release_project.sh --split 1900M

说明：
  先运行 prepare_mesa_topgear_team_release_project.sh 生成干净发布工程。
  本脚本再把 UnityProjects/VLN_MesaTopgear_TeamRelease 压缩到 VLN_ASSETS_CACHE/team_release_packages。
  输出文件不进入 Git，适合通过网盘、内网文件服务、移动硬盘或 GitHub Release 附件分发。
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --split)
      if [[ $# -lt 2 ]]; then
        echo "--split 缺少大小参数，例如 1900M"
        exit 2
      fi
      SPLIT_SIZE="$2"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "未知参数：$1"
      usage
      exit 2
      ;;
  esac
done

if [[ ! -d "$RELEASE_PROJECT" ]]; then
  echo "缺少发布工程：$RELEASE_PROJECT"
  echo "请先运行：$VLN_ROOT/scripts/prepare_mesa_topgear_team_release_project.sh"
  exit 1
fi

mkdir -p "$OUTPUT_DIR"

if command -v zstd >/dev/null 2>&1; then
  archive="$OUTPUT_DIR/$PACKAGE_BASENAME.tar.zst"
  tar --zstd -cf "$archive" -C "$VLN_ROOT/UnityProjects" "$(basename "$RELEASE_PROJECT")"
else
  archive="$OUTPUT_DIR/$PACKAGE_BASENAME.tar.gz"
  tar -czf "$archive" -C "$VLN_ROOT/UnityProjects" "$(basename "$RELEASE_PROJECT")"
fi

sha_file="$archive.sha256"
sha256sum "$archive" > "$sha_file"

if [[ -n "$SPLIT_SIZE" ]]; then
  split_prefix="$archive.part."
  split -b "$SPLIT_SIZE" -d -a 3 "$archive" "$split_prefix"
  sha256sum "$archive" "$split_prefix"* > "$archive.parts.sha256"
  echo "split_prefix=$split_prefix"
  echo "parts_sha256=$archive.parts.sha256"
fi

cat > "$archive.manifest.txt" <<EOF
package=$archive
sha256_file=$sha_file
release_project=$RELEASE_PROJECT
generated_at=$(date -Iseconds)
open_command=./scripts/open_mesa_topgear_team_release_project.sh
check_command=./scripts/check_mesa_topgear_team_release_project.sh
EOF

echo "VLN_MESA_TOPGEAR_TEAM_RELEASE_PACKAGE_READY"
echo "package=$archive"
echo "sha256_file=$sha_file"
echo "manifest=$archive.manifest.txt"
