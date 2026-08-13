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
10. 进入 VLN 感知层数据集/训练/算法对接。

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
- 验收：传感器随车体运动，TF/topic 命名稳定，RViz 显示无 frame 错误。

## 阶段 8：标准化输出

- 固定 topic 命名、frame 命名、频率、分辨率、点云配置。
- 产物：RViz2 配置、rosbag 示例、启动顺序文档、故障排查表。
- 验收：新终端按 `env.md` 步骤可复现实验。

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
