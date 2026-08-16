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
