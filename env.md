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
- 已导入项目级 Unity 包：`com.frj.unity-sensors` 与 `com.frj.unity-sensors-ros`，来源为 `https://github.com/Field-Robotics-Japan/UnitySensors.git`，当前锁定 hash `91698e3593abdb04baac022a670cc52fee027238`。
- 已导入项目级 Unity 包：`com.unity.ugui` `1.0.0`，用于满足 UnitySensors sample/UI 相关编译引用。
- 已导入项目级 Unity 包：`com.unity.test-framework` `1.1.33`，连带 `com.unity.ext.nunit` `1.0.6`，用于满足 UnitySensors 包内 Tests asmdef 对 `UnityEngine.TestRunner`、`UnityEditor.TestRunner` 和 `nunit.framework.dll` 的引用。注意：这是 Unity 工程内 UPM 依赖，不是系统包、Python 包或 Conda 包。
- Unity ROS2 编译符号：`ProjectSettings/ProjectSettings.asset` 中 `Standalone: ROS2`。
- Unity-ROS2 最小通信闭环脚本：`/home/ubuntu22/VLN/scripts/run_ros2_unity_smoke_test.sh`。

### UnitySensors 相机图像闭环验收

一键运行：

```bash
/home/ubuntu22/VLN/scripts/run_unitysensors_image_smoke_test.sh
```

脚本会自动启动 `ROS-TCP-Endpoint`，运行 Unity 批处理相机场景，并用 ROS2 Python 节点等待一个 `sensor_msgs/msg/Image` 消息。

当前固定相机输出：

- 图像 topic：`/vln/front/image_raw`
- 图像类型：`sensor_msgs/msg/Image`
- 相机内参 topic：`/vln/front/camera_info`
- 相机内参类型：`sensor_msgs/msg/CameraInfo`
- frame：`front_camera_optical_frame`
- 分辨率：640x480
- 编码：`rgb8`
- 初期频率：约 5Hz

成功输出：

```text
VLN_UNITYSENSORS_IMAGE_SMOKE_TEST_PASS
```

最近一次通过日志：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_image_20260813_230503`。

手工只看 ROS2 侧时，可在 endpoint 和 Unity 场景运行期间执行：

```bash
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic list -t | grep /vln/front
ros2 topic hz /vln/front/image_raw
python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py --topic /vln/front/image_raw --width 640 --height 480 --encoding rgb8 --frame-id front_camera_optical_frame --timeout 20
```

### UnitySensors LiDAR 点云闭环验收

一键运行：

```bash
/home/ubuntu22/VLN/scripts/run_unitysensors_lidar_smoke_test.sh
```

脚本会自动启动 `ROS-TCP-Endpoint`，运行 Unity 批处理 LiDAR 场景，并用 ROS2 Python 节点等待一个 `sensor_msgs/msg/PointCloud2` 消息。

当前固定 LiDAR 输出：

- 点云 topic：`/vln/lidar/points`
- 点云类型：`sensor_msgs/msg/PointCloud2`
- frame：`lidar_link`
- scan pattern：UnitySensors VLP-16
- 点数：7200 点/帧
- `point_step`：16 bytes
- 初期频率：约 5Hz
- 当前带宽：约 0.6 MB/s

成功输出：

```text
VLN_UNITYSENSORS_LIDAR_SMOKE_TEST_PASS
```

最近一次通过日志：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_lidar_20260813_230736`。

手工只看 ROS2 侧时，可在 endpoint 和 Unity 场景运行期间执行：

```bash
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic list -t | grep /vln/lidar
ros2 topic hz /vln/lidar/points
ros2 topic bw /vln/lidar/points
python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic /vln/lidar/points --width 7200 --point-step 16 --frame-id lidar_link --timeout 20 --min-nonzero-points 20
```

手工可视化点云时，先启动 endpoint，再在 Unity 中打开 `Assets/VLN/Scenes/UnitySensorsLidarSmokeTest.unity` 并点击 Play。另开终端执行：

