# 阶段 13：URDF/STL 物理车体工作流

## 目标

阶段 13 的目标是把当前“视觉小车候选”升级为“URDF 描述的物理底盘候选”。本阶段不覆盖已经跑通的 Unity-ROS2 主链路，而是在独立候选场景里验证真实底盘的 visual、collision、inertial 和 wheel joint。

当前第一候选来自师兄指定的 AgileX Scout V2：`agilexrobotics/ugv_gazebo_sim/scout/scout_description/urdf/scout_v2.xacro`。

## 当前模型格式

- 主描述文件：`scout_v2.xacro`。
- 展开产物：`generated/scout_v2.urdf`。
- 视觉 mesh：`base_link.dae`、`wheel_type1.dae`。
- 额外 mesh：目录内还有多个 `.STL`、`.dae`，包括 Scout Mini 和其他部件，本阶段第一轮只围绕 `scout_v2.xacro` 实际引用的 mesh。
- 物理描述：xacro 中已包含 base link 的 box collision、wheel 的 cylinder collision、底盘质量/惯性和四个连续轮关节。

## 本地缓存

```text
/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles/ugv_gazebo_sim_scout_description_raw
```

下载方式：没有继续完整 `git clone`，而是通过代理只下载 `scout/scout_description` 子目录，避免整仓克隆过慢。原始半截克隆已移动保留到 `VLN_ASSETS_CACHE/vehicles/ugv_gazebo_sim.partial_*`，未删除现场。

下载摘要：

```text
repo: agilexrobotics/ugv_gazebo_sim
ref: master
commit: 27633a956c845903ee630538afeb17fe70afdd84
subdir: scout/scout_description
file_count: 40
bytes_total: 94750231
```

## URDF 展开结果

展开命令不安装任何包，使用系统已有 ROS2 Humble 的 `xacro`。由于该描述包没有安装进 ROS2 ament index，不能直接解析 `$(find scout_description)`；当前用缓存内 staging 副本把 include 路径改成绝对路径后展开。

展开结果：

```text
generated/scout_v2.urdf
links: base_link, inertial_link, front_right_wheel_link, front_left_wheel_link, rear_left_wheel_link, rear_right_wheel_link
joints: inertial_joint, front_right_wheel, front_left_wheel, rear_left_wheel, rear_right_wheel
visual meshes: base_link.dae, wheel_type1.dae
collision_count: 6
inertial_count: 5
```

关键参数：

```text
base size: 0.925 x 0.380 x 0.210 m
wheelbase: 0.498 m
track: 0.58306 m
wheel radius: 0.16459 m
wheel length: 0.11653 m
base/inertial mass: 40 kg
each wheel mass: 3 kg
wheel joints: continuous, axis 0 -1 0
```

## 已知风险

- `scout_v2.xacro` 是 xacro，不是纯 URDF；Unity URDF Importer 通常需要最终 `.urdf`，所以必须先稳定展开。
- 原始 xacro 使用 `$(find scout_description)`，如果不安装成 ROS package，直接展开会失败。
- 展开后的 URDF 保留 Gazebo transmission/plugin 标签；Unity URDF Importer 可能忽略这些标签，不能把 Gazebo 插件当作 Unity 物理控制器。
- 初始 URDF 里传感器 link 不完整；第一轮继续使用当前已验证的 UnitySensors 相机和 LiDAR rig。
- 当前阶段不直接进入完整轮式动力学；先做静态导入、物理稳定、传感器回归和 `/vln/cmd_vel` 最小物理控制。
- Unity URDF Importer 对坐标轴设置敏感。Scout V2 当前必须使用 `ImportSettings.axisType.yAxis`；第一次使用 `zAxis` 时车体会竖起来，截图不合格。
- 不要使用 `UrdfRobotExtensions.CreateRuntime` 作为 Scout DAE 导入主线；该 runtime 路径会触发 Assimp `DllNotFoundException: libdl.so`。当前稳定路线是 Editor 导入路径 `UrdfRobotExtensions.Create(... forceRuntimeMode:false)`。

## Unity 导入结果

Unity 工程级依赖：

