# VLN Mesa Topgear 团队部署手册

版本日期：2026-08-27
适用场景：`mesa_topgear` 交付版，即 Pure Nature Mesa 沙漠环境 + Topgear 真实物理小车 + 四路鱼眼相机 + 16 线 LiDAR + ROS2 控制链路。

## 1. 交付范围

本手册只面向团队成员复现当前主线仿真环境。交付目标是：成员完成配置后，可以打开 Mesa 沙漠小车场景，手动控制车辆，并在 ROS2 中查看四路鱼眼相机和 LiDAR 点云。

### 1.1 包含内容

- Unity 场景：`VLNMesaDesertTopgearVehicleCandidate.unity`。
- 世界资产：Pure Nature Mesa Desert 主线资产子集。
- 车辆模型：Topgear 上装版小车、车身、轮胎、上装、官方 VLP-16 LiDAR 外观、官方 RealSense D405 相机外观。
- 物理链路：Rigidbody、WheelCollider、地形碰撞、沙地接触分类和基础障碍碰撞代理。
- ROS2 链路：`/vln/cmd_vel`、`/vln/odom`、四路 `/vln/*/image_raw`、四路 CameraInfo、`/vln/lidar/points`、TF。
- 脚本与配置：启动脚本、Endpoint 构建脚本、查看相机/RViz 脚本、锁定传感器/上装/真实相机位姿配置。

### 1.2 不包含内容

- 早期低模测试场景、旧 13 点路线场景、草地/青石/沙地挑战区。
- Oasis、Meadow、ForestLake、Mesa+Oasis 融合版等其它完整世界模型。
- Unity `Library`、`Temp`、`Logs`、`UserSettings`。
- 原始 `.unitypackage`、下载缓存、rosbag、截图、运行记录和开发过程日志。
- CUDA、PyTorch、Conda 或系统级依赖安装包。

## 2. 发布方式

推荐采用“代码仓库 + Mesa Topgear 发布资产包”的方式交付。

| 内容 | 分发方式 | 原因 |
| --- | --- | --- |
| 代码、脚本、配置、文档 | GitHub 仓库 | 体量小，适合版本管理和协作修改 |
| Mesa Topgear Unity 发布工程 | 单独压缩包 | 体量约数 GB，适合网盘、内网文件服务、移动硬盘或 GitHub Release 附件 |
| 其它历史资产和实验场景 | 不分发 | 与当前主线无关，容易干扰团队部署 |

不建议把数 GB Unity 资产直接提交进普通 Git 历史。普通 clone 会被迫下载完整历史，仓库会迅速膨胀，后续维护困难。若必须通过 GitHub 分发大包，应优先使用 Release 附件或团队文件服务；是否使用 Git LFS 需要单独确认账号额度、权限和许可证要求。

## 3. 前置环境

团队成员需要先自行安装以下基础软件。本文档只给出版本要求，不展开系统安装教程。

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Ubuntu 22.04 |
| Unity | 2022.3.62f1 LTS |
| ROS2 | Humble |
| Python | 3.10，使用系统 Python 和 ROS2 自带 `rclpy` |
| Git | 可正常 clone GitHub 仓库 |
| GPU | NVIDIA 独显优先，需能正常运行 Unity 和 RViz |

ROS2 侧建议具备以下组件：

- `colcon`
- `rviz2`
- `rqt`
- `rqt_image_view`
- `tf2_ros`
- `sensor_msgs`
- `geometry_msgs`
- `nav_msgs`
- `rclpy`

环境约束：不要为了本项目临时升级 CUDA、PyTorch、Conda、显卡驱动或系统包。ROS2 命令建议在干净 shell 中运行，不要在 Conda `base` 环境中运行。

## 4. 获取代码仓库

```bash
git clone https://github.com/yangou-ylz/VLN_Car.git VLN
cd VLN
```

如果 Unity Editor 不在默认位置，设置 `UNITY_EDITOR`：

```bash
export UNITY_EDITOR=/path/to/Unity/Editor/Unity
```

如果 Unity Package Manager 访问 Git 依赖较慢，可以在本地代理已启动的前提下设置：

```bash
export UNITY_PROXY=http://127.0.0.1:7897/
```

## 5. 放置 Mesa Topgear 发布工程

从负责人处获取 Mesa Topgear 发布包。文件名示例：

```text
VLN_MesaTopgear_TeamRelease_YYYYMMDD_HHMMSS.tar.zst
```

将压缩包放到仓库根目录或任意临时目录后解压，使目录结构变为：

```text
VLN/
  UnityProjects/
    VLN_MesaTopgear_TeamRelease/
      Assets/
      Packages/
      ProjectSettings/
      VLN_MESA_TOPGEAR_TEAM_RELEASE_MANIFEST.json
```

`.tar.zst` 解压命令：

```bash
tar --zstd -xf VLN_MesaTopgear_TeamRelease_YYYYMMDD_HHMMSS.tar.zst -C UnityProjects
```

如果收到的是分卷文件，先合并，再解压：

```bash
cat VLN_MesaTopgear_TeamRelease_YYYYMMDD_HHMMSS.tar.zst.part.* > VLN_MesaTopgear_TeamRelease_YYYYMMDD_HHMMSS.tar.zst
sha256sum -c VLN_MesaTopgear_TeamRelease_YYYYMMDD_HHMMSS.tar.zst.sha256
tar --zstd -xf VLN_MesaTopgear_TeamRelease_YYYYMMDD_HHMMSS.tar.zst -C UnityProjects
```

如果发布包为 `.tar.gz`，使用：

