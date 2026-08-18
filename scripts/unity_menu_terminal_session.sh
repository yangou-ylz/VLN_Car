#!/usr/bin/env bash

# Unity 菜单打开的新终端实际执行入口。
# 作用：登记当前终端进程组，运行目标脚本，并在脚本退出后保留窗口方便排错。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
MODE="${1:-unknown}"
TARGET_SCRIPT="${2:-}"
SUMMARY="${3:-}"
UNITY_PID="${4:-}"
RUNTIME_DIR="$VLN_ROOT/.runtime/unity_menu"
PID_FILE="$RUNTIME_DIR/processes.tsv"
LOG_DIR="$RUNTIME_DIR/logs"

mkdir -p "$RUNTIME_DIR" "$LOG_DIR"

SESSION_ID="${MODE}_$(date +%Y%m%d_%H%M%S)_$$"
LOG_FILE="$LOG_DIR/${SESSION_ID}.log"
exec > >(tee -a "$LOG_FILE") 2>&1

echo "== VLN Unity 菜单终端会话 =="
echo "session_id=$SESSION_ID"
echo "mode=$MODE"
echo "pid=$$"
echo "ppid=$PPID"
echo "started_at=$(date -Is)"
echo "log_file=$LOG_FILE"
echo

if [ -z "$TARGET_SCRIPT" ] || [ ! -x "$TARGET_SCRIPT" ]; then
  echo "目标脚本不存在或不可执行：$TARGET_SCRIPT" >&2
  echo "按回车关闭窗口..."
  read -r _ || true
  exit 1
fi

PGID="$(ps -o pgid= -p $$ | tr -d ' ')"
printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
  "$SESSION_ID" "$$" "$PGID" "$MODE" "$TARGET_SCRIPT" "$(date -Is)" "${UNITY_PID:-none}" >> "$PID_FILE"

echo "pgid=$PGID"
echo "unity_pid=${UNITY_PID:-none}"
echo

cd "$VLN_ROOT"
echo "$SUMMARY"
echo "执行脚本：$TARGET_SCRIPT"
echo

set +e
"$TARGET_SCRIPT"
status=$?
set -e

echo
echo "进程退出码：$status"
echo "日志文件：$LOG_FILE"
echo "窗口已保留。需要关闭时直接关窗口，或输入 exit 后回车。"

if [ -t 0 ]; then
  exec bash --noprofile --norc
fi

sleep 3600
exit "$status"
