#!/usr/bin/env bash

# Topgear V2 上装视觉对齐专项验收。
# 只打开现有 Unity 主场景生成多视角截图，不启动 ROS2 endpoint，不跑路线，不改物理链路。
# 注意：本脚本禁止重建主场景，避免覆盖用户在 Unity Editor 中手动保存的 Topgear 传感器位姿。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_topgear_visual_alignment_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
UNITY_LOG="$LOG_DIR/unity.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_result.txt"

mkdir -p "$LOG_DIR"

echo "Topgear 上装视觉专项验收：只 batch 打开现有 Unity 主场景生成截图，不启动 ROS2，不重建场景。"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前 Unity Editor，再运行本自动视觉验收脚本。"
  exit 2
fi

if find "$UNITY_PROJECT/Library" -maxdepth 1 -type f \( -name '*lock*' -o -name '*Lock*' \) 2>/dev/null | grep -q .; then
  echo "发现 Unity stale lock，先用项目恢复脚本移动 lock。" | tee "$LOG_DIR/run_summary.txt"
  "$VLN_ROOT/scripts/stop_unity_vln_project.sh" | tee -a "$LOG_DIR/run_summary.txt"
fi

for old_file in \
  "$RESULT_FILE" \
  "$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_front.png" \
  "$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_rear.png" \
  "$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_left.png" \
  "$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_right.png" \
  "$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_top.png"; do
  if [ -f "$old_file" ]; then
    mv "$old_file" "$LOG_DIR/previous_$(basename "$old_file")"
  fi
done

set +e
timeout 150s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadScoutWheelGroundTopgearVisualSmokeTestRunner.RunExistingScene \
  -logFile "$UNITY_LOG"
unity_status=$?
set -e

if [ -f "$RESULT_FILE" ]; then
  cp "$RESULT_FILE" "$LOG_DIR/$(basename "$RESULT_FILE")"
fi

for screenshot in \
  vln_offroad_scout_wheel_ground_topgear_visual_front.png \
  vln_offroad_scout_wheel_ground_topgear_visual_rear.png \
  vln_offroad_scout_wheel_ground_topgear_visual_left.png \
  vln_offroad_scout_wheel_ground_topgear_visual_right.png \
  vln_offroad_scout_wheel_ground_topgear_visual_top.png; do
  if [ -f "$UNITY_PROJECT/Logs/$screenshot" ]; then
    cp "$UNITY_PROJECT/Logs/$screenshot" "$LOG_DIR/$screenshot"
  fi
done

topgear_visual_present=$(grep -E '^topgear_visual_present=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_visual_collider_count=$(grep -E '^topgear_visual_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_visual_rigidbody_count=$(grep -E '^topgear_visual_rigidbody_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
success=$(grep -E '^success=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "log_dir=$LOG_DIR"
  echo "result_file=$RESULT_FILE"
  echo "topgear_visual_present=${topgear_visual_present:-0}"
  echo "topgear_visual_collider_count=${topgear_visual_collider_count:-missing}"
  echo "topgear_visual_rigidbody_count=${topgear_visual_rigidbody_count:-missing}"
  echo "front_screenshot=$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_front.png"
  echo "rear_screenshot=$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_rear.png"
  echo "left_screenshot=$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_left.png"
  echo "right_screenshot=$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_right.png"
  echo "top_screenshot=$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_top.png"
} | tee -a "$LOG_DIR/run_summary.txt"

if [ "$unity_status" -ne 0 ]; then
  tail -n 120 "$UNITY_LOG" || true
  exit 1
fi

if [ "${success:-0}" -ne 1 ]; then
  echo "topgear_visual_alignment_failed"
  cat "$RESULT_FILE" 2>/dev/null || true
  exit 1
fi

if [ "${topgear_visual_present:-0}" -ne 1 ]; then
  echo "topgear_visual_missing"
  exit 1
fi

if [ "${topgear_visual_collider_count:-1}" -ne 0 ]; then
  echo "topgear_visual_must_not_add_colliders"
  exit 1
fi

if [ "${topgear_visual_rigidbody_count:-1}" -ne 0 ]; then
  echo "topgear_visual_must_not_add_rigidbodies"
  exit 1
fi

echo "VLN_TOPGEAR_VISUAL_ALIGNMENT_SMOKE_TEST_PASS"
