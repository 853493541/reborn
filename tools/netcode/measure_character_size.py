#!/usr/bin/env python3
"""Measure character size in engine units from a JX3 HD .mesh (bind pose).

Reports:
  - mesh bounding box (units) and height
  - per-bone bind translations (pelvis / head heights) when available
  - height in cm and in 尺 (1 尺 = 64 units) under the unit hypothesis

Usage:
  python tools/netcode/measure_character_size.py FILE [FILE...] [--json OUT]
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from mesh import load_mesh  # noqa: E402

INTERESTING = ("bip01", "head", "neck", "spine", "pelvis", "foot", "ankle")


def bone_bind_translations(mesh) -> dict[str, list[float]]:
    out: dict[str, list[float]] = {}
    for bone in mesh.bones:
        if not bone.matrices:
            continue
        mat = bone.matrices[-1]
        try:
            t = [float(mat[0][3]), float(mat[1][3]), float(mat[2][3])]
        except Exception:  # noqa: BLE001
            continue
        out[bone.name] = [round(v, 3) for v in t]
    return out


def measure(path: Path) -> dict:
    mesh = load_mesh(path)
    v = mesh.positions
    mn = v.min(axis=0)
    mx = v.max(axis=0)
    size = mx - mn
    binds = bone_bind_translations(mesh)
    interesting = {k: val for k, val in binds.items() if any(t in k.lower() for t in INTERESTING)}
    return {
        "file": path.name,
        "vertices": mesh.vertex_count,
        "faces": mesh.face_count,
        "bbox_min": [round(float(x), 3) for x in mn],
        "bbox_max": [round(float(x), 3) for x in mx],
        "size_units": [round(float(x), 3) for x in size],
        "height_units": round(float(size[1]), 3),
        "height_cm_if_1unit_1cm": round(float(size[1]), 2),
        "height_chi": round(float(size[1]) / 64.0, 3),
        "bone_binds": interesting,
    }


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("files", nargs="+", type=Path)
    ap.add_argument("--json", type=Path)
    args = ap.parse_args(argv)

    results = []
    for path in args.files:
        if not path.is_file():
            print(f"missing: {path}", file=sys.stderr)
            continue
        res = measure(path)
        results.append(res)
        print(f"== {res['file']}  verts={res['vertices']} bones with bind data")
        print(f"   bbox min {res['bbox_min']} max {res['bbox_max']}")
        print(f"   size units {res['size_units']}  -> height {res['height_units']} units "
              f"= {res['height_cm_if_1unit_1cm']} cm = {res['height_chi']} 尺 (at 64 u/尺)")
        for name, t in list(res["bone_binds"].items())[:12]:
            print(f"      {name:<28} bind t = {t}")

    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(results, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"-> {args.json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
