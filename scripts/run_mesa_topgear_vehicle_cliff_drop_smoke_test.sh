#!/usr/bin/env bash

# 阶段 21：第一套 Mesa 世界里 Topgear 真实物理车的岩壁/悬崖下落专项验收。
# 只测试真实地形和场景 collider，不跑自动导航路线，不创建隐藏托底或假墙。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
RUN_ID="vln_mesa_topgear_vehicle_cliff_drop_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
CONTROL_LOG="$LOG_DIR/ros2_cmd_vel_cliff_drop.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_mesa_topgear_vehicle_candidate_result.txt"
CLIFF_RESULT_FILE="$UNITY_PROJECT/Logs/vln_mesa_topgear_vehicle_cliff_drop_result.txt"
CONTROLLER_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_wheel_ground_controller_result.txt"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

echo "Mesa + Topgear 悬崖下落 smoke test：真实 Mesa 地形/岩壁 collider，不跑自动路线。" | tee "$LOG_DIR/run_summary.txt"

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

for old_file in "$RESULT_FILE" "$CLIFF_RESULT_FILE" "$CONTROLLER_RESULT_FILE"; do
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

timeout 125s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_send_cmd_vel_and_wait_tf.py --cmd-topic /vln/cmd_vel --tf-topic /tf --odom-topic /vln/odom --linear-x 1.05 --angular-z 0.0 --duration 9.0 --timeout 120 --min-delta 1.20 --min-yaw-delta 0.0 --min-odom-delta 1.20 --min-odom-yaw-delta 0.0" >"$CONTROL_LOG" 2>&1 &
control_pid=$!

set +e
timeout 150s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnMesaTopgearVehicleCandidateBuilder.RunBuildAndCliffDropSmokeTest \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$control_pid" >/dev/null 2>&1 || true
fi
wait "$control_pid"; control_status=$?
set -e

for current_file in "$RESULT_FILE" "$CLIFF_RESULT_FILE" "$CONTROLLER_RESULT_FILE"; do
  if [ -f "$current_file" ]; then
    cp "$current_file" "$LOG_DIR/$(basename "$current_file")"
  fi
done

cliff_success=$(grep -E '^success=' "$CLIFF_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
height_drop=$(grep -E '^height_drop_m=' "$CLIFF_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
horizontal_delta=$(grep -E '^horizontal_delta_m=' "$CLIFF_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
slow_steep=$(grep -E '^slow_steep_contact_steps=' "$CLIFF_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
max_roll=$(grep -E '^max_roll_abs_deg=' "$CLIFF_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
max_pitch=$(grep -E '^max_pitch_abs_deg=' "$CLIFF_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
controller_cmd_count=$(grep -E '^cmd_vel_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "control_status=$control_status"
  echo "cliff_success=${cliff_success:-missing}"
  echo "height_drop_m=${height_drop:-missing}"
  echo "horizontal_delta_m=${horizontal_delta:-missing}"
  echo "slow_steep_contact_steps=${slow_steep:-missing}"
  echo "max_roll_abs_deg=${max_roll:-missing}"
  echo "max_pitch_abs_deg=${max_pitch:-missing}"
  echo "controller_cmd_count=${controller_cmd_count:-missing}"
  echo "log_dir=$LOG_DIR"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 cmd_vel cliff output:"
sed -n '1,180p' "$CONTROL_LOG" || true
echo "Unity cliff drop result:"
sed -n '1,180p' "$CLIFF_RESULT_FILE" 2>/dev/null || true
echo "Unity wheel-ground controller result:"
sed -n '1,220p' "$CONTROLLER_RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_MESA_TOPGEAR|VLN_SCOUT_WHEEL_GROUND|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,340p' || true

if [ "$unity_status" -ne 0 ]; then echo "unity_failed"; exit 1; fi
if [ "$control_status" -ne 0 ]; then echo "ros2_cmd_vel_control_validation_failed"; exit 1; fi
if [ "${cliff_success:-0}" != "1" ]; then echo "mesa_topgear_cliff_drop_failed"; exit 1; fi
if [ "${controller_cmd_count:-0}" -lt 1 ]; then echo "unity_controller_missing_cmd_vel"; exit 1; fi

echo "VLN_MESA_TOPGEAR_VEHICLE_CLIFF_DROP_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
