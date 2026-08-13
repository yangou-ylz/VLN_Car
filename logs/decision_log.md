# 决策日志

记录格式：时间、决策、备选项、理由、影响。

## 2026-08-13：主路线采用 Unity + ROS2 + UnitySensors

- 决策：当前阶段采用 Unity3D + ROS-TCP-Connector + ROS-TCP-Endpoint + UnitySensors / UnitySensorsROS + ROS2 Humble。
- 备选项：Gazebo / Isaac Sim / CARLA。
- 理由：师兄给定方向是 Unity3D，任务重点是越野视觉环境、相机图像和 LiDAR 点云输入，UnitySensors 已提供相机和多类 3D LiDAR 传感器。
- 影响：先完成 Unity-ROS2 传感器闭环，不提前切换到其他仿真器。

## 2026-08-13：采用小步验收工作流

- 决策：通信闭环、相机闭环、点云闭环、越野环境、小车导入依次推进。
- 备选项：一次性导入完整 Unity 项目和大量模型。
- 理由：用户机器环境珍贵，且 Unity/ROS2/传感器通信链条中任一环节失败都会阻断后续工作。
- 影响：每个阶段通过明确验收后再进入下一阶段。

## 2026-08-13：外部资料库放在仓库外

- 决策：官方资料和网页快照放在 `/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY`。
- 备选项：放在仓库 `docs/` 下。
- 理由：用户明确要求资料不要和工作区放一起；外部资料可能体积大、文件多，不应进入 git。
- 影响：仓库内只保留资料索引和结论，原始资料在仓库外。

## 2026-08-13：ROS-TCP-Endpoint 使用独立工作区

- 决策：在 `/home/ubuntu22/VLN/unity_ros2_ws` 独立克隆和构建 ROS-TCP-Endpoint，不放进已有 `~/ws_ros2`。
- 备选项：直接合入已有 ROS2/PX4 工作区，或使用 Docker。
- 理由：保护用户已有成熟 ROS2/PX4 环境；当前 endpoint 依赖简单，宿主机普通 colcon 构建已通过。
- 影响：后续 Unity-ROS2 通信实验只 source `/home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash`，不污染已有工作区。

## 2026-08-13：Unity Hub 采用用户级菜单入口

- 决策：不做系统级 apt 安装，创建 `/home/ubuntu22/.local/share/applications/unityhub-vln.desktop` 指向项目内 Unity Hub。
- 备选项：系统级安装 Unity Hub 到 `/usr/bin` 和 `/usr/share/applications`。
- 理由：用户希望应用菜单可见，同时仍要保护系统环境，避免污染全局包管理。
- 影响：应用菜单显示 `Unity Hub (VLN)`；删除该 `.desktop` 文件即可移除入口。

## 2026-08-13：Unity Hub 登录不作为仿真主线长期阻塞点

- 决策：先用统一代理出口修复 Hub 登录；如果 Unity Hub 仍卡在安全检查或 OAuth token 交换，则优先尝试直接用已安装 Unity Editor 创建工程，继续推进 Unity-ROS2 最小闭环。
- 备选项：继续长时间反复点 Hub 登录、或安装系统级 Unity Hub / libsecret 开发包。
- 理由：当前师兄要求的主线是 Unity-ROS2 仿真、相机图像和 LiDAR 点云，不是 Unity 账号体系排障；本地日志显示主因是网络/IP 会话一致性，系统级安装或 `libsecret` 不是直接解法。
- 影响：后续不因 Hub 登录反复卡住而偏离项目目标；任何系统包安装仍必须先获得用户确认。

## 2026-08-13：不把系统级安装作为 Unity Hub 登录首选修复

- 决策：当前不优先重装系统级 Unity Hub；先用严格代理启动、全新 OAuth conversation 和必要时直接 Editor 创建工程推进。
- 备选项：安装官方 `.deb` 到系统路径、继续使用项目内 Hub、绕过 Hub 直接启动 Editor。
- 理由：Unity 服务端错误明确显示 `conversationIp` 与 `userIp` 不一致，这是网络出口问题；系统级安装主要改善桌面集成和凭据保存，不会改变 Unity 服务端看到的公网 IP。
- 影响：避免为非根因引入系统包改动；若后续确认许可证必须依赖 Hub 且严格代理仍失败，再向用户确认是否安装官方系统版。

## 2026-08-13：Unity 主线需要有效许可证

