#!/usr/bin/env bash

# 检查当前运行中的 /vln/lidar/points 真实 ROS2 发布频率。
# 用途：区分 RViz 渲染帧率和 LiDAR topic 实际频率。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
DURATION="${VLN_LIDAR_RATE_DURATION:-6}"
MIN_HZ="${VLN_LIDAR_RATE_MIN_HZ:-1}"

source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source "$VLN_ROOT/unity_ros2_ws/install/setup.bash"

echo "检查 /vln/lidar/points 当前真实 ROS2 发布频率。"
echo "前提：Unity 已打开 mesa_topgear，ROS-TCP-Endpoint 已启动，Unity 已点击 Play。"

python3 "$VLN_ROOT/scripts/ros2_measure_topic_frequency.py" \
  --topic /vln/lidar/points \
  --msg-type pointcloud2 \
  --duration "$DURATION" \
  --timeout 10 \
  --min-hz "$MIN_HZ" \
  --frame-id lidar_link
