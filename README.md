# VLN Mesa Topgear Unity-ROS2 仿真环境

本仓库用于交付当前主线仿真环境：Pure Nature Mesa 沙漠场景、Topgear 真实物理小车、四路鱼眼相机、16 线 LiDAR，以及 ROS2 `/vln/cmd_vel` 控制链路。

## 快速入口

团队成员第一次部署请先看：[`docs/team_environment_setup.md`](docs/team_environment_setup.md)。该文档只覆盖 `mesa_topgear` 主线，不包含早期测试场景、Oasis/Meadow/ForestLake 等其它资产环境。

标准手工演示顺序：

```bash
cd /path/to/VLN
./scripts/open_mesa_topgear_team_release_project.sh
./scripts/start_ros_tcp_endpoint.sh
./scripts/start_mesa_topgear_local_keyboard_control.sh
```

注意：Unity 打开后先启动 ROS-TCP-Endpoint，再点击 Play，最后运行键盘控制、相机查看或 RViz 脚本。Mesa Topgear Unity 发布工程通过单独资产包分发；大型 Unity 资产、Asset Store 原包、rosbag、截图和本地运行缓存不进入普通 Git 历史。

## 团队交付物

| 交付物 | 内容 |
| --- | --- |
| GitHub 仓库 | 脚本、配置、文档、小型源码和检查工具 |
| Mesa Topgear 发布资产包 | `UnityProjects/VLN_MesaTopgear_TeamRelease`，只包含当前沙漠小车主线 |
| 不交付内容 | 早期低模测试场景、其它大世界、Unity 缓存、原始 `.unitypackage`、开发日志和运行记录 |

维护者生成发布工程：

```bash
./scripts/prepare_mesa_topgear_team_release_project.sh --refresh
./scripts/check_mesa_topgear_team_release_project.sh
./scripts/package_mesa_topgear_team_release_project.sh --split 1900M
```

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
