#!/usr/bin/env bash

# 手工演示：在 Unity wheel-ground 场景已经 Play、ROS-TCP-Endpoint 已启动时，发布后段挑战路线 /vln/cmd_vel。
# 这个脚本不自动打开 Unity，不做 batch 验收；用于用户在 Unity 界面里肉眼看草地、青石路、沙地和低矮障碍交互。

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
WORKSPACE="$VLN_ROOT/unity_ros2_ws"
CHALLENGE_ROUTE="4.0,0.0;8.0,0.0;12.0,0.0;15.0,0.0;18.0,0.0;22.0,0.0;26.0,0.0;28.0,0.0;30.0,0.0;34.0,0.0;42.0,0.0;50.0,0.0;54.0,0.0;60.0,0.0;66.0,0.0;72.0,0.0"

mkdir -p "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

source "$HOME/.bashrc" >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env
else
  source /opt/ros/humble/setup.bash
fi

source "$WORKSPACE/install/setup.bash"

echo "手工挑战路线入口：请确认 Unity 已打开目标场景、endpoint 已启动、Unity 已点击 Play。"

exec python3 "$VLN_ROOT/scripts/ros2_drive_scout_physics_route.py" \
  --relative-waypoints "$CHALLENGE_ROUTE" \
  --centerline-corridor \
  --centerline-forward-max 74.0 \
  --progress-only-gates \
  --skip-angular-calibration \
  --angular-sign 1 \
  --lookahead-distance 5.00 \
  --corridor-lateral-gain 0.30 \
  --corridor-max-heading-correction 0.34 \
  --max-angular 0.55 \
  --angular-gain 0.72 \
  --max-linear 0.98 \
  --linear-gain 0.62 \
  --linear-accel 0.68 \
  --angular-accel 0.30 \
  --min-linear-while-turning 0.38 \
  --goal-tolerance 1.60 \
  --gate-tolerance 0.95 \
  --max-lateral-offset 1.20 \
  --max-final-lateral-offset 0.90 \
  --max-bridge-lateral-offset 0.85 \
  --bridge-forward-min 9.5 \
  --bridge-forward-max 22.8 \
  --stall-skip-seconds 12.0 \
  --stall-skip-forward-margin 4.0 \
  --min-reached 16 \
  --min-total-progress 67.0 \
  --timeout 230 \
  "$@"
