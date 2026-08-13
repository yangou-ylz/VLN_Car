# UnitySensors 相机图像闭环

本页记录阶段 4 的最小可验证闭环。当前测试只验证 UnitySensors RGB 相机能通过 ROS-TCP-Connector 输出标准 ROS2 图像消息，不涉及 LiDAR、越野环境、小车模型或 VLN 算法。

## 测试内容

- Unity 场景中创建 `RGBCameraSensor`。
- UnitySensorsROS 发布 `/vln/front/image_raw`。
- ROS2 接收并校验 `sensor_msgs/msg/Image` 字段。
- 同时发布 `/vln/front/camera_info`，供后续视觉模块使用。
- 使用简单地面和三个彩色目标，保证图像不是空场景。

## 一键验收

```bash
/home/ubuntu22/VLN/scripts/run_unitysensors_image_smoke_test.sh
```

成功标志：

```text
VLN_UNITYSENSORS_IMAGE_SMOKE_TEST_PASS
```

最近一次通过记录：

```text
run_id=vln_image_20260813_230503
unity_status=0
image_status=0
hz_status=0
topic_status=0
log_dir=/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_image_20260813_230503
```

## 当前输出规格

| 项目 | 当前值 |
| --- | --- |
| 图像 topic | `/vln/front/image_raw` |
| 图像类型 | `sensor_msgs/msg/Image` |
| 相机内参 topic | `/vln/front/camera_info` |
| 相机内参类型 | `sensor_msgs/msg/CameraInfo` |
| frame | `front_camera_optical_frame` |
| 分辨率 | 640x480 |
| 编码 | `rgb8` |
| 频率 | 约 5Hz |
| ROS endpoint | `127.0.0.1:10000` |

## ROS2 字段验收

一键脚本内部会运行：

```bash
python3 /home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py \
  --topic /vln/front/image_raw \
  --width 640 \
  --height 480 \
  --encoding rgb8 \
  --frame-id front_camera_optical_frame \
  --timeout 70
```

通过时会看到：

```text
topic=/vln/front/image_raw
frame_id=front_camera_optical_frame
width=640
height=480
encoding=rgb8
step=1920
data_len=921600
VLN_UNITYSENSORS_IMAGE_MSG_OK
```

`ros2 topic hz /vln/front/image_raw` 当前稳定在约 5Hz。

## 关键路径

- Unity 工程：`/home/ubuntu22/VLN/UnityProjects/VLN_Offroad`
- 测试场景：`Assets/VLN/Scenes/UnitySensorsImageSmokeTest.unity`
- 场景生成脚本：`Assets/VLN/Editor/VlnUnitySensorsImageProjectSetup.cs`
- 批处理 runner：`Assets/VLN/Editor/VlnUnitySensorsImageSmokeTestRunner.cs`
- 运行时退出脚本：`Assets/VLN/Scripts/VlnUnitySensorsImageSmokeTest.cs`
- ROS2 图像校验脚本：`/home/ubuntu22/VLN/scripts/ros2_wait_for_image_once.py`
- 一键闭环脚本：`/home/ubuntu22/VLN/scripts/run_unitysensors_image_smoke_test.sh`

## 已知非致命日志

- Unity 许可证日志可能出现一次 `Access token is unavailable; failed to update`，随后同一日志显示 `Successfully updated license` 和 `Successfully resolved entitlement details`，当前不影响批处理运行。
- Unity batch 正常退出后 endpoint 可能记录 `Exception: No more data available`，这是 Unity 关闭 TCP 连接后的断开日志；只要 ROS2 已收到图像并输出成功标志，就不作为失败处理。

## 下一步

阶段 4 已通过，阶段 5 LiDAR 点云闭环也已通过。下一阶段进入极简越野 terrain 闭环：保留当前相机 topic `/vln/front/image_raw`，让相机看到地面、坡、石头、土路等越野元素，同时继续回归点云闭环。
