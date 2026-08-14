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
14. 进入 VLN 感知层数据集/训练/算法对接。

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

最近通过 run id：`vln_asset_baseline_20260814_040515`。车体候选显示修正 run id：`vln_offroad_vehicle_candidate_20260814_095155`，该候选场景的图像输出已提高到 `1280x720`。手工查看时先把 Unity Game 视图 Scale 调回 `1x` 或 `Fit`，不要用 `10x` 放大画面判断模型质量。下一步不要直接上完整 URDF 动力学；推荐先确认 Husky 低多边形车体是否足够展示，如果不够，应筛选高清 UGV/越野车视觉资产。

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
