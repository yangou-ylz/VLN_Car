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

## 2026-08-14：阶段 7 首次 TF 联合验收缺少 CameraInfo 快照

- 现象：阶段 7 初次自动验收中，图像、点云和 TF 均能收到，但 topic 快照或 CameraInfo 等待窗口没有稳定捕获 `/vln/front/camera_info`，导致脚本没有形成完整 PASS。
- 环境：Unity Editor `2022.3.62f1`，场景 `VLNOffroadTerrainSmokeTest.unity`，ROS2 Humble，UnitySensors 相机 + LiDAR + `/tf`。
- 根因：阶段 7 自动验收新增了多个并发 ROS2 校验任务，原有脚本只校验 Image 和 PointCloud2，没有独立等待一次 CameraInfo 的最小字段校验；topic snapshot 与 Unity topic 注册时序之间存在短窗口。
- 解决方案：新增 `/home/ubuntu22/VLN/scripts/ros2_wait_for_camera_info_once.py`，并在 `/home/ubuntu22/VLN/scripts/run_vehicle_tf_smoke_test.sh` 中把 CameraInfo 作为独立必检项，而不是只依赖 topic list 快照。
- 验收方式：重新运行 `/home/ubuntu22/VLN/scripts/run_vehicle_tf_smoke_test.sh`，输出 `VLN_VEHICLE_TF_SMOKE_TEST_PASS`，run id 为 `vln_vehicle_tf_20260814_010331`。
- 状态：已解决。

## 2026-08-14：阶段 8 正式 RViz 不应再使用临时静态 TF

- 现象：阶段 5 的 `/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh` 会临时发布 `map -> lidar_link`，但阶段 7/8 已经由 Unity 发布正式 `/tf`。如果继续把旧脚本当作主查看方式，容易掩盖正式 TF 树是否真的存在。
- 环境：阶段 8 标准输出，TF 树为 `map -> base_link -> front_camera_optical_frame,lidar_link`。
- 根因：单 LiDAR smoke test 阶段没有车体 TF，只能临时补 `map -> lidar_link`；进入可控占位车体阶段后，这个补丁不再是主路线。
- 解决方案：新增 `/home/ubuntu22/VLN/scripts/view_vln_vehicle_rviz.sh` 和 `/home/ubuntu22/VLN/config/vln_vehicle_sensors.rviz`，正式 RViz 配置只依赖 Unity 发布的 `/tf`，不主动发布静态 TF。旧 `view_lidar_rviz.sh` 保留给阶段 5 单 LiDAR 排障。
- 验收方式：`/home/ubuntu22/VLN/scripts/run_standardized_outputs_smoke_test.sh` 输出 `VLN_STANDARDIZED_OUTPUTS_SMOKE_TEST_PASS`，rosbag 中包含 `/tf`、图像、CameraInfo 和点云四类 topic。
- 状态：已解决。

## 2026-08-14：Unity Play 后车体未收到目标仍自动运动

- 现象：用户在 Unity 主场景点击 Play 后，即使没有运行路径点控制器、没有向 `/vln/cmd_vel` 发布速度或目标位置，车体仍按旧自动轨迹前进。
- 环境：Unity Editor `2022.3.62f1`，主场景 `Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity`，运行时脚本 `VlnVehicleTfPublisher`。
- 根因：`VlnVehicleTfPublisher` 中 `m_AutopilotUntilFirstCommand` 默认值为 `true`，并且 `VlnOffroadTerrainProjectSetup.ConfigureTfPublisher()` 重建场景时也会把该字段写回 `true`。因此 Play 以后车体会在首次 ROS2 控制指令到达前自动巡航。
- 解决方案：将 `VlnVehicleTfPublisher.cs`、`VlnOffroadTerrainProjectSetup.cs` 和已保存的 `VLNOffroadTerrainSmokeTest.unity` 中的 `m_AutopilotUntilFirstCommand` 改为 `false`；扩展 `ros2_wait_for_vehicle_tf.py` 支持 `--max-base-delta` 静止验收；阶段 7/8 脚本改为无指令 6 秒内最大位移不超过 0.05m。
- 验收方式：`run_vehicle_tf_smoke_test.sh` 通过，run id `vln_vehicle_tf_20260814_021645`，`max_base_delta=0.000m`；`run_cmd_vel_control_smoke_test.sh` 通过，run id `vln_cmd_vel_control_20260814_021738`，发 `/vln/cmd_vel` 后位移约 `2.262m`；`run_waypoint_control_smoke_test.sh` 通过，run id `vln_waypoint_control_20260814_021829`，到达 2/2 个路径点；`run_standardized_outputs_smoke_test.sh` 通过，run id `vln_standardized_outputs_20260814_021919`，rosbag 包含图像、CameraInfo、点云和 TF。
- 状态：已解决。后续主线规定：Unity Play 只启动仿真与传感器发布，车体运动必须来自 ROS2 `/vln/cmd_vel` 或其上层路径点/导航/VLN 控制器。

## 2026-08-14：阶段 12 候选场景异步截图未落盘

- 现象：首次运行 `/home/ubuntu22/VLN/scripts/run_offroad_asset_candidate_smoke_test.sh` 时，ROS2 图像、CameraInfo、点云和 TF 全部通过，但脚本最后报 `asset_candidate_screenshot_missing`。
- 环境：Unity Editor `2022.3.62f1`，候选场景 `Assets/VLN/Scenes/VLNOffroadAssetCandidate.unity`，运行时脚本 `VlnOffroadAssetCandidateSmokeTest.cs`。
- 根因：`ScreenCapture.CaptureScreenshot()` 是异步截图接口，batch 自动退出窗口内不保证 PNG 已完成写盘；传感器闭环实际已经正常。
- 解决方案：将截图实现改为用 `Offroad_ViewerCamera` 同步渲染到 `RenderTexture`，再用 `Texture2D.ReadPixels()` 和 `File.WriteAllBytes()` 写 PNG。这样截图成为确定性产物。
- 验收方式：重新运行 `/home/ubuntu22/VLN/scripts/run_offroad_asset_candidate_smoke_test.sh`，输出 `VLN_OFFROAD_ASSET_CANDIDATE_SMOKE_TEST_PASS`；截图文件为 `/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_offroad_asset_candidate_20260814_033514/vln_offroad_asset_candidate_screenshot.png`。
- 状态：已解决。

## 2026-08-14：控制面板 smoke test 遇到 8765 端口已占用

- 现象：完整资产升级回归第一次通过时，`control_panel.log` 中出现 `OSError: [Errno 98] Address already in use` traceback，但 HTTP 客户端仍通过，因为旧控制面板已经在 `127.0.0.1:8765` 运行。
- 环境：`/home/ubuntu22/VLN/scripts/run_control_panel_smoke_test.sh`，本地中文控制面板默认端口 `8765`。
- 根因：测试脚本没有区分“端口被已有可用控制面板占用”和“端口异常占用”，直接尝试再启动一个面板进程，导致重复 bind 报错。
- 解决方案：修改脚本：如果 `8765` 已监听且 `/api/status` 可响应，则复用已有控制面板，不再启动新进程，也不在 cleanup 中杀掉用户正在使用的面板；如果端口占用但不响应 `/api/status`，才作为错误退出。
- 验收方式：端口已有面板时重新运行 `/home/ubuntu22/VLN/scripts/run_control_panel_smoke_test.sh`，输出 `panel_already_listening=true` 和 `VLN_CONTROL_PANEL_SMOKE_TEST_PASS`，日志只记录“复用已启动的控制面板”，无 traceback。完整回归 `vln_asset_baseline_20260814_033514` 输出 `VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`。
- 状态：已解决。

## 2026-08-14：Husky 车体候选首次编译缺少运行时命名空间引用

- 现象：首次运行 `/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh` 时，Unity 在编译阶段停止，报 `VlnOffroadVehicleCandidateSmokeTest` 类型找不到，所有 ROS2 等待脚本随后因 Unity 未启动而退出。
- 环境：Unity Editor `2022.3.62f1`，新增 Editor 脚本 `VlnOffroadVehicleCandidateProjectSetup.cs`，新增运行时脚本 `VlnOffroadVehicleCandidateSmokeTest.cs`。
- 根因：Editor 脚本位于 `VLN.Editor` 命名空间，运行时脚本位于 `VLN.ROS2` 命名空间；新增场景生成器调用运行时组件时缺少 `using VLN.ROS2;`。
- 解决方案：在 `VlnOffroadVehicleCandidateProjectSetup.cs` 中补充 `using VLN.ROS2;`，不改 Unity 包、不安装新依赖。
- 验收方式：重新运行 `/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh` 输出 `VLN_OFFROAD_VEHICLE_CANDIDATE_SMOKE_TEST_PASS`；随后完整回归 `/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh` 输出 `VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`，run id `vln_asset_baseline_20260814_040515`。
- 状态：已解决。

## 2026-08-14：Unity Game 视图中 Husky 候选车体和场景显得过糊

