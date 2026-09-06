#!/usr/bin/env bash

# Mesa Topgear Mix 当前主线传感器验收：四路宽屏普通 RGB/pinhole 120° + 高频 LiDAR。
# 只验证传感器配置、四路预览截图、ROS2 topic 实际频率和 CameraInfo；不跑自动导航路线。

set -euo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
SCENE_PATH="Assets/VLN/Scenes/VLNMesaTopgearMixVehicleScale4p0WorldCandidate.unity"
RUN_ID="vln_mesa_topgear_pinhole_rgb_sensor_rate_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
APPLY_UNITY_LOG="$LOG_DIR/unity_apply_pinhole_rgb_config.log"
PLAY_UNITY_LOG="$LOG_DIR/unity_pinhole_rgb_sensor_rate_play.log"
RESULT_FILE="$UNITY_PROJECT/Logs/vln_topgear_pinhole_rgb_sensor_config_result.txt"
PREVIEW_DIR="$UNITY_PROJECT/Logs/topgear_pinhole_rgb_previews"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
PINHOLE_CAPTURE_LOG="$LOG_DIR/ros2_pinhole_rgb_capture_validate.log"
PINHOLE_CAPTURE_DIR="$LOG_DIR/ros2_pinhole_rgb_capture"
PROBE_RESULT_FILE="mesa_topgear_mix_sensor_rate_probe/${RUN_ID}.txt"

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

echo "Mesa Topgear Mix 宽屏普通 RGB/pinhole 传感器 smoke test：配置 120° FOV、960x540、17Hz，导出四路预览，测 ROS2 实际发布频率。" | tee "$LOG_DIR/run_summary.txt"

if pgrep -af "$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity" | grep -F -- "-projectPath $UNITY_PROJECT" >/dev/null 2>&1; then
  echo "请先关闭当前大资产副本 Unity Editor，再运行该自动验收脚本。"
  echo "原因：脚本会另起 batch Unity 打开同一工程，避免和你手工编辑的场景抢工程锁。"
  exit 2
fi

if [ -f "$RESULT_FILE" ]; then
  cp "$RESULT_FILE" "$LOG_DIR/previous_$(basename "$RESULT_FILE")"
fi
if [ -d "$PREVIEW_DIR" ]; then
  mkdir -p "$LOG_DIR/previous_topgear_pinhole_rgb_previews"
  find "$PREVIEW_DIR" -maxdepth 1 -type f -name '*.png' -exec cp {} "$LOG_DIR/previous_topgear_pinhole_rgb_previews/" \;
fi

set +e
timeout 900s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -quit \
  -executeMethod VLN.Editor.VlnTopgearPinholeRgbSensorConfig.ApplyMesaTopgearMixSceneBatch \
  -logFile "$APPLY_UNITY_LOG"
apply_status=$?
set -e

if [ -f "$RESULT_FILE" ]; then
  cp "$RESULT_FILE" "$LOG_DIR/$(basename "$RESULT_FILE")"
fi
if [ -d "$PREVIEW_DIR" ]; then
  mkdir -p "$LOG_DIR/topgear_pinhole_rgb_previews"
  find "$PREVIEW_DIR" -maxdepth 1 -type f -name '*.png' -exec cp {} "$LOG_DIR/topgear_pinhole_rgb_previews/" \;
fi

