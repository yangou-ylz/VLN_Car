# Scout wheel-ground 手工运行命令

本文件按你的实际使用习惯写：先打开 Unity 软件，再开终端跑控制/查看脚本。`run_*_smoke_test.sh` 是自动回归验收用的，不是你平时看效果的首选入口。

所有命令默认在 `/home/ubuntu22/VLN` 下执行。

## 0. 如果 Unity 卡住或异常退出，先清理残留锁

```bash
cd /home/ubuntu22/VLN
./scripts/stop_unity_vln_project.sh
```

正常情况不用每次都跑；只有 Unity 卡住、异常退出、或者提示工程被占用时再跑。希望看到 `Library 下未发现残留 lock 文件`。

## 1. 终端 1：打开 Unity 工程

```bash
cd /home/ubuntu22/VLN
./scripts/open_unity_vln_project.sh
```

Unity 打开后，进入场景 `Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`。先确认场景是 Scout 小车、独木桥、斜坡和后段挑战路段。

## 2. 终端 2：启动 ROS-TCP-Endpoint

```bash
cd /home/ubuntu22/VLN
./scripts/start_ros_tcp_endpoint.sh
```

这个终端保持开着，不要关。希望看到 `Starting server on 127.0.0.1:10000`。

## 3. 回 Unity：点击 Play

点击 Unity 顶部 Play。希望 endpoint 终端出现 `Connection from 127.0.0.1`，Unity 里 Scout 小车四轮贴地，传感器跟随车体。

## 4. 终端 3：运行原 13 点自动路线演示

```bash
cd /home/ubuntu22/VLN
./scripts/drive_scout_wheel_ground_route_demo.sh
```

这是手工演示入口，不会自动打开 Unity。希望看到终端持续输出路径点进度，小车沿路线通过独木桥和斜坡，最后出现 `VLN_SCOUT_PHYSICS_ROUTE_MSG_OK`。

## 4A. 可选：在 Unity 菜单里运行路线

Unity 顶部菜单打开：`VLN -> ROS2 手工演示面板`。

面板里的推荐顺序也是：`打开 Scout 场景` -> `启动 ROS-TCP-Endpoint` -> 回 Unity 点 `Play` -> `运行 13 点自动路线` 或 `运行 16 点挑战路线`。

这个面板只是帮你开新终端执行现有脚本，底层仍然是 ROS2 发布 `/vln/cmd_vel`，不是 Unity 内置导航。

## 5. 终端 3：运行新增后段挑战路线演示

```bash
cd /home/ubuntu22/VLN
./scripts/drive_scout_wheel_ground_challenge_route_demo.sh
```

这是新增草地、青石路、沙地和低矮障碍的手工演示入口，也不会自动打开 Unity。希望小车先通过原来的桥/坡，再继续走到后段挑战区；终端最后应出现 `VLN_SCOUT_PHYSICS_ROUTE_MSG_OK`。

当前阶段 18A 已给青石路和沙地接入 1K PBR 贴图；手工看效果仍然使用这个挑战路线演示脚本，不需要换新命令。

## 6. 终端 4：检查 ROS2 topic

```bash
cd /home/ubuntu22/VLN
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic list -t | grep -E '/vln|/tf'
```

希望看到 `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points`、`/vln/cmd_vel`、`/vln/odom`、`/tf`。

## 7. 终端 5：看相机图像

```bash
cd /home/ubuntu22/VLN
./scripts/view_front_image.sh
```

用于打开 rqt 图像窗口。希望能看到 Unity 相机画面；如果没有自动选中 topic，就手动选择 `/vln/front/image_raw`。

## 8. 终端 6：看 LiDAR 点云

```bash
cd /home/ubuntu22/VLN
./scripts/view_vln_vehicle_rviz.sh
```

用于打开 RViz。希望能看到 `/vln/lidar/points` 点云，Fixed Frame 使用 `map`，TF 不报错。

## 9. 可选：启动中文控制面板

```bash
cd /home/ubuntu22/VLN
./scripts/start_vln_control_panel.sh
```

浏览器打开 `http://127.0.0.1:8765/`。目标位置、速度控制、相机视图、雷达点云都从这里进。

## 10. 可选：直接发 `/vln/cmd_vel`

```bash
cd /home/ubuntu22/VLN
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic pub --once /vln/cmd_vel geometry_msgs/msg/Twist "{linear: {x: 0.5}, angular: {z: 0.0}}"
```

用于快速确认控制链路。发完后如果车还在动，用下面命令停车：

```bash
ros2 topic pub --once /vln/cmd_vel geometry_msgs/msg/Twist "{linear: {x: 0.0}, angular: {z: 0.0}}"
```

## 11. 自动回归验收：我排查或改代码后使用

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_route_smoke_test.sh
```

这是 13 点路线自动回归，会自己打开 batch Unity、启动 endpoint、检查图像/点云/odom/路线指标。希望最后看到 `VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS`。

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh
```

这是 16 点后段挑战路线自动回归。希望最后同时看到 `VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS` 和 `VLN_SCOUT_WHEEL_GROUND_CHALLENGE_ROUTE_SMOKE_TEST_PASS`。

注意：如果 Unity Editor 已经手工打开，先不要跑这两个 `run_*_smoke_test.sh`，因为同一工程不能同时被两个 Unity Editor 实例打开。你手工看效果时，用第 4/5 步的 `drive_*_demo.sh`。

## 当前基线

- 13 点自动路线当前通过 run id：`vln_scout_wheel_ground_route_20260817_183540`。
- 16 点挑战路线当前通过 run id：`vln_scout_wheel_ground_challenge_route_20260817_182912`。
- 挑战区当前已归档三段截图：草地、青石路、沙地；自动回归会检查三段截图和视觉细节数量。
- 关键约束：禁止隐藏托底、压平桥/坡、关闭碰撞、跳过卡点或放宽 gate 来掩盖失败。
