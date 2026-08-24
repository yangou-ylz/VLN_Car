#!/usr/bin/env bash

# 阶段 21：Mesa Topgear 四路鱼眼/广角相机与高频 LiDAR 发布验收。
# 只验证当前 Mesa Topgear 场景的传感器参数、四路预览截图和 ROS2 topic 实际频率；不跑自动路线。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
RUN_ID="vln_mesa_topgear_fisheye_sensor_rate_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
APPLY_UNITY_LOG="$LOG_DIR/unity_apply_fisheye_config.log"
PLAY_UNITY_LOG="$LOG_DIR/unity_sensor_rate_play.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_topgear_fisheye_sensor_config_result.txt"
PREVIEW_DIR="$UNITY_PROJECT/Logs/topgear_fisheye_previews"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"

FRONT_FREQ_LOG="$LOG_DIR/ros2_front_image_frequency.log"
REAR_FREQ_LOG="$LOG_DIR/ros2_rear_image_frequency.log"
LEFT_FREQ_LOG="$LOG_DIR/ros2_left_image_frequency.log"
RIGHT_FREQ_LOG="$LOG_DIR/ros2_right_image_frequency.log"
LIDAR_FREQ_LOG="$LOG_DIR/ros2_lidar_pointcloud_frequency.log"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

get_kv_value()
{
  local file_path="$1"
  local key="$2"
  awk -F= -v key="$key" '
    NR == 1 { sub(/^\xef\xbb\xbf/, "", $1) }
    $1 == key {
      sub(/^[^=]*=/, "")
      value = $0
    }
    END { if (value != "") print value }
  ' "$file_path" 2>/dev/null || true
}

echo "Mesa Topgear 鱼眼/高频传感器 smoke test：配置 FOV/频率，导出四路预览，测 ROS2 实际发布频率。" | tee "$LOG_DIR/run_summary.txt"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "unity_project_already_open=true" | tee -a "$LOG_DIR/run_summary.txt"
  echo "请先关闭当前大资产副本 Unity Editor，再运行该自动验收脚本。"
  exit 2
fi

if [ -f "$RESULT_FILE" ]; then
  mv "$RESULT_FILE" "$LOG_DIR/previous_$(basename "$RESULT_FILE")"
fi
if [ -d "$PREVIEW_DIR" ]; then
  mkdir -p "$LOG_DIR/previous_topgear_fisheye_previews"
  find "$PREVIEW_DIR" -maxdepth 1 -type f -name '*.png' -exec cp {} "$LOG_DIR/previous_topgear_fisheye_previews/" \;
fi

set +e
timeout 900s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -quit \
  -executeMethod VLN.Editor.VlnTopgearFisheyeSensorConfig.ApplyMesaTopgearSceneBatch \
  -logFile "$APPLY_UNITY_LOG"
apply_status=$?
set -e

if [ -f "$RESULT_FILE" ]; then
  cp "$RESULT_FILE" "$LOG_DIR/$(basename "$RESULT_FILE")"
fi
if [ -d "$PREVIEW_DIR" ]; then
  mkdir -p "$LOG_DIR/topgear_fisheye_previews"
  find "$PREVIEW_DIR" -maxdepth 1 -type f -name '*.png' -exec cp {} "$LOG_DIR/topgear_fisheye_previews/" \;
fi

config_success=$(get_kv_value "$RESULT_FILE" success)
camera_count=$(get_kv_value "$RESULT_FILE" camera_count)
lidar_count=$(get_kv_value "$RESULT_FILE" lidar_count)
camera_fov=$(get_kv_value "$RESULT_FILE" camera_target_fov_deg)
camera_frequency=$(get_kv_value "$RESULT_FILE" camera_target_frequency_hz)
lidar_frequency=$(get_kv_value "$RESULT_FILE" lidar_target_frequency_hz)
lidar_max_range=$(get_kv_value "$RESULT_FILE" lidar_target_max_range_m)
lidar_points_per_scan=$(get_kv_value "$RESULT_FILE" lidar_applied_points_per_scan)
lidar_scan_pattern_size=$(get_kv_value "$RESULT_FILE" lidar_scan_pattern_size)
distortion_enabled=$(get_kv_value "$RESULT_FILE" lens_distortion_enabled)
distortion_intensity=$(get_kv_value "$RESULT_FILE" lens_distortion_intensity)
distortion_scale=$(get_kv_value "$RESULT_FILE" lens_distortion_scale)
post_process_layer_count=$(get_kv_value "$RESULT_FILE" post_process_layer_set_count)
distortion_volume_configured=$(get_kv_value "$RESULT_FILE" lens_distortion_volume_configured)
lidar_max_range_set_count=$(get_kv_value "$RESULT_FILE" lidar_max_range_set_count)
lidar_points_per_scan_set_count=$(get_kv_value "$RESULT_FILE" lidar_points_per_scan_set_count)
preview_file_count=$(get_kv_value "$RESULT_FILE" preview_file_count)

