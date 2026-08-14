# 阶段 12：越野地图与小车模型升级工作流

## 目标

当前项目已经满足师兄的最小要求：Unity 越野场景、可控车体、RGB 相机、3D LiDAR、ROS2 topic、TF、RViz 和控制闭环都已跑通。下一步如果要更接近师兄说的“网上找成熟越野模型直接导入”，应该进入资产升级阶段。

本阶段原则：不直接替换主场景，不把未知许可证资产放进 git。先筛选、记录、隔离导入，再用已有自动验收脚本确认没有破坏图像、点云、TF 和控制链路。下载资产前优先使用本地代理；大资产仍需谨慎，第一轮优先选择许可证清楚、体积小、无复杂渲染管线要求的候选。

## 候选来源优先级

### 越野地图 / 环境

优先级从高到低：

1. Unity Asset Store 或 Unity 官方样例环境：优点是 Unity 兼容性更高，导入路径清晰；缺点是可能需要登录、许可证确认和较大下载。
2. Unity Terrain Tools + 轻量地形/植被资产：优点是可控、性能风险低；缺点是视觉真实感需要逐步调。
3. Sketchfab / CGTrader / TurboSquid 等通用 3D 资源站：只作为备选，必须确认格式、许可证、贴图完整性和 Unity 导入成本。

当前机器 RTX 5060 Laptop 约 8GB 显存，不建议第一轮导入 HDRP 超高精度森林、超大贴图、超密植被或电影级环境。

### 小车 / UGV 模型

优先级从高到低：

1. 带 ROS/URDF 的 UGV，例如 Clearpath Husky、Jackal 等：优点是 frame、尺寸、传感器挂载和机器人语义更接近真实机器人工作流。
2. Unity Asset Store 中带轮式车辆模型的低中等复杂度资产：优点是视觉好；缺点是通常没有 ROS frame、URDF 和真实参数。
3. 普通 3D 模型网站车辆模型：只适合视觉替换，控制/TF/碰撞体需要手工补。

## 第一轮候选清单

| 类别 | 候选 | 用途 | 风险 |
| --- | --- | --- | --- |
| 官方工具 | Unity Terrain Tools | 用更成熟的 Terrain 工作流替换当前程序化网格地形 | 需要控制地形尺寸和植被密度 |
| 官方工作流 | Unity URDF Importer / Robotics Hub | 导入真实机器人 URDF，小车 frame 更规范 | 需要处理材质、mesh 路径和坐标系 |
| UGV | Clearpath Husky / Jackal ROS description | 作为真实小车候选，优先保留 `/vln/cmd_vel` 和 `base_link` 语义 | 可能需要筛选 mesh、材质、比例、轮子碰撞体 |
| 地图资产 | Unity Asset Store 越野/森林/山地/荒漠环境 | 提升越野地图真实感 | 大资产、许可证、HDRP/URP 兼容性和显存压力 |
| 通用模型 | Sketchfab 等平台的 off-road terrain / UGV 模型 | 补充视觉候选和图片参考 | 许可证和 Unity 导入质量不稳定 |

## 第一轮已执行候选

已执行候选：Kenney Nature Kit 2.1。

- 来源：`https://kenney.nl/assets/nature-kit`。
- 许可证：Creative Commons Zero / CC0，许可证文件已保存到 `Assets/VLN/ExternalAssets/KenneyNatureKit/Reference/License.txt`。
- 原始 ZIP：`/home/ubuntu22/VLN/VLN_ASSETS_CACHE/kenney_nature-kit.zip`，约 `11M`，SHA256 为 `fa7974a0d342bfe63c38664ba9f8ec1a4aab8ea25f099bdc56870e33588c4d9d`。
- 导入策略：不把完整解压内容全部导入 Unity；只复制 70 个 FBX 子集到 `Assets/VLN/ExternalAssets/KenneyNatureKit`，约 `2.8M`。
- 候选场景：`Assets/VLN/Scenes/VLNOffroadAssetCandidate.unity`。
- 场景生成器：`Assets/VLN/Editor/VlnOffroadAssetCandidateProjectSetup.cs`，自动把树、岩石、灌木、栅栏、木桥、营地等放入当前越野地形，并给主要模型补 `MeshCollider`。
- 运行时验收：`Assets/VLN/Scripts/VlnOffroadAssetCandidateSmokeTest.cs`，仍发布标准相机、CameraInfo、LiDAR、TF，并生成候选场景截图。
- 最新验收：`/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh` 输出 `VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`，run id `vln_asset_baseline_20260814_033514`。

## 第二轮已执行候选

已执行候选：Clearpath Husky 视觉车体子集。

