#!/usr/bin/env bash

# 清理由 Unity 顶部 VLN 菜单启动的外部终端和 ROS2/可视化进程。
# 默认只清理登记过的菜单进程；--include-known 会额外清理本项目常见残留入口。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
RUNTIME_DIR="$VLN_ROOT/.runtime/unity_menu"
PID_FILE="$RUNTIME_DIR/processes.tsv"
MODE="${1:---tracked-only}"

mkdir -p "$RUNTIME_DIR"

is_number() {
  case "${1:-}" in
    ''|*[!0-9]*) return 1 ;;
    *) return 0 ;;
  esac
}

is_alive() {
  is_number "${1:-}" && kill -0 "$1" >/dev/null 2>&1
}

kill_process_group() {
  local pgid="${1:-}"
  [ -n "$pgid" ] || return 0
  is_number "$pgid" || return 0
  [ "$pgid" -gt 1 ] || return 0
  kill -TERM -- "-$pgid" >/dev/null 2>&1 || true
}

kill_pid() {
  local pid="${1:-}"
  is_alive "$pid" || return 0
  kill -TERM "$pid" >/dev/null 2>&1 || true
}

tracked_pids=()
tracked_pgids=()

if [ -f "$PID_FILE" ]; then
  while IFS=$'\t' read -r session_id pid pgid mode target_script started_at unity_pid; do
    [ -n "${pid:-}" ] || continue
    if ! is_alive "${pid:-}"; then
      echo "skip stale menu session: ${session_id:-unknown} pid=${pid:-none}"
      continue
    fi
    if is_number "${pgid:-}"; then
      tracked_pgids+=("$pgid")
      kill_process_group "$pgid"
    fi
    if is_number "${pid:-}"; then
      tracked_pids+=("$pid")
      kill_pid "$pid"
    fi
  done < "$PID_FILE"
fi

if [ "$MODE" = "--include-known" ] || [ "$MODE" = "--all" ]; then
  # 只匹配本项目自己的入口，避免误杀用户其他 ROS2 / RViz / Python 工作。
  pkill -TERM -f '^python3 /home/ubuntu22/VLN/scripts/vln_control_panel.py( |$)' >/dev/null 2>&1 || true
  pkill -TERM -f '^/usr/bin/python3 /home/ubuntu22/VLN/unity_ros2_ws/install/ros_tcp_endpoint/lib/ros_tcp_endpoint/default_server_endpoint( |$)' >/dev/null 2>&1 || true
  pkill -TERM -f '/home/ubuntu22/VLN/scripts/(drive_scout_wheel_ground_route_demo|drive_scout_wheel_ground_challenge_route_demo|view_front_image|view_all_camera_images|view_vln_vehicle_rviz)\.sh' >/dev/null 2>&1 || true
fi

sleep 0.8

for pgid in "${tracked_pgids[@]:-}"; do
  [ -n "$pgid" ] || continue
  kill -KILL -- "-$pgid" >/dev/null 2>&1 || true
done

for pid in "${tracked_pids[@]:-}"; do
  [ -n "$pid" ] || continue
  kill -KILL "$pid" >/dev/null 2>&1 || true
done

if [ "$MODE" = "--include-known" ] || [ "$MODE" = "--all" ]; then
  pkill -KILL -f '^python3 /home/ubuntu22/VLN/scripts/vln_control_panel.py( |$)' >/dev/null 2>&1 || true
  pkill -KILL -f '^/usr/bin/python3 /home/ubuntu22/VLN/unity_ros2_ws/install/ros_tcp_endpoint/lib/ros_tcp_endpoint/default_server_endpoint( |$)' >/dev/null 2>&1 || true
fi

: > "$PID_FILE"
echo "VLN_UNITY_MENU_CLEANUP_OK mode=$MODE"
