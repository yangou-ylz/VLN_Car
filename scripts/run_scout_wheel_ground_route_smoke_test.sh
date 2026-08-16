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
CONTROLLER_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_wheel_ground_controller_result.txt"
ROUTE_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_physics_route_demo_result.txt"

IMAGE_TOPIC="/vln/front/image_raw"
POINTS_TOPIC="/vln/lidar/points"
TF_TOPIC="/tf"
CMD_VEL_TOPIC="/vln/cmd_vel"
ODOM_TOPIC="/vln/odom"
RELATIVE_WAYPOINTS="${RELATIVE_WAYPOINTS:-4.0,0.0;8.0,0.0;12.0,0.0;15.0,0.0;18.0,0.0;22.0,0.0;28.0,0.0;34.0,0.0;42.0,0.0;50.0,0.0;54.0,0.0}"
ROUTE_EXTRA_ARGS="${ROUTE_EXTRA_ARGS:---progress-only-gates --skip-stalled-waypoints --skip-angular-calibration --angular-sign -1 --max-angular 0.42 --angular-gain 0.62 --max-linear 1.35 --linear-gain 0.75 --linear-accel 0.95 --angular-accel 0.28 --min-linear-while-turning 0.65 --stall-skip-seconds 7.0 --stall-skip-forward-margin 4.0}"

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

for old_file in "$RESULT_FILE" "$SCREENSHOT_FILE" "$CONTROLLER_RESULT_FILE" "$ROUTE_RESULT_FILE"; do
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

timeout 230s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_drive_scout_physics_route.py --cmd-topic $CMD_VEL_TOPIC --tf-topic $TF_TOPIC --odom-topic $ODOM_TOPIC --relative-waypoints '$RELATIVE_WAYPOINTS' --timeout 220 --goal-tolerance 2.00 --gate-tolerance 3.50 --min-reached 9 --min-total-progress 44.0 $ROUTE_EXTRA_ARGS" >"$ROUTE_LOG" 2>&1 &
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

reached_count=$(grep -E '^reached_count=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
total_progress=$(grep -E '^total_progress=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
total_forward_progress=$(grep -E '^total_forward_progress=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
stall_count=$(grep -E '^stall_count=' "$ROUTE_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
controller_cmd_count=$(grep -E '^cmd_vel_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
motor_command_count=$(grep -E '^motor_command_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)

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
  echo "reached_count=${reached_count:-0}"
  echo "total_progress=${total_progress:-0}"
  echo "total_forward_progress=${total_forward_progress:-0}"
  echo "stall_count=${stall_count:-0}"
  echo "controller_cmd_count=${controller_cmd_count:-0}"
  echo "motor_command_count=${motor_command_count:-0}"
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
if [ "${controller_cmd_count:-0}" -lt 20 ]; then
  echo "scout_route_controller_cmd_count_too_low"
  exit 1
fi
if [ "${motor_command_count:-0}" -lt 20 ]; then
  echo "scout_route_motor_command_count_too_low"
  exit 1
fi

echo "VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS"
