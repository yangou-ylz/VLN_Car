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

## 2026-08-13：Unity smoke test 手工验证通过但 batch runner 不退出

- 现象：首次 Unity-ROS2 smoke test 中，ROS2 已收到 Unity `/unity/heartbeat`，Unity 也收到 ROS2 `/ros2/command`，但 Unity Editor 没按预期退出，最终被外层 `timeout` 杀掉，退出码 `124`。
- 环境：Unity Editor `2022.3.62f1`，batchmode/nographics，测试场景 `Assets/VLN/Scenes/ROS2SmokeTest.unity`。
- 根因：`VlnRos2SmokeTestRunner` 依赖 `EditorApplication.update` 在 Play Mode 中计时退出；实际 batch Play Mode 下该退出路径不可靠，导致测试本身通过但进程不退出。
- 解决方案：在运行时脚本 `VlnRos2SmokeTest.cs` 中加入仅 `Application.isBatchMode` 生效的自动退出逻辑，约 14 秒后调用 `UnityEditor.EditorApplication.Exit(0)`；手动打开 Unity 时不自动退出。
- 附带处理：首次 timeout 产生的 `mono_crash.mem.*.blob` 已移动到对应 `_SmokeTestLogs` 目录；`.gitignore` 已加入 `UnityProjects/VLN_Offroad/mono_crash*.blob`，避免崩溃转储误提交。
- 验收方式：重新运行 `/home/ubuntu22/VLN/scripts/run_ros2_unity_smoke_test.sh`，Unity 退出码 `0`，并输出 `VLN_ROS2_SMOKE_TEST_PASS`。
- 状态：已解决。

## 2026-08-13：`.gitignore` 过度忽略整个 UnityProjects

- 现象：正式 Unity 工程源文件也被 `UnityProjects/` 总规则忽略，虽然安全但会导致 `Assets/`、`Packages/`、`ProjectSettings/` 这些轻量源码无法提交。
- 根因：为了防止 Unity `Library/` 和日志爆仓，早期采用了整目录忽略。
- 解决方案：改为忽略 `UnityProjects/*`，再显式放行 `UnityProjects/VLN_Offroad/Assets/**`、`Packages/**`、`ProjectSettings/**`；继续忽略 `Library/`、`Logs/`、`UserSettings/`、探测工程和各类测试日志。
- 验收方式：`git status --short --ignored` 显示 `?? UnityProjects/`，同时显示 `!! UnityProjects/VLN_Offroad/Library/`、`!! UnityProjects/VLN_Offroad/Logs/`、`!! UnityProjects/VLN_Offroad/UserSettings/`。
- 状态：已解决。

## 2026-08-13：UnitySensors 导入后缺少 UGUI / NUnit / Test Framework

