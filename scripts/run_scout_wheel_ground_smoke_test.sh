#!/usr/bin/env bash

# 阶段 14：Scout wheel-ground 真实动力学候选自动验收。
# 验收内容：Unity WheelCollider + Rigidbody 通过轮地接触驱动车体，旧 ROS2 感知/TF/odom 接口仍保持稳定。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_scout_wheel_ground_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
IMAGE_LOG="$LOG_DIR/ros2_image_once.log"
CAMERA_INFO_LOG="$LOG_DIR/ros2_camera_info_once.log"
CLOUD_LOG="$LOG_DIR/ros2_pointcloud2_once.log"
CONTROL_LOG="$LOG_DIR/ros2_cmd_vel_control.log"
ODOM_LOG="$LOG_DIR/ros2_odom_once.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_candidate_result.txt"
SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_candidate_screenshot.png"
TOPGEAR_SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_screenshot.png"
BRIDGE_SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_bridge_screenshot.png"
SHORT_RAMP_SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_short_ramp_screenshot.png"
CONTROL_RESULT_FILE="$UNITY_PROJECT/Logs/vln_vehicle_control_result.txt"
CONTROLLER_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_wheel_ground_controller_result.txt"
FOLLOW_RESULT_FILE="$UNITY_PROJECT/Logs/vln_follow_transform_pose_result.txt"
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

for old_file in "$RESULT_FILE" "$SCREENSHOT_FILE" "$TOPGEAR_SCREENSHOT_FILE" "$BRIDGE_SCREENSHOT_FILE" "$SHORT_RAMP_SCREENSHOT_FILE" "$CONTROL_RESULT_FILE" "$CONTROLLER_RESULT_FILE" "$FOLLOW_RESULT_FILE" "$ODOM_RESULT_FILE"; do
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

timeout 115s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic $IMAGE_TOPIC --width 640 --height 480 --encoding rgb8 --frame-id front_camera_optical_frame --timeout 110" >"$IMAGE_LOG" 2>&1 &
image_pid=$!

timeout 115s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_camera_info_once.py --topic $CAMERA_INFO_TOPIC --width 640 --height 480 --frame-id front_camera_optical_frame --timeout 110" >"$CAMERA_INFO_LOG" 2>&1 &
camera_info_pid=$!

timeout 115s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic $POINTS_TOPIC --width 7200 --point-step 16 --frame-id lidar_link --timeout 110 --min-nonzero-points 80" >"$CLOUD_LOG" 2>&1 &
cloud_pid=$!

timeout 115s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_send_cmd_vel_and_wait_tf.py --cmd-topic $CMD_VEL_TOPIC --tf-topic $TF_TOPIC --odom-topic $ODOM_TOPIC --linear-x 0.65 --angular-z 0.0 --duration 5.0 --timeout 110 --min-delta 0.18 --min-forward-delta 0.15 --min-yaw-delta 0.0 --min-odom-delta 0.18 --min-odom-forward-delta 0.15 --min-odom-yaw-delta 0.0" >"$CONTROL_LOG" 2>&1 &
control_pid=$!

timeout 115s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_odom_once.py --topic $ODOM_TOPIC --frame-id map --child-frame-id base_link --timeout 110" >"$ODOM_LOG" 2>&1 &
odom_pid=$!

bash -lc "sleep 14; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

set +e
timeout 170s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadScoutWheelGroundCandidateSmokeTestRunner.Run \
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

if [ -f "$SCREENSHOT_FILE" ]; then
  cp "$SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_scout_wheel_ground_candidate_screenshot.png"
fi
if [ -f "$TOPGEAR_SCREENSHOT_FILE" ]; then
  cp "$TOPGEAR_SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_scout_wheel_ground_topgear_visual_screenshot.png"
fi
if [ -f "$BRIDGE_SCREENSHOT_FILE" ]; then
  cp "$BRIDGE_SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_scout_wheel_ground_bridge_screenshot.png"
fi
if [ -f "$SHORT_RAMP_SCREENSHOT_FILE" ]; then
  cp "$SHORT_RAMP_SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_scout_wheel_ground_short_ramp_screenshot.png"
fi

for current_file in "$RESULT_FILE" "$CONTROL_RESULT_FILE" "$CONTROLLER_RESULT_FILE" "$FOLLOW_RESULT_FILE" "$ODOM_RESULT_FILE"; do
  if [ -f "$current_file" ]; then
    cp "$current_file" "$LOG_DIR/$(basename "$current_file")"
  fi
done

