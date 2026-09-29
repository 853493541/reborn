#!/usr/bin/env python3
"""Audit map world objects against the engine's physic file/folder white/black lists.

Usage: python tools/collision/audit_physic_lists.py [--regions DIR] [--physic DIR] [--out FILE]

Reads the 龙门寻宝 sceneinfo_full JSONs (default), collects distinct actorModel
paths, and classifies each against:
  - physic_file_white / physic_file_black      (model stem match)
  - physic_folder_white / physic_folder_black  (any path component match)

Writes a report with counts, samples and the per-list hit sets.
"""
import argparse
import collections
import json
import pathlib
import sys


def read_list(path: pathlib.Path) -> list[str]:
    return [l.strip().lower() for l in path.read_text(encoding="utf-8").splitlines() if l.strip()]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--regions", default=r"C:\jx3tmp\ent_all\out\data\source\maps")
    ap.add_argument("--physic", default="proof/collision/physic/utf8")
    ap.add_argument("--out", default="proof/collision/physic/audit_lists.txt")
    args = ap.parse_args()

    phys = pathlib.Path(args.physic)
    fw = set(read_list(phys / "physic_file_white.txt"))
    fb = set(read_list(phys / "physic_file_black.txt"))
    foldw = set(read_list(phys / "physic_folder_white.txt"))
    foldb = set(read_list(phys / "physic_folder_black.txt"))

    models: dict[str, dict] = {}
    for p in pathlib.Path(args.regions).rglob("sceneinfo_full/*.json"):
        try:
            d = json.loads(p.read_text(encoding="gb18030", errors="replace"))
        except Exception:
            continue
        for uuid, o in (d.get("worldObjects") or {}).items():
            m = (o.get("comRender") or {}).get("actorModel")
            if not m:
                continue
            norm = m.replace("\\", "/").lower()
            if norm not in models:
                models[norm] = {"count": 0, "ext": pathlib.PurePosixPath(norm).suffix}
            models[norm]["count"] += 1

    combos = collections.Counter()
    samples = collections.defaultdict(list)
    white_hits, black_hits = set(), set()
    for m, info in models.items():
        parts = m.split("/")
        base = pathlib.PurePosixPath(m).stem
        folders = set(parts[:-1])
        file_w = base in fw
        file_b = base in fb
        folder_w = bool(folders & foldw)
        folder_b = bool(folders & foldb)
        if file_w:
            white_hits.add(m)
        if file_b:
            black_hits.add(m)
        key = (folder_w, folder_b, file_w, file_b)
        combos[key] += 1
        if len(samples[key]) < 6:
            samples[key].append(m)

    lines = []
    lines.append("models=%d objects=%d" % (len(models), sum(i["count"] for i in models.values())))
    lines.append("file_white=%d file_black=%d folder_white=%d folder_black=%d"
                 % (len(fw), len(fb), len(foldw), len(foldb)))
    lines.append("")
    lines.append("folder_w folder_b file_w file_b -> models")
    for key, count in combos.most_common():
        lines.append("%-8s %-8s %-6s %-6s -> %d" % (key + (count,)))
        for s in samples[key]:
            lines.append("        " + s)
    lines.append("")
    lines.append("file_white hits (%d):" % len(white_hits))
    lines.extend("  " + m for m in sorted(white_hits)[:80])
    lines.append("file_black hits (%d):" % len(black_hits))
    lines.extend("  " + m for m in sorted(black_hits)[:80])

    out = pathlib.Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("wrote", out)
    for l in lines[:12]:
        print(l)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