- 现象：导入 `com.frj.unity-sensors` 与 `com.frj.unity-sensors-ros` 后 Unity 编译失败，先报 `UnityEngine.UI.RawImage` 找不到，随后报包内 `Tests/Editor/*.cs` 中 `NUnit`、`TestFixtureAttribute`、`TestAttribute` 找不到。
- 环境：Unity Editor `2022.3.62f1`，正式工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`，UnitySensors hash `91698e3593abdb04baac022a670cc52fee027238`。
- 根因：UnitySensors 包引用 UGUI；同时包内测试 asmdef 引用 `UnityEngine.TestRunner`、`UnityEditor.TestRunner` 和 `nunit.framework.dll`，空工程默认没有这些项目级 UPM 依赖。
- 解决方案：在 `Packages/manifest.json` 中加入 `com.unity.ugui` `1.0.0` 与 `com.unity.test-framework` `1.1.33`。这只修改 Unity 工程项目依赖，不是系统包、Python 包或 Conda 包安装。
- 验收方式：重新执行 `/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh -batchmode -nographics -quit -logFile <log>`，重跑后退出码为 `0`，不再出现 `RawImage` / `NUnit` 编译错误。
- 状态：已解决。

## 2026-08-13：导入 Test Framework 后 Unity 首次批处理崩溃

- 现象：加入 `com.unity.test-framework` 后第一次批处理导入退出码 `134`，日志中出现 `pal_utilities.h:160: int ToFileDescriptor(intptr_t): Assertion fd < sysconf(_SC_OPEN_MAX)`，发生在 Bee/ILPP 编译阶段。
- 环境：Unity Editor `2022.3.62f1`，batchmode/nographics，日志 `/home/ubuntu22/VLN/UnityProjects/_ImportLogs/import_unitysensors_after_testframework_20260813_223237.log`。
- 根因判断：Test Framework、Burst、NUnit 等依赖已成功写入 `packages-lock.json`；崩溃发生在 Unity/Mono/Bee 编译管线，重跑后正常，判断为首次导入后的偶发 Editor 编译崩溃，而不是系统依赖缺失。
- 解决方案：未清理全局环境，未删除 Unity `Library/`；直接重跑同一批处理导入命令，第二次退出码 `0`。
- 验收方式：`/home/ubuntu22/VLN/UnityProjects/_ImportLogs/import_unitysensors_retry_20260813_223327.log` 显示 `Exiting batchmode successfully now!`。
- 状态：已解决；若复发，优先保留日志并重跑一次，不要先做大范围删除或重装。

## 2026-08-13：相机闭环脚本 topic 正则误判

- 现象：第一次运行 `/home/ubuntu22/VLN/scripts/run_unitysensors_image_smoke_test.sh` 时，ROS2 已成功收到 `/vln/front/image_raw` 图像，字段校验和 `ros2 topic hz` 都正常，但脚本最后报 `ros2_topic_list_missing_image_topic`。
- 根因：脚本中的 `rg` 校验把 `$IMAGE_TOPIC` 写成了需要匹配的字面量，导致真实 topic 存在时仍判失败。
- 解决方案：修正 `rg` 正则，直接使用变量展开后的 topic 值匹配 `ros2 topic list -t` 输出。
- 验收方式：重新运行脚本，输出 `VLN_UNITYSENSORS_IMAGE_SMOKE_TEST_PASS`。
- 状态：已解决。

## 2026-08-13：相机测试结束后 endpoint 记录 No more data available

- 现象：阶段 4 通过日志中，Unity 正常退出后 endpoint 日志出现 `[UnityEndpoint]: Exception: No more data available`，随后记录 `Disconnected from 127.0.0.1`。
- 根因判断：Unity batch 测试结束时主动关闭 TCP 连接，endpoint 把断开连接记录为异常日志；该日志出现在成功注册 `/vln/front/camera_info` 和 `/vln/front/image_raw` 且 ROS2 已收到图像之后。
- 解决方案：当前不修改 endpoint；把它作为非致命断开日志记录。只有在测试过程中连接提前断开、topic 未注册或 ROS2 收不到消息时，才作为真实故障继续排查。
- 验收方式：同一 run id `vln_image_20260813_224841` 中 `ros2_image_once.log` 输出 `VLN_UNITYSENSORS_IMAGE_MSG_OK`，总脚本输出 `VLN_UNITYSENSORS_IMAGE_SMOKE_TEST_PASS`。
- 状态：已记录，非阻塞。

## 2026-08-13：LiDAR 测试首次启动时 Unity stale lock 误报已有实例

- 现象：第一次运行 `/home/ubuntu22/VLN/scripts/run_unitysensors_lidar_smoke_test.sh` 时，Unity 日志报 `It looks like another Unity instance is running with this project open`，退出码 `134`，ROS2 未收到点云。
- 环境：Unity Editor `2022.3.62f1`，正式工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`，日志目录 `/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_lidar_20260813_225930`。
- 根因判断：进程检查没有实际 Unity Editor 正在打开该工程，但 Unity `Library` 中残留 `ArtifactDB-lock`、`SourceAssetDB-lock`，导致 Editor 误判工程被占用。
- 解决方案：没有删除整个 `Library/`，只把残留 lock 文件移动到失败 run 的 `stale_locks/` 目录保留现场，然后重新运行 LiDAR smoke test。
- 验收方式：后续 `/home/ubuntu22/VLN/scripts/run_unitysensors_lidar_smoke_test.sh` 输出 `VLN_UNITYSENSORS_LIDAR_SMOKE_TEST_PASS`，最近通过 run id 为 `vln_lidar_20260813_230736`。
- 状态：已解决；若复发，先检查是否真的有 Unity 进程，再只处理 lock 文件，不做大范围删除或重装。

