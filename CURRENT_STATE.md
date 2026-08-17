# 当前状态快速入口

本文件是每次继续工作的第一层短上下文，用来替代过去每次全量阅读长日志的低效流程。它不取代 `AGENTS.md`、`PROJECT_MEMORY.md`、`workflow.md`、`env.md` 或 `logs/issue_log.md`；它只负责把当前阶段、不可破坏的基线和下一步读取策略压缩到一个短文件里。

更新时间：2026-08-17

## 启动读取策略

每次开始新任务或上下文压缩恢复后，默认先读：

1. `AGENTS.md` 的约束部分。
2. 本文件 `CURRENT_STATE.md`。
3. 与本次任务直接相关的 `workflow.md` 小节、`user.md` 小节、脚本或代码文件。

只有出现以下情况时，才需要额外读长文件全文或大段历史：

- 进入全新阶段、改变技术路线、改环境、下载/导入新资产、安装依赖。
- 自动验收失败、Unity/ROS2 行为和当前基线冲突、需要定位历史踩坑。
- 需要修改长期约束、环境文档、基线标准或回滚策略。
- 用户明确要求复盘历史、解释设计原因或追溯某个问题。

常规子任务中不要反复全量读取 `PROJECT_MEMORY.md`、`workflow.md`、`env.md` 和 `logs/issue_log.md`。优先用 `grep` 定位关键词，再 `sed` 读取相关小节。

## 全局硬约束

- 全程中文交流。
- 未经用户确认，禁止安装、卸载、升级系统包、Python 包、Conda 包或 Snap 包。
- 禁止改动 RTX 5060 当前 CUDA / PyTorch 组合。
- ROS2 命令优先使用用户已有 `ros2env`，避免 Conda 污染。
- 所有项目相关目录必须在 `/home/ubuntu22/VLN` 下。
- 新资料、资产、bag、Unity 缓存和大型生成文件默认不进 git。

## 当前主线

项目当前不是完整 VLN 算法阶段，而是 Unity3D 越野仿真环境 + ROS2 感知/控制链路阶段。已经跑通：

- Unity 2022.3.62f1 + ROS-TCP-Connector + ROS-TCP-Endpoint。
- UnitySensors 相机 `/vln/front/image_raw` 与 CameraInfo。
- UnitySensors LiDAR `/vln/lidar/points`，PointCloud2。
- `map -> base_link -> front_camera_optical_frame,lidar_link` TF。
- Scout V2 URDF 视觉模型 + Unity WheelCollider/Rigidbody 轮地物理候选。
- `/vln/cmd_vel` 控制、`/vln/odom`、中文控制面板。
- 固定自动路线后段已新增挑战场地：草地、青石路、沙地、低矮可越障碍；三段已分散到斜坡后的大空间，不再挤在最后一块地，终点挡墙后移到 `z=53.5m`；旧桥/坡基线保留。

## 当前金标准基线

### 手工演示默认流程

用户平时看效果时，不要优先跑自动 batch 验收脚本。默认按这个顺序：

```bash
cd /home/ubuntu22/VLN
./scripts/open_unity_vln_project.sh
```

在 Unity 里打开 `Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，先不要点 Play；另开终端启动 endpoint：

```bash
cd /home/ubuntu22/VLN
./scripts/start_ros_tcp_endpoint.sh
```

然后回 Unity 点击 Play。再另开一个终端跑演示路线：

```bash
cd /home/ubuntu22/VLN
./scripts/drive_scout_wheel_ground_route_demo.sh
```

如果要看新增草地、青石路、沙地和低矮障碍，跑：

```bash
cd /home/ubuntu22/VLN
./scripts/drive_scout_wheel_ground_challenge_route_demo.sh
```

### 自动路线基线

这是当前老师演示和后续回归的金标准。以后除非明确加入新障碍物、新路线或新物理阶段，否则不要大改这条自动路线。任何改动后的表现如果比这个差，应优先回退或修回到不低于该表现。

验收命令：

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_route_smoke_test.sh
```

