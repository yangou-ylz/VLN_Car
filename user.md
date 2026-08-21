# Scout wheel-ground 手工运行命令

本文件按你的实际使用习惯写：先打开 Unity 软件，再开终端跑控制/查看脚本。`run_*_smoke_test.sh` 是自动回归验收用的，不是你平时看效果的首选入口。

所有命令默认在 `/home/ubuntu22/VLN` 下执行。

## 0. 如果 Unity 卡住或异常退出，先清理残留锁

```bash
cd /home/ubuntu22/VLN
./scripts/stop_unity_vln_project.sh
```

正常情况不用每次都跑；只有 Unity 卡住、异常退出、或者提示工程被占用时再跑。希望看到 `Library 下未发现残留 lock 文件`。

## 1. 终端 1：打开 Unity 工程

```bash
cd /home/ubuntu22/VLN
./scripts/open_unity_vln_project.sh
```

Unity 打开后，进入场景 `Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`。先确认场景是 Scout 小车、独木桥、斜坡和后段挑战路段。

## 2. 终端 2：启动 ROS-TCP-Endpoint

```bash
cd /home/ubuntu22/VLN
./scripts/start_ros_tcp_endpoint.sh
```

这个终端保持开着，不要关。希望看到 `Starting server on 127.0.0.1:10000`。

## 3. 回 Unity：点击 Play

点击 Unity 顶部 Play。希望 endpoint 终端出现 `Connection from 127.0.0.1`，Unity 里 Scout 小车四轮贴地，传感器跟随车体。

## 4. 终端 3：运行原 13 点自动路线演示

```bash
cd /home/ubuntu22/VLN
./scripts/drive_scout_wheel_ground_route_demo.sh
```

这是手工演示入口，不会自动打开 Unity。希望看到终端持续输出路径点进度，小车沿路线通过独木桥和斜坡，最后出现 `VLN_SCOUT_PHYSICS_ROUTE_MSG_OK`。

## 4A. 可选：在 Unity 菜单里运行路线

Unity 顶部菜单打开：`VLN -> ROS2 手工演示面板`。

当前面板约定：13 点自动路线入口已移除；需要看路线时优先使用 16 点挑战路线，或按终端命令手工运行旧脚本。点击“查看相机图像”后，右侧会出现相机选项栏：

- `rqt`：打开四个 `rqt_image_view`，分别查看 `/vln/front/image_raw`、`/vln/rear/image_raw`、`/vln/left/image_raw`、`/vln/right/image_raw`。
- `全部相机`：在 Unity 内部打开一个四路拼接预览窗口，不弹终端。
- `前相机`、`后相机`、`左相机`、`右相机`：在 Unity 内部打开单路简洁预览窗口；如果 `全部相机` 已打开，单路按钮会暂时禁用，关闭全部相机窗口后恢复。

面板里的推荐顺序也是：`打开 Scout 场景` -> `启动 ROS-TCP-Endpoint` -> 回 Unity 点 `Play` -> `运行 16 点挑战路线` 或查看传感器。

这个面板只是帮你开新终端执行现有脚本，底层仍然是 ROS2 发布 `/vln/cmd_vel`，不是 Unity 内置导航。

从这个 Unity 菜单启动的 endpoint、路线、相机、RViz 和中文控制面板都会被登记。当前为了避免“一打开就被误杀”，已经临时禁用 Unity 退出自动清理；需要关闭后台终端时手动点击面板里的 `关闭 VLN 后台终端`。

菜单弹出的终端如果脚本报错，会保留窗口，不会再 1 秒自动关闭。对应日志在：

```bash
ls -lt /home/ubuntu22/VLN/.runtime/unity_menu/logs | head
```

如果 Unity 已经关了，或者下次启动控制面板遇到 `Address already in use`，就用：

```bash
cd /home/ubuntu22/VLN
./scripts/cleanup_unity_menu_processes.sh --include-known
```

## 5. 终端 3：运行新增后段挑战路线演示

```bash
cd /home/ubuntu22/VLN
./scripts/drive_scout_wheel_ground_challenge_route_demo.sh
```

这是新增草地、青石路、沙地和低矮障碍的手工演示入口，也不会自动打开 Unity。希望小车先通过原来的桥/坡，再继续走到后段挑战区；终端最后应出现 `VLN_SCOUT_PHYSICS_ROUTE_MSG_OK`。

