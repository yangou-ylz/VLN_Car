#!/usr/bin/env bash

# 使用阶段 7/8 的正式 TF 树打开 RViz2。
# 前提：endpoint 已启动，Unity 越野场景正在 Play，并且 Unity 正在发布 /tf。

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
RVIZ_CONFIG="$VLN_ROOT/config/vln_vehicle_sensors.rviz"

mkdir -p "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

source "$HOME/.bashrc" >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source "$VLN_ROOT/unity_ros2_ws/install/setup.bash"

topic_list="$(timeout 5s ros2 topic list -t 2>/dev/null || true)"

echo "打开 RViz2 标准配置：Fixed Frame=map，PointCloud2=/vln/lidar/points，TF=/tf。"
echo "本脚本不再临时发布 map -> lidar_link；它依赖 Unity 正式发布 map -> base_link -> lidar_link。"

if ! printf '%s\n' "$topic_list" | grep -F '/tf [tf2_msgs/msg/TFMessage]' >/dev/null 2>&1; then
  echo "提示：当前还没发现 /tf。请确认 Unity 场景已点击 Play，且使用阶段 7/8 越野场景。"
fi

if ! printf '%s\n' "$topic_list" | grep -F '/vln/lidar/points [sensor_msgs/msg/PointCloud2]' >/dev/null 2>&1; then
  echo "提示：当前还没发现 /vln/lidar/points。RViz 打开后可能暂时只有网格。"
fi

exec rviz2 -d "$RVIZ_CONFIG"
