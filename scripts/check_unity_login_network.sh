#!/usr/bin/env bash

# Unity Hub 登录前网络验收脚本。
# 目标：确认 Clash/Mihomo 运行态、显式代理出口、直连状态，避免 Unity OAuth
# conversation 在一个 IP 创建、又从另一个 IP authorize/token。

set -eo pipefail

PROXY_URL="http://127.0.0.1:7897"
MIHOMO_SOCKET="/tmp/verge/verge-mihomo.sock"

echo "[1/4] Mihomo 运行态"
if [ -S "$MIHOMO_SOCKET" ]; then
  curl -fsSL --max-time 5 --unix-socket "$MIHOMO_SOCKET" http://127.0.0.1/configs \
    | tr ',' '\n' \
    | grep -E '"mode"|"mixed-port"|"enable"' \
    | head -n 8 || true
else
  echo "未找到 Mihomo Unix socket: $MIHOMO_SOCKET"
fi

echo
echo "[2/4] 显式代理出口 IP"
curl -fsSL --max-time 12 --proxy "$PROXY_URL" https://api.ipify.org || true
echo

echo
echo "[3/4] 无代理直连状态"
curl -fsSL --max-time 8 --noproxy '*' https://api.ipify.org 2>/dev/null || echo "直连不可用或被拒绝"
echo

echo
echo "[4/4] Unity API 经代理连通性"
curl -fsSIL --max-time 12 --proxy "$PROXY_URL" https://api.unity.com/v1/oauth2/authorize \
  2>/dev/null | sed -n '1,8p' || true
