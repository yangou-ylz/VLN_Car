# 项目记忆总览

本文件用于防止上下文丢失。每次继续工作前先读本文件，再读 `AGENTS.md`、`workflow.md`、`env.md` 和 `logs/issue_log.md`。

## 项目目标

- 构建 Unity3D 越野仿真环境。
- 在仿真中实现 VLN 感知层两个核心输入：相机图像和 3D LiDAR 点云。
- 通过 ROS2 Humble 输出标准 topic，供后续感知、建图、VLM/VLN 推理、规划模块使用。

## 当前用户环境事实

- 操作系统：Ubuntu 22.04.5 LTS。
- CPU：AMD Ryzen 9 8945HX，16 核 32 线程。
- GPU：NVIDIA GeForce RTX 5060 Laptop GPU，显存约 8GB。
- NVIDIA 驱动：580.173.02。
- `nvidia-smi` 显示 CUDA Runtime 支持：13.0。
- 系统 `nvcc`：CUDA 13.0.48，`/usr/local/cuda -> /usr/local/cuda-13.0/`。
- Python：`/usr/bin/python3`，Python 3.10.12。
- PyTorch：2.10.0+cu128，`torch.cuda.is_available()` 为 True，PyTorch CUDA 为 12.8。
- 内存：约 30GiB，总体可用约 19GiB。
- 磁盘：根分区约 1.5T，可用约 1.2T。
- ROS2：已有 Humble，且已有 `rviz2`、`rqt`、`navigation2/nav2_bringup`、`sensor_msgs`、`tf2_ros`、`image_transport`。
- 用户自定义函数：`ros2env`，用于清理 Conda 相关环境并加载 ROS2 Humble。
- 代理：本机 Clash/Mihomo 监听 `127.0.0.1:7897`，环境变量已有 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY`。
- Unity Hub：项目内用户级安装，应用菜单入口为 `Unity Hub (VLN)`，启动脚本为 `/home/ubuntu22/VLN/scripts/run_unityhub.sh`。
- Unity Editor：`/home/ubuntu22/VLN/UnityEditors/2022.3.62f1/Editor/Unity`，版本 `2022.3.62f1`，Unity Personal 许可证已激活并通过空工程创建探测。
- Unity 正式工程：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`，已导入 `com.unity.robotics.ros-tcp-connector`。

## 已知风险

- RTX 50 系列对 CUDA / PyTorch 版本匹配敏感，禁止擅自改动相关环境。
- ROS2 与 Conda 易冲突，ROS2 命令应通过 `ros2env` 进入干净环境。
- 当前 shell 已经存在 ROS2/PX4 相关 `PYTHONPATH`、`LD_LIBRARY_PATH`，后续验证时不要假设这是理想干净环境。
- Unity 资产、ROS2 build/install/log、rosbag 和外部资料很容易造成 git 爆仓，必须依赖 `.gitignore` 和仓库外资料库。
- 当前目录 `/home/ubuntu22/VLN` 暂未初始化为 git 仓库；`.gitignore` 仍先建立，防止后续初始化后误提交垃圾文件。
- 运行环境中曾出现敏感 token 环境变量；后续日志和文档禁止记录任何 token、密钥、账号凭据。

## 当前结论

- 这台机器可以做 Unity + ROS2 + 中等规模越野仿真。
- 8GB 显存是主要边界：初期建议 Built-in/URP、低中等地形复杂度、单相机或低分辨率全景相机、VLP-16/Mid360 级 LiDAR、5-10Hz 传感器频率。
- 暂不建议一开始上 HDRP、大面积高密度植被、VLS-128 高频点云、多路高分辨率相机或端到端大模型在线训练。

## 最近一次工作记录

