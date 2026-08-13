# 环境配置与保姆级操作记录

本文件专门记录环境状态、启动方式和后续环境变更教程。任何新环境、新依赖、新工具链都必须先更新本文件，再执行安装或配置。

## 最高优先级约束

1. 全程中文交流。
2. 禁止未经用户确认安装、卸载、升级任何系统包或 Python/Conda 包。
3. 禁止改动现有 CUDA / PyTorch 组合。
4. ROS2 相关命令优先使用 `ros2env`，避免 Conda 与 ROS2 冲突。
5. 如必须新增 Python 依赖，先创建虚拟环境，并获得用户明确确认。
6. 外部资料放在 `/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY`，不要放进工作区。

## 当前机器配置

- 系统：Ubuntu 22.04.5 LTS，内核 `6.8.0-136-generic`。
- CPU：AMD Ryzen 9 8945HX with Radeon Graphics。
- CPU 规模：16 核 32 线程。
- 内存：约 30GiB，当前可用约 19GiB。
- 交换分区：约 15GiB。
- 磁盘：根分区约 1.5T，可用约 1.2T。
- GPU：NVIDIA GeForce RTX 5060 Laptop GPU。
- 显存：约 8151MiB。
- NVIDIA 驱动：580.173.02。
- `nvidia-smi` CUDA Runtime：13.0。
- `nvcc`：CUDA 13.0.48。
- `/usr/local/cuda`：指向 `/usr/local/cuda-13.0/`。

## 当前 Python / PyTorch

- Python：`/usr/bin/python3`。
- Python 版本：3.10.12。
- PyTorch：2.10.0+cu128。
- PyTorch CUDA：12.8。
- PyTorch GPU 可用：是。
- PyTorch 识别 GPU：NVIDIA GeForce RTX 5060 Laptop GPU。

注意：虽然系统 `nvcc` 是 CUDA 13.0，但 PyTorch wheel 使用 CUDA 12.8 运行时，这是当前机器已验证可用的组合，不要擅自修改。

## 当前 ROS2

- ROS 发行版：Humble。
- 已确认包：`rviz2`、`rqt`、`navigation2`、`nav2_bringup`、`sensor_msgs`、`tf2_ros`、`image_transport`。
- 用户函数：`ros2env`。
- Unity ROS2 独立工作区：`/home/ubuntu22/VLN/unity_ros2_ws`。
- 已克隆包：`ROS-TCP-Endpoint`，分支 `main-ros2`，包名 `ros_tcp_endpoint`。
- 已验证：`rosdep check --from-paths src --ignore-src` 显示系统依赖满足。
- 已构建：普通 `colcon build --packages-select ros_tcp_endpoint` 成功。
- 注意：当前环境下 `colcon build --symlink-install` 会因 Python setuptools editable/develop 兼容性失败，暂时不要使用 symlink 模式。
- 本地补丁：`/home/ubuntu22/VLN/unity_ros2_ws/src/ROS-TCP-Endpoint/ros_tcp_endpoint/default_server_endpoint.py` 已将退出时的 `rclpy.shutdown()` 改为仅在 `rclpy.ok()` 时调用，避免停止 endpoint 时重复 shutdown traceback。
- 构建备份：失败/旧构建生成物已移动到 `/home/ubuntu22/VLN/unity_ros2_ws/.build_backups/`，未直接删除。

### ROS2 启动方式

每次打开新终端，需要 ROS2 时执行：

```bash
ros2env
```

该函数会清理 Conda 相关变量，移除 Conda 路径，加载 `/opt/ros/humble/setup.bash`，并加载 Gazebo 相关环境。

### ROS2 验证命令

```bash
ros2env
ros2 pkg list | grep -E '^(rviz2|rqt|nav2_bringup|sensor_msgs|tf2_ros|image_transport)$'
rviz2 --help
rqt --help
```

### ROS-TCP-Endpoint 工作区构建记录

后续如需重新构建 ROS-TCP-Endpoint，使用：

