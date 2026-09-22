"""Sanity check the native PSS JSON output."""
from __future__ import annotations

import json
import sys
from pathlib import Path

for raw in sys.argv[1:]:
    payload = json.loads(Path(raw).read_text(encoding="utf-8"))
    lines = [f"FILE {payload['path']} v{payload['version']} blocks={len(payload['blocks'])} "
             f"emitters={len(payload['emitters'])}"]
    lines.append(f"  resources: textures={len(payload['resources']['textures'])} "
                 f"materials={len(payload['resources']['materials'])} "
                 f"meshes={len(payload['resources']['meshes'])}")
    for em in payload["emitters"]:
        res = em["resources"]
        tex = ", ".join(t["path"].rsplit("/", 1)[-1] for t in res["textures"][:3])
        lines.append(
            f"  [{em['index']:2d}] {em['name']!r} kind={em['kind']} "
            f"dur={em['timing']['duration_ms']} delay={em['timing']['delay_ms']} "
            f"face={em['face']!r} motion={em['motion']!r} blend={em['blend']!r} "
            f"shape={em['shape']!r} ptype={em['particle_type']!r}"
        )
        lines.append(f"        material={res['material']}")
        if res["mesh"]:
            lines.append(f"        mesh={res['mesh']}")
        lines.append(f"        textures=[{tex}]")
        lines.append(f"        modules={[m['type'] for m in em['modules']]}")
    out = Path("_pss_check.txt")
    out.write_text("\n".join(lines), encoding="utf-8")
    print(f"wrote {out} lines={len(lines)}")
