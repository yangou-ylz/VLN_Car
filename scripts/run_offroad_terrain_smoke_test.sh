#!/usr/bin/env bash

# 运行阶段 6：极简越野 terrain + UnitySensors 图像/点云联合闭环测试。
# 验收内容：ROS2 同时收到 /vln/front/image_raw 与 /vln/lidar/points，且 Unity 场景包含轻量越野地形和障碍物。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_offroad_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
IMAGE_LOG="$LOG_DIR/ros2_image_once.log"
CLOUD_LOG="$LOG_DIR/ros2_pointcloud2_once.log"
IMAGE_HZ_LOG="$LOG_DIR/ros2_image_hz.log"
CLOUD_HZ_LOG="$LOG_DIR/ros2_pointcloud2_hz.log"
CLOUD_BW_LOG="$LOG_DIR/ros2_pointcloud2_bw.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_terrain_result.txt"
IMAGE_TOPIC="/vln/front/image_raw"
CAMERA_INFO_TOPIC="/vln/front/camera_info"
POINTS_TOPIC="/vln/lidar/points"

mkdir -p "$LOG_DIR"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前 Unity Editor，再运行本脚本。Unity 不允许同一工程同时被两个 Editor 实例打开。"
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
  mv "$RESULT_FILE" "$LOG_DIR/previous_vln_offroad_terrain_result.txt"
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

timeout 90s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic $IMAGE_TOPIC --width 640 --height 480 --encoding rgb8 --frame-id front_camera_optical_frame --timeout 85" >"$IMAGE_LOG" 2>&1 &
image_pid=$!

timeout 90s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic $POINTS_TOPIC --width 7200 --point-step 16 --frame-id lidar_link --timeout 85 --min-nonzero-points 20" >"$CLOUD_LOG" 2>&1 &
cloud_pid=$!

timeout 38s bash -lc "$ROS_ENV; ros2 topic hz --window 5 $IMAGE_TOPIC" >"$IMAGE_HZ_LOG" 2>&1 &
image_hz_pid=$!

timeout 38s bash -lc "$ROS_ENV; ros2 topic hz --window 5 $POINTS_TOPIC" >"$CLOUD_HZ_LOG" 2>&1 &
cloud_hz_pid=$!

timeout 38s bash -lc "$ROS_ENV; ros2 topic bw $POINTS_TOPIC" >"$CLOUD_BW_LOG" 2>&1 &
cloud_bw_pid=$!

bash -lc "sleep 8; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

set +e
timeout 115s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadTerrainSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$image_pid" "$cloud_pid" "$image_hz_pid" "$cloud_hz_pid" "$cloud_bw_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$image_pid"
image_status=$?
wait "$cloud_pid"
cloud_status=$?
wait "$image_hz_pid"
image_hz_status_raw=$?
image_hz_status=$image_hz_status_raw
if [ "$image_hz_status_raw" -eq 124 ] && grep -q 'average rate:' "$IMAGE_HZ_LOG"; then
  image_hz_status=0
fi
wait "$cloud_hz_pid"
cloud_hz_status_raw=$?
cloud_hz_status=$cloud_hz_status_raw
if [ "$cloud_hz_status_raw" -eq 124 ] && grep -q 'average rate:' "$CLOUD_HZ_LOG"; then
  cloud_hz_status=0
fi
wait "$cloud_bw_pid"
cloud_bw_status_raw=$?
cloud_bw_status=$cloud_bw_status_raw
if [ "$cloud_bw_status_raw" -eq 124 ] && grep -E -q '([KMGT]?B/s|MB/s) from [0-9]+ messages' "$CLOUD_BW_LOG"; then
  cloud_bw_status=0
fi
wait "$topic_pid"
topic_status=$?
set -e

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "image_status=$image_status"
  echo "cloud_status=$cloud_status"
  echo "image_hz_status_raw=$image_hz_status_raw"
  echo "image_hz_status=$image_hz_status"
  echo "cloud_hz_status_raw=$cloud_hz_status_raw"
  echo "cloud_hz_status=$cloud_hz_status"
  echo "cloud_bw_status_raw=$cloud_bw_status_raw"
  echo "cloud_bw_status=$cloud_bw_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "result_file=$RESULT_FILE"
  echo "image_topic=$IMAGE_TOPIC"
  echo "camera_info_topic=$CAMERA_INFO_TOPIC"
  echo "points_topic=$POINTS_TOPIC"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 image once output:"
sed -n '1,120p' "$IMAGE_LOG" || true
echo "ROS2 pointcloud once output:"
sed -n '1,140p' "$CLOUD_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|lidar)' "$TOPIC_LOG" || true
echo "ROS2 image hz output:"
sed -n '1,120p' "$IMAGE_HZ_LOG" || true
echo "ROS2 pointcloud hz output:"
sed -n '1,120p' "$CLOUD_HZ_LOG" || true
echo "ROS2 pointcloud bw output:"
sed -n '1,120p' "$CLOUD_BW_LOG" || true
echo "Unity offroad terrain result:"
sed -n '1,100p' "$RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_OFFROAD_TERRAIN|Incompatible protocol|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,280p' || true

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  exit 1
fi

if [ "$image_status" -ne 0 ]; then
  echo "ros2_image_message_validation_failed"
  exit 1
fi

if [ "$cloud_status" -ne 0 ]; then
  echo "ros2_pointcloud2_message_validation_failed"
  exit 1
fi

if ! grep -q 'VLN_UNITYSENSORS_IMAGE_MSG_OK' "$IMAGE_LOG"; then
  echo "ros2_image_message_missing_success_marker"
  exit 1
fi

if ! grep -q 'VLN_UNITYSENSORS_POINTCLOUD2_MSG_OK' "$CLOUD_LOG"; then
  echo "ros2_pointcloud2_message_missing_success_marker"
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

if ! grep -q 'average rate:' "$IMAGE_HZ_LOG"; then
  echo "ros2_image_hz_no_average_rate"
  exit 1
fi

if ! grep -q 'average rate:' "$CLOUD_HZ_LOG"; then
  echo "ros2_pointcloud_hz_no_average_rate"
  exit 1
fi

if ! grep -E -q '([KMGT]?B/s|MB/s) from [0-9]+ messages' "$CLOUD_BW_LOG"; then
  echo "ros2_topic_bw_no_average"
  exit 1
fi

echo "VLN_OFFROAD_TERRAIN_SMOKE_TEST_PASS"