{
  echo "apply_status=$apply_status"
  echo "config_success=${config_success:-missing}"
  echo "camera_count=${camera_count:-missing}"
  echo "lidar_count=${lidar_count:-missing}"
  echo "camera_target_fov_deg=${camera_fov:-missing}"
  echo "camera_target_frequency_hz=${camera_frequency:-missing}"
  echo "lidar_target_frequency_hz=${lidar_frequency:-missing}"
  echo "lidar_target_max_range_m=${lidar_max_range:-missing}"
  echo "lidar_applied_points_per_scan=${lidar_points_per_scan:-missing}"
  echo "lidar_scan_pattern_size=${lidar_scan_pattern_size:-missing}"
  echo "lens_distortion_enabled=${distortion_enabled:-missing}"
  echo "lens_distortion_intensity=${distortion_intensity:-missing}"
  echo "lens_distortion_scale=${distortion_scale:-missing}"
  echo "post_process_layer_set_count=${post_process_layer_count:-missing}"
  echo "lens_distortion_volume_configured=${distortion_volume_configured:-missing}"
  echo "lidar_max_range_set_count=${lidar_max_range_set_count:-missing}"
  echo "lidar_points_per_scan_set_count=${lidar_points_per_scan_set_count:-missing}"
  echo "preview_file_count=${preview_file_count:-missing}"
} | tee -a "$LOG_DIR/run_summary.txt"

if [ "$apply_status" -ne 0 ]; then
  echo "unity_apply_fisheye_config_failed"
  grep -n -E "VLN_TOPGEAR_FISHEYE|Exception|NullReference|error CS|Compilation failed|Exiting" "$APPLY_UNITY_LOG" | sed -n '1,260p' || true
  exit 1
fi
if [ "${config_success:-0}" != "1" ]; then
  echo "fisheye_config_result_failed"
  sed -n '1,180p' "$RESULT_FILE" 2>/dev/null || true
  exit 1
fi
if [ "${camera_count:-0}" -ne 4 ]; then echo "fisheye_config_camera_count_failed"; exit 1; fi
if [ "${lidar_count:-0}" -lt 1 ]; then echo "fisheye_config_lidar_count_failed"; exit 1; fi
if [ "${distortion_enabled:-0}" != "1" ]; then echo "lens_distortion_not_enabled"; exit 1; fi
if [ "${post_process_layer_count:-0}" -ne 4 ]; then echo "lens_distortion_post_process_layer_count_failed"; exit 1; fi
if [ "${distortion_volume_configured:-0}" != "1" ]; then echo "lens_distortion_volume_missing"; exit 1; fi
if [ "${lidar_max_range_set_count:-0}" -lt 1 ]; then echo "lidar_max_range_not_set"; exit 1; fi
if [ "${lidar_points_per_scan_set_count:-0}" -lt 1 ]; then echo "lidar_points_per_scan_not_set"; exit 1; fi
if ! awk -v value="${lidar_max_range:-0}" 'BEGIN { exit !((value + 0) >= 89.9 && (value + 0) <= 90.1) }'; then echo "lidar_max_range_target_failed"; exit 1; fi
if ! awk -v value="${lidar_points_per_scan:-0}" 'BEGIN { exit !((value + 0) >= 57600) }'; then echo "lidar_points_per_scan_target_failed"; exit 1; fi
if [ "${preview_file_count:-0}" -lt 4 ]; then echo "fisheye_preview_count_failed"; exit 1; fi

