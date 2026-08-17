#!/usr/bin/env bash

# 阶段 15：Scout wheel-ground 固定路线自动验收。
# 验收内容：ROS2 路线控制脚本驱动物理车体从起点跑向桥、坡和终点方向，感知、TF、odom 仍在线。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_scout_wheel_ground_route_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
IMAGE_LOG="$LOG_DIR/ros2_image_once.log"
CLOUD_LOG="$LOG_DIR/ros2_pointcloud2_once.log"
ODOM_LOG="$LOG_DIR/ros2_odom_once.log"
ROUTE_LOG="$LOG_DIR/ros2_scout_physics_route.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_candidate_result.txt"
SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_candidate_screenshot.png"
BRIDGE_SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_bridge_screenshot.png"
SHORT_RAMP_SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_short_ramp_screenshot.png"
CONTROLLER_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_wheel_ground_controller_result.txt"
ROUTE_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_physics_route_demo_result.txt"

IMAGE_TOPIC="/vln/front/image_raw"
POINTS_TOPIC="/vln/lidar/points"
TF_TOPIC="/tf"
CMD_VEL_TOPIC="/vln/cmd_vel"
ODOM_TOPIC="/vln/odom"
RELATIVE_WAYPOINTS="${RELATIVE_WAYPOINTS:-4.0,0.0;8.0,0.0;12.0,0.0;15.0,0.0;18.0,0.0;22.0,0.0;26.0,0.0;28.0,0.0;30.0,0.0;34.0,0.0;42.0,0.0;50.0,0.0;54.0,0.0}"
ROUTE_EXTRA_ARGS="${ROUTE_EXTRA_ARGS:---centerline-corridor --centerline-forward-max 22.8 --progress-only-gates --skip-angular-calibration --angular-sign 1 --lookahead-distance 5.00 --corridor-lateral-gain 0.28 --corridor-max-heading-correction 0.32 --max-angular 0.55 --angular-gain 0.70 --max-linear 1.05 --linear-gain 0.62 --linear-accel 0.70 --angular-accel 0.30 --min-linear-while-turning 0.38 --max-lateral-offset 1.15 --max-final-lateral-offset 0.80 --max-bridge-lateral-offset 0.85 --bridge-forward-min 9.5 --bridge-forward-max 22.8 --stall-skip-seconds 12.0 --stall-skip-forward-margin 4.0}"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前 Unity Editor，再运行本自动验收脚本。同一工程不能被两个 Editor 实例同时打开。"
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

for old_file in "$RESULT_FILE" "$SCREENSHOT_FILE" "$BRIDGE_SCREENSHOT_FILE" "$SHORT_RAMP_SCREENSHOT_FILE" "$CONTROLLER_RESULT_FILE" "$ROUTE_RESULT_FILE"; do
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

timeout 150s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic $IMAGE_TOPIC --width 640 --height 480 --encoding rgb8 --frame-id front_camera_optical_frame --timeout 145" >"$IMAGE_LOG" 2>&1 &
image_pid=$!

timeout 150s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic $POINTS_TOPIC --width 7200 --point-step 16 --frame-id lidar_link --timeout 145 --min-nonzero-points 80" >"$CLOUD_LOG" 2>&1 &
cloud_pid=$!

timeout 150s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_odom_once.py --topic $ODOM_TOPIC --frame-id map --child-frame-id base_link --timeout 145" >"$ODOM_LOG" 2>&1 &
odom_pid=$!

timeout 240s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_drive_scout_physics_route.py --cmd-topic $CMD_VEL_TOPIC --tf-topic $TF_TOPIC --odom-topic $ODOM_TOPIC --relative-waypoints '$RELATIVE_WAYPOINTS' --timeout 230 --goal-tolerance 1.60 --gate-tolerance 0.95 --min-reached 13 --min-total-progress 44.0 $ROUTE_EXTRA_ARGS" >"$ROUTE_LOG" 2>&1 &
route_pid=$!

bash -lc "sleep 14; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

set +e
timeout 285s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadScoutWheelGroundRouteSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$image_pid" "$cloud_pid" "$odom_pid" "$route_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$image_pid"
image_status=$?
wait "$cloud_pid"
cloud_status=$?
wait "$odom_pid"
odom_status=$?
wait "$route_pid"
route_status=$?
wait "$topic_pid"
topic_status=$?
set -e

if [ -f "$SCREENSHOT_FILE" ]; then
  cp "$SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_scout_wheel_ground_candidate_screenshot.png"
fi
if [ -f "$BRIDGE_SCREENSHOT_FILE" ]; then
  cp "$BRIDGE_SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_scout_wheel_ground_bridge_screenshot.png"
fi
if [ -f "$SHORT_RAMP_SCREENSHOT_FILE" ]; then
  cp "$SHORT_RAMP_SCREENSHOT_FILE" "$LOG_DIR/vln_offroad_scout_wheel_ground_short_ramp_screenshot.png"
