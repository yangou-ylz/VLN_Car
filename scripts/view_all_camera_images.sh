#!/usr/bin/env bash

# 打开 Topgear 四路相机 rqt_image_view。
# 前提：Unity 已点击 Play，ROS-TCP-Endpoint 已启动，四路相机 topic 正在发布。

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

declare -a TOPICS=(
  "/vln/front/image_raw"
  "/vln/rear/image_raw"
  "/vln/left/image_raw"
  "/vln/right/image_raw"
)

echo "打开四路 rqt_image_view。"
echo "如果窗口为空，先确认 endpoint 已启动、Unity 对应场景已点击 Play。"

pids=()
for topic in "${TOPICS[@]}"; do
  echo "打开 rqt_image_view，topic=${topic}"
  if command -v rqt_image_view >/dev/null 2>&1; then
    rqt_image_view "$topic" &
  else
    ros2 run rqt_image_view rqt_image_view "$topic" &
  fi
  pids+=("$!")
  sleep 0.35
done

wait "${pids[@]}"
