# 阶段 12：复杂地图与真实小车选择分析

## 当前判断

下一步优先导入复杂越野地图，不优先替换真实小车。

原因：当前项目已经有可控占位车体、`/vln/cmd_vel`、相机、LiDAR、TF、RViz 和中文控制面板。换地图主要考验视觉/几何环境和传感器输出，通常不会直接破坏 `/vln/cmd_vel`、`base_link`、相机/LiDAR 挂载和 TF；换真实小车会直接触碰车体层级、frame、碰撞体、控制模型、传感器挂点和 URDF/mesh 导入链路，风险更大。

## 本机与项目约束

- 当前 Unity 工程没有发现 URP/HDRP 依赖，仍应按当前轻量渲染路线推进。
- 当前正式工程目录约 `1.9G`，但 `Assets` 源文件只有约 `2.3M`，说明主资产仍很轻。
- 机器磁盘足够，根分区约 `1.2T` 可用；显存约 `8GB` 是主要边界。
- 第一轮不要导入 HDRP-only、超大贴图、超密植被或电影级场景。

## 选择顺序

### 第一步：中等复杂度越野地图候选

目标是把当前程序化地图升级成更像真实越野环境的地图，但保持当前车体、传感器、topic、TF 和控制接口不变。

第一轮地图候选应满足：

- 支持 Unity 2022 LTS 或明确能在 Unity 2022.3 打开。
- 优先 Built-in 或可低风险导入；URP 资产只能先放候选工程或候选场景评估，不直接改主项目管线。
- 体积优先小于 `2GB`，第一轮越小越好。
- 包含 terrain、道路/土路、岩石/坡地/障碍物，最好有 collider。
- 不强依赖复杂第三方脚本。
- 许可证清晰，允许项目学习/科研演示使用。

### 第二步：真实小车 / UGV 候选

地图稳定后再换车。第一轮真实小车优先用 ROS/URDF 生态成熟的 UGV，而不是纯视觉车模。

小车候选优先级：

1. Husky：更像户外/越野 UGV，尺寸和载荷更适合相机 + LiDAR。
2. Jackal：更小、更轻，适合作为第二候选。
3. 普通 Unity/Sketchfab 车模：只适合视觉替换，不适合作为控制/TF 主模型。

小车导入时必须保持以下接口不乱改：

```text
base_link
front_camera_optical_frame
lidar_link
/vln/front/image_raw
/vln/front/camera_info
/vln/lidar/points
/tf
/vln/cmd_vel
```

## 决策矩阵

| 方案 | 对师兄要求匹配 | 当前风险 | 预计收益 | 结论 |
| --- | --- | --- | --- | --- |
| 先导复杂地图，保留当前占位车 | 高：直接提升越野环境真实性 | 中：主要是渲染管线、体积、collider | 高：相机/点云立刻更像真实环境 | 第一选择 |
| 先换 Husky/Jackal，保留当前地图 | 中：小车更真实，但地图仍简陋 | 中高：URDF、mesh、frame、控制会动核心链路 | 中：演示更专业，但感知环境仍弱 | 第二步 |
| 同时导入复杂地图和真实小车 | 高 | 很高：问题面太大，难定位 | 高但不稳定 | 拒绝 |
| 继续只优化程序化地图 | 中 | 低 | 中：可控但不符合“成熟模型导入”口径 | 备用 |

## 第一轮推荐

第一轮做“地图候选导入”，不换车。

具体执行方式：

1. 保留当前主场景 `VLNOffroadTerrainSmokeTest.unity` 作为回归基线。
2. 选 1 个地图候选，先只导入候选场景 `VLNOffroadAssetCandidate.unity`。
3. 把当前可控占位车体、相机、LiDAR、TF 发布器和 `/vln/cmd_vel` 控制逻辑挂到候选地图里。
4. 跑：

```bash
/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh
```

5. 再手工打开中文控制面板，检查相机视图和雷达点云。

## 第一轮执行结果

第一轮已选择 Kenney Nature Kit 2.1 作为安全候选，而不是直接上 Asset Store 大型森林/HDRP 场景。

选择依据：

- 许可证清楚：Creative Commons Zero / CC0，适合学习、科研演示和后续原型验证。
- 体积小：原始 ZIP 约 `11M`，导入 Unity 的 FBX 子集约 `2.8M`，不会明显压垮 RTX 5060 Laptop 的 8GB 显存边界。
- 管线风险低：低多边形 FBX 模型，不要求 URP/HDRP，不改当前 Built-in 风格工程。
- 对主线收益直接：树、岩石、灌木、栅栏、木桥和营地元素让相机画面和 LiDAR 点云更接近越野语义场景。
- 对既有闭环干扰小：保留当前占位车体、相机、LiDAR、`/tf` 和 `/vln/cmd_vel`，只新增候选场景。

执行结果：

- 候选场景：`Assets/VLN/Scenes/VLNOffroadAssetCandidate.unity`。
- 候选场景生成器：`Assets/VLN/Editor/VlnOffroadAssetCandidateProjectSetup.cs`。
- 候选验收脚本：`/home/ubuntu22/VLN/scripts/run_offroad_asset_candidate_smoke_test.sh`。
- 完整回归脚本：`/home/ubuntu22/VLN/scripts/run_asset_upgrade_baseline_check.sh`。
- 最新完整回归：`VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`，run id `vln_asset_baseline_20260814_033514`。

结论：第一轮地图候选已经达到“更复杂越野环境 + 保持 ROS2 感知/控制闭环稳定”的目标。下一步不应继续盲目堆地图资产，应先让用户手工打开候选场景看相机和点云，再进入真实 UGV/URDF 小车评估。

## 当前暂不选的方案

- 不优先导入 HDRP 大型森林/电影场景：当前 8GB 显存和已有 Built-in/轻量管线不适合第一轮承压。
- 不优先直接替换真实小车：会触碰控制、TF 和传感器挂载，容易把已经跑通的主线打乱。
- 不从通用模型站随便找未验证 FBX：许可证、collider、贴图和比例尺风险太高。

## 下一步具体动作

下一步应该先做地图候选筛选：从 Unity 官方/Asset Store 找 2-3 个中等复杂度越野/山地/林地/土路场景，记录许可证、体积、渲染管线、Unity 版本和预览图。筛选完成后只导入 1 个最稳的候选。