- 2026-08-13：完成本机硬件/环境只读体检；建立项目长期约束、环境文档、工作流、问题日志和 `.gitignore` 初始版本。
- 2026-08-13：按 Unity-ROS2 主路线推进阶段 3 的 ROS2 侧准备；在 `/home/ubuntu22/VLN/unity_ros2_ws` 克隆 ROS-TCP-Endpoint `main-ros2` 分支；依赖检查通过；普通 `colcon build --packages-select ros_tcp_endpoint` 成功；短暂启动 endpoint 后确认 `127.0.0.1:10000` 可监听。未安装任何新包。
- 2026-08-13：发现 endpoint 停止时重复 `rclpy.shutdown()` 产生 traceback；对本地 ROS-TCP-Endpoint 做最小补丁并重构建；验证启动脚本可监听 `127.0.0.1:10000`，停止后端口释放且无残留进程。
- 2026-08-13：Unity Hub 已项目内解包到 `/home/ubuntu22/VLN/tools/unityhub_extracted_3.20.1`；Unity Editor `2022.3.62f1` 已项目内安装到 `/home/ubuntu22/VLN/UnityEditors/2022.3.62f1/Editor/Unity` 并通过版本验证；已创建用户级应用菜单入口 `Unity Hub (VLN)`。
- 2026-08-13：Unity Hub 用户级菜单入口已补充 `unityhub://` 回调协议；`scripts/run_unityhub.sh` 已把项目内 Unity Hub bin 目录加入 PATH，避免 Hub 内部找不到自身可执行文件。
- 2026-08-13：Unity Hub 登录卡安全检查已定位：浏览器曾报 `Conversation Ip Violation`，Hub 日志显示 OAuth 回调后 POST `https://api.unity.com/v1/oauth2/token` 发生 `ECONNRESET`；当前判断为 Unity 登录链路的代理出口/IP 一致性问题，而非账号注册本身问题。后续登录前固定同一节点，优先用 Clash/Mihomo 全局/TUN，浏览器与终端 `api.ipify.org` 出口必须一致。
- 2026-08-13：二次登录截图确认 `conversationIp` 为直连 IP、`userIp` 为代理 IP；已加固 Unity Hub 启动脚本并新增登录网络检查脚本。结论：系统级安装不是当前根因；必须丢弃旧登录 conversation，重启 Hub 后再试。
- 2026-08-13：Unity 登录进一步卡在短信 2FA；用户点击重新发送短信后手机不再收到新码。结论：不要继续频繁重发；如果能进 Unity ID 网页则在 Security 中移除 2FA；如果进不去则使用恢复码或提交 Unity Support 工单。项目主线可转为直接尝试本地 Unity Editor 创建工程。
- 2026-08-13：本地 Unity Editor 许可证探测完成：`-version` 可输出 `2022.3.62f1`，但 `-createProject` 空工程失败，日志显示 `No valid Unity Editor license found. Please activate your license.` 因此“完全无账号/无许可证”不能完整推进 Unity 主线；账号问题可短期绕开做 ROS2、资料、工程配置准备，但进入 Editor、导入包、Play 模式和传感器闭环需要有效 Unity 许可证。
- 2026-08-13：定位 `Launching Unity Hub` 后仍回登录页的问题：用户级 `.desktop` 协议处理器缺少 `%u`，导致浏览器 `unityhub://` OAuth 回调参数被丢弃。已改为 `Exec=/home/ubuntu22/VLN/scripts/run_unityhub.sh %u` 并重新注册 MIME。
- 2026-08-13：用户已成功登录 Unity Hub；Hub 日志显示 Unity Personal 许可证已激活，许可证文件位于 `/home/ubuntu22/VLN/.unity_user/config/unity3d/Unity/licenses/UnityEntitlementLicense.xml`。重新执行 Unity Editor `-createProject` 探测成功，退出码 `0`，已创建 `/home/ubuntu22/VLN/UnityProjects/_LicenseProbe_20260813_215209`。后续不要点击 Hub 推荐的 Unity 6.5 安装，主线继续使用已安装的 `2022.3.62f1`。
- 2026-08-13：正式 Unity 工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad` 已创建成功；`Packages/manifest.json` 已加入 `com.unity.robotics.ros-tcp-connector`；Unity 批处理导入退出码 `0`，`Packages/packages-lock.json` 锁定 Connector 到 Git hash `c27f00c6cf750d2d0564349b3039d19aa3925e7c`。新增固定打开脚本 `/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh`，该脚本复用项目内 Unity 配置、许可证和代理。
