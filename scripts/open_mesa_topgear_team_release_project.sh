#!/usr/bin/env bash

# Open the clean Mesa Topgear team release project and target scene.

set -eo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
UNITY_EDITOR="${UNITY_EDITOR:-$VLN_ROOT/UnityEditors/2022.3.62f1/Editor/Unity}"
PROJECT_DIR="${VLN_MESA_TOPGEAR_RELEASE_PROJECT:-$VLN_ROOT/UnityProjects/VLN_MesaTopgear_TeamRelease}"
TARGET_SCENE="Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity"

if [[ ! -x "$UNITY_EDITOR" ]]; then
  echo "未找到 Unity Editor：$UNITY_EDITOR"
  echo "如果 Unity 安装在其它位置，请先设置 UNITY_EDITOR=/path/to/Unity"
  exit 1
fi

if [[ ! -d "$PROJECT_DIR" ]]; then
  echo "未找到 Mesa Topgear 团队发布工程：$PROJECT_DIR"
  echo "请先放入团队发布资产包，或运行：$VLN_ROOT/scripts/prepare_mesa_topgear_team_release_project.sh"
  exit 1
fi

if [[ ! -f "$PROJECT_DIR/$TARGET_SCENE" ]]; then
  echo "发布工程缺少目标场景：$PROJECT_DIR/$TARGET_SCENE"
  exit 1
fi

mkdir -p \
  "$VLN_ROOT/.unity_user/config" \
  "$VLN_ROOT/.unity_user/cache" \
  "$VLN_ROOT/.unity_user/cache/upm" \
  "$VLN_ROOT/.unity_user/cache/upm/db" \
  "$VLN_ROOT/.unity_user/cache/upm/git-lfs" \
  "$VLN_ROOT/.unity_user/cache/upm/npm" \
  "$VLN_ROOT/.unity_user/data" \
  "$VLN_ROOT/.unity_user/logs"

export XDG_CONFIG_HOME="$VLN_ROOT/.unity_user/config"
export XDG_CACHE_HOME="$VLN_ROOT/.unity_user/cache"
export XDG_DATA_HOME="$VLN_ROOT/.unity_user/data"
export UPM_CACHE_PATH="$VLN_ROOT/.unity_user/cache/upm/db"
export UPM_GIT_LFS_CACHE_PATH="$VLN_ROOT/.unity_user/cache/upm/git-lfs"
export UPM_NPM_CACHE_PATH="$VLN_ROOT/.unity_user/cache/upm/npm"

UNITY_PROXY="${UNITY_PROXY:-http://127.0.0.1:7897/}"
UNITY_NO_PROXY="${UNITY_NO_PROXY:-localhost,127.0.0.1,::1}"

export HTTP_PROXY="$UNITY_PROXY"
export HTTPS_PROXY="$UNITY_PROXY"
export ALL_PROXY="$UNITY_PROXY"
export http_proxy="$UNITY_PROXY"
export https_proxy="$UNITY_PROXY"
export all_proxy="$UNITY_PROXY"
export NO_PROXY="$UNITY_NO_PROXY"
export no_proxy="$UNITY_NO_PROXY"

echo "打开 Mesa Topgear 团队发布工程：$PROJECT_DIR"
echo "目标场景：$TARGET_SCENE"

exec "$UNITY_EDITOR" \
  -projectPath "$PROJECT_DIR" \
  -executeMethod VLN.Editor.VlnWorldModelManualSaveWindow.OpenRegisteredSceneFromCommandLine \
  --vln-open-scene "$TARGET_SCENE" \
  "$@"
