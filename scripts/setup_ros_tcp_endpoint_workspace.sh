#!/usr/bin/env bash

# Prepare the project-local ROS-TCP-Endpoint workspace.
# This script does not install system, Python, Conda, or Snap packages.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
WORKSPACE="${UNITY_ROS2_WS:-$VLN_ROOT/unity_ros2_ws}"
ENDPOINT_DIR="$WORKSPACE/src/ROS-TCP-Endpoint"
ENDPOINT_REPO="${ROS_TCP_ENDPOINT_REPO:-https://github.com/Unity-Technologies/ROS-TCP-Endpoint.git}"
ENDPOINT_BRANCH="${ROS_TCP_ENDPOINT_BRANCH:-main-ros2}"

usage() {
  cat <<'EOF'
用法：
  ./scripts/setup_ros_tcp_endpoint_workspace.sh

可选环境变量：
  UNITY_ROS2_WS              ROS2 workspace 路径，默认 <VLN>/unity_ros2_ws
  ROS_TCP_ENDPOINT_REPO      Endpoint Git 仓库，默认 Unity-Technologies/ROS-TCP-Endpoint
  ROS_TCP_ENDPOINT_BRANCH    Endpoint 分支，默认 main-ros2

说明：
  该脚本只做项目内 clone、轻量补丁和 colcon build；不会 apt/pip/conda/snap install。
EOF
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

if ! command -v git >/dev/null 2>&1; then
  echo "缺少 git。请先让系统管理员或团队负责人按项目标准环境安装 git。"
  exit 1
fi

source "$HOME/.bashrc" >/dev/null 2>&1 || true

if declare -F ros2env >/dev/null 2>&1; then
  ros2env >/dev/null
elif [[ -f /opt/ros/humble/setup.bash ]]; then
  source /opt/ros/humble/setup.bash
else
  echo "未找到 ROS2 Humble 环境。请先按团队标准安装 ROS2 Humble，再运行本脚本。"
  exit 1
fi

if ! command -v colcon >/dev/null 2>&1; then
  echo "缺少 colcon。请先按团队标准安装 colcon，再运行本脚本。"
  exit 1
fi

mkdir -p "$WORKSPACE/src"

if [[ -d "$ENDPOINT_DIR/.git" ]]; then
  echo "检测到已有 ROS-TCP-Endpoint：$ENDPOINT_DIR"
else
  if [[ -e "$ENDPOINT_DIR" ]]; then
    echo "目标路径已存在但不是 Git 仓库：$ENDPOINT_DIR"
    exit 1
  fi
  echo "克隆 ROS-TCP-Endpoint：$ENDPOINT_REPO ($ENDPOINT_BRANCH)"
  git clone --branch "$ENDPOINT_BRANCH" --single-branch "$ENDPOINT_REPO" "$ENDPOINT_DIR"
fi

PATCH_TARGET="$ENDPOINT_DIR/ros_tcp_endpoint/default_server_endpoint.py"
if [[ ! -f "$PATCH_TARGET" ]]; then
  echo "未找到 Endpoint 入口文件：$PATCH_TARGET"
  exit 1
fi

if ! grep -F 'if rclpy.ok():' "$PATCH_TARGET" >/dev/null 2>&1; then
  perl -0pi -e 's/\n    rclpy\.shutdown\(\)\n/\n    if rclpy.ok():\n        rclpy.shutdown()\n/' "$PATCH_TARGET"
  echo "已应用 Endpoint 退出补丁：仅在 rclpy.ok() 时 shutdown。"
else
  echo "Endpoint 退出补丁已存在。"
fi

echo "开始构建 ROS-TCP-Endpoint workspace：$WORKSPACE"
(
  cd "$WORKSPACE"
  colcon build --packages-select ros_tcp_endpoint
)

echo "VLN_ROS_TCP_ENDPOINT_WORKSPACE_READY"
echo "workspace=$WORKSPACE"
echo "start_command=$VLN_ROOT/scripts/start_ros_tcp_endpoint.sh"