```bash
/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh
```

该脚本使用固定 RViz 配置 `/home/ubuntu22/VLN/config/vln_lidar_pointcloud.rviz`：`Fixed Frame` 为 `map`，显示项为 `PointCloud2`，topic 为 `/vln/lidar/points`，并临时发布 `map -> lidar_link` 静态 TF。当前阶段还没有正式 `map -> odom -> base_link -> lidar_link` TF 树，所以这是手工可视化用的临时补丁；后续标准化阶段再补正式 TF。

注意：RViz 左侧 `VLN LiDAR PointCloud2` 显示 `Status: OK` 只代表配置、topic 类型和 TF 没有明显错误，不代表当前一定收到了新的点云帧。如果画面只有网格，先运行：

```bash
/home/ubuntu22/VLN/scripts/check_manual_visualization_state.sh
```

该脚本会短时间等待 `/vln/lidar/points` 的一帧 `PointCloud2`，并检查有效非零点数量。只有它输出 `LiDAR 正在实时发布有效点云帧`，RViz 才应该能看到点云。若脚本提示未收到有效点云帧，优先确认 Unity 当前打开的是 `UnitySensorsLidarSmokeTest.unity`，并且顶部 Play 按钮是蓝色。

当前 RViz 配置的 `Decay Time` 设置为 `2` 秒，原因是 UnitySensors 的 VLP-16 scan pattern 总长度为 57600 个方向，而当前测试场景每帧只发布 7200 点、5Hz；也就是每 8 帧扫完一整圈，约 1.6 秒。如果 `Decay Time=0`，RViz 只显示最新一帧，看起来会像一个不停旋转的扇形扫描片；保留最近 2 秒后，视觉上更接近传统 LiDAR 的稳定 360 度点云阵列。这个改动只影响 RViz 显示，不改变 ROS2 topic 的真实数据。

手工看图像时不要直接运行 `rqt_image_view /vln/front/image_raw`；当前机器有 `rqt_image_view` 包但没有独立 shell 命令。使用：

```bash
/home/ubuntu22/VLN/scripts/view_front_image.sh
```

如果需要排查 endpoint、Unity Play 和 topic 状态，使用：

```bash
/home/ubuntu22/VLN/scripts/check_manual_visualization_state.sh
```

如果 Unity 的 `Game` 面板在 `UnitySensorsImageSmokeTest.unity` 中显示 `No cameras rendering`，但 rqt 能看到 `/vln/front/image_raw`，说明 ROS 图像链路已经通了，只是旧场景没有普通 Unity 展示相机。当前场景构建器已补 `ImageSmokeTest_ViewerCamera`；关闭 Unity 后运行以下命令可重建轻量 smoke 场景：

```bash
/home/ubuntu22/VLN/scripts/rebuild_unity_smoke_scenes.sh
```

如果 Unity Editor 卡死无法退出，另开终端运行：

```bash
/home/ubuntu22/VLN/scripts/stop_unity_vln_project.sh
```

该脚本会先温和结束 VLN 工程相关 Unity 进程，必要时强制结束，并把残留 `ArtifactDB-lock`、`SourceAssetDB-lock` 移动到 `_ManualRecoveryLogs`。不要手工删除整个 `Library/`。

### 阶段 6：极简越野 terrain 联合闭环

当前场景：

- Unity 场景：`Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity`
- 场景内容：轻量程序化网格地形、土路、坡、石块、树木、障碍物、静态占位车体、前向 RGB 相机、VLP-16 LiDAR
- 图像 topic：`/vln/front/image_raw`，`sensor_msgs/msg/Image`，640x480，`rgb8`，约 5Hz
- 相机内参 topic：`/vln/front/camera_info`，`sensor_msgs/msg/CameraInfo`
- 点云 topic：`/vln/lidar/points`，`sensor_msgs/msg/PointCloud2`，7200 点/帧，约 5Hz，约 0.58 MB/s

一键自动验收：

```bash
/home/ubuntu22/VLN/scripts/run_offroad_terrain_smoke_test.sh
```

