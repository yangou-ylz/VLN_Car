# VLN Mesa Topgear 仿真环境

本项目提供 Unity-ROS2 仿真环境，用于运行 Mesa 沙漠场景中的 Topgear 小车，并输出四路鱼眼相机、16 线 LiDAR、odom、TF 和 `/vln/cmd_vel` 控制接口。

## 快速开始

部署步骤见：[docs/team_environment_setup.md](docs/team_environment_setup.md)。

标准运行顺序：

```bash
cd /path/to/VLN
./scripts/open_high_precision_world_model.sh --scene mesa_topgear
./scripts/start_ros_tcp_endpoint.sh
./scripts/start_mesa_topgear_local_keyboard_control.sh
```

Unity 打开后，先启动 ROS-TCP-Endpoint，再点击 Play，最后启动键盘控制或传感器显示工具。

## 传感器显示

查看四路鱼眼相机：

```bash
./scripts/view_all_camera_images.sh
```

查看 LiDAR 点云：

```bash
./scripts/view_vln_vehicle_rviz.sh
```

## 环境检查

```bash
./scripts/check_mesa_topgear_team_release_project.sh
./scripts/check_repo_release_readiness.sh
```
