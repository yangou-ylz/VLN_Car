#!/usr/bin/env bash

# 阶段 21：第一套 Mesa 世界接入 Topgear 后的 ROS2 /vln/cmd_vel 控制闭环验收。
# 会启动 ROS-TCP-Endpoint，batch 打开 Mesa+Topgear 场景，验证四路相机、LiDAR、odom、TF 和短距离速度控制。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
RUN_ID="vln_mesa_topgear_vehicle_cmd_vel_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
FRONT_IMAGE_LOG="$LOG_DIR/ros2_front_image_once.log"
REAR_IMAGE_LOG="$LOG_DIR/ros2_rear_image_once.log"
LEFT_IMAGE_LOG="$LOG_DIR/ros2_left_image_once.log"
RIGHT_IMAGE_LOG="$LOG_DIR/ros2_right_image_once.log"
CLOUD_LOG="$LOG_DIR/ros2_pointcloud2_once.log"
ODOM_LOG="$LOG_DIR/ros2_odom_once.log"
CONTROL_LOG="$LOG_DIR/ros2_cmd_vel_control.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_mesa_topgear_vehicle_candidate_result.txt"
SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_mesa_topgear_vehicle_candidate_screenshot.png"
CONTROLLER_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_wheel_ground_controller_result.txt"
FOLLOW_RESULT_FILE="$UNITY_PROJECT/Logs/vln_follow_transform_pose_result.txt"
ODOM_RESULT_FILE="$UNITY_PROJECT/Logs/vln_odom_publisher_result.txt"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

echo "Mesa + Topgear ROS2 /vln/cmd_vel smoke test：验证短距离速度控制和传感器链路。" | tee "$LOG_DIR/run_summary.txt"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee -a "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前大资产副本 Unity Editor，再运行该自动验收脚本。"
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

for old_file in "$RESULT_FILE" "$SCREENSHOT_FILE" "$CONTROLLER_RESULT_FILE" "$FOLLOW_RESULT_FILE" "$ODOM_RESULT_FILE"; do
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

timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic /vln/front/image_raw --width 640 --height 480 --encoding rgb8 --frame-id front_camera_optical_frame --timeout 100" >"$FRONT_IMAGE_LOG" 2>&1 &
front_image_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic /vln/rear/image_raw --width 640 --height 480 --encoding rgb8 --frame-id rear_camera_optical_frame --timeout 100" >"$REAR_IMAGE_LOG" 2>&1 &
rear_image_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic /vln/left/image_raw --width 640 --height 480 --encoding rgb8 --frame-id left_camera_optical_frame --timeout 100" >"$LEFT_IMAGE_LOG" 2>&1 &
left_image_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic /vln/right/image_raw --width 640 --height 480 --encoding rgb8 --frame-id right_camera_optical_frame --timeout 100" >"$RIGHT_IMAGE_LOG" 2>&1 &
right_image_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic /vln/lidar/points --width 7200 --point-step 16 --frame-id lidar_link --timeout 100 --min-nonzero-points 80" >"$CLOUD_LOG" 2>&1 &
cloud_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_odom_once.py --topic /vln/odom --frame-id map --child-frame-id base_link --timeout 100" >"$ODOM_LOG" 2>&1 &
odom_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_send_cmd_vel_and_wait_tf.py --cmd-topic /vln/cmd_vel --tf-topic /tf --odom-topic /vln/odom --linear-x 0.70 --angular-z 0.0 --duration 5.5 --timeout 100 --min-delta 0.30 --min-yaw-delta 0.0 --min-odom-delta 0.30 --min-odom-yaw-delta 0.0" >"$CONTROL_LOG" 2>&1 &
control_pid=$!
bash -lc "sleep 14; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

set +e
timeout 145s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnMesaTopgearVehicleCandidateBuilder.RunBuildAndCmdVelSmokeTest \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$front_image_pid" "$rear_image_pid" "$left_image_pid" "$right_image_pid" "$cloud_pid" "$odom_pid" "$control_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$front_image_pid"; front_image_status=$?
wait "$rear_image_pid"; rear_image_status=$?
wait "$left_image_pid"; left_image_status=$?
wait "$right_image_pid"; right_image_status=$?
wait "$cloud_pid"; cloud_status=$?
wait "$odom_pid"; odom_status=$?
wait "$control_pid"; control_status=$?
wait "$topic_pid"; topic_status=$?
set -e

