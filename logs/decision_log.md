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

## 2026-08-14：阶段 7 先用可控占位车体建立 TF 树

- 决策：阶段 7 暂不导入真实 URDF，先使用程序化可控占位车体发布正式 `/tf`，固定 `map -> base_link -> front_camera_optical_frame,lidar_link`。
- 备选项：立即导入真实小车 URDF、先接 navigation2、继续停留在静态车体。
- 理由：师兄当前目标仍是 Unity 越野场景 + ROS2 感知输入；真实小车模型会引入 URDF、材质、碰撞体、坐标轴、比例尺和控制链路问题，容易在相机/点云闭环还不稳定时扩大排障面。可控占位车体能先验证传感器挂载、TF 树和后续控制入口。
- 影响：阶段 7 已经为后续真实小车替换提供 frame 和 topic baseline；后续导入真实小车时必须保持 `/vln/front/*`、`/vln/lidar/points` 和 `map/base_link/camera/lidar` frame 语义不乱改。

## 2026-08-14：阶段 8 固化标准输出而不是继续加新功能

- 决策：阶段 8 优先固定 topic、frame、RViz 配置、rosbag 小样本记录和启动顺序，新增 `run_standardized_outputs_smoke_test.sh` 作为完整自动验收入口。
- 备选项：直接导入大型越野资产、真实小车模型、导航栈或 VLN/VLA 算法。
- 理由：当前已经能看到场景、图像和点云，但下游算法真正需要的是稳定、可复现、可记录的数据接口。先固化接口可以防止后续资产和模型导入时破坏基础感知链路。
- 影响：阶段 8 后，正式手工 RViz 使用 `/home/ubuntu22/VLN/scripts/view_vln_vehicle_rviz.sh`，rosbag 固定写到 `/home/ubuntu22/VLN/VLN_BAGS`，旧 `view_lidar_rviz.sh` 只作为阶段 5 单 LiDAR 排障工具。

## 2026-08-14：阶段 9 先建立 `/vln/cmd_vel` 控制闭环

- 决策：在进入导航、VLN/VLA 决策或真实小车模型导入前，先用标准 `geometry_msgs/msg/Twist` 固定 ROS2 控制入口 `/vln/cmd_vel`。
- 备选项：直接接 navigation2、直接训练 VLN/VLA、直接导入大型小车资产或复杂动力学插件。
- 理由：师兄路线的核心仍是 Unity3D + ROS2 仿真链路。感知输入已经稳定后，下一个最小闭环应证明 ROS2 能反向控制 Unity 车体运动；这比直接上导航栈或训练更小、更可验证，也能提前固定后续决策层的控制接口。
- 影响：后续导航、路径点控制或 VLN 决策模块优先输出 `/vln/cmd_vel`；当前控制模型是轻量运动学，不代表最终真实底盘动力学。当前已改为首次收到指令前保持静止，收到过指令后若 0.75 秒无新指令则停止，防止控制端退出后车体继续运动。

## 2026-08-14：阶段 10 先做轻量路径点控制，不直接接 navigation2

- 决策：先新增 ROS2 侧轻量路径点控制器，读取 `/tf` 并发布 `/vln/cmd_vel`，验证车体能到达相对路径点。
- 备选项：直接接 navigation2、直接做全局地图/代价地图、直接让 VLN 模型输出动作。
- 理由：navigation2 需要地图、定位、代价地图、行为树和参数体系，当前仿真主线刚完成感知、TF 和速度控制，直接接 Nav2 会把问题面扩大。轻量路径点控制器能先验证“TF 反馈 -> 控制计算 -> cmd_vel -> Unity 运动 -> 感知仍正常”的最小闭环。
- 影响：阶段 10 后，后续可以逐步替换控制器为 navigation2 或 VLN 决策模块，但外部接口仍优先保持 `/tf` 输入和 `/vln/cmd_vel` 输出。

## 2026-08-14：Unity Play 后默认静止，控制权交给 ROS2

- 决策：关闭 `VlnVehicleTfPublisher` 的默认自动巡航；Unity 点击 Play 后只发布图像、CameraInfo、点云和 TF，车体在没有 `/vln/cmd_vel` 时保持静止。
- 备选项：继续保留“首次收到控制指令前自动巡航”、完全删除自动巡航代码、或继续让阶段 7/8 自动验收依赖车体移动。
- 理由：后续 VLN、路径点控制和 navigation2 都应该通过 ROS2 输出控制指令；如果 Unity 自己先动，会让用户误以为已经发了目标点，也会污染数据采集、TF 起点和控制闭环判断。
- 影响：阶段 7/8 的验收条件改为“无指令静止 + 感知和 TF 正常”；阶段 9/10 继续负责验证“有 `/vln/cmd_vel` 后车体运动”。保留自动巡航字段但默认 false，必要时可手工打开做演示，不作为主线。

## 2026-08-14：控制面板采用浏览器 UI + ROS2 Python 后端

- 决策：本地控制面板不使用 PyQt、Tkinter 或 Electron，而是用 Python 标准库 HTTP server 提供浏览器中文界面，后端在同一 ROS2 Python 进程中读取 `/tf` 并发布 `/vln/cmd_vel`。
- 备选项：Unity 内嵌 UI、PyQt 桌面 UI、rqt 插件、Electron 应用。
- 理由：浏览器 UI 不需要安装新系统包或 Python 包，能快速做中文输入、步进按钮和触发按钮；ROS2 后端复用已有控制闭环，风险最低。
- 影响：启动方式固定为 `/home/ubuntu22/VLN/scripts/start_vln_control_panel.sh`，默认 URL 为 `http://127.0.0.1:8765/`；相机和雷达按钮只触发已有 rqt/RViz 脚本，不把传感器窗口嵌入网页。

## 2026-08-14：复杂地图和真实小车采用候选场景导入

- 决策：成熟越野地图和真实小车模型进入阶段 12，但不直接替换当前主场景；先登记候选来源、许可证、体积和渲染管线，再导入候选场景测试。
- 备选项：直接下载大型 Asset Store 地图覆盖主场景、直接导入真实小车替换占位车体、继续只用程序化简陋场景。
- 理由：当前基线已经满足师兄最小要求，下一步的风险主要来自大资产、许可证、显存、渲染管线和 frame/TF 破坏。候选场景能保护当前已跑通的通信、图像、点云、TF 和控制闭环。
- 影响：资料记录在 `/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/asset_candidates/index.md`；导入前后必须跑标准输出、cmd_vel 控制和控制面板验收；未确认前不下载或导入大型资产。

## 2026-08-14：阶段 12 第一轮先导复杂地图，不先换真实小车

- 决策：阶段 12 第一轮优先筛选并导入中等复杂度越野地图候选，保留当前可控占位车体、传感器、TF 和 `/vln/cmd_vel` 控制接口。
- 备选项：先导 Husky/Jackal 真实 UGV、同时导地图和小车、继续只优化程序化地图。
- 理由：地图升级直接改善师兄关心的越野环境、相机图像和 LiDAR 点云；保留当前车体能降低问题面。真实小车导入会触碰 URDF/mesh、frame、碰撞体、控制和传感器挂载，应放在地图候选通过回归后再做。
- 影响：下一步先做地图候选筛选和候选场景导入；主场景 `VLNOffroadTerrainSmokeTest.unity` 保留为回归基线。分析文档为 `/home/ubuntu22/VLN/docs/asset_selection_analysis.md`。

## 2026-08-14：阶段 12 第一轮地图候选采用 Kenney Nature Kit 轻量子集

- 决策：第一轮复杂地图候选采用 Kenney Nature Kit 2.1，不直接导入大型 Asset Store/HDRP 森林资产；Unity 工程只导入 70 个 FBX 子集。
- 备选项：Unity Asset Store 大型越野/森林地图、Quaternius Ultimate Nature、Sketchfab 通用地形模型、继续只用程序化地形。
- 理由：Kenney Nature Kit 许可证为 CC0，原始包约 `11M`，模型低多边形，不要求 URP/HDRP，不引入第三方 C# 脚本；非常适合 RTX 5060 Laptop 8GB 显存下做第一轮安全复杂度提升。它能增加树、岩石、灌木、栅栏、木桥和营地等越野语义元素，同时不触碰现有 `/vln/cmd_vel`、相机、LiDAR 和 TF 主链路。
- 影响：新增候选场景 `Assets/VLN/Scenes/VLNOffroadAssetCandidate.unity`，新增自动验收脚本 `/home/ubuntu22/VLN/scripts/run_offroad_asset_candidate_smoke_test.sh`，完整资产升级回归 `/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh` 已扩展为 4 步：候选场景、标准输出、cmd_vel 控制、中文控制面板。下一步进入真实 UGV/URDF 前，继续保留该地图候选作为回归场景。

## 2026-08-14：真实小车第一轮采用 Husky 视觉导入，不直接做完整 URDF 动力学

- 决策：第一轮真实小车候选选择 Clearpath Husky，导入 `.dae` 视觉 mesh 子集；Jackal 已下载但暂缓导入；暂不做完整 URDF Importer 和轮式动力学。
- 备选项：直接导入 Husky 完整 URDF、直接导入 Jackal、同时导入两个小车、继续使用程序化占位车体。
- 理由：Husky 有 `humble-devel` 分支，且关键视觉 mesh 为 `.dae`，更适合 Unity 直接候选导入；Jackal 主要是 `.stl`，更适合后续 URDF/转换路线。当前项目主线是师兄要求的 Unity 越野环境 + 相机图像 + LiDAR 点云，不应在已稳定的 ROS2 topic、TF 和控制接口上引入过多动力学风险。
- 影响：新增候选场景 `Assets/VLN/Scenes/VLNOffroadVehicleCandidate.unity` 和自动验收脚本 `/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh`；完整资产升级回归扩展为 5 步：地图候选、车体候选、标准输出、cmd_vel 控制、中文控制面板。当前真实车体是视觉替换，不代表最终底盘动力学。

## 2026-08-14：Husky 姿态修复限定为视觉网格修正

- 决策：对 Husky 候选使用 `RosYawToUnityRotation()` 加 mesh upright correction 修正“四脚朝天/露底盘”问题，但不改变已稳定的 `/tf`、`/vln/cmd_vel`、相机和 LiDAR rig。
- 备选项：整体翻转 `HuskyVisual_Root`、改传感器 rig、改 `base_link` TF、直接改完整 URDF/动力学导入路线。
- 理由：当前故障是视觉 mesh 姿态问题，不是 ROS2 控制或 TF 语义问题；整体翻转 rig 或 frame 会破坏已经通过的相机、点云和控制闭环。
- 影响：候选车体视觉上正过来，完整回归 `vln_asset_baseline_20260814_101820` 通过；后续如做真实动力学，需要作为单独阶段评估 Unity URDF Importer 和 wheel controller。

## 2026-08-15：阶段 13 单独开启 Scout V2 URDF/STL 物理车体路线

- 决策：根据师兄提供的 `scout_v2.xacro`，阶段 13 第一候选采用 AgileX Scout V2，并把 URDF/STL 物理车体作为独立候选场景推进。
- 备选项：继续在 Husky 视觉候选上手工加碰撞体、直接覆盖现有车体候选、等待师兄返校后的完整小车模型再开始。
- 理由：当前基础仿真链路已经打通，师兄明确给了可公开访问的 Scout V2 xacro 和 mesh；该模型包含 visual、collision、inertial 和 continuous wheel joint，适合作为物理车体流程的第一轮验证。独立阶段可以保护现有相机、LiDAR、TF、`/vln/cmd_vel` 和控制面板链路。
- 影响：新增阶段 13 文档 `/home/ubuntu22/VLN/docs/urdf_physics_vehicle_workflow.md`；本地缓存位于 `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles/ugv_gazebo_sim_scout_description_raw`；后续导入 Unity 时必须新建 Scout URDF 候选场景，不覆盖阶段 12 场景。

## 2026-08-15：阶段 13 第一轮保留现有传感器 rig 和 ROS2 接口

- 决策：Scout URDF 第一轮导入后，仍保留当前 UnitySensors 相机和 LiDAR rig，挂到 `base_link` 语义节点下；`/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points`、`/tf`、`/vln/cmd_vel` 不改名。
- 备选项：完全按 Scout 原始 xacro/gazebo 插件重建传感器和控制、直接把 Gazebo transmission 当作 Unity 控制器、同时新增完整轮式动力学和传感器重构。
- 理由：阶段 13 的第一目标是验证 URDF 物理底盘，不应同时重写已稳定的感知输出和控制入口；Gazebo 插件标签不能直接等价为 Unity 物理控制器。
- 影响：第一轮完成定义聚焦于 URDF 展开、Unity 导入、物理稳定、collision 生效和旧 ROS2 topic 回归；`/vln/odom` 可以作为候选新增输出，`/joint_states` 延后到轮式动力学稳定后再做。

## 2026-08-15：Scout V2 Unity 导入使用 `chosenAxis=yAxis`

- 决策：Scout V2 通过 Unity URDF Importer 导入时固定使用 `ImportSettings.axisType.yAxis`。
- 备选项：使用 `zAxis`、对导入后的整个 robot root 额外旋转、或手工逐个修正 mesh。
- 理由：`zAxis` 会让 Scout 车体竖起；`yAxis` 后导入尺寸约 `0.700 x 0.351 x 0.930m`，截图显示车身平放、四轮竖直贴地。整体额外旋转会增加 TF、collision 和后续 wheel joint 解释成本。
- 影响：`VlnOffroadScoutUrdfCandidateProjectSetup.cs` 中不要随意改回 `zAxis`；后续如果师兄给完整 xacro，应先单独验证坐标轴，而不是沿用假设。

## 2026-08-15：Scout 第一轮控制仍采用现有运动学 rig

- 决策：Scout URDF 第一轮通过后，`/vln/cmd_vel` 仍驱动当前已验证的 `VlnVehicleTfPublisher` rig，Scout URDF 车体作为 rig 子对象随车运动；暂不在同一轮把 `/vln/cmd_vel` 直接映射到 wheel joint / ArticulationBody。
- 备选项：立即实现四轮差速/滑移控制、直接驱动 ArticulationBody、等待完整真实车体模型后再做控制。
- 理由：本阶段主目标是按师兄要求跑通真实底盘 URDF 结构、相机图像和 LiDAR 点云，不应同时引入轮胎摩擦、悬挂、地面接触和电机控制问题。保留运动学 rig 可以证明旧 ROS2 感知/TF/控制接口不被 URDF 导入破坏。
- 影响：当前 Scout 候选满足“URDF 物理结构 + ROS2 感知/控制接口稳定”的阶段目标，但还不是论文级完整车辆动力学；下一阶段应单独做 wheel joint 控制、`/vln/odom` 和可选 `/joint_states`。

## 2026-08-15：Unity UPM 缓存固定在项目目录

- 决策：在 `scripts/open_unity_vln_project.sh` 中显式设置 `UPM_CACHE_PATH`、`UPM_GIT_LFS_CACHE_PATH`、`UPM_NPM_CACHE_PATH` 到 `/home/ubuntu22/VLN/.unity_user/cache/upm`。
- 备选项：使用 Unity 默认 `/home/ubuntu22/.config/unity3d/cache`，或修改系统级 Unity/UPM 配置。
- 理由：当前项目要求所有工作目录在 `/home/ubuntu22/VLN` 内，且执行环境可能无法写用户 home 默认 Unity cache；固定项目内缓存可复现、可清理，也不污染系统环境。
- 影响：后续 Unity 工程打开和批处理必须通过 `scripts/open_unity_vln_project.sh`；不要绕过该脚本直接调用 Unity 二进制做包导入。

## 2026-08-15：Scout URDF 采用 Editor 导入路径，不采用 Runtime 导入

- 决策：Scout V2 URDF 第一轮使用 `UrdfRobotExtensions.Create(... forceRuntimeMode:false)` 的 Editor 导入路径，不使用 `UrdfRobotExtensions.CreateRuntime`。
- 备选项：继续调 runtime 导入、安装/链接 Assimp 相关系统库、手工把 DAE 转成其他格式、完全绕开 URDF Importer。
- 理由：runtime 路径触发 Assimp `DllNotFoundException: libdl.so`，会把问题引向系统库和 Unity 动态加载；Editor 导入路径已经能稳定实例化 DAE、生成 collision asset，并通过 Scout 静态、控制和完整基线回归。用户环境保护优先级高，不应为了非阻塞日志去装系统库。
- 影响：后续 Scout 或师兄完整车体的 URDF 导入优先走 Editor 资产导入流程；若必须做运行时动态导入，需要单独立项并先确认环境修改风险。

## 2026-08-15：阶段 13 收口为“URDF 结构闭环”，动力学另开增强阶段

- 决策：阶段 13 当前收口标准是 Scout URDF visual/collision/inertial/joint 结构导入成功，并保持 `/vln/front/*`、`/vln/lidar/points`、`/tf`、`/vln/cmd_vel` 主接口稳定；完整轮胎-地面摩擦、悬挂、电机、`/vln/odom` 和 `/joint_states` 放到下一阶段。
- 备选项：在同一轮继续改为 wheel joint / ArticulationBody 真实驱动，或者直接把当前 rig 宣称为完整物理底盘。
- 理由：当前已满足师兄给定的“导入小车模型、加入相机和雷达、跑通 ROS2 接口”的第一轮要求；真实动力学需要单独处理轮胎接触、摩擦参数、质量/惯性、控制器稳定性和 odom 语义，和 URDF 导入混在一起会扩大风险。
- 影响：给师兄汇报时应表述为“Scout URDF 物理结构候选已打通，ROS2 感知/控制链路未破坏”；不要表述为“完整真实车辆动力学已完成”。下一阶段再做 wheel joint 控制和 odom。

## 2026-08-15：Scout wheel joint 先做信号探针，不直接切换整车动力学

- 决策：新增 `VlnScoutWheelJointCommandProbe`，把 `/vln/cmd_vel` 映射为四个 Scout wheel ArticulationBody 的 `xDrive.targetVelocity`，但暂时仍由现有 `VlnVehicleTfPublisher` 运动学 rig 负责整车位移。
- 备选项：立即取消运动学 rig、打开重力和地面接触，让四轮 ArticulationBody 直接驱动车体；或者完全不接 wheel joint。
- 理由：直接切换到真实轮胎-地面动力学会同时引入摩擦、接触稳定性、质量/惯性、joint 方向和 odom 语义问题，风险过大。先做信号探针能验证 ROS2 控制链路已经打到真实 URDF wheel joint，且不破坏已通过的图像、点云、TF 和 `/vln/cmd_vel` 回归。
- 影响：当前结果可汇报为“`/vln/cmd_vel` 已能进入 Scout wheel joint drive 信号层”，但仍不能宣称“轮胎物理驱动车体已完成”。下一步应新建或扩展候选实验，让 wheel-ground 接触驱动整车，并保持已新增的 `/vln/odom` 接口稳定。

## 2026-08-15：Scout 候选先新增基于 rig 位姿的 `/vln/odom`

- 决策：在 Scout URDF 候选场景中新增 `VlnOdomPublisher`，发布 `/vln/odom [nav_msgs/msg/Odometry]`，frame 为 `map`，child frame 为 `base_link`；odom 来自当前 Unity rig 的实际位姿差分，不直接等待 wheel-ground 真实动力学完成。
- 备选项：等真实轮地动力学完成后再发布 odom；或直接把 wheel joint 物理仿真结果作为唯一 odom 来源。
- 理由：后续动力学、导航和 VLN 控制都需要稳定 odom 观测接口。先在已验证 rig 上固定消息类型、frame、验收脚本和回归口径，可以降低下一步替换动力学源时的接口风险。
- 影响：当前可汇报为“Scout 候选已新增 `/vln/odom` 并与 TF 运动一致”，但必须说明它仍是 rig 位姿 odom，不是完整轮胎-地面物理 odom。下一阶段替换驱动源时应保持 `/vln/odom` 接口不变。

## 2026-08-15：ROS2 日志强制写项目内 `.ros/log`

- 决策：所有 ROS2 自动验收、手工查看、endpoint 和控制面板脚本统一导出 `ROS_LOG_DIR=/home/ubuntu22/VLN/.ros/log`。
- 备选项：使用 ROS2 默认 `/home/ubuntu22/.ros/log`；或修改系统权限/全局 shell 配置。
- 理由：项目约束要求工作目录都在 `/home/ubuntu22/VLN` 内；当前执行环境写 home 默认 ROS 日志目录会失败。脚本内显式导出变量可复现、可回滚，不污染全局环境。
- 影响：后续新增 ROS2 脚本必须沿用该日志目录；旧完整回归已重新通过 `vln_asset_baseline_20260815_193207`。

## 2026-08-15：阶段 14 第一版 wheel-ground 采用独立 `Rigidbody + WheelCollider` 候选

