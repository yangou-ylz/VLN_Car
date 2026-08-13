#!/usr/bin/env bash

# 记录阶段 8 标准感知输出的小 rosbag 样本。
# 使用前提：endpoint 已启动，Unity 越野场景正在 Play。

set -eo pipefail

DURATION_SECONDS="${1:-8}"
BAG_ROOT="/home/ubuntu22/VLN/VLN_BAGS"
RUN_ID="vln_sensor_sample_$(date +%Y%m%d_%H%M%S)"
BAG_DIR="$BAG_ROOT/$RUN_ID"
LOG_DIR="/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/rosbag_samples/$RUN_ID"

IMAGE_TOPIC="/vln/front/image_raw"
CAMERA_INFO_TOPIC="/vln/front/camera_info"
POINTS_TOPIC="/vln/lidar/points"
TF_TOPIC="/tf"

mkdir -p "$BAG_ROOT" "$LOG_DIR"

source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash

echo "准备记录 rosbag：$BAG_DIR"
echo "记录时长：${DURATION_SECONDS}s"
echo "topic：$IMAGE_TOPIC $CAMERA_INFO_TOPIC $POINTS_TOPIC $TF_TOPIC"

topic_list="$(timeout 8s ros2 topic list -t 2>/dev/null || true)"
printf '%s\n' "$topic_list" | tee "$LOG_DIR/topic_list_before_record.log"

for required_topic in \
  "$IMAGE_TOPIC [sensor_msgs/msg/Image]" \
  "$CAMERA_INFO_TOPIC [sensor_msgs/msg/CameraInfo]" \
  "$POINTS_TOPIC [sensor_msgs/msg/PointCloud2]" \
  "$TF_TOPIC [tf2_msgs/msg/TFMessage]"; do
  if ! printf '%s\n' "$topic_list" | grep -F "$required_topic" >/dev/null 2>&1; then
    echo "缺少 topic，暂不记录 rosbag：$required_topic" | tee "$LOG_DIR/error.log"
    echo "请先启动 endpoint、打开 Unity 越野场景并点击 Play。"
    exit 1
  fi
done

set +e
timeout --signal=SIGINT --kill-after=5s "${DURATION_SECONDS}s" \
  ros2 bag record \
    -o "$BAG_DIR" \
    "$IMAGE_TOPIC" \
    "$CAMERA_INFO_TOPIC" \
    "$POINTS_TOPIC" \
    "$TF_TOPIC" \
  >"$LOG_DIR/rosbag_record.log" 2>&1
record_status=$?
set -e

if [ "$record_status" -ne 0 ] && [ "$record_status" -ne 124 ] && [ "$record_status" -ne 130 ]; then
  echo "rosbag record 异常退出，status=$record_status" | tee -a "$LOG_DIR/error.log"
  sed -n '1,160p' "$LOG_DIR/rosbag_record.log" || true
  exit 1
fi

if [ ! -f "$BAG_DIR/metadata.yaml" ]; then
  echo "未生成 metadata.yaml：$BAG_DIR" | tee -a "$LOG_DIR/error.log"
  sed -n '1,160p' "$LOG_DIR/rosbag_record.log" || true
  exit 1
fi

ros2 bag info "$BAG_DIR" >"$LOG_DIR/rosbag_info.log" 2>&1
sed -n '1,220p' "$LOG_DIR/rosbag_info.log"

for topic_name in "$IMAGE_TOPIC" "$CAMERA_INFO_TOPIC" "$POINTS_TOPIC" "$TF_TOPIC"; do
  if ! grep -F "$topic_name" "$LOG_DIR/rosbag_info.log" >/dev/null 2>&1; then
    echo "rosbag info 中缺少 topic：$topic_name" | tee -a "$LOG_DIR/error.log"
    exit 1
  fi
done

cat >"$LOG_DIR/run_summary.txt" <<EOF
run_id=$RUN_ID
bag_dir=$BAG_DIR
duration_seconds=$DURATION_SECONDS
record_status=$record_status
EOF

if [ "$record_status" -eq 124 ] || [ "$record_status" -eq 130 ]; then
  echo "record_stop_reason=expected_timeout_stop_after_${DURATION_SECONDS}s" >>"$LOG_DIR/run_summary.txt"
else
  echo "record_stop_reason=process_exit" >>"$LOG_DIR/run_summary.txt"
fi

cat >>"$LOG_DIR/run_summary.txt" <<EOF
image_topic=$IMAGE_TOPIC
camera_info_topic=$CAMERA_INFO_TOPIC
points_topic=$POINTS_TOPIC
tf_topic=$TF_TOPIC
EOF

echo "VLN_ROSBAG_SAMPLE_RECORD_PASS"
