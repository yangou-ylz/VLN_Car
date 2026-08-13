# 技术路线与方案对比

## 当前阶段选择

本项目当前采用：Unity3D + ROS-TCP-Connector + ROS-TCP-Endpoint + UnitySensors / UnitySensorsROS + ROS2 Humble。

该路线服务于一个明确目标：先在仿真中稳定产生 VLN 感知层需要的相机图像和 3D LiDAR 点云，而不是马上实现完整 VLN 推理、训练或真实车控制。

## 为什么先用 Unity

- 师兄给定参考仓库就是 Unity Robotics Hub、ROS-TCP-Connector、UnitySensors。
- 越野视觉场景需要 terrain、植被、石头、土路、光照和材质，Unity 的资产生态和视觉搭建效率高。
- UnitySensors 已经覆盖 RGB Camera、Panoramic Camera、Velodyne 3D LiDAR、Livox 3D LiDAR，并有 UnitySensorsROS 负责通过 ROS-TCP-Connector 发 ROS 数据。
- Unity Robotics Hub 提供 ROS-Unity 通信、URDF 导入和官方教程。

## 方案对比

| 方案 | 优势 | 劣势 | 当前建议 |
| --- | --- | --- | --- |
| Unity + ROS-TCP + UnitySensors | 视觉场景强、越野资产丰富、相机和 LiDAR 插件现成、符合师兄方向 | ROS2 不是原生通信，点云/图像大流量可能吃 TCP bridge，坐标系要谨慎 | 主路线 |
| Gazebo / gz-sim + ros_gz_bridge | ROS2 机器人传统工作流成熟，物理/URDF/Nav2 接入自然 | 视觉资产和越野真实感通常不如 Unity，搭漂亮户外场景成本高 | 备用机器人动力学/导航验证路线 |
| Isaac Sim | RTX 传感器、ROS2 Bridge、GPU 仿真强，适合高保真机器人仿真 | 环境重，对显存和驱动要求高，50 系列/本机环境不宜一开始折腾 | 后续高保真传感器路线，不作为第一阶段 |
| CARLA | 自动驾驶城市道路传感器成熟 | 偏城市道路，不适合普通越野 VLN 场景；不是师兄指定方向 | 暂不采用 |

## 当前最小闭环

1. ROS2 端启动 ROS-TCP-Endpoint。
2. Unity 端导入 ROS-TCP-Connector。
3. Unity 发布一个测试 topic，ROS2 能 `echo`。
4. UnitySensors 发布相机图像，ROS2 能 `rqt_image_view`。
5. UnitySensors 发布 LiDAR 点云，ROS2 能 RViz2 显示 `PointCloud2`。
6. 加入极简越野 terrain，确认图像和点云随环境变化。
7. 加入小车模型或占位车体，传感器随车体运动。

## 标准 topic 草案

| 功能 | Topic | ROS2 类型 | 初期频率 |
| --- | --- | --- | --- |
| 前向 RGB | `/sim/camera/front/image_raw` | `sensor_msgs/msg/Image` | 10-20Hz |
| 相机内参 | `/sim/camera/front/camera_info` | `sensor_msgs/msg/CameraInfo` | 10-20Hz |
| 全景图像 | `/sim/camera/panorama/image_raw` | `sensor_msgs/msg/Image` | 5-10Hz |
| LiDAR 点云 | `/sim/lidar/points` | `sensor_msgs/msg/PointCloud2` | 5-10Hz |
| 车体控制 | `/cmd_vel` | `geometry_msgs/msg/Twist` | 按控制器 |
| 坐标变换 | `/tf`、`/tf_static` | `tf2_msgs/msg/TFMessage` | 按 frame |

## frame 草案

- `map`：世界坐标。
- `odom`：里程计坐标。
- `base_link`：小车主体。
- `lidar_link`：LiDAR 物理 frame。
- `camera_link`：相机物理 frame。
- `camera_optical_frame`：相机 optical frame。

## 关键工程约束

- Unity 坐标系与 ROS 坐标系不同，必须显式处理转换。
- RViz2 看不到点云时优先查 TF，而不是先怀疑 LiDAR。
- 图像和点云 topic 优先控制频率、分辨率和点数，先稳定再提高质量。
- 每个 topic 都必须能用 `ros2 topic info`、`ros2 topic hz`、`ros2 topic bw` 验证。
- 每次新增传感器或 frame，都要更新 `env.md` 或本文件。
