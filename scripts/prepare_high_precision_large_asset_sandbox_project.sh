#!/usr/bin/env bash
set -euo pipefail

ROOT="/home/ubuntu22/VLN"
SOURCE_PROJECT="$ROOT/UnityProjects/VLN_Offroad"
SANDBOX_PROJECT="$ROOT/UnityProjects/VLN_Offroad_LargeAssetSandbox"

if ! command -v rsync >/dev/null 2>&1; then
  echo "缺少 rsync，不能安全创建 Unity 副本工程。"
  exit 1
fi

if [[ ! -d "$SOURCE_PROJECT/Assets" || ! -d "$SOURCE_PROJECT/Packages" || ! -d "$SOURCE_PROJECT/ProjectSettings" ]]; then
  echo "源 Unity 工程结构不完整：$SOURCE_PROJECT"
  exit 1
fi

if [[ -e "$SANDBOX_PROJECT" && "${1:-}" != "--refresh" ]]; then
  echo "VLN_HIGH_PRECISION_LARGE_ASSET_SANDBOX_EXISTS"
  echo "副本工程已存在：$SANDBOX_PROJECT"
  echo "如需增量刷新主工程脚本/配置，运行：$0 --refresh"
  exit 0
fi

mkdir -p "$SANDBOX_PROJECT"

rsync -a \
  --exclude='Library/' \
  --exclude='Temp/' \
  --exclude='Obj/' \
  --exclude='Logs/' \
  --exclude='Build/' \
  --exclude='Builds/' \
  --exclude='UserSettings/' \
  --exclude='_ManualRecoveryLogs/' \
  "$SOURCE_PROJECT/Assets" \
  "$SOURCE_PROJECT/Packages" \
  "$SOURCE_PROJECT/ProjectSettings" \
  "$SANDBOX_PROJECT/"

cat > "$SANDBOX_PROJECT/VLN_LARGE_ASSET_SANDBOX_README.txt" <<'EOF'
VLN 高精荒漠大资产副本工程

用途：只用于导入/验证大型 Unity/Fab/Asset Store 场景包，避免污染主工程。

规则：
1. 禁止在这里修改或覆盖主场景 Topgear 传感器锁定文件。
2. 可以在这里测试 URP/HDRP、Package Settings、Quality/Graphics/Lighting。
3. 大包先放到 /home/ubuntu22/VLN/VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/。
4. 导入前先运行 /home/ubuntu22/VLN/scripts/scan_high_precision_large_scene_packages.sh。
5. 验证目标是打开第三方 demo scene 截图、记录 FPS/材质缺失/LOD/Collider/ProjectSettings 影响。
EOF

echo "VLN_HIGH_PRECISION_LARGE_ASSET_SANDBOX_READY"
echo "source_project=$SOURCE_PROJECT"
echo "sandbox_project=$SANDBOX_PROJECT"
