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