config_success=$(get_kv_value "$RESULT_FILE" success)
camera_count=$(get_kv_value "$RESULT_FILE" camera_count)
lidar_count=$(get_kv_value "$RESULT_FILE" lidar_count)
camera_model=$(get_kv_value "$RESULT_FILE" camera_projection_model)
camera_distortion=$(get_kv_value "$RESULT_FILE" camera_distortion_model)
camera_fov=$(get_kv_value "$RESULT_FILE" camera_target_fov_deg)
camera_frequency=$(get_kv_value "$RESULT_FILE" camera_target_frequency_hz)
camera_resolution=$(get_kv_value "$RESULT_FILE" camera_resolution)
fisheye_enabled=$(get_kv_value "$RESULT_FILE" fisheye_runtime_enabled)
fisheye_sensor_disabled_count=$(get_kv_value "$RESULT_FILE" fisheye_sensor_disabled_count)
image_publisher_rgb_source_count=$(get_kv_value "$RESULT_FILE" image_publisher_rgb_source_count)
depth_buffered_rgb_sensor_count=$(get_kv_value "$RESULT_FILE" depth_buffered_rgb_sensor_configured_count)
rgb_sensor_disabled_count=$(get_kv_value "$RESULT_FILE" rgb_sensor_disabled_count)
vln_pinhole_rgb_image_publisher_count=$(get_kv_value "$RESULT_FILE" vln_pinhole_rgb_image_publisher_configured_count)
unity_sensors_image_publisher_enabled_count=$(get_kv_value "$RESULT_FILE" unity_sensors_image_publisher_enabled_count)
unity_sensors_image_publisher_disabled_count=$(get_kv_value "$RESULT_FILE" unity_sensors_image_publisher_disabled_count)
camera_info_count=$(get_kv_value "$RESULT_FILE" camera_info_publisher_configured_count)
legacy_fisheye_info_enabled=$(get_kv_value "$RESULT_FILE" legacy_fisheye_camera_info_enabled)
post_process_disabled_count=$(get_kv_value "$RESULT_FILE" post_process_layer_disabled_count)
lidar_frequency=$(get_kv_value "$RESULT_FILE" lidar_target_frequency_hz)
lidar_max_range=$(get_kv_value "$RESULT_FILE" lidar_target_max_range_m)
lidar_points_per_scan=$(get_kv_value "$RESULT_FILE" lidar_target_points_per_scan)
lidar_pitch=$(get_kv_value "$RESULT_FILE" lidar_downward_pitch_deg)
lidar_raycast_mask=$(get_kv_value "$RESULT_FILE" lidar_raycast_layer_mask)
lidar_raycast_mask_set_count=$(get_kv_value "$RESULT_FILE" lidar_raycast_layer_mask_set_count)
lidar_pitch_set_count=$(get_kv_value "$RESULT_FILE" lidar_pitch_set_count)
preview_file_count=$(get_kv_value "$RESULT_FILE" preview_file_count)

{
  echo "apply_status=$apply_status"
  echo "config_success=${config_success:-missing}"
  echo "camera_count=${camera_count:-missing}"
  echo "lidar_count=${lidar_count:-missing}"
  echo "camera_projection_model=${camera_model:-missing}"
  echo "camera_distortion_model=${camera_distortion:-missing}"
  echo "camera_target_fov_deg=${camera_fov:-missing}"
  echo "camera_resolution=${camera_resolution:-missing}"
  echo "camera_target_frequency_hz=${camera_frequency:-missing}"
  echo "fisheye_runtime_enabled=${fisheye_enabled:-missing}"
  echo "legacy_fisheye_camera_info_enabled=${legacy_fisheye_info_enabled:-missing}"
  echo "depth_buffered_rgb_sensor_configured_count=${depth_buffered_rgb_sensor_count:-missing}"
  echo "rgb_sensor_disabled_count=${rgb_sensor_disabled_count:-missing}"
  echo "vln_pinhole_rgb_image_publisher_configured_count=${vln_pinhole_rgb_image_publisher_count:-missing}"
  echo "unity_sensors_image_publisher_enabled_count=${unity_sensors_image_publisher_enabled_count:-missing}"
  echo "unity_sensors_image_publisher_disabled_count=${unity_sensors_image_publisher_disabled_count:-missing}"
  echo "fisheye_sensor_disabled_count=${fisheye_sensor_disabled_count:-missing}"
  echo "image_publisher_rgb_source_count=${image_publisher_rgb_source_count:-missing}"
  echo "camera_info_publisher_configured_count=${camera_info_count:-missing}"
  echo "post_process_layer_disabled_count=${post_process_disabled_count:-missing}"
  echo "lidar_target_frequency_hz=${lidar_frequency:-missing}"
  echo "lidar_target_max_range_m=${lidar_max_range:-missing}"
  echo "lidar_target_points_per_scan=${lidar_points_per_scan:-missing}"
  echo "lidar_downward_pitch_deg=${lidar_pitch:-missing}"
  echo "lidar_raycast_layer_mask=${lidar_raycast_mask:-missing}"
  echo "lidar_raycast_layer_mask_set_count=${lidar_raycast_mask_set_count:-missing}"
  echo "lidar_pitch_set_count=${lidar_pitch_set_count:-missing}"
  echo "preview_file_count=${preview_file_count:-missing}"
} | tee -a "$LOG_DIR/run_summary.txt"

