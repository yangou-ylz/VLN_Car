# 工作流调研结论

## 综合结论

最适合当前项目的工作流不是“大一统环境”，而是“宿主机稳定 ROS2 + Unity Editor + 独立 ROS2 workspace + 仓库外资料/资产库 + 小步验收”。

这样做的原因：

- 用户已有稳定 ROS2 Humble、CUDA、PyTorch 和大量 ROS 包，不能被新项目污染。
- Unity 是 GUI 软件，传感器仿真和场景编辑更适合跑在宿主机。
- ROS-TCP-Endpoint 可以放在独立 workspace，既能复用系统 ROS2，又不破坏已有 `~/ws_ros2`。
- 后续如果需要 docker，只把 ROS2 endpoint 或算法侧放进 docker；不要一开始把 Unity 也塞进 docker。

## 推荐目录分层

```text
/home/ubuntu22/VLN/                         # 当前项目仓库：只放配置、脚本、轻量文档、必要源码
/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/       # 仓库外资料库：官方文档、网页快照、社区资料
/home/ubuntu22/VLN/unity_ros2_ws/               # 后续独立 ROS2 workspace：ROS-TCP-Endpoint 等
/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/   # 后续 Unity 工程：场景、Assets、ProjectSettings、Packages
/home/ubuntu22/VLN/VLN_ASSETS_CACHE/            # 可选仓库外大资产缓存：模型、地形、贴图、素材包
/home/ubuntu22/VLN/VLN_BAGS/                    # 可选仓库外 rosbag 输出目录
```

## 推荐执行节奏

1. 只读确认环境。
2. 下载并索引官方资料。
3. 创建 Unity 工程前确认 Unity Editor 版本。
4. 单独创建 `unity_ros2_ws`，先只放 ROS-TCP-Endpoint。
5. Unity 先导入 ROS-TCP-Connector，跑发布/订阅 demo。
6. 再导入 UnitySensors，先相机，后 LiDAR。
7. 再建 terrain，最后导小车。
8. 每一步都记录：命令、现象、验收、问题。

## 社区经验转化为本项目规则

- 不追求一次搭完整环境；先让最小链路可见。
- ROS2 大流量数据要关注 QoS、topic 带宽、录包压力和磁盘写入速度。
- Unity 项目必须提前写 `.gitignore`，`Library/`、`Temp/`、`Build/`、`Logs/`、`UserSettings/` 不进 git。
- Unity 的 `Assets/`、`ProjectSettings/`、`Packages/` 通常才是可复现工程的核心；但大型外部模型和素材包需要单独资产管理或 Git LFS 策略。
- ROS2 workspace 的 `build/`、`install/`、`log/` 不进 git。
- 如果 ROS2 topic 看得到但收不到数据，优先排查 QoS 匹配，而不是重装环境。
- 如果 RViz2 看不到点云，优先排查 fixed frame、TF、frame_id，而不是重装 LiDAR 插件。
- 如果 rosbag 丢帧或卡顿，先降频/降分辨率/减少点数/改录制位置，不先改系统。

## 当前项目的完成定义

第一阶段完成不是“Unity 场景很漂亮”，而是：

- ROS2 能看到 Unity 发出的测试 topic。
- `rqt_image_view` 能看到 Unity 相机图像。
- RViz2 能看到 Unity LiDAR 点云。
- topic 频率稳定，带宽可解释。
- frame 命名清晰，TF 不报错。
- rosbag2 能短时间记录并回放。
- 文档记录完整，下一次能复现。