- 来源：`https://github.com/husky/husky`。
- 下载分支：`humble-devel`。
- 下载 commit：`729f8aa45ccd86fa33a05e07ef698c52c451cd9c`。
- 原始缓存：`/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles/husky`，约 `16M`。
- 许可证记录：`husky_description/package.xml` 声明 `BSD`，本地副本保存为 `Assets/VLN/ExternalAssets/HuskyVisual/Reference/husky_description_package.xml`。
- 导入策略：不整仓导入 Unity；只复制 5 个 `.dae` 视觉 mesh 到 `Assets/VLN/ExternalAssets/HuskyVisual`，约 `5.0M`。
- 导入 mesh：`base_link.dae`、`top_chassis.dae`、`user_rail.dae`、`bumper.dae`、`wheel.dae`。
- 候选场景：`Assets/VLN/Scenes/VLNOffroadVehicleCandidate.unity`。
- 场景生成器：`Assets/VLN/Editor/VlnOffroadVehicleCandidateProjectSetup.cs`，在 Kenney 地图候选基础上把程序化占位车体替换为 Husky 视觉车体。
- 运行时验收：`Assets/VLN/Scripts/VlnOffroadVehicleCandidateSmokeTest.cs`，仍发布标准相机、CameraInfo、LiDAR、TF，并生成总览截图和车体近景截图。
- 自动验收：`/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh` 输出 `VLN_OFFROAD_VEHICLE_CANDIDATE_SMOKE_TEST_PASS`，run id `vln_offroad_vehicle_candidate_20260814_040554`。
- 完整回归：`/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh` 输出 `VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`，run id `vln_asset_baseline_20260814_040515`。

Jackal 当前只作为第二候选下载和记录，暂不导入 Unity。原因：Jackal 源仓体积更小，结构清楚，但 `jackal_description/meshes` 主要是 `.stl`，第一轮直接导入 Unity 的材质和视觉效果不如 Husky 的 `.dae`；如果后续需要更小车体或做 URDF Importer 路线，再进入 Jackal 导入。

## 导入前硬性检查

任何候选资产导入前，先记录到：

```text
/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/asset_candidates/index.md
```

必须记录：

- 来源 URL。
- 许可证或使用条款。
- 文件大小。
- Unity 支持版本和渲染管线：Built-in / URP / HDRP。
- 资产内容：地形、植被、车辆、贴图、材质、碰撞体、脚本。
- 是否需要安装额外 Unity package。
- 是否会引入 C# 脚本或编辑器扩展。

## 导入目录约束

- 下载缓存：`/home/ubuntu22/VLN/VLN_ASSETS_CACHE`。
- 原始资料和截图：`/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/asset_candidates`。
- Unity 内候选资产目录：`Assets/VLN/ExternalAssets/<asset_name>`。
- 候选测试场景：`Assets/VLN/Scenes/VLNOffroadAssetCandidate.unity`。
- 不允许直接覆盖当前主场景 `VLNOffroadTerrainSmokeTest.unity`。

`VLN_ASSETS_CACHE`、外部资料库、rosbag 和 Unity 生成缓存都不提交到 git。

## 导入测试流程

1. 选择 1 个地图候选或 1 个小车候选，不同时导入多个大资产。
2. 导入到候选目录，不改主场景。
3. 新建或复制候选场景 `VLNOffroadAssetCandidate.unity`。
4. 保留已有标准接口：

```text
/vln/front/image_raw
/vln/front/camera_info
/vln/lidar/points
/tf
/vln/cmd_vel
map -> base_link -> front_camera_optical_frame,lidar_link
```

5. 跑基础回归：

```bash
/home/ubuntu22/VLN/scripts/run_offroad_asset_candidate_smoke_test.sh
/home/ubuntu22/VLN/scripts/run_standardized_outputs_smoke_test.sh
/home/ubuntu22/VLN/scripts/run_cmd_vel_control_smoke_test.sh
/home/ubuntu22/VLN/scripts/run_control_panel_smoke_test.sh
```

真实小车视觉候选额外运行：

```bash
/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh
```

也可以直接运行一键基线检查：

```bash
/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh
```

成功标志：

```text
VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS
```

6. 手工看效果：

```bash
/home/ubuntu22/VLN/scripts/start_ros_tcp_endpoint.sh
/home/ubuntu22/VLN/scripts/open_unity_vln_project.sh
/home/ubuntu22/VLN/scripts/start_vln_control_panel.sh
```

7. 用 UI 打开相机视图和雷达点云，确认图像、点云和控制都还正常。

## 拒绝条件

出现以下任一情况，第一轮不要继续使用该资产：

- 许可证不清楚或不允许项目使用。
- HDRP-only，且导入会要求大范围切换渲染管线。
- 导入后 Unity 编译报错，需要额外未知系统依赖。
- 显存占用明显超过当前 8GB 边界，或 Unity Editor 明显卡顿不可控。
- 破坏 `/vln/front/*`、`/vln/lidar/points`、`/tf` 或 `/vln/cmd_vel`。
- 车体在无 `/vln/cmd_vel` 时又开始自动运动。
- RViz 无法显示点云，或点云有效点数明显异常。

## 推荐下一步

下一步不要同时做地图和小车。推荐顺序：

1. 先选一个轻量成熟越野地图资产，导入候选场景，保持当前占位车体和传感器不变。
2. 地图通过标准输出和控制面板回归后，再选一个 UGV/URDF 小车模型替换占位车体。
3. 小车替换时优先保持 `base_link`、`lidar_link`、`front_camera_optical_frame` 和 `/vln/cmd_vel` 不变。
