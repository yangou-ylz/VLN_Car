# Unity-ROS2 越野 VLN 仿真工作流

本工作流用于控制项目推进顺序。任何阶段未通过验收前，不进入下一阶段。

## 总体路线

1. 建立项目约束、环境记录、资料库和日志机制。
2. 只读确认本机环境：GPU、CUDA、PyTorch、ROS2、代理、磁盘、Unity 是否安装。
3. 收集并固化官方资料：Unity Robotics Hub、ROS-TCP-Connector、ROS-TCP-Endpoint、UnitySensors、URDF Importer、ROS2 消息与 TF 文档。
4. Unity 与 ROS2 最小通信闭环。
5. UnitySensors 相机图像闭环。
6. UnitySensors LiDAR 点云闭环。
7. 极简越野 terrain 闭环。
8. 小车模型或占位车体闭环。
9. topic、TF、rosbag、RViz 配置标准化。
10. ROS2 `/vln/cmd_vel` 控制闭环。
11. ROS2 路径点控制闭环。
12. 本地中文控制面板：目标位置、相机视图、雷达点云触发。
13. 成熟越野地图与真实小车模型候选导入。
14. URDF/STL 物理车体闭环：导入真实底盘 URDF，验证 visual/collision/inertial/joint，并保持 ROS2 感知与控制接口稳定。
15. Wheel-ground 真实动力学候选：新建独立候选场景，让轮地接触驱动车体，同时保持 ROS2 相机、LiDAR、TF、cmd_vel 和 odom 接口稳定。
16. Scout wheel-ground 固定路线物理巡航：新增 ROS2 固定路线脚本，驱动物理车体从起点沿道路通过桥/坡区域并跑向终点方向，用于观察轮地接触、坡地/路面交互、传感器跟随和是否穿模。
17. 手动示教路线记录与回放：在中文控制面板中用键盘手动驾驶真实物理车体，记录 `/vln/cmd_vel` 速度序列，导出 JSON 后可复现回放。
18. 进入 VLN 感知层数据集/训练/算法对接。

## 阶段 0：项目约束与记忆机制

- 产物：`AGENTS.md`、`PROJECT_MEMORY.md`、`env.md`、`logs/issue_log.md`、`logs/decision_log.md`、`.gitignore`。
- 验收：这些文件存在，且明确禁止未确认安装、禁止污染 CUDA/PyTorch/ROS2 环境。

## 阶段 1：只读环境体检

- 只读命令：`nvidia-smi`、`lscpu`、`free -h`、`df -h`、`python3 -c` 查询 torch、`ros2 pkg list`。
- 禁止：安装、卸载、升级、改 shell 配置。
- 验收：在 `env.md` 中记录硬件、驱动、CUDA、PyTorch、ROS2、代理和风险。

## 阶段 2：官方资料库

- 目录：`/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY`。
- 资料类型：官方 README、官方教程、ROS2 消息定义、Unity 版本要求、社区问题摘要。
- 验收：资料库内有 `index.md`，每条资料包含来源 URL、用途、保存路径、阅读结论。

## 阶段 3：ROS2-Unity 最小通信闭环

- 只使用独立 ROS2 workspace，例如 `~/unity_ros2_ws`，不要污染已有 `~/ws_ros2`。
- ROS2 侧：构建并启动 `ROS-TCP-Endpoint`。
- Unity 侧：导入 `ROS-TCP-Connector`，设置协议 ROS2、IP、端口。
- 验收：ROS2 能 echo Unity 发布的测试 topic；Unity 能响应 ROS2 发布的测试 topic。

当前状态：已完成。ROS2 侧 endpoint 可监听 `127.0.0.1:10000`；Unity 侧正式工程已导入 `ROS-TCP-Connector` 并设置 `ROS2` 编译符号；`/home/ubuntu22/VLN/scripts/run_ros2_unity_smoke_test.sh` 已验证 Unity 发布 `/unity/heartbeat` 可被 ROS2 echo，ROS2 发布 `/ros2/command` 可被 Unity 接收。

阶段 3 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_ros2_unity_smoke_test.sh
```

成功标志：`VLN_ROS2_SMOKE_TEST_PASS`。

## 阶段 4：相机图像闭环

- 前置条件：阶段 3 必须能复现 `VLN_ROS2_SMOKE_TEST_PASS`。
- Unity 侧：导入 UnitySensors 和 UnitySensorsROS，放置 RGB Camera 或 Panoramic Camera。
- ROS2 侧：订阅 `sensor_msgs/msg/Image`。
- 验收：`rqt_image_view` 能显示图像；`ros2 topic hz` 能看到稳定帧率；必要时记录 rosbag。

当前状态：已完成最小 RGB 相机闭环。UnitySensors `RGBCameraSensor` 发布 `/vln/front/image_raw`，类型为 `sensor_msgs/msg/Image`，640x480，`rgb8`，frame 为 `front_camera_optical_frame`，约 5Hz；同时发布 `/vln/front/camera_info`。

阶段 4 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_unitysensors_image_smoke_test.sh
```

