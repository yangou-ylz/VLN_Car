#!/usr/bin/env bash

# 阶段 21：高精度荒漠沙盒视觉导入验收。
# 只构建独立沙盒并截图，不接 Topgear、不启动 ROS2、不覆盖旧主场景。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_high_precision_desert_sandbox_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
UNITY_LOG="$LOG_DIR/unity.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_high_precision_desert_sandbox_result.txt"

mkdir -p "$LOG_DIR"

echo "阶段 21 沙盒视觉验收：构建 1000m x 1000m 高精荒漠沙盒，只截图，不跑 ROS2。" | tee "$LOG_DIR/run_summary.txt"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee -a "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前 Unity Editor，再运行该自动截图脚本。"
  exit 2
fi

if [ -f "$RESULT_FILE" ]; then
  mv "$RESULT_FILE" "$LOG_DIR/previous_$(basename "$RESULT_FILE")"
fi

set +e
timeout 260s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnHighPrecisionDesertSandboxSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
set -e

if [ "$unity_status" -ne 0 ]; then
  echo "unity_status=$unity_status" | tee -a "$LOG_DIR/run_summary.txt"
  tail -n 160 "$UNITY_LOG" || true
  exit "$unity_status"
fi

if [ ! -f "$RESULT_FILE" ]; then
  echo "missing_result_file=$RESULT_FILE" | tee -a "$LOG_DIR/run_summary.txt"
  tail -n 160 "$UNITY_LOG" || true
  exit 1
fi

cp "$RESULT_FILE" "$LOG_DIR/$(basename "$RESULT_FILE")"
while IFS='=' read -r key value; do
  case "$key" in
    *_screenshot)
      if [ -f "$value" ]; then
        cp "$value" "$LOG_DIR/$(basename "$value")"
      fi
      ;;
  esac
done < "$RESULT_FILE"

if ! grep -q '^success=1$' "$RESULT_FILE"; then
  echo "result_not_success" | tee -a "$LOG_DIR/run_summary.txt"
  cat "$RESULT_FILE" | tee -a "$LOG_DIR/run_summary.txt"
  exit 1
fi

cat "$RESULT_FILE" | tee -a "$LOG_DIR/run_summary.txt"
echo "VLN_HIGH_PRECISION_DESERT_SANDBOX_VISUAL_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
echo "log_dir=$LOG_DIR" | tee -a "$LOG_DIR/run_summary.txt"
