#!/usr/bin/env python3
"""Inspect a large Unity/Fab/open-source scene package before importing it.

The script is intentionally read-only. It scans a folder, zip, tar, or
Unity .unitypackage and reports whether the package likely contains a complete
scene, terrain assets, project settings, render-pipeline assets, models,
textures, materials, shaders, and possible collider/physics assets.
"""

from __future__ import annotations

import argparse
import json
import tarfile
import zipfile
from collections import Counter
from pathlib import Path
from typing import Iterable


SCENE_EXT = {".unity"}
MODEL_EXT = {".fbx", ".obj", ".dae", ".blend", ".gltf", ".glb"}
TEXTURE_EXT = {".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".exr", ".hdr", ".psd"}
MATERIAL_EXT = {".mat", ".terrainlayer"}
SHADER_EXT = {".shader", ".shadergraph", ".hlsl", ".compute"}
UNITY_ASSET_EXT = {".asset", ".prefab", ".controller", ".anim", ".physicmaterial"}
PROJECT_SETTING_HINTS = ("ProjectSettings/", "Packages/manifest.json", "Packages/packages-lock.json")
PIPELINE_HINTS = ("urp", "universalrenderpipeline", "hdrp", "highdefinitionrenderpipeline", "renderpipeline")
PHYSICS_HINTS = ("collider", "collision", "physic", "physics", "terraincollider")


def walk_folder(path: Path) -> list[str]:
    return [str(p.relative_to(path)) for p in path.rglob("*") if p.is_file()]


def walk_zip(path: Path) -> list[str]:
    with zipfile.ZipFile(path) as zf:
        return [n for n in zf.namelist() if not n.endswith("/")]


def walk_tar(path: Path) -> list[str]:
    with tarfile.open(path) as tf:
        return [m.name for m in tf.getmembers() if m.isfile()]


def normalize_unitypackage_pathname(raw: bytes) -> str:
    text = raw.decode("utf-8", "replace").replace("\x00", "").strip()
    # Unity exports pathname files with a trailing "00" line in some packages.
    return "".join(line for line in text.splitlines() if line != "00").strip()


def walk_unitypackage(path: Path) -> list[str]:
    paths: list[str] = []
    with tarfile.open(path) as tf:
        for member in tf:
            if not (member.isfile() and member.name.endswith("/pathname")):
                continue
            handle = tf.extractfile(member)
            if handle is None:
                continue
            pathname = normalize_unitypackage_pathname(handle.read())
            if pathname:
                paths.append(pathname)
    return paths


def list_entries(path: Path) -> list[str]:
    if path.is_dir():
        return walk_folder(path)
    lower = path.name.lower()
    if lower.endswith(".unitypackage"):
        return walk_unitypackage(path)
    if zipfile.is_zipfile(path):
        return walk_zip(path)
    if lower.endswith((".tar", ".tar.gz", ".tgz")):
        return walk_tar(path)
    raise ValueError(f"不支持的输入：{path}")


def has_any(entries: Iterable[str], hints: Iterable[str]) -> bool:
    lower_hints = tuple(h.lower() for h in hints)
    return any(any(h in e.lower() for h in lower_hints) for e in entries)


def count_by_ext(entries: Iterable[str]) -> Counter[str]:
    counter: Counter[str] = Counter()
    for entry in entries:
        suffix = Path(entry).suffix.lower()
        if suffix:
            counter[suffix] += 1
    return counter


def sample(entries: list[str], extensions: set[str], limit: int = 12) -> list[str]:
    out = []
    for entry in entries:
        if Path(entry).suffix.lower() in extensions:
            out.append(entry)
        if len(out) >= limit:
            break
    return out


def score(summary: dict) -> tuple[int, list[str], list[str]]:
    points = 0
    strengths: list[str] = []
    risks: list[str] = []

    if summary["scene_count"] > 0:
        points += 20
        strengths.append("包含 Unity scene，可作为整包场景候选")
    else:
        risks.append("未发现 .unity scene，可能只是素材库")

    if summary["terrain_asset_count"] > 0:
        points += 18
        strengths.append("包含 Terrain/TerrainLayer 相关资产")
    else:
        risks.append("Terrain 资产不明显，需要手工确认是否为 mesh 地形")

    if summary["model_count"] >= 20:
        points += 12
        strengths.append("模型数量较多，可能包含岩石/植被/道具库")
    elif summary["model_count"] > 0:
        points += 6

    if summary["texture_count"] >= 30:
        points += 12
        strengths.append("贴图数量较多，适合高精视觉")
    elif summary["texture_count"] > 0:
        points += 6

    if summary["material_count"] >= 10:
        points += 10
        strengths.append("材质/TerrainLayer 数量较多")
    elif summary["material_count"] > 0:
        points += 4

    if summary["prefab_count"] >= 20:
        points += 10
        strengths.append("Prefab 数量较多，方便迁移子集")
    elif summary["prefab_count"] > 0:
        points += 5

    if summary["has_pipeline_hint"]:
        points += 4
        strengths.append("发现渲染管线相关资产/关键词")

    if summary["has_physics_hint"]:
        points += 6
        strengths.append("发现 collider/physics 相关关键词")

    if summary["has_project_settings"]:
        risks.append("包含 ProjectSettings/Packages，必须进副本工程，不能直接导入主工程")
        points -= 8

    return max(points, 0), strengths, risks


def main() -> int:
    parser = argparse.ArgumentParser(description="只读扫描高精荒漠大资产包")
    parser.add_argument("path", help="资产包路径：文件夹、zip、tar 或 .unitypackage")
    parser.add_argument("--output", default=None, help="JSON 输出路径")
    args = parser.parse_args()

    path = Path(args.path).expanduser().resolve()
    entries = sorted(list_entries(path))
    ext_counter = count_by_ext(entries)
    lower_entries = [e.lower() for e in entries]

    summary = {
        "path": str(path),
        "entry_count": len(entries),
        "scene_count": sum(1 for e in entries if Path(e).suffix.lower() in SCENE_EXT),
        "model_count": sum(1 for e in entries if Path(e).suffix.lower() in MODEL_EXT),
        "texture_count": sum(1 for e in entries if Path(e).suffix.lower() in TEXTURE_EXT),
        "material_count": sum(1 for e in entries if Path(e).suffix.lower() in MATERIAL_EXT),
        "shader_count": sum(1 for e in entries if Path(e).suffix.lower() in SHADER_EXT),
        "prefab_count": ext_counter.get(".prefab", 0),
        "terrain_asset_count": sum(1 for e in lower_entries if "terrain" in e or e.endswith(".terrainlayer")),
        "has_project_settings": has_any(entries, PROJECT_SETTING_HINTS),
        "has_pipeline_hint": has_any(entries, PIPELINE_HINTS),
        "has_physics_hint": has_any(entries, PHYSICS_HINTS),
        "top_extensions": ext_counter.most_common(20),
        "sample_scenes": sample(entries, SCENE_EXT),
        "sample_models": sample(entries, MODEL_EXT),
        "sample_textures": sample(entries, TEXTURE_EXT),
        "sample_materials": sample(entries, MATERIAL_EXT),
    }
    package_score, strengths, risks = score(summary)
    summary["scene_package_score"] = package_score
    summary["strengths"] = strengths
    summary["risks"] = risks

    text = json.dumps(summary, ensure_ascii=False, indent=2)
    if args.output:
        out = Path(args.output)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(text + "\n", encoding="utf-8")
    print(text)
    print("VLN_HIGH_PRECISION_LARGE_ASSET_PACKAGE_INSPECT_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
