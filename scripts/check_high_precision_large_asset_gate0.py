#!/usr/bin/env python3
"""Check Gate 0 readiness for high-precision desert large assets.

Gate 0 answers: do we know the source, license/account status, expected size,
budget reserve, render-pipeline risk, and next action before downloading or
importing a large environment package?

This script is read-only. It does not download, install, import, or modify
Unity project settings.
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
MATRIX = RESEARCH / "large_asset_candidate_matrix.json"
OUTPUT = RESEARCH / "large_asset_gate0_report.md"
RAW_DOWNLOADS = ROOT / "VLN_ASSETS_CACHE/high_precision_desert/raw_downloads"
LARGE_PACKAGES = RAW_DOWNLOADS / "large_scene_packages"
HARD_LIMIT_GB = 100.0


ROLE_RESERVE_GB = {
    "visual_ceiling_complete_scene": 10.0,
    "natural_desert_complete_scene_low_risk": 5.0,
    "modular_tileable_desert_scene_candidate": 10.0,
    "builtin_compatible_complete_scene_test": 10.0,
    "ruins_complete_scene_candidate": 10.0,
    "legacy_canyon_terrain_reference": 3.0,
    "free_terrain_technical_base": 10.0,
    "mixed_migration_rock_cliff_booster": 5.0,
    "open_source_simulation_reference": 1.0,
}


def dir_size_bytes(path: Path) -> int:
    if not path.exists():
        return 0
    if path.is_file():
        return path.stat().st_size
    total = 0
    for root, _, files in os.walk(path):
        for name in files:
            try:
                total += (Path(root) / name).stat().st_size
            except OSError:
                pass
    return total


def gb(value: int | float) -> float:
    return float(value) / 1024 / 1024 / 1024


def load_candidates(path: Path) -> list[dict[str, Any]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError("candidate matrix must be a JSON list")
    return data


def reserve_gb(candidate: dict[str, Any]) -> float:
    explicit = candidate.get("budget_reserve_gb")
    if explicit is not None:
        return float(explicit)
    estimate = candidate.get("estimated_size_gb")
    if estimate is not None:
        # Leave room for package metadata, decompression and Unity import cache.
        return max(float(estimate) * 1.5, float(estimate) + 1.0)
    return ROLE_RESERVE_GB.get(str(candidate.get("role", "")), 10.0)


def gate0_status(candidate: dict[str, Any]) -> tuple[str, str]:
    license_status = str(candidate.get("license_status", "")).lower()
    pipeline = str(candidate.get("pipeline", "")).lower()
    role = str(candidate.get("role", ""))

    if "unreal" in pipeline:
        return "不进入 Unity 第一验证", "Unreal-only 或管线不匹配"
    if "free" in license_status:
        return "可优先下载验证", "免费/官方/低风险，但仍需进入缓存和副本工程"
    if "verify" in license_status or "paid" in license_status or "fab" in license_status:
        return "待账号/授权确认", "需要用户账号加入/购买并下载后才能 Gate 1 扫描"
    if "apache" in license_status:
        if role == "open_source_simulation_reference":
            return "仅技术参考", "不是高精荒漠视觉整包，不替代大资产路线"
        return "可优先下载验证", "免费/官方/低风险，但仍需进入缓存和副本工程"
    return "信息不足", "需要补授权、账号、包体和下载方式"


def markdown(candidates: list[dict[str, Any]]) -> str:
    raw_gb = gb(dir_size_bytes(RAW_DOWNLOADS))
    large_gb = gb(dir_size_bytes(LARGE_PACKAGES))
    reserve_total = sum(reserve_gb(c) for c in candidates)
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    lines = [
        "# 高精荒漠大资产 Gate 0 检查",
        "",
        f"更新时间：{now}",
        "",
        "## 预算状态",
        "",
        "| 项目 | 数值 |",
        "| --- | ---: |",
        f"| 总下载硬上限 | {HARD_LIMIT_GB:.2f}GB |",
        f"| 当前 raw_downloads 已占用 | {raw_gb:.2f}GB |",
        f"| 当前 large_scene_packages 已占用 | {large_gb:.2f}GB |",
        f"| 候选预留预算合计 | {reserve_total:.2f}GB |",
        "",
        "说明：预留预算是用于候选比较的上限估计，不代表已经下载；真实下载仍以实际包体和 100GB 硬上限为准。",
        "",
        "## 候选 Gate 0 状态",
        "",
        "| 候选 | 来源 | 预留预算 | 授权/账号状态 | Gate 0 状态 | 下一步 |",
        "| --- | --- | ---: | --- | --- | --- |",
    ]

    for c in candidates:
        status, next_step = gate0_status(c)
        lines.append(
            "| {name} | {source} | {reserve:.2f}GB | {license_status} | {status} | {next_step} |".format(
                name=str(c.get("name", "unknown")).replace("|", "/"),
                source=str(c.get("source", "unknown")).replace("|", "/"),
                reserve=reserve_gb(c),
                license_status=str(c.get("license_status", "待补充")).replace("|", "/"),
                status=status,
                next_step=next_step,
            )
        )

    lines.extend(
        [
            "",
            "## 当前结论",
            "",
            "- Gate 0 已能证明：预算硬上限、候选来源、授权风险和导入边界已被记录。",
            "- Gate 0 还不能证明：任何付费/Fab/Asset Store 大包已经可用，因为本机尚无真实大场景包。",
            "- 当前执行目标已改为免费路线：优先通过 Unity 官方入口获取/验证 `Terrain Sample Asset Pack`，并继续用 Poly Haven/ambientCG 精修当前 1km² 高精荒漠沙盒。",
            "- 付费/账号大包只保留为备用候选池；除非用户再次明确选择并确认授权/购买，否则不要把 Mojave、Coast & Dunes 等作为当前下载目标。",
            "- `Terrain Sample Asset Pack` 是免费低风险技术底座，不是完整荒漠整包；最终仍要靠当前沙盒的 Terrain、PBR、岩石、植被、物理代理和路线设计共同完成。",
            "",
            "## 下载后命令",
            "",
            "```bash",
            "cd /home/ubuntu22/VLN",
            "VLN_LARGE_ASSET_MIN_MB=100 ./scripts/find_high_precision_large_scene_packages.sh",
            "./scripts/stage_high_precision_large_scene_package.sh '<资产包完整路径>'",
            "./scripts/scan_high_precision_large_scene_packages.sh",
            "./scripts/report_high_precision_large_asset_status.py",
            "```",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="检查阶段 21 大资产 Gate 0 来源/授权/预算状态")
    parser.add_argument("--matrix", default=str(MATRIX), help="候选矩阵 JSON")
    parser.add_argument("--output", default=str(OUTPUT), help="Markdown 输出路径")
    args = parser.parse_args()

    matrix = Path(args.matrix).expanduser().resolve()
    output = Path(args.output).expanduser().resolve()
    text = markdown(load_candidates(matrix))
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(text, encoding="utf-8")
    print(text)
    print("VLN_HIGH_PRECISION_LARGE_ASSET_GATE0_CHECK_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
