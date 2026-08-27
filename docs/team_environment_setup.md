# 团队环境部署教程

更新时间：2026-08-27

本文档给团队成员从零部署当前 VLN Unity-ROS2 仿真环境。原则是：Git 仓库只保存代码、配置、小型必要资产和文档；大型 Unity 资产包、Unity 缓存、rosbag、截图、下载缓存和个人运行状态不放进仓库。

## 先给结论

不要把当前本机 50GB 以上的工作目录直接推给团队。正确流程是：先整理仓库和启动脚本，保证 `git clone` 后可复现基础主工程；再用本文档让成员配置 Unity、ROS2 和 ROS-TCP-Endpoint；高精 Mesa/Oasis/Meadow/ForestLake 等大型场景资产由负责人另行提供包文件，按资产流程导入本地副本工程。

当前仓库适合作为团队协作主仓库的内容包括：

- `UnityProjects/VLN_Offroad/Assets`、`Packages`、`ProjectSettings` 中的主 Unity 工程代码和小型资产。
- `scripts/` 中的启动、检查、演示和 ROS2 辅助脚本。
- `config/` 中的稳定配置、RViz 配置和已锁定的 Topgear 传感器/上装位姿文件。
- `docs/`、`AGENTS.md`、`CURRENT_STATE.md`、`workflow.md`、`env.md` 等协作文档。

不应该进入 Git 的内容包括：

- `UnityProjects/VLN_Offroad/Library`、`Temp`、`Logs`、`UserSettings`。
- `UnityProjects/VLN_Offroad_LargeAssetSandbox/` 大资产副本工程。
- `VLN_ASSETS_CACHE/`、`VLN_REFERENCE_LIBRARY/`、`VLN_BAGS/`、`VLN_RECORDINGS/`。
- `*.unitypackage`、`*.bag`、`*.db3`、`*.mcap`、批量截图和本机运行态 JSON。

## 推荐环境

| 项目 | 推荐值 | 说明 |
| --- | --- | --- |
| 系统 | Ubuntu 22.04 | 当前开发机环境基线 |
| Unity | 2022.3.62f1 LTS | 与当前工程 `ProjectVersion.txt` 一致 |
| ROS2 | Humble | 当前 ROS-TCP-Endpoint 和脚本基线 |
| Python | 3.10 | 使用系统 Python 和 ROS2 Python，不需要额外 pip 包 |
| GPU | NVIDIA 独显优先 | 用于 Unity 场景渲染和 RViz 点云显示 |

重要约束：不要为了本项目随意升级 CUDA、PyTorch、Conda、显卡驱动或系统包。ROS2 相关命令建议在干净 shell 中执行，不要在 Conda base 环境里跑。

## 第一次部署

### 1. 获取仓库

```bash
git clone <团队仓库地址> VLN
cd VLN
```

如果仓库 clone 到的不是 `/home/ubuntu22/VLN`，当前核心脚本也能自动按脚本所在目录识别项目根目录。若 Unity Editor 装在非默认路径，运行前设置：

```bash
export UNITY_EDITOR=/path/to/Unity/Editor/Unity
```

### 2. 准备 Unity

安装 Unity `2022.3.62f1`，并确保该 Editor 能正常打开项目。默认脚本优先查找：

```text
<VLN_ROOT>/UnityEditors/2022.3.62f1/Editor/Unity
```

如果使用 Unity Hub 或系统路径安装 Unity，只需要设置 `UNITY_EDITOR` 环境变量即可。国内网络下载 Unity Package Manager 依赖较慢时，可以先打开本地代理，再设置：

```bash
export UNITY_PROXY=http://127.0.0.1:7897/
```

打开主工程：

```bash
./scripts/open_unity_vln_project.sh
```

第一次打开时 Unity 会解析 `Packages/manifest.json` 中的 Git UPM 依赖，包括 ROS-TCP-Connector、UnitySensors、UnitySensorsROS 和 URDF Importer。这个过程可能需要几分钟。

### 3. 准备 ROS-TCP-Endpoint

团队成员本地不会从 Git 仓库拿到 `unity_ros2_ws/install`，需要在自己机器上构建一次：

```bash
./scripts/setup_ros_tcp_endpoint_workspace.sh
```

该脚本只在项目目录内 clone `ROS-TCP-Endpoint`、应用一个已验证的退出补丁并执行 `colcon build`；它不会执行 `apt install`、`pip install`、`conda install` 或 `snap install`。

如果机器没有 ROS2 Humble 或 colcon，请先按团队统一环境安装，不要在本项目脚本里临时混装。

### 4. 标准手工演示流程

终端 A 打开 Unity：

```bash
cd /path/to/VLN
./scripts/open_unity_vln_project.sh
```

在 Unity 中打开场景：

```text
Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity
```

终端 B 启动 ROS-TCP-Endpoint：

