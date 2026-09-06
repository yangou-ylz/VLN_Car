# VLN Mesa Topgear 仿真环境

本项目提供 Unity-ROS2 仿真环境，用于运行 Mesa Topgear Mix 沙漠场景中的 Topgear 小车，并输出四路宽屏普通 RGB 120° FOV 相机、16 线 LiDAR、odom、TF 和 `/vln/cmd_vel` 控制接口。当前相机为 `960x540 @ 17Hz`，LiDAR 为 `18Hz / 90m / 57600` 点每帧，近距 `0.15m`；LiDAR 数据根保持水平 360°，通过下扫增强线束覆盖近车地面。

## 快速开始

部署步骤见：[docs/team_environment_setup.md](docs/team_environment_setup.md)。

标准运行顺序：

```bash
cd /path/to/VLN
./scripts/open_high_precision_world_model.sh --scene mesa_topgear_mix
./scripts/start_ros_tcp_endpoint.sh
./scripts/start_mesa_topgear_local_keyboard_control.sh
```

Unity 打开后，先启动 ROS-TCP-Endpoint，再点击 Play，最后启动键盘控制或传感器显示工具。

## 传感器显示

查看四路宽屏普通 RGB 相机：

```bash
./scripts/view_all_camera_images.sh
```

查看 LiDAR 点云：

```bash
./scripts/view_vln_vehicle_rviz.sh
```

检查近地地面点云和距离环带分布：

```bash
./scripts/check_vln_lidar_ground_returns.sh
```

## 环境检查

```bash
./scripts/check_mesa_topgear_team_release_project.sh
./scripts/check_repo_release_readiness.sh
```
