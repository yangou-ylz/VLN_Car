#!/usr/bin/env bash

# 用固定配置打开 RViz2，显示 /vln/lidar/points 点云。
# 当前最小闭环还没有正式 TF 树，因此本脚本会临时发布 map -> lidar_link 静态 TF。

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
RVIZ_CONFIG="$VLN_ROOT/config/vln_lidar_pointcloud.rviz"
tf_pid=""

mkdir -p "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

cleanup()
{
  if [ -n "$tf_pid" ]; then
    kill "$tf_pid" >/dev/null 2>&1 || true
    wait "$tf_pid" >/dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

source "$HOME/.bashrc" >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source "$VLN_ROOT/unity_ros2_ws/install/setup.bash"

ros2 run tf2_ros static_transform_publisher \
  --frame-id map \
  --child-frame-id lidar_link \
  >/tmp/vln_lidar_static_tf.log 2>&1 &
tf_pid=$!

echo "打开 RViz2 LiDAR 配置：Fixed Frame=map，PointCloud2=/vln/lidar/points，并临时发布 map -> lidar_link 静态 TF"
echo "如果只看到网格，请确认 endpoint 已启动、Unity 的 LiDAR 场景已点击 Play。"

rviz2 -d "$RVIZ_CONFIG"
