#!/usr/bin/env bash

# 检查 /vln/lidar/points 中是否包含近地回波。
# 前提：Unity 已打开 mesa_topgear_mix，ROS-TCP-Endpoint 已启动，Unity 已点击 Play。

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
DURATION="${1:-5}"
TOPDOWN_PNG="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox/Logs/vln_lidar_ground_returns_topdown_latest.png"

mkdir -p "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

source "$HOME/.bashrc" >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
else
  source /opt/ros/humble/setup.bash
fi

source "$VLN_ROOT/unity_ros2_ws/install/setup.bash"

echo "检查 /vln/lidar/points 的近地回波和距离环带分布。"
echo "重点看 selected_axis_ring_ground_counts：0-2m、2-5m、5-10m 环带应有可见地面点。"
echo "同时导出真实 ROS PointCloud2 俯视图：$TOPDOWN_PNG"

python3 "$VLN_ROOT/scripts/ros2_capture_lidar_ground_returns.py" \
  --topic /vln/lidar/points \
  --duration "$DURATION" \
  --timeout 20 \
  --max-horizontal-range 30 \
  --near-ground-depth 1.5 \
  --ring-ground-depth 0.25 \
  --vertical-axis z \
  --topdown-png "$TOPDOWN_PNG" \
  --topdown-range 45 \
  --topdown-size 900
