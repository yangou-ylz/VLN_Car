# 当前状态快速入口

本文件是每次继续工作的第一层短上下文，用来替代过去每次全量阅读长日志的低效流程。它不取代 `AGENTS.md`、`PROJECT_MEMORY.md`、`workflow.md`、`env.md` 或 `logs/issue_log.md`；它只负责把当前阶段、不可破坏的基线和下一步读取策略压缩到一个短文件里。

更新时间：2026-08-20

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
- 新增真实性硬约束：挑战区后续必须做“视觉-物理一致”的材质交互。草地、沙地、青石/石板路不能只是视觉贴图；主要接触形状必须有简化物理代理、材质摩擦/阻尼或接触逻辑，允许简化但不能脱离真实材质特性。

## 当前主线

项目当前不是完整 VLN 算法阶段，而是 Unity3D 越野仿真环境 + ROS2 感知/控制链路阶段。已经跑通：

- Unity 2022.3.62f1 + ROS-TCP-Connector + ROS-TCP-Endpoint。
- UnitySensors 相机 `/vln/front/image_raw` 与 CameraInfo。
- UnitySensors LiDAR `/vln/lidar/points`，PointCloud2。
- `map -> base_link -> front_camera_optical_frame,lidar_link` TF。
- Scout V2 URDF 视觉模型 + Unity WheelCollider/Rigidbody 轮地物理候选。
- Topgear V2 涂装/上装视觉 mesh 已叠加到 Scout 车身上方；它只作为视觉上装，不增加 `Collider`、`Rigidbody` 或动力学参数，底盘物理、WheelCollider、PID 和 ROS2 控制继续沿用原 Scout wheel-ground 基线。`/home/ubuntu22/VLN/topgear_v2.dae` 的上装姿态和安装位置已经由用户确认完成，后续不要再改上装根姿态和贴合位置。
- Topgear 传感器挂载阶段已完成官方模型修正：上装顶部圆盘中心安装 1 个竖直 Velodyne VLP-16 官方/外部 DAE mesh LiDAR，LiDAR 根位姿约为车体局部 `(0, 0.846, 0.004)`；LiDAR 下方上层小方盒四面安装前/后/左/右 4 个 RealSense D405 官方 STL 相机。传感器视觉模型只负责显示和 ROS2 数据发布，不添加 collider 或 rigidbody，不改变车体物理；禁止再用程序化圆柱、方块、螺丝、小条等自建外观替代。
- `/vln/cmd_vel` 控制、`/vln/odom`、中文控制面板。
- 固定自动路线后段已新增挑战场地：草地、青石路、沙地、低矮可越障碍；三段已分散到斜坡后的大空间，不再挤在最后一块地，终点挡墙后移到 `z=53.5m`；旧桥/坡基线保留。
- Unity Editor 内已新增 `VLN/ROS2 手工演示面板` 和 `VLN/手工演示/*` 菜单。它们只启动现有 ROS2 shell 脚本，不把导航控制塞进 Unity；底层仍通过 ROS2 发布 `/vln/cmd_vel`。菜单启动的外部终端会登记到 `.runtime/unity_menu/processes.tsv`，每次输出日志写入 `.runtime/unity_menu/logs/`；2026-08-18 因自动退出清理会误杀刚启动的终端，已临时彻底禁用 Unity 退出自动清理，只保留面板/菜单里的“关闭 VLN 后台终端”手动按钮。菜单终端包装器不再使用 `setsid`，目标脚本退出后会保留窗口，方便看到报错。

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

也可以在 Unity 顶部菜单打开 `VLN -> ROS2 手工演示面板`，按同样顺序点击按钮。菜单只是帮你开新终端运行现有脚本，仍需要 endpoint 正常运行，并且运行路线/传感器查看前 Unity 已点击 Play。

如果从 Unity 菜单启动过 endpoint、相机、RViz、中文控制面板或路线脚本，这些进程会被登记，但当前不会在 Unity 退出时自动清理。若遇到 `Address already in use` 或确认要关闭 VLN 菜单拉起的后台终端，在 Unity 菜单/面板点击 `关闭 VLN 后台终端`，或在终端运行：

