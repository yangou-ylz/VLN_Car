# 本机仿真能力评估

## 结论

当前电脑可以承担本项目第一阶段和第二阶段：Unity 越野环境、单车、相机图像、3D LiDAR 点云、ROS2 topic 输出与 RViz2 验证。

但这不是无限制高保真训练机。主要瓶颈是 8GB 显存，而不是 CPU、内存或磁盘。

## 已确认配置

- OS：Ubuntu 22.04.5 LTS。
- CPU：AMD Ryzen 9 8945HX，16 核 32 线程。
- 内存：约 30GiB，可用约 19GiB。
- GPU：NVIDIA GeForce RTX 5060 Laptop GPU。
- 显存：约 8GB。
- 驱动：NVIDIA 580.173.02。
- 系统 CUDA：13.0，`nvcc` 为 13.0.48。
- PyTorch：2.10.0+cu128，CUDA 可用，识别 RTX 5060 Laptop GPU。
- ROS2：Humble，已有 RViz2、rqt、Navigation2、sensor_msgs、tf2_ros、image_transport。
- 磁盘：约 1.2T 可用。

## 能做什么

- Unity 2022.3 LTS 或更高版本的中等规模场景。
- 单车越野场景。
- RGB 相机或低/中分辨率全景相机。
- VLP-16、VLP-32、Mid360 级别 LiDAR 初期仿真。
- ROS2 中 `sensor_msgs/msg/Image` 与 `sensor_msgs/msg/PointCloud2` 输出。
- RViz2、rqt_image_view、rosbag2 级别验证。

## 不建议一开始做什么

- HDRP 大型森林、超高密度草木、实时复杂天气和高精度光照。
- 多相机高分辨率全景 + VLS-128 高频点云 + 在线大模型推理同时运行。
- 直接在本机训练大型 VLM/VLN 模型。
- 直接改 CUDA、PyTorch、驱动或 Conda 环境。

## 推荐初始性能预算

- Unity 渲染管线：Built-in 或 URP。
- 相机：640x480 或 1280x720，10-20Hz 起步。
- 全景相机：先低分辨率，确认链路后再提高。
- LiDAR：5-10Hz 起步，先 VLP-16/Mid360 类配置。
- ROS2 QoS：传感器 topic 优先 SensorDataQoS 思路，减少排队延迟。
- rosbag：先短时间录制，确认磁盘占用和回放可用，再扩大数据采集。

## 资源监控命令

```bash
nvidia-smi
htop
free -h
df -h
ros2 topic hz /sim/camera/image_raw
ros2 topic hz /sim/lidar/points
ros2 topic bw /sim/lidar/points
```

## 风险判断

- 机器足够做仿真输入层原型。
- 显存决定场景复杂度和传感器规模，必须逐步加负载。
- 系统环境已经有成熟 CUDA/PyTorch/ROS2 配置，不应为了 Unity 仿真改全局环境。
