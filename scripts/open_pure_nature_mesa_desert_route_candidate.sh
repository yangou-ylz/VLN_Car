#!/usr/bin/env bash

# 打开阶段 21 Pure Nature 2 Mesa Desert 的 VLN 路线候选场景。
# 若候选场景尚不存在，会先从第三方 Mesa_Demo 派生一份，再打开给用户肉眼验收。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
PROJECT_DIR="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
SOURCE_SCENE="$PROJECT_DIR/Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity"

if [ ! -d "$PROJECT_DIR" ]; then
  echo "未找到大资产副本工程：$PROJECT_DIR"
  echo "请先运行：$VLN_ROOT/scripts/prepare_high_precision_large_asset_sandbox_project.sh"
  exit 1
fi

if [ ! -f "$SOURCE_SCENE" ]; then
  echo "未找到 Mesa demo scene：$SOURCE_SCENE"
  echo "请先导入：$VLN_ROOT/VLN_ASSETS_CACHE/Pure Nature 2 Mesa Desert 1.0.unitypackage"
  exit 1
fi

exec "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -executeMethod VLN.Editor.VlnPureNatureMesaDesertRouteCandidateBuilder.OpenCandidateForManualReview