- 决策：新建 `VLNOffroadScoutWheelGroundCandidate.unity`，用独立 `ScoutWheelGround_PhysicsRoot` 承载 `Rigidbody`、chassis `BoxCollider` 和 4 个 `WheelCollider`；Scout URDF mesh 只作为视觉模型，旧 Scout URDF 候选场景保持不变。
- 备选项：直接在 `VLNOffroadScoutUrdfCandidate.unity` 上打开 ArticulationBody 重力和 wheel joint drive，让原始 URDF articulation 立即接管整车运动；或继续沿用运动学 rig。
- 理由：Unity URDF Importer 的 ArticulationBody 轮地动力学会同时引入坐标轴、joint 轴、接触稳定、摩擦、质量/惯性和传感器 rig 跟随等风险；直接覆盖旧场景容易破坏已经通过的相机、LiDAR、TF、cmd_vel 和 odom。先用 WheelCollider 做独立候选可以验证“cmd_vel -> 轮地接触 -> 物理车体前进 -> TF/odom/传感器跟随”的最小闭环。
- 影响：阶段 14 已通过 `vln_scout_wheel_ground_20260815_195417`，但只能称为第一版 wheel-ground 物理候选，不是最终论文级 Scout 动力学标定。后续应在该候选上继续验证差速转向、坡地、碰撞、轮胎参数、质量/惯性和 `/joint_states`，不要把阶段 13 的 URDF 结构候选删除或覆盖。

## 2026-08-16：阶段 15 默认路线收敛为稳定短路线

- 决策：新增 Scout wheel-ground 固定路线脚本，默认路线采用相对 `base_link` 前向 `3,0;6,0;9,0` 米，并固化为 `max_linear=0.45`、`max_angular=0.18`、`linear_gain=0.30`、`angular_gain=0.35`、`angular-sign=-1` 的低速小角度回正巡航。20m 长路线保留为实验覆盖，不作为默认验收。
- 备选项：继续把 `4,0;8,0;12,0;16,0;20,0` 当默认路线；直接做复杂路径点纠偏、绕障转向、接 navigation2；或继续只让用户手工控制。
- 理由：当前第一版 `WheelCollider + Rigidbody` skid-steer 车体在长路线第三段后会随机横向漂移到右侧障碍/不稳定地形区，在复杂高速转向时又容易横摆和侧滑；用户当前需求优先是观察轮地接触、坡地/路面交互、传感器跟随和穿模风险。稳定短路线可复现，并且不覆盖手动控制、控制面板、相机、LiDAR、TF 和 odom 主链路。
- 影响：阶段 15 可以作为物理演示和回归入口，短路线最新通过 run id `vln_scout_wheel_ground_route_20260816_041954`；但不能汇报成完整绕障导航或长路线已稳定。后续如要真正绕障或复杂坡地路线，需先单独标定低速转向、横向摩擦、碰撞边界和差速控制。

## 2026-08-16：阶段 15 默认路线升级为 54m 完整路线演示

- 决策：废弃“默认 9m 短路线即完成”的口径，阶段 15 默认路线改为 `4,0;8,0;12,0;15,0;18,0;22,0;28,0;34,0;42,0;50,0;54,0`，让小车从起点沿道路通过桥/坡区域并跑向终点方向。
- 备选项：继续保留 9m 短路线作为默认验收、直接接 navigation2、继续手工控制、或者只提高速度不改桥面碰撞体。
- 理由：用户明确要观察完整路线、桥、坡和真实物理交互；短路线虽然稳定，但偏离学长项目目的。长路线原先卡在前向约 `13.7m`，不是单纯导航问题，而是桥面简化碰撞体硬边阻挡 WheelCollider；因此必须修桥面物理过渡，而不是继续压低速度或缩短路线。
- 影响：阶段 15 当前完成定义变为完整路线自动验收通过：`vln_scout_wheel_ground_route_20260816_150349`，`reached_count=11/11`、`total_forward_progress=53.080m`、`stall_count=0`、`skipped_count=0`。控制器速度上限和助推参数已提高，Scout 视觉朝向与轮胎视觉偏移已修正。该阶段仍只代表写死路线物理巡航，不代表自主导航或 VLN 策略已经接入。

## 2026-08-16：已撤销方案：宽泛连续隐形物理路面

- 决策：该方案已撤销，不再作为当前路线。旧做法是在 Scout wheel-ground 候选场景中让 `Offroad_DirtRoad_*` 和 `Offroad_ShortRamp` 只保留视觉渲染，并新增宽泛连续隐形物理路面 `ScoutWheelGround_PhysicalTrailSurface_*` 作为车辆真实接触面。
- 备选项：继续使用每块视觉路面自带 BoxCollider、仅提高电机扭矩/速度、把所有路面做成完全平板、或者直接等待更真实地图资产。
- 理由：当时该方案能绕过小缝和坡口卡车问题，但用户指出它会让车在视觉上的独木桥、台阶、半坡处像穿过去一样平走，车轮真实接触面与肉眼可见地形不一致。这会偏离“真实物理链路”的主线，不能作为正式修复。
- 影响：`vln_scout_wheel_ground_route_20260816_153103` 和 `vln_scout_wheel_ground_20260816_153542` 只作为错误中间方案记录，不作为当前完成标准；后续任何正式验收必须要求 `broad_physical_trail_count=0`。

## 2026-08-16：阶段 15 改为可见局部物理通行面

- 决策：撤销 `ScoutWheelGround_PhysicalTrailSurface_*` 宽泛连续隐形路面，改为在可见道路、块间过渡、桥面/桥头坡、短坡处生成局部可见物理体：`ScoutWheelGround_PhysicalRoadSlab_*`、`ScoutWheelGround_PhysicalRoadSeam_*`、`ScoutWheelGround_PhysicalBridge*` 和 `ScoutWheelGround_PhysicalShortRamp*`。
- 备选项：回退到原始碎块 collider、继续使用宽泛隐形路面、只靠加大电机扭矩、或者等待更真实地图资产后再处理。
- 理由：真实越野仿真可以简化碰撞几何，但不能让接触面与用户看到的桥、坡、路面脱节。局部可见物理体能去掉 Unity `WheelCollider` 对小缝硬边的非现实敏感性，同时让小车确实压在可见路面、桥面和坡面上。
- 影响：完整路线默认验收更新为 `vln_scout_wheel_ground_route_20260816_165241`：`reached_count=11/11`、`total_forward_progress=53.118m`、`final_lateral_offset=-0.000m`、`max_reached_cross_track=0.002m`、`stall_count=0`、`skipped_count=0`、`broad_physical_trail_count=0`。基础 wheel-ground 回归更新为 `vln_scout_wheel_ground_20260816_164023`。后续若再修卡点，必须优先修真实/可见局部 collider、轮胎参数和控制器，禁止再用宽泛隐形平路掩盖问题。

## 2026-08-16：阶段 15 中间方案：可见加宽路肩和物理稳定控制

- 决策：道路/桥面局部物理体曾统一渲染为可见 `8.0m` 宽通行面；`VlnScoutWheelGroundController` 在 wheel torque 基础上加入 yaw assist 与 lateral damping，通过 `Rigidbody.AddTorque/AddForce` 施加物理力/力矩。
- 备选项：放宽 route gate、启用跳点、恢复宽泛隐形平路、继续只调路径控制器、直接等待师兄完整车辆参数。
- 理由：严格复跑证明撤销隐形路面后，车辆仍会在桥后/中段因为横向漂移和转向响应不足卡住。放宽 gate 或跳点会掩盖失败；恢复隐形平路偏离主线。当前缺少真实轮胎/悬挂/电机参数，物理力/力矩稳定项是对简化 WheelCollider 轮胎侧向阻尼和差速转向响应的透明近似。
- 影响：该中间方案曾通过 `vln_scout_wheel_ground_route_20260816_172640`：`reached_count=11/11`、`total_forward_progress=53.049m`、`stall_count=0`、`skipped_count=0`、`broad_physical_trail_count=0`。但它已经被后续 `2026-08-16：阶段 15 收窄物理通行面并增加接触审计` 替换，不再作为当前完成标准。

## 2026-08-16：阶段 15 收窄物理通行面并增加接触审计

- 决策：`8.0m` 宽桥/路可见通行面不再作为当前完成标准。当前主路物理 slab 设计宽度为 `6.2m`，桥面物理宽度为 `2.25m`，短坡改为连续可见 `ScoutWheelGround_PhysicalShortRampContinuous` MeshCollider，路面 slab 在桥区和短坡区让开。
- 备选项：继续使用 8m 可见通行面、回退宽泛隐形平路、只靠提高电机扭矩、或等待师兄完整车辆参数。
- 理由：用户指出 8m 方案虽然不是隐藏面，但仍像把独木桥、台阶和半坡的难点抹平，视觉上会让车像穿模/平走；这和当前“完整物理真实链路”的主线冲突。收窄桥面和移除桥/坡托底后，必须用接触审计证明车轮实际压过桥和短坡。
- 影响：当前完成标准更新为 `vln_scout_wheel_ground_route_20260816_181247`：`reached_count=13/13`、`total_forward_progress=52.920m`、`final_lateral_offset=-0.761m`、`max_reached_cross_track=0.741m`、`stall_count=0`、`skipped_count=0`、`road_physical_max_width_m=6.939`、`bridge_physical_max_width_m=2.250`、`short_ramp_physical_max_width_m=4.800`、`bridge_contact_steps=1937`、`short_ramp_contact_steps=1569`、`wheel_ground_height_span_m=0.369`。基础回归更新为 `vln_scout_wheel_ground_20260816_181841`。

## 2026-08-16：验收日志必须归档当前结果文件

- 决策：`run_scout_wheel_ground_route_smoke_test.sh` 和 `run_scout_wheel_ground_smoke_test.sh` 在每次运行结束后，除移动旧结果到 `previous_*` 外，还必须把当前 Unity/ROS 结果文件复制进本次 `_SmokeTestLogs/<run_id>/`。
- 备选项：继续只在 `run_summary.txt` 中记录当前指标，或继续只保留 `previous_*` 旧结果文件。
- 理由：只保留 `previous_*` 会让排障时误读旧失败文件，和 `run_summary.txt` 的当前通过结果冲突，影响后续复盘。
- 影响：后续看某次验收时，优先读该目录下无 `previous_` 前缀的当前结果文件；`previous_*` 只表示运行前从工程 `Logs/` 中移出的残留结果。

## 2026-08-16：轮胎视觉旋转采用累计滚动角

- 决策：`VlnScoutWheelGroundController` 中视觉轮 mesh 的旋转不再直接使用 `WheelCollider.GetWorldPose()` 返回的 rotation，而是用 wheel rpm / 车体速度估算角速度，再沿本地 X 轴累计滚动角显示。
- 备选项：继续使用 `GetWorldPose` 全量姿态；只隐藏轮胎旋转；或直接用固定动画速度硬转。
- 理由：`GetWorldPose` 的 rotation 更适合 WheelCollider 调试，不适合 Scout 低多边形轮胎 mesh 的直观滚动展示；它会把接触求解和局部姿态扰动反映成前后摆。累计滚动角能保持连续 360 度滚动，同时仍由真实 wheel rpm / 车体速度驱动，不是纯装饰假动画。
- 影响：自动验收新增 `wheel_visual_total_abs_roll_deg` 和 `wheel_visual_direction_reversal_count`，并在脚本中限制反转次数。后续如果改轮胎半径、轮向或 mesh 坐标轴，必须同时检查这个视觉滚动逻辑。

## 2026-08-16：独木桥只保留一个可见且有碰撞的桥面

- 决策：删除旧 Kenney 可见木桥对象，不再让旧视觉桥和新物理桥面同时存在；`ScoutWheelGround_PhysicalBridgeDeck` 同时承担 renderer 和 collider，左右栏杆仅用于视觉边界，不参与托底。
- 备选项：保留 Kenney 原桥但删 collider；保留原桥并在下方放物理桥；或用隐藏 collider 托住车辆。
- 理由：视觉桥和物理桥分离会让用户看到“车穿过桥”的效果，即使真实 collider 上有接触也无法解释。一个对象同时可见和可碰撞，且 renderer/collider 顶面对齐，是当前阶段最可验收的物理真实链路。
- 影响：验收脚本强制 `decorative_bridge_renderer_count=0`、`bridge_deck_has_renderer=1`、`bridge_deck_has_collider=1`、`bridge_deck_renderer_collider_top_delta_m<=0.01`。后续导入更真实地图资产时，也应遵循“可见接触面和物理接触面一致”的原则。

## 2026-08-16：阶段 15 增加非扁平地形审计和桥/坡截图证据

- 决策：完整路线验收不仅要求小车通过，还必须证明桥和短坡没有被压平或隐藏托底。Unity 结果文件写入 `terrain_geometry_policy=visible_local_physics_no_flattening_no_hidden_bypass`，路线脚本强制归档桥区截图和短坡截图。
- 备选项：只保留通过路线的数值指标；或继续依赖人工打开 Unity 肉眼检查；或降低桥/坡高度来提高通过率。
- 理由：用户已经明确指出“桥/斜坡越来越扁平”是偏离真实物理链路的风险。单纯 `reached_count=13/13` 不能证明没有作弊；必须同时检查地形宽度、高度跨度、接触对象和视觉证据。
- 影响：当前完成标准新增 `bridge_physical_height_span_m>=0.20`、`short_ramp_physical_height_span_m>=0.62`、`bridge_visual_detail_count>=40`、桥/坡截图文件存在。最新正式验收 run id 为 `vln_scout_wheel_ground_route_20260816_215127`，桥区和短坡截图均归档在对应 `_SmokeTestLogs` 目录。后续调参时不能用压平桥面、压平短坡、恢复隐藏托底或恢复道路宽桥面来换取通过。

## 2026-08-17：阶段 16 采用手动示教记录与回放

- 决策：在中文控制面板中新增“速度控制”模块，让用户用键盘手动驾驶 Scout wheel-ground 物理车体，满意后导出 `/vln/cmd_vel` 速度序列 JSON，再由回放脚本复现。
- 备选项：继续硬调固定路线控制器、直接接 navigation2、或继续手工但不保存速度数据。
- 理由：当前场景和车体已经具备真实轮地接触、桥/坡接触审计和传感器链路；继续把自动路线控制调到“看起来不 S 弯”会混合控制器问题与物理问题。人工示教能先得到用户认可的真实通过路线，并保留为可复现的速度数据，后续再逐步升级成闭环导航或 VLN 控制。
- 影响：控制面板顶部变为“目标位置 / 速度控制 / 相机视图 / 雷达点云”。键位固定为 `↑` 正线速度、`↓` 负线速度、`←/A` 正 `angular.z` 左转、`→/D` 负 `angular.z` 右转。导出记录写入 `/home/ubuntu22/VLN/VLN_RECORDINGS/manual_drives`，该目录不提交 git；回放入口为 `scripts/replay_manual_drive_recording.sh --file <json>`。

## 2026-08-17：手动速度控制以 Unity 实测方向和闭环响应为准

- 决策：阶段 16 的手动速度控制不再沿用旧固定路线脚本的角速度经验，而是以当前 `VLNOffroadScoutWheelGroundCandidate.unity` 中 Scout wheel-ground 场景实测行为为准：`↑` 发布正 `linear.x` 前进，`↓` 发布负 `linear.x` 后退，`←/A` 发布正 `angular.z` 且视觉左转，`→/D` 发布负 `angular.z` 且视觉右转。
- 备选项：继续用 WheelCollider 差速电机承担纯转向、只靠开环键盘速度、或为了直行/转向效果去改地图几何和碰撞体。
- 理由：纯转向使用轮端差速电机会在当前 WheelCollider/简化轮胎约束下产生大平移；只用 AddTorque 又会被轮地约束抵消。用户操作看的是 Unity 里车是否前进、直行、左转和右转，因此低层控制器必须把 `/cmd_vel` 解释成底盘速度伺服，同时保留碰撞、轮地接触、传感器跟随和桥/坡物理审计。
- 影响：当时 `scripts/vln_control_panel.py` 默认 `publish-rate=100Hz`、`manual-command-timeout=0.18s`、`manual-left-angular-sign=+1`；`VlnScoutWheelGroundController` 使用 `m_WheelAngularMotorScale=0`，并保留 yaw-rate PID + Rigidbody 角速度伺服。2026-08-18 已因真实浏览器体感问题将 fallback 超时改为 `0.35s` 并加入请求背压/序号过滤。

## 2026-08-17：旧固定路线控制器暂不作为阶段 16 完成标准

- 决策：手动控制修复后，旧 `run_scout_wheel_ground_route_smoke_test.sh` 固定路线控制器不再作为当前阶段 16 的完成标准；后续若需要自动路线，应单独开启“低速闭环路线跟踪 / 示教回放升级”阶段。
- 备选项：继续硬调旧固定路线参数直到通过；放宽 gate、跳点、恢复宽物理通行面；或者先把手动示教数据回放稳定再做自动闭环。
- 理由：旧固定路线脚本已经暴露 S 型、桥区横漂和栏杆碰撞边界问题。继续硬调容易再次诱导压平桥/坡、隐藏托底或放宽验收，偏离用户要求的真实物理链路。阶段 16 当前真正要解决的是“人能用键盘稳定操控真实物理车体，并导出可回放速度序列”。
- 影响：固定路线失败必须如实记录，不包装成通过；下一阶段应优先做示教 JSON 回放稳定复现，再考虑基于 `/tf`、`/vln/odom` 和路点的低速闭环路线跟踪。

## 2026-08-17：阶段 15 自动路线恢复为演示主线

- 决策：在用户要求老师演示视频前，优先恢复阶段 15 固定完整路线自动巡航；路线控制器统一使用 `angular-sign=1`，与当前手动速度控制和 Unity wheel-ground 底层“正 `angular.z` 左转”的约定一致。
- 备选项：继续把固定路线标为阶段 16 外的失败旧分支、用示教回放代替自动路线、或者通过放宽路线 gate/跳点/改桥坡几何让路线通过。
- 理由：复现显示失败根因是控制符号不同步，不是桥/坡物理几何必须再改；只改 `angular-sign` 能在不作弊、不降验收、不改地形的情况下恢复 13/13 完整路线。
- 影响：`run_scout_wheel_ground_route_smoke_test.sh`、`drive_scout_wheel_ground_route_demo.sh`、`ros2_drive_scout_physics_route.py` 默认都必须保持 `angular-sign=1`。该决策首次恢复通过 run id 为 `vln_scout_wheel_ground_route_20260817_125552`；PBR 材质升级后的当前最新 13 点金标准回归为 `vln_scout_wheel_ground_route_20260817_183540`。

## 2026-08-17：速度控制验收覆盖方向键和 A/D 两套输入

- 决策：`run_control_panel_manual_velocity_unity_smoke_test.sh` 的客户端不仅检查 A/D 左右转，还必须检查方向键 `←/→` 左右转；两套按键都要满足左转正 `angular.z`、右转负 `angular.z`、纯转向平移很小、停车漂移很小。
- 备选项：只测 A/D、只做后端 HTTP 静态检查、或只靠人工手感判断。
- 理由：用户明确反馈“往左/往右乱走”，实际使用时很可能按的是方向键；只测 A/D 会留下验收盲区。
- 影响：最新速度控制 Unity 联动 run id `vln_control_panel_manual_velocity_unity_20260817_130258` 已通过，后续若改控制面板键位、默认速度、底层 yaw PID 或发布频率，都必须复跑该脚本。

## 2026-08-17：上下文读取机制改为分级启动

- 决策：不再要求每次继续工作都全量阅读 `PROJECT_MEMORY.md`、`workflow.md`、`env.md` 和 `logs/issue_log.md`。新增 `CURRENT_STATE.md` 作为短上下文入口；每次默认先读 `AGENTS.md` + `CURRENT_STATE.md`，再按任务定向读取相关小节。
- 备选项：继续每次全量读取所有长文档；完全取消记忆机制；或只依赖模型压缩摘要。
- 理由：项目历史已经很长，全量读取会浪费时间和上下文，还容易把旧阶段失败方案重新带入当前子任务。短状态文件能保留当前基线和硬约束，长日志仍用于换阶段、排障、环境变更和历史追溯。
- 影响：质量要求不降低。涉及自动路线金标准、物理真实性、环境安全、安装下载、阶段切换或失败排障时，仍必须定向查长文档并更新对应记录。

## 2026-08-17：后段挑战场地作为扩展路线，不覆盖旧自动路线基线

