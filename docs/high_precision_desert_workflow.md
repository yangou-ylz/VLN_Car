# 高精度荒漠环境主线工作流

本文件是阶段 21 的执行工作流。目标是在不破坏阶段 20 Topgear 完美基线的前提下，逐步建立高精度荒漠环境视觉渲染 + 小车真实物理交互 + ROS2 感知/控制闭环。

## 当前定位

- 阶段 20 已作为新主线回退基线冻结：Topgear 小车、四路相机、16 线 LiDAR、ROS2 topic/TF、手动控制和现有 13 点金标准路线均保持可恢复。
- 阶段 21 不在旧低模挑战区继续小修小补，而是以用户已验收的 `Pure Nature 2 Mesa Desert 1.0` 完整场景为新底座，再逐步接回 Topgear 小车和 ROS2 链路。
- 第一轮不切主工程渲染管线，不覆盖 `Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity`，不改 `config/topgear_sensor_pose_user_locked.json`，不改 `config/topgear_sensor_hierarchy_user_locked.json`，不改 `config/topgear_sensor_scene_locked/`。
- 用户已把资产预算上限提高到 `100GB`；当前执行目标已从免费自建沙盒切换为本地 `Pure Nature 2 Mesa Desert 1.0` 完整大资产路线。旧 `1km²` 自建高精荒漠沙盒保留为回退和补充资产试验场，不再作为默认主线。

## 目录规则

| 层级 | 路径 | 作用 | git 策略 |
| --- | --- | --- | --- |
| 调研资料 | `VLN_REFERENCE_LIBRARY/high_precision_desert_research/` | 许可证、候选清单、预算表、网页摘要 | 已忽略，不提交 |
| 原始资产缓存 | `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/` | 原始 zip、glTF、FBX、贴图包 | 已忽略，不提交 |
| 筛选子集 | `VLN_ASSETS_CACHE/high_precision_desert/selected_unity_subset/` | 2K/4K、小模型、LOD 子集 | 已忽略，不提交 |
| 导入暂存 | `VLN_ASSETS_CACHE/high_precision_desert/import_staging/` | 解包和轴向/缩放检查 | 已忽略，不提交 |
| Unity 沙盒场景 | `Assets/VLN/Scenes/VLNHighPrecisionDesertSandbox.unity` | 第一版自建高精荒漠 MVP / 回退试验场 | 后续按需维护 |
| Mesa 路线候选场景 | `Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity` | 阶段 21 当前主工作场景，由 `Mesa_Demo.unity` 派生 | 保存在大资产副本工程，不提交大包内容 |
| 大资产副本工程 | `UnityProjects/VLN_Offroad_LargeAssetSandbox/` | 只用于导入/验证 Asset Store/Fab 大场景包和 URP/HDRP | 已被 `UnityProjects/*` 默认忽略，不提交 |

## 阶段 0：冻结当前完美基线

### 禁止改动

- 禁止改动 Topgear 上装根姿态、贴合高度和传感器外观模型。
- 禁止改动 `config/topgear_sensor_pose_user_locked.json`、`config/topgear_sensor_hierarchy_user_locked.json`、`config/topgear_sensor_scene_locked/`。
- 禁止在高精荒漠阶段跑会重建主场景的旧入口。

### 只读验收

```bash
cd /home/ubuntu22/VLN
./scripts/check_high_precision_desert_phase0_baseline.sh
```

成功标志：`VLN_HIGH_PRECISION_DESERT_PHASE0_BASELINE_OK`。

## 阶段 1：资产与工作流调研清单

### 候选类别

- 荒漠地形 / 悬崖岩壁。
- 土路 / 砂石路 PBR 材质。
- 岩石 / 灰岩 / 石山模型。
- 干旱植被 / 灌木 / 枯树。
- HDRI / 天空光照。

### 评分字段

每个候选必须记录：授权清晰度、Unity 可导入性、视觉质量、体积估计、LOD 状态、collision/proxy 状态、是否需要账号、是否可能破坏工程。

### 下载规则

- 大资产下载前必须记录元数据、预览链接、授权/账号状态、预算和导入风险；真实包体下载后先只读扫描，再进副本工程。
- 单个完整场景包只要低于 `100GB` 可纳入验证；超过 `100GB` 必须暂停。
- 自建补充资产默认 2K/4K，关键近景可升 8K；完整场景包按原包结构评估，不再人为压到 1GB 内。

### 大资产整包评估规则