## 2026-08-13：`ros2 topic bw` 输出格式与脚本预期不一致

- 现象：LiDAR 点云测试中 `ros2 topic bw /vln/lidar/points` 能输出带宽，但当前 ROS2 Humble 输出格式是 `KB/s from ... messages`，不是部分示例中的 `average:` 字段。
- 环境：ROS2 Humble，topic `/vln/lidar/points`，消息类型 `sensor_msgs/msg/PointCloud2`。
- 根因：`ros2 topic bw` 的 CLI 输出格式和 `ros2 topic hz` 不同，不能沿用 `average rate:` 这类正则。
- 解决方案：`run_unitysensors_lidar_smoke_test.sh` 中带宽验收改为匹配 `([KMGT]?B/s|MB/s) from [0-9]+ messages`，同时保留 `timeout 124` 但已有带宽输出时视为成功。
- 验收方式：最近 run id `vln_lidar_20260813_230736` 中 `ros2_pointcloud2_bw.log` 显示约 `0.6 MB/s`，总脚本输出 `VLN_UNITYSENSORS_LIDAR_SMOKE_TEST_PASS`。
- 状态：已解决。

## 2026-08-13：手工查看图像时 `rqt_image_view` 独立命令不存在

- 现象：用户执行 `rqt_image_view /vln/front/image_raw` 后终端报 `rqt_image_view：未找到命令`。
- 环境：ROS2 Humble；`ros2 pkg list` 能看到 `rqt_image_view` 包，`ros2 pkg executables rqt_image_view` 能看到 `rqt_image_view rqt_image_view`，但 `command -v rqt_image_view` 为空。
- 根因：当前安装形态提供了 ROS2 包内可执行入口，但没有提供独立 shell 命令。
- 解决方案：新增 `/home/ubuntu22/VLN/scripts/view_front_image.sh`，内部使用 `ros2 run rqt_image_view rqt_image_view /vln/front/image_raw`；文档不再要求直接执行 `rqt_image_view`。
- 验收方式：`/home/ubuntu22/VLN/scripts/check_manual_visualization_state.sh` 能列出 `rqt_image_view rqt_image_view`；图像查看使用 `/home/ubuntu22/VLN/scripts/view_front_image.sh`。
- 状态：已解决。

## 2026-08-13：手工 RViz 看不到点云

- 现象：用户打开 RViz2 后只看到网格，看不到 LiDAR 点云；截图中 RViz Fixed Frame 为 `laser_frame`，显示项为旧的 `LaserScan`、`RobotModel`、`Odometry`。
- 环境：当前 UnitySensors LiDAR 输出 topic 为 `/vln/lidar/points`，类型 `sensor_msgs/msg/PointCloud2`，frame 为 `lidar_link`；用户手工检查时 10000 端口未监听，`ros2 topic list -t` 只有 `/parameter_events` 和 `/rosout`。
- 根因：至少有三点同时存在：endpoint 未启动或 Unity 未点击 Play 导致没有 `/vln/lidar/points` topic；RViz 使用了旧默认配置 `laser_frame`；显示项选了 `LaserScan` 而不是 `PointCloud2`。
- 解决方案：新增 `/home/ubuntu22/VLN/scripts/check_manual_visualization_state.sh` 检查 endpoint 和 topic；新增 `/home/ubuntu22/VLN/config/vln_lidar_pointcloud.rviz` 与 `/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh`，固定 `Fixed Frame=map`、显示 `/vln/lidar/points` 的 `PointCloud2`，并临时发布 `map -> lidar_link` 静态 TF。
- 验收方式：先启动 endpoint，Unity 打开 LiDAR 场景并点击 Play；`check_manual_visualization_state.sh` 应看到 `/vln/lidar/points [sensor_msgs/msg/PointCloud2]`，再执行 `view_lidar_rviz.sh`。
- 状态：已解决。

