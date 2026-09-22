"""Summarize a parsed PSS JSON (written by pss.py)."""
from __future__ import annotations

import json
import sys
from pathlib import Path


def main() -> None:
    for raw in sys.argv[1:]:
        path = Path(raw)
        data = json.loads(path.read_text(encoding="utf-8"))
        print("=" * 100)
        print(f"{path.name}: version={data['version']} blocks={len(data['blocks'])} emitters={len(data['emitters'])}")
        res = data["resources"]
        print(f"  resources: {len(res['textures'])} textures, {len(res['meshes'])} meshes, {len(res['materials'])} materials")
        for mesh in res["meshes"]:
            print("    mesh:", mesh)
        for em in data["emitters"]:
            r = em["resources"]
            tex = ", ".join(t["path"].rsplit("/", 1)[-1] for t in r["textures"])
            print(
                f"  [{em['index']:2d}] {em['name']!r} kind={em['kind']} shape={em['shape']!r} "
                f"type={em['particle_type']!r} dur={em['timing']['duration_ms']} "
                f"blend={em['blend']} face={em['face']}"
            )
            if r["material"]:
                print(f"       material: {r['material']}")
            if r["mesh"]:
                print(f"       mesh: {r['mesh']}")
            if tex:
                print(f"       tex: {tex}")


if __name__ == "__main__":
    main()
