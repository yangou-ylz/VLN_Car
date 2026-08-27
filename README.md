# VLN Unity-ROS2 仿真环境

本仓库用于搭建 Unity3D 越野仿真环境，并通过 ROS2 输出 VLN 感知层需要的相机图像、CameraInfo、3D LiDAR 点云、TF、odom 和 `/vln/cmd_vel` 控制入口。

## 快速入口

团队成员第一次部署请先看：[`docs/team_environment_setup.md`](docs/team_environment_setup.md)。

已完成环境下的标准手工演示顺序：

```bash
cd /path/to/VLN
./scripts/open_unity_vln_project.sh
./scripts/start_ros_tcp_endpoint.sh
./scripts/drive_scout_wheel_ground_route_demo.sh
```

注意：Unity 打开后需要在软件中打开目标场景并点击 Play，再运行路线或查看传感器脚本。大型 Unity 资产、Asset Store 包、rosbag、截图和本地运行缓存不进入 Git。

## 发布检查

提交或推送前运行：

```bash
cd /path/to/VLN
./scripts/check_repo_release_readiness.sh
```

如果要做严格的 push 前检查：

```bash
./scripts/check_repo_release_readiness.sh --strict
```
