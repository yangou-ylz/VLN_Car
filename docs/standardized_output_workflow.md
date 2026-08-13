# 阶段 8：标准化输出工作流

## 目标

阶段 8 的目标不是增加新算法，而是把已经跑通的越野仿真感知输入固化成后续模块可以稳定接入的接口：topic 命名固定、frame 命名固定、RViz 查看方式固定、rosbag 记录方式固定、启动顺序固定。

这一步完成后，后续再接小车 URDF、导航、建图、VLN/VLA 感知模块时，不应随意改动这些基础接口。

## 当前标准输出

| 项目 | 当前值 |
| --- | --- |
| Unity 场景 | `Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity` |
| ROS-TCP Endpoint | `127.0.0.1:10000` |
| 图像 topic | `/vln/front/image_raw` |
| 图像类型 | `sensor_msgs/msg/Image` |
| 图像规格 | 640x480，`rgb8`，约 5Hz |
| 相机内参 topic | `/vln/front/camera_info` |
| 相机内参类型 | `sensor_msgs/msg/CameraInfo` |
| 点云 topic | `/vln/lidar/points` |
| 点云类型 | `sensor_msgs/msg/PointCloud2` |
| LiDAR 规格 | UnitySensors VLP-16，7200 点/帧，`point_step=16`，约 5Hz |
| TF topic | `/tf` |
| 固定坐标系 | `map` |
| 车体坐标系 | `base_link` |
| 相机 frame | `front_camera_optical_frame` |
| LiDAR frame | `lidar_link` |

当前 TF 树：

```text
map -> base_link
base_link -> front_camera_optical_frame
base_link -> lidar_link
```

## 一键自动验收

关闭正在打开的 Unity Editor 后运行：

```bash
/home/ubuntu22/VLN/scripts/run_standardized_outputs_smoke_test.sh
```

成功标志：

```text
VLN_STANDARDIZED_OUTPUTS_SMOKE_TEST_PASS
```

最近一次通过结果：

```text
run_id=vln_standardized_outputs_20260814_011059
log_dir=/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_standardized_outputs_20260814_011059
bag_dir=/home/ubuntu22/VLN/VLN_BAGS/vln_standardized_outputs_20260814_011059
```

该次自动验收记录到：

- 图像 39 帧
- CameraInfo 39 帧
- 点云 39 帧
- TF 78 条消息
- rosbag 大小约 38.7 MiB
- rosbag 时长约 7.71 秒

## 手工启动顺序

1. 启动 ROS-TCP endpoint：

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

5. 另开终端检查标准输出：

```bash
/home/ubuntu22/VLN/scripts/check_standardized_vln_outputs.sh
```

6. 看图像：

```bash
/home/ubuntu22/VLN/scripts/view_front_image.sh
```

7. 看正式 TF + 点云：

```bash
/home/ubuntu22/VLN/scripts/view_vln_vehicle_rviz.sh
```

8. 记录 8 秒 rosbag 小样本：

```bash
/home/ubuntu22/VLN/scripts/record_vln_sensor_bag_sample.sh
```

也可以指定时长，例如记录 15 秒：

```bash
/home/ubuntu22/VLN/scripts/record_vln_sensor_bag_sample.sh 15
```

## RViz 配置说明

正式 RViz 配置文件：

```text
/home/ubuntu22/VLN/config/vln_vehicle_sensors.rviz
```

该配置使用 `Fixed Frame=map`，显示 `/tf` 与 `/vln/lidar/points`。它不再临时发布静态 TF；如果 RViz 报 frame 错误，优先检查 Unity 是否正在发布 `/tf`。

旧的 LiDAR 专用脚本仍保留：

```bash
/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh
```

它适合阶段 5 的单 LiDAR 场景，会临时发布 `map -> lidar_link`。阶段 7/8 以后优先使用 `view_vln_vehicle_rviz.sh`。

## rosbag 目录规则

rosbag 固定输出到：

```text
/home/ubuntu22/VLN/VLN_BAGS
```

该目录已在 `.gitignore` 中忽略，不提交到 git。不要把 `.db3`、`.mcap`、`.bag`、`.pcd`、`.ply` 等数据文件提交到仓库。

## 故障排查顺序

1. 先检查 endpoint：`ss -ltnp | grep 10000`
2. 再检查 topic：`ros2 topic list -t | grep -E '/vln/(front|lidar)|^/tf '`
3. 再运行：`/home/ubuntu22/VLN/scripts/check_standardized_vln_outputs.sh`
4. 如果 topic 存在但 RViz 没点云，确认 `/tf` 是否存在，不要再用旧的临时静态 TF 思路判断阶段 8。
5. 如果 Unity 卡死，运行：`/home/ubuntu22/VLN/scripts/stop_unity_vln_project.sh`
6. 如果自动验收失败，先看对应 `UnityProjects/_SmokeTestLogs/<run_id>/run_summary.txt`，再看 `unity.log`、`endpoint.log` 和各 ROS2 校验日志。

## 完成定义

阶段 8 完成标准：

- 一键脚本输出 `VLN_STANDARDIZED_OUTPUTS_SMOKE_TEST_PASS`。
- rosbag info 中同时出现 `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/lidar/points`、`/tf`。
- RViz 使用 `config/vln_vehicle_sensors.rviz` 时不需要临时静态 TF。
- 新终端按本文件手工启动顺序可以复现图像、点云和 TF。
