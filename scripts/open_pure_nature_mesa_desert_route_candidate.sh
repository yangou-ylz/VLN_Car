#!/usr/bin/env bash

# 兼容旧入口：打开第一套 Pure Nature 2 Mesa Desert 独立 VLN 场景。
# 新统一入口为：scripts/open_high_precision_world_model.sh first

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
exec "$VLN_ROOT/scripts/open_high_precision_world_model.sh" first "$@"
