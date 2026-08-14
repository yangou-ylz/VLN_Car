#!/usr/bin/env bash

# 阶段 12：复杂越野地图候选场景自动验收。
# 当前候选：Kenney Nature Kit 轻量 FBX 子集 + 现有 VLN 传感器/TF/控制接口。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_offroad_asset_candidate_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
IMAGE_LOG="$LOG_DIR/ros2_image_once.log"
CAMERA_INFO_LOG="$LOG_DIR/ros2_camera_info_once.log"
CLOUD_LOG="$LOG_DIR/ros2_pointcloud2_once.log"
TF_LOG="$LOG_DIR/ros2_vehicle_tf.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_asset_candidate_result.txt"
SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_asset_candidate_screenshot.png"

IMAGE_TOPIC="/vln/front/image_raw"
CAMERA_INFO_TOPIC="/vln/front/camera_info"
POINTS_TOPIC="/vln/lidar/points"
TF_TOPIC="/tf"

mkdir -p "$LOG_DIR"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前 Unity Editor，再运行本脚本。同一工程不能被两个 Editor 实例同时打开。"
  exit 2
fi

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
  mv "$RESULT_FILE" "$LOG_DIR/previous_vln_offroad_asset_candidate_result.txt"
fi

if [ -f "$SCREENSHOT_FILE" ]; then
  mv "$SCREENSHOT_FILE" "$LOG_DIR/previous_vln_offroad_asset_candidate_screenshot.png"
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

timeout 95s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic $IMAGE_TOPIC --width 640 --height 480 --encoding rgb8 --frame-id front_camera_optical_frame --timeout 90" >"$IMAGE_LOG" 2>&1 &
image_pid=$!

timeout 95s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_camera_info_once.py --topic $CAMERA_INFO_TOPIC --width 640 --height 480 --frame-id front_camera_optical_frame --timeout 90" >"$CAMERA_INFO_LOG" 2>&1 &
camera_info_pid=$!

timeout 95s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic $POINTS_TOPIC --width 7200 --point-step 16 --frame-id lidar_link --timeout 90 --min-nonzero-points 80" >"$CLOUD_LOG" 2>&1 &
cloud_pid=$!

timeout 95s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_vehicle_tf.py --topic $TF_TOPIC --timeout 90 --min-base-delta 0.0 --max-base-delta 0.05 --stable-observe-seconds 6.0" >"$TF_LOG" 2>&1 &
tf_pid=$!

bash -lc "sleep 10; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

set +e
timeout 135s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadAssetCandidateSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$image_pid" "$camera_info_pid" "$cloud_pid" "$tf_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$image_pid"
image_status=$?
wait "$camera_info_pid"
camera_info_status=$?
wait "$cloud_pid"
cloud_status=$?
wait "$tf_pid"
tf_status=$?
wait "$topic_pid"
topic_status=$?
set -e

if [ -f "$SCREENSHOT_FILE" ]; then
  cp "$SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_asset_candidate_screenshot.png"
fi

model_count=$(find "$UNITY_PROJECT/Assets/VLN/ExternalAssets/KenneyNatureKit/Models" -maxdepth 1 -type f -name '*.fbx' 2>/dev/null | wc -l)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "image_status=$image_status"
  echo "camera_info_status=$camera_info_status"
  echo "cloud_status=$cloud_status"
  echo "tf_status=$tf_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "result_file=$RESULT_FILE"
  echo "screenshot_file=$SCREENSHOT_FILE"
  echo "asset_model_count=$model_count"
  echo "image_topic=$IMAGE_TOPIC"
  echo "camera_info_topic=$CAMERA_INFO_TOPIC"
  echo "points_topic=$POINTS_TOPIC"
  echo "tf_topic=$TF_TOPIC"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 image once output:"
sed -n '1,120p' "$IMAGE_LOG" || true
echo "ROS2 camera info once output:"
sed -n '1,120p' "$CAMERA_INFO_LOG" || true
echo "ROS2 pointcloud once output:"
sed -n '1,140p' "$CLOUD_LOG" || true
echo "ROS2 vehicle TF output:"
sed -n '1,140p' "$TF_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|lidar)|/tf' "$TOPIC_LOG" || true
echo "Unity asset candidate result:"
sed -n '1,140p' "$RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_OFFROAD_ASSET_CANDIDATE|Incompatible protocol|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,320p' || true

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  exit 1
fi

if [ "$image_status" -ne 0 ]; then
  echo "ros2_image_message_validation_failed"
  exit 1
fi

if [ "$camera_info_status" -ne 0 ]; then
  echo "ros2_camera_info_message_validation_failed"
  exit 1
fi

if [ "$cloud_status" -ne 0 ]; then
  echo "ros2_pointcloud2_message_validation_failed"
  exit 1
fi

if [ "$tf_status" -ne 0 ]; then
  echo "ros2_vehicle_tf_validation_failed"
  exit 1
fi

if ! grep -q 'VLN_UNITYSENSORS_IMAGE_MSG_OK' "$IMAGE_LOG"; then
  echo "ros2_image_message_missing_success_marker"
  exit 1
fi

if ! grep -q 'VLN_UNITYSENSORS_CAMERA_INFO_MSG_OK' "$CAMERA_INFO_LOG"; then
  echo "ros2_camera_info_message_missing_success_marker"
  exit 1
fi

if ! grep -q 'VLN_UNITYSENSORS_POINTCLOUD2_MSG_OK' "$CLOUD_LOG"; then
  echo "ros2_pointcloud2_message_missing_success_marker"
  exit 1
fi

if ! grep -q 'VLN_VEHICLE_TF_MSG_OK' "$TF_LOG"; then
  echo "ros2_vehicle_tf_missing_success_marker"
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

if ! grep -F -q "$POINTS_TOPIC [sensor_msgs/msg/PointCloud2]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_pointcloud2_topic"
  exit 1
fi

if ! grep -F -q "$TF_TOPIC [tf2_msgs/msg/TFMessage]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_tf_topic"
  exit 1
fi

if [ ! -s "$SCREENSHOT_FILE" ]; then
  echo "asset_candidate_screenshot_missing"
  exit 1
fi

echo "VLN_OFFROAD_ASSET_CANDIDATE_SMOKE_TEST_PASS"
