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

当前状态：ROS2 侧已完成准备和端口验收；启动脚本为 `/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh`。Unity 侧已创建正式工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`，并已导入 `ROS-TCP-Connector`。下一步只做最小通信场景和 ROS2 echo 验收，不进入传感器和越野资产导入。

## 阶段 4：相机图像闭环

- Unity 侧：导入 UnitySensors 和 UnitySensorsROS，放置 RGB Camera 或 Panoramic Camera。
- ROS2 侧：订阅 `sensor_msgs/msg/Image`。
- 验收：`rqt_image_view` 能显示图像；`ros2 topic hz` 能看到稳定帧率；必要时记录 rosbag。

## 阶段 5：LiDAR 点云闭环

- Unity 侧：优先低负载 LiDAR 配置，例如 VLP-16 或 Mid360，先低频测试。
- ROS2 侧：订阅 `sensor_msgs/msg/PointCloud2`。
- 验收：RViz2 能显示点云；`ros2 topic bw` 不异常；点云 frame 与 fixed frame 有 TF 关系。

## 阶段 6：越野环境

- 先做极简 terrain：地面、坡、土路、石头、树木，确认 collider 正常。
- 禁止一开始导入大型资产包或高清植被森林。
- 验收：相机能看到越野元素；LiDAR 点云能扫到地形和障碍物；帧率可接受。

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
