#!/usr/bin/env bash

# 阶段 20：Topgear V2 上装传感器套件自动验收。
# 验收内容：上装 16 线 LiDAR + 前后左右 4 个 RGB 相机安装在 Topgear 上装位置，ROS2 数据可收到，视觉传感器不参与物理碰撞。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
RUN_ID="vln_topgear_sensor_suite_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
TF_LOG="$LOG_DIR/ros2_topgear_sensor_tf.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_candidate_result.txt"
TOPGEAR_VISUAL_SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_visual_screenshot.png"
TOPGEAR_SENSOR_SCREENSHOT_FILE="$UNITY_PROJECT/Logs/vln_offroad_scout_wheel_ground_topgear_sensor_suite_screenshot.png"

declare -A IMAGE_TOPICS=(
  [front]="/vln/front/image_raw"
  [rear]="/vln/rear/image_raw"
  [left]="/vln/left/image_raw"
  [right]="/vln/right/image_raw"
)
declare -A INFO_TOPICS=(
  [front]="/vln/front/camera_info"
  [rear]="/vln/rear/camera_info"
  [left]="/vln/left/camera_info"
  [right]="/vln/right/camera_info"
)
declare -A FRAME_IDS=(
  [front]="front_camera_optical_frame"
  [rear]="rear_camera_optical_frame"
  [left]="left_camera_optical_frame"
  [right]="right_camera_optical_frame"
)
POINTS_TOPIC="/vln/lidar/points"
LIDAR_FRAME_ID="lidar_link"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前 Unity Editor，再运行本自动传感器验收脚本。"
  exit 2
fi

if find "$UNITY_PROJECT/Library" -maxdepth 1 -type f \( -name '*lock*' -o -name '*Lock*' \) 2>/dev/null | grep -q .; then
  echo "发现 Unity stale lock，先用项目恢复脚本移动 lock。" | tee "$LOG_DIR/run_summary.txt"
  "$VLN_ROOT/scripts/stop_unity_vln_project.sh" | tee -a "$LOG_DIR/run_summary.txt"
fi