当前阶段 18A 已给青石路和沙地接入 1K PBR 贴图；手工看效果仍然使用这个挑战路线演示脚本，不需要换新命令。

## 6. 终端 4：检查 ROS2 topic

```bash
cd /home/ubuntu22/VLN
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic list -t | grep -E '/vln|/tf'
```

希望看到 `/vln/front/image_raw`、`/vln/front/camera_info`、`/vln/rear/image_raw`、`/vln/rear/camera_info`、`/vln/left/image_raw`、`/vln/left/camera_info`、`/vln/right/image_raw`、`/vln/right/camera_info`、`/vln/lidar/points`、`/vln/cmd_vel`、`/vln/odom`、`/tf`。

## 7. 终端 5：看相机图像

```bash
cd /home/ubuntu22/VLN
./scripts/view_front_image.sh
```

用于打开 rqt 图像窗口。希望能看到 Unity 相机画面；如果没有自动选中 topic，就手动选择 `/vln/front/image_raw`。

现在 Topgear 上装已经有 4 路相机。也可以直接打开指定 topic：

```bash
cd /home/ubuntu22/VLN
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 run rqt_image_view rqt_image_view /vln/front/image_raw
```

把最后一个 topic 换成 `/vln/rear/image_raw`、`/vln/left/image_raw`、`/vln/right/image_raw`，就能分别看后向、左向、右向相机。

## 8. 终端 6：看 LiDAR 点云

```bash
cd /home/ubuntu22/VLN
./scripts/view_vln_vehicle_rviz.sh
```

用于打开 RViz。希望能看到 `/vln/lidar/points` 点云，Fixed Frame 使用 `map`，TF 不报错。

当前 LiDAR 仍沿用原来的 `/vln/lidar/points`，frame 是 `lidar_link`；Topgear 阶段只是把 LiDAR 挂到了上装顶部并补齐 TF，没有改原 RViz 查看入口。

## 9. 可选：启动中文控制面板

```bash
cd /home/ubuntu22/VLN
./scripts/start_vln_control_panel.sh
```

浏览器打开 `http://127.0.0.1:8765/`。目标位置、速度控制、相机视图、雷达点云都从这里进。

## 10. 可选：直接发 `/vln/cmd_vel`

```bash
cd /home/ubuntu22/VLN
ros2env
source /home/ubuntu22/VLN/unity_ros2_ws/install/setup.bash
ros2 topic pub --once /vln/cmd_vel geometry_msgs/msg/Twist "{linear: {x: 0.5}, angular: {z: 0.0}}"
```

用于快速确认控制链路。发完后如果车还在动，用下面命令停车：

```bash
ros2 topic pub --once /vln/cmd_vel geometry_msgs/msg/Twist "{linear: {x: 0.0}, angular: {z: 0.0}}"
```