```text
com.unity.robotics.urdf-importer = https://github.com/Unity-Technologies/URDF-Importer.git?path=/com.unity.robotics.urdf-importer#v0.5.2
locked hash = 90f353e4352aae4df52fa2c05e49b804631d2a63
dependency = com.unity.editorcoroutines 1.0.0
```

Unity 候选资产目录：

```text
Assets/VLN/ExternalAssets/ScoutUrdfPhysics
```

正式 Unity 导入入口：

```text
Assets/VLN/ExternalAssets/ScoutUrdfPhysics/scout_v2_unity_import.urdf
```

该入口把原始 `package://scout_description/meshes/...` 改成 `package://meshes/...`，只引用实际需要的两个 DAE：

```text
Assets/VLN/ExternalAssets/ScoutUrdfPhysics/meshes/base_link.dae
Assets/VLN/ExternalAssets/ScoutUrdfPhysics/meshes/wheel_type1.dae
```

Unity URDF Importer 自动生成并保留：

```text
Assets/VLN/ExternalAssets/ScoutUrdfPhysics/Materials/Default.mat
Assets/VLN/ExternalAssets/ScoutUrdfPhysics/meshes/Cylinder.asset
```

候选场景：

```text
Assets/VLN/Scenes/VLNOffroadScoutUrdfCandidate.unity
```

导入后验收计数：

```text
urdf_robot_count=1
urdf_link_count=6
urdf_joint_count=5
urdf_continuous_joint_count=4
urdf_inertial_count=5
urdf_collision_count=6
unity_collider_count=6
renderer_count=17
articulation_body_count=5
bounds_size=0.700,0.351,0.930
```

当前导入策略仍保留已有 UnitySensors 相机、LiDAR、TF 和 `/vln/cmd_vel` rig。Scout URDF 结构随 rig 运动；新增 wheel joint 信号探针后，`/vln/cmd_vel` 已能写入四个 wheel ArticulationBody 的 `xDrive.targetVelocity`。本轮还新增 `/vln/odom [nav_msgs/msg/Odometry]`，由当前 Unity rig 实际位姿差分发布，frame 为 `map`，child frame 为 `base_link`。第一轮仍不是完整轮胎-地面摩擦/悬挂/电机动力学。

## 小步实施顺序

1. 已完成：运行现有基线回归，确认旧场景仍通过。导入前冻结 run id 为 `vln_asset_baseline_20260815_182915`，输出 `VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`。
2. 已完成：Scout V2 xacro 体检和 URDF 展开，确认 mesh、collision、inertial 和 wheel joint 可用。
3. 已完成：在 Unity 工程内加入 URDF Importer，并记录为工程级 UPM 依赖，不触碰系统包、Python、Conda、CUDA 或 PyTorch。
4. 已完成：复制 Scout 最小导入子集到 `Assets/VLN/ExternalAssets/ScoutUrdfPhysics`。
5. 已完成：使用 `scout_v2_unity_import.urdf` 作为 Unity 导入入口，避免 `package://scout_description` 在 Unity 内解析混乱。
6. 已完成：新建 `VLNOffroadScoutUrdfCandidate.unity`，没有覆盖现有候选场景。
7. 已完成：第一轮静态物理和姿态验收，底盘不穿地、不爆飞、四轮位置正确，collision/inertial/joint 存在。
8. 已完成：相机、LiDAR、TF 发布器保持旧接口，图像、CameraInfo、PointCloud2 和 TF 回归通过。
9. 已完成：Scout 候选场景 `/vln/cmd_vel` 控制回归通过。
10. 已完成：导入后完整资产基线回归，确认地图候选、Husky 视觉候选、标准输出、cmd_vel 控制和中文控制面板仍通过，最新 run id 为 `vln_asset_baseline_20260815_193207`。
11. 已完成：新增 wheel joint / ArticulationBody 信号探针，确认 `/vln/cmd_vel` 能映射到四个 wheel 的 `xDrive.targetVelocity`。
12. 已完成：新增候选 `/vln/odom` 输出，静态和控制验收均通过；控制验收中 odom 位移和 yaw 与 TF 一致。
13. 已完成第一轮：让 wheel-ground 接触真正驱动整车运动；`/joint_states` 等轮式动力学稳定后再加。

## 验收标准