for current_file in "$RESULT_FILE" "$SCREENSHOT_FILE" "$CONTROLLER_RESULT_FILE" "$FOLLOW_RESULT_FILE" "$ODOM_RESULT_FILE"; do
  if [ -f "$current_file" ]; then
    cp "$current_file" "$LOG_DIR/$(basename "$current_file")"
  fi
done

success=$(grep -E '^success=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
controller_cmd_count=$(grep -E '^cmd_vel_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
motor_command_count=$(grep -E '^motor_command_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
sand_contact_steps=$(grep -E '^sand_contact_steps=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
terrain_contact_steps=$(grep -E '^terrain_contact_steps=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
physics_root_delta=$(grep -E '^physics_root_delta_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "front_image_status=$front_image_status"
  echo "rear_image_status=$rear_image_status"
  echo "left_image_status=$left_image_status"
  echo "right_image_status=$right_image_status"
  echo "cloud_status=$cloud_status"
  echo "odom_status=$odom_status"
  echo "control_status=$control_status"
  echo "topic_status=$topic_status"
  echo "success=${success:-missing}"
  echo "controller_cmd_count=${controller_cmd_count:-missing}"
  echo "motor_command_count=${motor_command_count:-missing}"
  echo "sand_contact_steps=${sand_contact_steps:-missing}"
  echo "terrain_contact_steps=${terrain_contact_steps:-missing}"
  echo "physics_root_delta_m=${physics_root_delta:-missing}"
  echo "log_dir=$LOG_DIR"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 front/rear/left/right image outputs:"
sed -n '1,80p' "$FRONT_IMAGE_LOG" || true
sed -n '1,80p' "$REAR_IMAGE_LOG" || true
sed -n '1,80p' "$LEFT_IMAGE_LOG" || true
sed -n '1,80p' "$RIGHT_IMAGE_LOG" || true
echo "ROS2 pointcloud output:"
sed -n '1,120p' "$CLOUD_LOG" || true
echo "ROS2 odom output:"
sed -n '1,120p' "$ODOM_LOG" || true
echo "ROS2 cmd_vel control output:"
sed -n '1,180p' "$CONTROL_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|rear|left|right|lidar|cmd_vel|odom)|/tf' "$TOPIC_LOG" || true
echo "Unity Mesa Topgear result:"
sed -n '1,180p' "$RESULT_FILE" 2>/dev/null || true
echo "Unity wheel-ground controller result:"
sed -n '1,220p' "$CONTROLLER_RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_MESA_TOPGEAR|VLN_SCOUT_WHEEL_GROUND|VLN_ODOM|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,360p' || true

if [ "$unity_status" -ne 0 ]; then echo "unity_failed"; exit 1; fi
if [ "$front_image_status" -ne 0 ] || [ "$rear_image_status" -ne 0 ] || [ "$left_image_status" -ne 0 ] || [ "$right_image_status" -ne 0 ]; then echo "ros2_camera_image_validation_failed"; exit 1; fi
if [ "$cloud_status" -ne 0 ]; then echo "ros2_pointcloud2_validation_failed"; exit 1; fi
if [ "$odom_status" -ne 0 ]; then echo "ros2_odom_validation_failed"; exit 1; fi
if [ "$control_status" -ne 0 ]; then echo "ros2_cmd_vel_control_validation_failed"; exit 1; fi
if [ "${success:-0}" != "1" ]; then echo "mesa_topgear_vehicle_runtime_smoke_failed"; exit 1; fi
if [ "${controller_cmd_count:-0}" -lt 1 ]; then echo "unity_controller_missing_cmd_vel"; exit 1; fi
if [ "${motor_command_count:-0}" -lt 1 ]; then echo "unity_controller_missing_motor_commands"; exit 1; fi

echo "VLN_MESA_TOPGEAR_VEHICLE_CMD_VEL_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
