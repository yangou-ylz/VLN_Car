#!/usr/bin/env bash

# 手工演示：在 Unity wheel-ground 场景已经 Play、ROS-TCP-Endpoint 已启动时，发布固定路线 /vln/cmd_vel。
# 不自动打开 Unity，不做 batch 回归；用户看效果时优先用这个入口。

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
WORKSPACE="$VLN_ROOT/unity_ros2_ws"

mkdir -p "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

source "$HOME/.bashrc" >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env
else
  source /opt/ros/humble/setup.bash
fi

source "$WORKSPACE/install/setup.bash"

echo "手工演示入口：请确认 Unity 已打开目标场景、endpoint 已启动、Unity 已点击 Play。"

exec python3 "$VLN_ROOT/scripts/ros2_drive_scout_physics_route.py" \
  --centerline-corridor \
  --centerline-forward-max 22.8 \
  --progress-only-gates \
  --skip-angular-calibration \
  --angular-sign 1 \
  --lookahead-distance 5.00 \
  --corridor-lateral-gain 0.28 \
  --corridor-max-heading-correction 0.32 \
  --max-angular 0.55 \
  --angular-gain 0.70 \
  --max-linear 1.05 \
  --linear-gain 0.62 \
  --linear-accel 0.70 \
  --angular-accel 0.30 \
  --min-linear-while-turning 0.38 \
  --goal-tolerance 1.60 \
  --gate-tolerance 0.95 \
  --max-lateral-offset 1.15 \
  --max-final-lateral-offset 0.80 \
  --max-bridge-lateral-offset 0.85 \
  --bridge-forward-min 9.5 \
  --bridge-forward-max 22.8 \
  --min-reached 13 \
  --stall-skip-seconds 12.0 \
  --stall-skip-forward-margin 4.0 \
  "$@"
