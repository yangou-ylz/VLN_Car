#!/usr/bin/env bash

# 运行 UnitySensors LiDAR -> ROS2 PointCloud2 最小闭环测试。
# 验收内容：ROS2 收到 /vln/lidar/points，类型 sensor_msgs/msg/PointCloud2，7200 点/帧，约 5Hz。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
RUN_ID="vln_lidar_$(date +%Y%m%d_%H%M%S)"
LOG_DIR="$VLN_ROOT/UnityProjects/_SmokeTestLogs/$RUN_ID"
ENDPOINT_LOG="$LOG_DIR/endpoint.log"
UNITY_LOG="$LOG_DIR/unity.log"
CLOUD_LOG="$LOG_DIR/ros2_pointcloud2_once.log"
HZ_LOG="$LOG_DIR/ros2_pointcloud2_hz.log"
BW_LOG="$LOG_DIR/ros2_pointcloud2_bw.log"
TOPIC_LOG="$LOG_DIR/ros2_topic_list.log"
RESULT_FILE="$VLN_ROOT/UnityProjects/VLN_Offroad/Logs/vln_unitysensors_lidar_result.txt"
POINTS_TOPIC="/vln/lidar/points"

mkdir -p "$LOG_DIR" "$VLN_ROOT/.ros/log"
export ROS_LOG_DIR="${ROS_LOG_DIR:-$VLN_ROOT/.ros/log}"

endpoint_pid=""

cleanup()
{
  if [ -n "$endpoint_pid" ]; then
    kill "$endpoint_pid" >/dev/null 2>&1 || true
    wait "$endpoint_pid" >/dev/null 2>&1 || true
  fi
}

trap cleanup EXIT

if [ -f "$RESULT_FILE" ]; then
  mv "$RESULT_FILE" "$LOG_DIR/previous_vln_unitysensors_lidar_result.txt"
fi

if ss -ltn 2>/dev/null | grep -E -q ':10000\b'; then
  echo "endpoint_already_listening=true" | tee "$LOG_DIR/run_summary.txt"
else
  "$VLN_ROOT/scripts/start_ros_tcp_endpoint.sh" >"$ENDPOINT_LOG" 2>&1 &
  endpoint_pid=$!
  echo "endpoint_pid=$endpoint_pid" | tee "$LOG_DIR/run_summary.txt"
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

timeout 85s bash -lc "$ROS_ENV; python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic $POINTS_TOPIC --width 7200 --point-step 16 --frame-id lidar_link --timeout 80 --min-nonzero-points 20" >"$CLOUD_LOG" 2>&1 &
cloud_pid=$!

timeout 34s bash -lc "$ROS_ENV; ros2 topic hz --window 5 $POINTS_TOPIC" >"$HZ_LOG" 2>&1 &
hz_pid=$!

timeout 34s bash -lc "$ROS_ENV; ros2 topic bw $POINTS_TOPIC" >"$BW_LOG" 2>&1 &
bw_pid=$!

set +e
timeout 100s "$VLN_ROOT/scripts/open_unity_vln_project.sh" \
  -batchmode -nographics \
  -executeMethod VLN.Editor.VlnUnitySensorsLidarSmokeTestRunner.Run \
  -logFile "$UNITY_LOG"
unity_status=$?
wait "$cloud_pid"
cloud_status=$?
wait "$hz_pid"
hz_status_raw=$?
hz_status=$hz_status_raw
if [ "$hz_status_raw" -eq 124 ] && grep -q 'average rate:' "$HZ_LOG"; then
  hz_status=0
fi
wait "$bw_pid"
bw_status_raw=$?
bw_status=$bw_status_raw
if [ "$bw_status_raw" -eq 124 ] && grep -E -q '([KMGT]?B/s|MB/s) from [0-9]+ messages' "$BW_LOG"; then
  bw_status=0
fi
timeout 12s bash -lc "$ROS_ENV; ros2 topic list -t" >"$TOPIC_LOG" 2>&1
topic_status=$?
set -e

{
  echo "run_id=$RUN_ID"
  echo "unity_status=$unity_status"
  echo "cloud_status=$cloud_status"
  echo "hz_status_raw=$hz_status_raw"
  echo "hz_status=$hz_status"
  echo "bw_status_raw=$bw_status_raw"
  echo "bw_status=$bw_status"
  echo "topic_status=$topic_status"
  echo "log_dir=$LOG_DIR"
  echo "result_file=$RESULT_FILE"
  echo "points_topic=$POINTS_TOPIC"
} | tee -a "$LOG_DIR/run_summary.txt"

echo "ROS2 pointcloud once output:"
sed -n '1,140p' "$CLOUD_LOG" || true
echo "ROS2 topic list excerpt:"
grep -n "/vln/lidar/points" "$TOPIC_LOG" || true
echo "ROS2 pointcloud hz output:"
sed -n '1,140p' "$HZ_LOG" || true
echo "ROS2 pointcloud bw output:"
sed -n '1,140p' "$BW_LOG" || true
echo "Unity lidar result:"
sed -n '1,80p' "$RESULT_FILE" 2>/dev/null || true
echo "Key Unity log lines:"
grep -n -E "VLN_UNITYSENSORS_LIDAR|Incompatible protocol|Exception|NullReference|error CS|Compilation failed|SocketException|Connection.*failed|Successfully connected|Exiting" "$UNITY_LOG" | sed -n '1,260p' || true

if [ "$unity_status" -ne 0 ]; then
  echo "unity_failed"
  exit 1
fi

if [ "$cloud_status" -ne 0 ]; then
  echo "ros2_pointcloud2_message_validation_failed"
  exit 1
fi

if ! grep -q 'VLN_UNITYSENSORS_POINTCLOUD2_MSG_OK' "$CLOUD_LOG"; then
  echo "ros2_pointcloud2_message_missing_success_marker"
  exit 1
fi

if ! grep -F -q "$POINTS_TOPIC [sensor_msgs/msg/PointCloud2]" "$TOPIC_LOG"; then
  echo "ros2_topic_list_missing_pointcloud2_topic"
  exit 1
fi

if ! grep -q 'average rate:' "$HZ_LOG"; then
  echo "ros2_topic_hz_no_average_rate"
  exit 1
fi

if ! grep -E -q '([KMGT]?B/s|MB/s) from [0-9]+ messages' "$BW_LOG"; then
  echo "ros2_topic_bw_no_average"
  exit 1
fi

echo "VLN_UNITYSENSORS_LIDAR_SMOKE_TEST_PASS"
