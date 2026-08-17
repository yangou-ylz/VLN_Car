# VLN Unity-ROS2 仿真项目长期约束

本文件是本仓库最高优先级的项目协作约束。后续所有开发、排错、环境搭建和文档更新，都必须先阅读本文件，再阅读 `CURRENT_STATE.md`。不要默认每次全量读取所有长文档；按“分级上下文读取机制”决定是否追加阅读 `PROJECT_MEMORY.md`、`env.md`、`workflow.md` 和日志。

## 交流与执行原则

- 全程使用中文交流，解释步骤、风险、命令和结论。
- 当前阶段目标不是完整 VLN 算法，而是先搭建 Unity3D 越野仿真环境，并通过 ROS2 跑通感知层输入：相机图像和 3D LiDAR 点云。
- 所有工作必须按小步推进：前一步未完成验收，禁止启动下一步大范围搭建。
- 每一步必须有明确输入、操作、验收命令或可视化验收方式。
- 优先保护已有 ROS2、CUDA、PyTorch、Conda 和系统环境；禁止为了省事污染全局环境。

## 环境安全红线

- 未经用户明确确认，禁止执行任何安装、卸载、升级、清理系统包的命令，包括但不限于 `apt install`、`apt upgrade`、`pip install`、`conda install`、`snap install`、`rm -rf` 大范围删除。
- 如确实需要新增 Python 包，必须优先创建项目内虚拟环境或独立临时虚拟环境，且先向用户确认。
- 不得改动用户已配好的 CUDA / PyTorch 组合。当前机器为 RTX 5060 系列，PyTorch 与 CUDA 版本匹配非常敏感。
- ROS2 使用用户已有 `ros2env` 函数进入环境；不要默认依赖全局 shell 已经 source 过 ROS2。
- Conda 与 ROS2 易冲突；ROS2 相关命令前优先使用 `ros2env` 或等价的干净 ROS2 shell。

## 工作流约束

- 每次继续开发前先读：`CURRENT_STATE.md`。如果任务涉及新阶段、环境变更、安装/下载、基线风险、排障或历史追溯，再按关键词定向读取 `PROJECT_MEMORY.md`、`workflow.md`、`env.md`、`logs/issue_log.md`、`logs/decision_log.md` 的相关小节。
- 禁止在普通子任务中无差别全量读取所有长记忆文件；优先使用 `grep` / `sed` 定位相关段落，减少上下文浪费。
- 每次完成环境变更、关键决策、踩坑修复、版本选择后，必须同步更新对应文档。
- `CURRENT_STATE.md` 必须保持短小，专门记录当前阶段、最新金标准基线、常用命令和快速读取策略；长历史仍归档到 `PROJECT_MEMORY.md`、`logs/issue_log.md` 和 `logs/decision_log.md`。
- 所有本项目相关工作目录都必须放在 `/home/ubuntu22/VLN` 内部；不要在 `/home/ubuntu22` 下散放 `unity_ros2_ws`、资料库、资产库、bag 目录或 Unity 工程目录。
- 官方资料、网页快照、外部仓库摘要放在 `/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY`，该目录在 git 中忽略，不提交。
- 大型 Unity 资产、模型、rosbag、构建产物、缓存目录不得提交到 git。
- 新增 `.gitignore` 规则时，以防止 Unity、ROS2、Python、rosbag、模型资产产生海量垃圾文件为优先。

## 技术路线约束

- 主路线：Unity3D + ROS-TCP-Connector + ROS-TCP-Endpoint + UnitySensors / UnitySensorsROS + ROS2 Humble。
- 先跑通信闭环，再跑传感器闭环，再导入越野环境，最后导入小车和规范 topic / TF。
- 相机输出优先标准 ROS2 消息：`sensor_msgs/msg/Image`，后续补 `CameraInfo`。
- LiDAR 输出优先标准 ROS2 消息：`sensor_msgs/msg/PointCloud2`。
- 坐标系、TF、topic 命名、帧率、分辨率、点云规模必须文档化。

## 质量要求

- 工作流应高效、复杂度可控、安全、通用、易扩展、易维护。
- 不追求一步到位的大而全搭建；优先构建可验证、可复现、可回滚的小闭环。
- 当前自动路线 `vln_scout_wheel_ground_route_20260817_125552` 是阶段 15 金标准基线。后续除非明确加入新障碍物、新路线或新物理阶段，否则不要大改；任何改动后的表现如果低于该基线，应优先回退或修回。
- 对官方文档和社区经验的引用必须标注来源，并在本地资料库建立索引。
- 每个阶段都要有“完成定义”：能看到什么、能 echo 什么、能记录什么、失败时查哪里。
