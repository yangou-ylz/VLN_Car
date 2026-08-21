#!/usr/bin/env bash

# 用已验证的 Unity 2022.3.62f1 打开阶段 21 大资产副本工程。
# 这个入口只用于 Asset Store/Fab 大包下载、导入和截图验证，避免污染主工程。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_EDITOR="$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity"
PROJECT_DIR="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"

if [ ! -x "$UNITY_EDITOR" ]; then
  echo "未找到 Unity Editor：$UNITY_EDITOR"
  exit 1
fi

if [ ! -d "$PROJECT_DIR" ]; then
  echo "未找到大资产副本工程：$PROJECT_DIR"
  echo "请先运行：$VLN_ROOT/scripts/prepare_high_precision_large_asset_sandbox_project.sh"
  exit 1
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

UNITY_PROXY="http://127.0.0.1:7897/"
UNITY_NO_PROXY="localhost,127.0.0.1,::1"

export HTTP_PROXY="$UNITY_PROXY"
export HTTPS_PROXY="$UNITY_PROXY"
export ALL_PROXY="$UNITY_PROXY"
export http_proxy="$UNITY_PROXY"
export https_proxy="$UNITY_PROXY"
export all_proxy="$UNITY_PROXY"
export NO_PROXY="$UNITY_NO_PROXY"
export no_proxy="$UNITY_NO_PROXY"

exec "$UNITY_EDITOR" -projectPath "$PROJECT_DIR" "$@"
