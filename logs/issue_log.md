# 问题日志

记录格式：时间、现象、环境、根因、解决方案、验收方式、是否复发。

## 2026-08-13：环境保护要求建立

- 现象：用户已有多个成熟环境，ROS2 与 Conda 曾发生冲突，RTX 50 系列 CUDA/PyTorch 组合敏感。
- 根因：系统级依赖、Conda、ROS2、CUDA、PyTorch 容易互相污染。
- 解决方案：建立 `AGENTS.md`、`env.md`、`workflow.md`，明确禁止未经确认安装；ROS2 使用 `ros2env`；Python 新依赖必须走虚拟环境。
- 验收方式：文档已建立，后续执行前必须读取。
- 状态：已记录。

## 2026-08-13：git 垃圾文件风险

- 现象：Unity、ROS2、rosbag、模型资产和构建缓存容易产生大量文件，导致 git 爆仓。
- 根因：Unity `Library/`、ROS2 `build/ install/ log/`、rosbag `.db3/.mcap`、资产包和缓存默认体积大。
- 解决方案：建立 `.gitignore`，默认忽略 Unity 缓存、ROS2 构建产物、Python 缓存、rosbag、大型资产、下载资料库、密钥文件。
- 验收方式：后续初始化 git 后执行 `git status --ignored` 检查。
- 状态：已记录。

## 2026-08-13：敏感环境变量风险

- 现象：只读环境检查时发现 shell 环境中存在敏感 token 变量。
- 根因：开发环境把令牌暴露到了普通 shell 环境。
- 解决方案：后续日志、文档、命令输出禁止记录 token 明文；建议用户轮换相关令牌并改用安全凭据管理方式。
- 验收方式：不在仓库文件中写入任何 token。
- 状态：已记录，待用户自行处理令牌轮换。

## 2026-08-13：ROS setup 与 `set -u` 不兼容

- 现象：使用 `set -euo pipefail` 后 source `/opt/ros/humble/setup.bash` 报 `AMENT_TRACE_SETUP_FILES: 未绑定的变量`。
- 环境：Ubuntu 22.04，ROS2 Humble，bash 严格未定义变量模式。
- 根因：ROS2 setup 脚本内部访问了未预设变量，和 `set -u` 不兼容。
- 解决方案：ROS2 启动/构建脚本使用 `set -eo pipefail`，不启用 `set -u`。
- 验收方式：去掉 `-u` 后进入 ROS2 环境正常，继续执行 colcon 构建。
- 状态：已解决。

## 2026-08-13：ROS-TCP-Endpoint symlink 构建失败

- 现象：`colcon build --packages-select ros_tcp_endpoint --symlink-install` 失败，报 `error: option --editable not recognized`。
- 环境：ROS-TCP-Endpoint `main-ros2`，用户级 Python 3.10 / setuptools。
- 根因：`--symlink-install` 触发旧式 develop/editable 安装路径，和当前 setuptools 行为不兼容。
- 解决方案：改用普通 `colcon build --packages-select ros_tcp_endpoint`。
- 验收方式：普通构建成功，endpoint 可启动并监听 `127.0.0.1:10000`。
- 状态：已解决；后续不要使用 symlink 模式构建该包。

## 2026-08-13：`ros2 run` 短测试留下 endpoint 子进程

- 现象：用脚本短启动 endpoint 后杀掉父进程，`127.0.0.1:10000` 仍被 `default_server_endpoint` 子进程占用。
- 环境：ROS2 Humble，ROS-TCP-Endpoint 普通 colcon 构建。
- 根因：`ros2 run` 通过 ROS2 CLI 派生实际可执行文件，脚本化短测试时只杀父进程不够干净。
- 解决方案：启动脚本改为直接执行 `/home/ubuntu22/VLN/unity_ros2_ws/install/ros_tcp_endpoint/lib/ros_tcp_endpoint/default_server_endpoint`。
- 验收方式：修改后短启动、杀进程、再次检查 10000 端口应释放。
- 状态：已解决。

## 2026-08-13：ROS-TCP-Endpoint 停止时重复 shutdown traceback

