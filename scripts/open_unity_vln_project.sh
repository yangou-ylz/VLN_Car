#!/usr/bin/env bash

# 用已验证的 Unity 2022.3.62f1 打开 VLN_Offroad 工程。
# 关键点：复用项目内 Unity Hub/许可证配置，避免从其他入口启动导致重复登录。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_EDITOR="$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity"
PROJECT_DIR="$VLN_ROOT/UnityProjects/VLN_Offroad"

if [ ! -x "$UNITY_EDITOR" ]; then
  echo "未找到 Unity Editor：$UNITY_EDITOR"
  exit 1
fi

if [ ! -d "$PROJECT_DIR" ]; then
  echo "未找到 Unity 工程：$PROJECT_DIR"
  exit 1
fi

mkdir -p \
  "$VLN_ROOT/.unity_user/config" \
  "$VLN_ROOT/.unity_user/cache" \
  "$VLN_ROOT/.unity_user/data" \
  "$VLN_ROOT/.unity_user/logs"

export XDG_CONFIG_HOME="$VLN_ROOT/.unity_user/config"
export XDG_CACHE_HOME="$VLN_ROOT/.unity_user/cache"
export XDG_DATA_HOME="$VLN_ROOT/.unity_user/data"

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
