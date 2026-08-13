# 阶段 6：极简越野 Terrain 联合闭环

## 目标

阶段 6 的目标是验证师兄路线中的“越野仿真环境 + 感知层输入”已经开始接起来：同一个 Unity 场景里同时提供前向相机图像和 3D LiDAR 点云，并且场景里不再只是简单方块，而是有轻量越野地形、土路、坡、石块、树木和障碍物。

当前仍不是最终 VLN 算法，也不是小车动力学阶段。这里的小车只是静态占位车体，用来承载相机和 LiDAR 的空间位置。

## 文件位置

- Unity 场景：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity`
- 场景生成器：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Editor/VlnOffroadTerrainProjectSetup.cs`
- Batch runner：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Editor/VlnOffroadTerrainSmokeTestRunner.cs`
- 运行时控制器：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Scripts/VlnOffroadTerrainSmokeTest.cs`
- 自动验收脚本：`/home/ubuntu22/VLN/scripts/run_offroad_terrain_smoke_test.sh`

## 自动验收

关闭正在打开的 Unity Editor 后运行：

```bash
/home/ubuntu22/VLN/scripts/run_offroad_terrain_smoke_test.sh
```

成功标志：

```text
VLN_OFFROAD_TERRAIN_SMOKE_TEST_PASS
```

最近一次通过日志：

```text
/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_offroad_20260814_004120
```

该次结果：

- `/vln/front/image_raw`：`sensor_msgs/msg/Image`，640x480，`rgb8`，约 5Hz
- `/vln/front/camera_info`：`sensor_msgs/msg/CameraInfo`
- `/vln/lidar/points`：`sensor_msgs/msg/PointCloud2`，7200 点/帧，约 5Hz
- 点云有效非零点：3316
- 点云带宽：约 0.58 MB/s

## 手工查看

1. 启动 endpoint：

```bash
/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh
```

2. 打开 Unity 工程：

```bash
/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh
```

3. 在 Unity 中打开场景：

```text
Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity
```

4. 点击 Unity 顶部 Play。

5. 另开终端看图像：

```bash
/home/ubuntu22/VLN/scripts/view_front_image.sh
```

6. 另开终端看点云：

```bash
/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh
```

## 注意事项

- 不要并行运行多个 Unity smoke test，同一工程不能被两个 Unity Editor 实例同时打开。
- 阶段 6 自动验收脚本使用 `-batchmode`，不要加 `-nographics`。当前机器上复杂 terrain + UnitySensors RGB 相机场景在 `-nographics` 下会触发 Unity 图形渲染段错误。
- 当前地形是程序化轻量网格地形，不是外部大型资产包。这个选择是为了先形成稳定、可复现、低负载的越野 baseline。
- 后续导入真实越野模型或资产前，必须先回归本阶段脚本，确保相机和 LiDAR 基线没被破坏。