- 现象：endpoint 正常监听，但停止进程时输出 `rclpy._rclpy_pybind11.RCLError: failed to shutdown: rcl_shutdown already called`。
- 环境：ROS-TCP-Endpoint `main-ros2`，ROS2 Humble。
- 根因：`default_server_endpoint.py` 在 executor 已经处理 shutdown 后仍无条件调用 `rclpy.shutdown()`。
- 解决方案：对本地克隆做最小补丁，将 `rclpy.shutdown()` 包裹为 `if rclpy.ok(): rclpy.shutdown()`。
- 验收方式：重新构建后短启动/停止 endpoint，不再出现重复 shutdown traceback，并且 10000 端口释放。
- 状态：已解决；重新构建后短启动/停止通过，端口释放且无 traceback。

## 2026-08-13：清理失败构建状态时避免直接删除

- 现象：本地构建状态受之前 symlink 失败影响，重新构建一度报 `error: option --uninstall not recognized`。
- 环境：`/home/ubuntu22/VLN/unity_ros2_ws` 本地 colcon 工作区。
- 根因：上一次失败构建留下的生成物状态干扰普通构建。
- 解决方案：没有直接删除生成物，而是移动到 `/home/ubuntu22/VLN/unity_ros2_ws/.build_backups/` 下保留现场，再重新构建。
- 验收方式：普通构建成功，endpoint 启停验证通过。
- 状态：已解决。

## 2026-08-13：外部资料库副本误出现在工作区

- 现象：收尾检查发现 `/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY` 出现在当前工作区内。
- 环境：当前项目仓库 `/home/ubuntu22/VLN`，外部资料库 `/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY`。
- 根因：资料下载/整理过程中出现了与外部资料库一致的工作区副本。
- 解决方案：先确认工作区副本与外部资料库完全一致，再将工作区副本移动到仓库外备份目录，保持工作区只放轻量项目文件。
- 验收方式：`find /home/ubuntu22/VLN -maxdepth 2 -type d -name "VLN_REFERENCE_LIBRARY"` 无输出。
- 状态：已解决。

## 2026-08-13：Unity Hub 注册/登录回调与内嵌注册页面问题

- 现象：用户在 Unity Hub 内嵌注册页面填写邮箱等信息后，点击创建账户按钮无响应；返回等其他按钮可响应。
- 环境：项目内 Unity Hub `3.20.1`，用户级菜单入口 `Unity Hub (VLN)`。
- 根因判断：菜单入口缺少 `unityhub://` 回调协议注册，且 Hub 日志出现 `Unable to find unity hub`；内嵌注册页面按钮无响应更可能是 Unity 登录网页/Electron WebView/网络策略兼容问题。
- 解决方案：给 `unityhub-vln.desktop` 注册 `x-scheme-handler/unityhub` 与 `application/x-unityhub`；在 `scripts/run_unityhub.sh` 中将项目内 Unity Hub `usr/bin` 加入 PATH。账号注册建议优先用系统浏览器完成，再回到 Hub 登录。
- 验收方式：`xdg-mime query default x-scheme-handler/unityhub` 返回 `unityhub-vln.desktop`。
- 状态：已处理，待用户用浏览器注册/登录验证。

## 2026-08-13：Unity Hub 登录卡在安全检查 / OAuth 回调失败