```bash
ros2env
cd /home/ubuntu22/VLN/unity_ros2_ws
colcon build --packages-select ros_tcp_endpoint
source install/setup.bash
```

不要使用：

```bash
colcon build --packages-select ros_tcp_endpoint --symlink-install
```

原因：当前用户级 setuptools 对 ROS-TCP-Endpoint 的旧式 `setup.cfg` / editable 安装路径不兼容，会报 `error: option --editable not recognized`。

### ROS-TCP-Endpoint 启动方式

默认本机 Unity 连接本机 ROS2：

```bash
/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh
```

该脚本直接执行 colcon 安装后的 endpoint 可执行文件，避免 `ros2 run` 在脚本化测试中留下子进程。

如果 Unity 需要从其他机器连接本机 ROS2，把 `ROS_IP` 改成可被 Unity 访问的网卡 IP：

```bash
ROS_IP=0.0.0.0 ROS_TCP_PORT=10000 /home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh
```

端口验证：

```bash
ss -ltnp | grep 10000
```

## 当前代理

- 本机代理服务：`verge-mihomo` / Clash Verge。
- 监听端口：`127.0.0.1:7897`。
- 当前环境变量：`HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY` 已存在。

### 终端主动挂代理

如需显式指定代理，使用：

```bash
export HTTP_PROXY=http://127.0.0.1:7897/
export HTTPS_PROXY=http://127.0.0.1:7897/
export ALL_PROXY=socks://127.0.0.1:7897/
export NO_PROXY=localhost,127.0.0.1,192.168.0.0/16,10.0.0.0/8,172.16.0.0/12,::1
```

验证网络：

```bash
curl -I https://github.com
```

## Unity 推荐策略

- 当前使用项目内 Unity Hub 和项目内 Unity Editor，不依赖全局 `unityhub` 或 `unity` 命令。
- 推荐 Unity 版本：优先 Unity 2022.3 LTS 或后续稳定 LTS。
- 原因：UnitySensors 明确面向 Unity 2022.3 或更高版本；Unity Robotics Hub 也要求较新的 Unity Editor。
- 当前不要安装 Hub 首页推荐的 Unity 6.5；本项目先固定使用已验证的 `2022.3.62f1`，避免偏离师兄要求和 UnitySensors 兼容路线。
- 渲染管线：初期建议 Built-in 或 URP，不建议一开始上 HDRP。
- 传感器负载：初期相机 640x480 或 1280x720，LiDAR 5-10Hz、VLP-16/Mid360 级别。

## Unity 当前安装状态

- Unity Hub：项目内解包安装，路径 `/home/ubuntu22/VLN/tools/unityhub_extracted_3.20.1`。
- Unity Hub 启动脚本：`/home/ubuntu22/VLN/scripts/run_unityhub.sh`。
- Unity Editor：项目内安装，路径 `/home/ubuntu22/VLN/UnityEditors/2022.3.62f1/Editor/Unity`。
- Unity Editor 版本验证：`2022.3.62f1`。
- Unity Editor 许可证状态：Unity Personal 已激活；空工程创建探测已成功，退出码 `0`。
- Unity 许可证文件：`/home/ubuntu22/VLN/.unity_user/config/unity3d/Unity/licenses/UnityEntitlementLicense.xml`。
- 用户级应用菜单入口：`/home/ubuntu22/.local/share/applications/unityhub-vln.desktop`，显示名 `Unity Hub (VLN)`。
- Unity Hub 回调协议：`x-scheme-handler/unityhub` 和 `application/x-unityhub` 已注册到 `unityhub-vln.desktop`；`Exec` 必须保留 `%u`，否则浏览器登录回调 URI 会被丢弃。
- 说明：这是用户级桌面入口，不是系统级 apt 安装；不会写入 `/usr/bin` 或系统应用目录。
- 正式 Unity 工程：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`。
- 固定打开脚本：`/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh`。
- 已导入项目级 Unity 包：`com.unity.robotics.ros-tcp-connector`，来源为 `https://github.com/Unity-Technologies/ROS-TCP-Connector.git?path=/com.unity.robotics.ros-tcp-connector`，当前锁定 hash `c27f00c6cf750d2d0564349b3039d19aa3925e7c`。

