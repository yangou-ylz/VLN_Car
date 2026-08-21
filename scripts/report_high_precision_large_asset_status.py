#!/usr/bin/env python3
"""Generate the current Stage 21 large-asset status dashboard.

The dashboard intentionally combines three separate views:
1. pre-download candidate ranking,
2. local downloaded/staged packages,
3. scanned package reports.

It does not import anything into Unity and does not mutate project assets.
"""

from __future__ import annotations

import argparse
import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any


ROOT = Path("/home/ubuntu22/VLN")
RESEARCH = ROOT / "VLN_REFERENCE_LIBRARY/high_precision_desert_research"
CACHE = ROOT / "VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages"
INSPECTIONS = RESEARCH / "large_asset_inspections"
DEFAULT_OUTPUT = RESEARCH / "large_asset_status_report.md"
CANDIDATE_RANKING = RESEARCH / "large_asset_candidate_ranking.md"
GATE0_REPORT = RESEARCH / "large_asset_gate0_report.md"
ACTIVE_TARGET = RESEARCH / "active_large_asset_target.json"


def file_size_text(path: Path) -> str:
    if path.is_dir():
        total = 0
        for root, _, files in os.walk(path):
            for name in files:
                try:
                    total += (Path(root) / name).stat().st_size
                except OSError:
                    pass
    else:
        total = path.stat().st_size
    gib = total / 1024 / 1024 / 1024
    mib = total / 1024 / 1024
    if gib >= 1:
        return f"{gib:.2f}GB"
    return f"{mib:.2f}MB"


def cache_entries() -> list[Path]:
    if not CACHE.exists():
        return []
    return sorted(p for p in CACHE.iterdir() if p.is_file() or p.is_dir())