- Unity 候选场景能看到 Scout V2 底盘和四个轮子，姿态正确。
- Unity Play 后无 `/vln/cmd_vel` 时底盘保持静止。
- 底盘 collision 生效，直行撞障碍物不穿模。
- ROS2 侧仍能收到 `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points`、`/tf`、`/vln/cmd_vel` 和 `/vln/odom`。
- 当前 `/vln/odom` 是基于 rig 位姿差分的候选输出；如果 `/joint_states` 第一轮不稳定，延后到轮式动力学增强阶段。

## 当前验收命令

静态 URDF / 姿态 / 感知回归：

```bash
/home/ubuntu22/VLN/scripts/run_scout_urdf_candidate_smoke_test.sh
```

成功标志：

```text
VLN_SCOUT_URDF_CANDIDATE_SMOKE_TEST_PASS
```

最近通过：

```text
run id: vln_scout_urdf_candidate_20260815_192630
urdf_link_count=6
urdf_joint_count=5
urdf_continuous_joint_count=4
urdf_inertial_count=5
urdf_collision_count=6
unity_collider_count=6
renderer_count=17
articulation_body_count=5
static_pose_delta_m=0.0000
odom_topic=/vln/odom
odom_type=nav_msgs/msg/Odometry
odom_frame=map
odom_child_frame=base_link
```

Scout 候选 `/vln/cmd_vel` 控制回归：

```bash
/home/ubuntu22/VLN/scripts/run_scout_urdf_cmd_vel_smoke_test.sh
```

成功标志：

```text
VLN_SCOUT_URDF_CMD_VEL_SMOKE_TEST_PASS
```

最近通过：

```text
run id: vln_scout_urdf_cmd_vel_20260815_195941
base_delta=2.260m
yaw_delta=2.836rad
odom_delta=2.260m
odom_yaw_delta=2.836rad
cmd_vel_count=48
collision_block_count=0
wheel_found_count=4
wheel_command_count=48
nonzero_target_count=4
odom_publish_count=339
```

导入后完整资产基线回归：

```bash
/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh
```

成功标志：

```text
VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS
```

最近通过：

```text
run id: vln_asset_baseline_20260815_200044
```

## 当前阶段边界

阶段 13 第一轮已经证明：Scout V2 的 URDF 结构可以进入 Unity，视觉 mesh、collision、inertial、continuous wheel joint 没有被完全丢失，并且旧 ROS2 图像、CameraInfo、LiDAR 点云、TF、`/vln/cmd_vel` 和候选 `/vln/odom` 仍然可用。

但阶段 13 本身还不能称为完整真实车辆动力学。`/vln/cmd_vel` 已经能写入四个 wheel joint drive 目标速度，`/vln/odom` 也能反映 rig 运动，但整车位移仍由现有运动学 rig 驱动，Scout URDF 车体作为该 rig 下的候选物理结构随车移动。阶段 14 已在独立候选场景中验证第一版 wheel-ground 接触驱动；后续如果要更接近论文级仿真，还应继续做差速转向、坡地、障碍物碰撞、轮胎摩擦、质量/惯性复核和可选 `/joint_states`。

## 阶段 14：wheel-ground 真实动力学候选

本阶段从“URDF 结构闭环”向“轮地接触驱动车体”推进。为保护已经通过的 Scout URDF 候选，阶段 14 不覆盖 `VLNOffroadScoutUrdfCandidate.unity`，而是新建独立场景：

```text
Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity
```

### 实现方式

- 新物理根：`ScoutWheelGround_PhysicsRoot`。
- 物理组件：`Rigidbody` + chassis `BoxCollider` + 4 个 `WheelCollider`。
- 视觉模型：`ScoutWheelGround_VisualUrdf` 复用 Scout V2 URDF mesh，但剥离 `Collider`、`Rigidbody`、`ArticulationBody` 和 URDF 脚本，只作为可视化模型。
- 控制器：`VlnScoutWheelGroundController` 订阅 `/vln/cmd_vel`，把 `linear.x` 和 `angular.z` 转换成左右轮目标转速，再通过 `WheelCollider.motorTorque` 和 `brakeTorque` 实现轮地接触驱动。
- TF/odom：`VlnVehicleTfPublisher` 在该场景关闭 `m_EnableKinematicMotion`，不再移动 rig；`VlnFollowTransformPose` 让传感器 rig 跟随真实物理根；`VlnOdomPublisher` 继续发布 `/vln/odom`。
- 高摩擦材质：`Assets/VLN/Materials/ScoutWheelGround_HighFriction.physicMaterial`，用于第一轮稳定轮地接触。

