# Unity-ROS2 最小通信闭环

本页记录阶段 3 的最小可验证闭环。当前测试只验证通信链路，不涉及相机、LiDAR、越野环境或小车模型。

## 测试内容

- Unity 发布 `std_msgs/msg/String` 到 `/unity/heartbeat`。
- ROS2 使用 `ros2 topic echo --once /unity/heartbeat std_msgs/msg/String` 接收 Unity 消息。
- ROS2 发布 `std_msgs/msg/String` 到 `/ros2/command`。
- Unity 订阅 `/ros2/command`，收到后写入 `UnityProjects/VLN_Offroad/Logs/vln_ros2_smoke_result.txt`。

## 一键验收

```bash
/home/ubuntu22/VLN/scripts/run_ros2_unity_smoke_test.sh
```

成功标志：

```text
VLN_ROS2_SMOKE_TEST_PASS
```

最近一次通过记录：

```text
run_id=vln_smoke_20260813_224611
unity_status=0
echo_status=0
pub_status=0
log_dir=/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_smoke_20260813_224611
```

## 关键路径

- Unity 工程：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`
- 测试场景：`Assets/VLN/Scenes/ROS2SmokeTest.unity`
- Unity 发布/订阅脚本：`Assets/VLN/Scripts/VlnRos2SmokeTest.cs`
- 场景生成脚本：`Assets/VLN/Editor/VlnRos2ProjectSetup.cs`
- 批处理 runner：`Assets/VLN/Editor/VlnRos2SmokeTestRunner.cs`
- ROS endpoint 启动脚本：`/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh`
- 闭环验收脚本：`/home/ubuntu22/VLN/scripts/run_ros2_unity_smoke_test.sh`

## 当前约定

- ROS-TCP-Endpoint 监听：`127.0.0.1:10000`
- Unity ROS IP：`127.0.0.1`
- Unity ROS Port：`10000`
- Unity 编译符号：`ROS2`
- Unity 版本：`2022.3.62f1`
- ROS2：Humble，通过用户已有 `ros2env` 进入环境

## 下一步

阶段 3 已通过。后续进入阶段 4 前，不要导入大越野资产；先导入 UnitySensors / UnitySensorsROS，并做相机 `sensor_msgs/msg/Image` 的最小闭环。
