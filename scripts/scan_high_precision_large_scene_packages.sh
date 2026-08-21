#!/usr/bin/env bash
set -euo pipefail

ROOT="/home/ubuntu22/VLN"
INPUT_DIR="$ROOT/VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages"
OUTPUT_DIR="$ROOT/VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_inspections"
SCANNER="$ROOT/scripts/inspect_high_precision_large_asset_package.py"
RANKER="$ROOT/scripts/rank_high_precision_large_asset_inspections.py"
RANKING_REPORT="$OUTPUT_DIR/large_asset_ranking.md"

mkdir -p "$INPUT_DIR" "$OUTPUT_DIR"

mapfile -t ENTRIES < <(find "$INPUT_DIR" -mindepth 1 -maxdepth 1 \( -type f -o -type d \) | sort)

if [[ ${#ENTRIES[@]} -eq 0 ]]; then
  echo "VLN_HIGH_PRECISION_LARGE_ASSET_SCAN_NO_PACKAGES"
  echo "大资产目录为空：$INPUT_DIR"
  echo "把 .unitypackage/.zip/.tar 或解包目录放进这里后再运行本脚本。"
  if [[ -x "$RANKER" ]]; then
    python3 "$RANKER" --output "$RANKING_REPORT" >/dev/null
    echo "排序报告：$RANKING_REPORT"
  fi
  exit 0
fi

echo "待扫描大资产数量：${#ENTRIES[@]}"
for entry in "${ENTRIES[@]}"; do
  base="$(basename "$entry")"
  safe="$(printf '%s' "$base" | tr -cs 'A-Za-z0-9._-' '_' | sed 's/^_*//; s/_*$//')"
  [[ -n "$safe" ]] || safe="asset_package"
  out="$OUTPUT_DIR/${safe}_inspection.json"
  echo "== 扫描：$entry"
  python3 "$SCANNER" "$entry" --output "$out"
  echo "输出：$out"
done

echo "VLN_HIGH_PRECISION_LARGE_ASSET_SCAN_DONE"

if [[ -x "$RANKER" ]]; then
  python3 "$RANKER" --output "$RANKING_REPORT"
  echo "排序报告：$RANKING_REPORT"
fi