### 当前参数

```text
mass: 52 kg
wheel_radius: 0.16459 m
wheelbase: 0.498 m
track: 0.58306 m
max_linear_speed: 2.0 m/s
max_angular_speed: 1.0 rad/s
max_motor_torque: 520 Nm
max_brake_torque: 220 Nm
forward_friction_stiffness: 8.50
sideways_friction_stiffness: 2.10
max_longitudinal_assist_accel: 4.00 m/s^2
yaw_assist_gain: 3.00
max_yaw_assist_angular_accel: 2.00 rad/s^2
lateral_damping_gain: 4.00
max_lateral_damping_accel: 3.00 m/s^2
physical_road_slab_design_width: 6.2 m
physical_road_max_width_verified: 6.939 m
physical_bridge_width: 2.25 m
physical_short_ramp_width: 4.8 m
wheel_visual_vertical_offset: 0.085 m
physics_fixed_delta_time: 0.01 s
```

这些参数是第一轮保守候选，不代表最终论文级 Scout 标定。`yaw_assist` 和 `lateral_damping` 是施加到 `Rigidbody` 的物理力/力矩稳定项，用来模拟当前简化 WheelCollider 模型缺失的差速转向响应和轮胎侧向阻尼；它们不改位姿、不关碰撞、不铺隐藏平路。后续若师兄给真实电机、轮胎、悬挂、整车质量和质心参数，应优先替换这里的候选值。

### 验收命令

```bash
/home/ubuntu22/VLN/scripts/run_scout_wheel_ground_smoke_test.sh
```

成功标志：

```text
VLN_SCOUT_WHEEL_GROUND_SMOKE_TEST_PASS
```

最近通过：

```text
run id: vln_scout_wheel_ground_20260816_181841
motion_source=wheel_ground_contact_not_kinematic_rig
physics_backend=Unity WheelCollider + Rigidbody
wheel_collider_count=4
broad_physical_trail_count=0
road_physical_slab_count=8
road_seam_transition_count=5
bridge_physics_count=3
short_ramp_physics_count=1
decorative_trail_collider_count=0
road_physical_max_width_m=6.939
bridge_physical_max_width_m=2.250
short_ramp_physical_max_width_m=4.800
visual_renderer_count=17
visual_collider_count=0
visual_articulation_body_count=0
physics_root_delta_m=3.2591m
cmd_vel_count=58
controller_cmd_count=58
motor_command_count=616
```

### 当前边界

- 已证明 `/vln/cmd_vel` 能通过 wheel-ground 接触驱动物理车体前进，不再依赖旧运动学 rig 位移。
- 已证明相机、CameraInfo、LiDAR 点云、TF 和 `/vln/odom` 在该候选场景中保持可用。
- 当前只验收低速直行，不等于完整越野动力学。
- 下一步应分小步验证：低速差速转向、停止制动、坡地通过、撞障碍不穿模、轮胎摩擦参数调参、质量/惯性复核和 `/joint_states` 输出。

## 阶段 15：固定路线物理巡航演示

阶段 15 在阶段 14 的 wheel-ground 候选场景上增加 ROS2 固定路线脚本，目的是让用户能一键观察小车沿固定路线低速运动时的物理交互，而不是继续手工点动。

### 入口脚本

```bash
/home/ubuntu22/VLN/scripts/run_scout_wheel_ground_route_smoke_test.sh
/home/ubuntu22/VLN/scripts/drive_scout_wheel_ground_route_demo.sh
```

### 默认路线和参数

```text
relative_waypoints=4.0,0.0;8.0,0.0;12.0,0.0;15.0,0.0;18.0,0.0;22.0,0.0;26.0,0.0;28.0,0.0;30.0,0.0;34.0,0.0;42.0,0.0;50.0,0.0;54.0,0.0
max_linear=1.05 m/s
linear_gain=0.62
linear_accel=0.70 m/s^2
max_angular=0.55 rad/s
angular_gain=0.70
angular_accel=0.30 rad/s^2
min_linear_while_turning=0.38 m/s
goal_tolerance=1.60 m
angular_sign=1
```