## 11. 自动回归验收：我排查或改代码后使用

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_route_smoke_test.sh
```

这是 13 点路线自动回归，会自己打开 batch Unity、启动 endpoint、检查图像/点云/odom/路线指标。希望最后看到 `VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS`。

Topgear 传感器专项自动验收入口：

```bash
cd /home/ubuntu22/VLN
./scripts/run_topgear_sensor_suite_smoke_test.sh
```

这是 16 线 LiDAR + 4 路相机 + TF 的专项回归。希望最后看到 `VLN_TOPGEAR_SENSOR_SUITE_SMOKE_TEST_PASS`。

```bash
cd /home/ubuntu22/VLN
./scripts/run_scout_wheel_ground_challenge_route_smoke_test.sh
```

这是 16 点后段挑战路线自动回归。希望最后同时看到 `VLN_SCOUT_WHEEL_GROUND_ROUTE_SMOKE_TEST_PASS` 和 `VLN_SCOUT_WHEEL_GROUND_CHALLENGE_ROUTE_SMOKE_TEST_PASS`。

注意：如果 Unity Editor 已经手工打开，先不要跑这些 `run_*_smoke_test.sh`，因为同一工程不能同时被两个 Unity Editor 实例打开。你手工看效果时，用第 4/5 步的 `drive_*_demo.sh` 和第 7/8 步的相机/RViz 查看入口。

当前约定：如果 13 点链路已经验证成功，本轮不需要再继续跑 16 点挑战自动回归；16 点只在新增障碍、挑战区变化或你明确要求时再跑。

## 当前基线

- Topgear 传感器专项当前通过 run id：`vln_topgear_sensor_suite_20260820_190104`，包含 4 路 Image、4 路 CameraInfo、LiDAR PointCloud2 和 6 条 TF 边。
- 13 点自动路线当前通过 run id：`vln_scout_wheel_ground_route_20260820_190253`。
- 16 点挑战路线保留最近已知通过 run id：`vln_scout_wheel_ground_challenge_route_20260820_172504`；阶段 20 后未继续重复跑，按你的要求交给你手工验证。
- 挑战区当前已归档三段截图：草地、青石路、沙地；自动回归会检查三段截图和视觉细节数量。
- 关键约束：禁止隐藏托底、压平桥/坡、关闭碰撞、跳过卡点或放宽 gate 来掩盖失败。

## 手动速度控制注意

- 控制面板入口：`cd /home/ubuntu22/VLN && ./scripts/start_vln_control_panel.sh`。
- 前提仍然是 Unity 已打开 `VLNOffroadScoutWheelGroundCandidate.unity`、endpoint 已启动、Unity 已点击 Play。
- 速度控制模块支持两种操作：真实键盘按住 `↑/↓/←/→/A/D`，或者用鼠标按住网页里的箭头/A/D 屏幕按钮。
- 如果只是单击一下屏幕按钮，小车只会短促动一下；要连续走，必须按住不松。
- 线速度默认 `0.55m/s`，但可以用输入框或旁边 `+/-` 按钮调高，当前上限 `20.0m/s`，线速度按钮每次变化 `0.50m/s`；角速度默认 `0.42rad/s`，当前上限 `1.00rad/s`，角速度按钮每次变化 `0.05rad/s`。
- 导出记录后，路径旁边有“复制路径”按钮；未导出前复制的是记录目录，导出后复制的是最新 JSON 文件路径。
- 当前手动控制已经修过请求堆积和旧请求晚到问题，但体感仍以用户人工验收为准；如果它明显不如自动路线，不要继续进入阶段 19，先回到阶段 16 修手动控制。

## 阶段 21：高精荒漠主线入口

阶段 21 先做高精荒漠环境，不覆盖旧主场景，不改 Topgear 传感器锁定文件。

只读确认阶段 20 回退基线：

```bash
cd /home/ubuntu22/VLN
./scripts/check_high_precision_desert_phase0_baseline.sh
```

查看阶段 21 工作流：

```bash
cd /home/ubuntu22/VLN
less docs/high_precision_desert_workflow.md
```

查看资产候选和下载预算：

```bash
cd /home/ubuntu22/VLN
less VLN_REFERENCE_LIBRARY/high_precision_desert_research/high_precision_asset_candidates.md
less VLN_REFERENCE_LIBRARY/high_precision_desert_research/download_budget.md
```

查看当前大资产阶段状态面板：

```bash
cd /home/ubuntu22/VLN
./scripts/report_high_precision_large_asset_status.py
```

它会汇总：下载前候选排序、本地是否已有大包、扫描报告、下一步命令。当前没有真实大包时会明确显示 `large_scene_packages/` 为空。

检查 Gate 0 来源/授权/预算是否满足：

```bash
cd /home/ubuntu22/VLN
./scripts/check_high_precision_large_asset_gate0.py
```

它会显示当前 100GB 总预算、已下载体积、候选预留预算、每个候选是否需要账号/授权确认。当前免费技术底座 `Terrain Sample Asset Pack` 会显示为“可优先下载验证”，但仍不能直接导入主工程。

重算下载前候选评分：

```bash
cd /home/ubuntu22/VLN
./scripts/rank_high_precision_large_asset_candidates.py
```

当前执行结论是：用户已取消当前付费 Mojave 路线，改为 Unity 官方免费 `Terrain Sample Asset Pack` + Poly Haven/ambientCG 继续精修自建 `1km²` 高精荒漠沙盒。`Coast & Dunes`、`Pure Nature 2 : Mojave Desert`、`Landscape Ground Pack 3` 等仍保留在候选排序里，但只作为备用调研池；除非用户重新明确选择并确认授权/购买，否则不要把它们作为当前下载目标。

当前判断：先按免费沙盒主线推进，不回到低模拼接，也不为了省事购买不确定的大包。当前重点是把现有 `VLNHighPrecisionDesertSandbox.unity` 做得更大、更真实、更统一、更复杂：地表、岩石、灌木、干河道、碎石带、路线边缘和远景都要自然变化，不能机械重复。

当前规则：正式下载任何大资产前，必须先更新预算表；总下载硬上限 `100GB`。免费官方 Terrain 包或以后重新启用的大型完整荒漠/越野场景包都只能进入副本/沙盒验证，禁止直接覆盖主工程、Topgear 锁定场景或主工程 ProjectSettings。

下载/校验第一批 Poly Haven 高精荒漠小样本资产：

```bash
cd /home/ubuntu22/VLN
./scripts/download_high_precision_desert_sample_assets.py --max-gb 10.0 --proxy http://127.0.0.1:7897/ --timeout 90
```

这一步会走本地代理，下载约 236MB 的 CC0 资产，写入 `VLN_ASSETS_CACHE/high_precision_desert/`，并镜像到 Unity 工程的 `Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven/`。

大资产整包验证入口：

```bash
cd /home/ubuntu22/VLN
less VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_scene_research.md
```

准备大资产副本工程：

```bash
cd /home/ubuntu22/VLN
./scripts/prepare_high_precision_large_asset_sandbox_project.sh
```

当前副本工程路径是：

```bash
/home/ubuntu22/VLN/UnityProjects/VLN_Offroad_LargeAssetSandbox
```

打开大资产副本工程：

```bash
cd /home/ubuntu22/VLN
./scripts/open_unity_large_asset_sandbox_project.sh
```

后续如果重新启用 `Pure Nature 2 : Mojave Desert`、`Coast & Dunes` 这类 Asset Store/Fab 大包，只能导入这个副本工程，不要导入主工程 `UnityProjects/VLN_Offroad`。当前免费沙盒路线一般不需要打开副本工程，除非要验证官方 Terrain Sample 包或新的大包。

如果浏览器或 Unity Asset Store 已把大场景包下载到本地，把原始 `.unitypackage`、`.zip`、`.tar` 或解包目录放到：

```bash
/home/ubuntu22/VLN/VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/
```

然后只读扫描包内容：

```bash
cd /home/ubuntu22/VLN
./scripts/inspect_high_precision_large_asset_package.py \
  VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/<资产包文件或目录> \
  --output VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_inspections/<资产名>_inspection.json