- 现象：用户已在网页端注册 Unity 账号并绑定手机号；本地 Unity Hub 登录时进入安全检查/二次验证页面，点击继续后反复卡住，并跳转浏览器；浏览器页面曾显示 `Conversation Ip Violation`，错误码 `132.189`。
- 环境：项目内 Unity Hub `3.20.1`，Unity Editor `2022.3.62f1`，代理端口 `127.0.0.1:7897`，系统代理模式为 GNOME `manual`，Unity Hub 通过 `/home/ubuntu22/VLN/scripts/run_unityhub.sh` 启动。
- 本地证据：Unity Hub 日志显示登录窗口已进入 `login.unity.com` / `id.unity.com` / `api.unity.com/v1/oauth2/authorize`，随后捕获 `unityhub://` 回调；但 `AuthService` 在向 `https://api.unity.com/v1/oauth2/token` 换 token 时出现 `ECONNRESET`，报 `Client network socket disconnected before secure TLS connection was established`。
- 根因判断：核心不是账号注册失败，而是 Unity 登录链路跨 Unity Hub 内嵌 WebView、系统浏览器、Hub 主进程 token 请求时网络出口不一致或直连被阻断。`Conversation Ip Violation` 表明 Unity 风控认为同一个登录 conversation 的创建 IP 与后续用户 IP 不一致；`ECONNRESET` 表明 Hub 主进程 token 请求没有稳定完成 TLS 连接。
- 非主因：`git-credential-libsecret` 构建失败只影响 Hub 持久保存 Git/token 凭据，不是安全检查卡住的直接原因；`public-cdn.cloud.unity3d.com/hub/prod/*.json` 404 使用 fallback 数据，也不是当前登录主因。
- 当前推荐解决方案：关闭 Unity Hub 和所有 Unity 登录页；固定 Clash/Mihomo 到同一节点，优先启用全局/TUN 模式，确保 Unity Hub、浏览器、Hub 主进程都走同一出口；不要在登录过程中切换节点；重新打开 Hub 触发新的登录 conversation。
- 验收方式：终端执行显式代理出口检查应稳定，例如 `curl -fsSL --max-time 12 --proxy http://127.0.0.1:7897 https://api.ipify.org`；浏览器访问 `https://api.ipify.org` 应与终端显示一致；重新登录后 Hub 右上角能显示账号，`accounts.db` 更新时间更新，Hub 不再报 `oauth2/token` 的 `ECONNRESET`。
- 状态：已解决。最终通过固定代理入口、修复 Unity Hub 协议回调 `%u`、重新完成登录流程后，Hub 成功收到 OAuth 回调并进入登录态。

补充排查：用户再次截图显示 `conversationIp=121.31.225.26`、`userIp=13.229.249.62`；本机显式代理出口检测为 `13.229.249.62`，Mihomo 配置文件曾为 `mode: rule`，运行态随后为 `global`。这进一步确认问题是旧 OAuth conversation 创建时被规则路由为直连，后续 authorize 走代理，导致 Unity 服务端拒绝。已加固 `scripts/run_unityhub.sh` 的代理变量和 Electron `--proxy-server` 参数，并新增 `scripts/check_unity_login_network.sh`。后续必须关闭旧 Hub/旧登录页，重新启动 Hub 生成新 conversation。

## 2026-08-13：Unity 账号短信 2FA 验证码不再接收

- 现象：用户多次触发 Unity 登录短信验证码后，点击重新发送短信验证码但手机不再收到新短信；之前几次短信较快到达。
- 根因判断：Unity 账号登录仍被二次验证阻塞；多次重发短信可能触发 Unity/短信通道/运营商侧限流或延迟。当前不能再依赖反复点击“重新发送”。
- 官方路径：若已能在网页进入 Unity ID，则进入 `id.unity.com` 的 `Security` 页面，在 `Two-factor authentication` 旁点击编辑，并移除对应 2FA 方法；若无法登录，则优先使用恢复码；没有恢复码且短信不可用时，需要提交 Unity Support 工单，请求协助移除/重置 2FA。
- 当前建议：停止频繁点击重发；先尝试网页端 Unity ID 登录和恢复码入口；若仍被短信卡住，提交账号支持工单，同时主线优先尝试直接使用本地 Unity Editor 创建工程，避免账号登录继续阻塞 Unity-ROS2 仿真闭环。
- 状态：已记录，待用户决定是否继续账号恢复或先绕过 Hub 推进本地工程。

## 2026-08-13：无有效 Unity Editor 许可证导致空工程创建失败

- 现象：执行 Unity Editor `-version` 能输出 `2022.3.62f1`；但执行 `-createProject` 创建空工程失败。
- 环境：Unity Editor `/home/ubuntu22/VLN/UnityEditors/2022.3.62f1/Editor/Unity`，探测目录位于 `/home/ubuntu22/VLN/UnityProjects/_LicenseProbe_*`。
- 根因：Unity Licensing Client 可连接，但无 access token、无 ULF license，日志显示 `No valid Unity Editor license found. Please activate your license.`
- 解决方案：需要激活 Unity Editor 许可证。Unity Personal 路线需要 Unity Hub 登录自动激活；若当前账号 2FA 卡住，可用恢复码、关闭 2FA、提交 Unity Support，或临时使用另一个可登录 Unity ID。
- 验收方式：重新执行 `-createProject` 能成功创建 `Assets/`、`ProjectSettings/` 等工程结构，并且不再出现 `No valid Unity Editor license found`。
- 状态：已解决。用户成功登录 Unity Hub 后，Unity Personal 许可证自动激活；重新运行 `-createProject` 探测成功，退出码 `0`，已创建 `/home/ubuntu22/VLN/UnityProjects/_LicenseProbe_20260813_215209`。