成功输出：

```text
VLN_OFFROAD_TERRAIN_SMOKE_TEST_PASS
```

最近一次通过日志：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_offroad_20260814_004120`。

手工查看方式：先运行 endpoint，再在 Unity 中打开 `Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity` 并点击 Play。图像用：

```bash
/home/ubuntu22/VLN/scripts/view_front_image.sh
```

点云用：

```bash
/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh
```

注意：阶段 6 自动验收脚本必须保留图形上下文，因此使用 `-batchmode`，不要加 `-nographics`。当前机器上 `-nographics` 会让 Unity 在 UnitySensors `RGBCameraSensor` 的 `Camera.Render` 路径段错误。这个限制只影响阶段 6 这种带复杂 terrain 场景的自动验收；阶段 4/5 原有单独 smoke test 已回归通过。

### 阶段 7：可控占位车体与 TF 树闭环

当前场景继续使用：

- Unity 场景：`Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity`
- 车体：程序化可控占位车体，后续真实小车/URDF 导入前的传感器载体 baseline
- 默认行为：Unity 点击 Play 后不自动巡航；未收到 `/vln/cmd_vel` 时保持静止
- TF topic：`/tf`，`tf2_msgs/msg/TFMessage`
- TF 树：`map -> base_link`，`base_link -> front_camera_optical_frame`，`base_link -> lidar_link`

一键自动验收：

```bash
/home/ubuntu22/VLN/scripts/run_vehicle_tf_smoke_test.sh
```

成功输出：

```text
VLN_VEHICLE_TF_SMOKE_TEST_PASS
```

最近一次通过日志：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_vehicle_tf_20260814_021645`。

该次结果：图像、CameraInfo、点云和 TF 全部通过；无 `/vln/cmd_vel` 指令时 `base_link` 最大位移 `0.000m`，满足默认静止要求。该阶段不再用自动巡航作为通过条件。

### 阶段 8：标准化 topic / TF / RViz / rosbag

当前固定接口：

- 图像：`/vln/front/image_raw`，`sensor_msgs/msg/Image`，640x480，`rgb8`，`front_camera_optical_frame`，约 5Hz
- 相机内参：`/vln/front/camera_info`，`sensor_msgs/msg/CameraInfo`，`front_camera_optical_frame`
- 点云：`/vln/lidar/points`，`sensor_msgs/msg/PointCloud2`，`lidar_link`，VLP-16，7200 点/帧，约 5Hz
- TF：`/tf`，`tf2_msgs/msg/TFMessage`，`map -> base_link -> front_camera_optical_frame,lidar_link`

一键自动验收：

```bash
/home/ubuntu22/VLN/scripts/run_standardized_outputs_smoke_test.sh
```

成功输出：

```text
VLN_STANDARDIZED_OUTPUTS_SMOKE_TEST_PASS
```

最近一次通过日志：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_standardized_outputs_20260814_021919`。

最近一次 rosbag：`/home/ubuntu22/VLN/VLN_BAGS/vln_standardized_outputs_20260814_021919`，大小约 `39.6 MiB`，时长约 `7.78s`，包含图像 40 帧、CameraInfo 39 帧、点云 39 帧、TF 78 条消息；无控制指令时车体保持静止。

手工检查标准输出：

```bash
/home/ubuntu22/VLN/scripts/check_standardized_vln_outputs.sh
```

正式 RViz 查看：

```bash
/home/ubuntu22/VLN/scripts/view_vln_vehicle_rviz.sh
```

该脚本使用 `/home/ubuntu22/VLN/config/vln_vehicle_sensors.rviz`，依赖 Unity 正式发布 `/tf`，不再临时发布 `map -> lidar_link`。

记录 rosbag 小样本：

```bash
/home/ubuntu22/VLN/scripts/record_vln_sensor_bag_sample.sh
```

默认记录 8 秒；如需指定时长，例如 15 秒：

```bash
/home/ubuntu22/VLN/scripts/record_vln_sensor_bag_sample.sh 15
```

rosbag 固定输出到 `/home/ubuntu22/VLN/VLN_BAGS`。该目录已被 `.gitignore` 忽略，不提交到 git。

### 阶段 9：ROS2 `/vln/cmd_vel` 控制闭环

当前固定控制接口：

- 控制 topic：`/vln/cmd_vel`
- 控制消息：`geometry_msgs/msg/Twist`
- 线速度：`linear.x`，单位 m/s
- 角速度：`angular.z`，单位 rad/s
- 当前最大线速度：2.0 m/s
- 当前最大角速度：1.2 rad/s
- 指令超时：0.75s

一键自动验收：

```bash
/home/ubuntu22/VLN/scripts/run_cmd_vel_control_smoke_test.sh
```

成功输出：

```text
VLN_CMD_VEL_CONTROL_SMOKE_TEST_PASS
```

最近一次通过日志：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_cmd_vel_control_20260814_021738`。