- 决策：新增草地、青石路、沙地和低矮可越障碍时，不直接替换旧 13 点自动路线完成标准，而是新增 `run_scout_wheel_ground_challenge_route_smoke_test.sh` 做 16 点扩展路线验收；旧 `run_scout_wheel_ground_route_smoke_test.sh` 默认仍作为老师演示金标准回归。
- 备选项：直接把旧路线延长并替换金标准、只做场景视觉新增不做自动路线验收、用手动示教回放代替自主路线。
- 理由：用户明确要求不要把之前验收好的独木桥、斜坡和自动路线改坏，同时演示仍要自主运行。独立扩展脚本能证明新增场地可自主通过，又能让旧 13 点路线继续一键回归。
- 影响：新增对象统一使用 `ScoutWheelGround_ChallengeSurface_*`、`ScoutWheelGround_ChallengeObstacle_*`、`ScoutWheelGround_ChallengeMarker_*` 前缀；验收脚本新增挑战对象计数、挑战接触步数和挑战截图检查。该分散布局首次稳定通过 run id `vln_scout_wheel_ground_challenge_route_20260817_144908`，PBR 材质升级后的当前最新扩展路线为 `vln_scout_wheel_ground_challenge_route_20260817_182912`，旧基线当前最新回归为 `vln_scout_wheel_ground_route_20260817_183540`。

## 2026-08-17：阶段 18 挑战区从末端堆叠改为分散布局

- 决策：草地、青石路、沙地不再全部挤在最后一块地；改为利用斜坡后的连续大空间分散布置，草地区约 `z=10.0..16.8`，青石路约 `z=20.0..28.0`，沙地区约 `z=32.0..49.0`，终点挡墙后移到 `z=53.5m`。
- 备选项：继续在末端小范围堆叠三块区域；只把终点墙后移但不改三段分布；直接引入大型外部场景资产。
- 理由：用户明确指出斜坡后有大空间，三段场地挤在最后不符合观察和演示需求；当前阶段仍应保护旧桥/坡和路线基线，所以优先用现有程序化低模生成器重做分布和细节，而不是立即引入不可控大资产。
- 影响：挑战路线仍是独立 16 点扩展路线，不替代旧 13 点金标准；视觉细节必须体现草地、青石路、沙地的形态特征，物理障碍必须有接触但可通过。该布局首次通过 run id `vln_scout_wheel_ground_challenge_route_20260817_144908`；PBR 材质升级后的当前最新扩展路线为 `vln_scout_wheel_ground_challenge_route_20260817_182912`，旧 13 点路线当前最新为 `vln_scout_wheel_ground_route_20260817_183540`。

## 2026-08-17：挑战障碍采用低矮扰动，不采用硬阻挡

- 决策：新增障碍只做低矮石纹凸条、偏置低横木、沙地波纹和两侧导向石；它们必须可见、有 collider、能产生轮胎接触统计，但不能形成不可越过的横向硬墙。
- 备选项：放置高石块/横木让车强行越障、只改材质不加物理扰动、铺隐藏平滑托底面保证通过。
- 理由：项目主线是“真实物理链路”，既需要看出车受地形影响，也不能为了演示把车卡死或用隐藏几何作弊。第一版横向低横木/凸条组合已证明过强障碍会导致最后路径点前停滞，因此改成低矮扰动更符合当前阶段。
- 影响：挑战路线要求 `challenge_surface_contact_steps>0` 和 `challenge_obstacle_contact_steps>0`，同时 `stall_count=0`、`skipped_count=0`。后续继续加障碍时先小幅增加难度并跑扩展路线，不要一次性放大障碍高度或恢复隐藏托底。

## 2026-08-17：用户手工演示流程优先于一键 batch 验收

- 决策：用户平时看效果时，默认流程是先打开 Unity 软件、进入场景、启动 ROS-TCP-Endpoint、点击 Play，再运行 `drive_*_demo.sh` 发布路线；`run_*_smoke_test.sh` 只作为我排查或改代码后的自动回归验收入口。
- 备选项：继续把一键 batch 验收命令作为文档首选入口；或只保留手工流程、删除自动验收入口。
- 理由：用户需要在 Unity 界面里亲自观察小车、相机、LiDAR、桥/坡和新增挑战场地的实时效果；batch 验收虽然可靠，但会自动打开 Unity 并隐藏主要观察过程，不符合演示和人工确认习惯。
- 影响：新增 `scripts/drive_scout_wheel_ground_challenge_route_demo.sh`；`user.md` 已改为手工流程优先；`run_*_smoke_test.sh` 输出提示自己是自动回归入口。后续给用户操作步骤时，除非明确说“自动验收/回归/你自己跑测试”，否则优先给 `open_unity_vln_project.sh` + `start_ros_tcp_endpoint.sh` + Unity Play + `drive_*_demo.sh` 的顺序。

## 2026-08-17：Unity 菜单只封装脚本，不内置导航控制

- 决策：新增 Unity Editor 面板和菜单按钮，用于启动 endpoint、13 点路线、16 点挑战路线、相机、RViz 和中文控制面板；按钮只调用现有 shell 脚本，实际控制仍由 ROS2 节点发布 `/vln/cmd_vel`。
- 备选项：把固定路线控制器重写成 Unity C# 组件，或者继续完全依赖终端命令。
- 理由：用户希望像普通 Unity 软件一样在界面里点按钮运行，但师兄路线要求 Unity3D 提供 ROS 接口和仿真传感器，控制链路应保持在 ROS2 外部，方便后续接 VLN/Nav2/VLA。
- 影响：新增 `Assets/VLN/Editor/VlnManualDemoLauncherWindow.cs` 和 `scripts/unity_menu_launch.sh`。Unity 菜单是操作便利层，不是新的控制架构；后续排障仍以现有 shell 脚本和 ROS2 topic 为准。

## 2026-08-17：挑战区视觉升级采用程序化低模层，不改物理主链路

- 决策：针对用户反馈“草地不像草、沙石地粗糙”，先用程序化低模视觉层升级现有挑战区：草地为 3 层草叶 mesh，青石路为不规则铺石、暗缝、裂纹和碎石视觉 field，沙地为沙纹、浅洼和颗粒 field；真实物理扰动仍由低矮可通过 collider 负责。
- 备选项：直接下载大型第三方高精度场景资产；把视觉对象也全部加 collider；只解释 Unity 能做高质量建模但暂不改当前场景。
- 理由：当前主线是按师兄路线先跑通 Unity + ROS2 + 相机 + LiDAR + 物理小车。立即引入大型资产会增加下载、授权、导入和性能风险；把所有视觉细节都加 collider 会改变通过性，可能破坏已经通过的桥/坡和挑战路线。程序化低模层能快速提高辨识度，同时保持可控、可回归、可维护。
- 影响：`run_scout_wheel_ground_challenge_route_smoke_test.sh` 现在检查草地/青石/沙地三段截图和视觉细节数量。低模视觉层首次通过 `vln_scout_wheel_ground_challenge_route_20260817_173720`，后续 PBR 材质升级通过 `vln_scout_wheel_ground_challenge_route_20260817_182912`；旧 13 点金标准回归通过 `vln_scout_wheel_ground_route_20260817_183540`。后续如果要进一步接近游戏级画面，应作为“外部美术资产/PBR 材质阶段”单独推进，不得直接覆盖当前物理基线。

## 2026-08-17：阶段 18A 采用 ambientCG 1K PBR 小样本升级青石/沙地

- 决策：青石路和沙地视觉升级采用 ambientCG 的 `PavingStones151_1K-JPG` 与 `Ground054_1K-JPG` 小体积 PBR 材质包；只导入 1K JPG 贴图子集，不导入大型源文件、不切换渲染管线、不改变物理 collider。
- 备选项：直接导入大型 Unity Asset Store 场景包；切到 URP/HDRP 后重做地表 shader；继续只用程序化纯色材质。
- 理由：用户需要更真实的沙地和石路，但当前项目主线仍是师兄要求的 Unity + ROS2 感知/物理链路。1K PBR 小样本能明显提升地表真实感，同时工程导入量约 `9.1M`，不会把仓库塞爆，也不触碰 CUDA/PyTorch/ROS2/Conda 环境。
- 影响：原始 zip 和完整解包缓存保留在 `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/pbr_materials/ambientcg`，Unity 工程只放行 `Assets/VLN/ExternalAssets/PBRMaterials/AmbientCG` 小子集；`.gitignore` 已增加对应例外规则。挑战路线验收新增 `challenge_pbr_albedo_material_count`、`challenge_pbr_normal_material_count`、`challenge_pbr_occlusion_material_count`，当前均为 `7`。挑战路线通过 `vln_scout_wheel_ground_challenge_route_20260817_182912`，旧 13 点路线通过 `vln_scout_wheel_ground_route_20260817_183540`。

## 2026-08-17：下一阶段转向“材质一致物理代理”，而不是继续只做视觉

- 决策：阶段 18B 明确为草地、沙地、青石/石板路建立和视觉形状对应的简化物理代理与接触审计。后续不能只加贴图和视觉 mesh，必须让主要可接触形状在物理层有对应效果。
- 备选项：继续只升级 PBR/外观；直接上高密度真实草、沙粒和碎石刚体；或把所有区域统一成普通高摩擦平面。
- 理由：用户的电脑硬件不是当前视觉/物理精度的根本上限。当前场景之所以保守，是为了先保护 Unity-ROS2、相机、LiDAR、Scout wheel-ground、固定路线和挑战路线基线。现在这些基线已稳定，下一步应该把物理真实性往材质维度推进：草地柔软轻阻力、沙地滚阻/低附着、石板刚性接缝/凸起。
- 影响：后续实现应使用合并 collider、PhysicMaterial、trigger 区域、轮地阻力/附着修正和可视化变形近似，而不是给每片草叶/每粒沙子做重型 Rigidbody。验收必须同时看分材质接触统计、路线通过性、旧 13 点基线和用户手工视觉观察。

## 2026-08-17：阶段 18B 采用低矮可见物理代理和分材质阻力审计

- 决策：草地、青石/石板路、沙地的第一版物理一致升级采用 `ScoutWheelGround_ChallengePhysicsProxy_*` 低矮可见代理 + WheelCollider 接触分类 + 控制器温和滚阻/低附着近似；不把每片草叶、每粒沙子、每块碎石都做成独立 Rigidbody。
- 备选项：继续只做 PBR/视觉贴图；给所有视觉细节加刚性 collider；或用隐藏触发区/隐藏托底面模拟材质效果。
- 理由：当前需求是“小车接触的主要材质语义要和视觉一致”，不是做不可控的高密度粒子级仿真。低矮代理能让轮胎实际接触主要草根/石缝/沙纹形状，控制器滚阻让草、沙、石表现出不同通行特性，同时保持 13 点金标准和 16 点挑战路线可回归。
- 影响：新增 22 个材质物理代理，挑战路线验收新增 `challenge_physics_proxy_count`、`grass/stone/sand_physics_proxy_count`、`challenge_visual_physics_proxy_audit_pass`、分材质接触步数、代理接触步数、平均速度和轮地高度扰动。第一版通过 `vln_scout_wheel_ground_challenge_route_20260817_210512`；旧 13 点金标准通过 `vln_scout_wheel_ground_route_20260817_210945`。后续升级草叶压倒、更多资产或 URP/HDRP 时必须保持这些审计项和两条路线不退化。

## 2026-08-18：手动速度控制上限放宽到 20m/s，但自动路线默认不变

- 决策：把中文控制面板速度模块的线速度可输入上限、后端 `--manual-max-linear` 默认值、Scout wheel-ground 控制器 `m_MaxLinearSpeedMetersPerSecond`、场景生成器和已保存 wheel-ground 场景中的线速度夹紧上限统一放宽到 `20.0m/s`。
- 备选项：继续保持 `1.20m/s` UI 上限和 `2.0m/s` Unity 控制器夹紧；或者把所有自动路线默认速度也改到极高。
- 理由：用户明确要求仿真里允许自行尝试高速，翻车可重新运行；限制太小会妨碍手动探索。自动演示路线已经是老师演示金标准，默认速度不应跟着放大，否则会把路线稳定性问题混进手动控制自由度调整。
- 影响：`scripts/vln_control_panel.py` 的线速度输入现在显示“最高 20”，`+/-` 步进为 `0.50m/s`；`VlnScoutWheelGroundController` 和 `VLNOffroadScoutWheelGroundCandidate.unity` 允许 `/vln/cmd_vel.linear.x` 到 `±20m/s`。`drive_*_demo.sh` 与 `run_*_smoke_test.sh` 里的自动路线 `--max-linear` 默认保持原值，用于保护阶段 15/18 回归基线。

## 2026-08-17：草地视觉反馈固定为第一版轻倒伏

- 决策：按用户最新反馈，草地视觉反馈回退并固定为第一版 `VlnChallengeGrassDeformer` 轻倒伏方案：车轮附近草叶被压低、向两侧推开，并以低恢复速度留下轻微轮迹感。
- 备选项：保留第二版明显深色压痕/强倒伏轮迹；继续加强车身 footprint 清扫式倒伏；或完全取消草叶变形只保留物理代理。
- 理由：用户明确表示不喜欢第二版特别明显的压痕版本，喜欢第一版倒伏版本。当前项目目标是仿真交互与视觉直觉一致，不是为了“看得很明显”牺牲真实感。
- 影响：`GrassTrackPainter`、深色轮迹贴片、`challenge_grass_track_*` 指标不再属于当前方案；后续除非用户明确改变偏好，不得恢复明显深色压痕或强倒伏轮迹。回退后 16 点挑战路线通过 `vln_scout_wheel_ground_challenge_route_20260817_231723`，旧 13 点金标准通过 `vln_scout_wheel_ground_route_20260817_232310`。

## 2026-08-18：手动速度控制按持续命令流处理

- 决策：控制面板速度模块不再只依赖浏览器键盘事件和无序 HTTP 请求；按“持续命令流”处理，屏幕按钮也必须可按住，速度/停车请求必须有序，旧请求不能覆盖新停车或新按键。
- 备选项：继续只让真实键盘控制；只提高发布频率；或放弃浏览器 UI 改成单独 ROS2 teleop 终端。
- 理由：用户实际体验比自动路线差，核心差异不是车体物理本身，而是输入链路不稳定。自动路线持续闭环发布 `/vln/cmd_vel`，浏览器手动控制如果出现请求堆积、焦点丢失或旧请求晚到，就会表现成延迟、只动一下或松键后还动。
- 影响：`scripts/vln_control_panel.py` 的箭头/A/D 显示改为真实按钮，前端加入请求背压，后端加入 `manual_command_seq` 过期请求过滤，fallback 超时改为 `0.35s`。阶段 19 前必须重新人工验收手动速度控制；如果仍明显差于自动路线，继续阶段 16 修复，不进入数据采集主线。

## 2026-08-18：速度控制默认值和可调上限分离（中间策略，已被 20m/s 上限覆盖）

- 决策：速度控制面板保持默认线速度 `0.55m/s` 不变。该中间策略曾把用户可调线速度上限从 `0.55m/s` 放宽到 `1.20m/s`，并让前端输入框和后端 clamp 使用同一上限；随后已按用户明确要求进一步统一放宽到 `20.0m/s`。
- 备选项：把默认速度也直接提高；继续固定最大 `0.55m/s`；或只改前端不改后端。
- 理由：默认速度需要保守，避免用户一打开就因桥/坡/窄路处横摆而误判物理链路；但用户显式调高速度时应该真实生效。只改前端会被后端夹回 `0.55`，只改后端会让 UI 看起来仍不能调。
- 影响：当前有效实现以同日“手动速度控制上限放宽到 20m/s，但自动路线默认不变”决策为准：`manualLinearSpeed` 前端 `max`、`--manual-max-linear` 默认值和 Unity wheel-ground 控制器夹紧上限均为 `20.0m/s`；自动路线默认速度仍不跟随放大。

## 2026-08-18：Unity 菜单外部终端采用登记和手动清理机制

- 决策：Unity 顶部 `VLN` 菜单启动的外部终端必须登记进程组，但暂时不再绑定 Unity Editor 退出事件自动清理；当前只保留手工“关闭 VLN 后台终端”入口处理异常退出和历史残留。
- 备选项：继续让用户手工找进程杀；每个脚本启动前先强杀同类进程；或者把 endpoint/RViz/控制面板都塞进 Unity 内部运行。
- 理由：用户现在主要通过 Unity 面板统一操作，旧终端残留会直接造成 `8765` 端口占用和重复启动失败。登记菜单进程组仍有价值，但外部 watchdog 和 C# 退出 hook 都出现过“一打开就被杀”的误杀风险。稳定优先，先保证菜单启动不会被自动清理打断。
- 影响：新增 `cleanup_unity_menu_processes.sh` 和 `unity_menu_terminal_session.sh`；`unity_menu_launch.sh` 不再直接拼接终端命令，而是通过会话包装器运行目标脚本。运行态登记文件位于 `.runtime/unity_menu/processes.tsv`，`.runtime/` 已加入 `.gitignore`。外部 watchdog 方案已删除；`VlnManualDemoLauncherCleanupHook` 也已删除，代码中不再订阅 `EditorApplication.quitting`。

## 2026-08-18：Unity 菜单终端不再使用 setsid 隔离

- 决策：`unity_menu_terminal_session.sh` 不再用 `setsid` 重启自身；菜单终端保持普通 GNOME Terminal 会话。每次启动写 `.runtime/unity_menu/logs/<session>.log`，目标脚本退出后保留一个交互 shell，方便用户看到错误。
- 备选项：继续使用 `setsid` 并尝试修 stdin；继续让终端报错后关闭；或者完全取消 Unity 菜单，只让用户手工开终端运行脚本。
- 理由：用户的真实操作入口是 Unity 菜单，如果终端 1 秒关闭，就无法判断是 ROS2 环境、端口占用、RViz/rqt 问题还是包装器问题。`setsid` 对当前目标不是必要条件，反而增加 GNOME Terminal 会话控制复杂度。手动清理可以通过登记 PID/PGID 和 `--include-known` 完成，不需要自动退出清理。
- 影响：菜单启动行为以“能稳定看到日志和错误”为最高优先级。`cleanup_unity_menu_processes.sh` 只清理仍活着的登记 PID，跳过 stale PGID，降低误杀风险。后续如果要恢复自动清理或重新隔离进程组，必须先证明不会导致菜单终端一打开就关闭。

## 2026-08-20：Topgear V2 上装只作为视觉 mesh 叠加到 Scout 物理底盘

- 决策：师兄提供的 `topgear_v2.dae` 在阶段 19 中只作为涂装/上装视觉件挂到 `ScoutWheelGround_VisualUrdf` 下；不新增 collider、rigidbody、质量、惯量、悬挂、WheelCollider 或动力学参数，也不覆盖已有 Scout wheel-ground 物理根。
- 备选项：直接用 Topgear mesh 替换整个车体；给上装 mesh 自动生成 MeshCollider；把真实雷达/四相机一起挂上；或等待师兄后续完整小车模型再处理。
- 理由：师兄明确说新增涂装版本都是 mesh、只起视觉作用，没有新的动力学和物理学参数要建立；当前最重要的是“套用原来小车的物理学和动力学建模，视觉上变成上装版本，但开起来仍和原来一样”。直接给上装加碰撞或动力学会改变当前金标准路线和物理调参边界，偏离本轮需求。
- 影响：阶段 19 的验收必须同时检查 `topgear_visual_present=1`、`topgear_visual_collider_count=0`、`topgear_visual_rigidbody_count=0`，并回归短动、13 点金标准路线和 16 点挑战路线。阶段 19 已通过 run id：视觉专项 `vln_topgear_visual_alignment_20260820_171033`，短动 `vln_scout_wheel_ground_20260820_171049`，13 点路线 `vln_scout_wheel_ground_route_20260820_171934`，16 点挑战路线 `vln_scout_wheel_ground_challenge_route_20260820_172504`。

## 2026-08-20：Topgear V2 姿态采用 DAE Z_UP 到 Scout Y_UP 的显式坐标转换

- 决策：`topgear_v2.dae` 的挂载姿态不再用单纯 yaw 翻转猜测；按文件自身 `Z_UP` 坐标定义处理，让 DAE `+Z` 对应 Scout/Unity 局部 `+Y` 竖直方向，让 DAE `+Y` 前向对应 Scout 局部 `+Z` 车头方向，再把渲染包围盒底部对齐到车身顶部平台附近。
- 备选项：继续只调 `Quaternion.Euler(0,180,0)`；直接在 Unity Inspector 中手工拖拽；或把 DAE 在外部建模软件中永久改轴。
- 理由：用户截图显示上装曾侧躺、悬空且前后方向不对；DAE 文件明确由 Blender 导出并声明 `up_axis=Z_UP`，而当前直接挂在 Unity/Scout 局部帧下会产生轴向错配。用代码显式转换能复现、可回归，也不污染原始模型文件。
- 影响：当前源码位置为 `Assets/VLN/Editor/VlnOffroadScoutWheelGroundCandidateProjectSetup.cs::AttachTopgearV2Visual()`。后续如果师兄给新的上装/完整车体 mesh，先读模型文件坐标系和材质/部件 bbox，再调整挂载常量，不要盲目套旧的 180°/90° 经验。本轮新增 `run_topgear_visual_alignment_smoke_test.sh` 专门输出前/后/左/右/顶视图和材质局部 bbox，用于以后快速验收上装视觉姿态。

