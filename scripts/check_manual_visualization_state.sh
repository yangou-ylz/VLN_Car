#!/usr/bin/env bash

# 检查手工可视化前的关键状态：endpoint、Unity topic、ROS2 工具入口。

set -eo pipefail

source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash

echo "== ROS2 工具入口 =="
echo "rviz2: $(command -v rviz2 || echo 未找到)"
echo "rqt: $(command -v rqt || echo 未找到)"
echo "rqt_image_view 独立命令: $(command -v rqt_image_view || echo 未找到，使用 ros2 run rqt_image_view rqt_image_view)"
ros2 pkg executables rqt_image_view || true

echo
echo "== ROS-TCP-Endpoint 端口 =="
if ss -ltnp 2>/dev/null | grep -E ':10000\b'; then
  echo "endpoint 正在监听 10000，Unity 可以连接。"
else
  echo "未发现 10000 端口监听：请先运行 /home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh"
fi

echo
echo "== 当前 /vln topic =="
topic_list="$(timeout 5s ros2 topic list -t 2>/dev/null || true)"
if printf '%s\n' "$topic_list" | grep -E '/vln/(front|lidar)'; then
  echo "已发现 UnitySensors topic。"
else
  echo "没有发现 /vln/front 或 /vln/lidar topic：通常是 Unity 没有点击 Play，或 endpoint 没开。"
fi

echo
echo "== LiDAR 点云实时帧 =="
if printf '%s\n' "$topic_list" | grep -F '/vln/lidar/points [sensor_msgs/msg/PointCloud2]' >/dev/null 2>&1; then
  echo "发现 /vln/lidar/points topic，继续等待一帧 PointCloud2 消息..."
  if timeout 8s python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py \
      --topic /vln/lidar/points \
      --width 7200 \
      --point-step 16 \
      --frame-id lidar_link \
      --timeout 6 \
      --min-nonzero-points 20; then
    echo "LiDAR 正在实时发布有效点云帧。"
  else
    echo "未收到有效点云帧：RViz 即使显示 Status: OK，也可能只知道 topic 存在，当前没有真实点云数据。"
    echo "请确认 Unity 当前打开的是 LiDAR 测试场景，并且顶部 Play 按钮是蓝色。"
  fi
else
  echo "未发现 /vln/lidar/points。若要看点云，请打开 LiDAR 场景并点击 Play。"
fi

echo
echo "== 当前 TF topic =="
if printf '%s\n' "$topic_list" | grep -E '^/tf_static '; then
  echo "已发现 /tf_static。RViz 固定坐标系通常可用。"
else
  echo "未发现 /tf_static：这不是报错，普通检查脚本不会主动发布 TF。"
  echo "看点云时请用 /home/ubuntu22/VLN/scripts/view_lidar_rviz.sh，它会临时发布 map -> lidar_link 静态 TF。"
fi

echo
echo "== 建议下一步 =="
echo "1. endpoint 未开：运行 /home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh"
echo "2. Unity 场景已打开但无 topic：点击 Unity 顶部 Play，按钮应变蓝"
echo "3. 看图像：/home/ubuntu22/VLN/scripts/view_front_image.sh"
echo "4. 看点云：/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh"
