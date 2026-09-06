#!/usr/bin/env bash

# Team entrypoint for the accepted Mesa Topgear Mix Unity scene.

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
UNITY_EDITOR="${UNITY_EDITOR:-$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity}"
RELEASE_PROJECT_DIR="${VLN_MESA_TOPGEAR_RELEASE_PROJECT:-$VLN_ROOT/UnityProjects/VLN_MesaTopgear_TeamRelease}"
DEV_PROJECT_DIR="$VLN_ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"
TARGET_SCENE_ASSET="Assets/VLN/Scenes/VLNMesaTopgearMixVehicleScale4p0WorldCandidate.unity"

usage() {
  cat <<'EOF'
用法：
  ./scripts/open_high_precision_world_model.sh --scene mesa_topgear_mix
  ./scripts/open_high_precision_world_model.sh mesa_topgear_mix
  ./scripts/open_high_precision_world_model.sh --scene mesa_topgear_mix -logFile /tmp/vln_unity.log

说明：
  mesa_topgear_mix 是当前团队主线场景。
  默认优先打开 UnityProjects/VLN_MesaTopgear_TeamRelease。
  如需指定其他 Unity 工程，可设置 VLN_LARGE_ASSET_PROJECT。
EOF
}

if [[ $# -lt 1 || "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit $([[ $# -lt 1 ]] && echo 2 || echo 0)
fi

WORLD_ARG=""
case "$1" in
  --scene|-scene|-s|--sence|-sence)
    if [[ $# -lt 2 ]]; then
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

PROJECT_DIR="${VLN_LARGE_ASSET_PROJECT:-}"
if [[ -z "$PROJECT_DIR" ]]; then
  if [[ -d "$RELEASE_PROJECT_DIR" ]]; then
    PROJECT_DIR="$RELEASE_PROJECT_DIR"
  elif [[ -d "$DEV_PROJECT_DIR" ]]; then
    PROJECT_DIR="$DEV_PROJECT_DIR"
  else
    PROJECT_DIR="$RELEASE_PROJECT_DIR"
  fi
fi

if [[ ! -d "$PROJECT_DIR" ]]; then
  echo "未找到 Unity 工程：$PROJECT_DIR"
  echo "请先按 docs/team_environment_setup.md 解压 Mesa Topgear Mix 发布资产包。"
  exit 1
fi

WORLD_KEY="$(printf '%s' "$WORLD_ARG" | tr '[:upper:]' '[:lower:]')"
case "$WORLD_KEY" in
  mesa_topgear_mix|mesa-topgear-mix|mesa_mix|mesa-mix|topgear_mix|topgear-mix|mix|团队混合版|混合荒漠)
    DIRECT_SCENE_ASSET="$TARGET_SCENE_ASSET"
    ;;
  Assets/VLN/Scenes/VLN*.unity|assets/vln/scenes/vln*.unity)
    DIRECT_SCENE_ASSET="$WORLD_ARG"
    ;;
  *)
    echo "未知世界模型参数：$WORLD_ARG"
    echo "当前团队交付入口为：--scene mesa_topgear_mix"
    exit 2
    ;;
esac

TARGET_SCENE="$PROJECT_DIR/$DIRECT_SCENE_ASSET"
if [[ ! -f "$TARGET_SCENE" ]]; then
  echo "缺少目标场景：$TARGET_SCENE"
  echo "请确认发布资产包已经完整解压。"
  exit 1
fi

same_project_pids="$(ps -eo pid=,args= | awk -v unity="$UNITY_EDITOR" -v project="-projectPath $PROJECT_DIR" -v self="$$" '
  index($0, unity) && index($0, project) && $1 != self && $0 !~ /awk -v unity=/ { print $1 }
' | tr '\n' ' ')"
if [[ -n "$same_project_pids" ]]; then
  echo "检测到同一 Unity 工程已有实例正在运行，已停止继续启动。"
  echo "工程：$PROJECT_DIR"
  echo "进程：$same_project_pids"
  ps -fp $same_project_pids 2>/dev/null || true
  exit 3
fi

echo "准备打开：Mesa Topgear Mix 主线场景"
echo "目标场景：$TARGET_SCENE"

VLN_LARGE_ASSET_PROJECT="$PROJECT_DIR" exec "$VLN_ROOT/scripts/open_unity_large_asset_sandbox_project.sh" \
  -executeMethod VLN.Editor.VlnWorldModelManualSaveWindow.OpenRegisteredSceneFromCommandLine \
  --vln-open-scene "$DIRECT_SCENE_ASSET" \
  "$@"