该次结果：ROS2 发布 `/vln/cmd_vel [geometry_msgs/msg/Twist]`，Unity 收到 48 条指令，`base_link` 位移约 `2.262m`，yaw 变化约 `2.851rad`，图像、CameraInfo、点云仍全部通过字段校验。`vln_vehicle_control_result.txt` 记录 `autopilot_until_first_command=False`。

手工控制验证：

```bash
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
python3 /home/ubuntu22/VLN/scripts/ros2_send_cmd_vel_and_wait_tf.py \
  --cmd-topic /vln/cmd_vel \
  --tf-topic /tf \
  --linear-x 0.8 \
  --angular-z 0.7 \
  --duration 4.0
```

也可以用 ROS2 CLI 手工发一次持续速度，但这种方式不会自动校验 TF：

```bash
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic pub /vln/cmd_vel geometry_msgs/msg/Twist "{linear: {x: 0.5}, angular: {z: 0.3}}" -r 10
```

停止时发布 0 速度：

```bash
ros2 topic pub --once /vln/cmd_vel geometry_msgs/msg/Twist "{linear: {x: 0.0}, angular: {z: 0.0}}"
```

注意：当前控制模型是轻量运动学模型，主要用于验证 ROS2 控制接口、传感器随车体移动和 TF 更新；它不是最终真实底盘动力学。

### 阶段 10：ROS2 路径点控制闭环

当前固定路径点控制方式：

- 输入 TF：`/tf` 中的 `map -> base_link`
- 输出控制：`/vln/cmd_vel [geometry_msgs/msg/Twist]`
- 默认相对路径点：`1.2,0.0;2.4,0.0`
- 路径点坐标含义：以启动时 `base_link` 为原点，x 为前向米，y 为左向米
- 默认到点阈值：0.35m

一键自动验收：

```bash
/home/ubuntu22/VLN/scripts/run_waypoint_control_smoke_test.sh
```

成功输出：

```text
VLN_WAYPOINT_CONTROL_SMOKE_TEST_PASS
```

最近一次通过日志：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_waypoint_control_20260814_021829`。

该次结果：到达 2/2 个路径点，总位移约 `2.100m`，最终距离最后路径点约 `0.300m`，ROS2 发布 `/vln/cmd_vel` 共 49 条，图像、CameraInfo、点云仍全部通过字段校验。

手工路径点控制验证：

```bash
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
python3 /home/ubuntu22/VLN/scripts/ros2_drive_waypoints.py \
  --cmd-topic /vln/cmd_vel \
  --tf-topic /tf \
  --relative-waypoints '1.2,0.0;2.4,0.0'
```

如果要改目标距离，例如前进 1m、2m、3m：

```bash
python3 /home/ubuntu22/VLN/scripts/ros2_drive_waypoints.py \
  --cmd-topic /vln/cmd_vel \
  --tf-topic /tf \
  --relative-waypoints '1.0,0.0;2.0,0.0;3.0,0.0'
