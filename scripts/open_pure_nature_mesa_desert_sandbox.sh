#!/usr/bin/env bash

# 打开阶段 21 大资产副本工程，并自动加载 Pure Nature 2 Mesa Desert demo scene。
# 只用于用户肉眼验收 Mesa 场景；不打开主工程、不接 ROS2、不改 Topgear 锁定场景。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
PROJECT_DIR="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
SCENE_PATH="$PROJECT_DIR/Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity"

if [ ! -d "$PROJECT_DIR" ]; then
  echo "未找到大资产副本工程：$PROJECT_DIR"
  echo "请先运行：$VLN_ROOT/scripts/prepare_high_precision_large_asset_sandbox_project.sh"
  exit 1
fi

if [ ! -f "$SCENE_PATH" ]; then
  echo "未找到 Mesa demo scene：$SCENE_PATH"
  echo "请先导入：$VLN_ROOT/VLN_ASSETS_CACHE/Pure Nature 2 Mesa Desert 1.0.unitypackage"
  exit 1
fi

exec "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -executeMethod VLN.Editor.VlnPureNatureMesaDesertSmokeTestRunner.OpenForManualReview