### Unity Hub 保持登录态的使用规则

1. 以后打开 Unity 只使用应用菜单 `Unity Hub (VLN)`，或命令 `/home/ubuntu22/VLN/scripts/run_unityhub.sh`。
2. 不要从系统里其他 `unityhub` 命令、下载目录里的临时 AppImage/解包目录、浏览器随意打开的旧入口启动 Hub，否则可能使用另一套配置目录，表现为“又要重新登录”。
3. 不要删除 `/home/ubuntu22/VLN/.unity_user/`，这里保存本项目 Unity Hub 配置、账号数据库、许可证与缓存。
4. 不要在 Hub 右上角手动 Sign out；正常关闭窗口即可。
5. 如果 Hub 将来要求刷新登录，先固定代理节点并运行 `/home/ubuntu22/VLN/scripts/check_unity_login_network.sh`，确认浏览器和终端出口一致后再登录。

### 打开正式 Unity 工程

推荐命令：

```bash
/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh
```

这个脚本会使用项目内 Unity Editor `2022.3.62f1`，并复用 `/home/ubuntu22/VLN/.unity_user/` 下的账号、许可证、缓存和代理配置。不要直接双击其他 Unity 可执行文件打开该工程，避免许可证配置目录不一致。

### Unity 许可证对主线的影响

当前结论：没有 Unity 账号/许可证不能“完全没问题”推进 Unity 主线。可以继续推进 ROS2 侧、资料整理、脚本、Git 忽略规则、package 清单和工程结构规划；但创建/打开 Unity 工程、导入 Unity Package Manager 包、进入 Play 模式、生成相机图像和 LiDAR 点云闭环，都需要有效 Unity Editor 许可证。

本地探测命令：

```bash
/home/ubuntu22/VLN/UnityEditors/2022.3.62f1/Editor/Unity \
  -batchmode -nographics -quit \
  -createProject /home/ubuntu22/VLN/UnityProjects/_LicenseProbe_YYYYMMDD_HHMMSS \
  -logFile /home/ubuntu22/VLN/UnityProjects/_LicenseProbeLogs/create_project.log
```

历史失败结果：未登录/未激活前退出码 `1`，日志显示 `No valid Unity Editor license found. Please activate your license.`

当前通过结果：2026-08-13 登录并激活 Unity Personal 后，探测工程 `/home/ubuntu22/VLN/UnityProjects/_LicenseProbe_20260813_215209` 创建成功，包含 `Assets/`、`Packages/`、`ProjectSettings/`、`UserSettings/`，退出码 `0`。

### Unity Hub 登录故障排查

当前已知问题：Unity Hub 登录可能卡在安全检查或二次验证页面。浏览器曾显示 `Conversation Ip Violation`，Hub 日志显示 OAuth 回调后换 token 时 `ECONNRESET`。

判断原则：这不是账号密码本身错误，主要是 Unity 登录链路对 IP 一致性很敏感。Hub 内嵌窗口、系统浏览器、Hub 主进程、代理规则或节点只要出口不一致，就可能触发 Unity 风控或 token 交换失败。

重试前操作：

```bash
# 1. 先关闭 Unity Hub 和所有 Unity 登录网页。
# 2. 在 Clash/Mihomo 中固定同一个节点，优先启用全局/TUN 模式。
# 3. 终端检查代理出口。
curl -fsSL --max-time 12 --proxy http://127.0.0.1:7897 https://api.ipify.org

# 4. 浏览器访问 https://api.ipify.org，确认显示 IP 与终端一致。
# 5. 再启动 Unity Hub。
/home/ubuntu22/VLN/scripts/run_unityhub.sh
```

如果第 3 步终端 IP 和浏览器 IP 不一致，先不要继续点登录；应先调整代理规则，否则会重复触发 Unity 登录风控。

