#!/usr/bin/env bash

# 阶段 13：Scout V2 URDF 候选场景 /vln/cmd_vel 控制验收。
# 验收内容：Scout URDF 候选场景启动后，ROS2 发布 Twist，Unity rig 响应移动，图像/CameraInfo/点云仍正常。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_scout_urdf_cmd_vel_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
IMAGE_LOG="$LOG_DIR/ros2_image_once.log"
CAMERA_INFO_LOG="$LOG_DIR/ros2_camera_info_once.log"
CLOUD_LOG="$LOG_DIR/ros2_pointcloud2_once.log"
CONTROL_LOG="$LOG_DIR/ros2_cmd_vel_control.log"
ODOM_LOG="$LOG_DIR/ros2_odom_once.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
SCOUT_RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_urdf_candidate_result.txt"
CONTROL_RESULT_FILE="$UNITY_PROJECT/Logs/vln_vehicle_control_result.txt"
WHEEL_PROBE_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_wheel_joint_command_probe_result.txt"
ODOM_RESULT_FILE="$UNITY_PROJECT/Logs/vln_odom_publisher_result.txt"

IMAGE_TOPIC="/vln/front/image_raw"
CAMERA_INFO_TOPIC="/vln/front/camera_info"
POINTS_TOPIC="/vln/lidar/points"
TF_TOPIC="/tf"
CMD_VEL_TOPIC="/vln/cmd_vel"
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

for old_file in "$SCOUT_RESULT_FILE" "$CONTROL_RESULT_FILE" "$WHEEL_PROBE_RESULT_FILE" "$ODOM_RESULT_FILE"; do
  if [ -f "$old_file" ]; then
    mv "$old_file" "$LOG_DIR/previous_$(basename "$old_file")"
  fi
done

if ss -ltn 2>/dev/null | grep -E -q ':10000\b'; then
  echo "endpoint_already_listening=true" | tee -a "$LOG_DIR/run_summary.txt"
else
  "$VLN_ROOT/scripts/start_ros_tcp_endpoint.sh" >"$ENDPOINT_LOG" 2>&1 &
  endpoint_pid=$!
  echo "endpoint_pid=$endpoint_pid" | tee -a "$LOG_DIR/run_summary.txt"
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

timeout 100s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_send_cmd_vel_and_wait_tf.py --cmd-topic $CMD_VEL_TOPIC --tf-topic $TF_TOPIC --odom-topic $ODOM_TOPIC --linear-x 0.8 --angular-z 0.7 --duration 4.0 --timeout 95 --min-delta 1.0 --min-yaw-delta 0.7 --min-odom-delta 1.0 --min-odom-yaw-delta 0.7" >"$CONTROL_LOG" 2>&1 &
control_pid=$!

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
  kill "$image_pid" "$camera_info_pid" "$cloud_pid" "$control_pid" "$odom_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$image_pid"
image_status=$?
wait "$camera_info_pid"
camera_info_status=$?
wait "$cloud_pid"
cloud_status=$?
wait "$control_pid"
control_status=$?
wait "$odom_pid"
odom_status=$?
wait "$topic_pid"
topic_status=$?
set -e

