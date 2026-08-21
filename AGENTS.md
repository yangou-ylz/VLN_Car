# VLN Unity-ROS2 仿真项目长期约束

本文件是本仓库最高优先级的项目协作约束。后续所有开发、排错、环境搭建和文档更新，都必须先阅读本文件，再阅读 `CURRENT_STATE.md`。不要默认每次全量读取所有长文档；按“分级上下文读取机制”决定是否追加阅读 `PROJECT_MEMORY.md`、`env.md`、`workflow.md` 和日志。

## 交流与执行原则

- 全程使用中文交流，解释步骤、风险、命令和结论。
- 当前阶段目标不是完整 VLN 算法，而是先搭建 Unity3D 越野仿真环境，并通过 ROS2 跑通感知层输入：相机图像和 3D LiDAR 点云。
- 所有工作必须按小步推进：前一步未完成验收，禁止启动下一步大范围搭建。
- 每一步必须有明确输入、操作、验收命令或可视化验收方式。
- 给用户手工操作步骤时，默认采用“先打开 Unity 软件/场景，再启动 ROS-TCP-Endpoint，再点击 Play，最后运行 `drive_*_demo.sh` 或查看脚本”的流程；`run_*_smoke_test.sh` 是自动回归验收入口，除非用户明确要求自动验收、排障或回归测试，不要把它作为用户看效果的首选命令，更不要只给一键 batch 脚本替代手工演示流程。
- 自动回归也要控制成本：如果本轮改动只影响 Topgear 上装/传感器挂载，且 13 点主链路已经验证成功，就不要继续默认跑 16 点挑战长路线；16 点只在挑战区、障碍物、材质物理、长路线控制被修改，或用户明确要求时再跑。
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
- 当前自动路线 `vln_scout_wheel_ground_route_20260820_190253` 是阶段 15 / Topgear 传感器套件后的最新 13 点主链路金标准基线；历史稳定基线 `vln_scout_wheel_ground_route_20260817_232310` 仍作为阶段 15 原始参考。后续除非明确加入新障碍物、新路线或新物理阶段，否则不要大改；任何改动后的表现如果低于该基线，应优先回退或修回。
- Topgear 传感器外观硬约束：LiDAR 和相机必须使用官方/外部真实模型资产。当前 LiDAR 使用 Velodyne VLP-16 官方/外部 DAE mesh，四个相机使用 RealSense D405 官方 STL mesh；禁止再用程序化圆柱、方块、螺丝、小条、玻璃片等自建外观替代或兜底。官方模型加载失败时必须报错并修导入/轴向/缩放问题，不能临时自建模型骗过验收。
- Topgear 传感器位姿硬约束：当前只能以用户在 Unity 中肉眼手动摆放并通过 `VLN -> Topgear 传感器手动微调` 点击“保存当前五个传感器位姿并锁定为唯一基线”后生成的 `config/topgear_sensor_pose_user_locked.json` 为最高优先级基线；`config/topgear_sensor_pose_overrides.json` 只是兼容副本，不得把其中旧数值自动当成用户确认正确。如果用户现场明确说当前打开效果不是他确认的版本，则当前 locked JSON 也必须视为旧值/失效值，不能继续引用它证明正确。禁止再根据源码默认锚点、模型包围盒、圆盘/孔位或旧截图推断结果擅自改位置或角度。若用户说打开场景后位置又乱了，优先怀疑自动脚本重建/覆盖了 `Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，不要再声称“已经保存正确”。Topgear 传感器/视觉专项验收必须使用现有场景入口，禁止调用会重建并保存主场景的 `BuildScoutWheelGroundCandidateScene()`；任何必须重建主场景的操作都要先备份场景，且重建后只能应用锁定 JSON。
- 后续所有可通行材质和主要障碍必须遵守“视觉-物理一致”原则：视觉上会被小车接触、碾压或阻挡的东西，必须有对应的简化物理代理、材质摩擦/阻尼或接触逻辑；禁止只做漂亮视觉层但真实交互与形状完全无关。极小装饰细节可以只做视觉，但必须不承担主要交互语义。
- 材质物理不能过度简化到失真：草地应体现柔软、轻阻力、可被车轮压过/推开；沙地应体现较高滚阻、较低附着、软质波纹/浅洼；石板路应体现刚性、高摩擦、接缝和低矮凸起。可以用合并 collider、触发区域、材质参数和控制器阻力项近似，禁止给每片草叶/每粒沙子做重型 Rigidbody 造成无意义性能浪费。
- 草地视觉反馈当前固定为“第一版轻倒伏”：车轮附近草叶被压低、向两侧推开，并以低恢复速度留下轻微轮迹感。用户不喜欢第二版明显深色压痕/强倒伏轮迹；除非用户明确改口，禁止恢复 `GrassTrackPainter`、深色轮迹贴片或大面积强制压痕。
- 对官方文档和社区经验的引用必须标注来源，并在本地资料库建立索引。
- 每个阶段都要有“完成定义”：能看到什么、能 echo 什么、能记录什么、失败时查哪里。