成功标志：`VLN_UNITYSENSORS_IMAGE_SMOKE_TEST_PASS`。

阶段 4 完成定义：ROS2 图像字段校验脚本输出 `VLN_UNITYSENSORS_IMAGE_MSG_OK`，`ros2 topic list -t` 同时出现 `/vln/front/image_raw [sensor_msgs/msg/Image]` 与 `/vln/front/camera_info [sensor_msgs/msg/CameraInfo]`，`ros2 topic hz` 能看到约 5Hz。

## 阶段 5：LiDAR 点云闭环

- Unity 侧：优先低负载 LiDAR 配置，例如 VLP-16 或 Mid360，先低频测试。
- ROS2 侧：订阅 `sensor_msgs/msg/PointCloud2`。
- 验收：RViz2 能显示点云；`ros2 topic bw` 不异常；点云 frame 与 fixed frame 有 TF 关系。

当前状态：已完成最小 LiDAR 点云闭环。UnitySensors `RaycastLiDARSensor` 使用 VLP-16 scan pattern，发布 `/vln/lidar/points`，类型为 `sensor_msgs/msg/PointCloud2`，frame 为 `lidar_link`，7200 点/帧，`point_step=16`，约 5Hz，带宽约 0.6 MB/s。

阶段 5 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_unitysensors_lidar_smoke_test.sh
```

成功标志：`VLN_UNITYSENSORS_LIDAR_SMOKE_TEST_PASS`。

阶段 5 完成定义：ROS2 点云字段校验脚本输出 `VLN_UNITYSENSORS_POINTCLOUD2_MSG_OK`，`ros2 topic list -t` 出现 `/vln/lidar/points [sensor_msgs/msg/PointCloud2]`，`ros2 topic hz` 约 5Hz，`ros2 topic bw` 约 0.6 MB/s。当前最小测试阶段用 `/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh` 临时发布 `map -> lidar_link` 静态 TF，并在 RViz2 中使用 `Fixed Frame=map`；后续阶段 8 再标准化完整 TF 树。手工排障时不能只看 topic 是否存在，必须用 `/home/ubuntu22/VLN/scripts/check_manual_visualization_state.sh` 确认能实时收到一帧有效 PointCloud2。

## 阶段 6：越野环境

- 先做极简 terrain：地面、坡、土路、石头、树木，确认 collider 正常。
- 禁止一开始导入大型资产包或高清植被森林。
- 验收：相机能看到越野元素；LiDAR 点云能扫到地形和障碍物；帧率可接受。

当前状态：已完成。场景 `Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity` 使用轻量程序化网格地形，不导入大型资产包；包含土路、坡、石块、树木、障碍物、静态占位车体、前向 RGB 相机和 VLP-16 LiDAR。

阶段 6 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_offroad_terrain_smoke_test.sh
```

成功标志：`VLN_OFFROAD_TERRAIN_SMOKE_TEST_PASS`。

阶段 6 完成定义：ROS2 图像字段校验输出 `VLN_UNITYSENSORS_IMAGE_MSG_OK`，ROS2 点云字段校验输出 `VLN_UNITYSENSORS_POINTCLOUD2_MSG_OK`，`ros2 topic list -t` 同时出现 `/vln/front/image_raw [sensor_msgs/msg/Image]`、`/vln/front/camera_info [sensor_msgs/msg/CameraInfo]`、`/vln/lidar/points [sensor_msgs/msg/PointCloud2]`，图像和点云 `ros2 topic hz` 均约 5Hz，点云带宽约 0.58 MB/s。阶段 6 完成后必须顺序回归阶段 4/5，避免破坏基础传感器闭环。

注意：阶段 6 联合 terrain + RGB 相机自动验收使用 Unity `-batchmode`，不要加 `-nographics`；当前机器上 `-nographics` 会让 Unity 在 `RGBCameraSensor` 的 `Camera.Render` 路径段错误。阶段 4/5 单独 smoke test 仍可继续使用原脚本。

## 阶段 7：小车模型

- 优先导入 URDF；没有 URDF 时用占位车体先跑传感器。
- 验收：传感器挂在 `base_link` 下，TF/topic 命名稳定，RViz 显示无 frame 错误；无控制指令时车体静止。

当前状态：已完成可控占位车体闭环。阶段 7 暂不导入真实 URDF，先让程序化占位车体承载前向 RGB 相机和 VLP-16 LiDAR，并由 Unity 发布正式 `/tf`。Unity Play 后默认静止，车体运动必须由 ROS2 `/vln/cmd_vel` 或其上层控制器触发。

阶段 7 固定 TF 树：

```text
map -> base_link
base_link -> front_camera_optical_frame
base_link -> lidar_link
```

