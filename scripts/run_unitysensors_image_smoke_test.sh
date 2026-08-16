#!/usr/bin/env bash

# 运行 UnitySensors RGB 相机 -> ROS2 Image 最小闭环测试。
# 验收内容：ROS2 收到 /vln/front/image_raw，类型 sensor_msgs/msg/Image，640x480，rgb8。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
RUN_ID="vln_image_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
IMAGE_LOG="$LOG_DIR/ros2_image_once.log"
HZ_LOG="$LOG_DIR/ros2_image_hz.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
RESULT_FILE="$VLN_ROOT/UnityProjects/VLN_Offroad/Logs/vln_unitysensors_image_result.txt"
IMAGE_TOPIC="/vln/front/image_raw"
CAMERA_INFO_TOPIC="/vln/front/camera_info"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

endpoint_pid=""

cleanup()
{
  if [ -n "$endpoint_pid" ]; then
    kill "$endpoint_pid" >/dev/null 2>&1 || true
    wait "$endpoint_pid" >/dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

if [ -f "$RESULT_FILE" ]; then
  mv "$RESULT_FILE" "$LOG_DIR/previous_vln_unitysensors_image_result.txt"
fi

if ss -ltn 2>/dev/null | grep -E -q ':10000\b'; then
  echo "endpoint_already_listening=true" | tee "$LOG_DIR/run_summary.txt"
else
  "$VLN_ROOT/scripts/start_ros_tcp_endpoint.sh" >"$ENDPOINT_LOG" 2>&1 &
  endpoint_pid=$!
  echo "endpoint_pid=$endpoint_pid" | tee "$LOG_DIR/run_summary.txt"
  for _ in $(seq 1 60); do
    if ss -ltn 2>/dev/null | grep -E -q ':10000\b'; then
      break
    fi
    sleep 0.25
  done
fi

if ! ss -ltn 2>/dev/null | grep -E -q ':10000\b'; then
  echo "endpoint_failed_to_listen" | tee -a "$LOG_DIR/run_summary.txt"
  tail -n 120 "$ENDPOINT_LOG" || true
  exit 1
fi

ROS_ENV='source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true; if declare -F ros2env >/dev/null 2>&1; then ros2env >/dev/null; else source /opt/ros/humble/setup.bash; fi; source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash'

timeout 75s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic $IMAGE_TOPIC --width 640 --height 480 --encoding rgb8 --frame-id front_camera_optical_frame --timeout 70" >"$IMAGE_LOG" 2>&1 &
image_pid=$!

timeout 32s bash -lc "$ROS_ENV; ros2 topic hz --window 5 $IMAGE_TOPIC" >"$HZ_LOG" 2>&1 &
hz_pid=$!

set +e
timeout 90s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode -nographics \
  -executeMethod VLN.Editor.VlnUnitySensorsImageSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
wait "$image_pid"
image_status=$?
wait "$hz_pid"
hz_status_raw=$?
hz_status=$hz_status_raw
if [ "$hz_status_raw" -eq 124 ] && grep -q 'average rate:' "$HZ_LOG"; then
  hz_status=0
fi
timeout 12s bash -lc "$ROS_ENV; ros2 topic list -t" >"$TOPIC_LOG" 2>&1
topic_status=$?
set -e

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "image_status=$image_status"
  echo "hz_status_raw=$hz_status_raw"
  echo "hz_status=$hz_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "result_file=$RESULT_FILE"
  echo "image_topic=$IMAGE_TOPIC"
  echo "camera_info_topic=$CAMERA_INFO_TOPIC"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 image once output:"
sed -n '1,120p' "$IMAGE_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E "/vln/front/(image_raw|camera_info)" "$TOPIC_LOG" || true
echo "ROS2 image hz output:"
sed -n '1,120p' "$HZ_LOG" || true
echo "Unity image result:"
sed -n '1,80p' "$RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_UNITYSENSORS_IMAGE|Incompatible protocol|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,240p' || true

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  exit 1
fi

if [ "$image_status" -ne 0 ]; then
  echo "ros2_image_message_validation_failed"
  exit 1
fi

if ! grep -q 'VLN_UNITYSENSORS_IMAGE_MSG_OK' "$IMAGE_LOG"; then
  echo "ros2_image_message_missing_success_marker"
  exit 1
fi

if ! grep -F -q "$IMAGE_TOPIC [sensor_msgs/msg/Image]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_image_topic"
  exit 1
fi

if ! grep -F -q "$CAMERA_INFO_TOPIC [sensor_msgs/msg/CameraInfo]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_camera_info_topic"
  exit 1
fi

if ! grep -q 'average rate:' "$HZ_LOG"; then
  echo "ros2_topic_hz_no_average_rate"
  exit 1
fi

echo "VLN_UNITYSENSORS_IMAGE_SMOKE_TEST_PASS"
