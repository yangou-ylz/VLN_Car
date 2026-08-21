#!/usr/bin/env bash

# 兼容旧入口：打开 Mesa+Oasis 融合场景。
# 新统一入口为：scripts/open_high_precision_world_model.sh stitched

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
exec "$VLN_ROOT/scripts/open_high_precision_world_model.sh" stitched "$@"