阶段 7 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_vehicle_tf_smoke_test.sh
```

成功标志：`VLN_VEHICLE_TF_SMOKE_TEST_PASS`。

阶段 7 完成定义：ROS2 图像字段校验输出 `VLN_UNITYSENSORS_IMAGE_MSG_OK`，CameraInfo 字段校验输出 `VLN_UNITYSENSORS_CAMERA_INFO_MSG_OK`，点云字段校验输出 `VLN_UNITYSENSORS_POINTCLOUD2_MSG_OK`，TF 校验输出 `VLN_VEHICLE_TF_MSG_OK`，且无 `/vln/cmd_vel` 指令时 `base_link` 在静止观察窗口内最大位移不超过 `0.05m`。最近通过 run id 为 `vln_vehicle_tf_20260814_021645`，`max_base_delta=0.000m`。

## 阶段 8：标准化输出

- 固定 topic 命名、frame 命名、频率、分辨率、点云配置。
- 产物：RViz2 配置、rosbag 示例、启动顺序文档、故障排查表。
- 验收：新终端按 `env.md` 步骤可复现实验。

当前状态：已完成。阶段 8 已固定 topic、frame、正式 RViz 配置、手工检查脚本、rosbag 小样本记录脚本和一键自动验收脚本。

阶段 8 固定标准输出：

```text
/vln/front/image_raw      sensor_msgs/msg/Image          front_camera_optical_frame  640x480 rgb8 ~5Hz
/vln/front/camera_info    sensor_msgs/msg/CameraInfo     front_camera_optical_frame
/vln/lidar/points         sensor_msgs/msg/PointCloud2    lidar_link                  VLP-16 7200点/帧 ~5Hz
/tf                       tf2_msgs/msg/TFMessage         map/base_link/camera/lidar
```

阶段 8 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_standardized_outputs_smoke_test.sh
```

成功标志：`VLN_STANDARDIZED_OUTPUTS_SMOKE_TEST_PASS`。

阶段 8 手工工具：

```bash
/home/ubuntu22/VLN/scripts/check_standardized_vln_outputs.sh
/home/ubuntu22/VLN/scripts/view_vln_vehicle_rviz.sh
/home/ubuntu22/VLN/scripts/record_vln_sensor_bag_sample.sh
```

阶段 8 完成定义：一键脚本通过；`ros2 bag info` 能看到 `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points`、`/tf`；RViz 使用 `/home/ubuntu22/VLN/config/vln_vehicle_sensors.rviz`，不再需要临时 `map -> lidar_link` 静态 TF；无控制指令时车体保持静止。最近回归 run id 为 `vln_standardized_outputs_20260814_021919`。

## 阶段 9：ROS2 控制接口闭环

- 固定控制 topic：`/vln/cmd_vel`。
- 固定控制消息：`geometry_msgs/msg/Twist`。
- 当前控制对象：程序化占位车体，后续可替换真实小车模型但不应改动控制入口。
- 验收：ROS2 发布速度指令，Unity 车体响应运动，`/tf` 中 `base_link` 位姿发生符合指令的位移和 yaw 变化，同时图像、CameraInfo 和点云仍正常。

当前状态：已完成。Unity 端 `VlnVehicleTfPublisher` 已订阅 `/vln/cmd_vel`；首次收到指令前保持静止，收到过指令后若 0.75 秒无新指令则停止，防止 ROS2 端退出后车体继续运动。

阶段 9 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_cmd_vel_control_smoke_test.sh
```

成功标志：`VLN_CMD_VEL_CONTROL_SMOKE_TEST_PASS`。

阶段 9 完成定义：ROS2 控制脚本输出 `VLN_CMD_VEL_CONTROL_MSG_OK`，Unity 控制结果文件记录 `cmd_vel_received` 和 `cmd_vel_count`，topic list 出现 `/vln/cmd_vel [geometry_msgs/msg/Twist]`，且 `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points` 仍通过字段校验。最近通过 run id 为 `vln_cmd_vel_control_20260814_021738`。

## 阶段 10：ROS2 路径点控制闭环

- 输入：`/tf` 中的 `map -> base_link`。
- 输出：`/vln/cmd_vel [geometry_msgs/msg/Twist]`。
- 当前路径点形式：以启动时 `base_link` 为原点的相对路径点，x 为前向米，y 为左向米。
- 验收：ROS2 控制器到达路径点，同时图像、CameraInfo、点云仍正常。

当前状态：已完成。新增轻量 ROS2 路径点控制器 `/home/ubuntu22/VLN/scripts/ros2_drive_waypoints.py`，默认相对路径点为 `1.2,0.0;2.4,0.0`。

阶段 10 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_waypoint_control_smoke_test.sh
```

成功标志：`VLN_WAYPOINT_CONTROL_SMOKE_TEST_PASS`。

