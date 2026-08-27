#!/usr/bin/env bash

# 本地键盘速度控制入口：绕过浏览器控制面板，直接发布 /vln/cmd_vel。
# 前提：ROS-TCP-Endpoint 已启动，Unity 已打开 mesa_topgear 场景并点击 Play。

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
WORKSPACE="$VLN_ROOT/unity_ros2_ws"

mkdir -p "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

source "$HOME/.bashrc" >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source "$WORKSPACE/install/setup.bash"

echo "本地键盘速度控制：请确认 Unity 已打开 mesa_topgear、endpoint 已启动、Unity 已点击 Play。"
echo "按键：↑/W 前进，↓/S 后退，←/A 左转，→/D 右转；松开即停，空格停车，Q 退出。"

exec python3 "$VLN_ROOT/scripts/local_keyboard_cmd_vel_control.py" "$@"
