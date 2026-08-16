# 阶段 14/15：Scout wheel-ground 运行命令

本文件只记录常用操作命令。所有命令默认在 `/home/ubuntu22/VLN` 下执行。

## 0. 先清理 Unity 残留锁

```bash
cd /home/ubuntu22/VLN
./scripts/stop_unity_vln_project.sh
```

用于避免 Unity 上次退出后残留 lock。希望看到 `Library 下未发现残留 lock 文件`。

## 1. 一键自动验收

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_smoke_test.sh
```

这是最省事的完整自动测试。希望最后看到 `VLN_SCOUT_WHEEL_GROUND_SMOKE_TEST_PASS`。

## 1A. 一键固定路线自动验收

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_route_smoke_test.sh
```

这是固定完整路线物理巡航自动测试，会自动打开 Unity、启动路线控制、检查图像/点云/odom。希望最后看到 `VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS`，并且日志里 `reached_count` 为 11、`total_forward_progress` 约 53 米。

## 2. 手工看效果：终端 1 启动 ROS-TCP-Endpoint

```bash
cd /home/ubuntu22/VLN
./scripts/start_ros_tcp_endpoint.sh
```

这个终端保持开着，不要关。希望看到 `Starting server on 127.0.0.1:10000`，Unity 点击 Play 后会出现 `Connection from 127.0.0.1`。

## 3. 手工看效果：终端 2 打开 Unity 工程

```bash
cd /home/ubuntu22/VLN
./scripts/open_unity_vln_project.sh
```

Unity 打开后，进入 `Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，然后点击顶部 Play。希望看到 Scout 小车在越野场景中，四轮贴地。

## 4. 手工看效果：终端 3 检查 ROS2 topic

```bash
cd /home/ubuntu22/VLN
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic list -t | grep -E '/vln|/tf'
```

用于确认 Unity 已经向 ROS2 发数据。希望看到 `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points`、`/vln/cmd_vel`、`/vln/odom`、`/tf`。

## 5. 手工看效果：终端 4 看相机图像

```bash
cd /home/ubuntu22/VLN
./scripts/view_front_image.sh
```

用于打开 rqt 图像窗口。希望能看到 Unity 相机画面；如果没有自动选中 topic，就手动选择 `/vln/front/image_raw`。

## 6. 手工看效果：终端 5 看 LiDAR 点云

```bash
cd /home/ubuntu22/VLN
./scripts/view_vln_vehicle_rviz.sh
```

用于打开 RViz。希望能看到 `/vln/lidar/points` 点云，Fixed Frame 使用 `map`，TF 不报错。

## 7. 手工控制方式 A：启动中文控制面板

```bash
cd /home/ubuntu22/VLN
./scripts/start_vln_control_panel.sh
```

浏览器打开 `http://127.0.0.1:8765/`。输入相对目标坐标并发送后，希望 Unity 里的小车移动，RViz 里的 TF/点云跟着变。

## 7A. 手工控制方式 A2：运行固定路线物理巡航

```bash
cd /home/ubuntu22/VLN
./scripts/drive_scout_wheel_ground_route_demo.sh
```

前提是终端 1 的 endpoint 正在运行，Unity 已打开 `VLNOffroadScoutWheelGroundCandidate.unity` 并点击 Play。希望看到终端持续输出路径点进度，小车沿前方完整路线通过桥/坡区域并跑向终点方向，最后出现 `VLN_SCOUT_PHYSICS_ROUTE_MSG_OK`。

如果想临时改路线，例如前进 12 米：

```bash
cd /home/ubuntu22/VLN
./scripts/drive_scout_wheel_ground_route_demo.sh --relative-waypoints '4.0,0.0;8.0,0.0;12.0,0.0' --min-reached 3 --min-total-progress 9.0
```

当前默认路线是 54m 固定完整路线物理演示，不是完整绕障导航，也不是 VLN 决策器。先用它观察轮地接触、桥/坡通过、传感器跟随和是否穿模。

如果想临时改成更慢的保守参数，可以这样跑自动验收：

```bash
cd /home/ubuntu22/VLN
ROUTE_EXTRA_ARGS='--progress-only-gates --skip-stalled-waypoints --skip-angular-calibration --angular-sign -1 --max-angular 0.30 --angular-gain 0.50 --max-linear 0.85 --linear-gain 0.55 --linear-accel 0.45 --angular-accel 0.18 --min-linear-while-turning 0.45 --stall-skip-seconds 8.0 --stall-skip-forward-margin 4.0' \
./scripts/run_scout_wheel_ground_route_smoke_test.sh
```

这个命令用于后续调参，不作为当前默认完成标准。

## 8. 手工控制方式 B：直接发 `/vln/cmd_vel`

```bash
cd /home/ubuntu22/VLN
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic pub --rate 10 /vln/cmd_vel geometry_msgs/msg/Twist "{linear: {x: 0.5}, angular: {z: 0.0}}"
```

用于直接让车低速前进。希望 Unity 小车向前运动；停止时按 `Ctrl+C`。

## 9. 停车命令

```bash
cd /home/ubuntu22/VLN
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic pub --once /vln/cmd_vel geometry_msgs/msg/Twist "{linear: {x: 0.0}, angular: {z: 0.0}}"
```

用于给小车发一次零速度。希望车辆停止，`/vln/odom` 仍继续发布。

## 10. 查看 odom

```bash
cd /home/ubuntu22/VLN
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic echo --once /vln/odom
```

用于确认里程计存在。希望看到 `header.frame_id: map` 和 `child_frame_id: base_link`。

## 11. 退出后再次清理

```bash
cd /home/ubuntu22/VLN
./scripts/stop_unity_vln_project.sh
```

Unity 退出后建议跑一次。希望没有 Unity 残留进程，也没有 Library lock。
