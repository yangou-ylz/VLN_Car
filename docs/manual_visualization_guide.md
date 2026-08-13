# 手工打开场景与查看 ROS2 数据指南

本页回答一个核心问题：现在不是只能看自动测试日志，你已经可以自己打开 Unity 场景，并用 ROS2 图形工具查看图像和点云。

## 当前能看到什么

- Unity 里能看到三个测试场景：通信测试场景、相机测试场景、LiDAR 点云测试场景。
- `UnitySensorsImageSmokeTest.unity` 中能看到简单地面和彩色目标，相机会发布 `/vln/front/image_raw`。
- `UnitySensorsLidarSmokeTest.unity` 中能看到地面、墙、坡、障碍物和一个 VLP-16 占位 LiDAR，LiDAR 会发布 `/vln/lidar/points`。
- 现在还不是最终越野环境，也还没有小车；下一阶段才把这两个传感器闭环迁移到极简越野 terrain。

## 打开 Unity 工程

推荐入口：

```bash
/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh
```

如果 Unity 卡死、关不掉，另开终端运行：

```bash
/home/ubuntu22/VLN/scripts/stop_unity_vln_project.sh
```

这个脚本只处理当前 VLN Unity 工程相关进程，并把残留 lock 文件移动到 `_ManualRecoveryLogs` 保留现场，不删除 `Library/`。

打开后，在 Unity 的 Project 面板中找：

- 相机测试：`Assets/VLN/Scenes/UnitySensorsImageSmokeTest.unity`
- 点云测试：`Assets/VLN/Scenes/UnitySensorsLidarSmokeTest.unity`

双击场景后点击 Unity 顶部 Play 按钮，就能看到当前测试场景。注意：如果你要让 ROS2 收到 topic，需要先启动 endpoint。

## Unity 里怎么点

按照你截图里的界面，从左到右大概是这些区域：

- 左侧 `Hierarchy`：当前场景里有哪些物体。你截图里已经是 `UnitySensorsLidarSmokeTest` 场景，说明 LiDAR 场景已经打开了。
- 中间上方 `Scene` / `Game`：`Scene` 是编辑器自由视角，`Game` 是相机看到的画面。
- 下方 `Project`：工程文件浏览器。你截图里下方已经看到 `Assets`，里面有 `Resources` 和 `VLN` 两个文件夹。
- 右侧 `Inspector`：选中物体后的参数面板。

从 Project 面板打开场景的具体路径：

1. 在底部 `Project` 面板里双击 `VLN` 文件夹。
2. 双击 `Scenes` 文件夹。
3. 双击 `UnitySensorsImageSmokeTest` 或 `UnitySensorsLidarSmokeTest` 场景文件。
4. 左侧 `Hierarchy` 顶部应显示对应场景名。

如果找不到文件夹，也可以在 Project 面板上方搜索框输入 `UnitySensors`，会筛出两个场景。

运行场景的具体动作：

1. 先在终端启动 endpoint：`/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh`。
2. 回到 Unity，点击顶部中间的三角形 Play 按钮。
3. Play 按钮正常会变成蓝色或高亮；这表示 Unity 进入运行态。
4. 只有进入 Play 后，UnitySensors 才会真正往 ROS2 发 `/vln/front/image_raw` 或 `/vln/lidar/points`。
5. 如果只是打开场景但没点 Play，ROS2 里通常只能看到 `/rosout` 和 `/parameter_events`，不会有 `/vln/...` topic。

看画面的区别：

- 想看 Unity 里摆了什么东西：用 `Scene` 标签页。
- 想看相机渲染出来的画面：切到 `Game` 标签页，或者用 ROS2 的图像工具看 `/vln/front/image_raw`。
- 想看 LiDAR 生成的点云：Unity 里主要看到场景几何体，真正点云在 RViz2 里看 `/vln/lidar/points`。

## 启动 ROS2 Endpoint

另开一个终端：

```bash
/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh
```

保持这个终端不要关闭。它负责 Unity 和 ROS2 之间的 TCP bridge。

如果不确定 endpoint 和 Unity topic 是否真的起来了，另开终端执行：

```bash
/home/ubuntu22/VLN/scripts/check_manual_visualization_state.sh
```

如果输出里没有 `/vln/front` 或 `/vln/lidar` topic，说明 Unity 还没有真正进入 Play，或 endpoint 没有启动。

## 查看图像

前置：endpoint 已启动，Unity 打开 `UnitySensorsImageSmokeTest.unity` 并点击 Play。