```

注意：这是轻量路径点控制器，不是 navigation2。它用于验证后续 VLN/规划模块可以“读 TF、发 cmd_vel、驱动车体”。

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

### Unity-ROS2 最小闭环验收

一键运行：

```bash
/home/ubuntu22/VLN/scripts/run_ros2_unity_smoke_test.sh
```

脚本会自动启动 `ROS-TCP-Endpoint`，运行 Unity smoke test，验证两个方向：

1. Unity -> ROS2：`/unity/heartbeat`，消息类型 `std_msgs/msg/String`。
2. ROS2 -> Unity：`/ros2/command`，消息类型 `std_msgs/msg/String`。

成功输出：

```text
VLN_ROS2_SMOKE_TEST_PASS
```

最近一次通过日志：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_smoke_20260813_224611`。

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

暂无。本项目尚未执行任何系统包、Python 包或 Conda 包安装命令。已新增的 Unity 包均为 `/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Packages/manifest.json` 内的项目级 UPM 依赖。

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
- 2026-08-13：新增并验证 Unity-ROS2 最小闭环脚本 `/home/ubuntu22/VLN/scripts/run_ros2_unity_smoke_test.sh`；阶段 3 已通过，下一阶段可以开始 UnitySensors 相机图像闭环。
- 2026-08-13：新增并验证 UnitySensors 相机图像闭环脚本 `/home/ubuntu22/VLN/scripts/run_unitysensors_image_smoke_test.sh` 和 ROS2 字段校验脚本 `/home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py`；阶段 4 已通过，下一阶段可以开始 UnitySensors LiDAR 点云闭环。
- 2026-08-13：新增并验证 UnitySensors LiDAR 点云闭环脚本 `/home/ubuntu22/VLN/scripts/run_unitysensors_lidar_smoke_test.sh` 和 ROS2 字段校验脚本 `/home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py`；阶段 5 已通过，下一阶段可以开始极简越野 terrain 闭环。
- 2026-08-14：新增并验证可控占位车体和 TF 树闭环脚本 `/home/ubuntu22/VLN/scripts/run_vehicle_tf_smoke_test.sh`；阶段 7 已通过，正式 TF 树为 `map -> base_link -> front_camera_optical_frame,lidar_link`，当前默认无 `/vln/cmd_vel` 指令时静止。
- 2026-08-14：新增并验证标准化输出脚本 `/home/ubuntu22/VLN/scripts/run_standardized_outputs_smoke_test.sh`、`check_standardized_vln_outputs.sh`、`view_vln_vehicle_rviz.sh`、`record_vln_sensor_bag_sample.sh` 和 RViz 配置 `/home/ubuntu22/VLN/config/vln_vehicle_sensors.rviz`；阶段 8 已通过，并在默认静止修复后回归通过，最近标准 rosbag 样本位于 `/home/ubuntu22/VLN/VLN_BAGS/vln_standardized_outputs_20260814_021919`。
- 2026-08-14：修复 Unity Play 后车体自动巡航问题；将 `m_AutopilotUntilFirstCommand` 默认值、场景生成器和已保存主场景都改为 false。该改动未安装任何新系统包、Python 包或 Conda 包。
- 2026-08-14：新增并验证 ROS2 `/vln/cmd_vel` 控制闭环脚本 `/home/ubuntu22/VLN/scripts/run_cmd_vel_control_smoke_test.sh` 和 `/home/ubuntu22/VLN/scripts/ros2_send_cmd_vel_and_wait_tf.py`；阶段 9 已通过。该阶段只使用已有 ROS2 `geometry_msgs`，未安装任何新系统包或 Python 包。
- 2026-08-14：新增并验证 ROS2 路径点控制脚本 `/home/ubuntu22/VLN/scripts/ros2_drive_waypoints.py` 和 `/home/ubuntu22/VLN/scripts/run_waypoint_control_smoke_test.sh`；阶段 10 已通过。该阶段只使用已有 ROS2 `geometry_msgs`、`tf2_msgs`，未安装任何新系统包或 Python 包。