## 2026-08-20：Topgear 传感器只做视觉挂载和 ROS2 数据发布，不改车辆物理

- 决策：阶段 20 的 16 线 LiDAR 和四个相机安装在 Topgear 上装上，但传感器视觉件不添加 `Collider`、`Rigidbody`、质量、惯量或任何底盘动力学参数；它们只作为 UnitySensors/UnitySensorsROS 的挂载点和可见模型。
- 备选项：给 LiDAR/相机外壳加 collider；把传感器质量计入车体重心；或直接把传感器简化成不可见 GameObject。
- 理由：师兄当前给的 Topgear 上装 mesh 明确是视觉上装，传感器阶段目标是先把感知层输入打通。给传感器外观件加入碰撞或质量会改变已经验收的 Scout wheel-ground 物理基线，且对当前相机/点云数据链路没有必要。
- 影响：阶段 20 完成标准固定检查 `topgear_sensor_collider_count=0`、`topgear_sensor_rigidbody_count=0`；如果后续要研究传感器重量、安装支架碰撞或完整硬件动力学，必须另开物理标定阶段，不在当前感知挂载阶段混入。

## 2026-08-20：保留旧前相机和 LiDAR topic，新增后/左/右相机 topic

- 决策：LiDAR 继续发布 `/vln/lidar/points`，前相机继续发布 `/vln/front/image_raw` 和 `/vln/front/camera_info`；后、左、右相机新增 `/vln/rear/*`、`/vln/left/*`、`/vln/right/*`，并分别使用 `rear_camera_optical_frame`、`left_camera_optical_frame`、`right_camera_optical_frame`。
- 备选项：把所有 topic 重命名成 Topgear 命名；只保留一个前相机；或把四路相机合成一个图像 topic。
- 理由：旧前相机和 LiDAR 已被用户的 rqt、RViz 和脚本反复使用，改名会破坏已有手工演示和回归入口。新增三路相机既满足上装四相机需求，又保持向后兼容。
- 影响：TF 扩展为 `map->base_link` 和 `base_link->front/rear/left/right_camera_optical_frame,lidar_link`。手工验证时可以继续用原 `view_front_image.sh` 和 `view_vln_vehicle_rviz.sh`，也可以用 `ros2 run rqt_image_view rqt_image_view /vln/rear/image_raw` 这类指定 topic 方式查看新增相机；准确 topic 以 `user.md` 和 `CURRENT_STATE.md` 为准。

## 2026-08-20：阶段 20 后 13 点通过即可停止，不默认继续跑 16 点

- 决策：Topgear 传感器专项通过后，只补跑 13 点金标准路线回归；13 点链路成功后，本轮停止自动验收，不再继续跑 16 点挑战路线。
- 备选项：每次传感器小改后继续全量跑 13 点 + 16 点；或完全不跑路线回归，只验收传感器 topic。
- 理由：用户明确指出 13 点链路验证成功后就可以不用再验证 16 点，后续应让用户亲自看效果。传感器视觉件不参与物理，13 点已经足够证明主链路没有退化；16 点耗时更长，当前没有新增挑战区或障碍物变化。
- 影响：阶段 20 当前记录为传感器专项 `vln_topgear_sensor_suite_20260820_190104` 和 13 点路线 `vln_scout_wheel_ground_route_20260820_190253`。16 点挑战路线保留最近已知通过结果，后续只有新增障碍、挑战区变更、路线风险或用户明确要求时再跑。

## 2026-08-20：Topgear 传感器外观必须使用官方模型，不允许程序化兜底

- 决策：阶段 20 的 LiDAR 外观固定使用 Velodyne VLP-16 官方/外部 DAE mesh，四个相机外观固定使用 RealSense D405 官方 STL mesh；不再允许用程序化圆柱、方块、螺丝、小条、玻璃片等自建外观补细节或替代。
- 备选项：继续在官方 mesh 外叠加程序化细节；官方模型加载失败时临时生成方块/圆柱兜底；或把传感器做成不可见挂载点。
- 理由：用户和师兄的主线是导入真实模型/mesh 到 Unity 中仿真，传感器外观验收看的是模型来源、姿态和挂载位置；程序化兜底会让视觉路线偏离需求，即使 ROS2 topic 能通也不算合格。
- 影响：`run_topgear_sensor_suite_smoke_test.sh` 现在检查 `topgear_sensor_vlp16_official_mesh_count=1`、`topgear_sensor_d405_official_stl_count=4`，并要求旧程序化 VLP rib / D405 screw 残留为 0。官方模型导入失败时应修导入、轴向、缩放或资产路径，而不是创建临时几何体。

## 2026-08-21：Topgear 传感器位姿以用户手动锁定状态为唯一基线

- 决策：阶段 20 传感器位置和角度以用户在 Unity 中手动拖动后锁定的三重状态为唯一基线：`config/topgear_sensor_pose_user_locked.json`、`config/topgear_sensor_hierarchy_user_locked.json`、`config/topgear_sensor_scene_locked/VLNOffroadScoutWheelGroundCandidate_user_locked.unity`。`config/topgear_sensor_pose_overrides.json` 只是兼容副本，不再作为最高优先级真值。
- 备选项：继续把源码默认几何锚点作为真值；或让模型 bbox/孔位检测自动微调相机、LiDAR。
- 理由：用户实测证明源码默认锚点与肉眼正确安装位置存在 10cm 级差异，尤其前相机；自动微调即使数值只动 2cm，也会把用户确认的视觉安装效果破坏。当前项目需要先尊重已保存的人工标定，再做传感器数据链路。
- 影响：后续任何 Topgear 传感器位姿调整必须由用户明确提出，并给出方向/幅度或通过 `VLN -> Topgear 传感器手动微调` 保存。自动验收、batch 重建、截图验证只能应用锁定 JSON / 层级 JSON，不能自行改位姿。若场景被覆盖，先运行 `./scripts/restore_topgear_sensor_locked_scene.sh` 用锁定整场景恢复。

## 2026-08-21：Topgear 专项验收只读现有场景，不再重建主场景

- 决策：`run_topgear_sensor_suite_smoke_test.sh` 和 `run_topgear_visual_alignment_smoke_test.sh` 改为调用 `RunExistingScene()`，只打开 `VLNOffroadScoutWheelGroundCandidate.unity` 的当前保存状态进行 ROS2 数据或截图验收，不再调用 `BuildScoutWheelGroundCandidateScene()`。
- 备选项：继续重建场景后验收；或把 JSON 继续当成唯一真值；或每次验收前让用户重新手动导出 JSON。
- 理由：用户已经用 Unity Editor 肉眼拖动传感器到满意位置，手工保存的主场景本身就是当前视觉基线。重建函数会重新生成并保存主场景，实际效果是覆盖用户场景，不是无害验收。JSON 只适合做重建同步，不应反向覆盖用户肉眼确认结果。
- 影响：Topgear 专项验收不会再破坏传感器位置。真正需要重建地形/车体/路线的基础回归仍可使用 `BuildScoutWheelGroundCandidateScene()`，但重建前会自动备份主场景到 `UnityProjects/_SceneBackups/<timestamp>/`；如后续必须重建，应先确认传感器场景状态已同步到 JSON 或已有备份。

## 2026-08-21：Unity 演示面板相机查看改为四路 rqt + 内部简洁预览

- 决策：`VLN -> ROS2 手工演示面板` 删除 13 点自动路线入口；`查看相机图像` 改为右侧选项栏，包含 `rqt`、`全部相机`、`前相机`、`后相机`、`左相机`、`右相机`。
- 备选项：继续只打开前向 rqt；或把所有图像查看都放到外部终端；或把四路图像做成复杂控制面板。
- 理由：当前演示重点是 Topgear 四路相机和 LiDAR，不再需要在 Unity 面板里保留旧 13 点路线按钮。用户需要低干扰查看画面：rqt 用于 ROS topic 级验证，Unity 内部小窗口用于快速看当前场景四个 Camera 视角。
- 影响：新增 `VlnTopgearCameraPreviewWindow.cs` 和 `scripts/view_all_camera_images.sh`。内部预览不启动终端，不改 ROS2 topic；rqt 入口会打开四个 `rqt_image_view`。`全部相机` 打开时单路相机按钮禁用。

## 2026-08-21：阶段 21 新开高精度荒漠环境主线

- 决策：阶段 20 Topgear 小车、四路相机、16 线 LiDAR、ROS2 控制链路、手动控制链路和 13 点金标准路线冻结为“高精荒漠主线回退基线”；新主线命名为阶段 21：高精度荒漠环境视觉渲染 + 小车真实物理交互。
- 备选项：继续在旧低模挑战区修补；直接下载大型完整荒漠包导入主工程；直接切 HDRP/URP 主工程；直接进入 VLN 算法训练。
- 理由：用户已经验收阶段 20 主链路，继续在旧低模挑战区补丁式提升会越来越难维护。高精荒漠需要从授权、资产体积、渲染管线、物理代理和 ROS2 回归重新分阶段搭建，才能既提升视觉质量又保护现有可演示链路。
- 影响：新增 `docs/high_precision_desert_workflow.md`、`scripts/check_high_precision_desert_phase0_baseline.sh`、`VLN_REFERENCE_LIBRARY/high_precision_desert_research/` 和 `VLN_ASSETS_CACHE/high_precision_desert/` 三层缓存目录。第一轮只做调研和预算，默认不下载大资产；总下载硬上限为 40GB，单资产预计超过 5GB 先暂停汇报。

## 2026-08-21：高精荒漠资产优先使用 CC0/免登录来源

- 决策：阶段 21 第一轮资产优先级为 Poly Haven 和 ambientCG，Fab/Quixel、Unity Asset Store 和其他第三方包只作为调研候选，不默认下载或导入主工程。
- 备选项：直接使用 Fab/Quixel Megascans；购买或导入 Unity Asset Store 完整场景包；继续只用 Kenney/程序化几何。
- 理由：Poly Haven/ambientCG 授权清晰、可脚本化记录来源，适合论文展示可追溯。Fab/Quixel 和 Asset Store 视觉质量可能更高，但账号、授权、包体和导入设置风险更大；Kenney 等轻量资产适合占位，视觉精度不足以作为本阶段主力。
- 影响：本地调研表为 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/high_precision_asset_candidates.md`，预算表为 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/download_budget.md`。正式下载前必须补齐具体资产页、许可证、文件体积和 Unity 导入计划。

## 2026-08-21：高精荒漠场景面积提升到固定室外规模

- 决策：阶段 21 沙盒地形不再采用 80m/150m 小闭环表述，用户要求至少 `7000㎡`。第一版按 `120m x 120m = 14400㎡` 设计，后续可按性能和路线需要扩大。
- 备选项：维持原 80m 到 150m 小闭环；直接做巨大开放世界；先做 120m x 120m 固定室外沙盒。
- 理由：当前目标是固定室外荒漠仿真场景，面积过小会像室内/庭院，无法承载荒漠岩壁、砂地、碎石路、植被遮挡和完整演示路线；扩大场景面积必须同时使用 LOD、GPU Instancing、物理代理简化和近路线高精原则控制性能。
- 影响：阶段 21 文档和后续 Unity 沙盒构建脚本都以 `120m x 120m` 为默认初版，不再按小院级场景设计。

## 2026-08-21：第一批 Poly Haven 高精荒漠资产下载并导入沙盒

- 决策：第一批只下载 Poly Haven CC0 小样本：3 组 4K PBR 地表/岩壁材质、2 个 4K HDRI、1 个 4K 岩石模型、2 个 2K 干旱植被模型；使用 JPG/HDR/FBX，避开 8K/16K 和大 EXR/PNG。
- 理由：用户要求资产要高精且业内常用，但当前仍处于沙盒导入验证阶段，不能一开始把 8K/16K 大包和授权复杂资产导入工程。Poly Haven 质量、授权和可追溯性适合作为第一版专业资产基底。
- 影响：下载通过本地代理 `127.0.0.1:7897` 完成，38 个文件约 `235.99MB`；生成 `VLNHighPrecisionDesertSandbox.unity`，地形面积 `14400㎡`，视觉 smoke test 通过。后续重点是远近景融合、路线自然化和物理代理一致性，而不是继续在旧低模挑战区补丁式修改。

## 2026-08-21：阶段 21 预算上限提升并启动完整场景包调研

- 决策：阶段 21 下载预算硬上限从 `40GB` 提高到 `100GB`；后续采用“大资产整包评估 + 当前 1km² 自建沙盒精修”的并行路线。
- 备选项：继续只用小样本零散资产；直接把第三方完整场景导入主工程；先调研完整包并在副本/沙盒验证。
- 理由：用户明确指出 1GB 小样本策略过保守，且成熟游戏环境包可能比逐个手工搭建更高效。直接导入主工程仍有风险，因为第三方场景可能改渲染管线、ProjectSettings、后处理和依赖，且不会自动满足 Topgear/ROS2/物理代理约束。
- 影响：新增 `large_asset_scene_research.md`。当前整包首选候选为 `Pure Nature 2 : Mojave Desert`；免费技术底座候选为 Unity 官方 `Terrain Sample Asset Pack`；Fab `Realistic Desert Pack` 因 Unreal Engine 格式暂不作为 Unity 第一验证目标。

## 2026-08-21：大资产整包候选重新排序

- 决策：把 `Coast & Dunes` 提升为视觉真实度第一候选，把 `Pure Nature 2 : Mojave Desert` 保留为低风险荒漠整包候选，把 Unity 官方 `Terrain Sample Asset Pack` 保留为免费技术底座。大资产验证目录固定为 `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/`，扫描输出固定到 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_inspections/`。
- 备选项：继续纯手工扩展当前沙盒；直接购买/导入 Mojave；先下载 Unreal/Fab 高质量包再迁移；直接把 URP/HDRP 大包导入主工程。
- 理由：`Coast & Dunes` 页面和发布者资料显示它有 1km x 1km demo、扫描资产、4K 贴图、LOD、实例化植被和大量 prefab，最接近用户要求的专业游戏级真实场景；但它是付费且偏 URP/HDRP，因此只能在副本工程先验证。`Mojave Desert` 更贴近荒漠主题且 Unity 2022.3 风险更低，但视觉可能不如扫描资产路线。官方 Terrain 包不是整包场景，但可补当前 1km² 沙盒的地形工具和 PBR 工作流。
- 影响：更新 `large_asset_scene_research.md`、`high_precision_asset_candidates.md`、`download_budget.md`、`source_index.md`、`user.md`、`workflow.md`、`PROJECT_MEMORY.md`；`scripts/inspect_high_precision_large_asset_package.py` 已通过语法检查并加执行权限。后续下载大包前仍要确认账号/购买/授权，但不再按 1GB 小样本策略限制推进。

## 2026-08-21：大资产扫描入口完成

- 决策：新增 `scripts/scan_high_precision_large_scene_packages.sh` 作为大包批量只读扫描入口；它扫描 `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/` 下的一层文件/目录，并把 JSON 报告写入 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_inspections/`。
- 理由：用户要求后续可以直接开始大资产验证；单包扫描脚本已经有了，但下载多个包后逐个手动写 output 容易出错。批量入口能保证下载后马上形成统一报告，且不会导入 Unity 或改工程。
- 验证：已运行 `./scripts/scan_high_precision_large_scene_packages.sh`，当前目录为空，正确输出 `VLN_HIGH_PRECISION_LARGE_ASSET_SCAN_NO_PACKAGES`；`bash -n` 通过，脚本已加执行权限。
- 影响：更新 `CURRENT_STATE.md`、`workflow.md`、`docs/high_precision_desert_workflow.md` 和 `user.md`。下一步若用户已在 Unity 账号中购买/加入资产，只需下载后放入 `large_scene_packages/` 再运行该脚本。

## 2026-08-21：高精荒漠沙盒地表材质混合修复

- 决策：修正 `VlnHighPrecisionDesertSandboxProjectSetup.cs` 的 TerrainLayer 和 alphamap 逻辑，沙层真实使用 `aerial_sand` diffuse/normal；岩层和 cliff 层降低大块浅色权重；外圈视觉地形也改用沙地材质，避免俯视图出现突兀白色/黄色大块。
- 理由：用户前序反馈过沙漠中出现白色/黄色突兀层。当前大资产包尚未下载，仍需把已有 1km² 自建沙盒作为稳定可控底座；这次修复只改阶段 21 独立沙盒，不碰旧 Topgear 主场景、ROS2 或传感器锁定。
- 验证：已运行 `./scripts/run_high_precision_desert_sandbox_visual_smoke_test.sh`，通过 run id `vln_high_precision_desert_sandbox_20260821_185310`，`terrain_size_m=1000.0`、`terrain_area_m2=1000000`、`collider_count=522`、`success=1`。截图亮白像素比例接近 0，俯视图只剩自然黄褐沙色。
- 影响：更新并保存 `Assets/VLN/Scenes/VLNHighPrecisionDesertSandbox.unity`、TerrainData 和 TerrainLayer 资产。后续大资产未下载前，继续以该沙盒作为自建精修底座。

## 2026-08-21：大资产副本 Unity 工程准备完成

- 决策：新增并运行 `scripts/prepare_high_precision_large_asset_sandbox_project.sh`，创建 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad_LargeAssetSandbox/`，用于导入/验证 Asset Store/Fab 大场景包和 URP/HDRP 管线。
- 理由：当前主工程是已验收的 Unity-ROS2/Topgear 链路，不能让第三方大包修改主工程 `ProjectSettings`、渲染管线、Lighting、Quality 或 Packages。`Coast & Dunes` 明确不支持 Built-in，只能在副本工程验证。
- 验证：脚本已执行成功，输出 `VLN_HIGH_PRECISION_LARGE_ASSET_SANDBOX_READY`；副本只复制 `Assets`、`Packages`、`ProjectSettings`，排除 `Library`、`Temp`、`Obj`、`Logs`、`Builds`、`UserSettings` 等生成目录。
- 影响：更新 `CURRENT_STATE.md`、`docs/high_precision_desert_workflow.md` 和 `user.md`。后续下载大包后先扫描，再导入 `VLN_Offroad_LargeAssetSandbox`，不碰主工程。

## 2026-08-21：大资产扫描评分与 Built-in 兼容候选补充

- 决策：新增 `scripts/rank_high_precision_large_asset_inspections.py`，用于汇总 `inspect_high_precision_large_asset_package.py` 生成的 JSON 报告，按 scene、terrain、prefab、model、texture、pipeline、ProjectSettings 和 physics 线索输出 Markdown 排序报告。批量扫描脚本现在会生成 `large_asset_ranking.md`。
- 补充候选：新增 Fab `Modular Post Apocalyptic Desert Environment / Unity Engine`，页面显示 Unity format、playable demo map，并兼容 HDRP/URP/Built-in；它不替代自然荒漠第一候选 `Coast & Dunes`，但作为当前 Built-in 工作流的低风险大包备选。
- 理由：用户要求“综合评估后继续往下走”，不能只列资产名。下载后必须有可复用评分机制，先判断大包是否有 scene、terrain、prefab、物理/碰撞线索和工程污染风险，再决定是否导入副本工程。
- 验证：`rank_high_precision_large_asset_inspections.py` 语法检查通过；当前无 inspection JSON 时能生成占位报告；`scan_high_precision_large_scene_packages.sh` 在空目录下输出 `VLN_HIGH_PRECISION_LARGE_ASSET_SCAN_NO_PACKAGES` 并生成 `large_asset_ranking.md`。另用 Python 临时目录构造带 `.unity` scene、terrainlayer、30 个模型、35 个贴图、22 个 prefab、ProjectSettings 和 physics 线索的模拟包，扫描评分为 `80`，建议为“优先导入副本工程：场景/物理线索较完整”。
- 影响：更新 `large_asset_scene_research.md`、`high_precision_asset_candidates.md`、`source_index.md`、`download_budget.md`、`docs/high_precision_desert_workflow.md` 和 `user.md`。

## 2026-08-21：大资产导入判定协议建立

- 决策：新增 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_validation_protocol.md`，把大资产从下载到迁移分成 Gate 0 到 Gate 5：来源预算、只读扫描、副本工程导入、视觉/性能截图、迁移决策、物理与 ROS2 回归。
- 理由：用户要求“仔细调研并综合评估后继续往下走”，因此不能下载后直接导入，也不能只凭截图决定。需要明确什么时候走整包路线、什么时候走混合迁移、什么时候继续自建精修。
- 影响：后续任何大包都必须先过 Gate 0/1，再进入 `VLN_Offroad_LargeAssetSandbox`；只有 Gate 4 明确迁移后，才接 Topgear、ROS2 和新荒漠自动路线。