cmd_vel_count=$(grep -E '^cmd_vel_count=' "$CONTROL_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
collision_block_count=$(grep -E '^collision_block_count=' "$CONTROL_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
wheel_found_count=$(grep -E '^wheel_found_count=' "$WHEEL_PROBE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
wheel_command_count=$(grep -E '^wheel_command_count=' "$WHEEL_PROBE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
nonzero_target_count=$(grep -E '^nonzero_target_count=' "$WHEEL_PROBE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
odom_publish_count=$(grep -E '^odom_publish_count=' "$ODOM_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "image_status=$image_status"
  echo "camera_info_status=$camera_info_status"
  echo "cloud_status=$cloud_status"
  echo "control_status=$control_status"
  echo "odom_status=$odom_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "scout_result_file=$SCOUT_RESULT_FILE"
  echo "control_result_file=$CONTROL_RESULT_FILE"
  echo "wheel_probe_result_file=$WHEEL_PROBE_RESULT_FILE"
  echo "odom_result_file=$ODOM_RESULT_FILE"
  echo "cmd_vel_count=${cmd_vel_count:-0}"
  echo "collision_block_count=${collision_block_count:-missing}"
  echo "wheel_found_count=${wheel_found_count:-0}"
  echo "wheel_command_count=${wheel_command_count:-0}"
  echo "nonzero_target_count=${nonzero_target_count:-0}"
  echo "odom_publish_count=${odom_publish_count:-0}"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 image once output:"
sed -n '1,120p' "$IMAGE_LOG" || true
echo "ROS2 camera info once output:"
sed -n '1,120p' "$CAMERA_INFO_LOG" || true
echo "ROS2 pointcloud once output:"
sed -n '1,140p' "$CLOUD_LOG" || true
echo "ROS2 cmd_vel control output:"
sed -n '1,180p' "$CONTROL_LOG" || true
echo "ROS2 odom once output:"
sed -n '1,140p' "$ODOM_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|lidar|cmd_vel|odom)|/tf' "$TOPIC_LOG" || true
echo "Unity Scout URDF candidate result:"
sed -n '1,180p' "$SCOUT_RESULT_FILE" 2>/dev/null || true
echo "Unity control result:"
sed -n '1,220p' "$CONTROL_RESULT_FILE" 2>/dev/null || true
echo "Scout wheel joint command probe result:"
sed -n '1,220p' "$WHEEL_PROBE_RESULT_FILE" 2>/dev/null || true
echo "Unity odom publisher result:"
sed -n '1,220p' "$ODOM_RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_OFFROAD_SCOUT_URDF_CANDIDATE|VLN_SCOUT_URDF_IMPORTED|VLN_SCOUT_WHEEL_JOINT|VLN_CMD_VEL|VLN_ODOM|Incompatible protocol|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,480p' || true

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

if [ "$control_status" -ne 0 ]; then
  echo "ros2_cmd_vel_control_validation_failed"
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

if ! grep -q 'VLN_CMD_VEL_CONTROL_MSG_OK' "$CONTROL_LOG"; then
  echo "ros2_cmd_vel_control_missing_success_marker"
  exit 1
fi

if ! grep -q 'VLN_ODOM_MOTION_MSG_OK' "$CONTROL_LOG"; then
  echo "ros2_odom_motion_missing_success_marker"
  exit 1
fi

if ! grep -q 'VLN_ODOM_MSG_OK' "$ODOM_LOG"; then
  echo "ros2_odom_message_missing_success_marker"
  exit 1
fi

if ! grep -F -q "$CMD_VEL_TOPIC [geometry_msgs/msg/Twist]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_cmd_vel_topic"
  exit 1
fi

if ! grep -F -q "$ODOM_TOPIC [nav_msgs/msg/Odometry]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_odom_topic"
  exit 1
fi

if [ "${cmd_vel_count:-0}" -lt 20 ]; then
  echo "unity_control_result_cmd_vel_count_too_low"
  exit 1
fi

if [ "${wheel_found_count:-0}" -lt 4 ]; then
  echo "scout_wheel_joint_probe_missing_wheels"
  exit 1
fi

if [ "${wheel_command_count:-0}" -lt 20 ]; then
  echo "scout_wheel_joint_probe_command_count_too_low"
  exit 1
fi

if [ "${nonzero_target_count:-0}" -lt 4 ]; then
  echo "scout_wheel_joint_probe_targets_not_written"
  exit 1
fi

if [ "${odom_publish_count:-0}" -lt 20 ]; then
  echo "odom_publish_count_too_low"
  exit 1
fi

echo "VLN_SCOUT_URDF_CMD_VEL_SMOKE_TEST_PASS"
