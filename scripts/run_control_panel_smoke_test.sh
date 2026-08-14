#!/usr/bin/env bash

# 阶段 11：本地中文控制面板自动验收。
# 验收内容：控制面板启动、收到 /tf、HTTP 发送目标、后端发布 /vln/cmd_vel，Unity 车体到达目标附近。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_control_panel_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
PANEL_LOG="$LOG_DIR/control_panel.log"
CLIENT_LOG="$LOG_DIR/control_panel_client.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_terrain_result.txt"
CONTROL_RESULT_FILE="$UNITY_PROJECT/Logs/vln_vehicle_control_result.txt"
PORT="${VLN_CONTROL_PANEL_PORT:-8765}"

mkdir -p "$LOG_DIR"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前 Unity Editor，再运行本脚本。同一工程不能被两个 Editor 实例同时打开。"
  exit 2
fi

endpoint_pid=""
panel_pid=""

cleanup()
{
  if [ -n "$panel_pid" ]; then
    kill "$panel_pid" >/dev/null 2>&1 || true
    wait "$panel_pid" >/dev/null 2>&1 || true
  fi
  if [ -n "$endpoint_pid" ]; then
    kill "$endpoint_pid" >/dev/null 2>&1 || true
    wait "$endpoint_pid" >/dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

if [ -f "$RESULT_FILE" ]; then
  mv "$RESULT_FILE" "$LOG_DIR/previous_vln_offroad_terrain_result.txt"
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

if ss -ltn 2>/dev/null | grep -E -q ":$PORT\b"; then
  if curl -fsS "http://127.0.0.1:$PORT/api/status" >/dev/null 2>&1; then
    echo "panel_already_listening=true" | tee -a "$LOG_DIR/run_summary.txt"
    echo "复用已启动的控制面板：http://127.0.0.1:$PORT/" >"$PANEL_LOG"
  else
    echo "control_panel_port_in_use_but_not_responding=$PORT" | tee -a "$LOG_DIR/run_summary.txt"
    ss -ltnp | grep -E ":$PORT\b" | tee -a "$PANEL_LOG" || true
    exit 1
  fi
else
  "$VLN_ROOT/scripts/start_vln_control_panel.sh" --no-browser --port "$PORT" >"$PANEL_LOG" 2>&1 &
  panel_pid=$!
  echo "panel_pid=$panel_pid" | tee -a "$LOG_DIR/run_summary.txt"
fi

for _ in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:$PORT/api/status" >/dev/null 2>&1; then
    break
  fi
  sleep 0.25
done

if ! curl -fsS "http://127.0.0.1:$PORT/api/status" >/dev/null 2>&1; then
  echo "control_panel_failed_to_listen" | tee -a "$LOG_DIR/run_summary.txt"
  sed -n '1,160p' "$PANEL_LOG" || true
  exit 1
fi

ROS_ENV='source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true; if declare -F ros2env >/dev/null 2>&1; then ros2env >/dev/null; else source /opt/ros/humble/setup.bash; fi; source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash'

bash -lc "sleep 10; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

bash -lc "sleep 8; $ROS_ENV; python3 /home/ubuntu22/VLN/scripts/vln_control_panel_smoke_client.py --base-url http://127.0.0.1:$PORT --target-x 1.2 --target-y 0.0 --timeout 35" >"$CLIENT_LOG" 2>&1 &
client_pid=$!

set +e
timeout 120s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadTerrainSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$client_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$client_pid"
client_status=$?
wait "$topic_pid"
topic_status=$?
set -e

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "client_status=$client_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "panel_url=http://127.0.0.1:$PORT/"
  echo "result_file=$RESULT_FILE"
  echo "control_result_file=$CONTROL_RESULT_FILE"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "Control panel client output:"
sed -n '1,220p' "$CLIENT_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|lidar|cmd_vel)|/tf' "$TOPIC_LOG" || true
echo "Unity control result:"
sed -n '1,180p' "$CONTROL_RESULT_FILE" 2>/dev/null || true
echo "Control panel log:"
sed -n '1,160p' "$PANEL_LOG" || true
echo "Key Unity log lines:"
grep -n -E "VLN_OFFROAD_TERRAIN|VLN_VEHICLE_TF|VLN_CMD_VEL|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,260p' || true

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  exit 1
fi

if [ "$client_status" -ne 0 ]; then
  echo "control_panel_http_client_failed"
  exit 1
fi

if ! grep -q 'VLN_CONTROL_PANEL_HTTP_SMOKE_OK' "$CLIENT_LOG"; then
  echo "control_panel_missing_success_marker"
  exit 1
fi

if ! grep -q '^cmd_vel_received=' "$CONTROL_RESULT_FILE"; then
  echo "unity_control_result_missing_cmd_vel_received"
  exit 1
fi

echo "VLN_CONTROL_PANEL_SMOKE_TEST_PASS"