阶段 10 完成定义：ROS2 路径点控制器输出 `VLN_WAYPOINT_CONTROL_MSG_OK`，到达 2/2 个路径点，最终距离最后路径点在阈值内，topic list 出现 `/vln/cmd_vel [geometry_msgs/msg/Twist]`，且 `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points` 仍通过字段校验。最近通过 run id 为 `vln_waypoint_control_20260814_021829`。

## 阶段 11：本地中文控制面板

- 输入：浏览器 UI 中的相对目标坐标 `X,Y`。
- 输出：ROS2 后端发布 `/vln/cmd_vel [geometry_msgs/msg/Twist]`。
- 触发工具：相机按钮打开 `view_front_image.sh`，雷达按钮打开 `view_vln_vehicle_rviz.sh`。
- 验收：HTTP 控制面板能收到 `/tf`，发送目标后 Unity 车体响应，传感器 topic 不受影响。

当前状态：已完成。新增 `/home/ubuntu22/VLN/scripts/vln_control_panel.py`、`start_vln_control_panel.sh`、`vln_control_panel_smoke_client.py` 和 `run_control_panel_smoke_test.sh`。UI 使用 Python 标准库 HTTP server + 浏览器，不安装新库。

阶段 11 固定启动命令：

```bash
/home/ubuntu22/VLN/scripts/start_vln_control_panel.sh
```

默认地址：

```text
http://127.0.0.1:8765/
```

