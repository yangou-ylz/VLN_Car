#!/usr/bin/env bash

# 验收控制面板的手动速度控制、记录导出和按键方向映射。
# 该脚本不需要 Unity 正在 Play；它只验证 ROS2 后端和 HTTP API。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
RUN_ID="vln_control_panel_manual_recording_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
PANEL_LOG="$LOG_DIR/control_panel.log"
CLIENT_LOG="$LOG_DIR/manual_recording_client.log"
PORT="${VLN_CONTROL_PANEL_MANUAL_PORT:-8876}"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

panel_pid=""

cleanup()
{
  if [ -n "$panel_pid" ]; then
    kill "$panel_pid" >/dev/null 2>&1 || true
    wait "$panel_pid" >/dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

if ss -ltn 2>/dev/null | grep -E -q ":$PORT\b"; then
  echo "control_panel_manual_port_in_use=$PORT" | tee "$LOG_DIR/run_summary.txt"
  ss -ltnp | grep -E ":$PORT\b" | tee -a "$LOG_DIR/run_summary.txt" || true
  exit 1
fi

"$VLN_ROOT/scripts/start_vln_control_panel.sh" --no-browser --port "$PORT" >"$PANEL_LOG" 2>&1 &
panel_pid=$!

for _ in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:$PORT/api/status" >/dev/null 2>&1; then
    break
  fi
  sleep 0.2
done

if ! curl -fsS "http://127.0.0.1:$PORT/api/status" >/dev/null 2>&1; then
  echo "control_panel_failed_to_listen" | tee "$LOG_DIR/run_summary.txt"
  sed -n '1,160p' "$PANEL_LOG" || true
  exit 1
fi

set +e
python3 "$VLN_ROOT/scripts/vln_control_panel_manual_recording_smoke_client.py" --base-url "http://127.0.0.1:$PORT" >"$CLIENT_LOG" 2>&1
client_status=$?
set -e

{
  echo "run_id=$RUN_ID"
  echo "panel_pid=$panel_pid"
  echo "client_status=$client_status"
  echo "panel_url=http://127.0.0.1:$PORT/"
  echo "log_dir=$LOG_DIR"
} | tee "$LOG_DIR/run_summary.txt"

echo "Manual recording client output:"
sed -n '1,220p' "$CLIENT_LOG" || true
echo "Control panel log:"
sed -n '1,120p' "$PANEL_LOG" || true

if [ "$client_status" -ne 0 ]; then
  echo "control_panel_manual_recording_client_failed"
  exit 1
fi

if ! grep -q 'VLN_CONTROL_PANEL_MANUAL_RECORDING_HTTP_SMOKE_OK' "$CLIENT_LOG"; then
  echo "control_panel_manual_recording_missing_success_marker"
  exit 1
fi

echo "VLN_CONTROL_PANEL_MANUAL_RECORDING_SMOKE_TEST_PASS"
