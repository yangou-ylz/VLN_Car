#!/usr/bin/env bash

# Read-only repository release checklist for the Mesa Topgear team handoff.

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

echo "== VLN Mesa Topgear 仓库发布检查 =="
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
    VLN_ASSETS_CACHE/*|VLN_REFERENCE_LIBRARY/*|VLN_BAGS/*|VLN_RECORDINGS/*|.runtime/*|UnityEditors/*|unity_ros2_ws/*|UnityProjects/VLN_Offroad_LargeAssetSandbox/*|UnityProjects/*/Library/*|UnityProjects/*/Temp/*|UnityProjects/*/Logs/*|*.unitypackage|*.assetpackage|*.bag|*.db3|*.mcap|*.pcd|*.ply|*.las|*.laz|*.tar.xz|Unity-*.tar.xz|config/world_model_current_save.json)
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

if [[ -f UnityProjects/VLN_Offroad/ProjectSettings/ProjectVersion.txt ]] && grep -q '2022.3.62f1' UnityProjects/VLN_Offroad/ProjectSettings/ProjectVersion.txt; then
  pass "主 Unity 工程版本锁定为 2022.3.62f1。"
else
  fail "未确认主 Unity 工程版本为 2022.3.62f1。"
fi

manifest="UnityProjects/VLN_Offroad/Packages/manifest.json"
if [[ -f "$manifest" ]] \
  && grep -q 'com.unity.robotics.ros-tcp-connector' "$manifest" \
  && grep -q 'com.frj.unity-sensors' "$manifest" \
  && grep -q 'com.frj.unity-sensors-ros' "$manifest"; then
  pass "Unity ROS/传感器 UPM 依赖在 manifest 中。"
else
  fail "Unity manifest 缺少 ROS-TCP-Connector 或 UnitySensors 依赖。"
fi

required_scripts=(
  scripts/setup_ros_tcp_endpoint_workspace.sh
  scripts/start_ros_tcp_endpoint.sh
  scripts/open_mesa_topgear_team_release_project.sh
  scripts/prepare_mesa_topgear_team_release_project.sh
  scripts/check_mesa_topgear_team_release_project.sh
  scripts/package_mesa_topgear_team_release_project.sh
  scripts/start_mesa_topgear_local_keyboard_control.sh
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