```bash
cd /home/ubuntu22/VLN
./scripts/cleanup_unity_menu_processes.sh --include-known
```

如果菜单弹出的终端又快速退出，先查看最新日志：

```bash
ls -lt /home/ubuntu22/VLN/.runtime/unity_menu/logs | head
```

### 自动路线基线

这是当前老师演示和后续回归的金标准。以后除非明确加入新障碍物、新路线或新物理阶段，否则不要大改这条自动路线。任何改动后的表现如果比这个差，应优先回退或修回到不低于该表现。

验收命令：

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_route_smoke_test.sh
```

当前通过 run id：`vln_scout_wheel_ground_route_20260820_190253`

关键指标：

```text
success=VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS
reached_count=13/13
total_forward_progress=52.441m
final_lateral_offset=-0.004m
max_abs_lateral_offset=0.035m
max_bridge_abs_lateral_offset=0.000m
stall_count=0
skipped_count=0
broad_physical_trail_count=0
bridge_contact_steps=1628
short_ramp_contact_steps=1649
bridge_physical_height_span_m=0.235
short_ramp_physical_height_span_m=0.804
wheel_visual_direction_reversal_count=0
topgear_visual_present=1
topgear_sensor_suite_present=1
topgear_sensor_camera_count=4
topgear_sensor_lidar_count=1
topgear_visual_collider_count=0
topgear_visual_rigidbody_count=0
topgear_sensor_collider_count=0
topgear_sensor_rigidbody_count=0
```

用户最新约定：本轮 Topgear 传感器挂载后，13 点金标准链路已通过即可停止自动回归；不要继续浪费时间跑 16 点挑战路线。16 点挑战路线保留为手工/必要回归入口，后续只有新增障碍物、挑战区变化或用户明确要求时再跑。

Topgear V2 当前姿态约束：`topgear_v2.dae` 是 Blender/COLLADA `Z_UP` 模型，Unity 挂载时将 DAE 的 `+Z` 映射为车体局部 `+Y` 竖直方向，并让 DAE 的 `+Y` 前向对准 Scout 局部 `+Z` 车头方向；模型底部对齐到车身顶部平台附近。不要把 Topgear 上装加入 WheelCollider、chassis collider 或刚体系统，除非后续明确进入上装碰撞/传感器物理阶段。

路线控制关键约定：`angular-sign=1`。原因是当前 Unity wheel-ground 底层已经统一为正 `angular.z` 左转。不要恢复旧的 `angular-sign=-1`。

不可接受的“通过方式”：隐藏托底、压平桥/坡、恢复宽泛隐形平路、恢复 8m 宽桥/路通行面、跳过卡点、放宽 gate 掩盖偏离。

### 后段挑战路线

这是当前新增场地的扩展验收，不替代上面的 13 点金标准基线。它在旧路线后继续走到草地、青石路、沙地和低矮可越障碍区域，用于证明新增障碍有物理接触但不会卡死。

验收命令：

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh
```

当前通过 run id：`vln_scout_wheel_ground_challenge_route_20260817_231723`

关键指标：