if [ "$apply_status" -ne 0 ]; then
  echo "unity_apply_pinhole_rgb_config_failed"
  grep -n -E "VLN_TOPGEAR_PINHOLE|Exception|NullReference|error CS|Compilation failed|Exiting" "$APPLY_UNITY_LOG" | sed -n '1,260p' || true
  exit 1
fi
if [ "${config_success:-0}" != "1" ]; then echo "pinhole_rgb_config_result_failed"; sed -n '1,180p' "$RESULT_FILE" 2>/dev/null || true; exit 1; fi
if [ "${camera_model:-missing}" != "pinhole" ]; then echo "camera_projection_model_not_pinhole"; exit 1; fi
if [ "${camera_distortion:-missing}" != "none" ]; then echo "camera_distortion_model_not_none"; exit 1; fi
if [ "${fisheye_enabled:-1}" != "0" ]; then echo "fisheye_runtime_should_be_disabled"; exit 1; fi
if [ "${legacy_fisheye_info_enabled:-1}" != "0" ]; then echo "legacy_fisheye_camera_info_should_be_disabled"; exit 1; fi
if [ "${camera_count:-0}" -ne 4 ]; then echo "pinhole_camera_count_failed"; exit 1; fi
if [ "${lidar_count:-0}" -lt 1 ]; then echo "lidar_count_failed"; exit 1; fi
if [ "${depth_buffered_rgb_sensor_count:-0}" -ne 4 ]; then echo "depth_buffered_rgb_sensor_configured_count_failed"; exit 1; fi
if [ "${rgb_sensor_disabled_count:-0}" -ne 4 ]; then echo "legacy_rgb_sensor_should_be_disabled_for_depth_buffered_ros_publish"; exit 1; fi
if [ "${vln_pinhole_rgb_image_publisher_count:-999}" -ne 0 ]; then echo "legacy_vln_pinhole_rgb_image_publisher_should_be_disabled"; exit 1; fi
if [ "${unity_sensors_image_publisher_enabled_count:-0}" -ne 4 ]; then echo "unity_sensors_image_publisher_should_be_enabled"; exit 1; fi
if [ "${fisheye_sensor_disabled_count:-0}" -ne 4 ]; then echo "fisheye_sensor_disabled_count_failed"; exit 1; fi
if [ "${image_publisher_rgb_source_count:-0}" -ne 4 ]; then echo "image_publisher_not_bound_to_depth_buffered_rgb"; exit 1; fi
if [ "${camera_info_count:-0}" -ne 4 ]; then echo "camera_info_not_configured"; exit 1; fi
if [ "${post_process_disabled_count:-0}" -ne 4 ]; then echo "post_process_layer_not_disabled"; exit 1; fi
if [ "${camera_resolution:-missing}" != "960x540" ]; then echo "camera_resolution_failed"; exit 1; fi
if ! awk -v value="${camera_fov:-0}" 'BEGIN { exit !((value + 0) >= 119.9 && (value + 0) <= 120.1) }'; then echo "camera_fov_target_failed"; exit 1; fi
if ! awk -v value="${camera_frequency:-0}" 'BEGIN { exit !((value + 0) >= 15.0) }'; then echo "camera_frequency_target_failed"; exit 1; fi
if ! awk -v value="${lidar_frequency:-0}" 'BEGIN { exit !((value + 0) >= 15.0) }'; then echo "lidar_frequency_target_failed"; exit 1; fi
if [ "${lidar_raycast_mask:-missing}" != "129" ]; then echo "lidar_raycast_layer_mask_failed"; exit 1; fi
if [ "${lidar_raycast_mask_set_count:-0}" -lt 1 ]; then echo "lidar_raycast_layer_mask_set_count_failed"; exit 1; fi
if [ "${lidar_pitch_set_count:-0}" -lt 1 ]; then echo "lidar_pitch_set_count_failed"; exit 1; fi
if [ "${preview_file_count:-0}" -lt 4 ]; then echo "pinhole_preview_count_failed"; exit 1; fi

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
timeout 105s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_capture_pinhole_rgb_images.py --output-dir '$PINHOLE_CAPTURE_DIR' --timeout 100 --width 960 --height 540 --encoding rgb8 --fov-deg 120" >"$PINHOLE_CAPTURE_LOG" 2>&1 &
pinhole_capture_pid=$!
bash -lc "sleep 12; $ROS_ENV; timeout 12s ros2 topic list -t" >"$TOPIC_LOG" 2>&1 &
topic_pid=$!