## 2026-08-21：本地大包查找/暂存入口与免费 smoke-test 候选

- 决策：新增 `scripts/find_high_precision_large_scene_packages.sh` 和 `scripts/stage_high_precision_large_scene_package.sh`。前者只读搜索 VLN 缓存、浏览器下载目录和 Unity 缓存中的 `.unitypackage/.zip/.tar` 等文件；后者把用户指定的资产包复制到 `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/`，并做 100GB 单包上限检查。
- 补充候选：新增 `Poly Desert [FREE]` 和 `Low-Poly Desert Environment Pack` 作为免费低模 smoke-test 候选。它们适合验证下载、扫描和副本导入流程，但不满足高精主路线视觉质量。
- 验证：已运行本地查找脚本，发现 `~/下载/robot_market.zip` 等压缩包；只读查看 `robot_market.zip` 后确认其主要内容为 mp4/json/parquet 数据集，不是 Unity 场景包，因此不纳入大资产验证。脚本已通过 `bash -n`，并已加执行权限。
- 影响：更新 `large_asset_scene_research.md`、`high_precision_asset_candidates.md`、`source_index.md`、`docs/high_precision_desert_workflow.md` 和 `user.md`。后续用户下载大包后，不需要猜路径，可先运行 find 脚本定位。

## 2026-08-21：YOPO-Sim 开源越野仿真参考扫描

- 决策：通过本地代理浅克隆 `https://github.com/TJU-Aerial-Robotics/YOPO-Sim` 到 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/open_source_simulators/YOPO-Sim/`，并用大资产扫描器评估其结构。
- 理由：YOPO-Sim 是 Unity 2022.3+、Apache-2.0、多传感器越野环境仿真项目，包含 ROS Integration、UnitySensors、随机 terrain/vegetation 和 data generation，和本项目技术路线高度相关。它能提供路线和架构参考，而不只是视觉资产。
- 验证：本地浅克隆约 `12MB`。扫描输出 `YOPO-Sim_inspection.json`：`scene_package_score=72`、`scene_count=30`、`terrain_asset_count=20`、`prefab_count=37`、`has_project_settings=1`、`has_pipeline_hint=1`、`has_physics_hint=1`；排序报告建议“优先导入副本工程：场景/物理线索较完整”。
- 风险：YOPO-Sim README 要求额外导入 Vista 和 Unity Terrain URP Demo Scene 等免费 Asset Store 包；当前克隆体不是完整高精荒漠视觉资产包，因此不直接导入主工程，也不替代 `Coast & Dunes` / `Pure Nature 2` 的大资产路线。
- 影响：更新 `large_asset_scene_research.md`、`high_precision_asset_candidates.md`、`source_index.md` 和 `CURRENT_STATE.md`。

## 2026-08-21：确认阶段 21 先走完整大资产验证路线

- 决策：阶段 21 不再以 `1GB` 小样本策略推进；`1GB` 只保留为第一批 Poly Haven 链路测试历史。后续优先寻找和验证成熟完整 Unity 荒漠/越野场景包，下载总硬上限保持 `100GB`。
- 路线：大包先过 Gate 0/1，再进入 `UnityProjects/VLN_Offroad_LargeAssetSandbox/`；通过视觉/性能截图后再决定整包路线、混合迁移或继续自建精修。
- 理由：用户明确要求固定室外大场景和游戏/业内常用级别资产。成熟 demo scene 如果有 Terrain、PBR、LOD、植被、岩石、光照和远近景融合，通常比继续零散手工搭建更高效。
- 风险控制：第三方大包可能改 Render Pipeline、ProjectSettings、Lighting、Quality、Packages 和 shader；因此禁止直接导入主工程，禁止覆盖 Topgear 传感器锁定、旧主场景和 13 点金标准路线。
- 本地状态：已通过本地代理保存 `Coast & Dunes` 和 `Terrain Sample Asset Pack` 的网页快照；Fab 页面命令行抓取受 403/SSL 限制，后续以浏览器账号页面和下载包扫描为准。再次扫描确认 `large_scene_packages/` 当前为空，没有可导入的大包。
- 影响：更新 `large_asset_scene_research.md`、`high_precision_asset_candidates.md`、`download_budget.md`、`source_index.md` 和 `CURRENT_STATE.md`。

## 2026-08-21：补充 Fab/Asset Store 完整场景与岩石包候选

- 决策：新增 `Western Desert Town Environment / Unity` 作为 Fab Unity 格式完整 demo-map 备选；新增 `Desert Rocks Pack` 作为混合迁移路线的岩石/峡谷/石山补充资产候选。
- 理由：用户要求继续调研成熟游戏/业内常用大资产。`Western Desert Town` 页面线索显示 Unity format、playable demo map 和 HDRP/URP/Built-in 兼容，适合验证大包导入流程；`Desert Rocks Pack` 有 LOD/collider/demo scene 线索，适合增强当前 1km² 沙盒的路边岩石、峡谷和物理代理。
- 限制：两个新增候选不改变当前主优先级。自然荒漠视觉上限仍优先 `Coast & Dunes`，低风险自然荒漠整包仍优先 `Pure Nature 2 : Mojave Desert`；Western/城镇/废土/遗迹类包只作为副本验证或结构参考，不能直接替代自然越野荒漠路线。
- 影响：更新 `large_asset_scene_research.md`、`high_precision_asset_candidates.md` 和 `source_index.md`。

## 2026-08-21：建立下载前候选加权评分矩阵

- 决策：新增 `large_asset_candidate_matrix.json` 和 `scripts/rank_high_precision_large_asset_candidates.py`，生成 `large_asset_candidate_ranking.md`，把在线候选按视觉真实度、自然越野贴合、完整 demo scene、Unity 2022 适配、Built-in 风险、物理迁移价值和下载可得性加权排序。
- 结果：当前加权排序为 `Coast & Dunes` 第一、`Pure Nature 2 : Mojave Desert` 第二、`Terrain Sample Asset Pack` 第三、`Modular Post Apocalyptic Desert Environment / Unity Engine` 和 `Western Desert Town Environment / Unity` 并列第二梯队。
- 解释：`Terrain Sample Asset Pack` 排名高是因为免费、低风险、官方技术底座价值高，但它不是完整荒漠场景，不能替代 `Coast & Dunes` 或 `Mojave Desert` 的整包验证路线。
- 影响：更新 `large_asset_acquisition_shortlist.md`、`CURRENT_STATE.md` 和 `source_index.md`。后续每次新增候选先更新 JSON 矩阵，再重算 Markdown 排序。

## 2026-08-21：增强本地大包查找脚本

- 决策：`scripts/find_high_precision_large_scene_packages.sh` 增加 `~/Unity/Asset Store-5.x`、Unity Hub Snap/Flatpak 等潜在缓存目录，并支持 `VLN_LARGE_ASSET_MIN_MB` 控制最小显示体积。
- 验证：默认 `1MB` 阈值能显示下载目录中的候选压缩包；`VLN_LARGE_ASSET_MIN_MB=100` 能过滤小杂项，只保留 100MB 以上候选。当前仍未发现真正 Unity 荒漠大场景包。
- 影响：更新 `large_asset_acquisition_shortlist.md`，下载目录混杂时建议用 `VLN_LARGE_ASSET_MIN_MB=100 ./scripts/find_high_precision_large_scene_packages.sh`。

## 2026-08-21：开源 terrain 仓库验证未采纳

- 决策：尝试浅克隆 `TheWizardsCode/Terrains` 作为免费开源 terrain/heightmap 技术底座候选，但不纳入阶段 21 大资产候选。
- 验证：`git ls-remote` 可访问远端 HEAD；随后通过代理浅克隆和 blob 过滤时在约 120 秒后因 `GnuTLS recv error` / `unexpected disconnect` 中断。残留目录约 `30MB`，`git status` 显示大量缺失文件和 `.git/index.lock`。
- 处理：残留目录已改名为 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/open_source_terrain_assets/Terrains.partial_20260821_203123`，并新增 `open_source_terrain_assets/README.md` 标记禁止导入/评分。
- 结论：该路线当前不如成熟 Unity/Fab/Asset Store 场景包可靠；主线仍优先 `Coast & Dunes` / `Pure Nature 2 : Mojave Desert` / 官方 `Terrain Sample Asset Pack`。

## 2026-08-21：建立阶段 21 大资产状态面板

- 决策：新增 `scripts/report_high_precision_large_asset_status.py`，生成 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_status_report.md`，把下载前候选排序、本地暂存大包、inspection 扫描报告和下一步命令合并到一个面板。
- 理由：阶段 21 文档和脚本较多，后续每次继续时不应反复手工翻 `candidate_ranking`、`large_scene_packages`、`large_asset_inspections` 和短名单。状态面板提供一个低成本、可复算的入口。
- 验证：脚本已运行成功，报告显示当前 `large_scene_packages/` 为空、真实大包扫描报告为空、YOPO-Sim 仅为开源技术参考，候选排序前 3 为 `Coast & Dunes`、`Pure Nature 2 : Mojave Desert`、`Terrain Sample Asset Pack`。
- 影响：更新 `CURRENT_STATE.md` 和 `source_index.md`。后续每次大资产下载/扫描后先运行 `./scripts/report_high_precision_large_asset_status.py` 刷新状态。

## 2026-08-21：建立大资产 Gate 0 预算/授权检查

- 决策：新增 `scripts/check_high_precision_large_asset_gate0.py`，生成 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_gate0_report.md`，用于检查来源、授权/账号状态、预留预算和 100GB 硬上限。
- 验证：脚本已运行成功，当前 `raw_downloads` 占用约 `0.23GB`，`large_scene_packages` 为 `0.00GB`，候选预留预算合计约 `49.36GB`，未超过 `100GB` 硬上限。`Terrain Sample Asset Pack` 被正确标记为“可优先下载验证”，付费/Fab/Asset Store 候选仍需账号/授权确认。
- 结论：Gate 0 已证明预算和来源/授权风险可控；但还不能进入 Gate 1，原因是本机仍没有真实 Unity/Fab/Asset Store 大场景包。
- 影响：更新 `CURRENT_STATE.md`、`user.md` 和 `source_index.md`。后续下载前/下载后都应运行该脚本刷新预算状态。

## 2026-08-21：阶段 21 二次调研后选择“完整大资产优先验证”

- 决策：阶段 21 下一步先按成熟完整 Unity/Fab/Asset Store 荒漠/越野大场景包验证推进，而不是继续只靠零散 Poly Haven/ambientCG 手工拼接；当前自建 `1km²` 沙盒保留为可控 ROS2/物理接入底座和回退路线。
- 备选项：继续精修当前自建沙盒；直接把付费大包导入主工程；先拿完整大包进入缓存/副本工程验证后再决定整包迁移或混合迁移。
- 理由：用户要求的是固定室外大场景和游戏/业内常用级资产，成熟 demo scene 若包含 Terrain、PBR、LOD、植被、岩石、光照和远近景融合，效率和视觉上限通常高于继续手工堆资产。直接导入主工程风险过高，因为第三方大包可能修改 Render Pipeline、ProjectSettings、Lighting、Quality、Packages 和 shader。
- 验证：候选矩阵新增 `Desert Environment - Town & Palace | CITADEL` 作为成熟大场景结构参考；重算 `large_asset_candidate_ranking.md`、`large_asset_gate0_report.md` 和 `large_asset_status_report.md` 后，排序仍为 `Coast & Dunes` 第一、`Pure Nature 2 : Mojave Desert` 第二、`Terrain Sample Asset Pack` 第三，候选预留预算约 `55.96GB`，没有超过 `100GB`。
- 影响：更新 `CURRENT_STATE.md`、`PROJECT_MEMORY.md`、`user.md`、`source_index.md`、`large_asset_scene_research.md`、`large_asset_acquisition_shortlist.md` 和候选评分矩阵。当前 `large_scene_packages/` 仍为空，不能伪造导入验证；真实大包下载后必须先走 Gate 1 只读扫描，再进 `UnityProjects/VLN_Offroad_LargeAssetSandbox/`。

## 2026-08-21：扩展 Pure Nature 系列和车辆路线/材质补强候选

- 决策：在候选矩阵中新增 `Pure Nature 2 : Mesa Desert`、`Pure Nature 2 : Oasis Desert`、`Desert Race Track: High-Speed Car Racing Environment` 和 `80+ Realistic Desert Environment Textures - Sand, Rocks & More`；`Desert Industrial Outpost` 因 Fab 命令行抓取只返回 Cloudflare 安全检查，暂不进入主排序。
- 理由：用户要求找“市面上或业内常用、精度高、渲染能力强”的完整荒漠/越野场景，不能只停留在已有候选。Pure Nature 同系列能形成自然荒漠整包备选池；Race Track 对车辆路线验证有价值；80+ Textures 能补当前沙盒近景地表，但不能替代完整场景。
- 结果：重算下载前排序后，前四名为 `Coast & Dunes`、`Pure Nature 2 : Mojave Desert`、`Pure Nature 2 : Mesa Desert`、`Pure Nature 2 : Oasis Desert`。`Terrain Sample Asset Pack` 退到第五，但仍是免费技术底座首选。`Desert Race Track` 排第六，定位为车辆路线验证备选。`80+ Textures` 排在补强类，明确不是完整场景。
- 预算：Gate 0 候选预留预算更新为约 `71.63GB`，仍低于 `100GB` 硬上限；真实下载前仍必须确认账号/授权，下载后先只读扫描。
- 影响：更新 `large_asset_candidate_matrix.json`、`large_asset_candidate_ranking.md`、`large_asset_gate0_report.md`、`large_asset_status_report.md`、`high_precision_asset_candidates.md`、`large_asset_scene_research.md`、`large_asset_acquisition_shortlist.md` 和 `source_index.md`。

## 2026-08-21：第三轮大资产调研加入可平铺荒漠候选和老牌峡谷参考

- 决策：新增 `Desert Terrain - Sand Storm and Dune Environment` 作为 Fab 可平铺荒漠/沙丘第二路线候选；新增 `PDG Canyon Terrain Vol1` 作为低优先级老牌峡谷地形参考；状态面板候选排序显示范围从前 8 扩到前 12，便于看到第二梯队候选。
- 理由：用户要求不要只保守地一块块拼，而要继续寻找更接近游戏/业内常用的大场景资产。可平铺荒漠包可能比手工铺沙丘更高效；老峡谷包可作为地形形态参考，但版本太旧，不能压过自然荒漠第一梯队。
- 风险控制：Fab 命令行快照仍是 Cloudflare 安全检查页，不能当授权、包体或 demo scene 证据；必须等浏览器/账号页面或真实下载包扫描。`PDG Canyon Terrain Vol1` 虽然 Asset Store 快照可提取约 `0.86GB`、146 assets 和付费状态，但只能作为低优先级参考。
- 验证：`python3 -m json.tool` 检查候选矩阵通过；`python3 -m py_compile` 检查相关 Python 脚本通过；`bash -n` 检查大资产 shell 脚本通过；重算排序/Gate 0/状态面板后候选预留预算约 `83.49GB`，仍低于 `100GB`，`scan_high_precision_large_scene_packages.sh` 仍正确报告 `VLN_HIGH_PRECISION_LARGE_ASSET_SCAN_NO_PACKAGES`。
- 影响：更新 `large_asset_candidate_matrix.json`、`large_asset_candidate_ranking.md`、`large_asset_gate0_report.md`、`large_asset_status_report.md`、`large_asset_scene_research.md`、`large_asset_acquisition_shortlist.md`、`download_budget.md`、`source_index.md`、`CURRENT_STATE.md` 和 `PROJECT_MEMORY.md`。主工程、Topgear 锁定文件、13 点金标准路线均未改动。

## 2026-08-21：低模免费包只保留为 smoke-test，不进入高精主线

- 决策：新增 `large_asset_download_attempts.md` 记录实际下载尝试。itch `Poly Desert [FREE]` 页面可通过本地代理访问，CSRF/cookie POST 可拿到临时下载页 URL，但最终 zip 请求被重定向回商品页；不继续绕 itch 前端/会话机制。
- 理由：该包是 1.9MB 低模 CC0 Unity package，只适合测试下载/扫描/副本导入链路，视觉精度和场景规模都不满足高精荒漠主线。继续在低模包自动下载上消耗时间，会偏离用户要求的成熟完整大资产路线。
- 影响：`Poly Desert [FREE]` 和 `Low-Poly Desert Environment Pack` 保留为浏览器/Unity 官方入口的 smoke-test 候选；阶段 21 主优先级仍是 `Coast & Dunes`、`Pure Nature 2` 系列、官方 `Terrain Sample Asset Pack` 和 Fab/Unity 格式完整 demo-map 备选。`100GB` 预算硬上限保持有效，主工程、Topgear 锁定文件和 13 点金标准路线均未改动。

## 2026-08-21：同步大资产第四轮排序和 100GB 预算面板

- 决策：重算并同步 `large_asset_candidate_ranking.md`、`large_asset_gate0_report.md` 和 `large_asset_status_report.md`。当前下载前排序前三为 `Coast & Dunes`、`Pure Nature 2 : Mojave Desert`、`Landscape Ground Pack 3 (Desert Dry Land Beach Sea Islands Coast)`。
- 理由：状态面板旧输出仍显示旧排序，容易让后续继续按 `Terrain Sample Asset Pack` 第三的旧口径推进。`Landscape Ground Pack 3` 与 `Coast & Dunes` 同源，作为扫描地表/terrain/material demo base，对远近景融合和地表真实度有高价值，但不是完整车辆路线包。
- 预算：Gate 0 候选预留预算约 `84.22GB`，低于用户允许的 `100GB` 硬上限；当前 `raw_downloads` 约 `0.23GB`，`large_scene_packages` 仍为空。
- 风险控制：状态面板默认显示候选范围从前 12 扩到前 20，避免截断；真实大包仍必须先进入 `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/`，只读扫描后再导入 `UnityProjects/VLN_Offroad_LargeAssetSandbox/`。
- 影响：更新 `CURRENT_STATE.md`、`PROJECT_MEMORY.md`、`workflow.md`、`user.md`、`docs/high_precision_desert_workflow.md`、`large_asset_acquisition_shortlist.md`、`large_asset_scene_research.md`、`download_budget.md` 和 `source_index.md`。本轮未下载真实大包、未导入 Unity、未改主工程、Topgear 锁定文件或 13 点金标准路线。

## 2026-08-21：阶段 21 确认按完整大资产 Gate 1 验证推进

- 决策：预算硬上限保持 `100GB`，阶段 21 下一步优先获取并验证成熟完整荒漠/越野大场景包，而不是继续只靠 Poly Haven/ambientCG 零散手工拼接。
- 最新排序：`Coast & Dunes` 第一，用于验证视觉真实度上限；`Pure Nature 2 : Mojave Desert` 第二，用于验证自然荒漠低风险整包；`Landscape Ground Pack 3` 第三，用于验证扫描地表/terrain/material 底座和混合迁移价值。
- 三路线：真实包体下载后先 Gate 1 只读扫描，再导入 `UnityProjects/VLN_Offroad_LargeAssetSandbox/` 截图验证；通过后按“整包路线 / 混合迁移 / 自建精修”择优。建筑、废土、遗迹、工业和集市类大包只作为结构参考或兼容性备选，不替代自然越野荒漠主线。
- 验证：重算 `large_asset_candidate_ranking.md`、`large_asset_gate0_report.md` 和 `large_asset_status_report.md` 通过；Gate 0 候选预留预算约 `89.32GB`，低于 `100GB`；当前 `raw_downloads` 约 `0.23GB`，`large_scene_packages` 仍为 `0.00GB`。`scan_high_precision_large_scene_packages.sh` 输出 `VLN_HIGH_PRECISION_LARGE_ASSET_SCAN_NO_PACKAGES`，说明本机还没有可进入 Gate 1 的真实大包。
- 来源处理：已通过本地代理刷新 `Coast & Dunes` 发布者页和 Unity Terrain Tools 5.1 文档快照；Fab `Modular Post Apocalyptic Desert` 与 `Desert Industrial Outpost` 仍返回 Cloudflare challenge，不能作为授权、体积或 demo scene 证据。
- 风险控制：本轮只更新文档、预算、状态面板和来源索引；未下载真实大包、未导入 Unity、未改主工程、Topgear 锁定文件、13 点金标准路线、CUDA/PyTorch/ROS2 环境。

## 2026-08-21：用户选择 Mojave 作为当前 Gate 1 目标

