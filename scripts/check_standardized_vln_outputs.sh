#!/usr/bin/env bash

# 阶段 8 手工检查：验证正式输出 topic、消息字段和 TF 树。
# 使用前提：ROS-TCP-Endpoint 已启动，Unity 越野场景已点击 Play。

set -eo pipefail

IMAGE_TOPIC="/vln/front/image_raw"
CAMERA_INFO_TOPIC="/vln/front/camera_info"
POINTS_TOPIC="/vln/lidar/points"
TF_TOPIC="/tf"
LOG_ROOT="/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/manual_standardized_checks"
RUN_ID="vln_standardized_check_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$LOG_ROOT/$RUN_ID"

mkdir -p "$LOG_DIR" "/home/ubuntu22/VLN/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-/home/ubuntu22/VLN/.ros/log}"

source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash

echo "== 阶段 8 标准输出检查 =="
echo "log_dir=$LOG_DIR"

echo
echo "== endpoint 端口 =="
if ss -ltnp 2>/dev/null | grep -E ':10000\b' | tee "$LOG_DIR/endpoint_port.log"; then
  echo "endpoint 正在监听 10000。"
else
  echo "未发现 endpoint 监听 10000：请先运行 /home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh" | tee "$LOG_DIR/error.log"
  exit 1
fi

echo
echo "== topic 类型 =="
topic_list="$(timeout 8s ros2 topic list -t 2>/dev/null || true)"
printf '%s\n' "$topic_list" | tee "$LOG_DIR/topic_list.log"

required_topics=(
  "$IMAGE_TOPIC [sensor_msgs/msg/Image]"
  "$CAMERA_INFO_TOPIC [sensor_msgs/msg/CameraInfo]"
  "$POINTS_TOPIC [sensor_msgs/msg/PointCloud2]"
  "$TF_TOPIC [tf2_msgs/msg/TFMessage]"
)

for required_topic in "${required_topics[@]}"; do
  if ! printf '%s\n' "$topic_list" | grep -F "$required_topic" >/dev/null 2>&1; then
    echo "缺少标准 topic：$required_topic" | tee -a "$LOG_DIR/error.log"
    echo "常见原因：Unity 场景没有点击 Play，或当前打开的不是 VLNOffroadTerrainSmokeTest 场景。"
    exit 1
  fi
done

echo
echo "== 图像消息 =="
python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py \
  --topic "$IMAGE_TOPIC" \
  --width 640 \
  --height 480 \
  --encoding rgb8 \
  --frame-id front_camera_optical_frame \
  --timeout 15 | tee "$LOG_DIR/image_once.log"

echo
echo "== CameraInfo 消息 =="
python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_camera_info_once.py \
  --topic "$CAMERA_INFO_TOPIC" \
  --width 640 \
  --height 480 \
  --frame-id front_camera_optical_frame \
  --timeout 15 | tee "$LOG_DIR/camera_info_once.log"

echo
echo "== LiDAR PointCloud2 消息 =="
python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py \
  --topic "$POINTS_TOPIC" \
  --width 7200 \
  --point-step 16 \
  --frame-id lidar_link \
  --timeout 15 \
  --min-nonzero-points 20 | tee "$LOG_DIR/pointcloud2_once.log"

echo
echo "== 车辆 TF 树 / 默认静止 =="
python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_vehicle_tf.py \
  --topic "$TF_TOPIC" \
  --timeout 15 \
  --min-base-delta 0.0 \
  --max-base-delta 0.05 \
  --stable-observe-seconds 6.0 | tee "$LOG_DIR/vehicle_tf.log"

echo
echo "== 频率快照 =="
set +e
timeout 8s ros2 topic hz "$IMAGE_TOPIC" >"$LOG_DIR/image_hz.log" 2>&1
image_hz_status=$?
timeout 8s ros2 topic hz "$POINTS_TOPIC" >"$LOG_DIR/pointcloud2_hz.log" 2>&1
points_hz_status=$?
set -e

sed -n '1,80p' "$LOG_DIR/image_hz.log" || true
sed -n '1,80p' "$LOG_DIR/pointcloud2_hz.log" || true

if ! grep -q 'average rate:' "$LOG_DIR/image_hz.log"; then
  echo "图像频率未采到 average rate，status=$image_hz_status" | tee -a "$LOG_DIR/error.log"
  exit 1
fi

if ! grep -q 'average rate:' "$LOG_DIR/pointcloud2_hz.log"; then
  echo "点云频率未采到 average rate，status=$points_hz_status" | tee -a "$LOG_DIR/error.log"
  exit 1
fi

cat >"$LOG_DIR/standard_outputs_manifest.txt" <<EOF
run_id=$RUN_ID
image_topic=$IMAGE_TOPIC
camera_info_topic=$CAMERA_INFO_TOPIC
points_topic=$POINTS_TOPIC
tf_topic=$TF_TOPIC
fixed_frame=map
base_frame=base_link
camera_frame=front_camera_optical_frame
lidar_frame=lidar_link
image_resolution=640x480
image_encoding=rgb8
lidar_scan_pattern=VLP-16
lidar_points_per_message=7200
lidar_point_step=16
expected_frequency_hz=5
EOF

echo
echo "VLN_STANDARDIZED_OUTPUTS_CHECK_PASS"