阶段 11 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_control_panel_smoke_test.sh
```

成功标志：`VLN_CONTROL_PANEL_SMOKE_TEST_PASS`。

阶段 11 完成定义：UI 截图检查通过；HTTP 客户端发送相对目标后，Unity 收到 `/vln/cmd_vel`，车体到达目标附近。最近通过 run id 为 `vln_control_panel_20260814_025329`。

## 阶段 12：成熟越野地图与真实小车模型候选导入

- 目标：按师兄口径，从成熟资产中选择越野地图和更真实的小车模型，逐步替换当前轻量程序化场景和占位车体。
- 原则：不直接替换主场景；不同时导入多个大资产；先记录许可证、体积、渲染管线和依赖，再导入候选场景。
- 候选资料库：`/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/asset_candidates/index.md`。
- 工作流文档：`/home/ubuntu22/VLN/docs/asset_upgrade_workflow.md`。
- 基线回归：导入前后必须跑候选场景、标准输出、cmd_vel 控制和控制面板 smoke test；可用 `/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh` 一键执行。

当前状态：已完成第一轮复杂越野地图候选导入。候选资产为 Kenney Nature Kit 2.1，许可证 CC0，下载缓存为 `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/kenney_nature-kit.zip`，Unity 仅导入 70 个轻量 FBX 子集到 `Assets/VLN/ExternalAssets/KenneyNatureKit`，候选场景为 `Assets/VLN/Scenes/VLNOffroadAssetCandidate.unity`。该场景保留当前可控占位车体、相机、LiDAR、`/tf` 和 `/vln/cmd_vel`，没有覆盖主场景。

当前状态补充：已完成第一轮真实小车视觉候选导入。已下载 Husky 与 Jackal 源仓；第一轮选择 Husky 作为视觉车体候选，只导入 `base_link.dae`、`top_chassis.dae`、`user_rail.dae`、`bumper.dae`、`wheel.dae` 到 `Assets/VLN/ExternalAssets/HuskyVisual`。候选场景为 `Assets/VLN/Scenes/VLNOffroadVehicleCandidate.unity`，采用“真实车体视觉替换 + 保留现有 ROS2 控制/TF/传感器 rig”的策略，不改 `/vln/front/*`、`/vln/lidar/points`、`/tf`、`/vln/cmd_vel`。

阶段 12 固定候选场景验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_offroad_asset_candidate_smoke_test.sh
/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh
```

成功标志：`VLN_OFFROAD_ASSET_CANDIDATE_SMOKE_TEST_PASS`。
小车候选成功标志：`VLN_OFFROAD_VEHICLE_CANDIDATE_SMOKE_TEST_PASS`。

阶段 12 固定完整回归命令：

```bash
/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh
```

成功标志：`VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`。

最近完整资产基线回归 run id：`vln_asset_baseline_20260815_191337`。车体候选显示修正 run id：`vln_offroad_vehicle_candidate_20260814_095155`，该候选场景的图像输出已提高到 `1280x720`。手工查看时先把 Unity Game 视图 Scale 调回 `1x` 或 `Fit`，不要用 `10x` 放大画面判断模型质量。Husky 视觉候选保留为低多边形工程展示资产；真实物理底盘路线已经转入阶段 13 Scout URDF 候选。

## 阶段 13：URDF/STL 物理车体闭环

- 目标：把阶段 12 的“视觉车体候选”升级为“URDF 描述的物理底盘候选”，验证真实底盘的 visual、collision、inertial 和 wheel joint，同时保留已跑通的 ROS2 感知与控制主链路。
- 原则：新建候选场景和候选资产目录，不覆盖 `VLNOffroadTerrainSmokeTest.unity`、`VLNOffroadAssetCandidate.unity` 或 `VLNOffroadVehicleCandidate.unity`。
- 当前第一候选：AgileX Scout V2，来源为 `agilexrobotics/ugv_gazebo_sim` 的 `scout/scout_description/urdf/scout_v2.xacro`。
- 本地缓存：`/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles/ugv_gazebo_sim_scout_description_raw`。
- 已完成体检：`scout_v2.xacro` 可展开为 `generated/scout_v2.urdf`；展开后包含 `base_link`、`inertial_link`、四个 wheel link，四个 continuous wheel joint，6 个 collision，5 个 inertial。
- 已完成导入前基线冻结：`/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh` 输出 `VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`，run id 为 `vln_asset_baseline_20260815_182915`。
- 已完成 Unity URDF 候选导入：新增候选场景 `Assets/VLN/Scenes/VLNOffroadScoutUrdfCandidate.unity`，Unity 导入入口为 `Assets/VLN/ExternalAssets/ScoutUrdfPhysics/scout_v2_unity_import.urdf`。
- 已完成姿态修复：URDF Importer 的 `chosenAxis` 使用 `ImportSettings.axisType.yAxis`；`zAxis` 会让 Scout 车体竖起。当前截图显示车身平放、四轮竖直贴地。
- 已完成静态验收：`/home/ubuntu22/VLN/scripts/run_scout_urdf_candidate_smoke_test.sh` 输出 `VLN_SCOUT_URDF_CANDIDATE_SMOKE_TEST_PASS`，run id `vln_scout_urdf_candidate_20260815_185336`。
- 已完成控制验收：`/home/ubuntu22/VLN/scripts/run_scout_urdf_cmd_vel_smoke_test.sh` 输出 `VLN_SCOUT_URDF_CMD_VEL_SMOKE_TEST_PASS`，最新 run id `vln_scout_urdf_cmd_vel_20260815_195941`。
- 已完成候选 odom 输出：Scout 候选场景新增 `/vln/odom [nav_msgs/msg/Odometry]`，frame 为 `map`，child frame 为 `base_link`，由当前 Unity rig 实际位姿差分生成；控制验收中 `odom_delta=2.261m`、`odom_yaw_delta=2.843rad`，与 TF 运动一致。
- 已完成 wheel joint 信号探针：四个 wheel ArticulationBody 均被找到，`/vln/cmd_vel` 已映射为 wheel `xDrive.targetVelocity`，验收结果为 `wheel_found_count=4`、`wheel_command_count=48`、`nonzero_target_count=4`。
- 已完成导入后完整基线回归：`/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh` 输出 `VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`，最新 run id `vln_asset_baseline_20260815_200044`。
- 初始接口保持不变：`/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points`、`/tf`、`/vln/cmd_vel` 不改名。
- 第一轮传感器策略：保留当前 UnitySensors 相机与 LiDAR rig，挂到导入底盘的 `base_link` 语义节点下；不在第一轮同时重写传感器组件。
- 第一轮控制策略：已把 `/vln/cmd_vel` 作为信号写入四个 wheel ArticulationBody 的 drive 目标速度，但暂不让 wheel-ground 接触决定整车位移。
- 当前边界：阶段 13 已具备 URDF visual/collision/inertial/joint 结构、可用 ROS2 感知/控制闭环、wheel joint 信号接入和候选 `/vln/odom` 输出；整车位移仍沿用现有运动学 rig，还没有完成真实轮胎-地面摩擦、悬挂、电机、轮速闭环或 `/joint_states`。

阶段 13 小步顺序：

1. 已完成：冻结现有基线，确认已有图像、点云、TF、cmd_vel 和控制面板仍通过。
2. 已完成：Scout URDF 体检，记录 xacro 展开结果、mesh 引用、collision/inertial/joint 清单和已知风险。
3. 已完成：Unity 工程级加入 `com.unity.robotics.urdf-importer`，不安装系统包或 Python 包。
4. 已完成：Scout 候选资产导入到 `Assets/VLN/ExternalAssets/ScoutUrdfPhysics`，候选场景不覆盖旧场景。
5. 已完成：静态物理验收，Unity Play 后底盘不爆飞、不穿地、不自动运动；mesh、collision 和 wheel link 姿态正确。
6. 已完成：传感器挂载回归，图像、CameraInfo、PointCloud2、TF 仍按旧 topic 输出。
7. 已完成：控制闭环回归，ROS2 发布 `/vln/cmd_vel` 后 Scout 候选随当前 rig 运动；停止发布后车辆停止。
8. 已完成：导入后完整资产基线回归，确认地图候选、Husky 视觉候选、标准输出、cmd_vel 控制和中文控制面板仍通过。
9. 已完成：wheel joint 信号探针，确认 `/vln/cmd_vel` 能写入四个 wheel ArticulationBody 的目标速度。
10. 已完成：新增候选 `/vln/odom`，静态和控制验收均通过。
11. 已完成第一轮：新建独立 wheel-ground 候选场景，使用 Unity `Rigidbody + WheelCollider` 让轮地接触驱动车体前进，旧 Scout URDF 候选场景不覆盖。
12. 下一步：增强差速转向、坡地、障碍物接触、轮胎参数标定和可选 `/joint_states`。

阶段 13 完成定义：

- `scout_v2.xacro` 或后续师兄提供的完整车体 xacro 能稳定展开为 URDF。
- Unity 候选场景中能看到正常姿态的 Scout 底盘和四个轮子。
- URDF collision/inertial/joint 不被丢弃，且物理稳定。
- ROS2 侧仍能看到标准图像、点云、TF 和 `/vln/cmd_vel`。
- 候选导入失败时，旧阶段 12 场景和脚本仍可独立回归。
- 当前第一轮仍是“URDF 物理结构 + 运动学控制 rig + 候选 odom”的安全闭环，不等于完整轮胎-地面摩擦、悬挂和电机动力学；后续论文级真实仿真应继续做 wheel-ground 接触、摩擦、质量/惯性和 joint state 增强。阶段 14 已开始把整车位移源从运动学 rig 切到独立物理根，但仍作为候选场景验证，不覆盖阶段 13 的 URDF 结构闭环。

阶段 13 关键风险记录：不要使用 `UrdfRobotExtensions.CreateRuntime` 做 DAE runtime 导入；该路径会触发 Assimp `DllNotFoundException: libdl.so`。当前稳定方案是 Editor 导入路径 `UrdfRobotExtensions.Create(... forceRuntimeMode:false)`，让 Unity 已导入的 DAE 资产实例化。日志里仍可能出现一次 `libdl.so` fallback 信息，但静态、控制和完整基线均已通过，暂不通过系统安装处理。

## 阶段 14：Scout wheel-ground 真实动力学候选

- 目标：让 Scout 候选小车不再由 `VlnVehicleTfPublisher` 运动学位移推动，而是由 Unity 物理系统中的轮地接触驱动车体。
- 场景：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`。
- 物理根：`ScoutWheelGround_PhysicsRoot`，包含 `Rigidbody`、1 个 chassis `BoxCollider` 和 4 个 `WheelCollider`。
- 视觉根：`ScoutWheelGround_VisualUrdf`，复用 Scout V2 URDF mesh，但剥离 collider、Rigidbody、ArticulationBody 和 URDF 脚本，只作为渲染模型。
- 控制器：`VlnScoutWheelGroundController` 订阅 `/vln/cmd_vel`，将 `geometry_msgs/msg/Twist` 转换为左右轮目标转速，并用 `WheelCollider.motorTorque/brakeTorque` 驱动。
- TF/odom：`VlnVehicleTfPublisher` 在该场景中关闭 `m_EnableKinematicMotion`，只负责发布 TF 和接收 cmd_vel 计数；`VlnFollowTransformPose` 让传感器 rig 跟随物理根；`VlnOdomPublisher` 继续发布 `/vln/odom`。
- 接口保持：`/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points`、`/tf`、`/vln/cmd_vel`、`/vln/odom` 不改名。

阶段 14 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_scout_wheel_ground_smoke_test.sh
```

成功标志：

```text
VLN_SCOUT_WHEEL_GROUND_SMOKE_TEST_PASS
```

最近通过：

```text
run id: vln_scout_wheel_ground_20260815_195417
motion_source=wheel_ground_contact_not_kinematic_rig
physics_backend=Unity WheelCollider + Rigidbody
wheel_collider_count=4
visual_renderer_count=17
visual_collider_count=0
visual_articulation_body_count=0
physics_root_delta_m=1.7739
forward_delta=1.771m
odom_forward_delta=1.771m
cmd_vel_count=58
controller_cmd_count=58
motor_command_count=614
odom_publish_count=378
```

阶段 14 当前边界：这是第一版可验证 wheel-ground 动力学候选，已经证明车体前进来自物理轮地接触而不是旧运动学 rig；但还没有完成论文级车辆模型标定，后续仍需要验证差速转向、坡地通过、障碍物碰撞、轮胎摩擦参数、质量/惯性复核和可选 `/joint_states`。

## 阶段 15：Scout wheel-ground 固定路线物理巡航

- 目标：在不覆盖手动控制、中文控制面板、相机、LiDAR、TF 和 odom 的前提下，增加一条可重复的固定路线演示，用来观察 Scout 物理车体和越野场景的交互。
- 场景：继续使用 `Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`。
- ROS2 控制脚本：`/home/ubuntu22/VLN/scripts/ros2_drive_scout_physics_route.py`。
- 手工演示入口：`/home/ubuntu22/VLN/scripts/drive_scout_wheel_ground_route_demo.sh`。
- 自动验收入口：`/home/ubuntu22/VLN/scripts/run_scout_wheel_ground_route_smoke_test.sh`。
- 默认路线：启动时以 `base_link` 为原点，沿前向依次到 `4,0;8,0;12,0;15,0;18,0;22,0;26,0;28,0;30,0;34,0;42,0;50,0;54,0` 米，用于从起点通过桥/坡区域并跑向终点方向。
- 默认控制策略：固定路径点物理巡航，`max_linear=1.05m/s`、`linear_accel=0.70m/s^2`、`max_angular=0.55rad/s`、`angular_gain=0.70`、`angular-sign=1`，不启用跳点，停滞即失败。`angular-sign=1` 必须与当前手动速度控制和 Unity wheel-ground 底层“正 angular.z 左转”的约定保持一致。
- 物理场景修复：Scout wheel-ground 视觉 URDF 不再额外 yaw `180°`，避免肉眼看成倒着开；轮胎视觉偏移为 `0.085m`，避免轮胎显示扎进地面；轮胎视觉旋转采用累计滚动角 `accumulated_roll_root_x`，不再把 `WheelCollider.GetWorldPose()` 的瞬时旋转直接套给视觉轮。旧的 `ScoutWheelGround_PhysicalTrailSurface_*` 连续隐形路面已撤销，旧的 `8.0m` 宽桥/路可见通行面也不再作为当前标准。当前使用受限宽度的可见局部物理体：主路物理 slab 设计宽度 `6.2m`、桥面物理宽度 `2.25m`、短坡连续可见 MeshCollider 宽度 `4.8m`；道路 slab 在桥区和短坡区让开，车轮必须接触桥/坡物理体。独木桥处旧 Kenney 可见桥已删除，`ScoutWheelGround_PhysicalBridgeDeck` 必须同时是可见桥面和碰撞桥面。
- 控制器稳定项：`VlnScoutWheelGroundController` 使用 wheel torque 驱动，并加入 yaw assist 与 lateral damping 物理力/力矩项，用于模拟差速转向响应和轮胎侧向阻尼；禁止通过改位姿、关碰撞或宽泛隐形路面通过验收。

阶段 15 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_scout_wheel_ground_route_smoke_test.sh
```

成功标志：

```text
VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS
```

最近通过：

```text
run id: vln_scout_wheel_ground_route_20260817_125552
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
bridge_screenshot=vln_offroad_scout_wheel_ground_bridge_screenshot.png
short_ramp_screenshot=vln_offroad_scout_wheel_ground_short_ramp_screenshot.png
```

补充记录：2026-08-17 手动速度控制修复后，自动路线一度失败，根因是路线脚本仍沿用旧 `angular-sign=-1`，与当前底层正 `angular.z` 左转的约定相反。已将 `run_scout_wheel_ground_route_smoke_test.sh`、`drive_scout_wheel_ground_route_demo.sh` 和 `ros2_drive_scout_physics_route.py` 默认值统一为 `angular-sign=1`，默认脚本复跑通过。

阶段 15 当前边界：它是“写死完整路线物理巡航演示”，不是完整 navigation2，也不是 VLN 决策控制器；路线沿道路前向通过桥/坡区域，但不做语义导航、自主绕障或目标重规划。当前强约束是禁止使用连续隐形平路、禁止铺道路宽桥面绕开独木桥、禁止用普通路面 slab 托底桥/坡、禁止压平桥和斜坡来通过路线、禁止跳过卡点、禁止把横向偏离很大的前向进度误判为到达；同时禁止恢复旧 Kenney 可见桥遮挡真实物理桥面，禁止恢复轮胎视觉高频正反抖。自动验收还必须保留桥区截图和短坡截图，且 `bridge_physical_height_span_m`、`short_ramp_physical_height_span_m` 必须满足非扁平阈值。后续若要绕障、上坡路径选择或接入 VLN 决策，应另开小步阶段，继续做稳定差速转向、横向摩擦、碰撞边界和低速转弯标定。

## 阶段 16：手动示教路线记录与回放

- 目标：让用户亲自驾驶 Scout wheel-ground 物理车体通过满意路线，记录实际 `/vln/cmd_vel` 速度数据，后续用该记录复现同一路线，避免继续硬调不稳定的自动 S 型路线。
- UI 入口：`/home/ubuntu22/VLN/scripts/start_vln_control_panel.sh`，浏览器打开 `http://127.0.0.1:8765/` 后进入“速度控制”模块。
- 键盘映射：`↑` 前进，`↓` 后退，`←` 或 `A` 左转，`→` 或 `D` 右转；前进/后退可与左转/右转组合。当前 Scout 是差速轮式底盘，不发布横向平移速度。
- 当前方向符号：`↑` 发布正 `linear.x`，`↓` 发布负 `linear.x`，`←/A` 发布正 `angular.z`，`→/D` 发布负 `angular.z`。该约定匹配当前 Unity wheel-ground 场景的视觉左/右方向。
- 默认速度：线速度 `0.55m/s`，角速度 `0.42rad/s`；可在 UI 中调整，但不要一开始调太快，避免物理车体在桥/坡和窄路处横摆。
- 安全保护：前端在按键保持时每 `50ms` 刷新速度心跳；后端 `manual-command-timeout=0.18s`，松键、浏览器失焦、页面隐藏或心跳丢失都会立即发布多帧 0 速度停车。
- 当前专项修复：`VlnScoutWheelGroundController` 不再让 WheelCollider 电机承担纯转向，轮端转向电机比例为 `0`；角速度由 Unity 物理层 yaw-rate PID + Rigidbody 角速度伺服执行，键位按 `←/A` 左转、`→/D` 右转。
- 记录格式：导出到 `/home/ubuntu22/VLN/VLN_RECORDINGS/manual_drives/manual_drive_YYYYMMDD_HHMMSS.json`，schema 为 `vln_manual_cmd_vel_recording_v1`，每条样本包含 `t`、`linear_x`、`angular_z`、按键状态和可选 `pose`。
- 回放入口：`/home/ubuntu22/VLN/scripts/replay_manual_drive_recording.sh --file <manual_drive_*.json>`。
- git 规则：`VLN_RECORDINGS/` 已加入 `.gitignore`，路线记录默认不提交，避免大量实验文件进入仓库。

阶段 16 当前验收状态：`./scripts/run_control_panel_manual_velocity_unity_smoke_test.sh` 已通过，最新 run id `vln_control_panel_manual_velocity_unity_20260817_130258`，覆盖 `↑` 正向直行、A/D 原地左右转、方向键 `←/→` 原地左右转和停车漂移检查；`./scripts/run_control_panel_manual_recording_smoke_test.sh` 已通过，run id `vln_control_panel_manual_recording_20260817_041218`；基础 wheel-ground 回归 `vln_scout_wheel_ground_20260817_041230` 已通过。阶段 15 自动路线已恢复，最新通过 run id `vln_scout_wheel_ground_route_20260817_125552`；后续仍不能用隐藏托底、压平桥/坡、跳点或放宽 gate 修路线。

阶段 16 固定验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_control_panel_manual_recording_smoke_test.sh
```

速度控制 Unity 联动验收命令：

```bash
/home/ubuntu22/VLN/scripts/run_control_panel_manual_velocity_unity_smoke_test.sh
```

成功标志：

```text
VLN_CONTROL_PANEL_MANUAL_VELOCITY_UNITY_SMOKE_TEST_PASS
```

成功标志：

```text
VLN_CONTROL_PANEL_MANUAL_RECORDING_SMOKE_TEST_PASS
```

回放文件最小验收示例：

```bash
/home/ubuntu22/VLN/scripts/replay_manual_drive_recording.sh --file /home/ubuntu22/VLN/VLN_RECORDINGS/manual_drives/manual_drive_YYYYMMDD_HHMMSS.json --time-scale 1.0
```

成功标志：

```text
VLN_MANUAL_DRIVE_REPLAY_OK
```

阶段 16 当前边界：记录/回放的是速度命令序列，不是闭环导航策略；如果仿真初始位置、场景物理参数、摩擦、碰撞体或车辆姿态改变，同一速度记录可能不会严格走出同一条空间轨迹。因此正式采集时应先固定起点、场景和车辆物理参数，再录制满意路线；后续若要变成鲁棒 VLN 或 Nav2，应另开阶段做定位、地图、路径规划和闭环纠偏。

## 工作流管理规则

- 每完成一个阶段，在 `PROJECT_MEMORY.md` 更新“最近一次工作记录”。
- 每出现一个问题，在 `logs/issue_log.md` 写明现象、根因、解决方式、复现/验收命令。
- 每做一个技术选择，在 `logs/decision_log.md` 写明选项、选择、理由、后续影响。
- 每改环境，在 `env.md` 追加，不覆盖旧记录。
- 每新增外部资料，在 `/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/index.md` 追加索引。

## 固定目录约定

- 当前轻量仓库：`/home/ubuntu22/VLN`。
- 项目内资料库：`/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY`。
- ROS2 独立工作区：`/home/ubuntu22/VLN/unity_ros2_ws`。
- 后续 Unity 工程：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`。
- Unity 工程固定打开脚本：`/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh`。
- 后续大资产缓存：`/home/ubuntu22/VLN/VLN_ASSETS_CACHE`。
- 后续 rosbag 输出：`/home/ubuntu22/VLN/VLN_BAGS`。

这些目录都在 `/home/ubuntu22/VLN` 下统一管理；资料、资产、bag、Unity 缓存和 ROS2 构建产物默认都不进 git。
