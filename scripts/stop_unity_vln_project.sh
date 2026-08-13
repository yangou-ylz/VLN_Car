#!/usr/bin/env bash

# 安全结束当前 VLN Unity 工程相关进程。
# 用途：Unity Editor 卡死、无法关闭、或下次打开提示工程已被占用。
# 策略：先 SIGTERM，仍不退出再 SIGKILL；只移动 lock 文件，不删除 Library。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
PROJECT_DIR="$VLN_ROOT/UnityProjects/VLN_Offroad"
UNITY_EDITOR="$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity"
RECOVERY_DIR="$VLN_ROOT/UnityProjects/_ManualRecoveryLogs/stop_unity_$(date +%Y%m%d_%H%M%S)"

collect_pids()
{
  pgrep -f "$UNITY_EDITOR -projectPath $PROJECT_DIR" 2>/dev/null || true
  pgrep -f "Unity -adb2 .* -projectPath $PROJECT_DIR" 2>/dev/null || true
  pgrep -f "UnityPackageMan" 2>/dev/null || true
  pgrep -f "UnityAutoQuitte" 2>/dev/null || true
}

unique_pids()
{
  awk '!seen[$0]++ && $0 ~ /^[0-9]+$/'
}

pids="$(collect_pids | unique_pids | tr '\n' ' ')"

mkdir -p "$RECOVERY_DIR"

if [ -n "$pids" ]; then
  echo "检测到 Unity 相关进程：$pids" | tee "$RECOVERY_DIR/summary.txt"
  ps -fp $pids >"$RECOVERY_DIR/processes_before.txt" 2>/dev/null || true

  echo "先发送 SIGTERM..."
  kill -TERM $pids >/dev/null 2>&1 || true
  sleep 6

  remaining="$(collect_pids | unique_pids | tr '\n' ' ')"
  if [ -n "$remaining" ]; then
    echo "仍未退出，发送 SIGKILL：$remaining" | tee -a "$RECOVERY_DIR/summary.txt"
    kill -KILL $remaining >/dev/null 2>&1 || true
    sleep 2
  fi
else
  echo "未发现正在运行的 VLN Unity Editor/worker 进程。" | tee "$RECOVERY_DIR/summary.txt"
fi

remaining_after="$(collect_pids | unique_pids | tr '\n' ' ')"
if [ -n "$remaining_after" ]; then
  echo "警告：仍有残留进程：$remaining_after" | tee -a "$RECOVERY_DIR/summary.txt"
  ps -fp $remaining_after >"$RECOVERY_DIR/processes_after.txt" 2>/dev/null || true
else
  echo "Unity 相关进程已清理。" | tee -a "$RECOVERY_DIR/summary.txt"
fi

lock_dir="$RECOVERY_DIR/stale_locks"
mkdir -p "$lock_dir"
for lock_file in "$PROJECT_DIR/Library/ArtifactDB-lock" "$PROJECT_DIR/Library/SourceAssetDB-lock"; do
  if [ -f "$lock_file" ]; then
    mv "$lock_file" "$lock_dir/"
    echo "已移动 stale lock：$lock_file -> $lock_dir/" | tee -a "$RECOVERY_DIR/summary.txt"
  fi
done

if find "$PROJECT_DIR/Library" -maxdepth 1 -type f \( -name '*lock*' -o -name '*Lock*' \) 2>/dev/null | grep -q .; then
  echo "警告：Library 下仍存在 lock 文件，请手工检查。" | tee -a "$RECOVERY_DIR/summary.txt"
else
  echo "Library 下未发现残留 lock 文件。" | tee -a "$RECOVERY_DIR/summary.txt"
fi

echo "恢复日志：$RECOVERY_DIR"