fi

for current_file in "$RESULT_FILE" "$CONTROLLER_RESULT_FILE" "$ROUTE_RESULT_FILE"; do
  if [ -f "$current_file" ]; then
    cp "$current_file" "$LOG_DIR/$(basename "$current_file")"
  fi
done

reached_count=$(grep -E '^reached_count=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
route_waypoint_count=$(grep -E '^route_waypoint_count=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
total_progress=$(grep -E '^total_progress=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
total_forward_progress=$(grep -E '^total_forward_progress=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
final_lateral_offset=$(grep -E '^final_lateral_offset=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
max_reached_cross_track=$(grep -E '^max_reached_cross_track=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
max_abs_lateral_offset=$(grep -E '^max_abs_lateral_offset=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
max_bridge_abs_lateral_offset=$(grep -E '^max_bridge_abs_lateral_offset=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
stall_count=$(grep -E '^stall_count=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
skipped_count=$(grep -E '^skipped_count=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
broad_physical_trail_count=$(grep -E '^broad_physical_trail_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
road_physical_slab_count=$(grep -E '^road_physical_slab_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
road_seam_transition_count=$(grep -E '^road_seam_transition_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_physics_count=$(grep -E '^bridge_physics_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
short_ramp_physics_count=$(grep -E '^short_ramp_physics_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_visual_detail_count=$(grep -E '^bridge_visual_detail_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_rail_collider_count=$(grep -E '^bridge_rail_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
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
controller_cmd_count=$(grep -E '^cmd_vel_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
motor_command_count=$(grep -E '^motor_command_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
road_contact_steps=$(grep -E '^road_contact_steps=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
bridge_contact_steps=$(grep -E '^bridge_contact_steps=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
short_ramp_contact_steps=$(grep -E '^short_ramp_contact_steps=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
body_height_span=$(grep -E '^body_height_span_m=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
wheel_ground_height_span=$(grep -E '^wheel_ground_height_span_m=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
wheel_visual_total_abs_roll_deg=$(grep -E '^wheel_visual_total_abs_roll_deg=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
wheel_visual_direction_reversal_count=$(grep -E '^wheel_visual_direction_reversal_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "image_status=$image_status"
  echo "cloud_status=$cloud_status"
  echo "odom_status=$odom_status"
  echo "route_status=$route_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "relative_waypoints=$RELATIVE_WAYPOINTS"
  echo "route_result_file=$ROUTE_RESULT_FILE"
  echo "bridge_screenshot_file=${BRIDGE_SCREENSHOT_FILE}"
  echo "short_ramp_screenshot_file=${SHORT_RAMP_SCREENSHOT_FILE}"
  echo "reached_count=${reached_count:-0}"
  echo "route_waypoint_count=${route_waypoint_count:-0}"
  echo "total_progress=${total_progress:-0}"
  echo "total_forward_progress=${total_forward_progress:-0}"
  echo "final_lateral_offset=${final_lateral_offset:-missing}"
  echo "max_reached_cross_track=${max_reached_cross_track:-missing}"
  echo "max_abs_lateral_offset=${max_abs_lateral_offset:-missing}"
  echo "max_bridge_abs_lateral_offset=${max_bridge_abs_lateral_offset:-missing}"
  echo "stall_count=${stall_count:-0}"
  echo "skipped_count=${skipped_count:-0}"
  echo "broad_physical_trail_count=${broad_physical_trail_count:-missing}"
  echo "road_physical_slab_count=${road_physical_slab_count:-missing}"
  echo "road_seam_transition_count=${road_seam_transition_count:-missing}"
  echo "bridge_physics_count=${bridge_physics_count:-missing}"
  echo "short_ramp_physics_count=${short_ramp_physics_count:-missing}"
  echo "bridge_visual_detail_count=${bridge_visual_detail_count:-missing}"
  echo "bridge_rail_collider_count=${bridge_rail_collider_count:-missing}"
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
  echo "controller_cmd_count=${controller_cmd_count:-0}"
  echo "motor_command_count=${motor_command_count:-0}"
  echo "road_contact_steps=${road_contact_steps:-0}"
  echo "bridge_contact_steps=${bridge_contact_steps:-0}"
  echo "short_ramp_contact_steps=${short_ramp_contact_steps:-0}"
  echo "body_height_span_m=${body_height_span:-0}"
  echo "wheel_ground_height_span_m=${wheel_ground_height_span:-0}"
  echo "wheel_visual_total_abs_roll_deg=${wheel_visual_total_abs_roll_deg:-0}"
  echo "wheel_visual_direction_reversal_count=${wheel_visual_direction_reversal_count:-0}"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 image once output:"
sed -n '1,100p' "$IMAGE_LOG" || true
echo "ROS2 pointcloud once output:"
sed -n '1,120p' "$CLOUD_LOG" || true
echo "ROS2 odom once output:"
sed -n '1,120p' "$ODOM_LOG" || true
echo "ROS2 Scout fixed route output:"
sed -n '1,260p' "$ROUTE_LOG" || true
echo "Route result file:"
sed -n '1,220p' "$ROUTE_RESULT_FILE" 2>/dev/null || true
echo "Unity Scout wheel-ground candidate result:"
sed -n '1,180p' "$RESULT_FILE" 2>/dev/null || true
echo "Unity wheel-ground controller result:"
sed -n '1,220p' "$CONTROLLER_RESULT_FILE" 2>/dev/null || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|lidar|cmd_vel|odom)|/tf' "$TOPIC_LOG" || true
echo "Key Unity log lines:"
grep -n -E "VLN_OFFROAD_SCOUT_WHEEL_GROUND|VLN_SCOUT_WHEEL_GROUND|VLN_CMD_VEL|VLN_ODOM|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,520p' || true

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
if [ "$odom_status" -ne 0 ]; then
  echo "ros2_odom_message_validation_failed"
  exit 1
fi
if [ "$route_status" -ne 0 ]; then
  echo "ros2_scout_physics_route_validation_failed"
  exit 1
fi
if ! grep -q 'VLN_SCOUT_PHYSICS_ROUTE_MSG_OK' "$ROUTE_LOG"; then
  echo "ros2_scout_physics_route_missing_success_marker"
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
if ! grep -q 'VLN_ODOM_MSG_OK' "$ODOM_LOG"; then
  echo "ros2_odom_message_missing_success_marker"
  exit 1
fi
if [ ! -s "$LOG_DIR/vln_offroad_scout_wheel_ground_bridge_screenshot.png" ]; then
  echo "bridge_visual_evidence_screenshot_missing"
  exit 1
fi
if [ ! -s "$LOG_DIR/vln_offroad_scout_wheel_ground_short_ramp_screenshot.png" ]; then
  echo "short_ramp_visual_evidence_screenshot_missing"
  exit 1
fi
if [ "${controller_cmd_count:-0}" -lt 20 ]; then
  echo "scout_route_controller_cmd_count_too_low"
  exit 1
fi
if [ "${motor_command_count:-0}" -lt 20 ]; then
  echo "scout_route_motor_command_count_too_low"
  exit 1
fi
if [ "${route_waypoint_count:-0}" -ne 13 ]; then
  echo "scout_route_waypoint_count_unexpected"
  exit 1
fi
if [ "${reached_count:-0}" -ne "${route_waypoint_count:-11}" ]; then
  echo "scout_route_must_reach_every_waypoint_without_shortcut"
  exit 1
fi
if [ "${stall_count:-0}" -ne 0 ]; then
  echo "scout_route_stall_count_must_be_zero"
  exit 1
fi
if [ "${skipped_count:-0}" -ne 0 ]; then
  echo "scout_route_skipped_count_must_be_zero"
  exit 1
fi
if ! awk "BEGIN { exit !(${max_reached_cross_track:-999} <= 0.95) }"; then
  echo "scout_route_reached_cross_track_too_large"
  exit 1
fi
if ! awk "BEGIN { offset=${final_lateral_offset:-999}; if (offset < 0) offset = -offset; exit !(offset <= 0.80) }"; then
  echo "scout_route_final_lateral_offset_too_large"
  exit 1
fi
if ! awk "BEGIN { exit !(${max_abs_lateral_offset:-999} <= 1.15) }"; then
  echo "scout_route_max_lateral_offset_too_large"
  exit 1
fi
if ! awk "BEGIN { exit !(${max_bridge_abs_lateral_offset:-999} <= 0.85) }"; then
  echo "scout_route_bridge_lateral_offset_too_large"
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
if [ "${bridge_rail_collider_count:-0}" -lt 30 ]; then
  echo "bridge_rail_colliders_missing"
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
if [ "${road_contact_steps:-0}" -lt 20 ]; then
  echo "road_contact_steps_too_low"
  exit 1
fi
if [ "${bridge_contact_steps:-0}" -lt 10 ]; then
  echo "bridge_contact_steps_missing"
  exit 1
fi
if [ "${short_ramp_contact_steps:-0}" -lt 10 ]; then
  echo "short_ramp_contact_steps_missing"
  exit 1
fi
if ! awk "BEGIN { exit !(${wheel_ground_height_span:-0} >= 0.12) }"; then
  echo "wheel_ground_height_span_too_flat_for_bridge_or_ramp_interaction"
  exit 1
fi
if ! awk "BEGIN { exit !(${wheel_visual_total_abs_roll_deg:-0} >= 2000.0) }"; then
  echo "wheel_visual_roll_not_accumulating"
  exit 1
fi
if [ "${wheel_visual_direction_reversal_count:-999}" -gt 8 ]; then
  echo "wheel_visual_direction_flapping"
  exit 1
fi

echo "VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS"
