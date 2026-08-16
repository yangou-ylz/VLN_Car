#!/usr/bin/env bash

# 启动 VLN 本地中文控制面板。
# 前提：ROS-TCP-Endpoint 已启动，Unity 主场景已点击 Play。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"

mkdir -p "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source "$VLN_ROOT/unity_ros2_ws/install/setup.bash"

exec python3 "$VLN_ROOT/scripts/vln_control_panel.py" "$@"