- 决策：用户明确选择 `Pure Nature 2 : Mojave Desert` 作为本轮第一个真实大包验证目标。
- 执行顺序：当前先获取 Mojave 包体，再暂存、只读扫描和副本工程导入验证；`Coast & Dunes` 保留为后续视觉上限备选，`Landscape Ground Pack 3` 保留为地表/terrain/material 混合迁移底座。
- 本地状态：再次查找本机大包缓存和下载目录，`large_scene_packages/` 仍为空；`~/下载/IsaacGym_Preview_4_Package.tar.gz` 与 `~/下载/robot_market.zip` 不是 Unity 荒漠场景包。
- 风险控制：Mojave 是 Unity Asset Store 付费/账号资产，必须通过用户 Unity 账号合法获取；没有真实包体前不伪造导入、截图或通过状态。下载后仍禁止直接导入主工程，只能走 `VLN_ASSETS_CACHE` 暂存和 `UnityProjects/VLN_Offroad_LargeAssetSandbox/` 副本工程验证。

## 2026-08-21：Mojave 目标页刷新和副本工程打开入口

- 决策：新增 `scripts/open_unity_large_asset_sandbox_project.sh`，专门打开 `UnityProjects/VLN_Offroad_LargeAssetSandbox/`，用于 Asset Store/Fab 大包下载、导入和截图验证，避免误导入主工程。
- 来源：通过本地代理 `127.0.0.1:7897` 刷新保存 `Pure Nature 2 : Mojave Desert` 页面快照到 `VLN_REFERENCE_LIBRARY/high_precision_desert_research/web_snapshots/pure_nature_2_mojave_desert_assetstore_refresh3.html`；页面显示 File size 1.2GB、Latest version 1.1、Latest release date Jul 31, 2026、Original Unity version 2022.3.10。
- 验证：`bash -n scripts/open_unity_large_asset_sandbox_project.sh`、`scripts/find_high_precision_large_scene_packages.sh`、`scripts/stage_high_precision_large_scene_package.sh` 通过；批处理打开副本工程最终正常退出，日志结尾为 `Exiting batchmode successfully now!`。`find_high_precision_large_scene_packages.sh` 已补充搜索项目内 `.unity_user/cache`、`.unity_user/config`、`.unity_user/data`，避免用副本工程下载后找不到包体；`stage_high_precision_large_scene_package.sh` 已支持文件和解包目录两种暂存输入。本机 `large_scene_packages/` 仍为空，说明 Mojave 真实包体尚未下载。
- 风险控制：本轮未导入 Mojave、未改主工程、未改 Topgear 传感器锁定文件、未跑 13 点/16 点长路线；下一步仍是用户账号合法下载后 Gate 1 只读扫描。

## 2026-08-21：阶段 21 切换为免费 Terrain + CC0/PBR 高精沙盒路线

- 决策：用户看到 `Pure Nature 2 : Mojave Desert` 页面显示付费价格后，明确要求改为 Unity 官方免费 `Terrain Sample Asset Pack` + Poly Haven/ambientCG 继续做高精荒漠沙盒。当前活动目标写入 `active_large_asset_target.json`，付费/账号资产不再作为当前下载目标。
- 理由：当前主需求是“场景大、真实、精度高、风格统一、复杂且自然”，而不是立即购买不确定的大包。免费 Terrain 技术底座 + 已下载 CC0/PBR 资产可以继续增强现有 `1km²` 沙盒，同时保留可控性、ROS2 接入边界和 Topgear 基线安全。
- 执行状态：沙盒生成器已增强非机械重复布局，包括岩石簇、碎石带、干河道、路线碎石细节和更随机的灌木/树分布；最新视觉 smoke test `vln_high_precision_desert_sandbox_20260822_001123` 通过，`terrain_area_m2=1000000`、`rock_cluster_count=236`、`pebble_count=370`、`dry_shrub_count=520`、`quiver_tree_count=72`、`collider_count=402`。
- 风险控制：`Coast & Dunes`、`Pure Nature 2` 系列、`Landscape Ground Pack 3` 等保留为备用候选池和历史调研；若以后重新启用，仍只能进 `VLN_ASSETS_CACHE` 或 `UnityProjects/VLN_Offroad_LargeAssetSandbox/`，禁止直接覆盖主工程、Topgear 锁定文件或 13 点金标准路线。

## 2026-08-22：Pure Nature 2 Mesa Desert 1.0 本地包导入副本工程通过

- 决策：用户已拿到 `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/Pure Nature 2 Mesa Desert 1.0.unitypackage`，当前阶段 21 活动目标切换为 Mesa Desert 视觉加载验收；只有用户肉眼验收通过后，才进入 Topgear/ROS2/路线迁移。
- 执行：修正 `scripts/inspect_high_precision_large_asset_package.py` 对 `.unitypackage` 内部 `pathname` 的解析，避免只统计内部 preview 而误判；将包硬链接暂存到 `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/`；导入 `UnityProjects/VLN_Offroad_LargeAssetSandbox/` 副本工程；新增 `scripts/open_pure_nature_mesa_desert_sandbox.sh` 供用户手工打开 Mesa demo。
- 验证：只读扫描分数 `86`，包含 2 个 scene、8 个 terrain 线索、86 个 prefab、83 个 model、236 个 texture、85 个 material/TerrainLayer，且无 ProjectSettings 风险。Unity 导入日志无 C# 编译错误，结尾 `Exiting batchmode successfully now!`。视觉加载 run id `vln_pure_nature_mesa_desert_20260822_005701` 通过：`terrain_count=1`、`renderer_count=21302`、`collider_count=16535`、`missing_material_slots=0`、`internal_error_materials=0`、`success=1`，截图已归档。
- 风险控制：本轮只导入副本工程，不导入主工程；不改 `VLNOffroadScoutWheelGroundCandidate.unity`、Topgear 传感器锁定文件、13 点金标准路线、CUDA/PyTorch/ROS2 环境。物理建模、Topgear 接入和 ROS2 长链路等下一阶段必须等用户肉眼验收 Mesa 视觉后再做。

## 2026-08-22：阶段 21 主线切换为 Mesa Desert 路线候选场景

- 决策：用户已经肉眼确认 `Pure Nature 2 Mesa Desert 1.0` 完整场景可用，阶段 21 后续路线和精细化默认基于该完整场景推进；免费自建 `VLNHighPrecisionDesertSandbox.unity` 退为回退/试验场，不再是默认主线。
- 场景策略：不覆盖第三方原始 `Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity`，而是在大资产副本工程中派生 VLN 自有场景 `Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity`。旧入口 `scripts/open_pure_nature_mesa_desert_sandbox.sh` 改为兼容打开这份候选场景，新增明确入口 `scripts/open_pure_nature_mesa_desert_route_candidate.sh`。
- 精细化：新增 `VlnPureNatureMesaDesertRouteCandidateBuilder.cs`，使用 Mesa 包自带 prefab，按固定随机种子不规则布置大石头、碎石、小树/仙人掌、草堆/灌木，并在场景中创建 `VLN_Mesa_ObstacleEnhancement` 和 `VLN_Mesa_RouteCandidate` 根节点，便于后续回滚和审计。
- 验证：新增 `scripts/run_pure_nature_mesa_desert_route_candidate_smoke_test.sh`，最新 run id `vln_pure_nature_mesa_desert_route_candidate_20260822_013001` 通过：`route_waypoint_count=9`、`added_rock_count=130`、`added_rubble_count=50`、`added_tree_count=38`、`added_plant_count=265`、`missing_material_slots=0`、`internal_error_materials=0`，并生成 overview、route_start、route_middle、obstacle_closeup、top_layout 截图。
- 风险控制：本轮只改大资产副本工程和文档/脚本；不把 Mesa 包导入 `UnityProjects/VLN_Offroad` 主工程，不改 Topgear 传感器锁定文件，不改旧 13 点金标准路线，不跑 ROS2 长链路。下一步应先让用户肉眼验收候选场景，再接入 Topgear/ROS2/物理路线并建立新的 Mesa 自动路线基线。

## 2026-08-22：阶段 21 升级为 Mesa+Oasis 拼接完整场景

- 决策：用户已提供 `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/Pure Nature 2 Oasis Desert Unity2022.unitypackage`，当前高精荒漠主工作场景从单 Mesa 升级为 Mesa+Oasis 拼接场景 `Assets/VLN/Scenes/VLNMesaOasisStitchedRouteCandidate.unity`。
- 场景策略：保留第三方原始 `Mesa_Demo.unity` 和 `Scene_Oasis_Day.unity` 不覆盖；在大资产副本工程内生成 VLN 派生拼接场景。两张完整地图使用原资产沙地边界重叠衔接，不额外铺一条手工假沙路。
- 开口策略：根据用户在俯视图红线标注的位置，在 Oasis 山体环入口处删除挡路的大型山体 mesh/collider，形成进入绿洲地图的山口；Terrain 沙地本身不被替换。
- 验证：最新 smoke test `vln_pure_nature_mesa_oasis_stitched_20260822_022523` 通过，`success=1`、`terrain_count=2`、`scene_bounds_size=5508.92,1037.79,7728.40`、`seam_height_delta_m=-0.192`、`seam_profile_mean_delta_m=4.006`、`oasis_gate_removed_obstacle_count=1`、`mountain_gate_removed_renderer_count=375`、`mountain_gate_removed_collider_count=29`、`missing_material_slots=0`、`internal_error_materials=0`；截图显示接缝无蓝色空缝，山口已打开。
- 风险控制：本轮只改大资产副本工程、拼接构建器、入口脚本和文档；未导入主工程，未改 Topgear 传感器锁定文件，未改旧 13 点金标准路线，未启动 ROS2 长链路。下一步应让用户肉眼验收 Mesa+Oasis 拼接场景，再接入 Topgear/ROS2/物理代理和新的荒漠自动路线。
## 2026-08-22：Mesa+Oasis 完整场景拼接成为阶段 21 当前主线

- 决策：阶段 21 当前默认高精荒漠场景从单 `Pure Nature 2 Mesa Desert 1.0` 升级为 `Pure Nature 2 Mesa Desert 1.0` + `Pure Nature 2 Oasis Desert Unity2022` 拼接场景，工作场景为大资产副本工程内的 `Assets/VLN/Scenes/VLNMesaOasisStitchedRouteCandidate.unity`。
- 拼接方式：不手工铺假沙路、不新建沙条过渡；用两张完整地图自带 Terrain 的沙地边界进行有控制的重叠贴合，并按用户标红示意在 Oasis 外圈山体环开入口，删除挡路的大型山体 mesh/collider。
- 验证：最新视觉 smoke test `vln_pure_nature_mesa_oasis_stitched_20260822_022523` 通过，`terrain_count=2`、`seam_height_delta_m=-0.192`、`seam_profile_mean_delta_m=4.006`、`seam_profile_max_delta_m=10.115`、`oasis_gate_removed_obstacle_count=1`、`mountain_gate_removed_renderer_count=375`、`mountain_gate_removed_collider_count=29`、`missing_material_slots=0`、`internal_error_materials=0`。
- 风险控制：第三方原始 `Mesa_Demo.unity` 和 `Scene_Oasis_Day.unity` 均不覆盖；Mesa 单场景 `VLNMesaDesertRouteCandidate.unity` 保留为来源/回退；暂不接入 Topgear/ROS2，先让用户肉眼验收拼接视觉和山口连通性。

## 2026-08-22：新增 Mesa+Oasis 世界模型手工保存与防覆盖机制

- 决策：在大资产副本工程 Unity 顶部菜单新增 `VLN -> 更改世界模型 -> 保存本次世界`，把用户在 Scene 视图中手工拖动、删除、添加后的 `VLNMesaOasisStitchedRouteCandidate.unity` 作为当前主世界保存。
- 保存校验：保存时写入 `VLN_WorldManualSaveMarker_*` 场景 marker，调用 `EditorSceneManager.SaveScene` 保存 `.unity` 文件，再写入 `config/world_model_current_save.json`，记录 scene path、marker、文件大小和 SHA256；只有 marker 出现在场景文件且 JSON 校验通过时才报告成功。
- 防覆盖：`open_pure_nature_mesa_oasis_stitched_scene.sh` 只打开已保存场景；`run_pure_nature_mesa_oasis_stitched_smoke_test.sh` 遇到保存记录时默认跳过重建，仅做只读截图检查；拼接构建器的普通重建入口会阻止覆盖，除非用户在 Unity 对话框里确认强制重建，或显式设置 `VLN_FORCE_REBUILD_STITCHED_WORLD=1`。
- 验证：`bash -n` 检查新增/修改 shell 脚本通过；Unity 大资产副本工程 batch 编译检查正常退出，日志结尾 `Exiting batchmode successfully now!`；新增 `scripts/check_world_model_manual_save_state.sh`，当前未保存状态会明确提示缺少 `config/world_model_current_save.json`，不会假报成功。
- 风险控制：本轮不运行 ROS2，不跑 13 点或 16 点路线，不改主工程、Topgear 锁定文件、第三方原始 Mesa/Oasis 场景；用户后续精修世界以后，以保存记录和 `.unity` 场景本身为最高优先级。

## 2026-08-22：拆分第一套/第二套世界并新增统一打开脚本

- 决策：为了后续分别把 Topgear 小车导入不同世界，新增统一入口 `scripts/open_high_precision_world_model.sh <world>`，通过参数打开世界模型，而不是维护多套互相分叉的打开脚本。
- 场景拆分：`first`/`mesa`/`第一套` 打开 Mesa 独立 VLN 场景 `Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity`；`second`/`oasis`/`第二套` 打开新建 Oasis 独立 VLN 场景 `Assets/VLN/Scenes/VLNOasisDesertRouteCandidate.unity`；`stitched`/`fusion`/`融合版` 保留打开原 Mesa+Oasis 融合场景 `Assets/VLN/Scenes/VLNMesaOasisStitchedRouteCandidate.unity`。
- 兼容性：旧入口 `open_pure_nature_mesa_desert_route_candidate.sh`、`open_pure_nature_mesa_desert_sandbox.sh`、`open_pure_nature_mesa_oasis_stitched_scene.sh` 仍保留，但底层改为调用统一入口；新增 `open_pure_nature_oasis_desert_route_candidate.sh` 作为第二套兼容入口。
- 验证：`bash -n` 检查所有打开脚本通过；Unity batch 顺序验证 `first`、`second`、`stitched` 三个参数均正常打开，日志分别出现 `VLN_MESA_ROUTE_CANDIDATE_OPENED_FOR_MANUAL_REVIEW`、`VLN_OASIS_ROUTE_CANDIDATE_OPENED_FOR_MANUAL_REVIEW`、`VLN_MESA_OASIS_STITCHED_OPENED_FOR_MANUAL_REVIEW`；`VLNOasisDesertRouteCandidate.unity` 已实际生成。
- 风险控制：本轮只改大资产副本工程和脚本/文档，不打开 ROS2、不跑路线、不改主工程、Topgear 锁定文件或第三方原始 `Mesa_Demo.unity` / `Scene_Oasis_Day.unity`。

## 2026-08-22：第一套 Mesa 世界接入 Topgear 真实物理车并通过基础物理/ROS2 验收

- 决策：阶段 21 先只基于第一套 Mesa 世界接入阶段 20 冻结的 Topgear 真实物理小车，暂不处理 Oasis 或融合版。候选场景为 `Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity`，保留原 `VLNMesaDesertRouteCandidate.unity` 作为世界来源。
- 接入方式：新增 `VlnMesaTopgearVehicleCandidateBuilder.cs`，从旧金标准 `VLNOffroadScoutWheelGroundCandidate.unity` 复制 `ScoutWheelGround_PhysicsRoot`、`Offroad_SensorRig_StaticVehiclePlaceholder` 和 `ROSConnection`，再重新绑定 rig 跟随物理车体；不调用旧场景重建函数，不重算 Topgear 传感器位姿，不改 `topgear_sensor_pose_user_locked.json`。
- 出生点：自动扫描 Mesa Terrain，选择平坦沙地洼地，记录到 `config/mesa_topgear_vehicle_candidate.json`；当前位置约 `(-177.961, 55.393, -610.063)`，坡度 `0.000°`，附近障碍数 `0`。
- 物理策略：Mesa TerrainCollider 绑定沙地物理/控制器接触分类；只在 Mesa 候选车上启用 `m_TreatTerrainContactAsSand`，旧 Topgear/13 点基线默认不受影响。
- 验证：物理落地 smoke test 通过，`wheel_collider_count=4`、`terrain_contact_steps=2172`、`no_wheel_contact_steps=1`、`body_height_span_m=0.0111`；ROS2 `/vln/cmd_vel` smoke test 通过，四路相机、LiDAR、odom、TF 在线，`cmd_vel_count=62`，位移约 `2.08m`；障碍撞击 smoke test 通过，真实碎石障碍 `VLN_Mesa_RubbleObstacle_034__RubbleSparse_3` 产生 `wheel_obstacle_contact_steps=232`。
- 新入口：手工查看 `scripts/open_mesa_topgear_vehicle_candidate.sh` 或 `scripts/open_high_precision_world_model.sh first-topgear`；自动验收 `scripts/run_mesa_topgear_vehicle_physics_smoke_test.sh`、`scripts/run_mesa_topgear_vehicle_cmd_vel_smoke_test.sh`、`scripts/run_mesa_topgear_vehicle_obstacle_impact_smoke_test.sh`。
- 风险控制：本轮只改大资产副本工程和脚本/文档，不导入主工程，不覆盖第三方原始 Mesa/Oasis 场景，不改旧 13 点金标准路线，不关闭碰撞、不压平地形、不创建假墙或隐藏托底。

## 2026-08-22：Mesa Topgear 先完成物理贴地和谷底出生点，不进入自动导航路线

- 决策：按用户要求暂停 Mesa 自动导航路线开发，先把第一套 Mesa 世界里的基础物理体验做好：小车出生点应在岩壁之间的低洼谷底，而不是高处岩壁平台；车轮视觉不能浮空；岩壁/悬崖碰撞不能表现成粘墙卡住。
- 出生点：`VlnMesaTopgearVehicleCandidateBuilder.cs` 的出生点扫描从“高处平坦平台优先”改为“低洼谷底优先”，当前 `config/mesa_topgear_vehicle_candidate.json` 记录位置约 `(820.117, 20.012, 205.443)`，`slope_deg=2.668`、`height_range_m=0.151`、`valley_wall_relief_m=62.493`、`obstacle_count=0`。
- 贴地：仅在 Mesa 候选车上把 `m_WheelVisualVerticalOffset` 设为 `0.0m`，修复肉眼看上去轮胎/车体与地面有明显间隙的问题；旧 13 点金标准场景默认参数不受影响。
- 物理材质：Mesa TerrainCollider 改用专用 `VLN_Mesa_SandTerrain.physicMaterial`，岩壁/rock/cliff/boulder/rubble/stone collider 绑定 `VLN_Mesa_RockCliff_Slide.physicMaterial`，避免复用旧挑战沙地材质导致岩壁接触过粘。
- WheelCollider 陡坡策略：只在 Mesa 候选车上启用陡坡/离地刹车放松、轮胎抓地衰减、轮胎悬挂支撑衰减和基于真实接触法线的重力沿坡补偿。目的不是作弊托底，而是避免 Unity WheelCollider 把 40°+ 岩壁误当成可长期支撑的正常地面。
- 验收：落地 `vln_mesa_topgear_vehicle_physics_20260822_072331` 通过，`terrain_contact_steps=2176`、`body_height_span_m=0.0102`；短 `/vln/cmd_vel` + 四路相机/LiDAR/odom `vln_mesa_topgear_vehicle_cmd_vel_20260822_072428` 通过，LiDAR `nonzero_points=7200`、位移约 `2.74m`；真实碎石障碍 `vln_mesa_topgear_vehicle_obstacle_impact_20260822_072617` 通过，`wheel_obstacle_contact_steps=82`；新增悬崖专项 `vln_mesa_topgear_vehicle_cliff_drop_20260822_072135` 通过，`height_drop_m=5.846`、`max_pitch_abs_deg=89.567`、`max_roll_abs_deg=154.670`。
- 风险控制：本轮没有跑或修改自动导航路线，没有改主工程，没有改旧 Topgear 传感器锁定文件，没有压平地形、关闭碰撞、加隐藏托底或假墙；所有修复限制在 `UnityProjects/VLN_Offroad_LargeAssetSandbox/` 的 Mesa 候选车链路。

## 2026-08-22：Mesa Topgear 出生点改为无水荒漠洼地