## 2026-08-13：用户终端运行检查脚本时报 `rg: 未找到命令`

- 现象：用户执行 `/home/ubuntu22/VLN/scripts/check_manual_visualization_state.sh` 时，脚本在端口检查和 topic 检查处报 `rg: 未找到命令`，并导致 ROS2 topic list 出现 BrokenPipe。
- 环境：普通用户终端；ROS2 Humble；`ros2env` 清理后 PATH 中没有 `rg`。
- 根因：脚本依赖了 ripgrep，但项目约束没有安装 `rg`，且不能要求用户为这个小脚本安装系统包。
- 解决方案：将 `check_manual_visualization_state.sh` 和三个 smoke test 脚本中的 `rg` 调用全部改为系统默认存在的 `grep` / `grep -E` / `grep -F`。
- 验收方式：`bash -n scripts/*.sh` 通过；`check_manual_visualization_state.sh` 不再依赖 `rg`。
- 状态：已解决。

## 2026-08-13：RViz 订阅点云但报 `Frame [lidar_link] does not exist`

- 现象：用户使用新 RViz 配置后，显示项 `VLN LiDAR PointCloud2` 状态为 OK，topic 为 `/vln/lidar/points`，但 Global Status 报 `No tf data. Actual error: Frame [lidar_link] does not exist`，画面仍只看到网格。
- 环境：当前阶段只有 UnitySensors LiDAR 点云闭环，没有正式 TF 树。
- 根因：`PointCloud2.header.frame_id` 是 `lidar_link`，但 ROS2 侧没有任何节点发布 `map -> lidar_link` 或 `base_link -> lidar_link` 的 TF。RViz 需要 fixed frame 与点云 frame 之间存在 TF 才能稳定显示。
- 解决方案：修改 `/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh`，打开 RViz 前临时启动 `ros2 run tf2_ros static_transform_publisher --frame-id map --child-frame-id lidar_link`；RViz 配置改为 `Fixed Frame=map`。
- 验收方式：先启动 endpoint 并在 Unity LiDAR 场景点击 Play，再运行 `/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh`；RViz 不应再报 `Frame [lidar_link] does not exist`。
- 状态：已解决；后续导入小车后应替换为正式 TF 树。

## 2026-08-13：Unity ImageTest 的 Game 面板显示 `No cameras rendering`

- 现象：用户在 `UnitySensorsImageSmokeTest.unity` 点击 Play 后，rqt 能看到 `/vln/front/image_raw` 图像，但 Unity `Game` 面板显示 `Display 1 No cameras rendering`。
- 环境：Unity Editor `2022.3.62f1`，UnitySensors image smoke 场景。
- 根因：ROS 图像由 UnitySensors `RGBCameraSensor` 生成并发布，不等同于 Unity Game 面板的普通展示相机；旧场景里没有稳定的普通 Viewer Camera 供 Game 面板显示。
- 解决方案：在 `VlnUnitySensorsImageProjectSetup.cs` 中新增 `ImageSmokeTest_ViewerCamera`，并新增 `/home/ubuntu22/VLN/scripts/rebuild_unity_smoke_scenes.sh` 用于关闭 Unity 后批处理重建轻量测试场景。
- 验收方式：关闭 Unity，运行 `rebuild_unity_smoke_scenes.sh`，重新打开 `UnitySensorsImageSmokeTest.unity` 并点击 Play，Game 面板应显示 Viewer Camera 视角；rqt 继续使用 `/vln/front/image_raw` 验证传感器图像。
- 状态：已修复生成器，待用户关闭 Unity 后重建场景生效。

## 2026-08-14：Unity Editor 卡死无法退出

