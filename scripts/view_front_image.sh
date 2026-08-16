#!/usr/bin/env bash

# 打开前向相机图像查看器。
# 当前机器有 rqt_image_view 包，但没有独立 rqt_image_view 命令，因此优先走 ros2 run。

set -eo pipefail

TOPIC="${1:-/vln/front/image_raw}"

mkdir -p /home/ubuntu22/VLN/.ros/log
export ROS_LOG_DIR="${ROS_LOG_DIR:-/home/ubuntu22/VLN/.ros/log}"

source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash

echo "打开 rqt_image_view，topic=${TOPIC}"
echo "如果窗口为空，先确认 endpoint 已启动、Unity 对应场景已点击 Play。"

if command -v rqt_image_view >/dev/null 2>&1; then
  exec rqt_image_view "$TOPIC"
fi

exec ros2 run rqt_image_view rqt_image_view "$TOPIC"