def inspection_reports() -> list[dict[str, Any]]:
    if not INSPECTIONS.exists():
        return []
    reports: list[dict[str, Any]] = []
    for path in sorted(INSPECTIONS.glob("*_inspection.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            continue
        data["_report_file"] = path.name
        reports.append(data)
    return sorted(reports, key=lambda x: int(x.get("scene_package_score", 0)), reverse=True)


def top_candidate_lines(limit: int = 20) -> list[str]:
    if not CANDIDATE_RANKING.exists():
        return ["候选评分报告不存在，请先运行 `./scripts/rank_high_precision_large_asset_candidates.py`。"]
    lines = CANDIDATE_RANKING.read_text(encoding="utf-8").splitlines()
    start = None
    for idx, line in enumerate(lines):
        if line.startswith("| 排名 |"):
            start = idx
            break
    if start is None:
        return ["候选评分报告未包含有效排序表。"]
    table_rows = []
    for line in lines[start:]:
        if not line.startswith("| "):
            break
        table_rows.append(line)
    if len(table_rows) < 3:
        return ["候选评分报告未包含有效排序表。"]
    return table_rows[:2] + table_rows[2 : 2 + limit]


def top_candidate_summary(limit: int = 3) -> str:
    if not CANDIDATE_RANKING.exists():
        return "候选评分报告未生成"
    lines = CANDIDATE_RANKING.read_text(encoding="utf-8").splitlines()
    names: list[str] = []
    in_table = False
    for line in lines:
        if line.startswith("| 排名 |"):
            in_table = True
            continue
        if not in_table or line.startswith("| ---"):
            continue
        if not line.startswith("| "):
            if names:
                break
            continue
        parts = [part.strip() for part in line.strip("|").split("|")]
        if len(parts) >= 2 and parts[0].isdigit():
            names.append(f"`{parts[1]}`")
            if len(names) >= limit:
                break
    return " → ".join(names) if names else "候选评分报告未包含有效排序"


def active_target() -> dict[str, Any] | None:
    if not ACTIVE_TARGET.exists():
        return None
    try:
        data = json.loads(ACTIVE_TARGET.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError):
        return None
    return data if isinstance(data, dict) else None


def zh_sentence(value: Any) -> str:
    text = str(value if value is not None else "未记录").strip()
    return text if text.endswith(("。", "！", "？", ".", "!", "?")) else f"{text}。"


def gate0_summary_lines() -> list[str]:
    if not GATE0_REPORT.exists():
        return ["Gate 0 报告不存在，请先运行 `./scripts/check_high_precision_large_asset_gate0.py`。"]
    lines = GATE0_REPORT.read_text(encoding="utf-8").splitlines()
    out: list[str] = []
    capture = False
    for line in lines:
        if line == "## 预算状态":
            capture = True
            continue
        elif line.startswith("## 候选 Gate 0 状态"):
            break
        if capture:
            out.append(line)
    return out or ["Gate 0 报告未包含预算状态。"]


def dashboard() -> str:
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    staged = cache_entries()
    reports = inspection_reports()
    real_package_reports = [r for r in reports if "YOPO-Sim" not in str(r.get("path", ""))]
    target = active_target()

    lines: list[str] = [
        "# 阶段 21 大资产状态面板",
        "",
        f"更新时间：{now}",
        "",
        "## 当前结论",
        "",
    ]
    if staged:
        lines.append(f"- 已发现 `{len(staged)}` 个暂存大资产包/目录，下一步应运行扫描和副本工程导入验证。")
    else:
        lines.append("- 当前 `large_scene_packages/` 为空，还没有可导入验证的真实 Unity/Fab/Asset Store 大场景包。")

    if real_package_reports:
        best = real_package_reports[0]
        lines.append(
            "- 已有真实大包扫描报告；当前最高分为 "
            f"`{Path(str(best.get('path', 'unknown'))).name}`，分数 `{best.get('scene_package_score', 0)}`。"
        )
    else:
        lines.append("- 当前没有真实大包扫描报告；已有 YOPO-Sim 只作为开源技术参考，不替代荒漠视觉大包。")

    target_name = str(target.get("name", "")) if target else ""
    target_gate = str(target.get("gate", "")) if target else ""
    if target and "Pure Nature 2 Mesa Desert" in target_name and "Gate 3" in target_gate:
        validation = target.get("validation_summary", {})
        route_scene = validation.get("route_candidate_scene", "Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity") if isinstance(validation, dict) else "Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity"
        lines.extend(
            [
                "- 当前活动路线已经切换为用户提供的本地 `Pure Nature 2 Mesa Desert 1.0` 完整场景，并已派生 VLN 自有路线候选场景。",
                f"- 当前工作场景：`{route_scene}`；第三方原始 `Mesa_Demo.unity` 保持不覆盖。",
                f"- 下载前候选排序只保留为备用候选池：{top_candidate_summary()}；当前不再按排序去下载其它包。",
                "- 下一步是在该 Mesa 候选场景上做用户肉眼验收，然后接入 Topgear/ROS2/物理路线并建立新的 Mesa 自动路线基线。",
                "",
            ]
        )
    elif target and "Pure Nature 2 Mesa Desert" in target_name:
        lines.extend(
            [
                "- 当前活动路线已切换为用户提供的本地 `Pure Nature 2 Mesa Desert 1.0` 包；它已经进入副本工程视觉加载验收阶段。",
                f"- 下载前候选排序只保留为备用候选池：{top_candidate_summary()}；当前不再按排序去下载其它包。",
                "- 用户肉眼验收 Mesa demo 前，禁止转换主路线、导入主工程或覆盖 Topgear 锁定状态。",
                "",
            ]
        )
    elif target and "Terrain Sample Asset Pack" in target_name:
        lines.extend(
            [
                "- 当前活动路线已切换为免费路线：Unity 官方 `Terrain Sample Asset Pack` 作为 Terrain 技术底座，Poly Haven/ambientCG 作为 CC0/PBR 视觉资产来源。",
                f"- 付费/账号大包排序只保留为备用候选池：{top_candidate_summary()}；当前不再把 Mojave 或 Coast & Dunes 当作执行目标。",
                "- 任何大包下载后仍必须走 Gate 0-5；禁止直接导入主工程或覆盖 Topgear 锁定状态。",
                "",
            ]
        )
    else:
        lines.extend(
            [
                f"- 下载前候选排序当前优先：{top_candidate_summary()}。",
                "- 任何大包下载后仍必须走 Gate 0-5；禁止直接导入主工程或覆盖 Topgear 锁定状态。",
                "",
            ]
        )

    if target:
        forbidden = target.get("forbidden_actions", [])
        if not isinstance(forbidden, list):
            forbidden = []
        lines.extend(
            [
                "## 当前活动目标",
                "",
                f"- 目标资产：`{target.get('name', 'unknown')}`。",
                f"- 来源：{target.get('source', 'unknown')}。",
                f"- 当前 Gate：{target.get('gate', 'unknown')}。",
                f"- 选择原因：{zh_sentence(target.get('reason', '未记录'))}",
                f"- 下一步：{zh_sentence(target.get('next_action', '未记录'))}",
            ]
        )
        if forbidden:
            lines.append(f"- 禁止事项：{'；'.join(str(item) for item in forbidden)}。")
        lines.append("")

    lines.extend(
        [
            "## Gate 0 预算状态",
            "",
        ]
    )
    lines.extend(gate0_summary_lines())

    lines.extend(
        [
            "",
            "## 下载前候选排序",
            "",
        ]
    )
    lines.extend(top_candidate_lines())

    lines.extend(["", "## 本地暂存大包", ""])
    if staged:
        lines.extend(["| 文件/目录 | 体积 |", "| --- | ---: |"])
        for entry in staged:
            lines.append(f"| `{entry.name}` | {file_size_text(entry)} |")
    else:
        lines.append("当前为空：`VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/`。")

    lines.extend(["", "## 已扫描报告", ""])
    if reports:
        lines.extend(
            [
                "| 报告 | 资产 | 分数 | scene | terrain | prefab | pipeline | physics | 备注 |",
                "| --- | --- | ---: | ---: | ---: | ---: | --- | --- | --- |",
            ]
        )
        for r in reports:
            asset = Path(str(r.get("path", "unknown"))).name
            note = "开源技术参考" if "YOPO-Sim" in str(r.get("path", "")) else "真实大包候选"
            lines.append(
                "| `{report}` | `{asset}` | {score} | {scene} | {terrain} | {prefab} | {pipeline} | {physics} | {note} |".format(
                    report=r.get("_report_file", "unknown"),
                    asset=asset,
                    score=r.get("scene_package_score", 0),
                    scene=r.get("scene_count", 0),
                    terrain=r.get("terrain_asset_count", 0),
                    prefab=r.get("prefab_count", 0),
                    pipeline="yes" if r.get("has_pipeline_hint") else "no",
                    physics="yes" if r.get("has_physics_hint") else "no",
                    note=note,
                )
            )
    else:
        lines.append("当前没有 inspection JSON。")

    lines.extend(
        [
            "",
            "## 下一步命令",
            "",
            "下载或 Unity/Fab 导出大包后，先定位：",
            "",
            "```bash",
            "cd /home/ubuntu22/VLN",
            "VLN_LARGE_ASSET_MIN_MB=100 ./scripts/find_high_precision_large_scene_packages.sh",
            "```",
            "",
            "找到目标 `.unitypackage` / `.zip` / `.tar` 后暂存并扫描：",
            "",
            "```bash",
            "./scripts/stage_high_precision_large_scene_package.sh '<资产包完整路径>'",
            "./scripts/scan_high_precision_large_scene_packages.sh",
            "./scripts/check_high_precision_large_asset_gate0.py",
            "```",
            "",
            "扫描分数和结构通过后，只能导入副本工程：",
            "",
            "```text",
            "/home/ubuntu22/VLN/UnityProjects/VLN_Offroad_LargeAssetSandbox",
            "```",
            "",
            "## 不允许事项",
            "",
            "- 不把大包直接导入 `UnityProjects/VLN_Offroad` 主工程。",
            "- 不覆盖 `config/topgear_sensor_pose_user_locked.json`、锁定场景或 13 点金标准路线。",
            "- 不用 Unreal-only 包作为 Unity 第一验证目标。",
            "- 不把低模/free smoke-test 包当作论文展示级主线。",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="生成阶段 21 高精荒漠大资产状态面板")
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT), help="Markdown 输出路径")
    args = parser.parse_args()

    output = Path(args.output).expanduser().resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    text = dashboard()
    output.write_text(text, encoding="utf-8")
    print(text)
    print("VLN_HIGH_PRECISION_LARGE_ASSET_STATUS_REPORT_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
