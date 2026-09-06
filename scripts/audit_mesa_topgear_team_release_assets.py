#!/usr/bin/env python3
"""Audit and copy the Mesa Topgear Mix Unity release asset set.

The script intentionally does not delete anything from the source project.  It
builds a conservative dependency graph from Unity GUID references, reports the
unused asset weight under the old broad-copy roots, and can copy only the target
scene dependencies plus the small VLN code/tooling folders into a release
project.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import sys
import time
from collections import deque
from pathlib import Path
from typing import Iterable


GUID_RE = re.compile(rb"guid:\s*([0-9a-fA-F]{32})")
DEFAULT_SCENE = "Assets/VLN/Scenes/VLNMesaTopgearMixVehicleScale4p0WorldCandidate.unity"
RAW_PACKAGE_SUFFIXES = (".unitypackage", ".assetpackage")

LEGACY_ASSET_ROOTS = (
    "Assets/BK/PureNature_MesaDesert",
    "Assets/BK/Pure_Common",
    "Assets/Castle Valley Collection 1",
    "Assets/NatureManufacture Assets",
    "Assets/ForestLake",
    "Assets/The Wasteland Package",
    "Assets/HIVEMIND/PostApocalypticTown",
    "Assets/VLN",
    "Assets/Resources",
)

REQUIRED_EXTRA_ROOTS = (
    "Assets/VLN/Editor",
    "Assets/VLN/Scripts",
    "Assets/VLN/LiDARScanPatterns",
    "Assets/VLN/Materials",
    "Assets/VLN/PostProcessing",
    "Assets/VLN/Shaders",
    "Assets/VLN/Terrain",
    "Assets/Resources",
)


def posix(path: Path) -> str:
    return path.as_posix()


def rel_to_project(project: Path, path: Path) -> str:
    return path.resolve().relative_to(project.resolve()).as_posix()


def is_raw_package(path: Path | str) -> bool:
    name = str(path).lower()
    return name.endswith(RAW_PACKAGE_SUFFIXES)


def file_size(path: Path) -> int:
    try:
        return path.stat().st_size
    except OSError:
        return 0


def read_bytes_for_guid_scan(path: Path) -> bytes:
    try:
        return path.read_bytes()
    except OSError:
        return b""


def find_guids(path: Path) -> set[str]:
    data = read_bytes_for_guid_scan(path)
    if not data:
        return set()
    return {match.group(1).decode("ascii").lower() for match in GUID_RE.finditer(data)}


def build_guid_map(project: Path) -> dict[str, str]:
    guid_map: dict[str, str] = {}
    assets_root = project / "Assets"
    for meta_path in assets_root.rglob("*.meta"):
        try:
            data = meta_path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", data, flags=re.MULTILINE)
        if not match:
            continue
        asset_path = meta_path.with_suffix("")
        guid_map[match.group(1).lower()] = rel_to_project(project, asset_path)
    return guid_map


def iter_files_under(project: Path, rel_roots: Iterable[str]) -> Iterable[str]:
    for rel_root in rel_roots:
        root = project / rel_root
        if root.is_file():
            if not is_raw_package(root):
                yield rel_root
            meta = root.with_name(root.name + ".meta")
            if meta.is_file():
                yield rel_to_project(project, meta)
            continue
        if not root.is_dir():
            continue
        for file_path in root.rglob("*"):
            if file_path.is_file() and not is_raw_package(file_path):
                yield rel_to_project(project, file_path)


def gather_parent_meta_files(project: Path, rel_paths: Iterable[str]) -> set[str]:
    metas: set[str] = set()
    for rel_path in rel_paths:
        parts = Path(rel_path).parts
        if not parts:
            continue
        current = Path(parts[0])
        for part in parts[1:-1]:
            meta = project / (posix(current) + ".meta")
            if meta.is_file():
                metas.add(rel_to_project(project, meta))
            current /= part
    return metas


def add_file_and_meta(project: Path, rel_path: str, selected: set[str]) -> None:
    path = project / rel_path
    if not path.is_file() or is_raw_package(path):
        return
    selected.add(rel_path)
    meta = path.with_name(path.name + ".meta")
    if meta.is_file():
        selected.add(rel_to_project(project, meta))


def resolve_scene_dependencies(project: Path, scene_rel: str, guid_map: dict[str, str]) -> tuple[set[str], set[str]]:
    selected: set[str] = set()
    missing_guids: set[str] = set()
    queue: deque[str] = deque()
    seen_assets: set[str] = set()

    queue.append(scene_rel)
    while queue:
        rel_path = queue.popleft()
        if rel_path in seen_assets:
            continue
        seen_assets.add(rel_path)
        path = project / rel_path
        if not path.exists() or is_raw_package(path):
            continue
        add_file_and_meta(project, rel_path, selected)

        scan_paths = [path]
        meta = path.with_name(path.name + ".meta")
        if meta.is_file():
            scan_paths.append(meta)

        for scan_path in scan_paths:
            for guid in find_guids(scan_path):
                dependency = guid_map.get(guid)
                if not dependency:
                    missing_guids.add(guid)
                    continue
                if dependency not in seen_assets:
                    queue.append(dependency)

    return selected, missing_guids


def sum_paths(project: Path, rel_paths: Iterable[str]) -> int:
    total = 0
    for rel_path in rel_paths:
        path = project / rel_path
        if path.is_file():
            total += file_size(path)
    return total


def summarize_by_root(project: Path, rel_paths: Iterable[str]) -> list[dict[str, object]]:
    buckets: dict[str, dict[str, object]] = {}
    for rel_path in rel_paths:
        path = project / rel_path
        if not path.is_file():
            continue
        parts = Path(rel_path).parts
        if len(parts) >= 3 and parts[0] == "Assets" and parts[1] == "BK":
            key = "/".join(parts[:3])
        elif len(parts) >= 2:
            key = "/".join(parts[:2])
            if key == "Assets/HIVEMIND" and len(parts) >= 3:
                key = "/".join(parts[:3])
            if key == "Assets/NatureManufacture" and len(parts) >= 3:
                key = "Assets/NatureManufacture Assets"
        else:
            key = rel_path
        bucket = buckets.setdefault(key, {"root": key, "file_count": 0, "size_bytes": 0})
        bucket["file_count"] = int(bucket["file_count"]) + 1
        bucket["size_bytes"] = int(bucket["size_bytes"]) + file_size(path)
    return sorted(buckets.values(), key=lambda item: int(item["size_bytes"]), reverse=True)


def format_bytes(size: int) -> str:
    units = ["B", "KiB", "MiB", "GiB", "TiB"]
    value = float(size)
    for unit in units:
        if value < 1024 or unit == units[-1]:
            return f"{value:.2f} {unit}" if unit != "B" else f"{int(value)} B"
        value /= 1024
    return f"{size} B"


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as fp:
        for chunk in iter(lambda: fp.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def copy_files(project: Path, release_project: Path, rel_paths: Iterable[str]) -> None:
    for rel_path in sorted(set(rel_paths)):
        source = project / rel_path
        if not source.is_file() or is_raw_package(source):
            continue
        target = release_project / rel_path
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)


def write_report(report: dict[str, object], report_path: Path) -> None:
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    md_path = report_path.with_suffix(".md")
    lines = [
        "# Mesa Topgear Mix Team Release Asset Audit",
        "",
        f"- generated_at: `{report['generated_at']}`",
        f"- scene: `{report['target_scene']}`",
        f"- old_broad_copy_size: `{report['legacy_scope_size_human']}`",
        f"- selected_release_asset_size: `{report['selected_asset_size_human']}`",
        f"- unused_under_old_roots: `{report['unused_legacy_asset_size_human']}`",
        f"- selected_asset_files: `{report['selected_asset_file_count']}`",
        f"- unused_legacy_files: `{report['unused_legacy_file_count']}`",
        "",
        "## Selected Assets By Root",
    ]
    for item in report["selected_by_root"]:  # type: ignore[index]
        lines.append(f"- `{item['root']}`: {item['file_count']} files, {item['size_human']}")
    lines.extend(["", "## Largest Unused Files Under Old Copy Roots"])
    for item in report["largest_unused_legacy_files"]:  # type: ignore[index]
        lines.append(f"- `{item['path']}`: {item['size_human']}")
    md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit/copy Mesa Topgear Mix release dependencies")
    parser.add_argument("--source-project", default="UnityProjects/VLN_Offroad_LargeAssetSandbox")
    parser.add_argument("--scene", default=DEFAULT_SCENE)
    parser.add_argument("--report", default=".runtime/release_asset_audit/mesa_topgear_mix_asset_dependency_report.json")
    parser.add_argument("--copy-to", default="", help="Optional release project directory to receive selected assets")
    parser.add_argument("--manifest-name", default="VLN_MESA_TOPGEAR_TEAM_RELEASE_MANIFEST.json")
    args = parser.parse_args()

    repo_root = Path.cwd().resolve()
    source_project = (repo_root / args.source_project).resolve()
    if not source_project.is_dir():
        print(f"source_project_missing={source_project}", file=sys.stderr)
        return 1
    scene_rel = args.scene.replace(os.sep, "/")
    scene_path = source_project / scene_rel
    if not scene_path.is_file():
        print(f"target_scene_missing={scene_path}", file=sys.stderr)
        return 1

    guid_map = build_guid_map(source_project)
    scene_dependencies, missing_guids = resolve_scene_dependencies(source_project, scene_rel, guid_map)

    selected = set(scene_dependencies)
    for rel_path in iter_files_under(source_project, REQUIRED_EXTRA_ROOTS):
        selected.add(rel_path)
    selected.update(gather_parent_meta_files(source_project, selected))
    selected = {path for path in selected if not is_raw_package(path) and (source_project / path).is_file()}

    legacy_files = set(iter_files_under(source_project, LEGACY_ASSET_ROOTS))
    unused_legacy = sorted(legacy_files - selected, key=lambda rel: file_size(source_project / rel), reverse=True)

    largest_unused = [
        {
            "path": rel,
            "size_bytes": file_size(source_project / rel),
            "size_human": format_bytes(file_size(source_project / rel)),
        }
        for rel in unused_legacy[:80]
    ]

    selected_by_root = []
    for item in summarize_by_root(source_project, selected):
        item["size_human"] = format_bytes(int(item["size_bytes"]))
        selected_by_root.append(item)

    legacy_size = sum_paths(source_project, legacy_files)
    selected_size = sum_paths(source_project, selected)
    unused_size = sum_paths(source_project, unused_legacy)
    generated_at = time.strftime("%Y-%m-%dT%H:%M:%S%z")

    report = {
        "schema": "vln_mesa_topgear_mix_release_asset_audit_v1",
        "generated_at": generated_at,
        "source_project": str(source_project),
        "target_scene": scene_rel,
        "target_scene_sha256": sha256(scene_path),
        "guid_meta_count": len(guid_map),
        "missing_guid_count": len(missing_guids),
        "missing_guid_sample": sorted(missing_guids)[:40],
        "legacy_scope_roots": list(LEGACY_ASSET_ROOTS),
        "legacy_scope_file_count": len(legacy_files),
        "legacy_scope_size_bytes": legacy_size,
        "legacy_scope_size_human": format_bytes(legacy_size),
        "scene_dependency_file_count": len(scene_dependencies),
        "scene_dependency_size_bytes": sum_paths(source_project, scene_dependencies),
        "scene_dependency_size_human": format_bytes(sum_paths(source_project, scene_dependencies)),
        "required_extra_roots": list(REQUIRED_EXTRA_ROOTS),
        "selected_asset_file_count": len(selected),
        "selected_asset_size_bytes": selected_size,
        "selected_asset_size_human": format_bytes(selected_size),
        "unused_legacy_file_count": len(unused_legacy),
        "unused_legacy_asset_size_bytes": unused_size,
        "unused_legacy_asset_size_human": format_bytes(unused_size),
        "selected_by_root": selected_by_root,
        "largest_unused_legacy_files": largest_unused,
    }

    release_project = Path(args.copy_to).resolve() if args.copy_to else None
    if release_project is not None:
        copy_files(source_project, release_project, selected)
        manifest = dict(report)
        manifest.update(
            {
                "schema": "vln_mesa_topgear_mix_team_release_manifest_v2",
                "release_project": str(release_project),
                "copy_mode": "scene_guid_dependencies_plus_vln_runtime_tooling",
                "open_command": "./scripts/open_high_precision_world_model.sh --scene mesa_topgear_mix",
                "check_command": "./scripts/check_mesa_topgear_team_release_project.sh",
            }
        )
        manifest_path = release_project / args.manifest_name
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        report["release_project"] = str(release_project)
        report["release_manifest"] = str(manifest_path)

    report_path = (repo_root / args.report).resolve()
    write_report(report, report_path)

    print("VLN_MESA_TOPGEAR_RELEASE_ASSET_AUDIT_OK")
    print(f"report={report_path}")
    print(f"target_scene={scene_rel}")
    print(f"legacy_scope_size={report['legacy_scope_size_human']}")
    print(f"selected_asset_size={report['selected_asset_size_human']}")
    print(f"unused_legacy_asset_size={report['unused_legacy_asset_size_human']}")
    if release_project is not None:
        print(f"release_project={release_project}")
        print(f"release_manifest={release_project / args.manifest_name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