wheel_collider_count=$(grep -E '^wheel_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
broad_physical_trail_count=$(grep -E '^broad_physical_trail_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
road_physical_slab_count=$(grep -E '^road_physical_slab_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
road_seam_transition_count=$(grep -E '^road_seam_transition_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_physics_count=$(grep -E '^bridge_physics_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
short_ramp_physics_count=$(grep -E '^short_ramp_physics_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_visual_detail_count=$(grep -E '^bridge_visual_detail_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
short_ramp_visual_detail_count=$(grep -E '^short_ramp_visual_detail_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
decorative_trail_collider_count=$(grep -E '^decorative_trail_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
decorative_bridge_renderer_count=$(grep -E '^decorative_bridge_renderer_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_deck_has_renderer=$(grep -E '^bridge_deck_has_renderer=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_deck_has_collider=$(grep -E '^bridge_deck_has_collider=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_deck_renderer_collider_top_delta=$(grep -E '^bridge_deck_renderer_collider_top_delta_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
road_physical_max_width=$(grep -E '^road_physical_max_width_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_physical_max_width=$(grep -E '^bridge_physical_max_width_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_physical_height_span=$(grep -E '^bridge_physical_height_span_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
short_ramp_physical_max_width=$(grep -E '^short_ramp_physical_max_width_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
short_ramp_physical_height_span=$(grep -E '^short_ramp_physical_height_span_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
visual_renderer_count=$(grep -E '^visual_renderer_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
visual_collider_count=$(grep -E '^visual_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
visual_articulation_body_count=$(grep -E '^visual_articulation_body_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_visual_present=$(grep -E '^topgear_visual_present=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_visual_renderer_count=$(grep -E '^topgear_visual_renderer_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_visual_collider_count=$(grep -E '^topgear_visual_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_visual_rigidbody_count=$(grep -E '^topgear_visual_rigidbody_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_visual_bounds_size=$(grep -E '^topgear_visual_bounds_size_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
physics_root_delta=$(grep -E '^physics_root_delta_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
cmd_vel_count=$(grep -E '^cmd_vel_count=' "$CONTROL_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
controller_cmd_count=$(grep -E '^cmd_vel_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
physics_step_count=$(grep -E '^physics_step_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
motor_command_count=$(grep -E '^motor_command_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
wheel_visual_total_abs_roll_deg=$(grep -E '^wheel_visual_total_abs_roll_deg=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
wheel_visual_direction_reversal_count=$(grep -E '^wheel_visual_direction_reversal_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
follow_update_count=$(grep -E '^follow_update_count=' "$FOLLOW_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
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
  echo "result_file=$RESULT_FILE"
  echo "screenshot_file=$SCREENSHOT_FILE"
  echo "bridge_screenshot_file=$BRIDGE_SCREENSHOT_FILE"
  echo "short_ramp_screenshot_file=$SHORT_RAMP_SCREENSHOT_FILE"
  echo "control_result_file=$CONTROL_RESULT_FILE"
  echo "controller_result_file=$CONTROLLER_RESULT_FILE"
  echo "follow_result_file=$FOLLOW_RESULT_FILE"
  echo "odom_result_file=$ODOM_RESULT_FILE"
  echo "wheel_collider_count=${wheel_collider_count:-0}"
  echo "broad_physical_trail_count=${broad_physical_trail_count:-missing}"
  echo "road_physical_slab_count=${road_physical_slab_count:-missing}"
  echo "road_seam_transition_count=${road_seam_transition_count:-missing}"
  echo "bridge_physics_count=${bridge_physics_count:-missing}"
  echo "short_ramp_physics_count=${short_ramp_physics_count:-missing}"
  echo "bridge_visual_detail_count=${bridge_visual_detail_count:-missing}"
  echo "short_ramp_visual_detail_count=${short_ramp_visual_detail_count:-missing}"
  echo "decorative_trail_collider_count=${decorative_trail_collider_count:-missing}"
  echo "decorative_bridge_renderer_count=${decorative_bridge_renderer_count:-missing}"
  echo "bridge_deck_has_renderer=${bridge_deck_has_renderer:-missing}"
  echo "bridge_deck_has_collider=${bridge_deck_has_collider:-missing}"
  echo "bridge_deck_renderer_collider_top_delta_m=${bridge_deck_renderer_collider_top_delta:-missing}"
  echo "road_physical_max_width_m=${road_physical_max_width:-missing}"
  echo "bridge_physical_max_width_m=${bridge_physical_max_width:-missing}"
  echo "bridge_physical_height_span_m=${bridge_physical_height_span:-missing}"
  echo "short_ramp_physical_max_width_m=${short_ramp_physical_max_width:-missing}"
  echo "short_ramp_physical_height_span_m=${short_ramp_physical_height_span:-missing}"
  echo "visual_renderer_count=${visual_renderer_count:-0}"
  echo "visual_collider_count=${visual_collider_count:-missing}"
  echo "visual_articulation_body_count=${visual_articulation_body_count:-missing}"
  echo "topgear_visual_present=${topgear_visual_present:-0}"
  echo "topgear_visual_renderer_count=${topgear_visual_renderer_count:-0}"
  echo "topgear_visual_collider_count=${topgear_visual_collider_count:-missing}"
  echo "topgear_visual_rigidbody_count=${topgear_visual_rigidbody_count:-missing}"
  echo "topgear_visual_bounds_size_m=${topgear_visual_bounds_size:-missing}"
  echo "physics_root_delta_m=${physics_root_delta:-missing}"
  echo "cmd_vel_count=${cmd_vel_count:-0}"
  echo "controller_cmd_count=${controller_cmd_count:-0}"
  echo "physics_step_count=${physics_step_count:-0}"
  echo "motor_command_count=${motor_command_count:-0}"
  echo "wheel_visual_total_abs_roll_deg=${wheel_visual_total_abs_roll_deg:-0}"
  echo "wheel_visual_direction_reversal_count=${wheel_visual_direction_reversal_count:-0}"
  echo "follow_update_count=${follow_update_count:-0}"
  echo "odom_publish_count=${odom_publish_count:-0}"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 image once output:"
sed -n '1,120p' "$IMAGE_LOG" || true
echo "ROS2 camera info once output:"
sed -n '1,120p' "$CAMERA_INFO_LOG" || true
echo "ROS2 pointcloud once output:"
sed -n '1,140p' "$CLOUD_LOG" || true
echo "ROS2 cmd_vel control output:"
sed -n '1,200p' "$CONTROL_LOG" || true
echo "ROS2 odom once output:"
sed -n '1,140p' "$ODOM_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|lidar|cmd_vel|odom)|/tf' "$TOPIC_LOG" || true
echo "Unity Scout wheel-ground candidate result:"
sed -n '1,220p' "$RESULT_FILE" 2>/dev/null || true
echo "Unity control result:"
sed -n '1,220p' "$CONTROL_RESULT_FILE" 2>/dev/null || true
echo "Unity wheel-ground controller result:"
sed -n '1,220p' "$CONTROLLER_RESULT_FILE" 2>/dev/null || true
echo "Unity follow transform result:"
sed -n '1,160p' "$FOLLOW_RESULT_FILE" 2>/dev/null || true
echo "Unity odom publisher result:"
sed -n '1,220p' "$ODOM_RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_OFFROAD_SCOUT_WHEEL_GROUND|VLN_SCOUT_WHEEL_GROUND|VLN_SCOUT_WHEEL_GROUND_VISUAL|VLN_CMD_VEL|VLN_ODOM|Incompatible protocol|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,520p' || true

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

if ! grep -F -q "$CMD_VEL_TOPIC [geometry_msgs/msg/Twist]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_cmd_vel_topic"
  exit 1
fi

if ! grep -F -q "$ODOM_TOPIC [nav_msgs/msg/Odometry]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_odom_topic"
  exit 1
fi

if [ ! -s "$SCREENSHOT_FILE" ]; then
  echo "scout_wheel_ground_candidate_screenshot_missing"
  exit 1
fi

if [ "${wheel_collider_count:-0}" -lt 4 ]; then
  echo "scout_wheel_ground_missing_wheel_colliders"
  exit 1
fi

if [ "${broad_physical_trail_count:-999}" -ne 0 ]; then
  echo "broad_physical_trail_must_not_exist"
  exit 1
fi

if [ "${road_physical_slab_count:-0}" -lt 7 ]; then
  echo "localized_road_physical_slabs_too_few"
  exit 1
fi
if [ "${road_seam_transition_count:-0}" -lt 5 ]; then
  echo "localized_road_seam_transitions_too_few"
  exit 1
fi
if [ "${bridge_physics_count:-0}" -lt 3 ]; then
  echo "bridge_physics_missing"
  exit 1
fi

if [ "${short_ramp_physics_count:-0}" -lt 1 ]; then
  echo "short_ramp_physics_missing"
  exit 1
fi

if [ "${decorative_trail_collider_count:-999}" -ne 0 ]; then
  echo "decorative_trail_colliders_should_be_replaced"
  exit 1
fi
if [ "${decorative_bridge_renderer_count:-999}" -ne 0 ]; then
  echo "decorative_bridge_visual_must_not_shadow_physical_bridge"
  exit 1
fi
if [ "${bridge_deck_has_renderer:-0}" -ne 1 ]; then
  echo "physical_bridge_deck_must_be_visible"
  exit 1
fi
if [ "${bridge_deck_has_collider:-0}" -ne 1 ]; then
  echo "physical_bridge_deck_must_have_collider"
  exit 1
fi
if ! awk "BEGIN { exit !(${bridge_deck_renderer_collider_top_delta:-999} <= 0.01) }"; then
  echo "bridge_deck_renderer_collider_misaligned"
  exit 1
fi
if [ "${bridge_visual_detail_count:-0}" -lt 40 ]; then
  echo "bridge_visual_detail_too_simple"
  exit 1
fi
if ! awk "BEGIN { exit !(${bridge_physical_height_span:-0} >= 0.20) }"; then
  echo "bridge_too_flat"
  exit 1
fi

if ! awk "BEGIN { exit !(${road_physical_max_width:-999} <= 7.1) }"; then
  echo "road_physical_width_too_broad"
  exit 1
fi

if ! awk "BEGIN { exit !(${bridge_physical_max_width:-999} <= 2.6) }"; then
  echo "bridge_physical_width_too_broad_or_bypass_like"
  exit 1
fi

if ! awk "BEGIN { exit !(${short_ramp_physical_max_width:-0} >= 4.5 && ${short_ramp_physical_max_width:-999} <= 5.4) }"; then
  echo "short_ramp_physical_width_not_matching_original_ramp"
  exit 1
fi
if [ "${short_ramp_visual_detail_count:-0}" -lt 5 ]; then
  echo "short_ramp_visual_detail_missing"
  exit 1
fi
if ! awk "BEGIN { exit !(${short_ramp_physical_height_span:-0} >= 0.62) }"; then
  echo "short_ramp_too_flat"
  exit 1
fi

if [ "${visual_renderer_count:-0}" -lt 5 ]; then
  echo "scout_wheel_ground_visual_renderers_missing"
  exit 1
fi

if [ "${visual_collider_count:-1}" -ne 0 ]; then
  echo "scout_wheel_ground_visual_colliders_should_be_stripped"
  exit 1
fi

if [ "${visual_articulation_body_count:-1}" -ne 0 ]; then
  echo "scout_wheel_ground_visual_articulation_bodies_should_be_stripped"
  exit 1
fi

if [ "${topgear_visual_present:-0}" -ne 1 ]; then
  echo "topgear_v2_visual_missing"
  exit 1
fi

if [ "${topgear_visual_renderer_count:-0}" -lt 1 ]; then
  echo "topgear_v2_visual_renderer_missing"
  exit 1
fi

if [ "${topgear_visual_collider_count:-1}" -ne 0 ]; then
  echo "topgear_v2_visual_must_not_add_colliders"
  exit 1
fi

if [ "${topgear_visual_rigidbody_count:-1}" -ne 0 ]; then
  echo "topgear_v2_visual_must_not_add_rigidbodies"
  exit 1
fi

if [ "${cmd_vel_count:-0}" -lt 20 ]; then
  echo "unity_tf_publisher_cmd_vel_count_too_low"
  exit 1
fi

if [ "${controller_cmd_count:-0}" -lt 20 ]; then
  echo "scout_wheel_ground_controller_cmd_count_too_low"
  exit 1
fi

if [ "${physics_step_count:-0}" -lt 100 ]; then
  echo "scout_wheel_ground_physics_step_count_too_low"
  exit 1
fi

if [ "${motor_command_count:-0}" -lt 20 ]; then
  echo "scout_wheel_ground_motor_command_count_too_low"
  exit 1
fi

if ! awk "BEGIN { exit !(${wheel_visual_total_abs_roll_deg:-0} >= 300.0) }"; then
  echo "wheel_visual_roll_not_accumulating"
  exit 1
fi
if [ "${wheel_visual_direction_reversal_count:-999}" -gt 2 ]; then
  echo "wheel_visual_direction_flapping"
  exit 1
fi

if [ "${follow_update_count:-0}" -lt 100 ]; then
  echo "sensor_rig_follow_update_count_too_low"
  exit 1
fi

if [ "${odom_publish_count:-0}" -lt 20 ]; then
  echo "odom_publish_count_too_low"
  exit 1
fi

if ! grep -q '^motion_source=wheel_ground_contact_not_kinematic_rig$' "$RESULT_FILE"; then
  echo "wheel_ground_motion_source_marker_missing"
  exit 1
fi

echo "VLN_SCOUT_WHEEL_GROUND_SMOKE_TEST_PASS"