- 现象：用户反馈 Unity 卡死，无法从界面退出。
- 环境：Unity Editor `2022.3.62f1`，工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`。
- 根因判断：Unity 主进程和 AssetImportWorker 残留；SIGTERM 未能让 Editor 正常退出。强制结束后 `Library/ArtifactDB-lock` 与 `Library/SourceAssetDB-lock` 残留。
- 解决方案：先对 VLN Unity Editor/worker 进程发送 SIGTERM，未退出后只对这些进程发送 SIGKILL；随后把残留 lock 文件移动到 `/home/ubuntu22/VLN/UnityProjects/_ManualRecoveryLogs/.../stale_locks`，不删除整个 `Library/`。新增 `/home/ubuntu22/VLN/scripts/stop_unity_vln_project.sh` 用于后续一键恢复。
- 验收方式：`pgrep` 不再显示 VLN Unity Editor/worker；`Library` 根目录不再有 `ArtifactDB-lock`、`SourceAssetDB-lock`；随后 `rebuild_unity_smoke_scenes.sh` 可运行完成。
- 状态：已解决。

## 2026-08-14：并行运行两个 Unity batch smoke test 导致工程占用

- 现象：同时运行图像和点云 smoke test 时，图像测试报 `It looks like another Unity instance is running with this project open`，退出码 `134`；点云测试正常通过。
- 环境：Unity Editor `2022.3.62f1`；同一工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`。
- 根因：Unity 不允许同一个工程被两个 Editor 实例并行打开。并行测试会造成一个测试拿到工程锁，另一个测试直接失败。
- 解决方案：以后 Unity smoke test 必须顺序运行，不能并行运行。失败后若无 Unity 进程但有 `ArtifactDB-lock`、`SourceAssetDB-lock`，移动锁文件保留现场后再重跑。
- 验收方式：顺序运行 `/home/ubuntu22/VLN/scripts/run_unitysensors_lidar_smoke_test.sh` 和 `/home/ubuntu22/VLN/scripts/run_unitysensors_image_smoke_test.sh` 均通过；最近点云 run id `vln_lidar_20260813_235800`，最近图像 run id `vln_image_20260813_235944`。
- 状态：已解决。

## 2026-08-14：RViz PointCloud2 状态 OK 但画面仍只有网格

- 现象：用户在 Unity 软件内已经能打开相机视角和雷达视角；`check_manual_visualization_state.sh` 曾能看到 `/vln/lidar/points [sensor_msgs/msg/PointCloud2]`，RViz 左侧 `VLN LiDAR PointCloud2` 显示 `Status: OK`，但主窗口仍只看到网格。
- 环境：ROS2 Humble，UnitySensors LiDAR topic `/vln/lidar/points`，RViz 固定坐标系 `map`，临时 TF 为 `map -> lidar_link`。
- 根因判断：RViz 的 `Status: OK` 只说明显示项、topic 类型和 TF 当前没有明显配置错误，不保证有新点云帧正在发布。本地只读订阅 `/vln/lidar/points` 等待 8 秒未收到 `PointCloud2`，说明当时 Unity/endpoint 已不在实时发布点云，或 topic discovery 与实际消息流不同步。
- 解决方案：增强 `/home/ubuntu22/VLN/scripts/check_manual_visualization_state.sh`，在发现 `/vln/lidar/points` 后继续短时间等待一帧 `PointCloud2`，并复用字段校验脚本检查有效非零点；同时把 `/home/ubuntu22/VLN/config/vln_lidar_pointcloud.rviz` 的显示改为橙色大点，QoS `Reliability Policy` 改为 `Best Effort`，降低 RViz 订阅兼容性和可见性问题。
- 验收方式：`bash -n scripts/check_manual_visualization_state.sh scripts/view_lidar_rviz.sh` 通过；当前无 endpoint/Unity Play 时，检查脚本明确提示未发现 `/vln/lidar/points`，不会再把缺少 `/tf_static` 写成疑似错误。下一次用户启动 endpoint 并在 Unity LiDAR 场景点击 Play 后，该脚本应输出 `LiDAR 正在实时发布有效点云帧`，再打开 RViz 应看到橙色点云。
- 状态：已修复脚本和 RViz 配置；待用户在 Unity LiDAR Play 运行时复测。

