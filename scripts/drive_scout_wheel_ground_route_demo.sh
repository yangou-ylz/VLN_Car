#!/usr/bin/env bash

# 手工演示：在 Unity wheel-ground 场景已经 Play、ROS-TCP-Endpoint 已启动时，发布固定路线 /vln/cmd_vel。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
WORKSPACE="$VLN_ROOT/unity_ros2_ws"

mkdir -p "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env
else
  source /opt/ros/humble/setup.bash
fi

source "$WORKSPACE/install/setup.bash"

exec python3 "$VLN_ROOT/scripts/ros2_drive_scout_physics_route.py" \
  --progress-only-gates \
  --skip-stalled-waypoints \
  --skip-angular-calibration \
  --angular-sign -1 \
  --max-angular 0.42 \
  --angular-gain 0.62 \
  --max-linear 1.35 \
  --linear-gain 0.75 \
  --linear-accel 0.95 \
  --angular-accel 0.28 \
  --min-linear-while-turning 0.65 \
  --stall-skip-seconds 7.0 \
  --stall-skip-forward-margin 4.0 \
  "$@"
