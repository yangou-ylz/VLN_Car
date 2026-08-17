#!/usr/bin/env bash

# 阶段 18：Scout wheel-ground 后段挑战路线自动验收。
# 在保留原 13 点金标准路线的基础上，继续通过草地、青石路、沙地和低矮可越障碍。

set -eo pipefail

VLN_ROOT="/home/ubuntu22/VLN"

export RUN_ID_PREFIX="vln_scout_wheel_ground_challenge_route"
export RELATIVE_WAYPOINTS="4.0,0.0;8.0,0.0;12.0,0.0;15.0,0.0;18.0,0.0;22.0,0.0;26.0,0.0;28.0,0.0;30.0,0.0;34.0,0.0;42.0,0.0;50.0,0.0;54.0,0.0;60.0,0.0;66.0,0.0;72.0,0.0"
export EXPECTED_ROUTE_WAYPOINT_COUNT="16"
export REQUIRE_CHALLENGE_COURSE="1"
export MIN_CHALLENGE_SURFACE_CONTACT_STEPS="20"
export MIN_CHALLENGE_OBSTACLE_CONTACT_STEPS="2"
export ROUTE_EXTRA_ARGS="--centerline-corridor --centerline-forward-max 74.0 --progress-only-gates --skip-angular-calibration --angular-sign 1 --lookahead-distance 5.00 --corridor-lateral-gain 0.30 --corridor-max-heading-correction 0.34 --max-angular 0.55 --angular-gain 0.72 --max-linear 0.98 --linear-gain 0.62 --linear-accel 0.68 --angular-accel 0.30 --min-linear-while-turning 0.38 --max-lateral-offset 1.20 --max-final-lateral-offset 0.90 --max-bridge-lateral-offset 0.85 --bridge-forward-min 9.5 --bridge-forward-max 22.8 --stall-skip-seconds 12.0 --stall-skip-forward-margin 4.0 --min-reached 16 --min-total-progress 67.0"

echo "自动挑战路线回归入口：该脚本会 batch 打开 Unity。手工看新增场地请用 drive_scout_wheel_ground_challenge_route_demo.sh。"

"$VLN_ROOT/scripts/run_scout_wheel_ground_route_smoke_test.sh"
echo "VLN_SCOUT_WHEEL_GROUND_CHALLENGE_ROUTE_SMOKE_TEST_PASS"