## 2026-08-14：RViz 中点云像旋转扇区而不是稳定点云阵列

- 现象：用户已经能在 RViz 看到 `/vln/lidar/points`，但画面不是传统意义上一整圈稳定点云，而是几条橙色弧线随时间旋转，看起来像扇形激光扫描动画。
- 环境：UnitySensors VLP-16 scan pattern；`_pointsNumPerScan=7200`，`_frequency=5`，scan pattern 总 `size=57600`；RViz `PointCloud2` 原先 `Decay Time=0`。
- 根因：UnitySensors `RaycastLiDARSensor` 每次从 scan pattern 中取连续 `pointsNum` 个方向做 raycast，发布后把 `indexOffset` 向前推进。因此当前每条 `PointCloud2` 只包含 1/8 圈，5Hz 下约 1.6 秒扫完整个 360 度。RViz `Decay Time=0` 时只显示最新一帧，所以看起来像一个旋转的扇区，而不是累计后的一整圈点云。
- 解决方案：先只改 RViz 显示，不改 Unity 真实数据流。将 `/home/ubuntu22/VLN/config/vln_lidar_pointcloud.rviz` 中 PointCloud2 的 `Decay Time` 设为 `2` 秒，`Size (Pixels)` 调回 `3`，让 RViz 累计最近一整圈扫描，视觉上接近传统稳定点云阵列。
- 后续选项：如果后续算法需要每个 `PointCloud2` 消息本身就是完整 360 度扫描，可把 Unity 场景中的 `_pointsNumPerScan` 提高到 57600；代价是单帧点数和 ROS 带宽约变为当前 8 倍，应在进入越野场景后再按性能验证决定。
- 状态：已完成 RViz 显示修正；Unity 传感器真实发布策略暂不改变。

## 2026-08-14：阶段 6 越野 terrain 场景在 `-nographics` 下段错误

- 现象：首次运行 `/home/ubuntu22/VLN/scripts/run_offroad_terrain_smoke_test.sh` 时，Unity 场景生成成功，`/vln/front/image_raw` 和 `/vln/lidar/points` topic 已注册，但 Unity 进入 Play 后段错误退出，状态码 `139`。
- 环境：Unity Editor `2022.3.62f1`，阶段 6 场景 `VLNOffroadTerrainSmokeTest.unity`，UnitySensors `RGBCameraSensor`，脚本参数原为 `-batchmode -nographics`。
- 根因：Unity 日志堆栈显示崩溃发生在 `UnitySensors.Sensor.Camera.RGBCameraSensor/<UpdateSensor>d__2 -> UnityEngine.Camera.Render -> GfxDevice::DrawSharedGeometryJobs`。这说明 ROS2 topic 和传感器配置已经启动，但复杂场景中的 RGB 相机在无图形上下文批处理渲染路径触发 Unity 图形层段错误。
- 解决方案：先将 Unity Terrain 组件改为轻量程序化网格地形，降低场景复杂度；随后将阶段 6 自动验收脚本改为 `-batchmode`，不再使用 `-nographics`，保留图形上下文。最终阶段 6 自动验收通过。
- 验收方式：`/home/ubuntu22/VLN/scripts/run_offroad_terrain_smoke_test.sh` 输出 `VLN_OFFROAD_TERRAIN_SMOKE_TEST_PASS`，run id 为 `vln_offroad_20260814_004120`。随后顺序回归 `/home/ubuntu22/VLN/scripts/run_unitysensors_image_smoke_test.sh` 与 `/home/ubuntu22/VLN/scripts/run_unitysensors_lidar_smoke_test.sh` 均通过。
- 状态：已解决。后续凡是包含复杂 terrain + UnitySensors RGB 相机的自动验收，不要默认加 `-nographics`。
