#!/usr/bin/env bash

# 恢复 Topgear 传感器用户锁定场景。
# 用途：如果自动脚本或误操作再次覆盖传感器位置，先备份当前主场景，再恢复用户锁定版。

set -euo pipefail

VLN_ROOT="/home/ubuntu22/VLN"
UNITY_PROJECT="$VLN_ROOT/UnityProjects/VLN_Offroad"
SCENE_REL="Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity"
SCENE_PATH="$UNITY_PROJECT/$SCENE_REL"
SCENE_META_PATH="$SCENE_PATH.meta"
LOCKED_SCENE="$VLN_ROOT/config/topgear_sensor_scene_locked/VLNOffroadScoutWheelGroundCandidate_user_locked.unity"
LOCKED_SCENE_META="$LOCKED_SCENE.meta"
BACKUP_DIR="$VLN_ROOT/UnityProjects/_ManualRecoveryLogs/restore_$(date +%Y%m%d_%H%M%S)"

if ps -eo args= | awk '/UnityEditors\/2022\.3\.62f1\/Editor\/Unity/ && /UnityProjects\/VLN_Offroad/ { found=1 } END { exit found ? 0 : 1 }'; then
  echo "Unity 工程正在打开，拒绝恢复场景。请先关闭 Unity 再运行本脚本。"
  exit 2
fi

if [ ! -f "$LOCKED_SCENE" ]; then
  echo "未找到锁定场景：$LOCKED_SCENE"
  exit 1
fi

mkdir -p "$BACKUP_DIR"

if [ -f "$SCENE_PATH" ]; then
  cp "$SCENE_PATH" "$BACKUP_DIR/VLNOffroadScoutWheelGroundCandidate_before_restore.unity"
fi

if [ -f "$SCENE_META_PATH" ]; then
  cp "$SCENE_META_PATH" "$BACKUP_DIR/VLNOffroadScoutWheelGroundCandidate_before_restore.unity.meta"
fi

cp "$LOCKED_SCENE" "$SCENE_PATH"

if [ -f "$LOCKED_SCENE_META" ]; then
  cp "$LOCKED_SCENE_META" "$SCENE_META_PATH"
fi

echo "已恢复 Topgear 传感器锁定场景。"
echo "恢复来源：$LOCKED_SCENE"
echo "恢复前备份：$BACKUP_DIR"
