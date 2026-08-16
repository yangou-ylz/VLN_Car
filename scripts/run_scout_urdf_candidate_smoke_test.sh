#!/usr/bin/env bash

# 阶段 13：Scout V2 URDF 物理车体候选场景自动验收。
# 当前候选：AgileX Scout V2 xacro 展开后的 URDF + Unity URDF Importer + 现有 VLN 传感器/TF/cmd_vel 接口。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_scout_urdf_candidate_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
IMAGE_LOG="$LOG_DIR/ros2_image_once.log"
CAMERA_INFO_LOG="$LOG_DIR/ros2_camera_info_once.log"
CLOUD_LOG="$LOG_DIR/ros2_pointcloud2_once.log"
TF_LOG="$LOG_DIR/ros2_vehicle_tf.log"
ODOM_LOG="$LOG_DIR/ros2_odom_once.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_urdf_candidate_result.txt"
SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_urdf_candidate_screenshot.png"
DETAIL_SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_urdf_candidate_detail_screenshot.png"
SCOUT_ASSET_ROOT="$UNITY_PROJECT/Assets/VLN/ExternalAssets/ScoutUrdfPhysics"

IMAGE_TOPIC="/vln/front/image_raw"
CAMERA_INFO_TOPIC="/vln/front/camera_info"
POINTS_TOPIC="/vln/lidar/points"
TF_TOPIC="/tf"
ODOM_TOPIC="/vln/odom"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前 Unity Editor，再运行本脚本。同一工程不能被两个 Editor 实例同时打开。"
  exit 2
fi

if find "$UNITY_PROJECT/Library" -maxdepth 1 -type f \( -name '*lock*' -o -name '*Lock*' \) 2>/dev/null | grep -q .; then
  echo "发现 Unity stale lock，先用项目恢复脚本移动 lock。" | tee "$LOG_DIR/run_summary.txt"
  "$VLN_ROOT/scripts/stop_unity_vln_project.sh" | tee -a "$LOG_DIR/run_summary.txt"
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

for old_file in "$RESULT_FILE" "$SCREENSHOT_FILE" "$DETAIL_SCREENSHOT_FILE"; do
  if [ -f "$old_file" ]; then
    mv "$old_file" "$LOG_DIR/previous_$(basename "$old_file")"
  fi
done

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

timeout 100s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic $IMAGE_TOPIC --width 640 --height 480 --encoding rgb8 --frame-id front_camera_optical_frame --timeout 95" >"$IMAGE_LOG" 2>&1 &
image_pid=$!

timeout 100s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_camera_info_once.py --topic $CAMERA_INFO_TOPIC --width 640 --height 480 --frame-id front_camera_optical_frame --timeout 95" >"$CAMERA_INFO_LOG" 2>&1 &
camera_info_pid=$!

timeout 100s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic $POINTS_TOPIC --width 7200 --point-step 16 --frame-id lidar_link --timeout 95 --min-nonzero-points 80" >"$CLOUD_LOG" 2>&1 &
cloud_pid=$!

timeout 100s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_vehicle_tf.py --topic $TF_TOPIC --timeout 95 --min-base-delta 0.0 --max-base-delta 0.05 --stable-observe-seconds 6.0" >"$TF_LOG" 2>&1 &
tf_pid=$!

timeout 100s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_odom_once.py --topic $ODOM_TOPIC --frame-id map --child-frame-id base_link --timeout 95" >"$ODOM_LOG" 2>&1 &
odom_pid=$!

bash -lc "sleep 12; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

set +e
timeout 150s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadScoutUrdfCandidateSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$image_pid" "$camera_info_pid" "$cloud_pid" "$tf_pid" "$odom_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$image_pid"
image_status=$?
wait "$camera_info_pid"
camera_info_status=$?
wait "$cloud_pid"
cloud_status=$?
wait "$tf_pid"
tf_status=$?
wait "$odom_pid"
odom_status=$?
wait "$topic_pid"
topic_status=$?
set -e

if [ -f "$SCREENSHOT_FILE" ]; then
  cp "$SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_scout_urdf_candidate_screenshot.png"
fi

if [ -f "$DETAIL_SCREENSHOT_FILE" ]; then
  cp "$DETAIL_SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_scout_urdf_candidate_detail_screenshot.png"
fi

