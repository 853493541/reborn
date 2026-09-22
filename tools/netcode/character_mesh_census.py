#!/usr/bin/env python3
"""Census of HD character mesh sizes (bind pose, engine units).

Prints height (Y extent) for every .mesh under a directory so the engine unit
can be calibrated against real character heights.

Usage: python tools/netcode/character_mesh_census.py DIR [--json OUT]
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from mesh import load_mesh  # noqa: E402


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("root", type=Path)
    ap.add_argument("--json", type=Path)
    args = ap.parse_args(argv)

    rows = []
    for path in sorted(args.root.rglob("*.mesh")):
        try:
            mesh = load_mesh(path)
        except Exception as exc:  # noqa: BLE001
            rows.append({"file": str(path), "error": str(exc)})
            continue
        v = mesh.positions
        mn = v.min(axis=0)
        mx = v.max(axis=0)
        rows.append({
            "file": path.name,
            "dir": path.parent.name,
            "height": round(float(mx[1] - mn[1]), 2),
            "ymin": round(float(mn[1]), 2),
            "ymax": round(float(mx[1]), 2),
            "x": round(float(mx[0] - mn[0]), 2),
            "z": round(float(mx[2] - mn[2]), 2),
            "verts": mesh.vertex_count,
        })

    rows.sort(key=lambda r: -r.get("height", -1))
    print(f"{'mesh':<44}{'height':>8}{'ymin':>8}{'ymax':>8}{'verts':>8}")
    for r in rows:
        if "error" in r:
            print(f"{r['file']:<44}  ERROR {r['error'][:40]}")
            continue
        print(f"{r['file']:<44}{r['height']:>8}{r['ymin']:>8}{r['ymax']:>8}{r['verts']:>8}")

    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(rows, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"-> {args.json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
