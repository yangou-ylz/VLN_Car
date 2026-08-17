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

这是固定完整路线物理巡航自动测试，会自动打开 Unity、启动路线控制、检查图像/点云/odom。希望最后看到 `VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS`，并且日志里 `reached_count=13`、`route_waypoint_count=13`、`stall_count=0`、`skipped_count=0`。

额外强约束：还应看到 `broad_physical_trail_count=0`、`road_physical_slab_count>=7`、`road_seam_transition_count>=5`、`bridge_physics_count>=3`、`short_ramp_physics_count>=1`、`bridge_contact_steps>0`、`short_ramp_contact_steps>0`、`bridge_physical_height_span_m>=0.20`、`short_ramp_physical_height_span_m>=0.62`。如果 `broad_physical_trail_count` 不是 0，或没有桥/短坡接触证据，或桥/坡高度跨度太小，说明又回到了托底/压平难点的方案，不能算通过。

验收目录里还必须有两张证据图：`vln_offroad_scout_wheel_ground_bridge_screenshot.png` 和 `vln_offroad_scout_wheel_ground_short_ramp_screenshot.png`。这两张图用于人工确认桥和坡没有为了通过测试被压平。

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

当前默认路线是 54m、13 个路径点的固定完整路线物理演示，不是完整绕障导航，也不是 VLN 决策器。先用它观察轮地接触、桥/坡通过、传感器跟随和是否穿模。

注意：现在已经禁止使用覆盖整条路线的宽泛隐形平路，也禁止用道路宽桥面或普通路面 slab 托底桥/坡。当前有效路线是受限宽度的可见局部物理体：主路物理 slab、块间 seam、窄桥面/桥头坡、连续可见短坡 MeshCollider；这不是把整条路铺平，而是避免 Unity `WheelCollider` 被视觉小缝误判成硬障碍，同时要求车轮真实接触桥面和短坡。

如果想临时改成更慢的保守参数，可以这样跑自动验收：

```bash
cd /home/ubuntu22/VLN
ROUTE_EXTRA_ARGS='--progress-only-gates --skip-angular-calibration --angular-sign 1 --max-angular 0.30 --angular-gain 0.50 --max-linear 0.85 --linear-gain 0.55 --linear-accel 0.45 --angular-accel 0.18 --min-linear-while-turning 0.45 --stall-skip-seconds 8.0 --stall-skip-forward-margin 4.0' \
./scripts/run_scout_wheel_ground_route_smoke_test.sh
```

这个命令用于后续调参，不作为当前默认完成标准。如果出现 `stall_count>0` 或 `skipped_count>0`，就说明真实物理路线仍未通过。

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

## 12. 当前阶段重点看什么

现在阶段 15 的默认路线已经要求小车走完整桥/坡路线。你手工看时重点盯两件事：轮胎应该连续 360 度滚动，不应该高频前后抖；过独木桥时车轮应该压在可见桥面上，不应该从旧木桥视觉模型下面穿过去。

如果要用自动脚本验收：

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_route_smoke_test.sh
```

希望最后看到 `VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS`。最近一次通过 run id 是 `vln_scout_wheel_ground_route_20260817_125552`，其中 `reached_count=13`、`route_waypoint_count=13`、`stall_count=0`、`skipped_count=0`、`final_lateral_offset=-0.015m`、`bridge_contact_steps=1629`、`short_ramp_contact_steps=1648`、`decorative_bridge_renderer_count=0`、`bridge_deck_renderer_collider_top_delta_m=0.0000`、`bridge_physical_height_span_m=0.235`、`short_ramp_physical_height_span_m=0.804`、`wheel_visual_direction_reversal_count=0`。这个 run 是当前自动路线金标准基线；以后除非明确加入新障碍物、新路线或新物理阶段，否则不要大改，表现变差就优先退回或修回该水平。本次 run 归档了桥区和短坡截图，方便你手工检查有没有压平或托底。

## 13. 手动驾驶记录路线

```bash
cd /home/ubuntu22/VLN
./scripts/start_vln_control_panel.sh
```

浏览器打开 `http://127.0.0.1:8765/`，进入“速度控制”。默认速度是中等速度：线速度 `0.55m/s`，角速度 `0.42rad/s`。

键位：`↑` 前进，`↓` 后退，`←` 或 `A` 左转，`→` 或 `D` 右转。前进/后退可以和左转/右转同时按。

安全保护：浏览器窗口失焦会清空按键并停车；松键、页面隐藏或按键心跳断开超过约 `0.18s`，后端都会立即发布多帧 0 速度。

当前实测方向：`←/A` 发布正 `angular.z` 且在 Unity 中左转，`→/D` 发布负 `angular.z` 且在 Unity 中右转。Unity 物理层负责 yaw-rate 闭环，直行时不会额外给 UI 层叠加角速度。

点击“开始记录”后再开车。满意后点击“停止记录”，再点“导出记录”。文件会生成在：

```text
/home/ubuntu22/VLN/VLN_RECORDINGS/manual_drives/
```

## 14. 回放手动路线

```bash
cd /home/ubuntu22/VLN
./scripts/replay_manual_drive_recording.sh --file /home/ubuntu22/VLN/VLN_RECORDINGS/manual_drives/manual_drive_YYYYMMDD_HHMMSS.json
```

前提是 endpoint 正在运行，Unity 已打开 `VLNOffroadScoutWheelGroundCandidate.unity` 并点击 Play。希望看到小车按导出的速度序列复现行驶，终端最后输出 `VLN_MANUAL_DRIVE_REPLAY_OK`。

如果只想快进测试文件能不能读：

```bash
cd /home/ubuntu22/VLN
./scripts/replay_manual_drive_recording.sh --file /home/ubuntu22/VLN/VLN_RECORDINGS/manual_drives/manual_drive_YYYYMMDD_HHMMSS.json --time-scale 5.0
```

如果回放速度太猛，可以临时降速：

```bash
cd /home/ubuntu22/VLN
./scripts/replay_manual_drive_recording.sh --file /home/ubuntu22/VLN/VLN_RECORDINGS/manual_drives/manual_drive_YYYYMMDD_HHMMSS.json --speed-scale 0.75
```

## 15. 手动记录功能自动验收

```bash
cd /home/ubuntu22/VLN
./scripts/run_control_panel_manual_recording_smoke_test.sh
```

这个脚本不需要打开 Unity，只检查控制面板后端、键位方向、记录导出和 JSON 格式。希望最后看到 `VLN_CONTROL_PANEL_MANUAL_RECORDING_SMOKE_TEST_PASS`。

## 16. 速度控制 Unity 联动验收

```bash
cd /home/ubuntu22/VLN
./scripts/run_control_panel_manual_velocity_unity_smoke_test.sh
```

这个脚本会自动打开 Unity wheel-ground 场景并通过控制面板 HTTP API 测试真实车体响应。希望最后看到 `VLN_CONTROL_PANEL_MANUAL_VELOCITY_UNITY_SMOKE_TEST_PASS`。最新通过 run id 是 `vln_control_panel_manual_velocity_unity_20260817_130258`，已经覆盖 `↑` 前进、A/D 左右转、方向键 `←/→` 左右转和松键停车。
