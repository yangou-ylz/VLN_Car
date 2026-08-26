# VLN 常用运行手册

所有命令默认从项目根目录执行：

```bash
cd /home/ubuntu22/VLN
```

## 打开荒漠小车项目
```bash
./scripts/open_high_precision_world_model.sh --scene mesa_topgear
```


## 基本手工流程

手工看效果时按这个顺序：先打开 Unity 场景，再启动 ROS-TCP-Endpoint，再点 Play，最后运行控制或查看脚本。

```bash
./scripts/open_high_precision_world_model.sh --scene mesa_topgear
./scripts/start_ros_tcp_endpoint.sh
```

然后回 Unity 点击顶部 `Play`。之后按需要运行控制、相机、雷达或问题记录脚本。

## 世界模型打开

统一用这个脚本打开不同世界；只推荐 `--scene` 写法。

```bash
./scripts/open_high_precision_world_model.sh --scene mesa_topgear
```

| `--scene` 参数 | 作用 | 示例 |
|---|---|---|
| `mesa_topgear` | 当前主线：Mesa Desert + Topgear 真实物理小车 | `./scripts/open_high_precision_world_model.sh --scene mesa_topgear` |
| `mesa_desert` | 第一套 Mesa Desert 独立世界 | `./scripts/open_high_precision_world_model.sh --scene mesa_desert` |
| `oasis_desert` | 第二套 Oasis Desert 独立世界 | `./scripts/open_high_precision_world_model.sh --scene oasis_desert` |
| `mesa_oasis` | Mesa + Oasis 融合世界 | `./scripts/open_high_precision_world_model.sh --scene mesa_oasis` |
| `meadow_forest` | Meadow Dynamic Nature 湖泊树林/草甸世界 | `./scripts/open_high_precision_world_model.sh --scene meadow_forest` |
| `forest_lake` | ForestLake 湖边村庄/森林湖泊世界 | `./scripts/open_high_precision_world_model.sh --scene forest_lake` |
| `VLNNewWorldCandidate` | 新导入后派生出来的 VLN 世界场景 | `./scripts/open_high_precision_world_model.sh --scene VLNNewWorldCandidate` |
| `Assets/VLN/Scenes/VLNNewWorldCandidate.unity` | 直接用场景路径打开新世界 | `./scripts/open_high_precision_world_model.sh --scene Assets/VLN/Scenes/VLNNewWorldCandidate.unity` |

普通肉眼查看只需要使用表格里的 `--scene` 参数。

以后新导入世界场景时，把派生后的场景保存到 `Assets/VLN/Scenes/`，并使用下面任一命名，Unity 保存面板会自动注册，不需要再手工改白名单：

```text
VLN*WorldCandidate.unity
VLN*RouteCandidate.unity
VLN*TopgearVehicleCandidate.unity
VLNHighPrecisionDesertSandbox.unity
```

`mesa_topgear` 打开后会自动选中并聚焦到小车；如果 Unity 视角没有跳过去，可点菜单 `VLN -> Mesa Desert -> Focus Topgear Vehicle In Scene View`。

## 保存世界模型

在 Unity 里手工拖动、删除或添加世界物体后，用菜单保存当前世界。

```text
VLN -> 更改世界模型 -> 保存本次世界
```

保存后用下面命令确认这次真的写进场景文件：

```bash
./scripts/check_world_model_manual_save_state.sh
```

希望看到 `VLN_WORLD_MODEL_MANUAL_SAVE_CHECK_PASS`。如果没保存过，它会提示没有保存记录，这是正常的。

## Topgear 上装整体微调

用于只调整 Topgear 上装、16 线雷达和四路相机的整体安装位置/角度；不改变底盘、轮子、Rigidbody、WheelCollider 或动力学参数。

先打开当前主线小车场景：

```bash
./scripts/open_high_precision_world_model.sh --scene mesa_topgear
```

在 Unity 里保持 Edit 模式，不要点击 Play。先点菜单把上装和传感器绑定成一个整体：

```text
VLN -> Topgear 上装整体微调 -> 绑定上装和传感器为整体
```

之后点下面这个菜单会选中整体节点，Scene 视图里直接拖动/旋转它即可：

```text
VLN -> Topgear 上装整体微调 -> 选中上装整体
```

需要拖动的对象名是：

```text
VLN_Topgear_UpperAssembly_UserAdjustableRoot
```

调整满意后点击保存：

```text
VLN -> Topgear 上装整体微调 -> 保存当前小车模型
```