endpoint_pid=""
cleanup()
{
  if [ -n "$endpoint_pid" ]; then
    kill "$endpoint_pid" >/dev/null 2>&1 || true
    wait "$endpoint_pid" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

for old_file in \
  "$RESULT_FILE" \
  "$TOPGEAR_VISUAL_SCREENSHOT_FILE" \
  "$TOPGEAR_SENSOR_SCREENSHOT_FILE"; do
  if [ -f "$old_file" ]; then
    mv "$old_file" "$LOG_DIR/previous_$(basename "$old_file")"
  fi
done

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

ROS_ENV='source /home/ubuntu22/.bashrc >/dev/null 2>&1 || true; if declare -F ros2env >/dev/null 2>&1; then ros2env >/dev/null; else source /opt/ros/humble/setup.bash; fi; source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash'

image_pids=()
info_pids=()
for view in front rear left right; do
  timeout 125s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic ${IMAGE_TOPICS[$view]} --width 640 --height 480 --encoding rgb8 --frame-id ${FRAME_IDS[$view]} --timeout 120" >"$LOG_DIR/ros2_${view}_image_once.log" 2>&1 &
  image_pids+=("$!")

  timeout 125s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_camera_info_once.py --topic ${INFO_TOPICS[$view]} --width 640 --height 480 --frame-id ${FRAME_IDS[$view]} --timeout 120" >"$LOG_DIR/ros2_${view}_camera_info_once.log" 2>&1 &
  info_pids+=("$!")
done

timeout 125s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic $POINTS_TOPIC --width 7200 --point-step 16 --frame-id $LIDAR_FRAME_ID --timeout 120 --min-nonzero-points 80" >"$LOG_DIR/ros2_lidar_pointcloud2_once.log" 2>&1 &
cloud_pid=$!

timeout 125s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_vehicle_tf.py --topic /tf --timeout 120 --stable-observe-seconds 1.5 --required-edge map:base_link --required-edge base_link:front_camera_optical_frame --required-edge base_link:rear_camera_optical_frame --required-edge base_link:left_camera_optical_frame --required-edge base_link:right_camera_optical_frame --required-edge base_link:lidar_link" >"$TF_LOG" 2>&1 &
tf_pid=$!

bash -lc "sleep 14; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

set +e
timeout 170s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnOffroadScoutWheelGroundCandidateSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "${image_pids[@]}" "${info_pids[@]}" "$cloud_pid" "$tf_pid" "$topic_pid" >/dev/null 2>&1 || true
fi

image_statuses=()
for pid in "${image_pids[@]}"; do
  wait "$pid"
  image_statuses+=("$?")
done

info_statuses=()
for pid in "${info_pids[@]}"; do
  wait "$pid"
  info_statuses+=("$?")
done

wait "$cloud_pid"
cloud_status=$?
wait "$tf_pid"
tf_status=$?
wait "$topic_pid"
topic_status=$?
set -e

if [ -f "$RESULT_FILE" ]; then
  cp "$RESULT_FILE" "$LOG_DIR/$(basename "$RESULT_FILE")"
fi
if [ -f "$TOPGEAR_VISUAL_SCREENSHOT_FILE" ]; then
  cp "$TOPGEAR_VISUAL_SCREENSHOT_FILE" "$LOG_DIR/$(basename "$TOPGEAR_VISUAL_SCREENSHOT_FILE")"
fi
if [ -f "$TOPGEAR_SENSOR_SCREENSHOT_FILE" ]; then
  cp "$TOPGEAR_SENSOR_SCREENSHOT_FILE" "$LOG_DIR/$(basename "$TOPGEAR_SENSOR_SCREENSHOT_FILE")"
fi
for sensor_view_file in "$UNITY_PROJECT"/Logs/vln_offroad_scout_wheel_ground_topgear_sensor_suite_screenshot_*.png; do
  if [ -f "$sensor_view_file" ]; then
    cp "$sensor_view_file" "$LOG_DIR/$(basename "$sensor_view_file")"
  fi
done

topgear_sensor_suite_present=$(grep -E '^topgear_sensor_suite_present=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_sensor_camera_count=$(grep -E '^topgear_sensor_camera_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_sensor_lidar_count=$(grep -E '^topgear_sensor_lidar_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_sensor_renderer_count=$(grep -E '^topgear_sensor_renderer_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_sensor_vlp16_official_mesh_count=$(grep -E '^topgear_sensor_vlp16_official_mesh_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_sensor_d405_official_stl_count=$(grep -E '^topgear_sensor_d405_official_stl_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_sensor_procedural_vlp16_rib_count=$(grep -E '^topgear_sensor_procedural_vlp16_rib_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_sensor_procedural_d405_screw_count=$(grep -E '^topgear_sensor_procedural_d405_screw_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_sensor_collider_count=$(grep -E '^topgear_sensor_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_sensor_rigidbody_count=$(grep -E '^topgear_sensor_rigidbody_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_visual_collider_count=$(grep -E '^topgear_visual_collider_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)
topgear_visual_rigidbody_count=$(grep -E '^topgear_visual_rigidbody_count=' "$RESULT_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "front_image_status=${image_statuses[0]}"
  echo "rear_image_status=${image_statuses[1]}"
  echo "left_image_status=${image_statuses[2]}"
  echo "right_image_status=${image_statuses[3]}"
  echo "front_camera_info_status=${info_statuses[0]}"
  echo "rear_camera_info_status=${info_statuses[1]}"
  echo "left_camera_info_status=${info_statuses[2]}"
  echo "right_camera_info_status=${info_statuses[3]}"
  echo "cloud_status=$cloud_status"
  echo "tf_status=$tf_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "result_file=$RESULT_FILE"
  echo "topgear_visual_screenshot_file=$TOPGEAR_VISUAL_SCREENSHOT_FILE"
  echo "topgear_sensor_suite_screenshot_file=$TOPGEAR_SENSOR_SCREENSHOT_FILE"
  echo "topgear_sensor_suite_present=${topgear_sensor_suite_present:-0}"
  echo "topgear_sensor_camera_count=${topgear_sensor_camera_count:-0}"
  echo "topgear_sensor_lidar_count=${topgear_sensor_lidar_count:-0}"
  echo "topgear_sensor_renderer_count=${topgear_sensor_renderer_count:-0}"
  echo "topgear_sensor_vlp16_official_mesh_count=${topgear_sensor_vlp16_official_mesh_count:-0}"
  echo "topgear_sensor_d405_official_stl_count=${topgear_sensor_d405_official_stl_count:-0}"
  echo "topgear_sensor_procedural_vlp16_rib_count=${topgear_sensor_procedural_vlp16_rib_count:-0}"
  echo "topgear_sensor_procedural_d405_screw_count=${topgear_sensor_procedural_d405_screw_count:-0}"
  echo "topgear_sensor_collider_count=${topgear_sensor_collider_count:-missing}"
  echo "topgear_sensor_rigidbody_count=${topgear_sensor_rigidbody_count:-missing}"
  echo "topgear_visual_collider_count=${topgear_visual_collider_count:-missing}"
  echo "topgear_visual_rigidbody_count=${topgear_visual_rigidbody_count:-missing}"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|rear|left|right|lidar)|/tf' "$TOPIC_LOG" || true
echo "ROS2 TF output:"
sed -n '1,120p' "$TF_LOG" || true
echo "Unity sensor result excerpt:"
grep -E '^(topgear|rear_|left_|right_|image_topic|camera_info_topic|pointcloud_topic|tf_tree|camera_count|lidar_)' "$RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E 'VLN_TOPGEAR_SENSOR|VLN_OFFROAD_SCOUT_WHEEL_GROUND_TOPGEAR|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting' "$UNITY_LOG" | sed -n '1,260p' || true

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  exit 1
fi

for status in "${image_statuses[@]}"; do
  if [ "$status" -ne 0 ]; then
    echo "ros2_camera_image_validation_failed"
    exit 1
  fi
done

for status in "${info_statuses[@]}"; do
  if [ "$status" -ne 0 ]; then
    echo "ros2_camera_info_validation_failed"
    exit 1
  fi
done

if [ "$cloud_status" -ne 0 ]; then
  echo "ros2_lidar_pointcloud_validation_failed"
  exit 1
fi

if [ "$tf_status" -ne 0 ]; then
  echo "ros2_topgear_sensor_tf_validation_failed"
  exit 1
fi

for view in front rear left right; do
  if ! grep -q 'VLN_UNITYSENSORS_IMAGE_MSG_OK' "$LOG_DIR/ros2_${view}_image_once.log"; then
    echo "ros2_${view}_image_missing_success_marker"
    exit 1
  fi
  if ! grep -q 'VLN_UNITYSENSORS_CAMERA_INFO_MSG_OK' "$LOG_DIR/ros2_${view}_camera_info_once.log"; then
    echo "ros2_${view}_camera_info_missing_success_marker"
    exit 1
  fi
  if ! grep -F -q "${IMAGE_TOPICS[$view]} [sensor_msgs/msg/Image]" "$TOPIC_LOG"; then
    echo "ros2_topic_list_missing_${view}_image_topic"
    exit 1
  fi
  if ! grep -F -q "${INFO_TOPICS[$view]} [sensor_msgs/msg/CameraInfo]" "$TOPIC_LOG"; then
    echo "ros2_topic_list_missing_${view}_camera_info_topic"
    exit 1
  fi
done

if ! grep -q 'VLN_UNITYSENSORS_POINTCLOUD2_MSG_OK' "$LOG_DIR/ros2_lidar_pointcloud2_once.log"; then
  echo "ros2_lidar_pointcloud_missing_success_marker"
  exit 1
fi

if ! grep -q 'VLN_VEHICLE_TF_MSG_OK' "$TF_LOG"; then
  echo "ros2_topgear_sensor_tf_missing_success_marker"
  exit 1
fi

if ! grep -F -q "$POINTS_TOPIC [sensor_msgs/msg/PointCloud2]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_lidar_pointcloud_topic"
  exit 1
fi

if [ "${topgear_sensor_suite_present:-0}" -ne 1 ]; then
  echo "topgear_sensor_suite_missing"
  exit 1
fi

if [ "${topgear_sensor_camera_count:-0}" -ne 4 ]; then
  echo "topgear_sensor_camera_count_wrong"
  exit 1
fi

if [ "${topgear_sensor_lidar_count:-0}" -lt 1 ]; then
  echo "topgear_sensor_lidar_missing"
  exit 1
fi

if [ "${topgear_sensor_renderer_count:-0}" -lt 5 ]; then
  echo "topgear_sensor_visual_detail_too_low"
  exit 1
fi

if [ "${topgear_sensor_vlp16_official_mesh_count:-0}" -lt 1 ]; then
  echo "topgear_sensor_vlp16_official_mesh_missing"
  exit 1
fi

if [ "${topgear_sensor_d405_official_stl_count:-0}" -lt 4 ]; then
  echo "topgear_sensor_d405_official_stl_missing"
  exit 1
fi

if [ "${topgear_sensor_procedural_vlp16_rib_count:-0}" -ne 0 ]; then
  echo "topgear_sensor_procedural_vlp16_rib_residue"
  exit 1
fi

if [ "${topgear_sensor_procedural_d405_screw_count:-0}" -ne 0 ]; then
  echo "topgear_sensor_procedural_d405_screw_residue"
  exit 1
fi

if [ "${topgear_sensor_collider_count:-1}" -ne 0 ]; then
  echo "topgear_sensor_visual_must_not_add_colliders"
  exit 1
fi

if [ "${topgear_sensor_rigidbody_count:-1}" -ne 0 ]; then
  echo "topgear_sensor_visual_must_not_add_rigidbodies"
  exit 1
fi

if [ "${topgear_visual_collider_count:-1}" -ne 0 ]; then
  echo "topgear_visual_must_stay_without_colliders"
  exit 1
fi

if [ "${topgear_visual_rigidbody_count:-1}" -ne 0 ]; then
  echo "topgear_visual_must_stay_without_rigidbodies"
  exit 1
fi

if [ ! -s "$TOPGEAR_SENSOR_SCREENSHOT_FILE" ]; then
  echo "topgear_sensor_suite_screenshot_missing"
  exit 1
fi

echo "VLN_TOPGEAR_SENSOR_SUITE_SMOKE_TEST_PASS"
