#!/usr/bin/env bash

# Mesa Topgear 小车本体视觉增强候选验收。
# 只生成候选场景、材质审计和截图；不跑 ROS2、不跑自动路线、不改原 mesa_topgear 场景。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
RUN_ID="vln_mesa_topgear_vehicle_visual_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
UNITY_LOG="$LOG_DIR/unity.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_mesa_topgear_vehicle_visual_result.txt"

mkdir -p "$LOG_DIR"

echo "Mesa Topgear 小车视觉增强 smoke test：只改候选场景，不跑路线。" | tee "$LOG_DIR/run_summary.txt"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee -a "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前大资产副本 Unity Editor，再运行该自动截图脚本。"
  exit 2
fi

if [ -f "$RESULT_FILE" ]; then
  mv "$RESULT_FILE" "$LOG_DIR/previous_$(basename "$RESULT_FILE")"
fi

set +e
timeout 170s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -quit \
  -executeMethod VLN.Editor.VlnMesaTopgearVehicleVisualEnhancer.RunBuildAndVisualSmokeTest \
  -logFile "$UNITY_LOG"
unity_status=$?
set -e

if [ -f "$RESULT_FILE" ]; then
  cp "$RESULT_FILE" "$LOG_DIR/$(basename "$RESULT_FILE")"
fi

while IFS='=' read -r key value; do
  case "$key" in
    vehicle_*_screenshot|wheel_close_screenshot|upper_sensor_module_screenshot)
      if [ -f "$value" ]; then
        cp "$value" "$LOG_DIR/$(basename "$value")"
      fi
      ;;
  esac
done < "$RESULT_FILE" 2>/dev/null || true

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "result_file=$RESULT_FILE"
  echo "log_dir=$LOG_DIR"
} | tee -a "$LOG_DIR/run_summary.txt"

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  tail -n 180 "$UNITY_LOG" || true
  exit 1
fi

if [ ! -f "$RESULT_FILE" ]; then
  echo "missing_result_file=$RESULT_FILE" | tee -a "$LOG_DIR/run_summary.txt"
  tail -n 180 "$UNITY_LOG" || true
  exit 1
fi

cat "$RESULT_FILE" | tee -a "$LOG_DIR/run_summary.txt"
echo "Key Unity log lines:" | tee -a "$LOG_DIR/run_summary.txt"
grep -n -E "VLN_MESA_TOPGEAR_VEHICLE_VISUAL|Exception|NullReference|error CS|Compilation failed|Exiting" "$UNITY_LOG" | sed -n '1,220p' | tee -a "$LOG_DIR/run_summary.txt" || true

if ! sed '1s/^\xef\xbb\xbf//' "$RESULT_FILE" | grep -q '^success=1$'; then
  echo "mesa_topgear_vehicle_visual_result_failed" | tee -a "$LOG_DIR/run_summary.txt"
  exit 1
fi

echo "VLN_MESA_TOPGEAR_VEHICLE_VISUAL_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