- 优先找带完整 demo scene / Terrain / lighting / PBR 材质 / 岩石植被 / LOD / collision proxy 的荒漠或越野环境包。
- 大包先进入 `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/` 或副本工程验证；禁止直接覆盖主工程场景和 ProjectSettings。
- 评估维度：授权清晰度、Unity 2022.3 兼容性、渲染管线、是否需要账号、包体、demo scene 完整度、能否迁移到 ROS2/Topgear 链路、是否有可用物理代理。
- 如果整包明显优于当前自建沙盒且风险可控，就优先导入整包；否则继续在当前 1km² 自建沙盒上精修。

当前执行目标：`Pure Nature 2 Mesa Desert 1.0 route candidate`。用户已经确认完整 Mesa 场景可用，当前从第三方 `Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity` 派生 VLN 自有工作场景 `Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity`，并在该场景上增加不规则大石头、碎石、小树/仙人掌、草堆/灌木作为第一轮障碍丰富化。下载前排序表里的 `Coast & Dunes`、`Pure Nature 2 : Mojave Desert`、`Landscape Ground Pack 3` 等只保留为备用候选池和历史调研；后续若重新启用其它候选，仍必须先在副本/沙盒中打开 demo scene 或扫描包内容，禁止直接污染主工程。

阶段 21 当前执行策略固定为“完整 Mesa 主线 + 自建沙盒回退 + 大包备用池”：

| 路线 | 触发条件 | 下一步 |
| --- | --- | --- |
| Mesa 整包路线 | 当前默认路线 | 在 `VLN_Offroad_LargeAssetSandbox` 的 `VLNMesaDesertRouteCandidate.unity` 中继续丰富场景、接回 Topgear/ROS2 并建立新路线基线 |
| 免费沙盒回退 | Mesa 路线出现不可接受风险或需要低成本试验 | 用官方 Terrain Sample、Poly Haven、ambientCG 和已有沙盒生成器继续维护 `1km²` 高精荒漠场景 |
| 其它整包路线 | 用户以后重新选择其它完整 demo scene，且授权/购买/下载明确 | 只在 `VLN_Offroad_LargeAssetSandbox` 或新副本中验证，不能覆盖当前 Mesa 基线 |
| 混合迁移 | 大包视觉资产强，但原 demo 路线、物理或管线不适合直接跑车 | 只迁移 TerrainLayer、岩石、植被、材质、天空和远景到当前 `1km²` 沙盒或新沙盒 |

Fab/Unity 建筑、废土、遗迹、集市、工业前哨类完整场景可以作为大场景组织方式和模块化资产参考，但不替代自然荒漠越野主线。地形工具类资产可以在后续增强大场景生产效率，但不是第一轮完整场景下载目标。当前主工程是 Unity 2022.3.62f1；如果后续评估 Terrain Tools，应优先按 Unity 2022.3 对应的 5.0.x 版本，不要把 Unity 2023.1+ 的 Terrain Tools 5.1 当作当前工程安装目标。

大资产副本工程准备入口：

```bash
cd /home/ubuntu22/VLN
./scripts/prepare_high_precision_large_asset_sandbox_project.sh
```

如果副本已经存在，脚本会输出 `VLN_HIGH_PRECISION_LARGE_ASSET_SANDBOX_EXISTS`；如需从主工程增量刷新脚本/配置，运行 `./scripts/prepare_high_precision_large_asset_sandbox_project.sh --refresh`。

只读扫描入口：

```bash
cd /home/ubuntu22/VLN
./scripts/inspect_high_precision_large_asset_package.py <资产包文件或目录> \
  --output VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_inspections/<资产名>_inspection.json
```

批量扫描入口：

```bash
cd /home/ubuntu22/VLN
./scripts/scan_high_precision_large_scene_packages.sh
```

当前没有大包时会输出 `VLN_HIGH_PRECISION_LARGE_ASSET_SCAN_NO_PACKAGES`，不视为失败。
扫描后会生成排序报告：`VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_inspections/large_asset_ranking.md`。

如果不确定浏览器或 Unity 把包下载到了哪里，先查找本机可疑压缩包：

```bash
cd /home/ubuntu22/VLN
./scripts/find_high_precision_large_scene_packages.sh
```

发现目标包后复制到大资产缓存目录：

```bash
cd /home/ubuntu22/VLN
./scripts/stage_high_precision_large_scene_package.sh '<资产包完整路径>'
```

该脚本只复制文件到 `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/`，不会导入 Unity。

单独重排已有扫描 JSON：

```bash
cd /home/ubuntu22/VLN
./scripts/rank_high_precision_large_asset_inspections.py \
  --output VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_inspections/large_asset_ranking.md
```

输出文件：`VLN_REFERENCE_LIBRARY/high_precision_desert_research/high_precision_asset_candidates.md`。