rm -f "$UNITY_PROJECT/Logs/$PROBE_RESULT_FILE"
export VLN_PROBE_SCENE_PATH="$SCENE_PATH"
export VLN_PROBE_RESULT_FILE="$PROBE_RESULT_FILE"
export VLN_PROBE_AUTO_EXIT_SECONDS="42.0"
export VLN_PROBE_SAMPLE_INTERVAL_SECONDS="0.50"
export VLN_PROBE_WRITE_FIXED_STEP_CSV="0"
export VLN_DIAG_SKIP_LIDAR_RAY_SAMPLE="1"

set +e
timeout 125s "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -batchmode \
  -executeMethod VLN.Editor.VlnMesaTopgearMixReadOnlyRuntimeProbeRunner.RunReadOnlyPerformanceProbe \
  -logFile "$PLAY_UNITY_LOG"
unity_status=$?
if [ "$unity_status" -ne 0 ]; then
  kill "$front_freq_pid" "$rear_freq_pid" "$left_freq_pid" "$right_freq_pid" "$lidar_freq_pid" "$pinhole_capture_pid" "$topic_pid" >/dev/null 2>&1 || true
fi
wait "$front_freq_pid"; front_freq_status=$?
wait "$rear_freq_pid"; rear_freq_status=$?
wait "$left_freq_pid"; left_freq_status=$?
wait "$right_freq_pid"; right_freq_status=$?
wait "$lidar_freq_pid"; lidar_freq_status=$?
wait "$pinhole_capture_pid"; pinhole_capture_status=$?
wait "$topic_pid"; topic_status=$?
set -e