保存会同时写入 `config/topgear_upper_assembly_user_locked.json` 和当前 Mesa Topgear 场景文件；以后再用 `./scripts/open_high_precision_world_model.sh --scene mesa_topgear` 打开，会自动应用这份上装整体保存基线。

注意：不要单独拖四个相机或雷达，除非你明确要重新做传感器局部位置；正常只拖 `VLN_Topgear_UpperAssembly_UserAdjustableRoot`。

## Topgear 真实相机数据位姿微调

用于保持四个 RealSense D405 视觉模型不动，只单独移动真正采集 `/vln/front|rear|left|right/image_raw` 的四路鱼眼相机传感器。

先打开当前主线小车场景，并保持 Edit 模式，不要点 Play：

```bash
./scripts/open_high_precision_world_model.sh --scene mesa_topgear
```

先执行一次解耦：

```text
VLN -> Topgear 相机数据位姿微调 -> 解耦视觉模型和真实相机
```

之后用下面菜单选中要调整的真实数据相机，然后在 Scene 视图里拖动/旋转：

```text
VLN -> Topgear 相机数据位姿微调 -> 选中前真实相机
VLN -> Topgear 相机数据位姿微调 -> 选中后真实相机
VLN -> Topgear 相机数据位姿微调 -> 选中左真实相机
VLN -> Topgear 相机数据位姿微调 -> 选中右真实相机
```

这些对象仍然叫：

```text
Topgear_Front_RGBCamera_UnitySensorsROS
Topgear_Rear_RGBCamera_UnitySensorsROS
Topgear_Left_RGBCamera_UnitySensorsROS
Topgear_Right_RGBCamera_UnitySensorsROS
```

调整满意后保存：

```text
VLN -> Topgear 相机数据位姿微调 -> 保存当前四路真实相机位姿
```

保存会写入 `config/topgear_camera_data_pose_user_locked.json`，并真实保存当前 Mesa Topgear 场景。以后再打开 `mesa_topgear`，会自动先恢复上装整体，再恢复这四路真实相机数据位姿。

注意：这个功能只改变真实图像采集点；D405 可见模型会留在原来的安装位置。如果想重新改可见模型位置，用上面的“Topgear 上装整体微调”，不要用这个菜单。

## Mesa Topgear 问题坡录制

用于记录“小车在某个坡、沟、岩壁或障碍处卡住/打滑/浮空/穿模”的连续动态过程。

先打开当前主线场景，启动 endpoint，Unity 点 Play，然后手动驾驶到问题地形附近：

```bash
./scripts/open_high_precision_world_model.sh --scene mesa_topgear
./scripts/start_ros_tcp_endpoint.sh
```

Unity 菜单也可以直接录制，不用另外开脚本：

```text
VLN -> Mesa Desert -> 录制问题轨迹 -> 开始录制 / 停止录制 / 标记问题点
```

Game 视图左上角会显示录制状态：`待命`、`录制中` 或 `已停止`，并显示样本数、标记数和当前记录目录。看到 `录制中` 后再开过问题坡，记录才是有效的。

快捷键仍然保留：

| 按键 | 作用 |
|---|---|
| `F6` | 开始录制当前问题路段 |
| `F7` | 结束录制，写 summary，并截结束图 |
| `F8` | 录制中标记当前问题点并截图，可按多次 |
| `F10` | 只截图 |
| `F9` | 手动写一次 summary |

推荐做法：到问题坡前按 `F6`，完整开过卡住/打滑/碰撞过程，再按 `F7`。记录目录在：

```text
UnityProjects/VLN_Offroad_LargeAssetSandbox/Logs/mesa_issue_records/mesa_issue_*/
```

分析最新一次录制：

```bash
./scripts/analyze_mesa_issue_recording.py
```

报告会判断卡滞窗口、坡度、轮胎接触、滑移、RPM、扭矩、刹车和主要碰撞体。如果提示没有 `samples.csv`，说明这次 Play 里没有按 `F6` 开始录制。

## 本地键盘速度控制

如果浏览器控制面板速度控制卡顿，优先用这个本地窗口。它绕过网页和 HTTP，直接用 ROS2 发布 `/vln/cmd_vel`。

前提仍然是：Unity 已打开 `mesa_topgear`，endpoint 已启动，Unity 已点击 `Play`。

```bash
./scripts/start_mesa_topgear_local_keyboard_control.sh
```