这些默认值来自最新完整路线自动验收。注意：`ScoutWheelGround_PhysicalTrailSurface_*` 连续隐形路面已经被判定为错误方案并撤销，因为它会让车轮真实接触面与用户肉眼看到的独木桥、台阶、半坡不一致；`8.0m` 宽可见桥/路通行面也不再作为当前标准，因为它视觉上仍像把桥和坡的难点抹平。当前方案改为受限宽度的可见局部物理体：主路物理 slab 设计宽度 `6.2m`、桥面物理宽度 `2.25m`、短坡连续可见 MeshCollider 宽度 `4.8m`。路面 slab 在桥区和短坡区让开，验收必须看到 wheel contact 审计中的 `bridge_contact_steps`、`short_ramp_contact_steps` 和足够的 `wheel_ground_height_span_m`。独木桥处旧 Kenney 可见桥必须被删除，`ScoutWheelGround_PhysicalBridgeDeck` 同时承担可见桥面和碰撞桥面，renderer/collider 顶面对齐。轮胎视觉旋转使用累计滚动角，不再直接套用 WheelCollider 瞬时旋转。正式验收强制 `broad_physical_trail_count=0`、`stall_count=0`、`skipped_count=0`、`decorative_bridge_renderer_count=0`，并限制轮胎视觉方向反转次数；只要出现宽泛隐形平路、桥/坡托底、旧视觉桥遮挡真实物理桥、停滞或跳点，就不能算通过。

补充强约束：用户已明确指出“桥和斜坡变扁、为了通过而简化”属于不可接受风险。后续不能为了路线通过而压平桥面、压平短坡、降低坡高或恢复隐藏托底面。当前 Unity 结果文件必须包含 `terrain_geometry_policy=visible_local_physics_no_flattening_no_hidden_bypass`；自动验收必须检查 `bridge_physical_height_span_m>=0.20`、`short_ramp_physical_height_span_m>=0.62`，并在每次完整路线验收中归档 `vln_offroad_scout_wheel_ground_bridge_screenshot.png` 和 `vln_offroad_scout_wheel_ground_short_ramp_screenshot.png`，供人工复查桥/坡是否仍像真实可见接触面。

### 最新验收

```text
run id: vln_scout_wheel_ground_route_20260817_125552
success marker: VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS
reached_count=13/13
total_forward_progress=52.435m
total_progress=52.435m
final_lateral_offset=-0.015m
max_reached_cross_track=0.015m
max_abs_lateral_offset=0.015m
max_bridge_abs_lateral_offset=0.014m
stall_count=0
skipped_count=0
broad_physical_trail_count=0
road_physical_slab_count=8
road_seam_transition_count=5
bridge_physics_count=3
short_ramp_physics_count=1
decorative_trail_collider_count=0
decorative_bridge_renderer_count=0
bridge_deck_has_renderer=1
bridge_deck_has_collider=1
bridge_deck_renderer_collider_top_delta_m=0.0000
road_physical_max_width_m=6.939
bridge_physical_max_width_m=2.250
bridge_physical_height_span_m=0.235
short_ramp_physical_max_width_m=4.800
short_ramp_physical_height_span_m=0.804
bridge_contact_steps=1629
short_ramp_contact_steps=1648
wheel_ground_height_span_m=0.821
wheel_visual_total_abs_roll_deg=73393.0
wheel_visual_direction_reversal_count=0
sensor checks: Image, CameraInfo, PointCloud2, odom pass
visual evidence: bridge screenshot and short-ramp screenshot archived in the run directory
```

2026-08-17 补充：手动速度控制底层修复后，正 `angular.z` 已统一为左转；固定路线脚本也必须使用 `angular_sign=1`。旧 `angular_sign=-1` 会让路线纠偏方向反掉，导致 S 型、偏出或卡点。

### 边界

阶段 15 不是完整路径规划，也不是 navigation2 或 VLN 控制器。它是可重复的完整路线物理巡航演示，能从起点沿道路通过桥/坡区域并跑向终点方向。后续如果要真正绕障、语义导航、重规划或接入 VLN 决策，应先单独标定低速转向、横向摩擦、制动、质心、碰撞边界和差速控制稳定性。
