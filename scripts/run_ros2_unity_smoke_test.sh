#!/usr/bin/env bash

# 运行 Unity <-> ROS2 最小通信闭环测试。
# 验收内容：
# 1. ROS2 能 echo Unity 发布的 /unity/heartbeat。
# 2. Unity 能收到 ROS2 发布到 /ros2/command 的 std_msgs/String。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
RUN_ID="vln_smoke_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
ECHO_LOG="$LOG_DIR/ros2_echo_unity_heartbeat.log"
PUB_LOG="$LOG_DIR/ros2_pub_command.log"
RESULT_FILE="$VLN_ROOT/UnityProjects/VLN_Offroad/Logs/vln_ros2_smoke_result.txt"

mkdir -p "$LOG_DIR"

endpoint_pid=""

cleanup()
{
  if [ -n "$endpoint_pid" ]; then
    kill "$endpoint_pid" >/dev/null 2>&1 || true
    wait "$endpoint_pid" >/dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

if [ -f "$RESULT_FILE" ]; then
  mv "$RESULT_FILE" "$LOG_DIR/previous_vln_ros2_smoke_result.txt"
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

timeout 45s bash -lc "$ROS_ENV; ros2 topic echo --once /unity/heartbeat std_msgs/msg/String" >"$ECHO_LOG" 2>&1 &
echo_pid=$!

(
  sleep 5
  timeout 12s bash -lc "$ROS_ENV; ros2 topic pub -r 2 /ros2/command std_msgs/msg/String \"{data: '$RUN_ID'}\"" >"$PUB_LOG" 2>&1 || true
) &
pub_pid=$!

set +e
timeout 80s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode -nographics \
  -executeMethod VLN.Editor.VlnRos2SmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
wait "$echo_pid"
echo_status=$?
wait "$pub_pid"
pub_status=$?
set -e

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "echo_status=$echo_status"
  echo "pub_status=$pub_status"
  echo "log_dir=$LOG_DIR"
  echo "result_file=$RESULT_FILE"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 echo output:"
sed -n '1,80p' "$ECHO_LOG" || true
echo "Unity received result:"
sed -n '1,80p' "$RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_ROS2_SMOKE|Incompatible protocol|Exception|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,220p' || true

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  exit 1
fi

if ! grep -q 'unity_heartbeat_' "$ECHO_LOG"; then
  echo "ros2_did_not_echo_unity_heartbeat"
  exit 1
fi

if ! grep -q "$RUN_ID" "$RESULT_FILE"; then
  echo "unity_did_not_receive_ros2_command"
  exit 1
fi

echo "VLN_ROS2_SMOKE_TEST_PASS"
