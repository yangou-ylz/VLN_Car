#!/usr/bin/env bash

# 阶段 10：ROS2 路径点控制闭环。
# 验收内容：ROS2 根据 /tf 计算 /vln/cmd_vel，驱动 Unity 占位车体通过相对路径点；感知输出仍正常。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_waypoint_control_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
IMAGE_LOG="$LOG_DIR/ros2_image_once.log"
CAMERA_INFO_LOG="$LOG_DIR/ros2_camera_info_once.log"
CLOUD_LOG="$LOG_DIR/ros2_pointcloud2_once.log"
WAYPOINT_LOG="$LOG_DIR/ros2_waypoint_control.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
TERRAIN_RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_terrain_result.txt"
CONTROL_RESULT_FILE="$UNITY_PROJECT/Logs/vln_vehicle_control_result.txt"

IMAGE_TOPIC="/vln/front/image_raw"
CAMERA_INFO_TOPIC="/vln/front/camera_info"
POINTS_TOPIC="/vln/lidar/points"
TF_TOPIC="/tf"
CMD_VEL_TOPIC="/vln/cmd_vel"
RELATIVE_WAYPOINTS="${RELATIVE_WAYPOINTS:-1.2,0.0;2.4,0.0}"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

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

if [ -f "$TERRAIN_RESULT_FILE" ]; then
  mv "$TERRAIN_RESULT_FILE" "$LOG_DIR/previous_vln_offroad_terrain_result.txt"
fi

if [ -f "$CONTROL_RESULT_FILE" ]; then
  mv "$CONTROL_RESULT_FILE" "$LOG_DIR/previous_vln_vehicle_control_result.txt"
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

timeout 90s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_camera_info_once.py --topic $CAMERA_INFO_TOPIC --width 640 --height 480 --frame-id front_camera_optical_frame --timeout 85" >"$CAMERA_INFO_LOG" 2>&1 &
camera_info_pid=$!

timeout 90s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic $POINTS_TOPIC --width 7200 --point-step 16 --frame-id lidar_link --timeout 85 --min-nonzero-points 20" >"$CLOUD_LOG" 2>&1 &
cloud_pid=$!

timeout 90s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_drive_waypoints.py --cmd-topic $CMD_VEL_TOPIC --tf-topic $TF_TOPIC --relative-waypoints '$RELATIVE_WAYPOINTS' --timeout 85 --goal-tolerance 0.35 --min-total-progress 1.4" >"$WAYPOINT_LOG" 2>&1 &
waypoint_pid=$!

bash -lc "sleep 10; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

set +e
timeout 120s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadTerrainSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$image_pid" "$camera_info_pid" "$cloud_pid" "$waypoint_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$image_pid"
image_status=$?
wait "$camera_info_pid"
camera_info_status=$?
wait "$cloud_pid"
cloud_status=$?
wait "$waypoint_pid"
waypoint_status=$?
wait "$topic_pid"
topic_status=$?
set -e

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "image_status=$image_status"
  echo "camera_info_status=$camera_info_status"
  echo "cloud_status=$cloud_status"
  echo "waypoint_status=$waypoint_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "terrain_result_file=$TERRAIN_RESULT_FILE"
  echo "control_result_file=$CONTROL_RESULT_FILE"
  echo "image_topic=$IMAGE_TOPIC"
  echo "camera_info_topic=$CAMERA_INFO_TOPIC"
  echo "points_topic=$POINTS_TOPIC"
  echo "tf_topic=$TF_TOPIC"
  echo "cmd_vel_topic=$CMD_VEL_TOPIC"
  echo "relative_waypoints=$RELATIVE_WAYPOINTS"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 image once output:"
sed -n '1,120p' "$IMAGE_LOG" || true
echo "ROS2 camera info once output:"
sed -n '1,120p' "$CAMERA_INFO_LOG" || true
echo "ROS2 pointcloud once output:"
sed -n '1,140p' "$CLOUD_LOG" || true
echo "ROS2 waypoint control output:"
sed -n '1,220p' "$WAYPOINT_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|lidar|cmd_vel)|/tf' "$TOPIC_LOG" || true
echo "Unity terrain result:"
sed -n '1,120p' "$TERRAIN_RESULT_FILE" 2>/dev/null || true
echo "Unity control result:"
sed -n '1,220p' "$CONTROL_RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_OFFROAD_TERRAIN|VLN_VEHICLE_TF|VLN_CMD_VEL|Incompatible protocol|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,320p' || true

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

if [ "$waypoint_status" -ne 0 ]; then
  echo "ros2_waypoint_control_validation_failed"
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

if ! grep -q 'VLN_WAYPOINT_CONTROL_MSG_OK' "$WAYPOINT_LOG"; then
  echo "ros2_waypoint_control_missing_success_marker"
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

if ! grep -F -q "$CMD_VEL_TOPIC [geometry_msgs/msg/Twist]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_cmd_vel_topic"
  exit 1
fi

if ! grep -q '^cmd_vel_received=' "$CONTROL_RESULT_FILE"; then
  echo "unity_control_result_missing_cmd_vel_received"
  exit 1
fi

echo "VLN_WAYPOINT_CONTROL_SMOKE_TEST_PASS"