```text
success=VLN_SCOUT_WHEEL_GROUND_CHALLENGE_ROUTE_SMOKE_TEST_PASS
reached_count=16/16
total_forward_progress=70.434m
final_lateral_offset=-0.004m
max_abs_lateral_offset=0.086m
stall_count=0
skipped_count=0
challenge_grass_surface_count=1
challenge_stone_surface_count=1
challenge_sand_surface_count=1
challenge_grass_blade_field_count=3
challenge_grass_deformer_count=3
challenge_grass_max_deformed_blade_count=418
challenge_grass_max_fresh_affected_blade_count=156
challenge_stone_visual_detail_count=80
challenge_stone_chip_field_count=1
challenge_sand_visual_detail_count=46
challenge_sand_grain_field_count=1
challenge_physics_proxy_count=22
grass_physics_proxy_count=5
stone_physics_proxy_count=7
sand_physics_proxy_count=10
challenge_visual_physics_proxy_audit_pass=1
challenge_pbr_albedo_material_count=7
challenge_pbr_normal_material_count=7
challenge_pbr_occlusion_material_count=7
challenge_obstacle_count=139
challenge_obstacle_collider_count=15
challenge_surface_contact_steps=16404
challenge_obstacle_contact_steps=1423
challenge_physics_proxy_contact_steps=991
grass_contact_steps=1334
stone_contact_steps=1351
sand_contact_steps=13752
grass_avg_speed_mps=0.580
stone_avg_speed_mps=0.631
sand_avg_speed_mps=0.110
grass_wheel_ground_height_span_m=0.086
stone_wheel_ground_height_span_m=0.055
sand_wheel_ground_height_span_m=0.106
challenge_surface_height_span_m=0.164
challenge_obstacle_height_span_m=0.653
challenge_end_wall_z=53.500
```

当前挑战区视觉证据已分段归档：`vln_offroad_scout_wheel_ground_challenge_grass_screenshot.png`、`vln_offroad_scout_wheel_ground_challenge_stone_screenshot.png`、`vln_offroad_scout_wheel_ground_challenge_sand_screenshot.png`。草地为三层程序化草叶 mesh + 5 条柔性低矮物理代理 + 第一版运行时草叶轻倒伏变形：车轮附近草叶被压低、向两侧推开，并以低恢复速度留下轻微轮迹感；青石路为铺石、暗缝、裂纹、碎石视觉层、ambientCG PavingStones151 1K PBR 贴图 + 7 条刚性接缝代理；沙地为沙纹、浅洼、颗粒视觉层、ambientCG Ground054 1K PBR 贴图 + 10 条软沙波纹代理。控制器已记录草/石/沙分材质接触步数、代理接触步数、平均速度和轮地高度扰动；不使用隐藏托底、关闭碰撞或重型粒子刚体。

阶段 18B 当前状态：已完成“材质一致物理代理 + 第一版草叶轻倒伏视觉反馈”。第二版明显深色压痕/强倒伏轮迹已回退，当前代码不应恢复 `GrassTrackPainter` 或深色轮迹贴片。下一步若继续升级，优先做用户手工观察后的微调或更多授权外部资产候选；不能破坏当前 13 点金标准和 16 点挑战路线。

### Topgear V2 上装视觉基线

阶段 19 当前状态：Topgear V2 上装已作为纯视觉件安装到 Scout 车身顶部，未改变底盘物理、WheelCollider、PID、ROS2 topic/TF 或已有相机/LiDAR 链路。用户已经确认上装安装位置完成，后续除非用户明确要求，不要再改上装安装位置、角度或贴合高度。专项多视角验收命令：

```bash
cd /home/ubuntu22/VLN
./scripts/run_topgear_visual_alignment_smoke_test.sh
```

当前视觉贴合基线：专项视觉验收 run id `vln_topgear_visual_alignment_20260820_175959` 曾显示上装底部到 Scout 顶板约 `0.009m`。后续按用户肉眼反馈做过 1cm 级微调，最终源码常量和主场景实例已冻结为 `AlignRendererBoundsToLocalFrame(... new Vector3(0f, 0.115f, 0.045f))` / `m_LocalPosition.y=0.115`；用户已确认安装位置完成。该修改只影响上装视觉 Y 向贴合，不改旋转、X/Z 位置、底盘物理或控制。Topgear 传感器阶段后的最新 13 点主链路通过 run id 为 `vln_scout_wheel_ground_route_20260820_190253`；按用户要求不继续默认跑 16 点挑战长路线。关键约束继续保持：`topgear_visual_present=1`、`topgear_visual_collider_count=0`、`topgear_visual_rigidbody_count=0`。

### Topgear 传感器挂载基线

