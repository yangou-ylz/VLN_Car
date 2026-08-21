#!/usr/bin/env python3
"""Download the controlled first batch of high precision desert assets.

This script only uses metadata already recorded under VLN_REFERENCE_LIBRARY. It
keeps downloads inside VLN_ASSETS_CACHE, mirrors the selected subset into the
Unity project, and writes a manifest for later import/audit.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


VLN_ROOT = Path(__file__).resolve().parents[1]
METADATA_FILE = VLN_ROOT / "VLN_REFERENCE_LIBRARY/high_precision_desert_research/polyhaven_metadata/selected_files.json"
RAW_ROOT = VLN_ROOT / "VLN_ASSETS_CACHE/high_precision_desert/raw_downloads/polyhaven"
SELECTED_ROOT = VLN_ROOT / "VLN_ASSETS_CACHE/high_precision_desert/selected_unity_subset/polyhaven"
UNITY_ROOT = VLN_ROOT / "UnityProjects/VLN_Offroad/Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven"
REFERENCE_ROOT = VLN_ROOT / "VLN_REFERENCE_LIBRARY/high_precision_desert_research"


@dataclass(frozen=True)
class AssetFileSpec:
    asset: str
    kind: str
    key_path: tuple[str, ...]
    purpose: str
    unity_subdir: str


FIRST_BATCH: tuple[AssetFileSpec, ...] = (
    # 4K terrain materials. JPG is deliberate: Unity imports it reliably and the
    # files remain small enough for quick iteration.
    AssetFileSpec("aerial_sand", "texture", ("Diffuse", "4k", "jpg"), "主沙地 albedo", "Surfaces/aerial_sand"),
    AssetFileSpec("aerial_sand", "texture", ("nor_gl", "4k", "jpg"), "主沙地 OpenGL normal", "Surfaces/aerial_sand"),
    AssetFileSpec("aerial_sand", "texture", ("AO", "4k", "jpg"), "主沙地 AO", "Surfaces/aerial_sand"),
    AssetFileSpec("aerial_sand", "texture", ("Rough", "4k", "jpg"), "主沙地 roughness", "Surfaces/aerial_sand"),
    AssetFileSpec("aerial_sand", "texture", ("Displacement", "4k", "jpg"), "主沙地高度/轮迹参考", "Surfaces/aerial_sand"),
    AssetFileSpec("aerial_ground_rock", "texture", ("Diffuse", "4k", "jpg"), "岩质地表 albedo", "Surfaces/aerial_ground_rock"),
    AssetFileSpec("aerial_ground_rock", "texture", ("nor_gl", "4k", "jpg"), "岩质地表 OpenGL normal", "Surfaces/aerial_ground_rock"),
    AssetFileSpec("aerial_ground_rock", "texture", ("AO", "4k", "jpg"), "岩质地表 AO", "Surfaces/aerial_ground_rock"),
    AssetFileSpec("aerial_ground_rock", "texture", ("Rough", "4k", "jpg"), "岩质地表 roughness", "Surfaces/aerial_ground_rock"),
    AssetFileSpec("aerial_ground_rock", "texture", ("Displacement", "4k", "jpg"), "岩质地表高度参考", "Surfaces/aerial_ground_rock"),
    AssetFileSpec("cliff_side", "texture", ("Diffuse", "4k", "jpg"), "岩壁/峡谷 albedo", "Surfaces/cliff_side"),
    AssetFileSpec("cliff_side", "texture", ("nor_gl", "4k", "jpg"), "岩壁/峡谷 OpenGL normal", "Surfaces/cliff_side"),
    AssetFileSpec("cliff_side", "texture", ("AO", "4k", "jpg"), "岩壁/峡谷 AO", "Surfaces/cliff_side"),
    AssetFileSpec("cliff_side", "texture", ("Rough", "4k", "jpg"), "岩壁/峡谷 roughness", "Surfaces/cliff_side"),
    AssetFileSpec("cliff_side", "texture", ("Displacement", "4k", "jpg"), "岩壁/峡谷高度参考", "Surfaces/cliff_side"),
    # Lighting.
    AssetFileSpec("goegap", "hdri", ("hdri", "4k", "hdr"), "荒漠主天空/环境光", "HDRI/goegap"),
    AssetFileSpec("goegap_road", "hdri", ("hdri", "4k", "hdr"), "荒漠道路天空/备选环境光", "HDRI/goegap_road"),
    # Rock model.
    AssetFileSpec("boulder_01", "model", ("fbx", "4k", "fbx"), "路边大石视觉模型", "Models/boulder_01"),
    AssetFileSpec("boulder_01", "model_texture", ("Diffuse", "4k", "jpg"), "大石 albedo", "Models/boulder_01"),
    AssetFileSpec("boulder_01", "model_texture", ("nor_gl", "4k", "jpg"), "大石 OpenGL normal", "Models/boulder_01"),
    AssetFileSpec("boulder_01", "model_texture", ("AO", "4k", "jpg"), "大石 AO", "Models/boulder_01"),
    AssetFileSpec("boulder_01", "model_texture", ("Rough", "4k", "jpg"), "大石 roughness", "Models/boulder_01"),
    # Dry vegetation. 2K is enough for first placement; later upgrade nearby hero assets to 4K if needed.
    AssetFileSpec("didelta_spinosa", "model", ("fbx", "2k", "fbx"), "干旱灌木视觉模型", "Models/didelta_spinosa"),
    AssetFileSpec("didelta_spinosa", "model_texture", ("Diffuse", "2k", "jpg"), "干旱灌木 albedo", "Models/didelta_spinosa"),
    AssetFileSpec("didelta_spinosa", "model_texture", ("Alpha", "2k", "jpg"), "干旱灌木 alpha", "Models/didelta_spinosa"),
    AssetFileSpec("didelta_spinosa", "model_texture", ("nor_gl", "2k", "jpg"), "干旱灌木 OpenGL normal", "Models/didelta_spinosa"),
    AssetFileSpec("didelta_spinosa", "model_texture", ("AO", "2k", "jpg"), "干旱灌木 AO", "Models/didelta_spinosa"),
    AssetFileSpec("didelta_spinosa", "model_texture", ("Rough", "2k", "jpg"), "干旱灌木 roughness", "Models/didelta_spinosa"),
    AssetFileSpec("didelta_spinosa", "model_texture", ("translucency", "2k", "jpg"), "干旱灌木半透参考", "Models/didelta_spinosa"),
    AssetFileSpec("quiver_tree_01", "model", ("fbx", "2k", "fbx"), "荒漠树视觉模型", "Models/quiver_tree_01"),
    AssetFileSpec("quiver_tree_01", "model_texture", ("trunk_diff", "2k", "jpg"), "荒漠树树干 albedo", "Models/quiver_tree_01"),
    AssetFileSpec("quiver_tree_01", "model_texture", ("trunk_nor_gl", "2k", "jpg"), "荒漠树树干 normal", "Models/quiver_tree_01"),
    AssetFileSpec("quiver_tree_01", "model_texture", ("trunk_ao", "2k", "jpg"), "荒漠树树干 AO", "Models/quiver_tree_01"),
    AssetFileSpec("quiver_tree_01", "model_texture", ("trunk_rough", "2k", "jpg"), "荒漠树树干 roughness", "Models/quiver_tree_01"),
    AssetFileSpec("quiver_tree_01", "model_texture", ("leaf_diff", "2k", "jpg"), "荒漠树叶片 albedo", "Models/quiver_tree_01"),
    AssetFileSpec("quiver_tree_01", "model_texture", ("leaf_nor_gl", "2k", "jpg"), "荒漠树叶片 normal", "Models/quiver_tree_01"),
    AssetFileSpec("quiver_tree_01", "model_texture", ("leaf_ao", "2k", "jpg"), "荒漠树叶片 AO", "Models/quiver_tree_01"),
    AssetFileSpec("quiver_tree_01", "model_texture", ("leaf_rough", "2k", "jpg"), "荒漠树叶片 roughness", "Models/quiver_tree_01"),
)


def get_nested(root: dict, path: Iterable[str]) -> dict:
    node = root
    for key in path:
        if not isinstance(node, dict) or key not in node:
            raise KeyError(".".join(path))
        node = node[key]
    if not isinstance(node, dict) or "url" not in node or "size" not in node:
        raise KeyError(".".join(path))
    return node


def file_name_from_url(url: str) -> str:
    return urllib.parse.unquote(url.rsplit("/", 1)[-1])


def md5(path: Path) -> str:
    h = hashlib.md5()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def human_size(n: int) -> str:
    value = float(n)
    for unit in ("B", "KB", "MB", "GB"):
        if value < 1024 or unit == "GB":
            return f"{value:.2f}{unit}"
        value /= 1024
    return f"{n}B"


def build_download_plan(metadata: dict) -> list[dict]:
    plan: list[dict] = []
    seen_urls: set[str] = set()
    for spec in FIRST_BATCH:
        file_meta = get_nested(metadata[spec.asset], spec.key_path)
        url = file_meta["url"]
        if url in seen_urls:
            continue
        seen_urls.add(url)
        filename = file_name_from_url(url)
        plan.append(
            {
                "asset": spec.asset,
                "kind": spec.kind,
                "purpose": spec.purpose,
                "key_path": list(spec.key_path),
                "url": url,
                "size": int(file_meta["size"]),
                "md5_expected": file_meta.get("md5"),
                "raw_path": str(RAW_ROOT / spec.asset / filename),
                "selected_path": str(SELECTED_ROOT / spec.unity_subdir / filename),
                "unity_path": str(UNITY_ROOT / spec.unity_subdir / filename),
                "license": "CC0 / Poly Haven",
                "source": f"https://polyhaven.com/a/{spec.asset}",
            }
        )
    return plan


def configure_proxy(proxy: str | None) -> str:
    chosen = proxy or os.environ.get("HTTPS_PROXY") or os.environ.get("https_proxy") or os.environ.get("ALL_PROXY") or os.environ.get("all_proxy")
    if not chosen:
        chosen = "http://127.0.0.1:7897/"
    os.environ["HTTP_PROXY"] = chosen
    os.environ["HTTPS_PROXY"] = chosen
    os.environ["ALL_PROXY"] = chosen
    os.environ["http_proxy"] = chosen
    os.environ["https_proxy"] = chosen
    os.environ["all_proxy"] = chosen
    os.environ.setdefault("NO_PROXY", "localhost,127.0.0.1,::1")
    os.environ.setdefault("no_proxy", "localhost,127.0.0.1,::1")
    return chosen


def download_one(item: dict, timeout: float) -> dict:
    dest = Path(item["raw_path"])
    dest.parent.mkdir(parents=True, exist_ok=True)
    expected_size = item["size"]
    expected_md5 = item.get("md5_expected")

    if dest.exists() and dest.stat().st_size == expected_size:
        current_md5 = md5(dest)
        if not expected_md5 or current_md5 == expected_md5:
            return {**item, "status": "skipped_existing", "md5": current_md5, "sha256": sha256(dest), "seconds": 0.0, "mbps": None}

    tmp = dest.with_suffix(dest.suffix + ".part")
    if tmp.exists():
        tmp.unlink()

    request = urllib.request.Request(item["url"], headers={"User-Agent": "VLN-high-precision-desert-downloader/1.0"})
    start = time.time()
    downloaded = 0
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response, tmp.open("wb") as f:
            while True:
                chunk = response.read(1024 * 1024)
                if not chunk:
                    break
                f.write(chunk)
                downloaded += len(chunk)
    except (urllib.error.URLError, TimeoutError) as exc:
        if tmp.exists():
            tmp.unlink()
        raise RuntimeError(f"下载失败：{item['url']} -> {exc}") from exc

    seconds = max(time.time() - start, 1e-6)
    tmp.replace(dest)

    actual_size = dest.stat().st_size
    if actual_size != expected_size:
        raise RuntimeError(f"文件大小不匹配：{dest} expected={expected_size} actual={actual_size}")
    current_md5 = md5(dest)
    if expected_md5 and current_md5 != expected_md5:
        raise RuntimeError(f"MD5 不匹配：{dest} expected={expected_md5} actual={current_md5}")

    return {**item, "status": "downloaded", "md5": current_md5, "sha256": sha256(dest), "seconds": seconds, "mbps": actual_size / seconds / 1024 / 1024}


def mirror_file(src: Path, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists() and dst.stat().st_size == src.stat().st_size and md5(dst) == md5(src):
        return
    shutil.copy2(src, dst)


def write_manifest(results: list[dict], proxy: str, dry_run: bool) -> None:
    REFERENCE_ROOT.mkdir(parents=True, exist_ok=True)
    manifest = {
        "generated_at": time.strftime("%Y-%m-%d %H:%M:%S %z"),
        "dry_run": dry_run,
        "proxy_used": proxy,
        "scene_scale_rule": "第一版高精荒漠沙盒至少 7000㎡，默认 120m x 120m = 14400㎡",
        "total_bytes": sum(int(x["size"]) for x in results),
        "total_human": human_size(sum(int(x["size"]) for x in results)),
        "items": results,
    }
    out_json = REFERENCE_ROOT / "polyhaven_first_batch_manifest.json"
    out_json.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    lines = [
        "# Poly Haven 第一批高精荒漠资产下载记录",
        "",
        f"生成时间：{manifest['generated_at']}",
        f"代理：`{proxy}`",
        f"总量：`{manifest['total_human']}`",
        f"沙盒面积规则：{manifest['scene_scale_rule']}",
        "",
        "| 资产 | 类型 | 用途 | 大小 | 状态 | 本地路径 |",
        "| --- | --- | --- | ---: | --- | --- |",
    ]
    for item in results:
        lines.append(
            f"| `{item['asset']}` | {item['kind']} | {item['purpose']} | {human_size(int(item['size']))} | {item.get('status', 'planned')} | `{item.get('unity_path') or item['raw_path']}` |"
        )
    (REFERENCE_ROOT / "polyhaven_first_batch_manifest.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="下载阶段 21 高精荒漠 Poly Haven 小样本资产")
    parser.add_argument("--dry-run", action="store_true", help="只打印计划和写 manifest，不下载")
    parser.add_argument("--proxy", default=None, help="显式代理，例如 http://127.0.0.1:7897/")
    parser.add_argument("--max-gb", type=float, default=1.0, help="本批下载上限，默认 1GB")
    parser.add_argument("--timeout", type=float, default=60.0, help="单连接超时秒数")
    parser.add_argument("--no-unity-mirror", action="store_true", help="不复制到 Unity ExternalAssets/HighPrecisionDesert")
    args = parser.parse_args()

    if not METADATA_FILE.exists():
        print(f"缺少元数据：{METADATA_FILE}", file=sys.stderr)
        return 2

    proxy = configure_proxy(args.proxy)
    metadata = json.loads(METADATA_FILE.read_text(encoding="utf-8"))
    plan = build_download_plan(metadata)
    total = sum(item["size"] for item in plan)
    limit = int(args.max_gb * 1024 * 1024 * 1024)

    print(f"代理：{proxy}")
    print(f"计划文件数：{len(plan)}")
    print(f"计划总量：{human_size(total)} / 上限 {args.max_gb:.2f}GB")
    if total > limit:
        print("本批计划超过上限，停止。请缩小批次或提高 --max-gb。", file=sys.stderr)
        write_manifest(plan, proxy, dry_run=True)
        return 3

    if args.dry_run:
        for item in plan:
            print(f"DRY {item['asset']} {item['purpose']} {human_size(item['size'])}")
        write_manifest(plan, proxy, dry_run=True)
        print("VLN_HIGH_PRECISION_DESERT_DOWNLOAD_DRY_RUN_OK")
        return 0

    RAW_ROOT.mkdir(parents=True, exist_ok=True)
    SELECTED_ROOT.mkdir(parents=True, exist_ok=True)
    if not args.no_unity_mirror:
        UNITY_ROOT.mkdir(parents=True, exist_ok=True)

    results: list[dict] = []
    for idx, item in enumerate(plan, 1):
        print(f"[{idx}/{len(plan)}] {item['asset']} - {item['purpose']} - {human_size(item['size'])}")
        result = download_one(item, timeout=args.timeout)
        src = Path(result["raw_path"])
        mirror_file(src, Path(result["selected_path"]))
        if not args.no_unity_mirror:
            mirror_file(src, Path(result["unity_path"]))
        speed = "复用已有文件" if result["mbps"] is None else f"{result['mbps']:.2f} MB/s"
        print(f"    {result['status']} {speed}")
        results.append(result)

    write_manifest(results, proxy, dry_run=False)
    print("VLN_HIGH_PRECISION_DESERT_SAMPLE_ASSETS_DOWNLOAD_OK")
    print(f"manifest={REFERENCE_ROOT / 'polyhaven_first_batch_manifest.json'}")
    print(f"unity_asset_root={UNITY_ROOT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
