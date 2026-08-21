#!/usr/bin/env python3
"""Rank high-precision desert asset candidates before download.

This is not a replacement for package inspection. It ranks online candidates
from the local research matrix so we can decide what to acquire first, then
actual downloaded packages still go through the Gate 0-5 validation flow.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


DEFAULT_MATRIX = Path(
    "/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_candidate_matrix.json"
)
DEFAULT_OUTPUT = Path(
    "/home/ubuntu22/VLN/VLN_REFERENCE_LIBRARY/high_precision_desert_research/large_asset_candidate_ranking.md"
)

WEIGHTS = {
    "visual_realism": 0.22,
    "natural_offroad_fit": 0.20,
    "complete_demo_scene": 0.16,
    "unity_2022_fit": 0.12,
    "builtin_low_risk": 0.10,
    "physics_migration_fit": 0.12,
    "download_access_fit": 0.08,
}


def load_candidates(path: Path) -> list[dict[str, Any]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError("candidate matrix must be a JSON list")
    return data


def weighted_score(candidate: dict[str, Any]) -> float:
    total = 0.0
    for key, weight in WEIGHTS.items():
        total += float(candidate.get(key, 0)) * weight
    return round(total, 2)


def recommendation(candidate: dict[str, Any]) -> str:
    role = str(candidate.get("role", ""))
    score = weighted_score(candidate)
    builtin = int(candidate.get("builtin_low_risk", 0))
    complete = int(candidate.get("complete_demo_scene", 0))
    natural = int(candidate.get("natural_offroad_fit", 0))

    if role == "visual_ceiling_complete_scene":
        return "第一优先：视觉上限验证，必须副本 URP/HDRP"
    if role == "natural_desert_complete_scene_low_risk":
        return "第一梯队：自然荒漠低风险整包"
    if complete >= 8 and builtin >= 8 and score >= 6.5:
        return "第二梯队：Built-in/副本工程整包验证"
    if role == "modular_tileable_desert_scene_candidate":
        return "第二梯队：可平铺荒漠场景/路线底座验证"
    if role == "legacy_canyon_terrain_reference":
        return "低优先级：老牌峡谷地形参考，不作主线"
    if role == "free_terrain_technical_base":
        return "免费技术底座：买包前可先验证"
    if "booster" in role or natural >= 7 and complete < 6:
        return "混合迁移补强：不替代完整场景"
    return "参考/备选：先看授权和 demo 截图"


def markdown(candidates: list[dict[str, Any]]) -> str:
    ranked = sorted(candidates, key=weighted_score, reverse=True)
    lines = [
        "# 高精荒漠大资产候选评分",
        "",
        "更新时间：2026-08-21",
        "",
        "本表用于下载前排序；真实资产下载后仍必须经过 `large_asset_validation_protocol.md` 的 Gate 0-5。",
        "",
        "## 权重",
        "",
        "| 指标 | 权重 |",
        "| --- | ---: |",
    ]
    for key, weight in WEIGHTS.items():
        lines.append(f"| `{key}` | {weight:.2f} |")

    lines.extend(
        [
            "",
            "## 排序",
            "",
            "| 排名 | 候选 | 加权分 | 用途 | 管线/格式 | 估计体积 | 建议 |",
            "| ---: | --- | ---: | --- | --- | ---: | --- |",
        ]
    )
    for idx, c in enumerate(ranked, start=1):
        size = c.get("estimated_size_gb")
        size_text = "待查" if size is None else f"{float(size):.2f}GB"
        lines.append(
            "| {idx} | {name} | {score:.2f} | {role} | {pipeline} / {fmt} | {size} | {rec} |".format(
                idx=idx,
                name=str(c.get("name", "unknown")).replace("|", "/"),
                score=weighted_score(c),
                role=str(c.get("role", "")).replace("|", "/"),
                pipeline=str(c.get("pipeline", "")).replace("|", "/"),
                fmt=str(c.get("format", "")).replace("|", "/"),
                size=size_text,
                rec=recommendation(c).replace("|", "/"),
            )
        )

    lines.extend(
        [
            "",
            "## 执行结论",
            "",
            "- 当前执行路线已改为免费路线：优先验证 Unity 官方 `Terrain Sample Asset Pack`，并继续用 Poly Haven/ambientCG 精修当前 1km² 高精荒漠沙盒。",
            "- `Coast & Dunes`、`Pure Nature 2 : Mojave Desert` 等付费/账号资产只保留为备用候选池；除非用户再次明确选择并确认授权/购买，否则不作为当前下载目标。",
            "- 排序表仍保留调研价值，用于以后比较视觉上限、整包路线或混合迁移，不覆盖当前免费路线。",
            "- 城镇、废土、遗迹类完整场景包只作为副本验证或结构参考，不替代自然越野荒漠主线。",
            "- `Desert Rocks Pack` 这类素材包只用于混合迁移补强，不能当完整场景。",
        ]
    )
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description="生成高精荒漠大资产候选下载前排序")
    parser.add_argument("--matrix", default=str(DEFAULT_MATRIX), help="候选 JSON 矩阵")
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT), help="Markdown 输出路径")
    args = parser.parse_args()

    matrix = Path(args.matrix).expanduser().resolve()
    output = Path(args.output).expanduser().resolve()
    text = markdown(load_candidates(matrix))
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(text, encoding="utf-8")
    print(text)
    print("VLN_HIGH_PRECISION_LARGE_ASSET_CANDIDATE_RANKING_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