```

这一步只统计场景、模型、贴图、材质、shader、prefab、Terrain、ProjectSettings 和 collider/physics 关键词，不导入 Unity，不改工程。

如果目录里放了多个大包，直接批量扫描：

```bash
cd /home/ubuntu22/VLN
./scripts/scan_high_precision_large_scene_packages.sh
```

当前如果还没下载大包，会看到 `VLN_HIGH_PRECISION_LARGE_ASSET_SCAN_NO_PACKAGES`，这是正常状态。扫描后会生成排序报告：

```bash
/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_inspections/large_asset_ranking.md
```

如果不知道下载到了哪里，先查找本机可疑包：

```bash
cd /home/ubuntu22/VLN
./scripts/find_high_precision_large_scene_packages.sh
```

如果下载目录里杂项很多，只显示 100MB 以上候选：

```bash
cd /home/ubuntu22/VLN
VLN_LARGE_ASSET_MIN_MB=100 ./scripts/find_high_precision_large_scene_packages.sh
```

如果上面找到了目标资产包，把它暂存到 VLN 大资产目录：

```bash
cd /home/ubuntu22/VLN
./scripts/stage_high_precision_large_scene_package.sh '<资产包完整路径>'
```

然后再运行扫描：

```bash
cd /home/ubuntu22/VLN
./scripts/scan_high_precision_large_scene_packages.sh
```

注意：`Poly Desert [FREE]`、`Low-Poly Desert Environment Pack` 这类低模免费包只用于下载/扫描 smoke test，不是高精荒漠主线。2026-08-21 已确认 itch `Poly Desert [FREE]` 命令行只能拿到临时下载页，最终 zip 会重定向回商品页；如果以后确实要用它做 smoke test，请直接在浏览器点击免费下载，再按上面的暂存/扫描流程处理。

构建并截图验证高精荒漠沙盒：

```bash
cd /home/ubuntu22/VLN
./scripts/run_high_precision_desert_sandbox_visual_smoke_test.sh
```

希望看到 `VLN_HIGH_PRECISION_DESERT_SANDBOX_VISUAL_SMOKE_TEST_PASS`。该脚本只做视觉导入和截图，不启动 ROS2，不改旧 Topgear 主场景。

手工查看沙盒：

```bash
cd /home/ubuntu22/VLN
./scripts/open_unity_vln_project.sh
```

在 Unity 里打开 `Assets/VLN/Scenes/VLNHighPrecisionDesertSandbox.unity`。这是阶段 21 的独立高精荒漠沙盒，不是旧 Topgear 主场景。

## 阶段 21 免费沙盒下一步

当前下载预算硬上限是 `100GB`，不是旧的 `1GB` 小样本限制。但本轮不再主动购买或下载 Mojave；当前执行顺序是：继续精修 `VLNHighPrecisionDesertSandbox.unity`，必要时通过 Unity 官方入口获取 `Terrain Sample Asset Pack`，再用 Poly Haven/ambientCG 继续补地表、岩石、灌木、干河道、碎石带、路线边缘和远近景融合。建筑/废土/遗迹/集市类 Fab 包只作为参考，不替代自然越野荒漠主线。

### 查看当前免费沙盒

自动截图验收：

```bash
cd /home/ubuntu22/VLN
./scripts/run_high_precision_desert_sandbox_visual_smoke_test.sh
```

希望看到：`VLN_HIGH_PRECISION_DESERT_SANDBOX_VISUAL_SMOKE_TEST_PASS`。该脚本只做视觉导入和截图，不启动 ROS2，不改旧 Topgear 主场景。

手工打开 Unity 查看：

```bash
cd /home/ubuntu22/VLN
./scripts/open_unity_vln_project.sh
```

在 Unity 里打开：

```text
Assets/VLN/Scenes/VLNHighPrecisionDesertSandbox.unity
```

### 获取官方 Terrain Sample 包时

通过浏览器或 Unity Asset Store / Package Manager 打开 Unity 官方 `Terrain Sample Asset Pack` 页面，用合法 Unity 账号加入/下载。下载完成后不要直接导入主工程；先让我或你在终端运行下面的定位命令。

先看当前状态：

```bash
cd /home/ubuntu22/VLN
./scripts/report_high_precision_large_asset_status.py
./scripts/check_high_precision_large_asset_gate0.py
```

希望看到：预算低于 `100GB`，并且 `large_scene_packages/` 为空或显示你刚下载的官方 Terrain 包。

下载官方包或以后重新启用的大包时，都要走浏览器、Unity Asset Store、Fab 或 Unity Package Manager 的合法账号入口。下载完后，不要直接导入主工程；先让脚本找包：

```bash
cd /home/ubuntu22/VLN
VLN_LARGE_ASSET_MIN_MB=100 ./scripts/find_high_precision_large_scene_packages.sh
```

找到目标 `.unitypackage`、`.zip`、`.tar` 或解包目录后，先暂存：

```bash
cd /home/ubuntu22/VLN
./scripts/stage_high_precision_large_scene_package.sh '<资产包完整路径>'
```

然后只读扫描：

```bash
cd /home/ubuntu22/VLN
./scripts/scan_high_precision_large_scene_packages.sh
./scripts/report_high_precision_large_asset_status.py
```

只有扫描确认 scene、terrain、prefab、材质、LOD、ProjectSettings 和 physics/collider 线索后，才导入副本工程：

```text
/home/ubuntu22/VLN/UnityProjects/VLN_Offroad_LargeAssetSandbox
```

禁止直接导入主工程：

```text
/home/ubuntu22/VLN/UnityProjects/VLN_Offroad
```

导入副本工程后再判断：官方 Terrain 包能提供有价值的 TerrainLayer、细节对象、地形笔刷或 demo 结构，就把可控子集迁移到当前 `1km²` 沙盒；如果只是参考资料，则继续保留为资料库/备用资产，不覆盖主工程。

## Pure Nature 2 Mesa Desert 1.0 手工验收

当前 Mesa 包已经导入大资产副本工程，不在主工程里。你要肉眼验收时运行：

```bash
cd /home/ubuntu22/VLN
./scripts/open_pure_nature_mesa_desert_sandbox.sh
```

希望看到：Unity 打开的是副本工程 `UnityProjects/VLN_Offroad_LargeAssetSandbox`，并自动加载：

```text
Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity
```

当前自动加载验收已经通过：`vln_pure_nature_mesa_desert_20260822_005701`，`terrain_count=1`、`renderer_count=21302`、`collider_count=16535`、`missing_material_slots=0`、`internal_error_materials=0`。如果你肉眼确认场景效果可以，再进入下一步：把 Topgear 小车、四路相机、LiDAR 和 ROS2 控制链路迁移到 Mesa 场景。