- 决策：用户指出上一版“低洼谷底”实际落到水池/绿洲水域，和当前荒漠小车测试目标不符。Mesa 第一世界 Topgear 出生点必须继续在岩壁之间的低洼可导航区域，但要硬排除水池、池塘、绿洲水面附近区域，优先选择仙人掌/荒漠植被附近的沙漠洼地。
- 实现：`VlnMesaTopgearVehicleCandidateBuilder.cs` 的出生点扫描新增水体硬过滤和荒漠植被约束：识别 `water/lake/pond/pool/river/stream/oasis_water` 等水体对象或水材质；识别 `saguaro/cactus/opuntia/senita/yucca/agave/brittlebush/drygrass/grasspatch` 等荒漠植被；主出生点要求最近可见水体距离至少 `180m`，并在存在植被信息时要求靠近荒漠植被。
- 当前出生点：`config/mesa_topgear_vehicle_candidate.json` 记录位置约 `(-143.657,55.389,-729.390)`，`slope_deg=1.989`、`height_range_m=0.174`、`valley_wall_relief_m=58.543`、`nearest_cactus_distance_m=22.121`、`nearby_cactus_count=48`、`nearest_water_distance_m=9999.000`。
- 验收：重新生成候选场景日志显示 `VLN_MESA_SPAWN_CONTEXT water_bounds=2 cactus_positions=923`；最小物理落地 `vln_mesa_topgear_vehicle_physics_20260822_074538` 通过，`terrain_contact_steps=2086`、`no_wheel_contact_steps=1`、`body_height_span_m=0.0135`；同步截图 `UnityProjects/VLN_Offroad_LargeAssetSandbox/Logs/vln_mesa_topgear_vehicle_candidate_screenshot.png` 显示车辆在沙纹地面和荒漠植被旁边，不在水中。
- 风险控制：本轮只改第一套 Mesa Topgear 候选场景出生点筛选和 smoke 截图同步保存，不跑自动导航路线，不改旧 13 点金标准路线，不改 Topgear 传感器锁定文件，不引入隐藏托底、假地面或关闭碰撞。

## 2026-08-23：导入 Meadow / ForestLake 并升级世界模型统一入口

- 决策：新增两套非荒漠完整世界作为阶段 21 世界库候选：`Meadow Environment - Dynamic Nature 2022` 作为 `meadow_forest`，`ForestLake 1.5` 作为 `forest_lake`。两者只导入大资产副本工程 `UnityProjects/VLN_Offroad_LargeAssetSandbox/`，不进入主工程。
- 暂存与扫描：两个包硬链接到 `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/`，避免重复拷贝数 GB 文件。Meadow 只读扫描分数 `86`，包含 2 个 scene、219 个模型、345 张贴图、429 个 prefab、117 个 Terrain 相关资产；ForestLake 只读扫描分数 `78`，包含 1 个 scene、24 个模型、88 张贴图、29 个 prefab、5 个 Terrain 相关资产，并带 ProjectSettings/Packages 风险，因此必须继续限制在副本工程。
- 导入：Unity batch 导入 `Meadow Environment - Dynamic Nature 2022.unitypackage` 成功，日志 `import_meadow_dynamic_nature_20260823_185249.log`；导入 `ForestLake 1.5.unitypackage` 成功，日志 `import_forestlake_20260823_185454.log`。
- 派生场景：新增 `VlnImportedWorldSceneRegistry.cs`，从第三方原始 demo scene 派生 VLN 自有入口场景，不直接让用户依赖原始包路径。Meadow 派生 `Assets/VLN/Scenes/VLNMeadowDynamicNatureWorldCandidate.unity`，ForestLake 派生 `Assets/VLN/Scenes/VLNForestLakeWorldCandidate.unity`。
- 截图验收：Meadow 初始 smoke `vln_meadow_dynamic_nature_world_20260823_190201` 主体截图显示森林草甸/湖泊环境，但后续确认 `missing_material_slots=210` 会导致 Play/Scene 中动态粒子对象出现白色移动标志，不能作为长期可忽略项；该问题已在 2026-08-23 单独修复。ForestLake smoke `vln_forest_lake_world_20260823_190233` 通过，`terrain_count=1`、`renderer_count=2592`、`collider_count=562`、`missing_material_slots=0`、`internal_error_materials=0`，截图显示森林湖泊大场景。
- 统一入口：`scripts/open_high_precision_world_model.sh` 推荐改为 `--scene <scene_name>`，当前支持 `mesa_desert`、`oasis_desert`、`mesa_oasis`、`mesa_topgear`、`meadow_forest`、`forest_lake`；同时兼容用户误拼的 `--sence` / `-sence` 和旧的 `first/second/stitched/first-topgear`，避免旧命令立即失效。
- 保存机制：`VLN -> 更改世界模型 -> 保存本次世界` 已把 Meadow / ForestLake 加入已注册世界白名单；用户后续在 Unity 里手工移动/删除/添加物体后，保存按钮会对这两个新场景同样写 marker 和 `config/world_model_current_save.json` 校验。
- 风险控制：本轮未改主工程、未改 Topgear 传感器锁定文件、未改旧 13 点金标准路线、未跑自动导航；ForestLake 的远景截图雾效较重，属于原包视觉设置，后续接车或做论文展示前可单独调相机/雾效/出生点。

## 2026-08-23：Meadow 动态粒子白色标志按材质缺失处理

- 决策：用户在 Meadow 场景中看到规律移动的白色标志后，不再把 `missing_material_slots` 当作非阻断导入警告；Meadow smoke test 现在要求 `missing_material_slots=0` 才算通过。
- 根因：完整包内材质文件存在，但 4 个动态 prefab 的 `ParticleSystemRenderer.m_Materials` 第二槽为 `{fileID: 0}`，包括 `prefab_Bees_Particle`、`prefab_Leafs`、`prefab_Meadow_Dust`、`prefab_Meadow_Dust 2`。这些对象属于昆虫、落叶、尘土粒子系统，Play/Scene 中会移动，因此空槽会表现成动态白色标志/白片。
- 修复：新增 `VLN/World Models/Meadow Dynamic Nature/Fix Dynamic Missing Materials` 和 batch 入口 `VLN.Editor.VlnImportedWorldSceneRegistry.FixMeadowDynamicMissingMaterialsBatch`；脚本会审计 Meadow prefab/场景 renderer，把 Bees 绑定 `M_meadow_insects_01`，Leafs 绑定 `M_leaf_particles`，Dust 绑定 `M_meadow_particle_01`，Dust 2 绑定 `M_meadow_particle_02`，并写入 `vln_meadow_missing_material_audit.txt`。
- 验证：审计报告显示 `prefabs_touched=4`、`material_slots_fixed=4`、`unresolved_renderers=0`、`scene_missing_slots_after_all=0`、`active_internal_error_materials_after_all=0`、`success=1`；重新跑 Meadow smoke `meadow_after_material_fix_v2_smoke_20260823_201956.log` 通过，结果文件 `missing_material_slots=0`、`internal_error_materials=0`、`success=1`。
- 风险控制：本轮只改大资产副本工程中的 Meadow 动态 prefab、候选场景注册/验收脚本和文档；不改主工程、不改 Mesa/Topgear、不改旧 13 点金标准路线、不跑自动导航。

## 2026-08-23：Meadow 白色四角标/大圆标按编辑器 Gizmo 图标处理

- 决策：用户新截图中的白色四角星标和大白圆标不是运行时模型或材质渲染错误，而是 Unity Scene/Game 视图的编辑器 Annotation/Gizmo 图标。处理方式是隐藏编辑器覆盖层图标，不删除对应动态粒子、风场、探针或光源对象。
- 实现：`VlnImportedWorldSceneRegistry.cs` 的 Meadow 打开入口现在会自动执行 SceneView 图标清理，并连续重试直到 SceneView 创建；新增菜单 `VLN -> World Models -> Meadow Dynamic Nature -> Hide Scene View Editor Icons`，也可通过 batch 入口 `HideMeadowSceneViewEditorIconsBatch` 执行。
- 范围：通过 Unity 内部 `AnnotationUtility` 关闭 classID `82/108/123/182/198/215/220/259` 的图标，即 AudioSource、Light/LensFlare、WindZone、ParticleSystem、ReflectionProbe、LightProbeGroup、LightProbeProxyVolume 等。另在 Meadow 打开时把当前 SceneView 的 `drawGizmos` 设为 false，保证肉眼查看场景时不再被白色组件图标覆盖。
- 验证：`vln_meadow_scene_view_icon_cleanup.txt` 显示 `annotation_icons_disabled=8`、`success=1`；随后 Meadow smoke `meadow_after_gizmo_cleanup_v2_smoke` 通过，结果仍为 `missing_material_slots=0`、`internal_error_materials=0`、`success=1`。
- 风险控制：这只影响 Unity 编辑器视图显示，不改变 Meadow 运行时 mesh、材质、碰撞、粒子对象、Topgear、ROS2 或任何旧路线。后续如果要看编辑器组件图标，可以在 Unity Scene 右上角手动打开 Gizmos。

## 2026-08-23：Mesa Topgear 问题坡先记录再优化

- 决策：荒漠主线下一步仍是 Mesa 场景里的 Topgear 小车真实物理仿真；遇到“某个斜坡开不上去/卡住/打滑/像被粘住”的问题时，不直接盲调扭矩、摩擦或坡面，而是先记录真实问题地点的数据，再判断是地形坡口、碰撞代理、轮胎滑移、刹车/阻尼误触发、底盘碰撞还是控制器低速爬坡能力不足。
- 实现：新增运行时组件 `VlnMesaTopgearIssueRecorder`，由 `VlnMesaTopgearVehicleCandidateBuilder.RebindVehicleForMesa()` 自动挂到 `ScoutWheelGround_PhysicsRoot`。记录器订阅 `/vln/cmd_vel`，每 0.05s 输出 `samples.csv`，字段包括车体位置/姿态/速度、命令速度、轮胎接触数、Terrain/非 Terrain 接触、轮地坡度、forward/sideways slip、RPM、motor/brake torque、地形高度/坡度、向下 Raycast 命中的真实地面或障碍、卡滞信号和碰撞体名称。
- 手工流程：用户打开 `./scripts/open_high_precision_world_model.sh --scene mesa_topgear`，按平时流程启动 endpoint 并点击 Play，手动开到问题地形后按 `F8` 标记并截图，`F10` 只截图，`F9` 写 summary。记录保存在 `UnityProjects/VLN_Offroad_LargeAssetSandbox/Logs/mesa_issue_records/mesa_issue_*`。
- 分析入口：新增 `scripts/analyze_mesa_issue_recording.py`，默认分析最新记录目录并生成 `analysis_report.txt`，自动检测卡滞窗口、最大坡度、轮胎滑移、轮胎转速、扭矩/刹车、轮胎离地和主要碰撞体，给出下一步修复方向。
- 验证：Unity 构建 `mesa_issue_recorder_build_v2` 通过；最小物理 smoke `vln_mesa_topgear_vehicle_physics_20260823_212829` 通过，记录器生成 `mesa_issue_20260823_212921/samples.csv`、`events.txt`、`summary.txt`；分析脚本可读取并生成报告。该 smoke 中没有手动前进命令，因此无卡滞窗口，属于预期。
- 风险控制：本轮没有改旧 13 点金标准路线、Topgear 传感器锁定、ROS2 topic 命名或自动导航路线；记录器只增加诊断输出，不改变 WheelCollider、Rigidbody、碰撞体或控制器参数。

## 2026-08-23：Mesa Topgear 问题记录改为手动开始/结束的动态段落

- 决策：单点截图或单个瞬间无法判断“连续坡面上为什么开不上去”。后续问题坡排障改为段落录制：用户在接近问题地形前按 `F6` 开始，完整驾驶通过卡坡/打滑/碰撞过程，中途按 `F8` 打标记，最后按 `F7` 结束。
- 实现：`VlnMesaTopgearIssueRecorder` 不再 Play 后立刻写 `samples.csv`；它先进入待命状态，只有按 `F6` 后才按 0.05s 间隔连续采样，按 `F7` 时写结束事件、自动 summary 和结束截图。CSV 增加 `recording_time_s`，summary 增加录制开始/结束 UTC、持续时间、样本数和按键说明。
- 分析入口：`scripts/analyze_mesa_issue_recording.py` 兼容新的 `recording_time_s`，如果没有按 `F6` 导致没有 `samples.csv`，会明确提示先开始录制再分析。
- 验证：`python3 -m py_compile scripts/analyze_mesa_issue_recording.py` 通过；Unity batch 构建 `mesa_issue_recorder_segment_build_20260823_223123.log` 通过；最小物理 smoke `vln_mesa_topgear_vehicle_physics_20260823_223451` 通过，`success=1`、`wheel_collider_count=4`、`terrain_contact_steps=2028`、`no_wheel_contact_steps=1`、`body_height_span_m=0.0135`。中途一次 Unity batch 在 AssetDatabase 刷新阶段触发 Mono fd 断言并中止，重跑通过，未进入 VLN 方法，未作为记录器逻辑失败处理。
- 风险控制：本轮只改诊断记录器、分析脚本提示和文档；不改 Mesa 场景、Topgear 小车、传感器、物理材质、WheelCollider 或任何自动路线。

## 2026-08-24：Mesa Topgear 打开后自动聚焦小车

- 决策：`./scripts/open_high_precision_world_model.sh --scene mesa_topgear` 不需要额外参数；用户打开后初始视角不在小车附近，是 Unity Scene View 保留上次编辑器视角，不是世界参数错误。
- 实现：`VlnMesaTopgearVehicleCandidateBuilder.OpenCandidateForManualReview()` 打开场景后会延迟选中 `ScoutWheelGround_PhysicsRoot` 并对所有 Scene View 调用 `FrameSelected()`；新增菜单 `VLN -> Mesa Desert -> Focus Topgear Vehicle In Scene View`，用于手动重新聚焦。
- 风险控制：该改动只影响编辑器 Scene View 的选中和视角，不保存场景、不改变小车、传感器、物理、ROS2 topic 或任何路线。

## 2026-08-24：Mesa Topgear 手动速度控制抗卡顿与问题轨迹菜单化

- 决策：用户反馈中文控制面板在 Mesa Topgear 中持续按速度按钮时会周期性卡住；先按控制链路排障处理，不改地形、物理材质、WheelCollider、传感器或自动路线。
- 控制面板修复：`scripts/vln_control_panel.py` 的 `/api/velocity` 前端请求改为短超时并自动续发，避免一个慢 HTTP 请求堵住后续 50ms 速度心跳；`/api/status` 轮询增加超时和 in-flight 限流，避免状态查询挤占浏览器连接；后端手动命令超时默认从 `0.35s` 放宽到 `1.50s`，松键仍通过 `/api/velocity_stop` 立即停车。
- 诊断：`/api/status` 的 manual 字段增加 `timeout_count`、`publish_count`、`last_publish_gap`，后续如果仍卡顿，可以区分是浏览器心跳断、HTTP 堵塞、ROS 发布间隔异常，还是 Unity 物理响应问题。
- 问题轨迹录制：新增 Unity 菜单 `VLN -> Mesa Desert -> 录制问题轨迹 -> 开始录制 / 停止录制 / 标记问题点 / 截图 / 写入 Summary / 打开或复制当前记录目录`，调用现有 `VlnMesaTopgearIssueRecorder`，不再要求用户额外记住外部录制脚本；`F6/F7/F8/F9/F10` 快捷键保留。
- 风险控制：本轮只改控制面板、记录器菜单和文档；不跑自动导航路线，不改 Mesa 场景、小车物理、Topgear 传感器锁定、旧 13 点金标准路线或任何大资产。

## 2026-08-24：Mesa Topgear 问题录制增加 Game 视图 HUD

- 决策：用户录制卡坡过程时需要肉眼确认是否真的开始录制，避免开过问题路段后才发现没有 `samples.csv`。在 Game 视图左上角增加轻量状态 HUD，显示 `待命/录制中/已停止`、样本数、标记数和当前记录目录。
- 实现：`VlnMesaTopgearIssueRecorder` 新增 `m_ShowRecordingHud` 和 `OnGUI()` 状态面板；录制中显示绿色背景，待命/停止显示深色背景。HUD 只读显示，不参与 `/vln/cmd_vel`、WheelCollider、Rigidbody、碰撞、材质或传感器链路。
- 验证：`python3 -m py_compile scripts/analyze_mesa_issue_recording.py` 和 `git diff --check` 通过；Unity batch 构建 `mesa_issue_recorder_hud_build_20260824_004748.log` 通过，日志显示 `Tundra build success`、`VLN_MESA_TOPGEAR_VEHICLE_CANDIDATE_BUILT`、`Exiting batchmode successfully now!`。
- 风险控制：本轮不跑自动导航路线，不改小车物理、Mesa 地形、Topgear 传感器位姿、ROS2 topic 或任何大资产；只增强录制可见性和使用文档。

## 2026-08-24：网页手动速度控制确认命令断流，新增本地键盘控制

- 诊断：用户录制 `mesa_issue_20260824_003724`，时长约 `32.5s`、样本 `189`。报告显示 `command_active` 只有 `20` 个样本，活跃率 `10.6%`；非零速度命令只形成 `2` 段短脉冲，最长连续有效段约 `1.07s`。最大坡度约 `6.05°`、最大滑移 `0.030`、轮胎持续接触 Terrain，说明问题不是地形卡住或物理摩擦导致，而是网页/HTTP/前端按键心跳没有持续送达 `/vln/cmd_vel`。
- 分析脚本：`scripts/analyze_mesa_issue_recording.py` 增加命令健康诊断，报告输出命令活跃率、非零命令样本、有效命令段数、最长连续有效命令段、平均/最大采样间隔；以后遇到类似情况会明确提示“控制命令明显断流”。
- 本地控制：新增 `scripts/local_keyboard_cmd_vel_control.py` 和入口 `scripts/start_mesa_topgear_local_keyboard_control.sh`。它使用本地 Tk 窗口捕获按键，直接通过 rclpy 以默认 `100Hz` 发布 `/vln/cmd_vel`，绕过浏览器和 HTTP。方向约定：`↑/W` 前进、`↓/S` 后退、`←/A` 左转正 `angular.z`、`→/D` 右转负 `angular.z`，松开即停，空格停车，`Q` 退出。
- 验证：`tkinter_ok=1`；`python3 -m py_compile scripts/analyze_mesa_issue_recording.py scripts/local_keyboard_cmd_vel_control.py` 通过；`bash -n scripts/start_mesa_topgear_local_keyboard_control.sh` 通过；入口脚本 `--help` 能在 ROS2 环境下正常打印参数说明。
- 风险控制：本轮没有改 Unity 场景、小车物理、WheelCollider、TerrainCollider、传感器、ROS2 topic 命名或自动路线；只是新增本地手动控制入口并增强诊断报告。

## 2026-08-24：本地键盘控制修复方向键被速度控件吃掉

- 现象：本地 Tk 控制窗口中，焦点落在线速度或角速度控件后，按 `↑/↓/←/→` 会优先调节滑条/数值框，而不是发布车辆速度命令。
- 根因：Tk 默认事件顺序是控件自身/class 绑定先处理方向键，原来的 `bind_all` 全局绑定在后面才执行，因此方向键先被 `Scale/Spinbox` 消耗。
- 修复：`scripts/local_keyboard_cmd_vel_control.py` 给窗口内所有控件插入最高优先级 bindtag `VlnKeyboardControlCapture`，并把 `KeyPress/KeyRelease` 绑定到该 tag。现在方向键、`W/A/S/D`、空格和 `Q` 会先进入车辆控制逻辑；速度值仍可用鼠标拖动滑条或在数值框里输入数字调整。
- 验证：`python3 -m py_compile scripts/local_keyboard_cmd_vel_control.py`、`bash -n scripts/start_mesa_topgear_local_keyboard_control.sh`、入口 `--help`、`git diff --check` 均通过。

## 2026-08-24：Mesa Topgear 补入世界模型手动保存白名单

- 决策：`./scripts/open_high_precision_world_model.sh --scene mesa_topgear` 打开的 `Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity` 是当前阶段 21 主线小车场景，应允许通过 Unity 菜单 `VLN -> 更改世界模型 -> 保存本次世界` 保存用户手工编辑结果。
- 根因：`VlnWorldModelManualSaveWindow.cs` 的可保存世界白名单只注册了 Mesa、Oasis、Mesa+Oasis、Meadow、ForestLake，漏掉 `VlnMesaTopgearVehicleCandidateBuilder.CandidateScenePath`，导致正确场景被误报为“当前场景不是已注册世界”。
- 修复：手动保存面板已补入 Mesa Topgear 标签、保存白名单和 `next_open_command` 映射；错误提示和面板 warning 同步加入 `mesa_topgear`，避免继续误导用户换流程。
- 验证：`git diff --check` 通过；Unity 大资产副本工程批处理编译 `world_save_mesa_topgear_allow_compile_20260824_122407.log` 正常退出，日志显示 `AssetDatabase: script compilation time` 和 `Exiting batchmode successfully now!`，未执行场景重建或自动保存。
- 风险控制：本轮只改保存注册逻辑和文档，不改变 Mesa Topgear 场景内容、小车物理、Topgear 传感器位姿、ROS2 topic、旧 13 点金标准路线或任何自动导航路线。

