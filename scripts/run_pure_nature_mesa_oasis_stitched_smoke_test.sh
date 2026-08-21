#!/usr/bin/env bash

# 阶段 21：Pure Nature 2 Mesa + Oasis 拼接场景构建与视觉验收。
# 默认不启动 ROS2，不覆盖旧主工程。
# 如果已通过“VLN -> 更改世界模型 -> 保存本次世界”手工保存，本脚本只打开已保存场景做只读截图检查，不重建覆盖。
# 只有显式设置 VLN_FORCE_REBUILD_STITCHED_WORLD=1 时，才允许覆盖手工保存场景并清除保存记录。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
RUN_ID="vln_pure_nature_mesa_oasis_stitched_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
UNITY_LOG="$LOG_DIR/unity.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_pure_nature_mesa_oasis_stitched_result.txt"

mkdir -p "$LOG_DIR"

echo "阶段 21 Mesa + Oasis 拼接场景：按平坦沙地边界拼接两张完整地图，只做视觉验收；手工保存世界存在时不重建覆盖。" | tee "$LOG_DIR/run_summary.txt"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee -a "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前大资产副本 Unity Editor，再运行该自动截图脚本。"
  exit 2
fi

if [ -f "$RESULT_FILE" ]; then
  mv "$RESULT_FILE" "$LOG_DIR/previous_$(basename "$RESULT_FILE")"
fi

set +e
timeout 420s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnPureNatureMesaOasisStitchBuilder.RunBuildAndSmokeTest \
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

echo "VLN_PURE_NATURE_MESA_OASIS_STITCHED_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
echo "log_dir=$LOG_DIR" | tee -a "$LOG_DIR/run_summary.txt"