弹出窗口后，先点击一下这个窗口让它获得键盘焦点，再按键控制：

| 按键 | 作用 |
|---|---|
| `↑` / `W` | 前进 |
| `↓` / `S` | 后退 |
| `←` / `A` | 左转，发布正 `angular.z` |
| `→` / `D` | 右转，发布负 `angular.z` |
| `Space` | 立即停车 |
| `Q` | 退出窗口 |

窗口里可以调线速度和角速度。默认发布频率是 `100Hz`，松开按键会自动归零停车。

注意：方向键、`W/A/S/D`、空格和 `Q` 会被窗口优先捕获用于驾驶，不会再调节线速度/角速度控件。调速度请用鼠标拖动滑条，或直接在数值框里输入数字。

## Mesa Topgear 鱼眼相机和传感器频率验收

用于确认四路相机已经切到 UnitySensors 官方 `FisheyeCameraSensor` 真实鱼眼视角，并且相机和 LiDAR 的 ROS2 发布频率达标。这个脚本会自动 batch 打开 Unity，不跑自动导航路线。

```bash
./scripts/run_mesa_topgear_fisheye_sensor_rate_smoke_test.sh
```

希望看到：`VLN_MESA_TOPGEAR_FISHEYE_SENSOR_RATE_SMOKE_TEST_PASS`。

四路预览图会导出到：

```text
UnityProjects/VLN_Offroad_LargeAssetSandbox/Logs/topgear_fisheye_previews/
```

当前目标参数是：四路相机 `Equidistant` 等距鱼眼、`190°` 视角、`640x640`、`20Hz`；CameraInfo 为 `distortion_model=equidistant`、`fx=fy≈192.996px`、`cx=cy=320px`；LiDAR `18Hz`、最大距离 `90m`、每帧 `57600` 点完整一圈。实际验收要求是相机至少 `15Hz`，LiDAR 至少 `15Hz`。

现在四路 ROS 图像由 UnitySensors 官方 `FisheyeCameraSensor.texture0` 经官方 `ImageMsgPublisher` 直接发布，不再使用 `FOV=120° + Lens Distortion` 近似方案，也不再使用自定义重渲染发布器。脚本会同时保存四路 raw 圆形鱼眼图和 `90°` 反校正图，用来检查鱼眼数据是否科学可反校正。

RViz 点云显示配置已调成：`Frame Rate=60`、PointCloud2 `Depth=20`、`Decay Time=1`、点大小 `2px`。这只优化肉眼显示流畅度，真实数据频率仍以 `/vln/lidar/points` 的 ROS2 频率验收为准。

最近一次通过记录：`vln_mesa_topgear_fisheye_sensor_rate_20260826_135154`。实测四路相机约 `19.665/19.831/20.857/19.801Hz`，LiDAR 约 `17.840Hz`；四路 raw 圆形鱼眼和反校正图均通过，结果目录为 `UnityProjects/_SmokeTestLogs/vln_mesa_topgear_fisheye_sensor_rate_20260826_135154/ros2_fisheye_capture/`。

如果 RViz 里看起来还是只有 `5fps`，先不要只看 RViz 面板，直接量真实 topic：

```bash
./scripts/check_vln_lidar_runtime_rate.sh
```

希望看到 `/vln/lidar/points` 的 `average_hz` 大于 `15Hz`。如果这里是 `15Hz+`，说明 RViz 只是显示/视角/负载问题；如果这里也是 `5Hz` 或没有消息，先关闭当前 Unity，重新用 `./scripts/open_high_precision_world_model.sh --scene mesa_topgear` 打开，因为这个入口现在会在打开时强制把 LiDAR 写回 `16Hz / 90m / 57600点每帧`。

## 中文控制面板

用于浏览器里控制目标位置、速度、相机和雷达入口。

```bash
./scripts/start_vln_control_panel.sh
```

浏览器打开：

```text
http://127.0.0.1:8765/
```

使用前仍要保证 Unity 场景已打开、endpoint 已启动、Unity 已点击 Play。速度控制要按住按钮或键盘键，单击只会短促发送一次。如果它仍然卡顿，直接换用上面的本地键盘速度控制。


## 旧 Scout 金标准演示
Unity 打开 `Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity` 后点击 Play，再运行 13 点路线：


## Unity 卡死清理

只有 Unity 卡住、异常退出或提示工程被占用时才用。

```bash
./scripts/stop_unity_vln_project.sh
```

正常手工流程不需要每次运行它。
