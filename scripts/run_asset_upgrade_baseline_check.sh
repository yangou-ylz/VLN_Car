#!/usr/bin/env bash

# 成熟地图 / 小车模型导入前后的基线回归。
# 用途：每次导入候选资产前后，确认候选场景、标准输出、cmd_vel 控制和中文控制面板都没被破坏。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
RUN_ID="vln_asset_baseline_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

echo "run_id=$RUN_ID" | tee "$LOG_DIR/run_summary.txt"
echo "log_dir=$LOG_DIR" | tee -a "$LOG_DIR/run_summary.txt"

echo "== 1/5 地图资产候选场景回归 =="
"$VLN_ROOT/scripts/run_offroad_asset_candidate_smoke_test.sh" 2>&1 | tee "$LOG_DIR/asset_candidate.log"

echo "== 2/5 真实小车视觉候选场景回归 =="
"$VLN_ROOT/scripts/run_offroad_vehicle_candidate_smoke_test.sh" 2>&1 | tee "$LOG_DIR/vehicle_candidate.log"

echo "== 3/5 标准输出回归 =="
"$VLN_ROOT/scripts/run_standardized_outputs_smoke_test.sh" 2>&1 | tee "$LOG_DIR/standardized_outputs.log"

echo "== 4/5 cmd_vel 控制回归 =="
"$VLN_ROOT/scripts/run_cmd_vel_control_smoke_test.sh" 2>&1 | tee "$LOG_DIR/cmd_vel_control.log"

echo "== 5/5 中文控制面板回归 =="
"$VLN_ROOT/scripts/run_control_panel_smoke_test.sh" 2>&1 | tee "$LOG_DIR/control_panel.log"

if ! grep -q 'VLN_OFFROAD_ASSET_CANDIDATE_SMOKE_TEST_PASS' "$LOG_DIR/asset_candidate.log"; then
  echo "asset_baseline_candidate_scene_failed"
  exit 1
fi

if ! grep -q 'VLN_OFFROAD_VEHICLE_CANDIDATE_SMOKE_TEST_PASS' "$LOG_DIR/vehicle_candidate.log"; then
  echo "asset_baseline_vehicle_candidate_scene_failed"
  exit 1
fi

if ! grep -q 'VLN_STANDARDIZED_OUTPUTS_SMOKE_TEST_PASS' "$LOG_DIR/standardized_outputs.log"; then
  echo "asset_baseline_standardized_outputs_failed"
  exit 1
fi

if ! grep -q 'VLN_CMD_VEL_CONTROL_SMOKE_TEST_PASS' "$LOG_DIR/cmd_vel_control.log"; then
  echo "asset_baseline_cmd_vel_failed"
  exit 1
fi

if ! grep -q 'VLN_CONTROL_PANEL_SMOKE_TEST_PASS' "$LOG_DIR/control_panel.log"; then
  echo "asset_baseline_control_panel_failed"
  exit 1
fi

echo "VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS"
