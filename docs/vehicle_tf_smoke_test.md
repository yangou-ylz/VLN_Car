# 阶段 7：可控占位车体与 TF 树闭环

## 目标

阶段 7 的目标是把阶段 6 的静态占位车体变成后续小车/导航/VLN 都会依赖的可控传感器载体，并建立正式 TF 树。当前仍不导入真实 URDF，也不做复杂动力学；重点是验证相机和 LiDAR 挂在 `base_link` 下，并且无 ROS2 控制指令时车体保持静止。

## 文件位置

- Unity 场景：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity`
- 场景生成器：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Editor/VlnOffroadTerrainProjectSetup.cs`
- 运行时 TF 发布器：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Scripts/VlnVehicleTfPublisher.cs`
- TF 校验脚本：`/home/ubuntu22/VLN/scripts/ros2_wait_for_vehicle_tf.py`
- 自动验收脚本：`/home/ubuntu22/VLN/scripts/run_vehicle_tf_smoke_test.sh`

## 标准 TF 树

当前阶段固定使用：

```text
map -> base_link
base_link -> front_camera_optical_frame
base_link -> lidar_link
```

说明：

- `map` 是当前仿真世界固定坐标系。
- `base_link` 是可控占位车体坐标系。
- `front_camera_optical_frame` 是前向 RGB 相机输出 frame。
- `lidar_link` 是 VLP-16 LiDAR 输出 frame。

## 自动验收

关闭正在打开的 Unity Editor 后运行：

```bash
/home/ubuntu22/VLN/scripts/run_vehicle_tf_smoke_test.sh
```

成功标志：

```text
VLN_VEHICLE_TF_SMOKE_TEST_PASS
```

最近一次通过日志：

```text
/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_vehicle_tf_20260814_021645
```

该次结果：

- `/vln/front/image_raw`：`sensor_msgs/msg/Image`，640x480，`rgb8`
- `/vln/front/camera_info`：`sensor_msgs/msg/CameraInfo`
- `/vln/lidar/points`：`sensor_msgs/msg/PointCloud2`，7200 点/帧，有效非零点约 3318
- `/tf`：`tf2_msgs/msg/TFMessage`
- 无 `/vln/cmd_vel` 指令时 `base_link` 最大位移：0.000m

## 注意事项

- 阶段 7 仍然使用程序化占位车体，不代表最终小车模型。
- Unity 点击 Play 后默认不动；如果车体要动，必须由 `/vln/cmd_vel`、路径点控制器或后续 navigation/VLN 模块发控制指令。
- 当前 TF 由 Unity 运行时脚本发布，不需要 RViz 侧再临时发布 `map -> lidar_link`。
- 自动验收仍使用 Unity `-batchmode` 保留图形上下文，不加 `-nographics`。
- 该阶段通过后，下一步才是标准化 topic、RViz、rosbag 和启动顺序；运动能力由阶段 9/10 验证。
