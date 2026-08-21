#!/usr/bin/env bash

# 阶段 21 高精荒漠世界模型统一打开入口。
# 用法：./scripts/open_high_precision_world_model.sh <first|mesa|second|oasis|stitched> [Unity 额外参数]

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
PROJECT_DIR="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"

usage() {
  cat <<'EOF'
用法：
  ./scripts/open_high_precision_world_model.sh first
  ./scripts/open_high_precision_world_model.sh second
  ./scripts/open_high_precision_world_model.sh stitched
  ./scripts/open_high_precision_world_model.sh first-topgear

参数别名：
  first / 1 / mesa / mesa-desert / 第一套      打开第一套 Mesa Desert 独立场景
  second / 2 / oasis / oasis-desert / 第二套   打开第二套 Oasis Desert 独立场景
  stitched / 3 / fusion / mesa-oasis / 融合版  打开 Mesa+Oasis 融合场景
  first-topgear / mesa-topgear / 车载Mesa     打开第一套 Mesa + Topgear 真实物理车候选场景

后面的参数会原样传给 Unity，例如：
  ./scripts/open_high_precision_world_model.sh second -batchmode -nographics -quit -logFile /tmp/oasis.log
EOF
}

if [ $# -lt 1 ]; then
  usage
  exit 2
fi

WORLD_ARG="$1"
shift

if [ "$WORLD_ARG" = "--help" ] || [ "$WORLD_ARG" = "-h" ]; then
  usage
  exit 0
fi

if [ ! -d "$PROJECT_DIR" ]; then
  echo "未找到大资产副本工程：$PROJECT_DIR"
  echo "请先运行：$VLN_ROOT/scripts/prepare_high_precision_large_asset_sandbox_project.sh"
  exit 1
fi

WORLD_KEY="$(printf '%s' "$WORLD_ARG" | tr '[:upper:]' '[:lower:]')"
case "$WORLD_KEY" in
  first|1|mesa|mesa-desert|mesa_desert|pure-nature-mesa|pure_nature_mesa|第一套)
    MODEL_ID="mesa"
    LABEL="第一套 Mesa Desert 独立场景"
    REQUIRED_SCENE="$PROJECT_DIR/Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity"
    TARGET_SCENE="$PROJECT_DIR/Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity"
    METHOD="VLN.Editor.VlnPureNatureMesaDesertRouteCandidateBuilder.OpenCandidateForManualReview"
    ;;
  second|2|oasis|oasis-desert|oasis_desert|pure-nature-oasis|pure_nature_oasis|第二套)
    MODEL_ID="oasis"
    LABEL="第二套 Oasis Desert 独立场景"
    REQUIRED_SCENE="$PROJECT_DIR/Assets/BK/PureNature_Oasis/Scenes/Scene_Oasis_Day.unity"
    TARGET_SCENE="$PROJECT_DIR/Assets/VLN/Scenes/VLNOasisDesertRouteCandidate.unity"
    METHOD="VLN.Editor.VlnPureNatureOasisDesertRouteCandidateBuilder.OpenCandidateForManualReview"
    ;;
  stitched|3|fusion|merged|mesa-oasis|mesa_oasis|stitched-scene|stitched_scene|融合版)
    MODEL_ID="stitched"
    LABEL="Mesa+Oasis 融合场景"
    REQUIRED_SCENE="$PROJECT_DIR/Assets/BK/PureNature_Oasis/Scenes/Scene_Oasis_Day.unity"
    TARGET_SCENE="$PROJECT_DIR/Assets/VLN/Scenes/VLNMesaOasisStitchedRouteCandidate.unity"
    METHOD="VLN.Editor.VlnPureNatureMesaOasisStitchBuilder.OpenStitchedForManualReview"
    ;;
  first-topgear|first_topgear|mesa-topgear|mesa_topgear|mesa-vehicle|mesa_vehicle|topgear-mesa|topgear_mesa|车载mesa|第一套小车)
    MODEL_ID="mesa_topgear"
    LABEL="第一套 Mesa Desert + Topgear 真实物理车候选场景"
    REQUIRED_SCENE="$PROJECT_DIR/Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity"
    TARGET_SCENE="$PROJECT_DIR/Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity"
    METHOD="VLN.Editor.VlnMesaTopgearVehicleCandidateBuilder.OpenCandidateForManualReview"
    ;;
  *)
    echo "未知世界模型参数：$WORLD_ARG"
    usage
    exit 2
    ;;
esac

if [ ! -f "$REQUIRED_SCENE" ]; then
  echo "缺少源场景：$REQUIRED_SCENE"
  echo "请确认对应 Pure Nature 2 资产包已经导入大资产副本工程。"
  exit 1
fi

echo "准备打开：$LABEL"
echo "目标场景：$TARGET_SCENE"

if [ "$MODEL_ID" = "stitched" ]; then
  if [ -f "$VLN_ROOT/config/world_model_current_save.json" ]; then
    echo "检测到手工保存世界记录：$VLN_ROOT/config/world_model_current_save.json"
    echo "融合版会直接加载已保存场景，不自动重建覆盖。"
  fi
fi

exec "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -executeMethod "$METHOD" \
  "$@"
