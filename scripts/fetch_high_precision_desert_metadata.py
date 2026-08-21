#!/usr/bin/env python3
"""Fetch metadata for high-precision desert asset candidates.

This script downloads only small JSON metadata from public APIs. It does not
download texture/model/HDRI binaries and does not modify the Unity project.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path


VLN_ROOT = Path("/home/ubuntu22/VLN")
OUTPUT_ROOT = VLN_ROOT / "VLN_REFERENCE_LIBRARY" / "high_precision_desert_research" / "polyhaven_metadata"
USER_AGENT = "VLNHighPrecisionDesertResearch/0.1"

ASSET_QUERIES = {
    "textures_ground_terrain": {"type": "textures", "category": "ground-terrain"},
    "textures_stone": {"type": "textures", "category": "stone"},
    "models_rocks_stone": {"type": "models", "category": "nature/rocks-stone"},
    "models_plants": {"type": "models", "category": "nature/plants"},
    "models_trees": {"type": "models", "category": "nature/trees"},
    "hdris_desert_arid": {"type": "hdris", "category": "desert-arid"},
}

SELECTED_SLUGS = [
    "aerial_sand",
    "aerial_ground_rock",
    "aerial_rocks_01",
    "cliff_side",
    "boulder_01",
    "coast_rocks_01",
    "coastal_cliff_01",
    "quiver_tree_01",
    "quiver_tree_02",
    "didelta_spinosa",
    "goegap",
    "goegap_road",
    "aarfontein_dirt_road",
]


def fetch_json(url: str, timeout: float) -> object:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        payload = response.read()
    return json.loads(payload.decode("utf-8"))


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--timeout", type=float, default=25.0, help="HTTP timeout in seconds")
    parser.add_argument("--sleep", type=float, default=0.2, help="Delay between requests")
    args = parser.parse_args()

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    summary: dict[str, object] = {
        "generated_at_unix": time.time(),
        "note": "Metadata only; no asset binaries downloaded.",
        "queries": {},
        "selected_slugs": SELECTED_SLUGS,
    }

    for name, params in ASSET_QUERIES.items():
        url = "https://api.polyhaven.com/assets?" + urllib.parse.urlencode(params)
        payload = fetch_json(url, args.timeout)
        write_json(OUTPUT_ROOT / f"{name}.json", payload)
        count = len(payload) if isinstance(payload, dict) else 0
        summary["queries"][name] = {"url": url, "count": count}
        print(f"{name}: count={count}")
        time.sleep(args.sleep)

    selected_files: dict[str, object] = {}
    selected_errors: dict[str, str] = {}
    for slug in SELECTED_SLUGS:
        url = "https://api.polyhaven.com/files/" + urllib.parse.quote(slug)
        try:
            payload = fetch_json(url, args.timeout)
            selected_files[slug] = payload
            print(f"files:{slug}: ok")
        except Exception as exc:  # Keep metadata refresh useful even if one slug disappears.
            selected_errors[slug] = str(exc)
            print(f"files:{slug}: error={exc}", file=sys.stderr)
        time.sleep(args.sleep)

    write_json(OUTPUT_ROOT / "selected_files.json", selected_files)
    if selected_errors:
        write_json(OUTPUT_ROOT / "selected_errors.json", selected_errors)
    summary["selected_file_metadata_count"] = len(selected_files)
    summary["selected_file_error_count"] = len(selected_errors)
    write_json(OUTPUT_ROOT / "summary.json", summary)
    print(f"metadata_dir={OUTPUT_ROOT}")
    print("VLN_HIGH_PRECISION_DESERT_METADATA_OK")
    return 0 if not selected_errors else 2


if __name__ == "__main__":
    raise SystemExit(main())