endpoint_pid=""
cleanup()
{
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
  for _ in $(seq 1 80); do
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

timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_measure_topic_frequency.py --topic /vln/front/image_raw --msg-type image --duration 8 --timeout 100 --min-hz 15 --frame-id front_camera_optical_frame" >"$FRONT_FREQ_LOG" 2>&1 &
front_freq_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_measure_topic_frequency.py --topic /vln/rear/image_raw --msg-type image --duration 8 --timeout 100 --min-hz 15 --frame-id rear_camera_optical_frame" >"$REAR_FREQ_LOG" 2>&1 &
rear_freq_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_measure_topic_frequency.py --topic /vln/left/image_raw --msg-type image --duration 8 --timeout 100 --min-hz 15 --frame-id left_camera_optical_frame" >"$LEFT_FREQ_LOG" 2>&1 &
left_freq_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_measure_topic_frequency.py --topic /vln/right/image_raw --msg-type image --duration 8 --timeout 100 --min-hz 15 --frame-id right_camera_optical_frame" >"$RIGHT_FREQ_LOG" 2>&1 &
right_freq_pid=$!
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_measure_topic_frequency.py --topic /vln/lidar/points --msg-type pointcloud2 --duration 8 --timeout 100 --min-hz 15 --frame-id lidar_link" >"$LIDAR_FREQ_LOG" 2>&1 &
lidar_freq_pid=$!
bash -lc "sleep 12; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

set +e
timeout 125s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnMesaTopgearVehicleCandidateBuilder.RunExistingSceneSensorRateSmokeTest \
  -logFile "$PLAY_UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$front_freq_pid" "$rear_freq_pid" "$left_freq_pid" "$right_freq_pid" "$lidar_freq_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$front_freq_pid"; front_freq_status=$?
wait "$rear_freq_pid"; rear_freq_status=$?
wait "$left_freq_pid"; left_freq_status=$?
wait "$right_freq_pid"; right_freq_status=$?
wait "$lidar_freq_pid"; lidar_freq_status=$?
wait "$topic_pid"; topic_status=$?
set -e

front_hz=$(get_kv_value "$FRONT_FREQ_LOG" average_hz)
rear_hz=$(get_kv_value "$REAR_FREQ_LOG" average_hz)
left_hz=$(get_kv_value "$LEFT_FREQ_LOG" average_hz)
right_hz=$(get_kv_value "$RIGHT_FREQ_LOG" average_hz)
lidar_hz=$(get_kv_value "$LIDAR_FREQ_LOG" average_hz)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "front_freq_status=$front_freq_status"
  echo "rear_freq_status=$rear_freq_status"
  echo "left_freq_status=$left_freq_status"
  echo "right_freq_status=$right_freq_status"
  echo "lidar_freq_status=$lidar_freq_status"
  echo "topic_status=$topic_status"
  echo "front_image_average_hz=${front_hz:-missing}"
  echo "rear_image_average_hz=${rear_hz:-missing}"
  echo "left_image_average_hz=${left_hz:-missing}"
  echo "right_image_average_hz=${right_hz:-missing}"
  echo "lidar_average_hz=${lidar_hz:-missing}"
  echo "preview_dir=$PREVIEW_DIR"
  echo "copied_preview_dir=$LOG_DIR/topgear_fisheye_previews"
  echo "log_dir=$LOG_DIR"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "鱼眼/高频配置结果："
sed -n '1,180p' "$RESULT_FILE" 2>/dev/null || true
echo "四路相机频率："
sed -n '1,80p' "$FRONT_FREQ_LOG" || true
sed -n '1,80p' "$REAR_FREQ_LOG" || true
sed -n '1,80p' "$LEFT_FREQ_LOG" || true
sed -n '1,80p' "$RIGHT_FREQ_LOG" || true
echo "LiDAR 频率："
sed -n '1,100p' "$LIDAR_FREQ_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|rear|left|right|lidar)|/tf' "$TOPIC_LOG" || true
echo "Key Unity log lines:"
grep -n -E "VLN_TOPGEAR_FISHEYE|VLN_MESA_TOPGEAR|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$APPLY_UNITY_LOG" "$PLAY_UNITY_LOG" | sed -n '1,360p' || true

if [ "$unity_status" -ne 0 ]; then echo "unity_sensor_rate_play_failed"; exit 1; fi
if [ "$front_freq_status" -ne 0 ] || [ "$rear_freq_status" -ne 0 ] || [ "$left_freq_status" -ne 0 ] || [ "$right_freq_status" -ne 0 ]; then echo "ros2_camera_frequency_validation_failed"; exit 1; fi
if [ "$lidar_freq_status" -ne 0 ]; then echo "ros2_lidar_frequency_validation_failed"; exit 1; fi

echo "VLN_MESA_TOPGEAR_FISHEYE_SENSOR_RATE_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
