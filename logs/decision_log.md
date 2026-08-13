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