```bash
tar -xzf VLN_MesaTopgear_TeamRelease_YYYYMMDD_HHMMSS.tar.gz -C UnityProjects
```

解压后运行只读检查：

```bash
./scripts/check_mesa_topgear_team_release_project.sh
```

成功标志：

```text
VLN_MESA_TOPGEAR_TEAM_RELEASE_CHECK_OK
```

## 6. 构建 ROS-TCP-Endpoint

首次部署需要在本机生成 ROS2 Endpoint workspace：

```bash
./scripts/setup_ros_tcp_endpoint_workspace.sh
```

该脚本只在项目目录内执行 clone、补丁和 `colcon build`，不安装系统包、Python 包、Conda 包或 Snap 包。

成功标志：

```text
VLN_ROS_TCP_ENDPOINT_WORKSPACE_READY
```

## 7. 标准运行流程

按以下顺序运行。不要用自动回归脚本替代首次手工演示。

### 7.1 打开 Unity 场景

终端 A：

```bash
cd /path/to/VLN
./scripts/open_mesa_topgear_team_release_project.sh
```

Unity 打开后确认场景为：

```text
Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity
```

### 7.2 启动 ROS-TCP-Endpoint

终端 B：

```bash
cd /path/to/VLN
./scripts/start_ros_tcp_endpoint.sh
```

保持该终端运行。

### 7.3 启动仿真

回到 Unity，点击 Play。

### 7.4 手动控制小车

终端 C：

```bash
cd /path/to/VLN
./scripts/start_mesa_topgear_local_keyboard_control.sh
```

键盘控制窗口用于持续发布 `/vln/cmd_vel`。该方式用于当前 Mesa Topgear 场景的人工演示，响应比网页端控制更稳定。

### 7.5 查看四路鱼眼相机

新开终端：

```bash
cd /path/to/VLN
./scripts/view_all_camera_images.sh
```

也可以在 ROS2 中检查 topic：

```bash
ros2 topic list -t | grep /vln
ros2 topic hz /vln/front/image_raw
```

四路相机 topic：

```text
/vln/front/image_raw
/vln/rear/image_raw
/vln/left/image_raw
/vln/right/image_raw
```

### 7.6 查看 LiDAR 点云

新开终端：

```bash
cd /path/to/VLN
./scripts/view_vln_vehicle_rviz.sh
```

或直接检查频率：

```bash
ros2 topic hz /vln/lidar/points
```

当前主线 LiDAR 配置目标为 16 线、约 18Hz、约 90m 最大距离。团队验收时以实际 topic 有稳定 PointCloud2 数据为准。

## 8. 成功标准

完成部署后应满足以下条件：

- Unity 能打开 Mesa Topgear 场景，车辆位于无水荒漠低洼通行区域。
- Unity 点击 Play 后，小车不会明显浮空或穿入地面。
- 本地键盘控制可以驱动车辆前进、后退和转向。
- ROS2 中存在 `/vln/cmd_vel`、`/vln/odom`、四路图像、四路 CameraInfo、`/vln/lidar/points` 和 TF。
- `rqt_image_view` 能看到四路圆形鱼眼图像。
- RViz 能看到随车辆和地形变化的 LiDAR 点云。

## 9. 常见问题

### 9.1 Unity Editor 未找到

确认 `UNITY_EDITOR` 指向真实可执行文件：

```bash
echo "$UNITY_EDITOR"
ls -l "$UNITY_EDITOR"
```

然后重新运行：

```bash
./scripts/open_mesa_topgear_team_release_project.sh
```

### 9.2 发布工程缺失

运行检查：

```bash
./scripts/check_mesa_topgear_team_release_project.sh
```

如果提示缺少 `UnityProjects/VLN_MesaTopgear_TeamRelease`，说明 Mesa Topgear 发布资产包尚未解压到指定位置。

### 9.3 Endpoint 无法启动

先确认 workspace 已构建：

```bash
ls unity_ros2_ws/install/setup.bash
```

如果文件不存在，重新运行：

```bash
./scripts/setup_ros_tcp_endpoint_workspace.sh
```

如果端口被占用，检查旧进程：

```bash
ss -ltnp | grep 10000
```

关闭旧 Endpoint 后重新启动：

```bash
./scripts/start_ros_tcp_endpoint.sh
```

### 9.4 ROS2 看不到相机或 LiDAR

按顺序确认：

```bash
ros2 topic list -t | grep /vln
ros2 topic hz /vln/front/image_raw
ros2 topic hz /vln/lidar/points
```

如果没有数据，通常是以下原因之一：Unity 未点击 Play、Endpoint 未启动、场景不是 Mesa Topgear 发布场景、ROS2 环境未正确 source。

### 9.5 小车不能控制

确认控制脚本正在发布 `/vln/cmd_vel`：

```bash
ros2 topic echo /vln/cmd_vel --once
```

若无输出，重新打开本地键盘控制：

```bash
./scripts/start_mesa_topgear_local_keyboard_control.sh
```

## 10. 负责人打包流程

以下步骤只由维护者执行，普通团队成员不需要执行。

生成干净发布工程：

```bash
./scripts/prepare_mesa_topgear_team_release_project.sh --refresh
```

检查发布工程：

```bash
./scripts/check_mesa_topgear_team_release_project.sh
```

生成发布压缩包：

```bash
./scripts/package_mesa_topgear_team_release_project.sh --split 1900M
```

输出目录：

```text
VLN_ASSETS_CACHE/team_release_packages/
```

发布前检查代码仓库：

```bash
./scripts/check_repo_release_readiness.sh
```

提交 GitHub 的内容仅限代码、脚本、配置和文档。发布工程压缩包不提交到普通 Git 历史。