```bash
cd /path/to/VLN
./scripts/start_ros_tcp_endpoint.sh
```

回到 Unity 点击 Play。

终端 C 运行金标准自动路线演示：

```bash
cd /path/to/VLN
./scripts/drive_scout_wheel_ground_route_demo.sh
```

如果要查看四路相机：

```bash
./scripts/view_all_camera_images.sh
```

如果要查看 RViz 点云和 TF：

```bash
./scripts/view_vln_vehicle_rviz.sh
```

如果要打开中文网页控制面板：

```bash
./scripts/start_vln_control_panel.sh
```

浏览器地址默认是：

```text
http://127.0.0.1:8765/
```

## 大型世界资产说明

Mesa/Oasis/Meadow/ForestLake 这类完整 Unity 场景资产不进入 Git。团队成员如果需要复现高精荒漠或其它大世界，需要先从负责人处拿到对应 `.unitypackage`，放到：

```text
VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/
```

再创建或刷新大资产副本工程：

```bash
./scripts/prepare_high_precision_large_asset_sandbox_project.sh
```

然后按对应资产导入流程在 Unity 副本工程中导入包。大资产世界统一打开入口是：

```bash
./scripts/open_high_precision_world_model.sh --scene mesa_desert
./scripts/open_high_precision_world_model.sh --scene oasis_desert
./scripts/open_high_precision_world_model.sh --scene mesa_topgear
./scripts/open_high_precision_world_model.sh --scene meadow_forest
./scripts/open_high_precision_world_model.sh --scene forest_lake
```

如果大资产副本工程不存在，脚本会提示先运行 `prepare_high_precision_large_asset_sandbox_project.sh`。不要把副本工程或原始资产包提交到 Git。

## 提交和推送前检查

开发者准备提交前运行：

```bash
./scripts/check_repo_release_readiness.sh
```

准备正式 push 前运行严格检查：

```bash
./scripts/check_repo_release_readiness.sh --strict
```

严格检查会拦截这些问题：没有 Git remote、工作区还有未提交改动、误追踪大资产/缓存/运行态文件、Unity 版本不匹配、关键启动脚本不可执行、部署文档缺失。

标准提交流程：

```bash
git status --short --untracked-files=all
./scripts/check_repo_release_readiness.sh
git add README.md docs/team_environment_setup.md scripts/check_repo_release_readiness.sh scripts/setup_ros_tcp_endpoint_workspace.sh .gitignore
git commit -m "Prepare team environment deployment docs"
git push -u origin main
```

如果 `git remote -v` 没有输出，说明还没有绑定 GitHub/GitLab 仓库，需要先添加远程仓库地址：

```bash
git remote add origin <团队仓库地址>
git push -u origin main
```

## 常见问题

### Unity 打不开

先确认 `UNITY_EDITOR` 指向真实可执行文件：

```bash
echo "$UNITY_EDITOR"
ls -l "$UNITY_EDITOR"
```

如果没有设置 `UNITY_EDITOR`，脚本会默认找 `<VLN_ROOT>/UnityEditors/2022.3.62f1/Editor/Unity`。

### Unity 包下载很慢

先开启本地代理，再设置：

```bash
export UNITY_PROXY=http://127.0.0.1:7897/
```

然后重新运行 `./scripts/open_unity_vln_project.sh`。

### Endpoint 启动失败

先确认 workspace 构建成功：

```bash
ls unity_ros2_ws/install/setup.bash
./scripts/setup_ros_tcp_endpoint_workspace.sh
```

如果端口占用：

```bash
ss -ltnp | grep 10000
```

关闭旧 endpoint 后再启动：

```bash
./scripts/start_ros_tcp_endpoint.sh
```

### RViz 看不到点云

按顺序确认：

```bash
ros2 topic list -t | grep -E '/vln/lidar|/tf'
ros2 topic hz /vln/lidar/points
```

如果没有 `/tf` 或 `/vln/lidar/points`，通常是 Unity 还没点击 Play、Endpoint 没启动，或当前场景不是传感器场景。

### 相机窗口为空

确认 Unity 已 Play 且图像 topic 存在：

```bash
ros2 topic list -t | grep /vln
ros2 topic hz /vln/front/image_raw
```

然后重新运行：

```bash
./scripts/view_all_camera_images.sh
```

## 团队分工建议

- Unity 场景组：维护世界模型、光照、材质、碰撞代理和视觉真实性。
- 车辆物理组：维护 Rigidbody、WheelCollider、摩擦/阻尼和 `/vln/cmd_vel` 响应。
- 传感器组：维护四路鱼眼 CameraInfo/Image、16 线 LiDAR、TF 和 RViz 配置。
- 控制与数据组：维护路线脚本、本地键盘控制、rosbag 记录和分析脚本。
- 文档与回归组：维护本文档、`CURRENT_STATE.md`、发布检查和 smoke test 记录。
