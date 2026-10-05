#!/usr/bin/env python3
"""Rendering / LOD / weather option corpus census (P0.1 of
docs/engine_host/RENDERING_OPTIONS_PLAN.md).

Read-only: parses the shipped graphics presets and the editor config, writes TSV
evidence under proof/render/, and prints the census summary. Stdlib only.

Usage:
  python tools/render/preset_census.py [--config-dir DIR] [--editor-config FILE] [--out DIR]
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

KEY_RE = re.compile(r"^\s*([A-Za-z_0-9]+)\s*=\s*(.*?)\s*$")
SEC_RE = re.compile(r"^\s*\[(.+?)\]\s*$")

GROUPS = (
    ("bEnableRC_", "postfx"),
    ("bEnableDayNight", "daynight"),
    ("bShowTrueSky", "daynight"),
    ("bWeather", "weather"),
    ("VolumetricCloud", "weather"),
    ("TrueSky", "weather"),
    ("bEnableFoliage", "foliage"),
    ("bEnableSubFoliage", "foliage"),
    ("nFoliage", "foliage"),
    ("fFoliage", "foliage"),
    ("fSpeedTree", "speedtree"),
    ("nSpeedTree", "speedtree"),
    ("bEnableModelLod", "lod"),
    ("nMinimumModelLod", "lod"),
    ("fModelLod", "lod"),
    ("fModelViewAngle", "lod"),
    ("fNodeLod", "lod"),
    ("nStartLodLevel", "lod"),
    ("Shadow", "shadow"),
    ("shadow", "shadow"),
    ("nShadow", "shadow"),
    ("fParticleSystem", "culling"),
    ("fSimpleModel", "culling"),
    ("fPointLight", "culling"),
    ("fAngleCull", "culling"),
    ("fSceneNodeCluster", "streaming"),
    ("MDLRender", "limits"),
    ("ClientSFX", "limits"),
    ("Terrain", "terrain"),
    ("nWater", "water"),
    ("CameraDistance", "camera"),
    ("nEngineGraphicsLevel", "selector"),
)


def group_of(key: str) -> str:
    for prefix, name in GROUPS:
        if key.startswith(prefix):
            return name
    return "other"


def parse_ini(path: Path):
    section = ""
    entries = []
    dup = 0
    seen = set()
    with path.open("r", encoding="gb18030", errors="replace") as fh:
        for line in fh:
            m = SEC_RE.match(line)
            if m:
                section = m.group(1)
                continue
            m = KEY_RE.match(line)
            if m:
                key = m.group(1)
                if key in seen:
                    dup += 1
                seen.add(key)
                entries.append((section, key, m.group(2)))
    return entries, dup


def tier_files(config_dir: Path):
    files = []
    for level in range(1, 10):
        hits = sorted(config_dir.glob("config_%d_*.ini" % level))
        if hits:
            files.append(("tier%d" % level, hits[0]))
    return files


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--config-dir", default=r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\config")
    ap.add_argument("--editor-config", default=r"C:\SeasunGame\MovieEditor\config.ini")
    ap.add_argument("--out", default="proof/render")
    args = ap.parse_args()

    config_dir = Path(args.config_dir)
    if not config_dir.is_dir():
        print("FAIL: config dir not found: %s" % config_dir)
        return 1

    sources = []
    default_ini = config_dir / "config.default.ini"
    if default_ini.exists():
        sources.append(("default", default_ini))
    sources.extend(tier_files(config_dir))
    for bd in sorted(config_dir.glob("config_bd_*.ini")):
        sources.append(("bd:" + bd.stem, bd))
    editor = Path(args.editor_config)
    if editor.exists():
        sources.append(("editor", editor))

    values = {}
    sections = {}
    per_file = []
    for label, path in sources:
        entries, dup = parse_ini(path)
        per_file.append((label, path.name, len(entries), dup))
        for section, key, value in entries:
            values.setdefault(key, {})[label] = value
            sections.setdefault(key, section)

    tier_labels = [label for label, _ in sources if label.startswith("tier")]
    if not tier_labels:
        print("FAIL: no config_N tier files found")
        return 1

    varying = []
    for key, by_source in values.items():
        tv = {by_source.get(lab, "") for lab in tier_labels}
        tv.discard("")
        if len(tv) > 1:
            varying.append(key)

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    matrix_path = out_dir / "option_matrix.tsv"
    labels = [label for label, _ in sources]
    with matrix_path.open("w", encoding="utf-8", newline="\n") as fh:
        fh.write("\t".join(["key", "group", "section", "tier_distinct"] + labels) + "\n")
        for key in sorted(values):
            by_source = values[key]
            tv = {by_source.get(lab, "") for lab in tier_labels}
            tv.discard("")
            row = [key, group_of(key), sections.get(key, ""), str(len(tv))]
            row.extend(by_source.get(lab, "") for lab in labels)
            fh.write("\t".join(row) + "\n")

    varying_path = out_dir / "varying.tsv"
    with varying_path.open("w", encoding="utf-8", newline="\n") as fh:
        fh.write("\t".join(["key", "group", "tier_distinct"] + tier_labels) + "\n")
        for key in sorted(varying):
            tv = {values[key].get(lab, "") for lab in tier_labels}
            tv.discard("")
            row = [key, group_of(key), str(len(tv))]
            row.extend(values[key].get(lab, "") for lab in tier_labels)
            fh.write("\t".join(row) + "\n")

    gpu_tab = config_dir / "GpuSwitchOptionTab.tab"
    gpu_rows = 0
    gpu_cols = 0
    if gpu_tab.exists():
        with gpu_tab.open("r", encoding="gb18030", errors="replace") as fh:
            lines = [ln.rstrip("\r\n") for ln in fh if ln.strip()]
        if lines:
            gpu_cols = len(lines[0].split("\t"))
            gpu_rows = max(0, len(lines) - 2)
        (out_dir / "gpu_switch_summary.tsv").write_text(
            "rows\t%d\ncolumns\t%d\nheader\t%s\n" % (gpu_rows, gpu_cols, lines[0] if lines else ""),
            encoding="utf-8",
        )

    print("sources:")
    for label, name, count, dup in per_file:
        print("  %-22s %-28s %4d keys (dups %d)" % (label, name, count, dup))
    print("unique keys (all sources): %d" % len(values))
    print("unique keys (tier files):  %d" % len({k for k in values if any(
        lab in values[k] for lab in tier_labels)}))
    print("varying across %d tiers:   %d" % (len(tier_labels), len(varying)))
    print("wrote %s (%d rows)" % (matrix_path, len(values)))
    print("wrote %s (%d rows)" % (varying_path, len(varying)))
    if gpu_tab.exists():
        print("gpu switch tab: %d data rows, %d columns" % (gpu_rows, gpu_cols))

    ok = len(values) >= 200 and len(varying) >= 10 and len(tier_labels) == 9
    print("SELFTEST %s" % ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
