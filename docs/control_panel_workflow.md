# 阶段 11：中文控制面板工作流

## 目标

阶段 11 的目标是给手工调试增加一个极简中文 UI：在一个浏览器窗口里输入相对目标坐标，点击发送后由 ROS2 后端发布 `/vln/cmd_vel`，让 Unity 主场景里的车体实时响应。同时，UI 顶部的“相机视图”和“雷达点云”作为触发按钮，分别打开已有的 rqt 图像窗口和 RViz 点云窗口。

这个阶段不新增系统包、不新增 Python 包、不修改 CUDA/PyTorch/Conda。UI 后端使用 ROS2 已有 Python 环境和 Python 标准库 HTTP server；前端是本地浏览器页面。

## 文件位置

- 控制面板后端与页面：`/home/ubuntu22/VLN/scripts/vln_control_panel.py`
- 启动入口：`/home/ubuntu22/VLN/scripts/start_vln_control_panel.sh`
- 自动验收脚本：`/home/ubuntu22/VLN/scripts/run_control_panel_smoke_test.sh`
- HTTP 验收客户端：`/home/ubuntu22/VLN/scripts/vln_control_panel_smoke_client.py`
- UI 截图：`/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/control_panel_screenshots/vln_control_panel_ui_20260814_025613.png`

## 使用顺序

1. 启动 ROS-TCP endpoint：

```bash
/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh
```

2. 打开 Unity 工程：

```bash
/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh
```

3. 在 Unity 中打开主场景并点击 Play：

```text
Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity
```

4. 启动控制面板：

```bash
/home/ubuntu22/VLN/scripts/start_vln_control_panel.sh
```

默认 URL：

```text
http://127.0.0.1:8765/
```

## UI 操作说明

- “目标位置”是默认模块。
- 相对 X：以当前 `base_link` 为起点，正数表示向车头前方走，负数表示后退。
- 相对 Y：以当前 `base_link` 为起点，正数表示向左，负数表示向右。
- 步进值：控制 `+` / `-` 按钮每次增减多少米。
- “发送目标”：后端把输入坐标转换成当前 `map` 坐标下的目标点，然后持续发布 `/vln/cmd_vel`，直到到达阈值附近。
- “停止小车”：立即停止当前目标控制，并连续发布 0 速度。
- “相机视图”：触发 `/home/ubuntu22/VLN/scripts/view_front_image.sh`，弹出 rqt 图像窗口。
- “雷达点云”：触发 `/home/ubuntu22/VLN/scripts/view_vln_vehicle_rviz.sh`，弹出 RViz 点云窗口。

## 自动验收

关闭正在打开的 Unity Editor 后运行：

```bash
/home/ubuntu22/VLN/scripts/run_control_panel_smoke_test.sh
```

成功标志：

```text
VLN_CONTROL_PANEL_SMOKE_TEST_PASS
```

最近一次通过日志：

```text
/home/ubuntu22/VLN/UnityProjects/_SmokeTestLogs/vln_control_panel_20260814_025329
```

该次结果：

- 控制面板后端启动成功，HTTP 状态接口可访问。
- Unity 主场景发布 `/tf` 后，控制面板识别到 `map -> base_link`。
- HTTP 客户端发送相对目标 `X=1.20m, Y=0.00m`。
- Unity 收到 30 条 `/vln/cmd_vel` 指令。
- 控制面板判断到达目标附近，剩余距离约 `0.27m`。

## 注意事项

- 控制面板不是独立仿真器；endpoint 和 Unity Play 没启动时，它只会显示“等待 TF”。
- 坐标是“相对当前车体”的目标，不是 Unity 世界绝对坐标。
- 相机和点云按钮只是启动已有脚本，不把 rqt/RViz 嵌进网页。
- 如果 8765 端口被占用，可指定端口：`/home/ubuntu22/VLN/scripts/start_vln_control_panel.sh --port 8766`。
- 退出控制面板时会发布几次 0 速度，避免车体继续运动。
