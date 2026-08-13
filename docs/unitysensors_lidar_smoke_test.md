# UnitySensors LiDAR 点云闭环记录

本页记录阶段 5 的最小可验证闭环。当前测试验证 UnitySensors LiDAR 能通过 ROS-TCP-Connector 输出标准 ROS2 点云消息，不涉及小车模型、完整 TF 树、大型越野资产或 VLN 算法训练。

## 当前结论

- 阶段状态：已通过。
- 最近通过 run id：`vln_lidar_20260813_230736`。
- 成功标志：`VLN_UNITYSENSORS_LIDAR_SMOKE_TEST_PASS`。
- 日志目录：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_lidar_20260813_230736`。

## 产物文件

- Unity 场景：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Scenes/UnitySensorsLidarSmokeTest.unity`
- Unity 场景构建器：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Editor/VlnUnitySensorsLidarProjectSetup.cs`
- Unity 批处理 runner：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Editor/VlnUnitySensorsLidarSmokeTestRunner.cs`
- Unity 运行时脚本：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Assets/VLN/Scripts/VlnUnitySensorsLidarSmokeTest.cs`
- ROS2 字段校验脚本：`/home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py`
- 一键验收脚本：`/home/ubuntu22/VLN/scripts/run_unitysensors_lidar_smoke_test.sh`

## 点云规格

- Topic：`/vln/lidar/points`
- ROS2 类型：`sensor_msgs/msg/PointCloud2`
- Frame：`lidar_link`
- UnitySensors 传感器：`RaycastLiDARSensor`
- Scan pattern：VLP-16
- 点数：7200 点/帧
- `height`：1
- `point_step`：16 bytes
- `row_step`：115200 bytes
- 字段：`x`、`y`、`z`、`intensity`
- 频率：约 5Hz
- 当前带宽：约 0.6 MB/s
- 最近校验非零点：4744 / 7200

## 一键验收

运行：

```bash
/home/ubuntu22/VLN/scripts/run_unitysensors_lidar_smoke_test.sh
```

脚本会自动执行：

- 启动 `/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh`。
- 以 batchmode 打开 Unity 工程并运行 `VLN.Editor.VlnUnitySensorsLidarSmokeTestRunner.Run`。
- 等待并校验 `/vln/lidar/points` 的 `PointCloud2` 字段。
- 采集 `ros2 topic hz /vln/lidar/points`。
- 采集 `ros2 topic bw /vln/lidar/points`。
- 检查 `ros2 topic list -t` 中存在 `/vln/lidar/points [sensor_msgs/msg/PointCloud2]`。

成功时末尾应看到：

```text
VLN_UNITYSENSORS_LIDAR_SMOKE_TEST_PASS
```

## 手工查看点云

先启动 endpoint：

```bash
/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh
```

再打开 Unity 工程：

```bash
/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh
```

在 Unity 中打开 `Assets/VLN/Scenes/UnitySensorsLidarSmokeTest.unity`，点击 Play。另开终端：

```bash
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
rviz2
```

推荐直接运行 `/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh`，它会临时发布 `map -> lidar_link` 静态 TF 并加载固定 RViz 配置。手工设置时：

- `Fixed Frame`：`map`
- 添加显示项：`PointCloud2`
- Topic：`/vln/lidar/points`

当前阶段还没有完整 TF 树，因此先用临时静态 TF 让 RViz 能显示点云。后续加入小车和标准化 topic/TF 后，再改成正式的 `map`、`odom`、`base_link`、`lidar_link` 关系。

## 手工命令验收

在 endpoint 和 Unity 场景运行期间：

```bash
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic list -t | grep /vln/lidar
ros2 topic hz /vln/lidar/points
ros2 topic bw /vln/lidar/points
python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_pointcloud2_once.py --topic /vln/lidar/points --width 7200 --point-step 16 --frame-id lidar_link --timeout 20 --min-nonzero-points 20
```

字段校验成功时应看到：

```text
VLN_UNITYSENSORS_POINTCLOUD2_MSG_OK
```

## 当前限制

- 这是低负载点云基线，不代表最终传感器精度。
- 当前 LiDAR 是静态场景中的传感器，还没有挂到小车 `base_link` 下。
- 当前没有正式 TF 树，RViz2 可视化先通过 `view_lidar_rviz.sh` 临时发布 `map -> lidar_link`。
- 当前没有导入大型越野资产；下一阶段只做极简 terrain 原型。
- 不要在相机和点云回归测试不能通过时继续导入小车或大型模型。

## 已知问题

- Unity 可能因残留 `Library/ArtifactDB-lock`、`Library/SourceAssetDB-lock` 误报工程已有实例打开。处理方式是先确认没有真实 Unity 进程，再只移动 lock 文件保留现场，不删除整个 `Library/`。
- `ros2 topic bw` 在当前 ROS2 Humble 中输出 `KB/s from ... messages`，不是 `average:`；脚本已按当前格式验收。
- Unity batch 退出后 endpoint 可能记录 `No more data available`，只要点云字段校验和总脚本 PASS，该日志为非致命断开信息。
