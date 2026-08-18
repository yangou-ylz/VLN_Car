#!/usr/bin/env bash

# Unity Editor 菜单启动包装器。
# 只负责打开新终端并运行现有 ROS2/可视化脚本；不实现任何 Unity 内置导航逻辑。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
MODE="${1:-help}"
UNITY_PID="${2:-}"
SESSION_SCRIPT="$VLN_ROOT/scripts/unity_menu_terminal_session.sh"

case "$MODE" in
  endpoint)
    TITLE="VLN ROS-TCP-Endpoint"
    TARGET_SCRIPT="$VLN_ROOT/scripts/start_ros_tcp_endpoint.sh"
    SUMMARY="启动 ROS-TCP-Endpoint。保持这个终端开着，然后回 Unity 点击 Play。"
    ;;
  route)
    TITLE="VLN 13点自动路线"
    TARGET_SCRIPT="$VLN_ROOT/scripts/drive_scout_wheel_ground_route_demo.sh"
    SUMMARY="发布 13 点自动路线。前提：Unity 已打开场景、endpoint 已启动、Unity 已点击 Play。"
    ;;
  challenge)
    TITLE="VLN 16点挑战路线"
    TARGET_SCRIPT="$VLN_ROOT/scripts/drive_scout_wheel_ground_challenge_route_demo.sh"
    SUMMARY="发布 16 点挑战路线。前提：Unity 已打开场景、endpoint 已启动、Unity 已点击 Play。"
    ;;
  image)
    TITLE="VLN 相机图像"
    TARGET_SCRIPT="$VLN_ROOT/scripts/view_front_image.sh"
    SUMMARY="打开 rqt_image_view 查看 /vln/front/image_raw。"
    ;;
  rviz)
    TITLE="VLN 雷达点云"
    TARGET_SCRIPT="$VLN_ROOT/scripts/view_vln_vehicle_rviz.sh"
    SUMMARY="打开 RViz2 查看 /vln/lidar/points、TF 和车辆传感器。"
    ;;
  panel)
    TITLE="VLN 中文控制面板"
    TARGET_SCRIPT="$VLN_ROOT/scripts/start_vln_control_panel.sh"
    SUMMARY="启动中文控制面板。浏览器地址：http://127.0.0.1:8765/。"
    ;;
  selftest)
    for script in \
      "$VLN_ROOT/scripts/unity_menu_terminal_session.sh" \
      "$VLN_ROOT/scripts/cleanup_unity_menu_processes.sh" \
      "$VLN_ROOT/scripts/start_ros_tcp_endpoint.sh" \
      "$VLN_ROOT/scripts/drive_scout_wheel_ground_route_demo.sh" \
      "$VLN_ROOT/scripts/drive_scout_wheel_ground_challenge_route_demo.sh" \
      "$VLN_ROOT/scripts/view_front_image.sh" \
      "$VLN_ROOT/scripts/view_vln_vehicle_rviz.sh" \
      "$VLN_ROOT/scripts/start_vln_control_panel.sh"; do
      test -x "$script"
      echo "ok: $script"
    done
    echo "VLN_UNITY_MENU_LAUNCH_SELFTEST_OK"
    exit 0
    ;;
  *)
    echo "用法：$0 {endpoint|route|challenge|image|rviz|panel|selftest}" >&2
    exit 2
    ;;
esac

if [ ! -x "$TARGET_SCRIPT" ]; then
  echo "目标脚本不存在或不可执行：$TARGET_SCRIPT" >&2
  exit 1
fi

if [ ! -x "$SESSION_SCRIPT" ]; then
  echo "终端会话包装器不存在或不可执行：$SESSION_SCRIPT" >&2
  exit 1
fi

if command -v gnome-terminal >/dev/null 2>&1; then
  exec gnome-terminal --title="$TITLE" -- "$SESSION_SCRIPT" "$MODE" "$TARGET_SCRIPT" "$SUMMARY" "$UNITY_PID"
fi

if command -v kgx >/dev/null 2>&1; then
  exec kgx --title "$TITLE" -- "$SESSION_SCRIPT" "$MODE" "$TARGET_SCRIPT" "$SUMMARY" "$UNITY_PID"
fi

if command -v konsole >/dev/null 2>&1; then
  exec konsole --new-tab -p "tabtitle=$TITLE" -e "$SESSION_SCRIPT" "$MODE" "$TARGET_SCRIPT" "$SUMMARY" "$UNITY_PID"
fi

if command -v xfce4-terminal >/dev/null 2>&1; then
  exec xfce4-terminal --title="$TITLE" --command="$SESSION_SCRIPT '$MODE' '$TARGET_SCRIPT' '$SUMMARY' '$UNITY_PID'"
fi

if command -v xterm >/dev/null 2>&1; then
  exec xterm -T "$TITLE" -e "$SESSION_SCRIPT" "$MODE" "$TARGET_SCRIPT" "$SUMMARY" "$UNITY_PID"
fi

echo "没有找到可用终端模拟器，将在当前进程中运行。" >&2
exec "$SESSION_SCRIPT" "$MODE" "$TARGET_SCRIPT" "$SUMMARY" "$UNITY_PID"