大资产进入 Unity 前必须执行判定协议：`VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_validation_protocol.md`。下载、扫描、副本导入、截图、迁移判断和 ROS2 回归分别对应 Gate 0 到 Gate 5，禁止跳过 Gate 0/1 直接导入主工程。

### 当前 Mesa 路线候选验证入口

构建并截图验证 Mesa 路线候选场景：

```bash
cd /home/ubuntu22/VLN
./scripts/run_pure_nature_mesa_desert_route_candidate_smoke_test.sh
```

希望看到 `VLN_PURE_NATURE_MESA_DESERT_ROUTE_CANDIDATE_SMOKE_TEST_PASS`。该脚本只在大资产副本工程中派生/刷新 VLN 自有场景、增加障碍、截图，不启动 ROS2，不改旧 Topgear 主场景。

手工查看 Mesa 路线候选场景：

```bash
cd /home/ubuntu22/VLN
./scripts/open_pure_nature_mesa_desert_route_candidate.sh
```

兼容旧入口：

```bash
cd /home/ubuntu22/VLN
./scripts/open_pure_nature_mesa_desert_sandbox.sh
```

两个入口都会打开大资产副本工程中的 `Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity`。第三方原始 `Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity` 不再作为默认工作场景，也不要直接覆盖它。

### 免费沙盒回退验证入口

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

## 阶段 2：渲染管线沙盒验证

- 第一轮保留 Unity 2022.3.62f1 和 Built-in 管线，只在独立沙盒场景导入少量 PBR/HDRI/岩石/植被资产。
- URP 只在副本或分支式沙盒中评估，禁止直接转换主工程。
- HDRP 只作为后续调研候选，不作为第一轮默认路线。
- 验收需要同机位 Built-in/URP 对比截图、材质丢失数量、相机输出状态、LiDAR 输出状态和基础 FPS/帧时间记录。

## 阶段 3：荒漠场景 MVP

- 场景规模不得再按室内/小院级别设计；当前沙盒已推进为 `1000m x 1000m = 1,000,000㎡`，后续默认按 1km² 室外场景继续精修。
- 仍然先做固定室外荒漠演示路线，不直接做巨大开放世界；高精视觉优先集中在车辆路线附近，远景用岩壁、HDRI、低模山体和植被层增强尺度感。
- 使用 Terrain/高度图做地貌基底，外部 PBR/模型做主体视觉。
- 程序化几何只允许用于地形基底、碰撞代理和调试标记，不作为论文展示级主体视觉。
- 固定 6 个截图机位：车头、斜坡、岩石峡谷、植被遮挡、俯视布局、LiDAR 点云覆盖。

## 阶段 4：物理代理与材质一致性

- Terrain 主地面使用 TerrainCollider。
- 大岩石、树干、崖壁、路沿使用简化 MeshCollider 或组合 Box/Sphere/Capsule Collider。
- 土路、沙地、碎石、岩坡分别绑定 Physic Material 或控制器接触分类。
- 草、沙、尘交互使用触发区、轮胎接触点、shader/VFX 和少量代理 collider 近似；禁止给每片草叶/每粒沙子加 Rigidbody。

## 阶段 5：接入 Topgear 小车与传感器

- 从阶段 20 锁定基线接入 Topgear 小车，不重算传感器位姿，不改四路相机和 LiDAR 外观模型。
- topic/frame 保持兼容：`/vln/front/*`、`/vln/rear/*`、`/vln/left/*`、`/vln/right/*`、`/vln/lidar/points`、`/vln/cmd_vel`、`/vln/odom`。
- 第一版沿用 640x480、5Hz、VLP-16 7200 点/帧；稳定后再提高画质。

## 阶段 6：高精荒漠自动演示路线

- 新建荒漠路线，不复用旧 13 点路线本体。
- 控制策略沿用 ROS2 外部闭环脚本：读取 TF/odom，持续发布 `/vln/cmd_vel`，底层仍由 Unity WheelCollider/物理系统响应。
- 不允许跳点、隐形平路、关闭碰撞、压平坡面换通过。

## 阶段 7：性能与论文展示质量优化

- 启用 LODGroup、GPU Instancing、静态合批/实例化、遮挡/视锥剔除、纹理压缩和 mipmap。
- 高质量细节集中在小车路线附近；远景用低模 cliff、HDRI、雾效/天空盒营造空间感。
- 输出固定截图、四路相机样张、点云截图、路线轨迹、FPS/帧时间和资产体积清单。

## 完成定义

阶段 21 完成时必须同时满足：用户肉眼确认场景不再像几何体拼接；车轮与可见路面接触一致；四路相机、LiDAR、TF、odom、cmd_vel 均保持稳定；自动荒漠路线无卡死、无穿模、无隐藏托底；资产来源和许可可追溯。
