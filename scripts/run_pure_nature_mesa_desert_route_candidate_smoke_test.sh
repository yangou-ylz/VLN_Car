#!/usr/bin/env bash

# 阶段 21：Pure Nature 2 Mesa Desert 路线候选场景构建与视觉验收。
# 只在大资产副本工程内派生 VLN 自有场景并截图，不启动 ROS2，不覆盖旧主工程。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
RUN_ID="vln_pure_nature_mesa_desert_route_candidate_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
UNITY_LOG="$LOG_DIR/unity.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_pure_nature_mesa_desert_route_candidate_result.txt"

mkdir -p "$LOG_DIR"

echo "阶段 21 Mesa 路线候选场景：派生 VLN 场景，增加不规则石块/植被障碍，只做视觉验收。" | tee "$LOG_DIR/run_summary.txt"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee -a "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前大资产副本 Unity Editor，再运行该自动截图脚本。"
  exit 2
fi

if [ -f "$RESULT_FILE" ]; then
  mv "$RESULT_FILE" "$LOG_DIR/previous_$(basename "$RESULT_FILE")"
fi

set +e
timeout 320s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnPureNatureMesaDesertRouteCandidateBuilder.RunBuildAndSmokeTest \
  -logFile "$UNITY_LOG"
unity_status=$?
set -e

if [ "$unity_status" -ne 0 ]; then
  echo "unity_status=$unity_status" | tee -a "$LOG_DIR/run_summary.txt"
  tail -n 180 "$UNITY_LOG" || true
  exit "$unity_status"
fi

if [ ! -f "$RESULT_FILE" ]; then
  echo "missing_result_file=$RESULT_FILE" | tee -a "$LOG_DIR/run_summary.txt"
  tail -n 180 "$UNITY_LOG" || true
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

cat "$RESULT_FILE" | tee -a "$LOG_DIR/run_summary.txt"

if ! grep -q '^success=1$' "$RESULT_FILE"; then
  echo "result_not_success" | tee -a "$LOG_DIR/run_summary.txt"
  exit 1
fi

echo "VLN_PURE_NATURE_MESA_DESERT_ROUTE_CANDIDATE_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
echo "log_dir=$LOG_DIR" | tee -a "$LOG_DIR/run_summary.txt"
