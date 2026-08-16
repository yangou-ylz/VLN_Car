#!/usr/bin/env bash

# 启动 Unity ROS-TCP-Endpoint。
# 默认只监听本机 127.0.0.1:10000；如果 Unity 在同一台电脑上运行，用默认值即可。
# 如果 Unity 在另一台机器上，需要显式传入可被 Unity 访问的 ROS_IP。

set -eo pipefail

ROS_IP_VALUE="${ROS_IP:-127.0.0.1}"
ROS_TCP_PORT_VALUE="${ROS_TCP_PORT:-10000}"
WORKSPACE="${UNITY_ROS2_WS:-/home/ubuntu22/VLN/unity_ros2_ws}"
VLN_ROOT="/home/ubuntu22/VLN"

mkdir -p "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

if [ ! -f "$WORKSPACE/install/setup.bash" ]; then
  echo "未找到 $WORKSPACE/install/setup.bash，请先构建 ROS-TCP-Endpoint 工作区。"
  exit 1
fi

source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env
else
  source /opt/ros/humble/setup.bash
fi

source "$WORKSPACE/install/setup.bash"

ENDPOINT_BIN="$WORKSPACE/install/ros_tcp_endpoint/lib/ros_tcp_endpoint/default_server_endpoint"

if [ ! -x "$ENDPOINT_BIN" ]; then
  echo "未找到可执行文件 $ENDPOINT_BIN，请重新构建 ROS-TCP-Endpoint。"
  exit 1
fi

echo "启动 ROS-TCP-Endpoint：${ROS_IP_VALUE}:${ROS_TCP_PORT_VALUE}"
exec "$ENDPOINT_BIN" --ros-args \
  -p ROS_IP:="${ROS_IP_VALUE}" \
  -p ROS_TCP_PORT:="${ROS_TCP_PORT_VALUE}"
