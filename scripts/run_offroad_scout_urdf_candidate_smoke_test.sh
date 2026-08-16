#!/usr/bin/env bash

# 兼容入口：阶段 13 Scout V2 URDF 静态候选验收。
# 新的主脚本名为 run_scout_urdf_candidate_smoke_test.sh；保留本文件避免旧文档/终端命令失效。

set -eo pipefail

exec /home/ubuntu22/VLN/scripts/run_scout_urdf_candidate_smoke_test.sh "$@"
