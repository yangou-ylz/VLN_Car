#!/usr/bin/env bash

# 阶段 21：第一套 Mesa 世界接入 Topgear 真实物理车后的最小物理落地验收。
# 只检查 Unity 内 WheelCollider/Rigidbody 与 Mesa TerrainCollider 的真实接触；不启动 ROS2 长链路。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
RUN_ID="vln_mesa_topgear_vehicle_physics_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
UNITY_LOG="$LOG_DIR/unity.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_mesa_topgear_vehicle_candidate_result.txt"
SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_mesa_topgear_vehicle_candidate_screenshot.png"
CONTROLLER_RESULT_FILE="$UNITY_PROJECT/Logs/vln_scout_wheel_ground_controller_result.txt"
FOLLOW_RESULT_FILE="$UNITY_PROJECT/Logs/vln_follow_transform_pose_result.txt"
ODOM_RESULT_FILE="$UNITY_PROJECT/Logs/vln_odom_publisher_result.txt"

mkdir -p "$LOG_DIR"

echo "Mesa + Topgear 物理落地 smoke test：不重建旧主场景，不跑旧 13 点路线。" | tee "$LOG_DIR/run_summary.txt"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee -a "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前大资产副本 Unity Editor，再运行该自动验收脚本。"
  exit 2
fi

for old_file in "$RESULT_FILE" "$SCREENSHOT_FILE" "$CONTROLLER_RESULT_FILE" "$FOLLOW_RESULT_FILE" "$ODOM_RESULT_FILE"; do
  if [ -f "$old_file" ]; then
    mv "$old_file" "$LOG_DIR/previous_$(basename "$old_file")"
  fi
done

set +e
timeout 170s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnMesaTopgearVehicleCandidateBuilder.RunBuildAndPhysicsSmokeTest \
  -logFile "$UNITY_LOG"
unity_status=$?
set -e

if [ -f "$RESULT_FILE" ]; then
  cp "$RESULT_FILE" "$LOG_DIR/$(basename "$RESULT_FILE")"
fi
if [ -f "$SCREENSHOT_FILE" ]; then
  cp "$SCREENSHOT_FILE" "$LOG_DIR/$(basename "$SCREENSHOT_FILE")"
fi
for current_file in "$CONTROLLER_RESULT_FILE" "$FOLLOW_RESULT_FILE" "$ODOM_RESULT_FILE"; do
  if [ -f "$current_file" ]; then
    cp "$current_file" "$LOG_DIR/$(basename "$current_file")"
  fi
done

success=$(grep -E '^success=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
wheel_collider_count=$(grep -E '^wheel_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
terrain_contact_steps=$(grep -E '^terrain_contact_steps=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
no_wheel_contact_steps=$(grep -E '^no_wheel_contact_steps=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
body_height_span=$(grep -E '^body_height_span_m=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
final_position=$(grep -E '^final_position=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2-)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "success=${success:-missing}"
  echo "wheel_collider_count=${wheel_collider_count:-missing}"
  echo "terrain_contact_steps=${terrain_contact_steps:-missing}"
  echo "no_wheel_contact_steps=${no_wheel_contact_steps:-missing}"
  echo "body_height_span_m=${body_height_span:-missing}"
  echo "final_position=${final_position:-missing}"
  echo "result_file=$RESULT_FILE"
  echo "screenshot_file=$SCREENSHOT_FILE"
  echo "log_dir=$LOG_DIR"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "Unity Mesa Topgear result:"
sed -n '1,180p' "$RESULT_FILE" 2>/dev/null || true
echo "Unity wheel-ground controller result:"
sed -n '1,180p' "$CONTROLLER_RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_MESA_TOPGEAR|VLN_SCOUT_WHEEL_GROUND|VLN_ODOM|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Exiting" "$UNITY_LOG" | sed -n '1,260p' || true

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  exit 1
fi
if [ "${success:-0}" != "1" ]; then
  echo "mesa_topgear_vehicle_physics_result_failed"
  exit 1
fi

echo "VLN_MESA_TOPGEAR_VEHICLE_PHYSICS_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
