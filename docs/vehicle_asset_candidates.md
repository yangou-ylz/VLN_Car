# 阶段 12 真实小车候选评估

## 当前结论

第一轮真实小车候选选择 Clearpath Husky，采用“视觉 mesh 导入 + 保留现有控制/TF/传感器 rig”的安全路线。Jackal 已下载并记录，但暂不导入 Unity。

这个选择不是最终机器人动力学方案，只是为了贴合师兄要求中的“导入小车模型”，同时不破坏已经稳定的 Unity-ROS2 相机图像和 LiDAR 点云闭环。

重要限制：Husky ROS description 自带 mesh 是工程/仿真低多边形模型，不是游戏级高清车模。如果 Unity Game 视图右上方 Scale 被拖到 `10x`，即使模型正常，也会被像素级放大，看起来像马赛克。手工查看时应把 Game 视图 Scale 调回 `1x` 或 `Fit`。

## 候选对比

| 维度 | Husky | Jackal |
| --- | --- | --- |
| 仓库 | `https://github.com/husky/husky` | `https://github.com/jackal/jackal` |
| 本次分支 | `humble-devel` | `foxy-devel` |
| 本次 commit | `729f8aa45ccd86fa33a05e07ef698c52c451cd9c` | `017b8b581a90873047f7d6fe438bd87513be4a76` |
| 本地缓存 | `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles/husky` | `/home/ubuntu22/VLN/VLN_ASSETS_CACHE/vehicles/jackal` |
| 缓存大小 | 约 `16M` | 约 `4.6M` |
| 许可证 | `husky_description/package.xml` 声明 `BSD` | 根目录 `LICENSE` 为 BSD 3-Clause |
| Mesh 格式 | 关键视觉件有 `.dae` | 主要为 `.stl` |
| Unity 第一轮适配 | 更适合直接视觉导入 | 更适合后续 URDF/转换路线 |
| 机器人语义 | 户外 UGV，体型更像越野平台 | 小型 UGV，适合轻量对照 |
| 当前动作 | 已导入视觉子集并通过回归 | 已下载，暂缓导入 |

## Husky 导入结果

- Unity 导入目录：`Assets/VLN/ExternalAssets/HuskyVisual`。
- 导入 mesh：`base_link.dae`、`top_chassis.dae`、`user_rail.dae`、`bumper.dae`、`wheel.dae`。
- 候选场景：`Assets/VLN/Scenes/VLNOffroadVehicleCandidate.unity`。
- 生成器：`Assets/VLN/Editor/VlnOffroadVehicleCandidateProjectSetup.cs`。
- 验收脚本：`/home/ubuntu22/VLN/scripts/run_offroad_vehicle_candidate_smoke_test.sh`。
- 验收结果：`VLN_OFFROAD_VEHICLE_CANDIDATE_SMOKE_TEST_PASS`。
- 完整回归：`VLN_ASSET_UPGRADE_BASELINE_CHECK_PASS`，run id `vln_asset_baseline_20260814_040515`。
- 视觉修正：已新增 `VehicleCandidate_GameCamera` 近距离展示视角；候选场景的 ROS 图像输出已提高到 `1280x720`，run id `vln_offroad_vehicle_candidate_20260814_095155`。
- 姿态修正：已修复“四脚朝天/露底盘”的视觉姿态错误。当前实现对 Husky mesh 做 ROS -> Unity 坐标转换，并追加 `180°` 绕 Unity X 轴的 upright correction；最终验收 run id `vln_offroad_vehicle_candidate_20260814_101556`，近景截图确认黄色上盖在上、四个轮子竖直贴地。

## 技术边界

- 当前不是完整 URDF Importer 导入，也不是真实轮式动力学。
- 当前 Unity 仍由 `VlnVehicleTfPublisher` 订阅 `/vln/cmd_vel` 并发布 `/tf`。
- 相机和 LiDAR 仍挂在当前稳定 rig 上，frame 保持 `front_camera_optical_frame` 和 `lidar_link`。
- Husky 姿态修正只影响视觉 mesh，不改变 `map -> base_link -> front_camera_optical_frame,lidar_link` 的 TF 语义。
- 后续如要做完整机器人模型，应先评估 Unity URDF Importer、mesh 材质、joint、碰撞体和 wheel controller，不要直接覆盖当前候选场景。

## 下一步建议

1. 用户手工打开 `VLNOffroadVehicleCandidate.unity`，确认 Husky 视觉车体是否满足师兄演示口径。
2. 手工看 Game 视图时先把 Scale 调到 `1x` 或 `Fit`，不要用 `10x` 判断清晰度。
3. 如果只需要阶段成果展示，先保持当前视觉替换，不急着做 URDF 动力学。
4. 如果要提高真实性，下一步应筛选游戏级/仿真级高清 UGV 或越野车资产，而不是继续只依赖 ROS description mesh。
5. 如果要做完整机器人工作流，再单独开阶段评估 Unity URDF Importer，不要和地图资产继续混改。