front_hz=$(get_kv_value "$FRONT_FREQ_LOG" average_hz)
rear_hz=$(get_kv_value "$REAR_FREQ_LOG" average_hz)
left_hz=$(get_kv_value "$LEFT_FREQ_LOG" average_hz)
right_hz=$(get_kv_value "$RIGHT_FREQ_LOG" average_hz)
lidar_hz=$(get_kv_value "$LIDAR_FREQ_LOG" average_hz)
probe_result="$UNITY_PROJECT/Logs/$PROBE_RESULT_FILE"
active_fisheye=$(get_kv_value "$probe_result" active_fisheye_sensor_count)
active_rgb=$(get_kv_value "$probe_result" active_rgb_sensor_count)
active_vln_pinhole=$(get_kv_value "$probe_result" active_vln_pinhole_rgb_image_publisher_count)

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "front_freq_status=$front_freq_status"
  echo "rear_freq_status=$rear_freq_status"
  echo "left_freq_status=$left_freq_status"
  echo "right_freq_status=$right_freq_status"
  echo "lidar_freq_status=$lidar_freq_status"
  echo "pinhole_capture_status=$pinhole_capture_status"
  echo "topic_status=$topic_status"
  echo "front_image_average_hz=${front_hz:-missing}"
  echo "rear_image_average_hz=${rear_hz:-missing}"
  echo "left_image_average_hz=${left_hz:-missing}"
  echo "right_image_average_hz=${right_hz:-missing}"
  echo "lidar_average_hz=${lidar_hz:-missing}"
  echo "runtime_active_fisheye_sensor_count=${active_fisheye:-missing}"
  echo "runtime_active_rgb_sensor_count=${active_rgb:-missing}"
  echo "runtime_active_vln_pinhole_rgb_image_publisher_count=${active_vln_pinhole:-missing}"
  echo "preview_dir=$PREVIEW_DIR"
  echo "copied_preview_dir=$LOG_DIR/topgear_pinhole_rgb_previews"
  echo "ros2_pinhole_capture_dir=$PINHOLE_CAPTURE_DIR"
  echo "probe_result=$probe_result"
  echo "log_dir=$LOG_DIR"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "普通 RGB 配置结果："
sed -n '1,180p' "$RESULT_FILE" 2>/dev/null || true
echo "四路相机频率："
sed -n '1,80p' "$FRONT_FREQ_LOG" || true
sed -n '1,80p' "$REAR_FREQ_LOG" || true
sed -n '1,80p' "$LEFT_FREQ_LOG" || true
sed -n '1,80p' "$RIGHT_FREQ_LOG" || true
echo "LiDAR 频率："
sed -n '1,100p' "$LIDAR_FREQ_LOG" || true
echo "ROS2 普通 RGB 图像和 CameraInfo："
sed -n '1,220p' "$PINHOLE_CAPTURE_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n -E '/vln/(front|rear|left|right|lidar)|/tf' "$TOPIC_LOG" || true
echo "Key Unity log lines:"
grep -n -E "VLN_TOPGEAR_PINHOLE|VLN_MESA_TOPGEAR|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$APPLY_UNITY_LOG" "$PLAY_UNITY_LOG" | sed -n '1,360p' || true

if [ "$unity_status" -ne 0 ]; then echo "unity_pinhole_rgb_sensor_rate_play_failed"; exit 1; fi
if [ "${active_fisheye:-999}" -ne 0 ]; then echo "runtime_fisheye_sensor_should_be_zero"; exit 1; fi
if [ "${active_rgb:-0}" -ne 4 ]; then echo "runtime_depth_buffered_rgb_sensor_count_failed"; exit 1; fi
if [ "${active_vln_pinhole:-999}" -ne 0 ]; then echo "runtime_legacy_vln_pinhole_rgb_image_publisher_should_be_disabled"; exit 1; fi
if [ "$front_freq_status" -ne 0 ] || [ "$rear_freq_status" -ne 0 ] || [ "$left_freq_status" -ne 0 ] || [ "$right_freq_status" -ne 0 ]; then echo "ros2_camera_frequency_validation_failed"; exit 1; fi
if [ "$lidar_freq_status" -ne 0 ]; then echo "ros2_lidar_frequency_validation_failed"; exit 1; fi
if [ "$pinhole_capture_status" -ne 0 ]; then echo "ros2_pinhole_rgb_capture_validation_failed"; exit 1; fi

echo "VLN_MESA_TOPGEAR_PINHOLE_RGB_SENSOR_RATE_SMOKE_TEST_PASS" | tee -a "$LOG_DIR/run_summary.txt"
