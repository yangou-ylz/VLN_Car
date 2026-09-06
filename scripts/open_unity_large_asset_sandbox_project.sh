#!/usr/bin/env bash

# 用已验证的 Unity 2022.3.62f1 打开 VLN Unity 工程。
# 默认用于大资产副本工程；团队发布入口会通过 VLN_LARGE_ASSET_PROJECT 指向发布工程。

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
UNITY_EDITOR="${UNITY_EDITOR:-$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity}"
PROJECT_DIR="${VLN_LARGE_ASSET_PROJECT:-$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox}"

if [ ! -x "$UNITY_EDITOR" ]; then
  echo "未找到 Unity Editor：$UNITY_EDITOR"
  exit 1
fi

if [ ! -d "$PROJECT_DIR" ]; then
  echo "未找到 Unity 工程：$PROJECT_DIR"
  echo "团队部署请先解压 Mesa Topgear Mix 发布资产包；开发机请确认 VLN_LARGE_ASSET_PROJECT 或大资产副本工程路径。"
  exit 1
fi

collect_same_project_unity_pids() {
  ps -eo pid=,args= | awk -v unity="$UNITY_EDITOR" -v project="-projectPath $PROJECT_DIR" '
    {
      pid = $1;
      $1 = "";
      sub(/^ +/, "", $0);
      split($0, parts, " ");
      if (parts[1] == unity && index($0, project)) print pid;
    }
  '
}

same_project_pids="$(collect_same_project_unity_pids | tr '\n' ' ')"
if [ -n "$same_project_pids" ]; then
  echo "检测到同一 Unity 工程已有实例正在运行，已停止继续启动，避免 Unity 弹出多实例错误窗口。"
  echo "工程：$PROJECT_DIR"
  echo "进程：$same_project_pids"
  ps -fp $same_project_pids 2>/dev/null || true
  echo "请先关闭这个 Unity 窗口；如果确认窗口已关但进程残留，再运行：./scripts/stop_unity_large_asset_sandbox_project.sh"
  exit 3
fi

stale_lock_files=()
for lock_file in \
  "$PROJECT_DIR/Temp/UnityLockfile" \
  "$PROJECT_DIR/Library/ArtifactDB-lock" \
  "$PROJECT_DIR/Library/SourceAssetDB-lock"; do
  if [ -f "$lock_file" ]; then
    stale_lock_files+=("$lock_file")
  fi
done

if [ "${#stale_lock_files[@]}" -gt 0 ]; then
  RECOVERY_DIR="$VLN_ROOT/UnityProjects/_ManualRecoveryLogs/stale_large_asset_unity_lock_$(date +%Y%m%d_%H%M%S)"
  mkdir -p "$RECOVERY_DIR"
  for lock_file in "${stale_lock_files[@]}"; do
    mv "$lock_file" "$RECOVERY_DIR/"
    echo "检测到无活动 Unity 进程但存在 stale lock，已移动：$lock_file -> $RECOVERY_DIR/"
  done
fi

mkdir -p \
  "$VLN_ROOT/.unity_user/config" \
  "$VLN_ROOT/.unity_user/cache" \
  "$VLN_ROOT/.unity_user/cache/upm" \
  "$VLN_ROOT/.unity_user/cache/upm/db" \
  "$VLN_ROOT/.unity_user/cache/upm/git-lfs" \
  "$VLN_ROOT/.unity_user/cache/upm/npm" \
  "$VLN_ROOT/.unity_user/data" \
  "$VLN_ROOT/.unity_user/logs"

export XDG_CONFIG_HOME="$VLN_ROOT/.unity_user/config"
export XDG_CACHE_HOME="$VLN_ROOT/.unity_user/cache"
export XDG_DATA_HOME="$VLN_ROOT/.unity_user/data"

# Unity Package Manager has its own cache knobs. Keep downloads in the VLN tree.
export UPM_CACHE_PATH="$VLN_ROOT/.unity_user/cache/upm/db"
export UPM_GIT_LFS_CACHE_PATH="$VLN_ROOT/.unity_user/cache/upm/git-lfs"
export UPM_NPM_CACHE_PATH="$VLN_ROOT/.unity_user/cache/upm/npm"

UNITY_PROXY="${UNITY_PROXY:-http://127.0.0.1:7897/}"
UNITY_NO_PROXY="${UNITY_NO_PROXY:-localhost,127.0.0.1,::1}"

export HTTP_PROXY="$UNITY_PROXY"
export HTTPS_PROXY="$UNITY_PROXY"
export ALL_PROXY="$UNITY_PROXY"
export http_proxy="$UNITY_PROXY"
export https_proxy="$UNITY_PROXY"
export all_proxy="$UNITY_PROXY"
export NO_PROXY="$UNITY_NO_PROXY"
export no_proxy="$UNITY_NO_PROXY"

exec "$UNITY_EDITOR" -projectPath "$PROJECT_DIR" "$@"
