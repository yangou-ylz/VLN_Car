#!/usr/bin/env bash
set -euo pipefail

ROOT="/home/ubuntu22/VLN"
CACHE_DIR="$ROOT/VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages"
MIN_MB="${VLN_LARGE_ASSET_MIN_MB:-1}"

SEARCH_DIRS=(
  "$CACHE_DIR"
  "$ROOT/.unity_user/cache"
  "$ROOT/.unity_user/config"
  "$ROOT/.unity_user/data"
  "$HOME/Downloads"
  "$HOME/下载"
  "$HOME/Unity/Asset Store-5.x"
  "$HOME/.local/share/unity3d/Asset Store-5.x"
  "$HOME/.cache/unity3d"
  "$HOME/.config/unity3d"
  "$HOME/snap/unityhub/common"
  "$HOME/.var/app/com.unity.UnityHub"
)

echo "VLN_HIGH_PRECISION_LARGE_ASSET_PACKAGE_SEARCH"
echo "目标缓存目录：$CACHE_DIR"
echo "最小显示体积：${MIN_MB}MB（可用 VLN_LARGE_ASSET_MIN_MB 调整）"
echo

found=0
for dir in "${SEARCH_DIRS[@]}"; do
  [[ -d "$dir" ]] || continue
  echo "== 搜索目录：$dir"
  while IFS= read -r -d '' file; do
    size_bytes=$(stat -c '%s' "$file" 2>/dev/null || echo 0)
    keep=$(awk -v b="$size_bytes" -v min="$MIN_MB" 'BEGIN { print (b / 1024 / 1024 >= min) ? 1 : 0 }')
    [[ "$keep" == "1" ]] || continue
    found=1
    size_mb=$(awk -v b="$size_bytes" 'BEGIN { printf "%.2f", b / 1024 / 1024 }')
    printf '%10s MB  %s\n' "$size_mb" "$file"
  done < <(find "$dir" -maxdepth 5 -type f \( \
      -iname '*.unitypackage' -o \
      -iname '*.assetpackage' -o \
      -iname '*.zip' -o \
      -iname '*.tar' -o \
      -iname '*.tgz' -o \
      -iname '*.tar.gz' \
    \) -print0 2>/dev/null | sort -z)
  echo
done

if [[ "$found" -eq 0 ]]; then
  echo "VLN_HIGH_PRECISION_LARGE_ASSET_PACKAGE_SEARCH_EMPTY"
  echo "未发现可疑大资产包。浏览器/Unity 下载完成后，可用 stage 脚本复制到缓存目录。"
else
  echo "VLN_HIGH_PRECISION_LARGE_ASSET_PACKAGE_SEARCH_DONE"
  echo "如果发现目标包，运行："
  echo "  /home/ubuntu22/VLN/scripts/stage_high_precision_large_scene_package.sh '<上面显示的文件路径>'"
fi
