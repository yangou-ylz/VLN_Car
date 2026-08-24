#!/usr/bin/env bash

# 阶段 21 高精世界模型统一打开入口。
# 推荐用法：./scripts/open_high_precision_world_model.sh --scene mesa_desert [Unity 额外参数]

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
PROJECT_DIR="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"

is_auto_registered_scene_asset() {
  local asset_path="$1"
  local base_name
  base_name="$(basename "$asset_path")"

  case "$asset_path" in
    Assets/VLN/Scenes/VLN*WorldCandidate.unity|\
    Assets/VLN/Scenes/VLN*RouteCandidate.unity|\
    Assets/VLN/Scenes/VLN*TopgearVehicleCandidate.unity|\
    Assets/VLN/Scenes/VLNHighPrecisionDesertSandbox.unity)
      case "$base_name" in
        VLN*.unity) return 0 ;;
      esac
      ;;
  esac
  return 1
}

resolve_direct_scene_asset() {
  local raw="$1"
  local asset_path=""

  case "$raw" in
    "$PROJECT_DIR"/Assets/*.unity)
      asset_path="Assets/${raw#"$PROJECT_DIR"/Assets/}"
      ;;
    Assets/*.unity)
      asset_path="$raw"
      ;;
    *.unity)
      asset_path="Assets/VLN/Scenes/$(basename "$raw")"
      ;;
    VLN*)
      asset_path="Assets/VLN/Scenes/${raw%.unity}.unity"
      ;;
  esac

  if [ -n "$asset_path" ] && is_auto_registered_scene_asset "$asset_path" && [ -f "$PROJECT_DIR/$asset_path" ]; then
    printf '%s\n' "$asset_path"
  fi
}

usage() {
  cat <<'EOF'
用法：
  ./scripts/open_high_precision_world_model.sh --scene mesa_desert
  ./scripts/open_high_precision_world_model.sh --scene oasis_desert
  ./scripts/open_high_precision_world_model.sh --scene mesa_oasis
  ./scripts/open_high_precision_world_model.sh --scene mesa_topgear
  ./scripts/open_high_precision_world_model.sh --scene meadow_forest
  ./scripts/open_high_precision_world_model.sh --scene forest_lake
  ./scripts/open_high_precision_world_model.sh --scene VLNNewWorldCandidate
  ./scripts/open_high_precision_world_model.sh --scene Assets/VLN/Scenes/VLNNewWorldCandidate.unity

参数说明：
  --scene mesa_desert      打开 Mesa Desert 独立场景
  --scene oasis_desert     打开 Oasis Desert 独立场景
  --scene mesa_oasis       打开 Mesa+Oasis 融合场景
  --scene mesa_topgear     打开 Mesa + Topgear 真实物理车候选场景
  --scene meadow_forest    打开 Meadow Dynamic Nature 湖泊树林/草甸场景
  --scene forest_lake      打开 ForestLake 湖边村庄/森林湖泊场景
  --scene VLN*.unity       直接打开 Assets/VLN/Scenes 下自动注册的 VLN 世界场景

自动注册命名规则：
  Assets/VLN/Scenes/VLN*WorldCandidate.unity
  Assets/VLN/Scenes/VLN*RouteCandidate.unity
  Assets/VLN/Scenes/VLN*TopgearVehicleCandidate.unity
  Assets/VLN/Scenes/VLNHighPrecisionDesertSandbox.unity

兼容旧写法：first、second、stitched、first-topgear 仍可用。
兼容拼写：--sence / -sence 也会按 --scene 处理。

后面的参数会原样传给 Unity，例如：
  ./scripts/open_high_precision_world_model.sh --scene forest_lake -batchmode -quit -logFile /tmp/forest_lake.log
EOF
}

if [ $# -lt 1 ]; then
  usage
  exit 2
fi

if [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
  usage
  exit 0
fi

WORLD_ARG=""
case "$1" in
  --scene|-scene|-s|--sence|-sence)
    if [ $# -lt 2 ]; then
      echo "缺少场景名：$1 <scene_name>"
      usage
      exit 2
    fi
    WORLD_ARG="$2"
    shift 2
    ;;
  --scene=*|-scene=*|--sence=*|-sence=*)
    WORLD_ARG="${1#*=}"
    shift
    ;;
  *)
    WORLD_ARG="$1"
    shift
    ;;
esac

if [ ! -d "$PROJECT_DIR" ]; then
  echo "未找到大资产副本工程：$PROJECT_DIR"
  echo "请先运行：$VLN_ROOT/scripts/prepare_high_precision_large_asset_sandbox_project.sh"
  exit 1
fi

WORLD_KEY="$(printf '%s' "$WORLD_ARG" | tr '[:upper:]' '[:lower:]')"
DIRECT_SCENE_ASSET=""
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
  meadow|meadow-forest|meadow_forest|meadow-dynamic-nature|meadow_dynamic_nature|dynamic-nature|dynamic_nature|lake-forest|lake_forest)
    MODEL_ID="meadow_forest"
    LABEL="Meadow Dynamic Nature 湖泊树林/草甸场景"
    REQUIRED_SCENE="$PROJECT_DIR/Assets/NatureManufacture Assets/Meadow Environment Dynamic Nature/Demo Scenes/Unity Standard Demo Scene.unity"
    TARGET_SCENE="$PROJECT_DIR/Assets/VLN/Scenes/VLNMeadowDynamicNatureWorldCandidate.unity"
    METHOD="VLN.Editor.VlnImportedWorldSceneRegistry.OpenMeadowForManualReview"
    ;;
  forestlake|forest-lake|forest_lake|lake-village|lake_village|village-lake|village_lake)
    MODEL_ID="forest_lake"
    LABEL="ForestLake 湖边村庄/森林湖泊场景"
    REQUIRED_SCENE="$PROJECT_DIR/Assets/ForestLake/Maps/Demo_01.unity"
    TARGET_SCENE="$PROJECT_DIR/Assets/VLN/Scenes/VLNForestLakeWorldCandidate.unity"
    METHOD="VLN.Editor.VlnImportedWorldSceneRegistry.OpenForestLakeForManualReview"
    ;;
  *)
    DIRECT_SCENE_ASSET="$(resolve_direct_scene_asset "$WORLD_ARG")"
    if [ -z "$DIRECT_SCENE_ASSET" ]; then
      echo "未知世界模型参数：$WORLD_ARG"
      echo "如果这是新导入世界，请先把派生场景保存为 Assets/VLN/Scenes/VLN*WorldCandidate.unity / VLN*RouteCandidate.unity / VLN*TopgearVehicleCandidate.unity。"
      usage
      exit 2
    fi
    MODEL_ID="direct_scene"
    LABEL="自动注册 VLN 世界场景：$DIRECT_SCENE_ASSET"
    REQUIRED_SCENE="$PROJECT_DIR/$DIRECT_SCENE_ASSET"
    TARGET_SCENE="$PROJECT_DIR/$DIRECT_SCENE_ASSET"
    METHOD="VLN.Editor.VlnWorldModelManualSaveWindow.OpenRegisteredSceneFromCommandLine"
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

UNITY_ARGS=(-executeMethod "$METHOD")
if [ -n "$DIRECT_SCENE_ASSET" ]; then
  UNITY_ARGS+=(--vln-open-scene "$DIRECT_SCENE_ASSET")
fi

exec "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  "${UNITY_ARGS[@]}" \
  "$@"
