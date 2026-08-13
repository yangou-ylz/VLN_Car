# 阶段 9：ROS2 `/vln/cmd_vel` 控制闭环

## 目标

阶段 9 的目标是验证 ROS2 可以主动控制 Unity 越野场景中的车体运动，而不是只让 Unity 自己自动巡航。这个闭环是后续接 navigation2、路径规划、VLN/VLA 决策模块之前的基础接口。

当前仍不做完整动力学、不接真实底盘控制器、不导入大型小车模型。这里的控制对象还是阶段 7/8 的程序化可控占位车体，但它已经能接收 ROS2 标准速度指令。

## 标准接口

| 项目 | 当前值 |
| --- | --- |
| 控制 topic | `/vln/cmd_vel` |
| 控制消息 | `geometry_msgs/msg/Twist` |
| 线速度字段 | `linear.x`，单位 m/s |
| 角速度字段 | `angular.z`，单位 rad/s |
| 最大线速度 | 2.0 m/s |
| 最大角速度 | 1.2 rad/s |
| 指令超时 | 0.75s |
| 无指令默认行为 | 保持静止；收到过指令后，超时则停在当前位置 |

## 文件位置

- Unity 控制/TF 脚本：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Scripts/VlnVehicleTfPublisher.cs`
- 场景生成器：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Editor/VlnOffroadTerrainProjectSetup.cs`
- ROS2 控制校验脚本：`/home/ubuntu22/VLN/scripts/ros2_send_cmd_vel_and_wait_tf.py`
- 自动验收脚本：`/home/ubuntu22/VLN/scripts/run_cmd_vel_control_smoke_test.sh`

## 自动验收

关闭正在打开的 Unity Editor 后运行：

```bash
/home/ubuntu22/VLN/scripts/run_cmd_vel_control_smoke_test.sh
```

成功标志：

```text
VLN_CMD_VEL_CONTROL_SMOKE_TEST_PASS
```

最近一次通过日志：

```text
/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_cmd_vel_control_20260814_021738
```

该次结果：

- ROS2 发布 `/vln/cmd_vel [geometry_msgs/msg/Twist]`
- Unity 收到 48 条速度指令
- 指令为 `linear.x=0.8`、`angular.z=0.7`，持续约 4 秒，随后发送 0 速度停止
- `base_link` 位移约 2.262m
- `base_link` yaw 变化约 2.851rad
- Unity 控制结果记录 `autopilot_until_first_command=False`
- `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points` 同时通过字段校验

## 手工验证

1. 启动 endpoint：

```bash
/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh
```

2. 打开 Unity 工程并点击 Play：

```bash
/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh
```

Unity 场景：

```text
Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity
```

3. 另开终端发布一段速度指令并校验 TF：

```bash
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
python3 /home/ubuntu22/VLN/scripts/ros2_send_cmd_vel_and_wait_tf.py \
  --cmd-topic /vln/cmd_vel \
  --tf-topic /tf \
  --linear-x 0.8 \
  --angular-z 0.7 \
  --duration 4.0
```

4. 同时可以查看图像和点云：

```bash
/home/ubuntu22/VLN/scripts/view_front_image.sh
/home/ubuntu22/VLN/scripts/view_vln_vehicle_rviz.sh
```

## 注意事项

- `/vln/cmd_vel` 是后续导航/VLN 决策层最可能复用的控制入口，后续不要随意改名。
- 当前控制是轻量运动学模型，只用于接口和感知闭环验证，不代表真实底盘动力学。
- Unity Play 后不再自动巡航；这是为了保证数据采集和路径点控制的起点可控。
- 指令超时后停止，是为了防止 ROS2 端崩溃或关闭后 Unity 车体继续乱跑。
- 阶段 9 通过后，后续可以在不破坏感知 topic 的前提下接 navigation2、路径点控制或真实小车模型。