- 现象：用户截图中 `VLNOffroadVehicleCandidate.unity` 的 Game 视图显示车体和场景像素块明显，用户反馈“这个是车吗、太糊、精细度不够”。
- 环境：Unity Editor `2022.3.62f1`，候选场景 `Assets/VLN/Scenes/VLNOffroadVehicleCandidate.unity`，Game 视图右上方 Scale 显示为 `10x`。
- 根因：第一，Unity Game 视图 Scale=`10x` 会把渲染画面放大十倍，任何 640x480 或 1280x720 图像都会被像素级放大成马赛克；第二，Husky ROS description mesh 是工程/仿真低多边形模型，不是高清游戏级车模，细节上限本来就有限。
- 解决方案：在 `VlnOffroadVehicleCandidateProjectSetup.cs` 中新增近距离 `VehicleCandidate_GameCamera` 作为候选场景默认 Game 展示相机；仅在车体候选场景中把 `RGBCameraSensor` 分辨率从 `640x480` 提高到 `1280x720`，并同步更新 `VlnOffroadVehicleCandidateSmokeTest.cs` 和验收脚本的 Image/CameraInfo 校验尺寸。
- 验收方式：重新运行 `/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh`，run id `vln_offroad_vehicle_candidate_20260814_095155`，输出 `width=1280`、`height=720`、`VLN_UNITYSENSORS_IMAGE_MSG_OK` 和 `VLN_UNITYSENSORS_CAMERA_INFO_MSG_OK`；自动截图显示车体近距离可见。
- 状态：已解决显示/分辨率问题；模型本身仍是低多边形资产。若用户要求更真实外观，下一步应单独筛选高清 UGV/越野车资产。

## 2026-08-14：Husky 车体候选姿态错误，轮子平躺、车身像侧翻

- 现象：用户截图显示 Husky 视觉候选完全不像正常小车：车体近似侧翻，轮子平放在地面，整体姿态明显错误。
- 环境：Unity Editor `2022.3.62f1`，候选场景 `Assets/VLN/Scenes/VLNOffroadVehicleCandidate.unity`，Husky mesh 来源为 ROS description 的 `.dae` 视觉件。
- 根因：ROS mesh 使用 ROS 坐标语义，Unity 使用另一套坐标语义；首次导入只处理了 yaw 和位置近似转换，漏掉完整坐标基变换。第一次补 `RosYawToUnityRotation()` 后轮子虽然立起来，但截图仍能看到底盘下侧，用户判断像“四脚朝天”，说明 Unity DAE 导入后的可见 mesh 还需要额外 upright correction。
- 解决方案：先在 `Assets/VLN/Editor/VlnOffroadVehicleCandidateProjectSetup.cs` 中新增 `RosYawToUnityRotation()`，用 ROS 局部 x/y/z 轴向量构造 Unity rotation matrix；随后对每个 Husky mesh 实例追加 `Quaternion.AngleAxis(180f, Vector3.right)`，只翻转视觉网格本身，不改传感器 rig、`/tf` 或 `/vln/cmd_vel` 控制接口。
- 验收方式：关闭 Unity 后清理 stale lock，重新运行 `/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh`。最终通过 run id `vln_offroad_vehicle_candidate_20260814_101556`，近景截图 `UnityProjects/_SmokeTestLogs/vln_offroad_vehicle_candidate_20260814_101556/vln_offroad_vehicle_candidate_detail_screenshot.png` 显示黄色上盖在上、四个轮子竖直贴地；图像 `1280x720`、CameraInfo `1280x720`、点云 `7200` 点/帧、TF 静止验证均通过。
- 状态：已解决。当前车体姿态已正过来，但 Husky mesh 仍是低多边形工程模型，不是高清游戏级外观。

## 2026-08-14：用户关闭 Unity 后仍残留工程 lock 文件

- 现象：用户手工关闭 Unity 后请求验证；本地检查没有 VLN Unity Editor/worker 进程，但 `Library/ArtifactDB-lock` 与 `Library/SourceAssetDB-lock` 仍存在。
- 环境：Unity 工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`。
- 根因判断：Unity Editor 已退出，但上次打开工程后未清理完 Library lock 文件；如果不处理，下一次 batch smoke test 或手工打开可能误报工程被占用。
- 解决方案：运行 `/home/ubuntu22/VLN/scripts/stop_unity_vln_project.sh`，只移动 stale lock 到 `/home/ubuntu22/VLN/UnityProjects/_ManualRecoveryLogs/stop_unity_20260814_101116/stale_locks/`，不删除整个 `Library/`，不改系统环境。
- 验收方式：`pgrep` 未发现 VLN Unity 进程；`find UnityProjects/VLN_Offroad/Library -maxdepth 1 -type f -name '*lock*'` 无输出；随后车体候选 smoke test 通过，run id `vln_offroad_vehicle_candidate_20260814_101146`。
- 状态：已解决。

## 2026-08-14：Unity Game / 全局地图视角不能拖动

- 现象：用户在 Unity 里看全局地图或候选车体时，不能像正常地图一样随便拖动视角，怀疑视角被锁死。
- 环境：Unity Editor `2022.3.62f1`，主场景/地图候选/车体候选的 `Game` 标签页；展示相机为 `Offroad_ViewerCamera` 或 `VehicleCandidate_GameCamera`。
- 根因：不是故意锁死。Unity 的 `Scene` 标签页是编辑器自由视角，天然支持拖动；`Game` 标签页显示的是运行时 Camera 画面，原来这些展示相机只是固定相机，没有挂输入控制器，所以拖动鼠标不会改变视角。
- 解决方案：新增运行时脚本 `Assets/VLN/Scripts/VlnRuntimeMapCameraController.cs`，支持左/右键拖动旋转、中键拖动平移、滚轮缩放、右键按住时 WASD/QE 移动；在 `VlnOffroadTerrainProjectSetup.cs` 与 `VlnOffroadVehicleCandidateProjectSetup.cs` 中给展示相机挂载控制器；脚本还通过 `RuntimeInitializeOnLoadMethod` 自动给当前已打开场景中的展示相机补挂控制器，避免必须重建场景后才能用。
- 验收方式：当前用户还开着 Unity Editor，自动 batch 验收因同一工程被占用而未运行；已检查 `Logs/AssetImportWorker*.log` 未发现 `error CS`、`Compilation failed` 或 `Scripts have compiler errors`。用户当前窗口等 Unity 编译完成后点击 Play，可直接在 `Game` 标签页测试拖动。
- 状态：已实现，待用户手工拖动验证；关闭 Unity 后可再运行 `/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh` 做自动编译/传感器回归。

## 2026-08-15：AgileX `ugv_gazebo_sim` 完整 git clone 过慢超时

- 现象：按师兄链接克隆 `agilexrobotics/ugv_gazebo_sim` 时，即使设置 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY`，完整 `git clone --depth=1` 仍在 120 秒后超时。
- 环境：本地代理 `127.0.0.1:7897`，目标仓库 `https://github.com/agilexrobotics/ugv_gazebo_sim.git`，缓存目录 `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles`。
- 根因判断：完整仓库克隆连接/对象传输不稳定；但 GitHub raw/API 经代理可正常下载，说明不是完全没挂代理，而是完整 git clone 对当前节点和仓库体积不友好。
- 解决方案：未删除半截克隆，将其移动到 `VLN_ASSETS_CACHE/vehicles/ugv_gazebo_sim.partial_*` 保留现场；改用 GitHub API + 代理只下载 `scout/scout_description` 子目录，共 40 个文件、约 94.8MB。
- 验收方式：本地存在 `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles/ugv_gazebo_sim_scout_description_raw/DOWNLOAD_SUMMARY.json`，记录 commit `27633a956c845903ee630538afeb17fe70afdd84`、file_count `40`、bytes_total `94750231`。
- 状态：已解决；后续如果只需要某个 ROS description 包，优先轻量下载子目录，不默认完整 clone 大仓库。

## 2026-08-15：`scout_v2.xacro` 直接展开失败，找不到 `scout_description`

- 现象：执行 ROS2 Humble 的 `xacro scout_v2.xacro` 时报 `PackageNotFoundError: package 'scout_description' not found`。
- 环境：Scout 描述包只下载到 `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles/ugv_gazebo_sim_scout_description_raw/scout/scout_description`，没有安装进 ROS2 workspace 或 ament index。
- 根因：原始 xacro 使用 `$(find scout_description)` 解析 include 和 Gazebo 文件；ROS2 `xacro` 会从 ament index 查找 package，本地缓存目录不是已安装 ROS2 package。
- 解决方案：不安装任何包；在缓存 `generated/staging` 下创建临时展开副本，把 `$(find scout_description)/urdf/` 指向 staging，把 mesh 路径指向本地缓存，然后生成 `generated/scout_v2.urdf`。
- 验收方式：`generated/scout_v2.urdf` 已生成，体检显示 6 个 link、5 个有效 joint、6 个 collision、5 个 inertial；引用的 `base_link.dae` 和 `wheel_type1.dae` 均存在。
- 状态：已解决；正式 Unity 导入前仍需决定是使用 staging 展开 URDF，还是把 `scout_description` 复制到 Unity 资产目录后统一修正 mesh 路径。

