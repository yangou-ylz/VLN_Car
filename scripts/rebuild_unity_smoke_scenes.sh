#!/usr/bin/env bash

# 重新生成当前三个轻量 smoke test 场景。
# 用途：更新 Unity 场景源码，例如补 ViewerCamera、重新固化传感器配置。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
PROJECT_DIR="$VLN_ROOT/UnityProjects/VLN_Offroad"
LOG_DIR="$VLN_ROOT/UnityProjects/_SetupLogs/rebuild_smoke_scenes_$(date +%Y%m%d_%H%M%S)"
UNITY_EDITOR="$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity"

if pgrep -f "$UNITY_EDITOR -projectPath $PROJECT_DIR" >/dev/null 2>&1; then
  echo "检测到 Unity 正在打开 $PROJECT_DIR。"
  echo "请先在 Unity 里停止 Play 并关闭 Editor，再重新运行本脚本，避免 Unity 工程锁冲突。"
  exit 1
fi

mkdir -p "$LOG_DIR"

run_setup()
{
  local method="$1"
  local log_file="$2"
  echo "重建场景：$method"
  "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
    -batchmode -nographics -quit \
    -executeMethod "$method" \
    -logFile "$log_file"
}

run_setup "VLN.Editor.VlnRos2ProjectSetup.BuildSmokeScene" "$LOG_DIR/ros2_smoke_scene.log"
run_setup "VLN.Editor.VlnUnitySensorsImageProjectSetup.BuildImageSmokeScene" "$LOG_DIR/image_smoke_scene.log"
run_setup "VLN.Editor.VlnUnitySensorsLidarProjectSetup.BuildLidarSmokeScene" "$LOG_DIR/lidar_smoke_scene.log"

echo "VLN_UNITY_SCENES_REBUILD_DONE"
echo "日志目录：$LOG_DIR"