当前通过 run id：`vln_scout_wheel_ground_route_20260817_145357`

关键指标：

```text
success=VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS
reached_count=13/13
total_forward_progress=52.427m
final_lateral_offset=-0.024m
max_abs_lateral_offset=0.040m
max_bridge_abs_lateral_offset=0.000m
stall_count=0
skipped_count=0
broad_physical_trail_count=0
bridge_contact_steps=1628
short_ramp_contact_steps=1648
bridge_physical_height_span_m=0.235
short_ramp_physical_height_span_m=0.804
wheel_visual_direction_reversal_count=0
```

路线控制关键约定：`angular-sign=1`。原因是当前 Unity wheel-ground 底层已经统一为正 `angular.z` 左转。不要恢复旧的 `angular-sign=-1`。

不可接受的“通过方式”：隐藏托底、压平桥/坡、恢复宽泛隐形平路、恢复 8m 宽桥/路通行面、跳过卡点、放宽 gate 掩盖偏离。

### 后段挑战路线

这是当前新增场地的扩展验收，不替代上面的 13 点金标准基线。它在旧路线后继续走到草地、青石路、沙地和低矮可越障碍区域，用于证明新增障碍有物理接触但不会卡死。

验收命令：

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh
```

当前通过 run id：`vln_scout_wheel_ground_challenge_route_20260817_144908`

关键指标：

```text
success=VLN_SCOUT_WHEEL_GROUND_CHALLENGE_ROUTE_SMOKE_TEST_PASS
reached_count=16/16
total_forward_progress=70.432m
final_lateral_offset=0.065m
max_abs_lateral_offset=0.088m
stall_count=0
skipped_count=0
challenge_grass_surface_count=1
challenge_stone_surface_count=1
challenge_sand_surface_count=1
challenge_obstacle_count=155
challenge_obstacle_collider_count=15
challenge_surface_contact_steps=16576
challenge_obstacle_contact_steps=498
challenge_surface_height_span_m=0.164
challenge_obstacle_height_span_m=0.144
challenge_end_wall_z=53.500
```

### 速度控制基线

验收命令：

```bash
cd /home/ubuntu22/VLN
./scripts/run_control_panel_manual_velocity_unity_smoke_test.sh
```

当前通过 run id：`vln_control_panel_manual_velocity_unity_20260817_130258`

键位约定：

```text
↑  -> 正 linear.x，前进
↓  -> 负 linear.x，后退
←/A -> 正 angular.z，左转
→/D -> 负 angular.z，右转
```

当前专项验收覆盖 `↑`、A/D、`←/→` 和停车漂移。

## 常用入口

- Unity 工程入口：`./scripts/open_unity_vln_project.sh`
- ROS-TCP-Endpoint：`./scripts/start_ros_tcp_endpoint.sh`
- 手工 13 点路线演示：`./scripts/drive_scout_wheel_ground_route_demo.sh`
- 手工 16 点挑战路线演示：`./scripts/drive_scout_wheel_ground_challenge_route_demo.sh`
- 控制面板：`./scripts/start_vln_control_panel.sh`
- 自动 13 点回归验收：`./scripts/run_scout_wheel_ground_route_smoke_test.sh`
- 自动 16 点挑战回归验收：`./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh`
- 速度控制专项回归：`./scripts/run_control_panel_manual_velocity_unity_smoke_test.sh`
- 手动记录回归：`./scripts/run_control_panel_manual_recording_smoke_test.sh`
- 清理 Unity lock：`./scripts/stop_unity_vln_project.sh`

## 需要查长文档时的定位方式

- 当前阶段和验收：查 `workflow.md` 的阶段 15/16/17/18。
- 用户复制命令：查 `user.md`。
- 环境和安装限制：查 `env.md`。
- 问题根因：先在 `logs/issue_log.md` 里按关键词搜。
- 技术取舍：先在 `logs/decision_log.md` 里按关键词搜。
- 总历史状态：需要跨阶段复盘时再查 `PROJECT_MEMORY.md`。