## 2026-08-15：Scout V2 缓存目录出现重复轻量副本

- 现象：排查 GitHub 下载时额外产生了 `VLN_ASSETS_CACHE/vehicles/agilex_scout_v2` 轻量副本，与正式缓存 `VLN_ASSETS_CACHE/vehicles/ugv_gazebo_sim_scout_description_raw` 同时存在，容易造成后续路径口径混乱。
- 环境：阶段 13 Scout V2 URDF/STL 物理车体路线，本地缓存目录 `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles`。
- 根因：完整 git fetch 超时后，先手工下载了 `scout_v2.xacro`、include 文件和两个实际引用的 `.dae` mesh；随后发现标准缓存目录已存在完整 `scout/scout_description` 子目录和 `DOWNLOAD_SUMMARY.json`。
- 解决方案：保留正式缓存作为唯一后续入口；将重复轻量副本移动到 `VLN_ASSETS_CACHE/vehicles/_scratch_duplicates/agilex_scout_v2_*`，不删除现场。
- 验收方式：`find VLN_ASSETS_CACHE/vehicles -maxdepth 2 -type d` 显示正式入口为 `ugv_gazebo_sim_scout_description_raw`，重复副本只在 `_scratch_duplicates` 下。
- 状态：已解决；后续阶段 13 全部使用 `ugv_gazebo_sim_scout_description_raw/generated/scout_v2.urdf` 和其 `scout/scout_description` 资产目录。

## 2026-08-15：URDF Importer 触发 Unity UPM 写入 home 缓存失败

- 现象：加入 `com.unity.robotics.urdf-importer` 后，控制面板回归中的 Unity 批处理退出码为 `1`，日志显示 `EROFS: read-only file system, mkdir '/home/ubuntu22/.config/unity3d/cache/npm/packages.unity.com/...'`。
- 环境：Unity Editor `2022.3.62f1`，工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`，当前执行环境只允许写 `/home/ubuntu22/VLN` 和 `/tmp`。
- 根因：URDF Importer 依赖 `com.unity.editorcoroutines`，Unity Package Manager 尝试把 npm registry 缓存写到默认 home 配置目录，而不是项目内 `.unity_user/cache`。
- 解决方案：在 `/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh` 中设置 `UPM_CACHE_PATH`、`UPM_GIT_LFS_CACHE_PATH`、`UPM_NPM_CACHE_PATH` 到 `/home/ubuntu22/VLN/.unity_user/cache/upm/...`；不安装系统包，不修改全局 Unity 配置。
- 验收方式：重新运行 `/home/ubuntu22/VLN/scripts/run_control_panel_smoke_test.sh`，run id `vln_control_panel_20260815_183918`，输出 `VLN_CONTROL_PANEL_SMOKE_TEST_PASS`。
- 状态：已解决；后续所有 Unity 批处理必须通过 `scripts/open_unity_vln_project.sh` 入口。

## 2026-08-15：控制面板测试退出时 ROS 上下文失效导致假 traceback

- 现象：控制面板日志在测试结束时出现 `ExternalShutdownException`，随后 cleanup 发布 zero Twist 报 `publisher's context is invalid`。
- 环境：`/home/ubuntu22/VLN/scripts/vln_control_panel.py`，ROS2 Humble，控制面板 smoke test 由外层脚本结束进程。
- 根因：外层测试结束时 ROS2 上下文可能已经 shutdown，cleanup 仍尝试发布停止指令和销毁 ROS 对象，导致非业务失败的 traceback。
- 解决方案：`publish_zero()` 在 `rclpy.ok()` 为 false 时直接返回，发布异常时 break；cleanup 销毁 ROS 对象加保护；主循环捕获 `rclpy.executors.ExternalShutdownException`。
- 验收方式：`python3 -m py_compile scripts/vln_control_panel.py` 通过；`run_control_panel_smoke_test.sh` 输出 `VLN_CONTROL_PANEL_SMOKE_TEST_PASS`，无该 cleanup traceback。
- 状态：已解决。

## 2026-08-15：Scout V2 URDF 首次 Unity 导入姿态竖起

- 现象：Scout V2 URDF 候选第一次自动截图显示车体竖起来，四轮在侧面，视觉上不符合正常小车姿态；虽然 URDF link/joint/collision 计数和 ROS2 topic 都通过，但不能算姿态验收通过。
- 环境：Unity URDF Importer `v0.5.2`，Scout V2 DAE 的 `<up_axis>` 为 `Z_UP`，候选场景 `VLNOffroadScoutUrdfCandidate.unity`。
- 根因：`VlnOffroadScoutUrdfCandidateProjectSetup.cs` 初次使用 `ImportSettings.axisType.zAxis`，URDF Importer 的坐标修正与 Scout DAE 的 Z-up 导入叠加后导致整体车体竖起。
- 解决方案：将 `ImportSettings.chosenAxis` 改为 `ImportSettings.axisType.yAxis`，重新导入场景，并保持旧 ROS2 相机、LiDAR、TF 和 `/vln/cmd_vel` 接口不变。
- 验收方式：`/home/ubuntu22/VLN/scripts/run_scout_urdf_candidate_smoke_test.sh` 输出 `VLN_SCOUT_URDF_CANDIDATE_SMOKE_TEST_PASS`，最终 run id `vln_scout_urdf_candidate_20260815_185336`；截图显示车身平放、四轮竖直贴地，导入尺寸约 `0.700 x 0.351 x 0.930m`。
- 状态：已解决；后续 Scout V2 Unity 导入不要改回 `zAxis`。

## 2026-08-15：Scout URDF 批处理后残留 Unity Library lock

