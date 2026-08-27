#!/usr/bin/env bash

# 打开第一套 Pure Nature 2 Mesa Desert + Topgear 真实物理车候选场景。
# 这是手工看效果入口：先打开 Unity，再启动 endpoint，再点 Play。

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
exec "$VLN_ROOT/scripts/open_high_precision_world_model.sh" first-topgear "$@"
