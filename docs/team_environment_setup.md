# VLN 仿真环境部署与运行手册

版本日期：2026-08-27

## 1. 环境要求

| 项目 | 版本或要求 |
| --- | --- |
| 操作系统 | Ubuntu 22.04 |
| Unity | 2022.3.62f1 LTS |
| ROS2 | Humble |
| Python | 3.10 |
| Git | 可访问团队 GitHub 仓库 |
| 显卡 | NVIDIA 独显优先 |

ROS2 环境需要具备以下常用组件：

- `colcon`
- `rviz2`
- `rqt`
- `rqt_image_view`
- `tf2_ros`
- `sensor_msgs`
- `geometry_msgs`
- `nav_msgs`
- `rclpy`

Unity Editor 如果不在默认路径，请在终端中设置：

```bash
export UNITY_EDITOR=/path/to/Unity/Editor/Unity
```

## 2. 获取项目文件

项目由两部分组成：代码仓库和仿真资产包。

### 2.1 获取代码仓库

```bash
git clone https://github.com/yangou-ylz/VLN_Car.git VLN
cd VLN
```

### 2.2 解压仿真资产包

将 `VLN_MesaTopgear_TeamRelease_*.tar.zst` 放到项目根目录，然后执行：

```bash
mkdir -p UnityProjects
tar --zstd -xf VLN_MesaTopgear_TeamRelease_*.tar.zst -C UnityProjects
```

如果资产包以分卷形式提供，先合并再解压：

```bash
cat VLN_MesaTopgear_TeamRelease_*.tar.zst.part.* > VLN_MesaTopgear_TeamRelease.tar.zst
tar --zstd -xf VLN_MesaTopgear_TeamRelease.tar.zst -C UnityProjects
```

解压完成后，目录应为：

```text
VLN/
  UnityProjects/
    VLN_MesaTopgear_TeamRelease/
```

检查资产包：

```bash
./scripts/check_mesa_topgear_team_release_project.sh
```

看到以下输出表示资产包可用：

```text
VLN_MESA_TOPGEAR_TEAM_RELEASE_CHECK_OK
```

## 3. 初始化 ROS-TCP-Endpoint

首次运行前执行：

```bash
./scripts/setup_ros_tcp_endpoint_workspace.sh
```

看到以下输出表示初始化完成：

```text
VLN_ROS_TCP_ENDPOINT_WORKSPACE_READY
```

## 4. 启动仿真

请按顺序打开三个终端。

### 4.1 终端 A：打开 Unity 场景

```bash
cd /path/to/VLN
./scripts/open_high_precision_world_model.sh --scene mesa_topgear
```

Unity 打开后，确认当前场景为：

```text
Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity
```

### 4.2 终端 B：启动 ROS-TCP-Endpoint

```bash
cd /path/to/VLN
./scripts/start_ros_tcp_endpoint.sh
```

保持该终端运行。然后回到 Unity，点击 Play。

### 4.3 终端 C：启动键盘控制

```bash
cd /path/to/VLN
./scripts/start_mesa_topgear_local_keyboard_control.sh
```

键盘控制：

| 按键 | 功能 |
| --- | --- |
| `W` 或 `↑` | 前进 |
| `S` 或 `↓` | 后退 |
| `A` 或 `←` | 左转 |
| `D` 或 `→` | 右转 |
| `Space` | 停车 |
| `Q` | 退出 |

## 5. 查看传感器数据

Unity 已点击 Play 且 ROS-TCP-Endpoint 正在运行后，再打开传感器显示工具。

### 5.1 四路鱼眼相机

```bash
cd /path/to/VLN
./scripts/view_all_camera_images.sh
```

相机 topic：

```text
/vln/front/image_raw
/vln/rear/image_raw
/vln/left/image_raw
/vln/right/image_raw
```

### 5.2 LiDAR 点云

```bash
cd /path/to/VLN
./scripts/view_vln_vehicle_rviz.sh
```

LiDAR topic：

```text
/vln/lidar/points
```

## 6. 运行检查

### 6.1 检查 ROS2 topic

```bash
ros2 topic list -t | grep /vln
```

应能看到相机、LiDAR、odom 和控制相关 topic。

### 6.2 检查图像频率

```bash
ros2 topic hz /vln/front/image_raw
```

### 6.3 检查点云频率

```bash
ros2 topic hz /vln/lidar/points
```

### 6.4 检查控制指令

```bash
ros2 topic echo /vln/cmd_vel --once
```

## 7. 常见问题

### 7.1 Unity 无法启动

检查 Unity 路径：

```bash
echo "$UNITY_EDITOR"
ls -l "$UNITY_EDITOR"
```

如果路径为空或文件不存在，重新设置：

```bash
export UNITY_EDITOR=/path/to/Unity/Editor/Unity
```

### 7.2 找不到 Unity 工程

确认资产包已经解压：

```bash
ls UnityProjects/VLN_MesaTopgear_TeamRelease
```

然后重新执行：

```bash
./scripts/check_mesa_topgear_team_release_project.sh
```

### 7.3 Endpoint 启动失败

确认 Endpoint workspace 已生成：

```bash
ls unity_ros2_ws/install/setup.bash
```

如果文件不存在，重新初始化：

```bash
./scripts/setup_ros_tcp_endpoint_workspace.sh
```

如果端口被占用，检查 10000 端口：

```bash
ss -ltnp | grep 10000
```

关闭旧进程后重新运行：

```bash
./scripts/start_ros_tcp_endpoint.sh
```

### 7.4 相机或点云没有数据

按顺序确认：

```bash
ros2 topic list -t | grep /vln
ros2 topic hz /vln/front/image_raw
ros2 topic hz /vln/lidar/points
```

常见原因是 Unity 尚未点击 Play、ROS-TCP-Endpoint 未启动，或 ROS2 环境未正确加载。

### 7.5 小车没有响应

先确认控制指令存在：

```bash
ros2 topic echo /vln/cmd_vel --once
```

如果没有输出，重新打开键盘控制：

```bash
./scripts/start_mesa_topgear_local_keyboard_control.sh
```
