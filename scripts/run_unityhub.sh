#!/usr/bin/env bash

# 项目内 Unity Hub 启动入口。
# 目的：把 Unity Hub 的配置、缓存和下载状态固定在 /home/ubuntu22/VLN 内部，
# 避免把本项目状态散落到用户 home 目录。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITYHUB_BIN="$VLN_ROOT/tools/unityhub_extracted_3.20.1/usr/bin/unityhub"
UNITYHUB_BIN_DIR="$VLN_ROOT/tools/unityhub_extracted_3.20.1/usr/bin"

if [ ! -x "$UNITYHUB_BIN" ]; then
  echo "未找到 Unity Hub：$UNITYHUB_BIN"
  exit 1
fi

mkdir -p \
  "$VLN_ROOT/.unity_user/config" \
  "$VLN_ROOT/.unity_user/cache" \
  "$VLN_ROOT/.unity_user/data" \
  "$VLN_ROOT/.unity_user/logs" \
  "$VLN_ROOT/UnityEditors" \
  "$VLN_ROOT/UnityProjects" \
  "$VLN_ROOT/tools/unityhub_download"

export XDG_CONFIG_HOME="$VLN_ROOT/.unity_user/config"
export XDG_CACHE_HOME="$VLN_ROOT/.unity_user/cache"
export XDG_DATA_HOME="$VLN_ROOT/.unity_user/data"
export PATH="$UNITYHUB_BIN_DIR:$PATH"
UNITY_PROXY="http://127.0.0.1:7897/"
UNITY_NO_PROXY="localhost,127.0.0.1,::1"

# Unity 登录链路对公网 IP 一致性很敏感。这里显式固定 Hub 主进程、
# Electron/Chromium 网络栈和子进程都走同一个本机 HTTP mixed proxy。
export HTTP_PROXY="$UNITY_PROXY"
export HTTPS_PROXY="$UNITY_PROXY"
export ALL_PROXY="$UNITY_PROXY"
export http_proxy="$UNITY_PROXY"
export https_proxy="$UNITY_PROXY"
export all_proxy="$UNITY_PROXY"
export NO_PROXY="$UNITY_NO_PROXY"
export no_proxy="$UNITY_NO_PROXY"

exec "$UNITYHUB_BIN" \
  --proxy-server="http://127.0.0.1:7897" \
  --proxy-bypass-list="<-loopback>;localhost;127.0.0.1;::1" \
  "$@"
