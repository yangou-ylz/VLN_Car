#!/usr/bin/env bash

# Read-only validation for the clean Mesa Topgear team release project.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
RELEASE_PROJECT="${VLN_MESA_TOPGEAR_RELEASE_PROJECT:-$VLN_ROOT/UnityProjects/VLN_MesaTopgear_TeamRelease}"
TARGET_SCENE="Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity"

fail_count=0
warn_count=0

pass() { printf '[PASS] %s\n' "$1"; }
warn() { printf '[WARN] %s\n' "$1"; warn_count=$((warn_count + 1)); }
fail() { printf '[FAIL] %s\n' "$1"; fail_count=$((fail_count + 1)); }

echo "== Mesa Topgear 团队发布工程检查 =="
echo "release_project=$RELEASE_PROJECT"

if [[ -d "$RELEASE_PROJECT" ]]; then
  pass "发布工程目录存在。"
else
  fail "发布工程目录不存在。请先运行 scripts/prepare_mesa_topgear_team_release_project.sh，或放入团队发布资产包。"
fi

required_paths=(
  "$RELEASE_PROJECT/$TARGET_SCENE"
  "$RELEASE_PROJECT/Assets/BK/PureNature_MesaDesert"
  "$RELEASE_PROJECT/Assets/BK/Pure_Common"
  "$RELEASE_PROJECT/Assets/VLN"
  "$RELEASE_PROJECT/Assets/Resources"
  "$RELEASE_PROJECT/Packages/manifest.json"
  "$RELEASE_PROJECT/ProjectSettings/ProjectVersion.txt"
  "$RELEASE_PROJECT/VLN_MESA_TOPGEAR_TEAM_RELEASE_MANIFEST.json"
)

for path in "${required_paths[@]}"; do
  if [[ -e "$path" ]]; then
    pass "存在：${path#$RELEASE_PROJECT/}"
  else
    fail "缺少：${path#$RELEASE_PROJECT/}"
  fi
done

excluded_paths=(
  "$RELEASE_PROJECT/Assets/BK/PureNature_Oasis"
  "$RELEASE_PROJECT/Assets/NatureManufacture Assets"
  "$RELEASE_PROJECT/Assets/ForestLake"
  "$RELEASE_PROJECT/Library"
  "$RELEASE_PROJECT/Temp"
  "$RELEASE_PROJECT/Logs"
  "$RELEASE_PROJECT/UserSettings"
)

for path in "${excluded_paths[@]}"; do
  if [[ -e "$path" ]]; then
    fail "发布工程不应包含：${path#$RELEASE_PROJECT/}"
  fi
done

if [[ -f "$RELEASE_PROJECT/ProjectSettings/ProjectVersion.txt" ]] && grep -q '2022.3.62f1' "$RELEASE_PROJECT/ProjectSettings/ProjectVersion.txt"; then
  pass "Unity 版本为 2022.3.62f1。"
else
  fail "Unity 版本不是 2022.3.62f1 或版本文件缺失。"
fi

if [[ -f "$RELEASE_PROJECT/Packages/manifest.json" ]] \
  && grep -q 'com.unity.robotics.ros-tcp-connector' "$RELEASE_PROJECT/Packages/manifest.json" \
  && grep -q 'com.frj.unity-sensors' "$RELEASE_PROJECT/Packages/manifest.json" \
  && grep -q 'com.frj.unity-sensors-ros' "$RELEASE_PROJECT/Packages/manifest.json"; then
  pass "Unity ROS/传感器依赖已写入 manifest。"
else
  fail "manifest 缺少 ROS-TCP-Connector 或 UnitySensors 依赖。"
fi

if [[ -f "$VLN_ROOT/config/topgear_sensor_pose_user_locked.json" \
  && -f "$VLN_ROOT/config/topgear_upper_assembly_user_locked.json" \
  && -f "$VLN_ROOT/config/topgear_camera_data_pose_user_locked.json" ]]; then
  pass "仓库根目录包含 Topgear 传感器/上装/真实相机锁定配置。"
else
  fail "仓库根目录缺少 Topgear 锁定配置。"
fi

if [[ -d "$RELEASE_PROJECT" ]]; then
  echo "== 发布工程体量 =="
  du -sh "$RELEASE_PROJECT" 2>/dev/null || true
  find "$RELEASE_PROJECT" -type f -size +95M -printf '%s %p\n' 2>/dev/null | sort -nr | sed -n '1,40p'
fi

echo "summary: failures=$fail_count warnings=$warn_count"
if (( fail_count > 0 )); then
  echo "VLN_MESA_TOPGEAR_TEAM_RELEASE_CHECK_FAILED"
  exit 1
fi

echo "VLN_MESA_TOPGEAR_TEAM_RELEASE_CHECK_OK"
