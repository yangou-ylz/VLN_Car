#!/usr/bin/env bash

# 阶段 21 前置只读检查：确认阶段 20 Topgear 完美基线和锁定恢复点存在。
# 本脚本不启动 Unity、不运行 ROS2、不下载资产、不修改任何文件。

set -euo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
MAIN_SCENE="$UNITY_PROJECT/Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity"
LOCKED_PARENT_POSE="$VLN_ROOT/config/topgear_sensor_pose_user_locked.json"
LOCKED_HIERARCHY="$VLN_ROOT/config/topgear_sensor_hierarchy_user_locked.json"
LOCKED_SCENE="$VLN_ROOT/config/topgear_sensor_scene_locked/VLNOffroadScoutWheelGroundCandidate_user_locked.unity"
CURRENT_STATE="$VLN_ROOT/CURRENT_STATE.md"
WORKFLOW_DOC="$VLN_ROOT/docs/high_precision_desert_workflow.md"

failures=0

check_file() {
  local label="$1"
  local path="$2"
  if [ -f "$path" ]; then
    printf 'OK   %-34s %s\n' "$label" "$path"
  else
    printf 'MISS %-34s %s\n' "$label" "$path"
    failures=$((failures + 1))
  fi
}

check_text() {
  local label="$1"
  local pattern="$2"
  local path="$3"
  if grep -F -- "$pattern" "$path" >/dev/null 2>&1; then
    printf 'OK   %-34s %s\n' "$label" "$pattern"
  else
    printf 'MISS %-34s %s\n' "$label" "$pattern"
    failures=$((failures + 1))
  fi
}

echo "== 阶段 21 前置基线检查：只读 =="
check_file "Unity 主场景" "$MAIN_SCENE"
check_file "Topgear 父位姿锁定" "$LOCKED_PARENT_POSE"
check_file "Topgear 层级锁定" "$LOCKED_HIERARCHY"
check_file "Topgear 整场景锁定" "$LOCKED_SCENE"
check_file "当前状态文档" "$CURRENT_STATE"
check_file "阶段 21 工作流" "$WORKFLOW_DOC"

echo
echo "== 文档基线关键词 =="
check_text "Topgear 传感器最近通过" "vln_topgear_sensor_suite_20260821_141404" "$CURRENT_STATE"
check_text "13 点金标准路线" "vln_scout_wheel_ground_route_20260820_190253" "$CURRENT_STATE"
check_text "锁定场景恢复脚本" "restore_topgear_sensor_locked_scene.sh" "$CURRENT_STATE"
check_text "阶段 21 不覆盖主场景" "不覆盖" "$WORKFLOW_DOC"

echo
echo "== 下载预算 =="
echo "download_budget_gb_limit=40"
echo "downloaded_this_phase_gb=0"

if [ "$failures" -ne 0 ]; then
  echo "VLN_HIGH_PRECISION_DESERT_PHASE0_BASELINE_FAIL failures=$failures"
  exit 1
fi

echo "VLN_HIGH_PRECISION_DESERT_PHASE0_BASELINE_OK"
