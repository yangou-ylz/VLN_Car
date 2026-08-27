#!/usr/bin/env bash

# Read-only repository release checklist for the public Mesa Topgear handoff.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VLN_ROOT="${VLN_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
STRICT=0

if [[ "${1:-}" == "--strict" ]]; then
  STRICT=1
elif [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  cat <<'EOF'
用法：
  ./scripts/check_repo_release_readiness.sh
  ./scripts/check_repo_release_readiness.sh --strict

默认模式：发现致命仓库问题才失败，未提交改动/无 remote 只警告。
strict：未提交改动、未配置 remote 也会失败，适合 push 前最后检查。
EOF
  exit 0
fi

cd "$VLN_ROOT"

fail_count=0
warn_count=0

pass() { printf '[PASS] %s\n' "$1"; }
warn() { printf '[WARN] %s\n' "$1"; warn_count=$((warn_count + 1)); }
fail() { printf '[FAIL] %s\n' "$1"; fail_count=$((fail_count + 1)); }

echo "== VLN Mesa Topgear 公开仓库检查 =="
echo "repo=$VLN_ROOT"

if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  pass "当前目录是 Git 仓库。"
else
  fail "当前目录不是 Git 仓库。"
fi

if git remote -v | grep -q .; then
  pass "已配置 Git remote。"
else
  warn "未配置 Git remote；可以本地提交，但不能直接 push。"
fi

if [[ -z "$(git status --short --untracked-files=all)" ]]; then
  pass "工作区干净。"
else
  warn "工作区存在未提交或未追踪改动；push 前应确认是否提交或忽略。"
fi

blocked=()
large=()
max_bytes=$((50 * 1024 * 1024))

while IFS= read -r -d '' path; do
  case "$path" in
    AGENTS.md|CURRENT_STATE.md|PROJECT_MEMORY.md|env.md|workflow.md|user.md|study.md|logs/*|docs/asset_*|docs/*_workflow.md|docs/*_smoke_test.md|docs/workflow_research.md|docs/reference_sources.md|docs/hardware_assessment.md|docs/manual_visualization_guide.md|docs/technology_route.md|docs/vehicle_asset_candidates.md|scripts/run_*|scripts/check_high_precision_*|scripts/check_manual_*|scripts/check_standardized_*|scripts/check_unity_*|scripts/check_vln_*|scripts/check_world_model_*|scripts/analyze_*|scripts/inspect_*|scripts/rank_*|scripts/record_*|scripts/replay_*|scripts/report_*|scripts/scan_*|scripts/download_*|scripts/fetch_*|scripts/stage_*|scripts/rebuild_*|scripts/*smoke*|VLN_ASSETS_CACHE/*|VLN_REFERENCE_LIBRARY/*|VLN_BAGS/*|VLN_RECORDINGS/*|.runtime/*|UnityEditors/*|unity_ros2_ws/*|UnityProjects/VLN_Offroad/*|UnityProjects/VLN_Offroad_LargeAssetSandbox/*|UnityProjects/*/Library/*|UnityProjects/*/Temp/*|UnityProjects/*/Logs/*|*.unitypackage|*.assetpackage|*.bag|*.db3|*.mcap|*.pcd|*.ply|*.las|*.laz|*.tar.xz|Unity-*.tar.xz|config/world_model_current_save.json)
      blocked+=("$path")
      ;;
  esac

  if [[ -f "$path" ]]; then
    size_bytes="$(stat -c '%s' "$path" 2>/dev/null || printf '0')"
    if [[ "$size_bytes" =~ ^[0-9]+$ ]] && (( size_bytes > max_bytes )); then
      large+=("$size_bytes $path")
    fi
  fi
done < <(git ls-files -z)

if (( ${#blocked[@]} == 0 )); then
  pass "未发现被追踪的大资产、缓存、运行态或世界保存 marker。"
else
  fail "发现不应被 Git 追踪的文件："
  printf '  %s\n' "${blocked[@]}"
fi

if (( ${#large[@]} == 0 )); then
  pass "未发现超过 50MiB 的单个 tracked 文件。"
else
  fail "发现超过 50MiB 的 tracked 文件："
  printf '  %s\n' "${large[@]}" | sort -nr | sed 's/^/  /'
fi

release_project="UnityProjects/VLN_MesaTopgear_TeamRelease"
if [[ -f "$release_project/ProjectSettings/ProjectVersion.txt" ]] && grep -q '2022.3.62f1' "$release_project/ProjectSettings/ProjectVersion.txt"; then
  pass "本机 Mesa Topgear 发布工程版本为 2022.3.62f1。"
elif [[ -d "$release_project" ]]; then
  fail "本机 Mesa Topgear 发布工程版本不是 2022.3.62f1。"
else
  pass "Unity 发布工程由资产包分发，Git 仓库不直接追踪 Unity 工程。"
fi

manifest="$release_project/Packages/manifest.json"
if [[ -f "$manifest" ]] \
  && grep -q 'com.unity.robotics.ros-tcp-connector' "$manifest" \
  && grep -q 'com.frj.unity-sensors' "$manifest" \
  && grep -q 'com.frj.unity-sensors-ros' "$manifest"; then
  pass "本机 Mesa Topgear 发布工程包含 Unity ROS/传感器 UPM 依赖。"
elif [[ -d "$release_project" ]]; then
  fail "本机 Mesa Topgear 发布工程 manifest 缺少 ROS-TCP-Connector 或 UnitySensors 依赖。"
else
  pass "Unity manifest 位于发布资产包中，Git 仓库不直接追踪。"
fi

required_scripts=(
  scripts/setup_ros_tcp_endpoint_workspace.sh
  scripts/start_ros_tcp_endpoint.sh
  scripts/prepare_mesa_topgear_team_release_project.sh
  scripts/check_mesa_topgear_team_release_project.sh
  scripts/package_mesa_topgear_team_release_project.sh
  scripts/open_high_precision_world_model.sh
  scripts/open_unity_large_asset_sandbox_project.sh
  scripts/start_mesa_topgear_local_keyboard_control.sh
  scripts/local_keyboard_cmd_vel_control.py
  scripts/view_all_camera_images.sh
  scripts/view_vln_vehicle_rviz.sh
  scripts/check_repo_release_readiness.sh
)

missing_scripts=()
for script in "${required_scripts[@]}"; do
  if [[ ! -x "$script" ]]; then
    missing_scripts+=("$script")
  fi
done

if (( ${#missing_scripts[@]} == 0 )); then
  pass "团队部署关键脚本存在且可执行。"
else
  fail "关键脚本不存在或不可执行："
  printf '  %s\n' "${missing_scripts[@]}"
fi

if [[ -f docs/team_environment_setup.md ]] && grep -q 'mesa_topgear' docs/team_environment_setup.md && grep -q 'VLN_MesaTopgear_TeamRelease' docs/team_environment_setup.md; then
  pass "Mesa Topgear 团队部署文档存在。"
else
  fail "缺少 Mesa Topgear 团队部署文档，或文档未指向当前主线交付工程。"
fi

required_configs=(
  config/mesa_topgear_vehicle_candidate.json
  config/topgear_camera_data_pose_user_locked.json
  config/topgear_sensor_hierarchy_user_locked.json
  config/topgear_sensor_pose_user_locked.json
  config/topgear_upper_assembly_user_locked.json
  config/vln_lidar_pointcloud.rviz
  config/vln_vehicle_sensors.rviz
)

missing_configs=()
for config_path in "${required_configs[@]}"; do
  if [[ ! -f "$config_path" ]]; then
    missing_configs+=("$config_path")
  fi
done

if (( ${#missing_configs[@]} == 0 )); then
  pass "Mesa Topgear 必要配置文件存在。"
else
  fail "缺少 Mesa Topgear 必要配置文件："
  printf '  %s\n' "${missing_configs[@]}"
fi

echo "== tracked 文件体量 Top 10 =="
top_tracked_files="$({
  while IFS= read -r -d '' path; do
    [[ -f "$path" ]] || continue
    printf '%s\t%s\n' "$(stat -c '%s' "$path" 2>/dev/null || printf '0')" "$path"
  done < <(git ls-files -z)
} | sort -nr | head -10 || true)"
printf '%s\n' "$top_tracked_files"

if (( STRICT == 1 && warn_count > 0 )); then
  fail "strict 模式下 warning 也需要处理。"
fi

echo "summary: failures=$fail_count warnings=$warn_count strict=$STRICT"

if (( fail_count > 0 )); then
  echo "VLN_REPO_RELEASE_READINESS_FAILED"
  exit 1
fi

echo "VLN_REPO_RELEASE_READINESS_OK"
