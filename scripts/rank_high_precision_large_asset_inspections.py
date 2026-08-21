#!/usr/bin/env python3
"""Rank scanned high-precision desert large-asset packages.

Input is one or more JSON files produced by
inspect_high_precision_large_asset_package.py. The output is a concise Markdown
table that helps decide whether a package deserves Unity sandbox import.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def load_report(path: Path) -> dict[str, Any]:
    data = json.loads(path.read_text(encoding="utf-8"))
    data["_report_path"] = str(path)
    return data


def decision(report: dict[str, Any]) -> str:
    score = int(report.get("scene_package_score", 0))
    scene_count = int(report.get("scene_count", 0))
    has_settings = bool(report.get("has_project_settings"))
    has_pipeline = bool(report.get("has_pipeline_hint"))
    has_physics = bool(report.get("has_physics_hint"))

    if scene_count <= 0:
        return "不优先：未发现 Unity scene，先当素材库看"
    if score >= 60 and has_physics:
        return "优先导入副本工程：场景/物理线索较完整"
    if score >= 55:
        return "可导入副本工程：先截图看 demo scene"
    if has_settings or has_pipeline:
        return "谨慎：只进副本工程，先查管线/ProjectSettings"
    return "备选：信息不足，需手工看目录和预览图"


def row(report: dict[str, Any]) -> list[str]:
    name = Path(str(report.get("path", "unknown"))).name
    return [
        name,
        str(report.get("scene_package_score", 0)),
        str(report.get("scene_count", 0)),
        str(report.get("terrain_asset_count", 0)),
        str(report.get("prefab_count", 0)),
        str(report.get("model_count", 0)),
        str(report.get("texture_count", 0)),
        "yes" if report.get("has_pipeline_hint") else "no",
        "yes" if report.get("has_project_settings") else "no",
        "yes" if report.get("has_physics_hint") else "no",
        decision(report),
    ]


def markdown_table(reports: list[dict[str, Any]]) -> str:
    headers = [
        "资产包",
        "分数",
        "scene",
        "terrain",
        "prefab",
        "model",
        "texture",
        "pipeline",
        "project settings",
        "physics",
        "建议",
    ]
    rows = [row(r) for r in sorted(reports, key=lambda x: int(x.get("scene_package_score", 0)), reverse=True)]
    lines = ["| " + " | ".join(headers) + " |", "| " + " | ".join(["---"] * len(headers)) + " |"]
    for values in rows:
        lines.append("| " + " | ".join(v.replace("|", "/") for v in values) + " |")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="汇总大资产扫描 JSON 并生成排序建议")
    parser.add_argument("reports", nargs="*", help="inspection JSON 文件；不填则扫描默认目录")
    parser.add_argument("--output", default=None, help="Markdown 输出路径")
    args = parser.parse_args()

    if args.reports:
        paths = [Path(p).expanduser().resolve() for p in args.reports]
    else:
        default_dir = Path("/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_inspections")
        paths = sorted(default_dir.glob("*_inspection.json"))

    if not paths:
        text = (
            "# 大资产扫描排序\n\n"
            "当前没有 inspection JSON。请先把大资产放入 `VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/large_scene_packages/`，"
            "再运行 `./scripts/scan_high_precision_large_scene_packages.sh`。\n"
        )
    else:
        reports = [load_report(p) for p in paths]
        text = "# 大资产扫描排序\n\n" + markdown_table(reports) + "\n"

    if args.output:
        out = Path(args.output)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(text, encoding="utf-8")
    print(text)
    print("VLN_HIGH_PRECISION_LARGE_ASSET_RANKING_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