urdf_file_count=$(find "$SCOUT_ASSET_ROOT" -maxdepth 4 -type f \( -name '*.urdf' -o -name '*.dae' \) 2>/dev/null | wc -l)
urdf_link_count=$(grep -E '^urdf_link_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
urdf_joint_count=$(grep -E '^urdf_joint_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
urdf_inertial_count=$(grep -E '^urdf_inertial_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
urdf_collision_count=$(grep -E '^urdf_collision_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
unity_collider_count=$(grep -E '^unity_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
renderer_count=$(grep -E '^renderer_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
static_pose_delta=$(grep -E '^static_pose_delta_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "image_status=$image_status"
  echo "camera_info_status=$camera_info_status"
  echo "cloud_status=$cloud_status"
  echo "tf_status=$tf_status"
  echo "odom_status=$odom_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "result_file=$RESULT_FILE"
  echo "screenshot_file=$SCREENSHOT_FILE"
  echo "detail_screenshot_file=$DETAIL_SCREENSHOT_FILE"
  echo "scout_asset_file_count=$urdf_file_count"
  echo "urdf_link_count=${urdf_link_count:-0}"
  echo "urdf_joint_count=${urdf_joint_count:-0}"
  echo "urdf_inertial_count=${urdf_inertial_count:-0}"
  echo "urdf_collision_count=${urdf_collision_count:-0}"
  echo "unity_collider_count=${unity_collider_count:-0}"
  echo "renderer_count=${renderer_count:-0}"
  echo "static_pose_delta_m=${static_pose_delta:-missing}"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 image once output:"
sed -n '1,120p' "$IMAGE_LOG" || true
echo "ROS2 camera info once output:"
sed -n '1,120p' "$CAMERA_INFO_LOG" || true
echo "ROS2 pointcloud once output:"
sed -n '1,140p' "$CLOUD_LOG" || true
echo "ROS2 vehicle TF output:"
sed -n '1,140p' "$TF_LOG" || true
echo "ROS2 odom once output:"
sed -n '1,140p' "$ODOM_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|lidar|odom)|/tf' "$TOPIC_LOG" || true
echo "Unity Scout URDF candidate result:"
sed -n '1,180p' "$RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_OFFROAD_SCOUT_URDF_CANDIDATE|VLN_SCOUT_URDF_IMPORTED|Incompatible protocol|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,380p' || true

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

if [ "$odom_status" -ne 0 ]; then
  echo "ros2_odom_message_validation_failed"
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

if ! grep -q 'VLN_ODOM_MSG_OK' "$ODOM_LOG"; then
  echo "ros2_odom_message_missing_success_marker"
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

if ! grep -F -q "$ODOM_TOPIC [nav_msgs/msg/Odometry]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_odom_topic"
  exit 1
fi

if [ ! -s "$SCREENSHOT_FILE" ]; then
  echo "scout_urdf_candidate_screenshot_missing"
  exit 1
fi

if [ ! -s "$DETAIL_SCREENSHOT_FILE" ]; then
  echo "scout_urdf_candidate_detail_screenshot_missing"
  exit 1
fi

if [ "$urdf_file_count" -lt 3 ]; then
  echo "scout_urdf_asset_subset_incomplete"
  exit 1
fi

if [ "${urdf_link_count:-0}" -lt 6 ]; then
  echo "scout_urdf_links_missing"
  exit 1
fi

if [ "${urdf_joint_count:-0}" -lt 5 ]; then
  echo "scout_urdf_joints_missing"
  exit 1
fi

if [ "${urdf_inertial_count:-0}" -lt 5 ]; then
  echo "scout_urdf_inertials_missing"
  exit 1
fi

if [ "${urdf_collision_count:-0}" -lt 6 ]; then
  echo "scout_urdf_collisions_missing"
  exit 1
fi

if [ "${unity_collider_count:-0}" -lt 6 ]; then
  echo "scout_unity_colliders_missing"
  exit 1
fi

if [ "${renderer_count:-0}" -lt 5 ]; then
  echo "scout_renderers_missing"
  exit 1
fi

if ! awk -v delta="${static_pose_delta:-999}" 'BEGIN { exit !(delta <= 0.05) }'; then
  echo "scout_static_pose_unstable"
  exit 1
fi

echo "VLN_SCOUT_URDF_CANDIDATE_SMOKE_TEST_PASS"