## 2026-08-24：世界模型保存改为自动注册新 VLN 场景

- 决策：后续每次导入新世界模型后，不能再只靠硬编码白名单逐个补场景名。统一规则改为：内置世界继续保留明确别名，新派生世界只要保存到 `Assets/VLN/Scenes/` 并符合 `VLN*WorldCandidate.unity`、`VLN*RouteCandidate.unity`、`VLN*TopgearVehicleCandidate.unity` 或 `VLNHighPrecisionDesertSandbox.unity` 命名，就自动进入 `VLN -> 更改世界模型 -> 保存本次世界` 的可保存范围。
- 实现：`VlnWorldModelManualSaveWindow.cs` 新增自动扫描 `Assets/VLN/Scenes/*.unity`，生成动态注册列表；自动注册场景会显示“自动注册 VLN 世界”，保存 manifest 的 `next_open_command` 会写成 `./scripts/open_high_precision_world_model.sh --scene Assets/VLN/Scenes/<scene>.unity`。
- 统一脚本：`scripts/open_high_precision_world_model.sh` 除了 `mesa_desert / oasis_desert / mesa_oasis / mesa_topgear / meadow_forest / forest_lake`，现在还能直接接受 `VLNNewWorldCandidate` 或 `Assets/VLN/Scenes/VLNNewWorldCandidate.unity`，并调用 `OpenRegisteredSceneFromCommandLine` 打开已自动注册场景。
- 验证：`bash -n scripts/open_high_precision_world_model.sh`、`git diff --check` 通过；Unity 批处理直接打开自动注册场景 `VLNForestLakeWorldCandidate` 通过，日志 `world_auto_register_direct_open_20260824_123610.log` 显示 `VLN_WORLD_MODEL_REGISTERED_SCENE_OPENED Assets/VLN/Scenes/VLNForestLakeWorldCandidate.unity` 和 `Exiting batchmode successfully now!`。
- 风险控制：自动注册只覆盖 `Assets/VLN/Scenes` 下符合 VLN 世界候选命名的场景，不放开第三方原始 Demo 场景、ROS smoke test 场景或旧低模工程场景；本轮不改变任何场景内容、小车物理、传感器位姿、ROS2 topic 或自动路线。

## 2026-08-24：Mesa Topgear 四路相机改为 120° 鱼眼/超广角近似，传感器频率提升

- 决策：按师兄口径“如果只能调 FOV，大于 90° 就算鱼眼视角”，当前不引入真实鱼眼畸变 shader 或相机模型重构，先把 Mesa Topgear 四路 UnitySensors RGB 相机设为 `FOV=120°`、`640x480`、`20Hz`，LiDAR 设为 `10Hz`。
- 理由：Unity 普通 `Camera.fieldOfView` 本质是透视相机广角，不等同真实等距/等固角鱼眼投影；但本阶段师兄明确接受通过 FOV 实现鱼眼视角，120° 能明显扩大视野并保留现有 ROS2 Image/CameraInfo 链路稳定性。
- 实现：新增 `VlnTopgearFisheyeSensorConfig.cs`，提供 Unity 菜单 `VLN -> Topgear 传感器 -> 应用鱼眼视角与高频发布到当前场景` 和 batch 入口 `ApplyMesaTopgearSceneBatch`；Mesa Topgear 构建器和默认 Topgear 传感器生成参数同步引用该配置，避免后续重建候选场景时回退到旧 FOV/频率。
- 验证：新增 `scripts/ros2_measure_topic_frequency.py` 和 `scripts/run_mesa_topgear_fisheye_sensor_rate_smoke_test.sh`。最新 run id `vln_mesa_topgear_fisheye_sensor_rate_20260824_125706` 通过：前/后/左/右相机实际频率约 `20.022/20.119/20.819/20.089Hz`，LiDAR 实际频率约 `10.393Hz`；四路预览 PNG 已导出到 `UnityProjects/VLN_Offroad_LargeAssetSandbox/Logs/topgear_fisheye_previews/`，肉眼检查为明显广角视野。
- 风险控制：本轮只改 Mesa Topgear 当前主线和候选构建默认传感器参数，不改 Topgear 传感器位姿锁定 JSON/锁定场景、不换官方 VLP-16/D405 外观模型、不改小车物理、控制器、旧 13 点金标准路线或自动导航路线。

## 2026-08-24：Mesa Topgear 加入 Lens Distortion，LiDAR 提升到 15Hz/90m

- 决策：在上一版 `FOV=120°` 的基础上，四路 Topgear 相机额外挂 Unity Post Processing `Lens Distortion`，参数为 `intensity=-55`、`scale=1.08`；LiDAR 从 `10Hz / 45m` 提升到 `15Hz / 90m`，其中 `90m` 是原默认最大距离的两倍。
- 实现：`VlnTopgearFisheyeSensorConfig.cs` 统一写入相机畸变、LiDAR `_frequency` 和 `_maxRange`；`VlnOffroadScoutWheelGroundCandidateProjectSetup.cs` 的 Topgear LiDAR 默认最大距离改为引用同一配置，避免后续重建退回 45m；验收脚本新增 `lidar_target_max_range_m=90.0` 和 `lidar_max_range_set_count=1` 检查。
- RViz：`config/vln_vehicle_sensors.rviz` 和 `config/vln_lidar_pointcloud.rviz` 改为 `Frame Rate=60`、PointCloud2 `Depth=20`、`Decay Time=0.4`、点大小 `2px`，这只优化显示刷新，不替代真实 `/vln/lidar/points` 频率验收。
- 验证：`scripts/run_mesa_topgear_fisheye_sensor_rate_smoke_test.sh` 最新通过 run id `vln_mesa_topgear_fisheye_sensor_rate_20260824_132435`，前/后/左/右相机实际频率约 `19.806/20.094/20.884/20.071Hz`，LiDAR 实际频率约 `15.078Hz`，结果文件确认 `lidar_target_max_range_m=90.0`、`lidar_max_range_set_count=1`。
- 风险控制：本轮不降低 LiDAR 点数、不关闭碰撞、不改小车物理、不改 Topgear 传感器位姿锁定文件、不改官方 VLP-16/D405 外观模型、不跑或修改旧 13 点金标准路线。

## 2026-08-24：Mesa Topgear LiDAR 改为每帧完整一圈点云

- 决策：用户反馈 RViz 中只能看到几十度扇区慢慢扫，无法持续看到整圈。根因不是单纯 RViz 帧率，而是 UnitySensors Raycast LiDAR 当前 `pointsNumPerScan=7200`，而 VLP-16 scan pattern 总 `size=57600`，每条 PointCloud2 只包含 1/8 圈。改为每帧直接输出完整 `57600` 点一圈。
- 实现：`VlnTopgearFisheyeSensorConfig.TopgearLidarPointsPerScan=57600`，并在每次应用 Mesa Topgear 传感器配置时写入 `_pointsNumPerScan`；旧 Topgear 构建默认点数同步引用该配置。LiDAR 目标频率从 `15Hz` 提到 `16Hz`，给实际调度留出余量，最大距离保持 `90m`。
- RViz：完整一圈点云不再需要长时间残留拼圈，`config/vln_vehicle_sensors.rviz` 和 `config/vln_lidar_pointcloud.rviz` 使用 `Frame Rate=60`、PointCloud2 `Depth=20`、`Decay Time=1`、点大小 `2px`。
- 验证：`scripts/run_mesa_topgear_fisheye_sensor_rate_smoke_test.sh` 最新通过 run id `vln_mesa_topgear_fisheye_sensor_rate_20260824_170436`，结果文件显示 `lidar_target_frequency_hz=16.0`、`lidar_target_max_range_m=90.0`、`lidar_applied_points_per_scan=57600`、`lidar_scan_pattern_size=57600`，实测 `/vln/lidar/points` 为 `16.080Hz`。
- 风险控制：只改 LiDAR 数据密度/频率和 RViz 显示，不改 Topgear 传感器位姿、官方外观模型、小车物理、碰撞、控制器或旧 13 点路线。

## 2026-08-26：Mesa Topgear 四路相机改为 UnitySensors 官方真实鱼眼

- 决策：停止使用旧 `FOV=120° + Unity Post Processing Lens Distortion` 近似鱼眼方案，改用 UnitySensors 包内已有 `FisheyeCameraSensor`。四路相机统一为 `Equidistant` 等距鱼眼模型，`view_angle=190°`、`640x640`、`20Hz`；ROS2 图像由 UnitySensors 官方 `ImageMsgPublisher` 直接发布 `FisheyeCameraSensor.texture0`，CameraInfo 发布 `distortion_model=equidistant` 和对应内参。
- 理由：用户明确要求“UnitySensors 如果本来就有鱼眼摄像头就直接用，不要自己乱编”。`FisheyeCameraSensor` 已内置 UCM/EUCM/DS/KB4/OCAM/Equidistant 等成熟模型，比普通 FOV 或后处理畸变更适合给师兄解释和后续做反校正验证。
- 实现：`VlnTopgearFisheyeSensorConfig.cs` 现在启用 `FisheyeCameraSensor`，移除旧 `RGBCameraSensor`，停用旧 Lens Distortion 后处理，停用旧 CameraInfo publisher，配置官方 `ImageMsgPublisher` 指向官方鱼眼 sensor，并用 `VlnFisheyeCameraInfoPublisher` 发布等距模型 CameraInfo。Unity AssetDatabase 不能直接把 `Samples~` 官方材质作为场景资产引用，因此工程内保留一份 UnitySensors 官方 `FisheyeCamera` shader 副本和四路独立材质实例；这些材质只服务官方 sensor，不是自写相机模型。
- 验证：`scripts/run_mesa_topgear_fisheye_sensor_rate_smoke_test.sh` 最新通过 run id `vln_mesa_topgear_fisheye_sensor_rate_20260826_135154`。实测前/后/左/右图像频率约 `19.665/19.831/20.857/19.801Hz`，LiDAR 约 `17.840Hz`；`scripts/ros2_capture_fisheye_images.py` 保存四路 raw 圆形鱼眼和 `90°` 反校正图，报告 `VLN_FISHEYE_CAPTURE_AND_RECTIFY_OK`。
- 风险控制：本轮只改 Mesa Topgear 当前传感器数据链路、CameraInfo 和验收脚本；不改 Topgear 传感器手动锁定位姿、不改官方 VLP-16/D405 外观模型、不改小车物理、WheelCollider、控制器、Mesa 地形或旧 13 点金标准路线。

## 2026-08-26：Unity 内部相机预览必须直接显示官方鱼眼纹理

- 决策：Unity 内部 `全部相机/前相机/后相机/左相机/右相机` 预览窗口不再用普通 `Camera.Render()` 作为 Mesa Topgear 的显示源，必须优先显示 `FisheyeCameraSensor.texture0`，保证它和 ROS2 `/vln/*/image_raw` 同源。
- UI 约束：顶部菜单 `VLN -> 手工演示 -> 查看相机图像` 改为直接展开 `rqt / 全部相机 / 前相机 / 后相机 / 左相机 / 右相机`，禁止再为选择相机弹出额外面板或右侧选项栏。
- 风险控制：本轮只改 Unity Editor 菜单和预览窗口；不改传感器位姿、相机/雷达官方外观模型、ROS2 topic、Mesa 场景、小车物理或自动路线。

## 2026-08-26：Topgear 上装、LiDAR 和四路相机绑定为可手动保存整体

- 决策：用户需要在 Unity Scene 视图中手动调整 Topgear 上装整体安装角度/前后位置，但不希望重新调整已经验收的 LiDAR 和四路相机局部位置。因此新增整体节点 `VLN_Topgear_UpperAssembly_UserAdjustableRoot`，把 `ScoutWheelGround_TopgearV2Visual` 和 `ScoutWheelGround_TopgearSensorSuite` 共同挂到该节点下。
- 保存：Unity 菜单新增 `VLN -> Topgear 上装整体微调 -> 绑定上装和传感器为整体 / 选中上装整体 / 保存当前小车模型`。保存会写入 `config/topgear_upper_assembly_user_locked.json`，同时调用世界模型保存机制写入当前 Mesa Topgear 场景，并更新 `config/world_model_current_save.json` 做 marker + SHA 校验，避免“前端显示成功但下次打开仍是旧模型”。
- 自动应用：`mesa_topgear` 打开入口现在会先应用当前鱼眼/雷达配置，再应用 `config/topgear_upper_assembly_user_locked.json` 中的上装整体保存基线；如果将来强制重建 Mesa Topgear 候选场景，也会在最终保存前尝试恢复该整体基线。
- 风险控制：本轮只改变上装视觉和传感器 rig 的父子层级与可保存 transform；不改底盘、轮子、Rigidbody、WheelCollider、车辆动力学、Mesa 物理材质、旧 13 点路线，也不重新计算单个传感器位姿。

## 2026-08-26：Topgear 白色信号器只做可行性审计，不删除

- 结论：`topgear_v2.dae` 的上装主体导入为一个 `topgear_v2-mesh`，不是独立的多个 GameObject；但 mesh 内部按材质分为 `other/screen/cover/iron/gps/plugin` 六个材质 submesh。白色信号器的圆盘主体高度集中在 `gps-material`，包围盒约 `0.153 x 0.153 x 0.062`，立杆/连接白件主要落在 `iron-material`，因此可以通过材质/submesh 抽离、隐藏或重新导出 mesh 来处理。
- 风险：`iron-material` 不一定只包含信号器立杆，直接整材质隐藏可能误伤其它白色支架；如果下一步真的去掉信号器，应先生成预览或复制 mesh 后按空间包围盒裁剪，不直接改原始 `topgear_v2.dae`。
- 状态：本轮没有删除、隐藏或改动白色信号器，只完成可行性判断。

## 2026-08-26：四路真实相机数据位姿与 D405 视觉模型解耦

- 决策：Unity 的 `Camera/FisheyeCameraSensor/ImageMsgPublisher` 组件本身没有独立 Transform；如果组件和 D405 视觉 mesh 在同一相机 GameObject 层级下，拖动真实相机必然带着视觉模型一起走。因此新增直接菜单 `VLN -> Topgear 相机数据位姿微调`，把四个 D405 视觉模型移到独立根 `VLN_Topgear_CameraVisuals_UserLockedRoot`，真实数据相机仍保留 `Topgear_Front/Rear/Left/Right_RGBCamera_UnitySensorsROS` 原名和原 topic。
- 保存：用户调整四路真实数据相机后，点击 `保存当前四路真实相机位姿` 会写入 `config/topgear_camera_data_pose_user_locked.json`，并调用世界模型保存机制真实保存 Mesa Topgear 场景；同时更新上装整体保存，避免旧上装层级恢复时覆盖解耦后的视觉树。
- 自动应用：`mesa_topgear` 打开/强制重建时，会在应用上装整体基线之后应用四路真实相机数据位姿，保证下次加载的 ROS 图像采集点等于用户上次保存的位置。
- 风险控制：本轮不改四路相机 topic、CameraInfo frame、UnitySensors 官方鱼眼模型、D405 官方视觉 mesh、LiDAR、车体物理、WheelCollider、Mesa 地形或旧 13 点路线。

## 2026-08-27：回退场景级写实增强，只保留小车视觉增强候选

- 决策：用户不满意上一版 `mesa_topgear_realism` 场景级写实增强，因此当前主线回退到原始 `mesa_topgear`；不再默认恢复被否决的场景级光照、地表、植被或后处理改动。
- 归档：上一版场景级增强器、候选场景、材质/地形资源、配置和 smoke 脚本已移到 `.runtime/rejected_mesa_topgear_scene_realism_20260827/`，避免 Unity 自动编译或打开入口继续引用。
- 新候选：新增 `VlnMesaTopgearVehicleVisualEnhancer.cs`，生成 `Assets/VLN/Scenes/VLNMesaTopgearVehicleVisualCandidate.unity`。它从 `Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity` 复制，不覆盖原始 `mesa_topgear`，只给小车轮胎、车身和上装生成材质变体、程序化轻微沙尘/橡胶/粗糙表面贴图、8 个无碰撞沙尘细节面片和 3 个局部车体补光。
- 入口：`./scripts/open_high_precision_world_model.sh --scene mesa_topgear_vehicle_visual` 打开小车视觉增强候选；`./scripts/run_mesa_topgear_vehicle_visual_smoke_test.sh` 只做候选构建、截图和审计，不跑 ROS2、不跑自动路线。
- 验证：最新 `vln_mesa_topgear_vehicle_visual_20260827_175503` 通过，`base_scene_unchanged=1`、`vehicle_material_variant_slot_count=23`、`surface_detail_quad_count=8`、`surface_detail_collider_count=0`、`wheel_collider_count=4`、`fisheye_sensor_count=4`、`image_publisher_count=4`、`missing_material_slots=0`、`internal_error_materials=0`。
- 风险控制：本轮不改沙漠地形、不改 Terrain/Collider/物理材质、不改 Rigidbody/WheelCollider/控制器、不改官方 VLP-16/D405 外观模型、不改传感器位姿锁定 JSON、不改 ROS2 topic、不改旧 13 点金标准路线。

## 2026-08-27：团队部署文档与仓库发布策略

- 决策：师兄要求周六前准备团队环境部署教程。当前不应把本机 50GB+ 工作目录整体上传，而应先整理 Git 仓库，只提交代码、配置、小型必要资产和文档；大型 Asset Store/Fab/Unity 场景包、Unity 缓存、rosbag、截图和运行态文件继续留在本地或由负责人单独分发。
- 新增文档：`README.md` 作为仓库快速入口；`docs/team_environment_setup.md` 作为团队成员部署教程，覆盖 Unity 2022.3.62f1、ROS2 Humble、ROS-TCP-Endpoint workspace、标准手工演示流程、大资产副本工程和常见故障。
- 新增脚本：`scripts/setup_ros_tcp_endpoint_workspace.sh` 用于团队成员在项目内 clone/build ROS-TCP-Endpoint，并应用已验证的 `rclpy.ok()` 退出补丁；该脚本不执行 apt/pip/conda/snap 安装。
- 新增检查：`scripts/check_repo_release_readiness.sh` 作为提交/推送前只读检查，拦截误追踪大资产、Unity 缓存、`.runtime`、`unity_ros2_ws`、`.unitypackage`、rosbag 和 `config/world_model_current_save.json` 等本机状态。
- 可迁移性：常用启动脚本已改为从脚本位置推导 `VLN_ROOT`，并支持 `UNITY_EDITOR`、`VLN_UNITY_PROJECT`、`VLN_LARGE_ASSET_PROJECT`、`UNITY_ROS2_WS` 环境变量覆盖，方便团队成员 clone 到不同用户名目录。
- 上传状态：当前 remote 已配置为 `https://github.com/yangou-ylz/VLN_Car.git`；真正 push 仍取决于本机是否有该仓库写权限的 GitHub 凭据或 SSH key。

## 2026-08-27：团队交付范围收窄为 Mesa Topgear 主线

- 决策：用户明确纠正团队交付范围。团队成员不需要早期低模测试环境、旧 13 点路线环境、Oasis/Meadow/ForestLake、Mesa+Oasis 融合版、长开发日志、调研缓存或中途实验资产；交付包只保留当前已验收的 `mesa_topgear` 主线：Pure Nature Mesa 沙漠环境 + Topgear 真实物理小车 + 四路鱼眼相机 + 16 线 LiDAR + ROS2 控制链路。
- 发布策略：继续采用“GitHub 代码仓库 + Mesa Topgear Unity 发布工程资产包”。GitHub 仓库 `https://github.com/yangou-ylz/VLN_Car.git` 只承载脚本、配置、文档和小型源码；主线 Unity 发布工程由 `scripts/prepare_mesa_topgear_team_release_project.sh --refresh` 生成，再由 `scripts/package_mesa_topgear_team_release_project.sh --split 1900M` 打包后通过网盘、内网文件服务、移动硬盘或 GitHub Release 附件分发。
- 文档重写：`docs/team_environment_setup.md` 已改为正式部署手册口径，聚焦交付范围、前置版本、仓库获取、Mesa Topgear 发布包解压、ROS-TCP-Endpoint 构建、Unity/Endpoint/Play/键盘控制顺序、四路鱼眼相机和 LiDAR 验收、常见问题。不再把其它大世界资产流程写成团队默认路径。
- 检查更新：`README.md` 默认入口改为 Mesa Topgear 团队发布工程；`scripts/check_repo_release_readiness.sh` 检查项改为 Mesa Topgear 发布脚本和主线部署文档，避免发布检查继续默认旧主工程/旧路线演示脚本。
- 风险控制：普通 Git 历史不提交数 GB Unity 发布工程、原始 `.unitypackage`、Unity 缓存、rosbag、截图或运行态 marker；`config/world_model_current_save.json` 仍保持本机状态文件，不进入团队共享版本。