阶段 20 当前状态：16 线 LiDAR + 前/后/左/右 4 个相机已经安装到 Topgear 上装对应位置，并通过 ROS2 数据链路验收。2026-08-20 晚间修复过两类重要问题：第一，旧版把 `topgear_v2.dae` 的 GPS/大黑箱区域误当成 LiDAR 下方小方盒，导致 LiDAR 偏离圆盘、四个相机散在错误位置；第二，曾为了“看起来更精细”加入程序化圆柱、方块、螺丝和竖条，这是不允许的。当前已改为只加载官方/外部真实模型：Velodyne VLP-16 DAE mesh + RealSense D405 STL mesh，并把自建传感器外观残留从源码和主场景清掉。LiDAR 仍保留原 `/vln/lidar/points` topic，前相机仍保留原 `/vln/front/*` topic，避免破坏既有 rqt/RViz/脚本；后/左/右相机新增独立 topic。2026-08-21 已确认一个重要问题：旧 Topgear 自动验收会调用 `BuildScoutWheelGroundCandidateScene()`，该函数会重建并保存主场景，可能覆盖用户在 Unity 中手动保存的传感器位置。现在 Topgear 传感器/视觉专项脚本已改为只打开现有主场景验证；后续看到位置回旧版时，优先查是否误跑了会重建场景的脚本，而不是继续微调传感器。

自动专项验收入口；该脚本只打开现有主场景，不重建、不保存覆盖主场景：

```bash
cd /home/ubuntu22/VLN
./scripts/run_topgear_sensor_suite_smoke_test.sh
```

最近一次通过 run id：`vln_topgear_sensor_suite_20260821_141404`。该次验收确认 Unity runner 使用 `rebuild_scene=False`，没有调用 `BuildScoutWheelGroundCandidateScene()`，四路相机、CameraInfo、LiDAR 点云和 TF 全部通过。

关键输出：

```text
success=VLN_TOPGEAR_SENSOR_SUITE_SMOKE_TEST_PASS
topgear_sensor_suite_present=1
topgear_sensor_camera_count=4
topgear_sensor_lidar_count=1
topgear_sensor_renderer_count=7
topgear_sensor_vlp16_official_mesh_count=1
topgear_sensor_d405_official_stl_count=4
topgear_sensor_procedural_vlp16_rib_count=0
topgear_sensor_procedural_d405_screw_count=0
topgear_sensor_collider_count=0
topgear_sensor_rigidbody_count=0
image_resolution=640x480
lidar_scan_pattern=VLP-16
lidar_points_per_scan=7200
lidar_nonzero_points>=80
tf_edges=map->base_link,base_link->front_camera_optical_frame,base_link->rear_camera_optical_frame,base_link->left_camera_optical_frame,base_link->right_camera_optical_frame,base_link->lidar_link
```

当前视觉约束：LiDAR 和相机视觉模型必须使用官方/外部真实模型资产，不得使用程序化外壳、圆柱、方块、螺丝、小条、玻璃片等自建外观。传感器位姿不再按源码默认“圆盘中心/孔位”推断。2026-08-21 14:11 已完成三重保险锁定：父传感器位姿 `config/topgear_sensor_pose_user_locked.json`、完整传感器层级位姿 `config/topgear_sensor_hierarchy_user_locked.json`、整场景固定恢复副本 `config/topgear_sensor_scene_locked/VLNOffroadScoutWheelGroundCandidate_user_locked.unity`。如果以后位置再次乱掉，先运行 `./scripts/restore_topgear_sensor_locked_scene.sh` 恢复整场景，再打开 Unity 检查；禁止用几何推断或旧截图重新改位置。

当前 Unity 菜单约束：`VLN -> ROS2 手工演示面板` 中已移除 13 点自动路线按钮；相机查看改为右侧选项栏。`rqt` 会打开四路 `rqt_image_view`，Unity 内部的 `全部相机/前相机/后相机/左相机/右相机` 会直接显示当前场景四个 Camera 的简洁预览窗口，不弹终端；打开 `全部相机` 时，单路相机按钮默认禁用，关闭全部相机窗口后再恢复。

当前 topic/frame：

