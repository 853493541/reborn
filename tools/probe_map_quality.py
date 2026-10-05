#!/usr/bin/env python3
"""Probe which map quality tiers actually ship in the client PakV4.

For each BR map, probes the tiers declared in its `.jsonmap` (`bd/bddnc/mb/low`)
plus HD-root scene-data witnesses, using the official extractor; prints
HITS/MISSES. Result doc: `docs/engine_host/MAP_QUALITY_TIERS.md`
(2026-10-04: bd + low ship on all 5 maps, bddnc/mb ship nothing).

Read-only on the install; extraction goes to a temp work dir.

Usage:
  .venv\\Scripts\\python.exe tools\\probe_map_quality.py
"""
from __future__ import annotations

import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools" / "sandbox"))

from build_sandbox import run_pakv4  # noqa: E402

CLIENT = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd")
MAPS = ["龙门寻宝", "龙门寻宝_夜晚", "海岛绝境", "白龙绝境", "天原绝境"]
TIERS = ["bd", "bddnc", "mb", "low"]


def candidates() -> list[str]:
    ents = []
    for m in MAPS:
        b = "data/source/maps/%s" % m
        for q in TIERS:
            ents.append("%s/%s/environment.json" % (b, q))
            ents.append("%s/%s/playerEnvironment.json" % (b, q))
            ents.append("%s/%s/%s.rcidx" % (b, q, m))
            ents.append("%s/%s/env_probe/skybox_s.dds" % (b, q))
        # scene-data witnesses: HD root vs tier dirs
        ents.append("%s/landscape/heightmap/%s_002_002.r32" % (b, m))
        for q in ("bd", "mb", "low"):
            ents.append("%s/%s/landscape/heightmap/%s_002_002.r32" % (b, q, m))
    return ents


def main() -> int:
    work = Path(tempfile.gettempdir()) / "reborn_map_quality"
    work.mkdir(parents=True, exist_ok=True)
    ents = candidates()
    found = run_pakv4(ents, work, CLIENT / "bin64" / "PakV4SfxExtract.exe")
    for e in ents:
        if e in found:
            print("HIT  %-95s %d B" % (e, len(found[e])))
        else:
            print("MISS " + e)
    print("hits %d of %d" % (len(found), len(ents)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