如果仍失败：暂时不要继续长时间消耗在 Hub 登录上；优先尝试直接用已安装 Unity Editor 创建本地工程，验证是否可以进入项目。如果 Editor 强制要求许可证，再回到 Unity Hub 登录问题。

进一步定位记录：截图中 `conversationIp=121.31.225.26`，`userIp=13.229.249.62`；本机显式代理出口检测为 `13.229.249.62`。这说明登录 conversation 创建阶段曾被规则路由成直连，而 authorize 阶段走了代理。该问题与 Unity Hub 是否系统级安装无直接关系；是 Unity 服务器看到同一个 OAuth 会话前后公网 IP 不一致。

已加固 `/home/ubuntu22/VLN/scripts/run_unityhub.sh`：强制设置大小写代理环境变量，并传入 Electron/Chromium `--proxy-server=http://127.0.0.1:7897`。登录前可执行：

```bash
/home/ubuntu22/VLN/scripts/check_unity_login_network.sh
```

若输出显示 Mihomo `mode` 为 `global`，显式代理出口稳定，且直连不可用/不用，则关闭旧 Unity Hub 和旧 Unity 登录网页后重新启动 Hub；不要继续使用旧的 `conversation` 页面。

### Unity 账号 2FA / 短信验证码处理

如果 Unity 登录卡在短信二次验证：

1. 不要连续频繁点击“重新发送短信验证码”，避免触发短信通道限流。
2. 如果网页端还能进入 Unity ID：打开 `id.unity.com`，进入 `Security`，在 `Two-factor authentication` 旁点击编辑，移除不想使用的 2FA 方法。
3. 如果无法收到短信但有 recovery codes：优先使用恢复码登录。
4. 如果既收不到短信也没有恢复码：提交 Unity Support / Customer Experience 工单，请求账号 2FA 重置或移除。
5. 项目主线不应长期阻塞在 Unity Hub 登录；若账号恢复耗时，优先尝试直接用本地 Unity Editor 创建 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`。

## 后续安装记录

暂无。本项目尚未执行任何系统包、Python 包或 Conda 包安装命令。

## 后续源码/工作区记录

- 2026-08-13：创建 `/home/ubuntu22/VLN/unity_ros2_ws`，克隆 `Unity-Technologies/ROS-TCP-Endpoint` 的 `main-ros2` 分支；完成依赖检查、普通 colcon 构建和 10000 端口短暂启动验收。
- 2026-08-13：为 ROS-TCP-Endpoint 添加本地退出补丁；重新构建后验证 endpoint 可监听 `127.0.0.1:10000`，停止后端口释放且无残留进程。
- 2026-08-13：定位 Unity Hub 登录卡安全检查问题；记录为代理出口/IP 一致性与 OAuth token 交换问题，不修改系统包、不安装 `libsecret`。
- 2026-08-13：加固 Unity Hub 启动脚本代理参数，并新增 `/home/ubuntu22/VLN/scripts/check_unity_login_network.sh` 用于登录前检查 Mihomo 运行模式、代理出口和 Unity API 连通性。
- 2026-08-13：记录 Unity 账号短信 2FA 验证码不接收问题；后续不再频繁重发短信，优先网页端关闭 2FA、恢复码或 Unity Support 工单。
- 2026-08-13：完成本地 Unity Editor 许可证探测；确认当前无有效许可证，不能直接创建 Unity 工程。
- 2026-08-13：修复 Unity Hub 浏览器登录回调：`unityhub-vln.desktop` 的 `Exec` 已从无参数改为 `/home/ubuntu22/VLN/scripts/run_unityhub.sh %u`。
- 2026-08-13：用户成功登录 Unity Hub；Unity Personal 许可证自动激活；重新运行 Editor 空工程创建探测成功。后续 Unity 主线可以继续创建正式工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`。
- 2026-08-13：正式 Unity 工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad` 已创建；`Packages/manifest.json` 已加入 ROS-TCP-Connector；批处理打开和包导入验证成功。新增 `/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh` 作为固定工程入口。