另开终端：

```bash
/home/ubuntu22/VLN/scripts/view_front_image.sh
```

说明：当前机器有 `rqt_image_view` 这个 ROS2 包，但没有独立的 `rqt_image_view` shell 命令；脚本内部会用 `ros2 run rqt_image_view rqt_image_view /vln/front/image_raw` 打开。如果窗口为空，在 rqt_image_view 顶部下拉框选择 `/vln/front/image_raw`。

如果 Unity 的 `Game` 面板显示 `No cameras rendering`，但 rqt 能看到 `/vln/front/image_raw`，说明 ROS 图像链路是通的，只是 Unity Game 视图缺普通展示相机。当前工程已经在场景构建器里补了 `ImageSmokeTest_ViewerCamera`；如果你打开的是旧场景，需要先关闭 Unity，再运行：

```bash
/home/ubuntu22/VLN/scripts/rebuild_unity_smoke_scenes.sh
```

不要同时运行两个 Unity smoke test 脚本；Unity 不允许同一个工程被两个 Editor 实例并行打开。需要回归时，先跑图像，再跑点云，或者反过来顺序执行。

重建完成后重新打开 Unity，再打开 `UnitySensorsImageSmokeTest.unity`，Game 面板就应该能看到普通 Unity 相机视角。注意：rqt 看到的是 UnitySensors 传感器输出，Game 面板看到的是 Viewer Camera 输出，两者用途不同。

命令行确认：

```bash
ros2 topic list -t | grep /vln/front
ros2 topic hz /vln/front/image_raw
```

## 查看点云

前置：endpoint 已启动，Unity 打开 `UnitySensorsLidarSmokeTest.unity` 并点击 Play。

另开终端：

```bash
/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh
```

该脚本会临时发布 `map -> lidar_link` 静态 TF，并用 `/home/ubuntu22/VLN/config/vln_lidar_pointcloud.rviz` 打开固定配置。手工设置时应为：

- 左侧 Global Options 的 `Fixed Frame` 填 `map`。
- 点击 Add，选择 `PointCloud2`。
- 在 PointCloud2 的 Topic 里选择 `/vln/lidar/points`。

不要沿用旧 RViz 默认配置里的 `laser_frame`、`LaserScan`、`RobotModel` 或 `/odom`。当前 UnitySensors 输出的是 `PointCloud2`，不是 `LaserScan`，frame 是 `lidar_link`，不是 `laser_frame`。

你截图里的 RViz 状态具体问题是：

- `Fixed Frame` 写成了 `laser_frame`，当前脚本会使用 `map`，并临时发布 `map -> lidar_link`。
- 左侧显示项有 `LaserScan`，但当前没有 `/scan`，也没有 `sensor_msgs/msg/LaserScan`。
- 当前应该添加 `PointCloud2`，topic 选 `/vln/lidar/points`。
- 如果 `/vln/lidar/points` 下拉框里没有这个 topic，说明 endpoint 没开、Unity 没点 Play，或者 Unity 还没连上 endpoint。
- 如果 `PointCloud2` 状态是 OK 但 Global Status 报 `Frame [lidar_link] does not exist`，说明缺 TF；重新用 `/home/ubuntu22/VLN/scripts/view_lidar_rviz.sh` 打开，它会自动补临时 TF。

如果只看到网格，先执行：

```bash
/home/ubuntu22/VLN/scripts/check_manual_visualization_state.sh
```

只有看到 `/vln/lidar/points [sensor_msgs/msg/PointCloud2]` 后，RViz 才有数据可显示。

命令行确认：

```bash
ros2 topic list -t | grep /vln/lidar
ros2 topic hz /vln/lidar/points
ros2 topic bw /vln/lidar/points
```

## 为什么现在 Fixed Frame 用 `map`

当前只是传感器最小闭环，LiDAR 还没有挂到小车 `base_link` 下，也没有正式 `map -> odom -> base_link -> lidar_link` 的 TF 树。为了先看点云，`view_lidar_rviz.sh` 会临时发布一个身份变换 `map -> lidar_link`。后续导入小车后，再把 TF 标准化，并删除这个临时可视化补丁。

## 自动验收和手工查看的区别

- 自动验收脚本用于快速判断链路是否真的通了，适合排错和回归。
- 手工 Unity + rqt/RViz 用于你自己看场景、图像和点云效果。
- 以后每次我们改场景或传感器配置，都应该先跑自动验收，再手工可视化确认效果。
