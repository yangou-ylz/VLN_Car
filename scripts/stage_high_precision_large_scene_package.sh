#!/usr/bin/env bash
set -euo pipefail

ROOT="/home/ubuntu22/VLN"
CACHE_DIR="$ROOT/VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages"
MAX_BYTES=$((100 * 1024 * 1024 * 1024))

if [[ $# -ne 1 ]]; then
  echo "用法：$0 <大资产包文件或解包目录路径>"
  echo "文件支持：.unitypackage .assetpackage .zip .tar .tgz .tar.gz"
  exit 2
fi

src="${1%/}"
if [[ ! -e "$src" ]]; then
  echo "路径不存在：$src"
  exit 1
fi

if [[ -f "$src" ]]; then
  case "${src,,}" in
    *.unitypackage|*.assetpackage|*.zip|*.tar|*.tgz|*.tar.gz) ;;
    *)
      echo "不支持的文件类型：$src"
      exit 1
      ;;
  esac
elif [[ ! -d "$src" ]]; then
  echo "不支持的路径类型：$src"
  exit 1
fi

if [[ -d "$src" ]]; then
  size_bytes=$(du -sb "$src" | awk '{print $1}')
else
  size_bytes=$(stat -c '%s' "$src")
fi
if (( size_bytes > MAX_BYTES )); then
  size_gb=$(awk -v b="$size_bytes" 'BEGIN { printf "%.2f", b / 1024 / 1024 / 1024 }')
  echo "文件大小 ${size_gb}GB 超过 100GB 硬上限，暂停。"
  exit 1
fi

mkdir -p "$CACHE_DIR"
base=$(basename "$src")
dest="$CACHE_DIR/$base"

if [[ -e "$dest" ]]; then
  echo "目标已存在，不覆盖：$dest"
  echo "如需重新暂存，请先人工改名源文件或目标文件。"
  exit 1
fi

cp -a "$src" "$dest"

echo "VLN_HIGH_PRECISION_LARGE_ASSET_PACKAGE_STAGED"
echo "source=$src"
echo "dest=$dest"
echo "size_bytes=$size_bytes"
echo "下一步："
echo "  cd /home/ubuntu22/VLN"
echo "  ./scripts/scan_high_precision_large_scene_packages.sh"
