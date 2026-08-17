#!/usr/bin/env bash

# 验收控制面板“速度控制”在 Unity wheel-ground 场景中的真实行为。
# 检查：↑ 正向前进、直行横漂/偏航受控、松键快速停车、A/D 近似原地左右转。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_control_panel_manual_velocity_unity_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
PANEL_LOG="$LOG_DIR/control_panel.log"
CLIENT_LOG="$LOG_DIR/manual_velocity_unity_client.log"
PORT="${VLN_CONTROL_PANEL_MANUAL_UNITY_PORT:-8887}"

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
panel_pid=""
client_pid=""

cleanup()
{
  if [ -n "$client_pid" ]; then
    kill "$client_pid" >/dev/null 2>&1 || true
    wait "$client_pid" >/dev/null 2>&1 || true
  fi
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

if ss -ltn 2>/dev/null | grep -E -q ":$PORT\b"; then
  echo "control_panel_manual_velocity_port_in_use=$PORT" | tee -a "$LOG_DIR/run_summary.txt"
  ss -ltnp | grep -E ":$PORT\b" | tee -a "$LOG_DIR/run_summary.txt" || true
  exit 1
fi

"$VLN_ROOT/scripts/start_vln_control_panel.sh" --no-browser --port "$PORT" >"$PANEL_LOG" 2>&1 &
panel_pid=$!
echo "panel_pid=$panel_pid" | tee -a "$LOG_DIR/run_summary.txt"

for _ in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:$PORT/api/status" >/dev/null 2>&1; then
    break
  fi
  sleep 0.2
done

if ! curl -fsS "http://127.0.0.1:$PORT/api/status" >/dev/null 2>&1; then
  echo "control_panel_failed_to_listen" | tee -a "$LOG_DIR/run_summary.txt"
  sed -n '1,160p' "$PANEL_LOG" || true
  exit 1
fi

bash -lc "sleep 8; python3 '$VLN_ROOT/scripts/vln_control_panel_manual_velocity_unity_client.py' --base-url 'http://127.0.0.1:$PORT' --timeout 40" >"$CLIENT_LOG" 2>&1 &
client_pid=$!

set +e
timeout 130s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadScoutWheelGroundCandidateSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$client_pid" >/dev/null 2>&1 || true
fi
wait "$client_pid"
client_status=$?
client_pid=""
set -e

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "client_status=$client_status"
  echo "panel_url=http://127.0.0.1:$PORT/"
  echo "log_dir=$LOG_DIR"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "Manual velocity Unity client output:"
sed -n '1,260p' "$CLIENT_LOG" || true
echo "Control panel log:"
sed -n '1,160p' "$PANEL_LOG" || true
echo "Key Unity log lines:"
grep -n -E "VLN_OFFROAD_SCOUT_WHEEL_GROUND|VLN_SCOUT_WHEEL_GROUND|VLN_CMD_VEL|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,360p' || true

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  exit 1
fi

if [ "$client_status" -ne 0 ]; then
  echo "control_panel_manual_velocity_unity_client_failed"
  exit 1
fi

if ! grep -q 'VLN_CONTROL_PANEL_MANUAL_VELOCITY_UNITY_HTTP_OK' "$CLIENT_LOG"; then
  echo "manual_velocity_unity_missing_success_marker"
  exit 1
fi

echo "VLN_CONTROL_PANEL_MANUAL_VELOCITY_UNITY_SMOKE_TEST_PASS"