```text
/vln/front/image_raw        front_camera_optical_frame
/vln/front/camera_info      front_camera_optical_frame
/vln/rear/image_raw         rear_camera_optical_frame
/vln/rear/camera_info       rear_camera_optical_frame
/vln/left/image_raw         left_camera_optical_frame
/vln/left/camera_info       left_camera_optical_frame
/vln/right/image_raw        right_camera_optical_frame
/vln/right/camera_info      right_camera_optical_frame
/vln/lidar/points           lidar_link
```

阶段 20 边界：传感器视觉件不参与物理碰撞，不改变车辆质量、惯量、悬挂、轮胎或路线控制；它们是 UnitySensors/UnitySensorsROS 组件挂载点 + ROS-TCP-Connector 发布链路。下一步由用户按手工流程亲自打开 Unity 看模型、看四路图像和 LiDAR 点云。

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

用户 2026-08-18 反馈：浏览器 UI 手动速度控制实际体验仍明显差于自动路线，表现为按住前进/方向后小车只动一下或响应慢。已定位主要差异：自动路线是 ROS2 侧闭环控制器持续读取 `/tf` 并发布 `/vln/cmd_vel`；浏览器 UI 原先更依赖键盘心跳和 HTTP 请求，且界面上的箭头/A/D 只是状态显示，不是真正可按住的屏幕按钮。当前已修复为：屏幕箭头/A/D 也可按住控制；前端速度请求有背压保护；速度/停车请求带序号，旧请求晚到不会覆盖新停车或新按键；心跳 fallback 超时为 `0.35s`，松键仍立即停车。补充修复：导出目录/文件路径旁新增“复制路径”按钮；线速度默认仍为 `0.55m/s`，但 UI、后端和 Unity wheel-ground 控制器可调上限已放宽到 `20.0m/s`；线速度 `+/-` 步进改为 `0.50m/s`，调速时会立即刷新当前按键速度。注意：自动路线脚本默认 `--max-linear` 不改，仍保护老师演示金标准；20m/s 只用于用户手动速度控制上限。该修复已通过 Python / shell 静态检查、备用端口短启动页面检查和 `/api/velocity` 夹紧检查；当前 8765 控制面板已重启为新版，但仍需要用户按手工流程亲自验收 Unity 体感。

## 常用入口

- Unity 工程入口：`./scripts/open_unity_vln_project.sh`
- Unity 内演示面板：`VLN -> ROS2 手工演示面板`
- ROS-TCP-Endpoint：`./scripts/start_ros_tcp_endpoint.sh`
- 手工 13 点路线演示：`./scripts/drive_scout_wheel_ground_route_demo.sh`
- 手工 16 点挑战路线演示：`./scripts/drive_scout_wheel_ground_challenge_route_demo.sh`
- 控制面板：`./scripts/start_vln_control_panel.sh`
- 自动 13 点回归验收：`./scripts/run_scout_wheel_ground_route_smoke_test.sh`
- 自动 16 点挑战回归验收：`./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh`
- Topgear 上装视觉对齐验收：`./scripts/run_topgear_visual_alignment_smoke_test.sh`
- Topgear 传感器专项验收：`./scripts/run_topgear_sensor_suite_smoke_test.sh`
- 速度控制专项回归：`./scripts/run_control_panel_manual_velocity_unity_smoke_test.sh`
- 手动记录回归：`./scripts/run_control_panel_manual_recording_smoke_test.sh`
- 清理 Unity lock：`./scripts/stop_unity_vln_project.sh`

## 需要查长文档时的定位方式

- 当前阶段和验收：查 `workflow.md` 的阶段 15/16/17/18/19/20。
- 用户复制命令：查 `user.md`。
- 环境和安装限制：查 `env.md`。
- 问题根因：先在 `logs/issue_log.md` 里按关键词搜。
- 技术取舍：先在 `logs/decision_log.md` 里按关键词搜。
- 总历史状态：需要跨阶段复盘时再查 `PROJECT_MEMORY.md`。