- 现象：一次 Scout URDF smoke test 通过后，紧接着再次运行 Unity batch 报 `It looks like another Unity instance is running with this project open`，但 `pgrep` 没有真实 Unity Editor 进程。
- 环境：Unity 工程 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`，残留文件为 `Library/ArtifactDB-lock` 和 `Library/SourceAssetDB-lock`。
- 根因判断：Unity 批处理退出后偶发没有清理 Library lock，导致下一次 batch 误判工程被占用。
- 解决方案：继续使用项目已有 `/home/ubuntu22/VLN/scripts/stop_unity_vln_project.sh`，只移动 stale lock 到 `_ManualRecoveryLogs`，不删除整个 `Library/`；新增的 Scout 静态/控制脚本在无真实 Unity 进程但发现 lock 时自动调用该恢复脚本。
- 验收方式：`run_scout_urdf_cmd_vel_smoke_test.sh` 启动时自动移动 stale lock，并最终输出 `VLN_SCOUT_URDF_CMD_VEL_SMOKE_TEST_PASS`，run id `vln_scout_urdf_cmd_vel_20260815_185425`。
- 状态：已解决；若其他旧脚本遇到相同假占用，先运行 `scripts/stop_unity_vln_project.sh`。

## 2026-08-15：`.gitignore` 外部资产保护被全局 Assets 放行规则压过

- 现象：`git check-ignore` 显示 Scout 重复目录 `ScoutUrdfPhysics/scout_description/...` 和原始 `scout_v2.urdf` 没有按预期被外部资产保护规则忽略，而是被 `!UnityProjects/VLN_Offroad/Assets/**` 放行。
- 环境：`.gitignore` 中 Unity 工程源码放行规则和 `Assets/VLN/ExternalAssets` 保护规则同时存在。
- 根因：外部资产默认忽略规则只写了 `ExternalAssets/*`，不能压住前面递归放行的 `Assets/**` 深层文件。
- 解决方案：将默认忽略规则改为 `UnityProjects/VLN_Offroad/Assets/VLN/ExternalAssets/**`，再显式放行 Kenney、Husky 和 Scout 已审子集；Scout 只放行正式 Unity 导入入口、mesh、默认材质、Cylinder collision asset 和 Reference 元数据。
- 验收方式：`git check-ignore -v` 显示 `scout_description/...` 与 `scout_v2.urdf` 被忽略，`Materials/Default.mat` 与 `meshes/Cylinder.asset` 被放行。
- 状态：已解决。

## 2026-08-15：URDF Runtime 导入触发 Assimp `libdl.so` 异常

- 现象：尝试使用 URDF Importer 的 runtime 导入路径时，Unity 日志出现 Assimp 相关 `DllNotFoundException: libdl.so` / fallback handler 信息，DAE mesh 不能按预期稳定实例化。
- 环境：Unity Editor `2022.3.62f1`，`com.unity.robotics.urdf-importer` `v0.5.2`，Scout V2 的 `base_link.dae` 与 `wheel_type1.dae`。
- 根因判断：`UrdfRobotExtensions.CreateRuntime` 走运行时 mesh 导入路径，会触发 Assimp 动态库加载问题；这不是 ROS2、CUDA、PyTorch 或 xacro 问题。
- 解决方案：不通过系统安装或修改 Unity Editor 解决；改用 Editor 导入路径 `UrdfRobotExtensions.Create(... forceRuntimeMode:false)`，让 Unity 先把 DAE 作为资产导入，再由 URDF Importer 实例化。日志里仍可能出现一次 `libdl.so` fallback 信息，但不阻断当前 Editor 导入结果。
- 验收方式：Scout 静态验收 `vln_scout_urdf_candidate_20260815_185336` 通过，控制验收 `vln_scout_urdf_cmd_vel_20260815_191235` 通过，导入后最新完整基线 `vln_asset_baseline_20260815_191337` 通过。
- 状态：已解决；后续不要再把 `CreateRuntime` 作为 Scout DAE 导入主线。

## 2026-08-15：旧 ROS2 验收脚本默认写 `/home/ubuntu22/.ros/log` 导致回归失败

- 现象：完整资产基线回归 `vln_asset_baseline_20260815_192846` 和 `vln_asset_baseline_20260815_192954` 首轮失败。第一次是 ROS-TCP-Endpoint 启动时报 `Failed opening file /home/ubuntu22/.ros/log/... Read-only file system`；第二次是 ROS2 Python 字段校验脚本 `rclpy.init()` 同样尝试写 `/home/ubuntu22/.ros/log`。
- 环境：当前执行环境只允许写 `/home/ubuntu22/VLN` 和 `/tmp`；项目约束要求工作目录都在 `/home/ubuntu22/VLN` 内。
- 根因：部分旧 smoke test、手工查看脚本和 endpoint 启动脚本没有显式导出 `ROS_LOG_DIR`，ROS2/rclpy 回退到默认 home 日志目录。
- 解决方案：在 `scripts/start_ros_tcp_endpoint.sh`、`run_asset_upgrade_baseline_check.sh`、所有 ROS2 自动验收脚本、手工检查/查看脚本和控制面板启动脚本中统一创建并导出 `ROS_LOG_DIR=/home/ubuntu22/VLN/.ros/log`。
- 验收方式：`bash -n scripts/*.sh` 通过；检查所有含 ROS2 调用的 shell 脚本均包含 `ROS_LOG_DIR`；重新运行完整回归 `vln_asset_baseline_20260815_193207` 输出 `VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`。
- 状态：已解决；后续新增 ROS2 脚本必须继承项目内日志目录。

## 2026-08-15：wheel-ground 首轮视觉 URDF 残留 ArticulationBody

- 现象：第一次运行 `/home/ubuntu22/VLN/scripts/run_scout_wheel_ground_smoke_test.sh` 时，ROS2 图像、点云、TF、odom 和物理前进都已通过，但脚本最后因 `visual_articulation_body_count=5` 失败；Unity 日志显示 `Can't remove ArticulationBody because UrdfInertial / UrdfJointContinuous / UrdfJointFixed depends on it`。
- 环境：Unity URDF Importer `v0.5.2`，阶段 14 场景 `VLNOffroadScoutWheelGroundCandidate.unity`，Scout 视觉模型由 Editor URDF 导入后再挂到物理根下。
- 根因：URDF Importer 生成的 `UrdfJoint`、`UrdfInertial`、`UrdfCollision`、`UrdfVisual`、`UrdfLink`、`UrdfRobot` 脚本依赖 `ArticulationBody`，如果先删 `ArticulationBody`，Unity 会拒绝删除。
- 解决方案：在 `VlnOffroadScoutWheelGroundCandidateProjectSetup.RemovePhysicsComponents()` 中按依赖顺序先删除 URDF 相关脚本，再删除 `ArticulationBody`、`Rigidbody` 和 `Collider`；视觉模型只保留 Renderer。
- 验收方式：重新运行 `run_scout_wheel_ground_smoke_test.sh`，run id `vln_scout_wheel_ground_20260815_195417` 输出 `VLN_SCOUT_WHEEL_GROUND_SMOKE_TEST_PASS`，结果文件显示 `visual_collider_count=0`、`visual_articulation_body_count=0`、`wheel_collider_count=4`。
- 状态：已解决；后续如果将 URDF mesh 当纯视觉模型使用，必须先剥离 URDF 脚本依赖，再剥离物理组件。

## 2026-08-16：wheel-ground 固定路线复杂转向不稳定

- 现象：阶段 15 尝试使用路径点纠偏控制驱动 Scout wheel-ground 候选走复杂路线时，第二/第三路径点附近容易出现横摆、侧滑、偏离道路或进度不足；20m 长路线第三段后会随机横向漂移到右侧障碍/不稳定地形区；简单调高角速度会让 skid-steer 车体更不稳定。
- 环境：Unity `VLNOffroadScoutWheelGroundCandidate.unity`，`Rigidbody + WheelCollider` 第一版物理车体，控制入口 `/vln/cmd_vel`。
- 根因判断：当前轮胎横向摩擦、质心、差速转向和控制器还只是第一版候选参数；在没有完成低速转向标定前，把复杂路径点纠偏当成导航控制会混淆“物理属性问题”和“控制器未标定问题”。
- 解决方案：阶段 15 默认收敛为 9m 短路线低速小角度回正巡航：路线 `3,0;6,0;9,0`，参数 `max_linear=0.45`、`linear_gain=0.30`、`linear_accel=0.12`、`max_angular=0.18`、`angular_gain=0.35`、`angular-sign=-1`、`angular_accel=0.08`、`min_linear_while_turning=0.32`。同时保留脚本参数覆盖能力，后续可单独做转向、摩擦和路线标定。
- 验收方式：短路线运行 `run_scout_wheel_ground_route_smoke_test.sh`，最新 run id `vln_scout_wheel_ground_route_20260816_041954` 输出 `VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS`，`reached_count=3/3`、`total_forward_progress=8.334m`、`total_progress=8.355m`、`stall_count=0`，图像、CameraInfo、PointCloud2 和 odom 同时通过。
- 状态：已解决为第一版稳定短路线演示；20m 长路线、复杂绕障和高速转向路线暂不作为默认验收。

## 2026-08-16：固定路线偏离用户要求、速度过慢、轮胎视觉陷地

- 现象：用户手工验证后反馈上一版固定路线不是从起点走到桥/坡/终点方向的完整路线，而是短距离慢速巡航；小车视觉上像倒着走，速度太慢；轮胎看起来陷进地板。
- 环境：`VLNOffroadScoutWheelGroundCandidate.unity`，Scout V2 URDF 视觉模型 + Unity `Rigidbody + WheelCollider` 物理根，ROS2 控制脚本 `/home/ubuntu22/VLN/scripts/ros2_drive_scout_physics_route.py`。
- 根因：第一版阶段 15 为了先得到稳定回归，把默认路线收敛为 9m 短路线，偏离了用户要观察桥、坡和终点方向的目的；wheel-ground 视觉 URDF 额外设置了 `Quaternion.Euler(0,180,0)`，导致物理车体虽然沿道路前进但视觉车体朝向反了；桥面简化碰撞体边缘形成硬台阶，长路线在前向约 `13.7m` 处顶住；轮胎视觉跟随 WheelCollider 世界姿态时偏低，截图中容易看成轮胎陷地。
- 解决方案：默认路线升级为 `4,0;8,0;12,0;15,0;18,0;22,0;28,0;34,0;42,0;50,0;54,0`；速度参数一度提高到 `max_linear=1.35m/s`、`linear_accel=0.95m/s^2`；移除 Scout 视觉根额外 `180°` yaw；轮胎视觉偏移提高到 `0.085m`；桥面新增前后物理过渡坡；路线控制脚本增加 `--skip-stalled-waypoints` 作为排障参数。
- 验收方式：`/home/ubuntu22/VLN/scripts/run_scout_wheel_ground_route_smoke_test.sh` 输出 `VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS`，run id `vln_scout_wheel_ground_route_20260816_150349`，`reached_count=11/11`、`total_forward_progress=53.080m`、`total_progress=53.102m`、`stall_count=0`、`skipped_count=0`，图像、CameraInfo、PointCloud2 和 odom 均通过。随后 `/home/ubuntu22/VLN/scripts/run_scout_wheel_ground_smoke_test.sh` 输出 `VLN_SCOUT_WHEEL_GROUND_SMOKE_TEST_PASS`，run id `vln_scout_wheel_ground_20260816_150809`，直行 5 秒前向位移约 `3.466m`，截图显示车体姿态正常、轮胎不再明显扎进地面。
- 状态：该记录中的高速参数和部分物理过渡方案已被后续可见局部物理通行面方案替换；当前完成标准见 `2026-08-16：撤销连续隐形物理路面并恢复可见局部物理接触`。

## 2026-08-16：泥土路块缝隙和短坡坡口导致 WheelCollider 卡车

- 现象：用户手工验证时发现物理车体、车高和轮胎转动已经正常，但泥土路一块一块之间有缝隙和小高度差，小车过缝时会卡很久；短坡和地面交界处也会卡住上不去。用户判断现实中轮胎直径远大于这些小缝/小坎，普通泥地不应像完全光滑或沼泽一样打滑卡死。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，程序化路面 `Offroad_DirtRoad_00..08` 为分块 `Cube`，`Offroad_ShortRamp` 为倾斜 `Cube`，Scout 物理车体使用 Unity `Rigidbody + WheelCollider`。
- 根因：这不是正常泥地摩擦不足，而是仿真碰撞几何不合理。视觉分块路面和短坡 collider 形成了小缝、硬边和小台阶；Unity `WheelCollider` 对这种离散硬边非常敏感，会把小视觉缝隙当成真实硬障碍。现实大轮胎会跨过/压过的小坎，在当前 collider 里变成了必须爬上的几何台阶。
- 旧解决方案：曾采用“视觉分块、物理连续”的宽泛隐形通行面：保留 `Offroad_DirtRoad_*` 和 `Offroad_ShortRamp` 的视觉渲染，但删除它们的 collider；新增 2 段 `ScoutWheelGround_PhysicalTrailSurface_Rear/Front`，让车轮实际接触连续平滑路面。
- 旧验收方式：`run_scout_wheel_ground_route_smoke_test.sh` 曾在 run id `vln_scout_wheel_ground_route_20260816_153103` 中通过，结果文件显示 `physical_trail_surface_count=2`；基础直行回归 run id 为 `vln_scout_wheel_ground_20260816_153542`。
- 状态：该旧方案已撤销。虽然它解决了卡车，但会让可见地形和真实接触面不一致，属于用户明确禁止的“为了通过而作弊”。当前有效方案见下一条记录。

## 2026-08-16：撤销连续隐形物理路面并恢复可见局部物理接触

- 现象：用户指出上一版为了避免卡缝/坡口，实际铺了宽泛连续隐形通行面，导致 Scout 经过独木桥、台阶和半坡时视觉上像穿模或平走，没有沿可见平面真实交互。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，Scout V2 视觉模型 + `Rigidbody + WheelCollider` 物理根，固定路线脚本 `/home/ubuntu22/VLN/scripts/ros2_drive_scout_physics_route.py`。
- 根因：宽泛隐形接触面把“车轮应该接触什么”从可见路面/桥面/坡面转移到了用户看不到的平滑面。它能提高自动测试通过率，但破坏了真实物理链路的解释性和可验收性。
- 解决方案：删除旧 `ScoutWheelGround_PhysicalTrailSurface_*` 生成逻辑；旧对象在场景生成时强制清理；改为生成可见局部物理体：道路 slab、块间 seam、桥面/桥头坡、短坡本体/前后过渡。正式验收脚本要求 `broad_physical_trail_count=0`，并要求 bridge、short ramp、road slab、road seam 计数都存在。
- 验收方式：基础回归 `./scripts/run_scout_wheel_ground_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_20260816_164023`；完整路线 `./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260816_165241`，`reached_count=11/11`、`total_forward_progress=53.118m`、`final_lateral_offset=-0.000m`、`max_reached_cross_track=0.002m`、`stall_count=0`、`skipped_count=0`、`broad_physical_trail_count=0`、`road_physical_slab_count=8`、`road_seam_transition_count=6`、`bridge_physics_count=3`、`short_ramp_physics_count=3`、`decorative_trail_collider_count=0`。
- 状态：该方案已被后续收窄物理通行面方案替换，不再作为当前默认方案。后续任何路线修复都不得使用宽泛隐形平路；必须修可见局部 collider、轮胎参数、车体参数或控制器。

## 2026-08-16：严格复跑后发现路线仍会卡住，改为可见加宽通行面和物理稳定控制

- 现象：撤销连续隐形物理路面后，严格复跑不是稳定通过。run id `vln_scout_wheel_ground_route_20260816_170330` 到第 10 个路径点时横向偏移约 `9.9m` 且 `stall_count=3`；零角速度试跑 `vln_scout_wheel_ground_route_20260816_170855` 更差，车离开可通行区域；低速自动符号试跑 `vln_scout_wheel_ground_route_20260816_171456` 在中段停滞。
- 环境：`VLNOffroadScoutWheelGroundCandidate.unity`，固定路线 `4..54m`，强验收要求 `broad_physical_trail_count=0`、`stall_count=0`、`skipped_count=0`。
- 根因：这次失败不是 ROS2 或传感器问题，而是 wheel-ground 物理车体在桥后/中段的横向稳定性和转向响应不足；局部桥面/路面宽度与简化 WheelCollider 横向漂移余量也不匹配。脚本正确判失败，不能通过放宽 gate、跳点或隐藏平路解决。
- 解决方案：保持旧 `ScoutWheelGround_PhysicalTrailSurface_*` 为撤销状态；把道路/桥面局部物理体渲染为可见 `8.0m` 宽通行面；在 `VlnScoutWheelGroundController` 中加入 `yaw_assist` 和 `lateral_damping`，通过 `Rigidbody.AddTorque/AddForce` 施加物理力/力矩，模拟差速转向响应和轮胎侧向阻尼，不改位姿、不关碰撞。
- 验收方式：完整路线默认验收 `./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260816_172640`，`reached_count=11/11`、`total_forward_progress=53.049m`、`final_lateral_offset=-0.163m`、`max_reached_cross_track=0.981m`、`stall_count=0`、`skipped_count=0`、`broad_physical_trail_count=0`、`road_physical_slab_count=8`、`road_seam_transition_count=6`、`bridge_physics_count=3`、`short_ramp_physics_count=3`、`decorative_trail_collider_count=0`。基础回归 `./scripts/run_scout_wheel_ground_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_20260816_173110`，`forward_delta=3.273m`，图像、CameraInfo、PointCloud2、TF、cmd_vel 和 odom 全部通过。
- 状态：该 8m 可见通行面方案已被后续收窄物理通行面方案替换，不再作为当前默认方案。该修复仍是重要中间记录：放宽 gate、跳点或隐藏平路都不能作为修复路径。

## 2026-08-16：8m 可见通行面仍被用户判定为偏离真实物理链路

- 现象：用户指出 8m 宽可见通行面虽然不是隐藏平路，但小车经过独木桥、台阶和半坡时仍像被大平面托住，视觉上表现为穿模/平走，没有沿窄桥和坡面真实交互。
- 环境：`VLNOffroadScoutWheelGroundCandidate.unity`，阶段 15 固定路线物理巡航，上一轮通过结果为 `vln_scout_wheel_ground_route_20260816_172640`。
- 根因：8m 通行面把道路/桥面横向余量做得过大，尤其桥面不再像独木桥难点；同时短坡由多段物理体拼接时容易出现内部硬边或由普通路面托底。该方案能提高自动测试通过率，但不能满足用户要求的“完整物理真实链路”。
- 解决方案：主路物理 slab 设计宽度收窄为 `6.2m`，桥面物理宽度收窄为 `2.25m`；路面 slab 在桥区和短坡区让开；短坡改为单个连续可见 `ScoutWheelGround_PhysicalShortRampContinuous` MeshCollider；`VlnScoutWheelGroundController` 增加 wheel contact 审计，记录 road/bridge/short_ramp 接触步数和高度跨度；验收脚本检查宽度上限、桥/短坡接触步数和 `wheel_ground_height_span_m`。
- 验收方式：完整路线 `./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260816_181247`，`reached_count=13/13`、`total_forward_progress=52.920m`、`final_lateral_offset=-0.761m`、`max_reached_cross_track=0.741m`、`stall_count=0`、`skipped_count=0`、`broad_physical_trail_count=0`、`road_physical_slab_count=8`、`road_seam_transition_count=5`、`bridge_physics_count=3`、`short_ramp_physics_count=1`、`road_physical_max_width_m=6.939`、`bridge_physical_max_width_m=2.250`、`short_ramp_physical_max_width_m=4.800`、`bridge_contact_steps=1937`、`short_ramp_contact_steps=1569`、`wheel_ground_height_span_m=0.369`。基础回归 `./scripts/run_scout_wheel_ground_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_20260816_181841`，`physics_root_delta_m=3.2591`，相机、CameraInfo、PointCloud2、TF、cmd_vel 和 odom 全部通过。
- 状态：当前有效完成标准。后续如果再卡，禁止恢复宽泛隐形平路、禁止恢复 8m 桥/路通行面、禁止用道路 slab 托底桥/坡；必须修可见几何、轮胎参数、质心/悬挂、电机扭矩或控制器。

## 2026-08-16：阶段 15 日志目录只保留 previous 文件造成排障误读

- 现象：`vln_scout_wheel_ground_route_20260816_175924/run_summary.txt` 记录当前路线通过，但同目录的 `previous_vln_scout_physics_route_demo_result.txt` 是上一轮失败结果，容易误读为本轮失败；后续 `vln_scout_wheel_ground_route_20260816_181247` 已验证新归档逻辑生效。
- 根因：验收脚本运行前会把工程 `Logs/` 中旧结果移动到本次目录的 `previous_*`，但运行结束后没有把新生成的当前结果文件复制到同一个目录。
- 解决方案：修改 `run_scout_wheel_ground_route_smoke_test.sh` 和 `run_scout_wheel_ground_smoke_test.sh`，在每次 Unity/ROS 验收结束后把当前结果文件复制进本次 `_SmokeTestLogs/<run_id>/`，无 `previous_` 前缀；`previous_*` 只代表运行前残留结果。
- 验收方式：脚本语法检查通过；`vln_scout_wheel_ground_route_20260816_181247` 日志目录中已同时看到当前 `vln_scout_physics_route_demo_result.txt`、`vln_offroad_scout_wheel_ground_candidate_result.txt`、`vln_scout_wheel_ground_controller_result.txt` 等文件；基础回归目录 `vln_scout_wheel_ground_20260816_181841` 也已归档当前结果文件。
- 状态：已修复并验证。

## 2026-08-16：轮胎视觉正反抖动，独木桥视觉仍像穿模

- 现象：用户手工观察到两个问题：轮胎不像现实车轮连续 360 度滚动，而像往前转又往后转；过独木桥时视觉上仍像车体没有压在桥面上，而是从桥区域穿过去。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，Scout V2 视觉模型 + Unity `Rigidbody + WheelCollider` 物理根，阶段 15 固定完整路线脚本。
- 根因：轮胎视觉直接套用 `WheelCollider.GetWorldPose()` 的旋转会把 WheelCollider 悬挂/转向求解的瞬时姿态带到 mesh 上，视觉上容易出现正反摆动；独木桥处旧 Kenney 可见桥和后加的物理桥面同时存在时，肉眼看到的桥与真实 collider 可能分离，造成“看起来穿模”。
- 解决方案：轮胎视觉改为只跟随 `WheelCollider.GetWorldPose()` 的位置，旋转改由累计滚动角 `accumulated_roll_root_x` 平滑积分生成；删除旧 Kenney 可见木桥，改由 `ScoutWheelGround_PhysicalBridgeDeck` 同时承担可见桥面和碰撞桥面，左右栏杆只做视觉且无 collider。
- 验收方式：基础回归 `./scripts/run_scout_wheel_ground_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_20260816_205824`；完整路线 `./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260816_205923`，`decorative_bridge_renderer_count=0`、`bridge_deck_has_renderer=1`、`bridge_deck_has_collider=1`、`bridge_deck_renderer_collider_top_delta_m=0.0000`、`wheel_visual_total_abs_roll_deg=515506.5`、`wheel_visual_direction_reversal_count=0`。桥区截图 `vln_offroad_scout_wheel_ground_bridge_screenshot.png` 人工检查显示车辆在可见桥面上方。
- 状态：已解决并补强验收。路线脚本现在会检查轮胎滚动累计，且新增 `wheel_visual_direction_flapping` gate，后续如果视觉轮大量正反跳变会直接失败。

## 2026-08-16：用户质疑桥/斜坡被压平，存在“为了通过测试作弊”的风险

- 现象：用户指出当前桥和斜坡看起来比原始地图更扁、更简约，怀疑为了让 Scout 小车更容易通过而把真实地形难度抹平。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，阶段 15 固定完整路线物理巡航，前序修复已经撤销宽泛隐形路面和 8m 宽通行面。
- 根因判断：用户质疑合理。此前确实出现过“宽泛隐形路面”和“8m 可见通行面”这类能提高通过率但解释性差的中间方案；即使当前已撤销，也需要把“不能压平桥/坡”变成硬验收，而不是只靠口头约束。
- 解决方案：保留受限宽度的可见局部物理体，不恢复隐藏托底或道路宽桥面；在 Unity 结果文件中写入 `terrain_geometry_policy=visible_local_physics_no_flattening_no_hidden_bypass`；自动验收强制 `bridge_visual_detail_count>=40`、`bridge_physical_height_span_m>=0.20`、`short_ramp_physical_height_span_m>=0.62`、`bridge_contact_steps>0`、`short_ramp_contact_steps>0`，并新增短坡截图归档。完整路线脚本现在要求桥区截图和短坡截图都存在，否则失败。
- 验收方式：`./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260816_215127`，`reached_count=13/13`、`total_forward_progress=53.642m`、`stall_count=0`、`skipped_count=0`、`broad_physical_trail_count=0`、`bridge_physical_height_span_m=0.235`、`short_ramp_physical_height_span_m=0.804`、`bridge_contact_steps=1018`、`short_ramp_contact_steps=1874`、`wheel_ground_height_span_m=0.913`、`wheel_visual_direction_reversal_count=0`。桥区截图和短坡截图均已归档并人工查看，当前视觉仍是低模工程场景，但不是隐藏托底或压平坡面。
- 状态：已解决并补强验收。后续任何让桥/坡更容易通过的改动，都必须同时保留可见接触面一致性、非扁平高度阈值、桥/坡接触审计和截图证据。

## 2026-08-17：固定路线控制出现 S 型和撞桥栏杆风险，改用手动示教记录

- 现象：用户手工运行固定路线时，小车会先旋转再慢慢拐回中心线，随后沿路线走出 S 型；过桥时由于横向偏差和栏杆碰撞边界问题，车辆可能擦到或穿过栏杆并偏出道路。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，Scout wheel-ground 物理车体，固定路线脚本仍输出 `/vln/cmd_vel`。
- 根因判断：固定路线脚本是开环/弱闭环路径点控制，地图道路中心线、桥面窄通行区域、WheelCollider 横向漂移、差速转向响应和栏杆物理边界会叠加；继续单纯调自动控制参数容易把物理问题和控制器问题混在一起，也容易诱导再次用“作弊式”几何修复。
- 解决方案：新增控制面板“速度控制”模块，用户亲自用键盘驾驶真实物理车体通过满意路线；后端按 100Hz 持续发布当前速度，失焦或点击“速度归零”会发布零速度；前端按键心跳会持续刷新当前按键，若浏览器意外关闭或心跳丢失，后端 `manual-command-timeout=0.18s` 会自动停车；记录功能导出 `vln_manual_cmd_vel_recording_v1` JSON，后续用回放脚本按时间戳重放 `/vln/cmd_vel`。方向映射固定为 `↑` 正线速度、`↓` 负线速度、`←/A` 正 `angular.z` 左转、`→/D` 负 `angular.z` 右转。
- 验收方式：`python3 -m py_compile scripts/vln_control_panel.py scripts/replay_manual_drive_recording.py scripts/vln_control_panel_manual_recording_smoke_client.py` 通过；`bash -n scripts/start_vln_control_panel.sh scripts/replay_manual_drive_recording.sh scripts/run_control_panel_manual_recording_smoke_test.sh scripts/run_control_panel_smoke_test.sh` 通过；`./scripts/run_control_panel_manual_recording_smoke_test.sh` 输出 `VLN_CONTROL_PANEL_MANUAL_RECORDING_SMOKE_TEST_PASS`，run id `vln_control_panel_manual_recording_20260817_022128`；导出的示例 JSON `manual_drive_20260817_022130.json` 使用 `./scripts/replay_manual_drive_recording.sh --file ... --time-scale 20 --speed-scale 0 --max-duration 0.2` 输出 `VLN_MANUAL_DRIVE_REPLAY_OK`；旧目标位置回归 `./scripts/run_control_panel_smoke_test.sh` 输出 `VLN_CONTROL_PANEL_SMOKE_TEST_PASS`，run id `vln_control_panel_20260817_021533`。
- 状态：已解决为阶段 16 手动示教记录/回放闭环。后续如果要把示教路线升级成自主导航，应新开阶段做闭环路径跟踪或 Nav2/VLN 接入，不要通过压平桥/坡、关闭碰撞或放宽路线 gate 来掩盖控制问题。

## 2026-08-17：手动速度控制方向、直行和停车响应严重不符合预期

- 现象：用户手工操作速度控制模块时发现 `↑/↓` 前后方向相反，按前进不能直行而会慢慢偏航，`←/→` 或 `A/D` 不是近似原地转而是乱走；松键后约数秒才停，控制延迟不可接受。用户明确要求至少 100Hz 控制频率，并要求有角度闭环/PID 控制。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，中文控制面板速度控制模块，Scout wheel-ground `Rigidbody + WheelCollider` 物理候选，控制 topic 为 `/vln/cmd_vel`。
- 根因：第一版手动控制只把键盘映射成 `/vln/cmd_vel` 速度，没有同时标定 Unity wheel-ground 场景里的轮端差速符号、视觉左/右方向、直行航向保持符号和停止超时；控制面板后端持续发布频率和心跳超时也偏保守，导致松键停车响应慢。固定路线脚本上的旧 `angular-sign` 经验不能直接照搬到手动控制 UI。
- 解决方案：`scripts/vln_control_panel.py` 默认后端发布频率改为 `100Hz`，前端按键心跳为 `50ms`，后端 `manual-command-timeout=0.18s`；松键、窗口失焦、页面隐藏和心跳丢失都会立即调用 `/api/velocity_stop` 并连续发布多帧 0 速度。键位固定为 `↑` 正 `linear.x`、`↓` 负 `linear.x`、`←/A` 正 `angular.z`、`→/D` 负 `angular.z`。Unity 物理层不再让 WheelCollider 电机承担纯转向，`m_WheelAngularMotorScale=0`；角速度由 yaw-rate PID 计算，再通过 Rigidbody 角速度伺服/`MoveRotation` 施加到底盘，避免原地转变成大平移。
- 验收方式：静态检查 `python3 -m py_compile scripts/vln_control_panel.py scripts/vln_control_panel_manual_velocity_unity_client.py scripts/vln_control_panel_manual_recording_smoke_client.py scripts/replay_manual_drive_recording.py scripts/ros2_drive_scout_physics_route.py` 通过；`bash -n scripts/run_control_panel_manual_velocity_unity_smoke_test.sh scripts/run_control_panel_manual_recording_smoke_test.sh scripts/start_vln_control_panel.sh scripts/replay_manual_drive_recording.sh scripts/run_scout_wheel_ground_smoke_test.sh` 通过。专项 Unity 验收脚本 `./scripts/run_control_panel_manual_velocity_unity_smoke_test.sh` 用于检查前进为正、横漂/偏航受控、松键快速停车、A/D 近似原地转；最近已通过 run id `vln_control_panel_manual_velocity_unity_20260817_040919`，指标为前进 `0.531m`、横漂约 `0.000m`、偏航约 `0.000rad`、停车漂移 `0.001m/0.002m`、A 左转 yaw `+0.599rad`、D 右转 yaw `-0.634rad`、纯转向平移 `0.017m/0.016m`。手动记录验收 `vln_control_panel_manual_recording_20260817_041218` 通过；基础 wheel-ground 回归 `vln_scout_wheel_ground_20260817_041230` 通过。
- 状态：已二次修复并新增专项验收。后续如果用户再次手工发现方向反或延迟大，第一优先级是复跑 `run_control_panel_manual_velocity_unity_smoke_test.sh`，不要先改地图、桥/坡几何或固定路线控制器。

补充验证：2026-08-17 已将专项客户端扩展到方向键 `←/→`，最新 run id `vln_control_panel_manual_velocity_unity_20260817_130258` 通过：`↑` 前进 `0.531m`、A 左转 `+0.575rad`、D 右转 `-0.609rad`、`←` 左转 `+0.600rad`、`→` 右转 `-0.659rad`，最终停车漂移 `0.002m`。

## 2026-08-17：手动控制修复后固定自动路线纠偏方向反了

- 现象：用户要求优先恢复老师演示用自动路线；此前修完手动速度控制后，旧固定路线脚本会走 S 型、偏离中心线或卡点，默认完整路线一度失败。
- 环境：`VLNOffroadScoutWheelGroundCandidate.unity`，Scout wheel-ground 物理车体，固定路线脚本 `scripts/ros2_drive_scout_physics_route.py`，验收脚本 `scripts/run_scout_wheel_ground_route_smoke_test.sh`。
- 根因：手动速度控制修复后，Unity 底层底盘已经统一为正 `angular.z` 左转；但自动路线脚本、手工演示脚本和路线控制器默认值仍保留旧 `angular-sign=-1`。这会把中心线纠偏方向反过来，不是地图、桥面、坡面或碰撞体的新问题。
- 解决方案：只修控制符号入口，不改地形物理体、不放宽 gate、不跳点：`run_scout_wheel_ground_route_smoke_test.sh`、`drive_scout_wheel_ground_route_demo.sh` 和 `ros2_drive_scout_physics_route.py` 默认统一改为 `angular-sign=1`；`user.md` 中调参示例也改为 `angular-sign=1`。
- 验收方式：静态检查 `bash -n scripts/run_scout_wheel_ground_route_smoke_test.sh scripts/drive_scout_wheel_ground_route_demo.sh`、`python3 -m py_compile scripts/ros2_drive_scout_physics_route.py` 通过；已搜索脚本、文档和日志，旧的负号角速度参数不再作为可执行入口残留。默认运行 `./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260817_125552`，`reached_count=13/13`、`total_forward_progress=52.435m`、`final_lateral_offset=-0.015m`、`max_abs_lateral_offset=0.015m`、`max_bridge_abs_lateral_offset=0.014m`、`stall_count=0`、`skipped_count=0`、`bridge_contact_steps=1629`、`short_ramp_contact_steps=1648`。
- 状态：已解决。后续如再改手动控制或底层 yaw 约定，必须同步检查固定路线的 `angular-sign`。

## 2026-08-17：后段挑战场地第一版障碍过强导致路线末端停滞

- 现象：在阶段 18 后段挑战场地第一版中，Scout 小车已经通过旧桥/坡和大部分新增路面，但在挑战路线最后路径点前停滞；失败 run id 为 `vln_scout_wheel_ground_challenge_route_20260817_135425`，脚本按 `stall_count>0` 正确判失败。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，新增草地、青石路、沙地和低矮障碍，扩展路线入口为 `scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh`。
- 根因：第一版横向低横木和石纹凸条组合过强，障碍横向阻挡过长、局部高度/接触边缘对当前 WheelCollider 轮地模型不友好，变成了接近硬阻挡的卡点；这不符合用户要求的“有影响但能克服”，也不能用跳点、隐藏托底或降低验收掩盖。
- 解决方案：保留挑战路面和可见物理接触原则，但降低凸条高度、缩短横向阻挡长度、把低横木偏置到路侧，并保留沙地波纹和两侧导向石作为低矮扰动；新增障碍继续要求有 collider、能接触、能通过，不做不可越过硬墙。
- 验收方式：静态检查 `bash -n scripts/run_scout_wheel_ground_route_smoke_test.sh scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh` 和 `python3 -m py_compile scripts/ros2_drive_scout_physics_route.py` 通过；扩展路线 `./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_challenge_route_20260817_135927`，`reached_count=16/16`、`total_forward_progress=60.947m`、`stall_count=0`、`skipped_count=0`、`challenge_surface_contact_steps=14513`、`challenge_obstacle_contact_steps=320`、`challenge_obstacle_collider_count=10`、`challenge_end_wall_z=39.200`；旧 13 点金标准路线随后回归通过，run id `vln_scout_wheel_ground_route_20260817_140352`。该版本随后被用户否定为“场地挤在最后、视觉粗糙、青石段卡住”，不是当前最终基线。
- 状态：已解决并记录。后续继续加新障碍时先小幅提高难度并复跑 13 点旧基线和 16 点挑战路线；禁止为了通过而压平桥/坡、关闭碰撞、铺隐藏托底面、跳过卡点或放宽 gate。

## 2026-08-17：后段挑战场地分布和视觉建模不符合用户要求

- 现象：用户指出新增三个场地全部挤在最后一块地，斜坡后已有大空间没有利用；草地、青石路、沙地只是彩色地面加少量简单方块/木条，视觉不像对应材质；青石段小车会被弹回并卡住，说明物理交互存在但难度和几何不合适。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，阶段 18 后段挑战场地，扩展路线脚本 `scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh`。
- 根因：第一版挑战区布局太集中，视觉细节不足；青石段采用过强或边缘过硬的低矮障碍，导致当前 WheelCollider 轮地模型被周期性弹回，变成卡点而不是可克服扰动。
- 解决方案：不改旧桥/坡基线，把挑战区重做为斜坡后大空间分散布局：草地区约 `z=10.0..16.8`，青石路约 `z=20.0..28.0`，沙地区约 `z=32.0..49.0`，终点墙后移到 `z=53.5m`。草地增加草簇、土斑和根茎扰动；青石路改成铺石板、暗色缝隙和低矮沉降凸起；沙地增加沙纹、浅洼、软沙波纹和侧边石。物理扰动保持可见、有 collider、有接触统计，但不再做横向硬阻挡。
- 验收方式：扩展路线 `./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_challenge_route_20260817_144908`，`reached_count=16/16`、`total_forward_progress=70.432m`、`final_lateral_offset=0.065m`、`max_abs_lateral_offset=0.088m`、`stall_count=0`、`skipped_count=0`、`challenge_obstacle_count=155`、`challenge_obstacle_collider_count=15`、`challenge_surface_contact_steps=16576`、`challenge_obstacle_contact_steps=498`、`challenge_end_wall_z=53.500`；旧 13 点金标准路线回归通过，run id `vln_scout_wheel_ground_route_20260817_145357`。
- 状态：已解决并替换为当前阶段 18 基线。后续若用户手工观察仍认为视觉不够，应继续增加低模细节或引入合适外部材质/模型，但不能为了外观破坏旧路线、隐藏碰撞或降低物理真实性。

## 2026-08-17：手工演示入口和自动回归入口被混淆

- 现象：给用户后段挑战场地的操作建议时，把 `run_scout_wheel_ground_challenge_route_smoke_test.sh` 作为主要入口发给用户；该脚本会自动 batch 打开 Unity 并做回归验收，不符合用户一直采用的“先打开 Unity 软件，再开终端运行演示脚本、自己看效果”的工作习惯。
- 环境：阶段 18 后段挑战场地已经通过自动回归，用户需要在 Unity 图形界面里观察小车通过新增草地、青石路、沙地和障碍，而不是只看自动验收日志。
- 根因：上下文读取机制精简后，短状态里保留了自动验收命令，但没有把“用户手工演示默认入口”放在更高优先级，导致回答时把我用于回归的 `run_*_smoke_test.sh` 误当成用户操作入口。
- 解决方案：新增 `scripts/drive_scout_wheel_ground_challenge_route_demo.sh`，它只在 Unity 已打开、endpoint 已启动、场景已 Play 的情况下发布 16 点挑战路线，不自动打开 Unity；重写 `user.md`，把手工流程放在最前；在 `CURRENT_STATE.md` 和 `AGENTS.md` 写入“手工演示优先、自动验收只用于回归”的约束。
- 验收方式：`bash -n scripts/drive_scout_wheel_ground_route_demo.sh scripts/drive_scout_wheel_ground_challenge_route_demo.sh scripts/run_scout_wheel_ground_route_smoke_test.sh scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh` 通过；`python3 -m py_compile scripts/ros2_drive_scout_physics_route.py` 通过；`git diff --check` 对相关文档和新脚本无输出。
- 状态：已再次加固并写入最高约束。后续给用户“怎么看、怎么运行”的步骤时，除非用户明确说“自动验收/回归/你自己跑测试”，否则先给 `open_unity_vln_project.sh`、`start_ros_tcp_endpoint.sh`、Unity Play、`drive_*_demo.sh` 的顺序；自动回归只能作为我改代码后的内部验证。

## 2026-08-17：挑战区草地、沙石地视觉仍显粗糙

- 现象：用户指出当前草地不像草，只像绿色地面上放方块；沙石地也不像真实沙石，质疑 Unity 作为游戏开发软件不应只有这种粗糙建模能力。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，阶段 18 后段挑战场地，草地、青石路、沙地均为程序化低模场景。
- 根因：当前场景最初为了保护 ROS2、传感器、WheelCollider 物理和自动路线基线，采用低多边形程序化几何和简单材质，没有引入 Terrain 草系统、PBR 地表材质、Shader Graph、贴图 splat 或外部高质量资产；所以视觉辨识度不足，但这不是 Unity 本身能力上限。
- 解决方案：不安装新包、不下载大资产、不改桥/坡/路线控制。用程序化低模视觉层增强现有挑战区：草地改为 3 层草叶 mesh + 土斑/根茎；青石路加入不规则铺石、暗缝、裂纹和碎石 field；沙地加入更密沙纹、浅洼和颗粒 field。视觉增强层主要不加 collider，避免为了外观改变通过性；真实扰动仍由低矮可越 collider 负责。
- 验收方式：`./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_challenge_route_20260817_173720`，`reached_count=16/16`、`stall_count=0`、`skipped_count=0`、`challenge_grass_blade_field_count=3`、`challenge_stone_visual_detail_count=80`、`challenge_sand_visual_detail_count=46`，并归档草地、青石路、沙地三段截图；旧 13 点基线 `./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260817_174346`。
- 状态：已小步修复并补强自动验收。当前仍是低模工程演示风格，不是游戏级高精度美术资产；后续若要更真实，应单独开“外部材质/资产升级阶段”，先筛选授权和性能，再小步导入，不能覆盖当前物理基线。

## 2026-08-17：青石路和沙地需要 PBR 材质真实感

- 现象：草地低模升级经用户确认可接受后，用户要求继续进入 PBR 材质/外部资产/植被系统升级阶段，重点把沙地、石路做得更真实。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，阶段 18 后段挑战路线已通过，旧桥/坡和 ROS2 控制链路为不可破坏基线。
- 根因：上一轮主要靠程序化低模几何和颜色区分地表，缺少真实照片纹理、normal map、AO 等 PBR 信息，所以近景仍有“工程演示感”。
- 解决方案：使用代理下载 ambientCG `Ground054_1K-JPG` 和 `PavingStones151_1K-JPG`，只导入 1K JPG 小子集；Unity Built-in Standard 材质接入 Albedo、Normal、Occlusion，并给挑战地面 profile mesh 增加 world-space UV。视觉材质不改变 collider 和通过性；`Roughness` 贴图保留，后续如切到 URP/HDRP 或自定义 shader 再转换使用。
- 验收方式：`./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_challenge_route_20260817_182912`，16/16 到达，`stall_count=0`、`skipped_count=0`，`challenge_pbr_albedo_material_count=7`、`challenge_pbr_normal_material_count=7`、`challenge_pbr_occlusion_material_count=7`；旧 13 点基线 `./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260817_183540`。
- 状态：已解决本轮 PBR 小样本升级。当前仍不是完整高精度美术生产流程；下一步如果继续提升，可做 Terrain/Decal/URP 或更多外部资产候选，但必须继续小步验收。

## 2026-08-17：挑战区材质视觉与物理交互仍可能脱节

- 现象：用户指出草地、沙地、石板路不能只是视觉贴图或纯色模型；小车接触区域必须体现材质本身的形状和物理特性，不能视觉上是草/沙/石，物理上却全部等价为普通地面。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，阶段 18A 后已有 PBR 贴图和低模视觉细节，但主要视觉细节仍大多无 collider，控制器只记录总体挑战区接触。
- 根因：前几轮优先保护 Unity-ROS2、相机、LiDAR、Scout wheel-ground、旧桥/坡和自动路线基线，因此视觉层与物理层刻意解耦；进入阶段 18B 后，这种解耦已不满足“材质一致物理仿真”的目标。
- 解决方案：新增 22 个低矮可见 `ScoutWheelGround_ChallengePhysicsProxy_*` 代理，分别对应草地柔性根/草阻、石板刚性接缝、沙地软波纹；控制器按 WheelCollider 命中的对象区分草/石/沙，施加温和滚阻和沙地低附着近似，并输出分材质接触步数、代理接触、平均速度和高度扰动。
- 验收方式：`./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_challenge_route_20260817_210512`，`challenge_physics_proxy_count=22`、`challenge_visual_physics_proxy_audit_pass=1`、`challenge_physics_proxy_contact_steps=1003`、`grass_contact_steps=1255`、`stone_contact_steps=1330`、`sand_contact_steps=13843`，且 `reached_count=16/16`、`stall_count=0`、`skipped_count=0`；旧 13 点金标准 `./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260817_210945`。
- 状态：已完成第一版修复。后续如果手工观察仍觉得某个材质不真实，应微调代理几何、阻力参数或视觉反馈，不能回退到纯贴图、隐藏托底或关闭碰撞。

## 2026-08-17：草地第二版明显压痕不符合用户偏好

- 现象：用户反馈加强版草叶渲染中的明显压痕/强倒伏效果不满意，要求回退到第一版倒伏版本，即经过时草叶被压低、向两侧推开，并留下低恢复速度的轮迹。
- 环境：`Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，阶段 18B 草地视觉反馈和材质一致物理代理已接入，16 点挑战路线和旧 13 点路线为不可破坏基线。
- 根因：第二版为了让轮迹更明显，加入了独立轮迹 painter / 深色压痕视觉层，视觉反馈过重，偏离用户想要的自然轻倒伏效果。
- 解决方案：回退到第一版 `VlnChallengeGrassDeformer` 方案，只保留草叶 mesh 的运行时轻倒伏、侧向推开和慢恢复；移除 `GrassTrackPainter`、深色轮迹贴片和 `challenge_grass_track_*` 当前验收指标；在 `AGENTS.md`、`CURRENT_STATE.md`、`PROJECT_MEMORY.md`、`workflow.md` 和决策日志中记录禁止无明确要求恢复强压痕。
- 验收方式：16 点挑战路线 `./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_challenge_route_20260817_231723`，`reached_count=16/16`、`total_forward_progress=70.434m`、`final_lateral_offset=-0.004m`、`max_abs_lateral_offset=0.086m`、`stall_count=0`、`skipped_count=0`、`challenge_grass_deformer_count=3`、`challenge_grass_max_deformed_blade_count=418`、`challenge_grass_max_fresh_affected_blade_count=156`；旧 13 点金标准 `./scripts/run_scout_wheel_ground_route_smoke_test.sh` 通过，run id `vln_scout_wheel_ground_route_20260817_232310`，`reached_count=13/13`、`stall_count=0`、`skipped_count=0`。
- 状态：已解决并锁定为当前偏好。后续草地升级可以微调草叶密度、材质、恢复速度或侧推强度，但不得默认恢复明显深色压痕、强倒伏轮迹或车身 footprint 清扫式压痕。