- 决策：不能假设无账号/无许可证也能完整推进 Unity 仿真主线；当前必须解决 Unity 许可证激活，或临时换用可登录账号/已有许可证。
- 备选项：完全绕过 Unity 账号继续做 Unity 工程、只推进 ROS2/文档准备、换账号或恢复当前账号 2FA。
- 理由：本地 `-createProject` 探测失败，Editor 日志显示无有效许可证；官方文档也说明 Unity Personal 通过登录 Unity Hub 自动激活，Hub 是 Personal 许可证激活/归还的唯一方法。
- 影响：Unity-ROS2 相机/点云闭环前必须获得有效 Editor 许可证；账号问题未解决期间只做不依赖 Editor 许可证的准备工作。

## 2026-08-13：Unity 版本固定为 2022.3.62f1 继续主线

- 决策：虽然 Unity Hub 首页推荐安装 Unity 6.5，但当前 VLN 仿真主线继续使用已安装并通过许可证探测的 Unity `2022.3.62f1`。
- 备选项：点击 Hub 首页推荐的 Unity 6.5、改装其他 Unity 版本、继续使用 2022.3 LTS。
- 理由：师兄给定路线是 Unity3D + ROS 接口 + 传感器仿真，当前依赖 ROS-TCP-Connector 与 UnitySensors；2022.3 LTS 已满足 UnitySensors 要求并已完成本机验证，换 Unity 6.5 会引入额外兼容风险和大体积安装。
- 影响：下一步直接创建 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`，导入 ROS-TCP-Connector 与 UnitySensors，不再把时间花在安装 Unity 6.5 上。

## 2026-08-13：Unity 工程入口固定为项目脚本

- 决策：新增并使用 `/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh` 打开正式工程。
- 备选项：从 Hub、任意 Unity 二进制、文件管理器双击 `.unity` 场景或系统级命令打开。
- 理由：当前 Unity Hub、账号数据库、许可证和缓存都被固定在 `/home/ubuntu22/VLN/.unity_user/`，直接从其他入口启动可能使用另一套 XDG 配置，导致看起来“又要登录”或找不到许可证。
- 影响：后续排错和复现统一用该脚本；用户也可从 Hub 的 `项目` 页面添加同一工程，但不要换 Editor 版本。

## 2026-08-13：阶段 3 用 std_msgs/String 做最小闭环

- 决策：Unity-ROS2 最小闭环先使用 `std_msgs/msg/String`，topic 为 `/unity/heartbeat` 和 `/ros2/command`。
- 备选项：直接从相机 `Image` 或 LiDAR `PointCloud2` 开始。
- 理由：师兄当前目标是先跑通 Unity ROS 接口；字符串 topic 能把连接、协议、ROS2 编译符号、endpoint、双向 topic 注册全部验证清楚，风险最低。
- 影响：阶段 3 已完成后，阶段 4 再引入 UnitySensors 和图像消息，阶段 5 再引入点云消息。

## 2026-08-13：UnitySensors 依赖用项目级 UPM 解决

- 决策：为正式 Unity 工程加入 `com.unity.ugui` `1.0.0` 与 `com.unity.test-framework` `1.1.33`，解决 UnitySensors / UnitySensorsROS 导入编译问题。
- 备选项：直接修改 `Library/PackageCache` 中的 UnitySensors 包、删除包内 Tests、安装系统依赖或更换 Unity 版本。
- 理由：UPM 依赖是 Unity 工程内可复现配置；PackageCache 是生成缓存，不应直接改；系统安装和 Unity 版本切换都会偏离当前已验证路线。
- 影响：`Packages/manifest.json` 和 `Packages/packages-lock.json` 成为阶段 4 复现源；不污染系统 ROS2、CUDA、PyTorch 或 Python 环境。

## 2026-08-13：阶段 4 固定前向 RGB 相机最小规格

- 决策：相机最小闭环固定为 `/vln/front/image_raw`，类型 `sensor_msgs/msg/Image`，640x480，`rgb8`，`front_camera_optical_frame`，约 5Hz；同时发布 `/vln/front/camera_info`。
- 备选项：直接上 1280x720/20Hz、多相机、全景相机或压缩图像。
- 理由：当前重点是跑通师兄要求的“感知层图像输入”，低负载标准消息最容易验收，也适合 RTX 5060 8GB 显存逐步加压。
- 影响：阶段 4 已完成后，阶段 5 可以沿用同样命名风格做 `/vln/front/points` 或 `/vln/lidar/points` 的 `PointCloud2` 闭环；后续提高分辨率或帧率必须先有基线对比。

## 2026-08-13：阶段 5 固定低负载 VLP-16 点云基线

- 决策：LiDAR 最小闭环固定为 `/vln/lidar/points`，类型 `sensor_msgs/msg/PointCloud2`，`lidar_link`，UnitySensors VLP-16 scan pattern，7200 点/帧，约 5Hz。
- 备选项：直接使用 VLP-16 默认更高点数、高频 LiDAR、多 LiDAR、Livox/Mid360 或外部点云资产导入。
- 理由：当前目标是先跑通师兄要求的“雷达点云输入”，RTX 5060 Laptop 8GB 显存需要低负载基线；ROS-TCP bridge 对图像和点云流量敏感，先稳定再加压。
- 影响：阶段 6 越野 terrain 必须沿用该点云基线做回归；只有在相机和点云闭环都稳定后，才提高点云密度、频率或导入大型环境资产。

## 2026-08-14：阶段 6 先使用程序化轻量网格地形

- 决策：阶段 6 越野环境先使用 Unity 工程内程序化生成的轻量网格地形，配合土路、坡、石块、树木、障碍物和静态占位车体，不导入外部大型资产包。
- 备选项：直接导入 Asset Store 越野资产、高清植被森林、真实地形包，或使用 Unity Terrain 组件。
- 理由：师兄当前要求是先跑通 Unity-ROS2 感知输入，不是追求最终视觉资产；用户机器显存 8GB，需要低负载、可复现、可回滚的 baseline。首次尝试 Unity Terrain 组件在阶段 6 batch 图像渲染中触发 Unity 段错误，因此改成更稳定的网格地形。
- 影响：阶段 6 已能验证相机和 LiDAR 在越野语义场景中同时输出；后续要导入真实越野资产时，必须以该轻量场景作为回归基线。

## 2026-08-14：阶段 6 自动验收保留图形上下文

- 决策：`/home/ubuntu22/VLN/scripts/run_offroad_terrain_smoke_test.sh` 使用 Unity `-batchmode`，不使用 `-nographics`。
- 备选项：继续沿用阶段 4/5 的 `-batchmode -nographics`，或只做手工 Unity Play 验收。
- 理由：阶段 6 联合 terrain + RGB 相机场景在 `-nographics` 下会在 UnitySensors `RGBCameraSensor` 的 `Camera.Render` 路径段错误；保留图形上下文后同一场景自动验收通过，并且没有改变 ROS2、CUDA、PyTorch 或系统环境。
- 影响：阶段 6 自动验收会短暂打开 Unity 图形上下文，不能在无显示环境跑；阶段 4/5 原有单独 smoke test 继续按原脚本执行并已回归通过。

## 2026-08-14：阶段 7 先用移动占位车体建立 TF 树

- 决策：阶段 7 暂不导入真实 URDF，先使用程序化移动占位车体发布正式 `/tf`，固定 `map -> base_link -> front_camera_optical_frame,lidar_link`。
- 备选项：立即导入真实小车 URDF、先接 navigation2、继续停留在静态车体。
- 理由：师兄当前目标仍是 Unity 越野场景 + ROS2 感知输入；真实小车模型会引入 URDF、材质、碰撞体、坐标轴、比例尺和控制链路问题，容易在相机/点云闭环还不稳定时扩大排障面。移动占位车体能先验证传感器挂载和 TF 树。
- 影响：阶段 7 已经为后续真实小车替换提供 frame 和 topic baseline；后续导入真实小车时必须保持 `/vln/front/*`、`/vln/lidar/points` 和 `map/base_link/camera/lidar` frame 语义不乱改。

## 2026-08-14：阶段 8 固化标准输出而不是继续加新功能

- 决策：阶段 8 优先固定 topic、frame、RViz 配置、rosbag 小样本记录和启动顺序，新增 `run_standardized_outputs_smoke_test.sh` 作为完整自动验收入口。
- 备选项：直接导入大型越野资产、真实小车模型、导航栈或 VLN/VLA 算法。
- 理由：当前已经能看到场景、图像和点云，但下游算法真正需要的是稳定、可复现、可记录的数据接口。先固化接口可以防止后续资产和模型导入时破坏基础感知链路。
- 影响：阶段 8 后，正式手工 RViz 使用 `/home/ubuntu22/VLN/scripts/view_vln_vehicle_rviz.sh`，rosbag 固定写到 `/home/ubuntu22/VLN/VLN_BAGS`，旧 `view_lidar_rviz.sh` 只作为阶段 5 单 LiDAR 排障工具。