## 2026-08-13：浏览器显示 Launching Unity Hub 但 Hub 回到登录页

- 现象：用户短信验证通过后，浏览器显示 `Launching Unity Hub` 和 `link to login page`；点击后能弹回 Unity Hub，但 Hub 仍显示需要重新登录并再次要求验证码。
- 环境：用户级协议处理器 `unityhub-vln.desktop`，默认处理 `x-scheme-handler/unityhub`。
- 根因：`.desktop` 文件的 `Exec` 原为 `/home/ubuntu22/VLN/scripts/run_unityhub.sh`，缺少 `%u`。浏览器调用 `unityhub://...` 回调时，桌面系统能启动 Hub，但没有把登录回调 URI 参数传给启动脚本；Hub 没收到 OAuth code/state，只能回到未登录状态。
- 解决方案：将 `/home/ubuntu22/.local/share/applications/unityhub-vln.desktop` 的 `Exec` 改为 `/home/ubuntu22/VLN/scripts/run_unityhub.sh %u`，并重新设置 `xdg-mime default unityhub-vln.desktop x-scheme-handler/unityhub`。
- 验收方式：`xdg-mime query default x-scheme-handler/unityhub` 返回 `unityhub-vln.desktop`；`.desktop` 文件中 `Exec` 带 `%u`；用户重新点击浏览器 `Launching Unity Hub` 页面中的链接后，Hub 应收到回调并完成登录。
- 状态：已解决。日志显示新启动参数已携带 `unityhub://login/?code=...` 回调，随后 Hub 获取用户信息、激活 Unity Personal 许可证，并写入本地许可证文件。

## 2026-08-13：Unity Hub 登录态与许可证本地持久化

- 现象：用户已进入 Unity Hub，要求以后不要每次都重新走短信/浏览器回调流程。
- 环境：项目内 Unity Hub `3.20.1`，启动脚本 `/home/ubuntu22/VLN/scripts/run_unityhub.sh` 使用项目内 `XDG_CONFIG_HOME=/home/ubuntu22/VLN/.unity_user/config`。
- 当前证据：账号数据库 `/home/ubuntu22/VLN/.unity_user/config/unityhub/accounts.db` 已更新；许可证文件 `/home/ubuntu22/VLN/.unity_user/config/unity3d/Unity/licenses/UnityEntitlementLicense.xml` 已生成；Editor 空工程创建探测成功。
- 解决方案：以后只从 `Unity Hub (VLN)` 菜单入口或 `/home/ubuntu22/VLN/scripts/run_unityhub.sh` 启动；不要删除 `/home/ubuntu22/VLN/.unity_user/`；不要手动退出登录；保持同一代理入口用于必要的 token 刷新。
- 验收方式：`/home/ubuntu22/VLN/UnityEditors/2022.3.62f1/Editor/Unity -batchmode -nographics -quit -createProject <probe_dir> -logFile <log_file>` 退出码为 `0`，并创建 `Assets/`、`Packages/`、`ProjectSettings/`。
- 状态：已解决。若未来 Hub 再提示重新登录，优先检查是否从错误入口启动或 `.unity_user` 是否被清理。

## 2026-08-13：`.gitignore` 误忽略根目录 `logs/`

- 现象：`git status --ignored` 显示根目录 `logs/` 被忽略，导致 `issue_log.md`、`decision_log.md` 等项目记忆日志可能无法提交。
- 环境：仓库根目录 `/home/ubuntu22/VLN`，`.gitignore` 中 Unity 生成文件规则包含 `[Ll]ogs/`。
- 根因：Unity 生成日志目录规则过宽，匹配到了项目根目录的 `logs/`。
- 解决方案：在 `.gitignore` 后部追加例外规则 `!logs/` 和 `!logs/*.md`，保留项目记忆日志可追踪，同时 Unity 工程整体仍由 `UnityProjects/` 忽略。
- 验收方式：`git status --short --ignored` 显示 `?? logs/`，同时 `!! UnityProjects/` 仍被忽略。
- 状态：已解决。
