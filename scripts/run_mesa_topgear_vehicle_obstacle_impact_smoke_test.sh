#!/usr/bin/env bash

# 阶段 21：第一套 Mesa 世界里 Topgear 真实物理车撞真实场景障碍物的反馈验收。
# 使用真实 Mesa collider，不创建假墙；外部仍通过 ROS2 /vln/cmd_vel 给速度。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
RUN_ID="vln_mesa_topgear_vehicle_obstacle_impact_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
CONTROL_LOG="$LOG_DIR/ros2_cmd_vel_obstacle_impact.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_mesa_topgear_vehicle_candidate_result.txt"
IMPACT_RESULT_FILE="$UNITY_PROJECT/Logs/vln_mesa_topgear_vehicle_obstacle_impact_result.txt"
CONTROLLER_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_wheel_ground_controller_result.txt"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

echo "Mesa + Topgear 障碍物撞击 smoke test：真实 Mesa 障碍物 collider，不加假墙。" | tee "$LOG_DIR/run_summary.txt"

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

for old_file in "$RESULT_FILE" "$IMPACT_RESULT_FILE" "$CONTROLLER_RESULT_FILE"; do
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

timeout 125s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_send_cmd_vel_and_wait_tf.py --cmd-topic /vln/cmd_vel --tf-topic /tf --odom-topic /vln/odom --linear-x 0.85 --angular-z 0.0 --duration 12.0 --timeout 120 --min-delta 0.50 --min-yaw-delta 0.0 --min-odom-delta 0.50 --min-odom-yaw-delta 0.0" >"$CONTROL_LOG" 2>&1 &
control_pid=$!

set +e
timeout 160s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnMesaTopgearVehicleCandidateBuilder.RunBuildAndObstacleImpactSmokeTest \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$control_pid" >/dev/null 2>&1 || true
fi
wait "$control_pid"; control_status=$?
set -e

for current_file in "$RESULT_FILE" "$IMPACT_RESULT_FILE" "$CONTROLLER_RESULT_FILE"; do
  if [ -f "$current_file" ]; then
    cp "$current_file" "$LOG_DIR/$(basename "$current_file")"
  fi
done

impact_success=$(grep -E '^success=' "$IMPACT_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
collision_enter_count=$(grep -E '^collision_enter_count=' "$IMPACT_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
collision_stay_count=$(grep -E '^collision_stay_count=' "$IMPACT_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
unique_obstacles=$(grep -E '^unique_obstacle_collision_count=' "$IMPACT_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
target_name=$(grep -E '^target_name=' "$IMPACT_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2-)
controller_cmd_count=$(grep -E '^cmd_vel_count=' "$CONTROLLER_RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "control_status=$control_status"
  echo "impact_success=${impact_success:-missing}"
  echo "target_name=${target_name:-missing}"
  echo "collision_enter_count=${collision_enter_count:-missing}"
  echo "collision_stay_count=${collision_stay_count:-missing}"
  echo "unique_obstacle_collision_count=${unique_obstacles:-missing}"
  echo "controller_cmd_count=${controller_cmd_count:-missing}"
  echo "log_dir=$LOG_DIR"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 cmd_vel obstacle output:"
sed -n '1,180p' "$CONTROL_LOG" || true
echo "Unity obstacle impact result:"
sed -n '1,180p' "$IMPACT_RESULT_FILE" 2>/dev/null || true
echo "Unity wheel-ground controller result:"
sed -n '1,220p' "$CONTROLLER_RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_MESA_TOPGEAR|VLN_SCOUT_WHEEL_GROUND|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,340p' || true

if [ "$unity_status" -ne 0 ]; then echo "unity_failed"; exit 1; fi
if [ "$control_status" -ne 0 ]; then echo "ros2_cmd_vel_control_validation_failed"; exit 1; fi
if [ "${impact_success:-0}" != "1" ]; then echo "mesa_topgear_obstacle_impact_failed"; exit 1; fi
if [ "${controller_cmd_count:-0}" -lt 1 ]; then echo "unity_controller_missing_cmd_vel"; exit 1; fi

echo "VLN_MESA_TOPGEAR_VEHICLE_OBSTACLE_IMPACT_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
