# 阶段 10：ROS2 路径点控制闭环

## 目标

阶段 10 的目标是把“直接发速度”推进到“ROS2 根据 TF 自动算速度”。也就是说，ROS2 不再只是手工发 `/vln/cmd_vel`，而是读取 `/tf` 中的 `map -> base_link`，根据目标路径点计算线速度和角速度，再持续发布 `/vln/cmd_vel` 控制 Unity 车体运动。

这一步仍不是完整 navigation2，也不是完整 VLN 算法。它是后续接路径规划、导航栈或 VLN 决策模块前的最小可验证控制闭环。

## 标准接口

| 项目 | 当前值 |
| --- | --- |
| 输入 TF | `/tf`，`map -> base_link` |
| 输出控制 | `/vln/cmd_vel` |
| 控制消息 | `geometry_msgs/msg/Twist` |
| 默认相对路径点 | `1.2,0.0;2.4,0.0` |
| 路径点含义 | 以启动时 `base_link` 为原点，x 为前向米，y 为左向米 |
| 默认到点阈值 | 0.35m |
| 默认最大线速度 | 0.9m/s |
| 默认最大角速度 | 0.9rad/s |

## 文件位置

- ROS2 路径点控制器：`/home/ubuntu22/VLN/scripts/ros2_drive_waypoints.py`
- 一键自动验收脚本：`/home/ubuntu22/VLN/scripts/run_waypoint_control_smoke_test.sh`
- Unity 控制入口：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Scripts/VlnVehicleTfPublisher.cs`

## 自动验收

关闭正在打开的 Unity Editor 后运行：

```bash
/home/ubuntu22/VLN/scripts/run_waypoint_control_smoke_test.sh
```

成功标志：

```text
VLN_WAYPOINT_CONTROL_SMOKE_TEST_PASS
```

最近一次通过日志：

```text
/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_waypoint_control_20260814_021829
```

该次结果：

- 相对路径点：`1.2,0.0;2.4,0.0`
- 到达路径点数量：2/2
- 总位移：约 2.100m
- 最终距离最后路径点：约 0.300m
- ROS2 发布 `/vln/cmd_vel` 共 49 条
- Unity 收到 49 条速度指令
- `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points` 同时通过字段校验

## 手工验证

1. 启动 endpoint：

```bash
/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh
```

2. 打开 Unity 工程：

```bash
/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh
```

3. 在 Unity 中打开主场景并点击 Play：

```text
Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity
```

4. 另开终端运行路径点控制器：

```bash
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
python3 /home/ubuntu22/VLN/scripts/ros2_drive_waypoints.py \
  --cmd-topic /vln/cmd_vel \
  --tf-topic /tf \
  --relative-waypoints '1.2,0.0;2.4,0.0'
```

5. 同时看图像和点云：

```bash
/home/ubuntu22/VLN/scripts/view_front_image.sh
/home/ubuntu22/VLN/scripts/view_vln_vehicle_rviz.sh
```

## 注意事项

- 路径点是相对启动时 `base_link` 的，不是 Unity 世界坐标的绝对点。
- 当前控制器是轻量纯追踪控制器，用于验证 ROS2 侧闭环控制，不替代 navigation2。
- Unity Play 后默认静止，所以路径点控制器启动前不会因为 Unity 自动巡航而改变起点。
- 如果后续要接 navigation2 或 VLN 决策，优先让上层模块继续输出 `/vln/cmd_vel`，不要绕开这个已经验证过的控制入口。
- 自动验收时仍同步校验图像、CameraInfo 和点云，避免控制改动破坏感知输入。
