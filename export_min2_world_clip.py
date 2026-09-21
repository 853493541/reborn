# -*- coding: utf-8 -*-
"""Export MIN2 skel .ani as WORLD-space pos+quat JSON (NaN-safe)."""
from __future__ import annotations

import json
import math
from pathlib import Path

from min2 import load_min2_stick

ROOT = Path(__file__).resolve().parent
OUT_DIR = ROOT / "web" / "runtime"


def _f(x: float) -> float:
    x = float(x)
    return 0.0 if (math.isnan(x) or math.isinf(x)) else x


def export_world(ani_path: Path, out_path: Path, *, name: str) -> dict:
    clip = load_min2_stick(ani_path)
    fps = float(clip.fps or 30.0)
    fc = int(clip.frame_count)
    duration = (fc - 1) / fps if fc > 1 else 0.0
    bones = []
    for bi, bname in enumerate(clip.bone_names):
        positions = []
        quats = []
        for fi in range(fc):
            px, py, pz = clip.positions_at(fi)[bi]
            qx, qy, qz, qw = clip.quats_at(fi)[bi]
            positions.append([_f(px), _f(py), _f(pz)])
            n = math.sqrt(_f(qx) ** 2 + _f(qy) ** 2 + _f(qz) ** 2 + _f(qw) ** 2) or 1.0
            quats.append([_f(qx) / n, _f(qy) / n, _f(qz) / n, _f(qw) / n])
        bones.append({"name": bname, "positions": positions, "quats": quats})
    payload = {
        "name": name,
        "space": "worldUuid",
        "fps": fps,
        "duration": duration,
        "frame_count": fc,
        "bones": bones,
        "source_ani": str(ani_path).replace("\\", "/"),
        "driver": "min2WorldUuidRetarget",
    }
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(payload, allow_nan=False, separators=(",", ":")), encoding="utf-8")
    return {"out": str(out_path), "bytes": out_path.stat().st_size, "bones": len(bones), "frames": fc}


def main() -> None:
    charge = ROOT / "samples/player/moves/f1s07cj重剑技能15蓄力_奇穴.ani"
    cast = ROOT / "samples/player/moves/fenglaiwushan/f1s07cj重剑技能15.ani"
    for ani, out_name, clip_name in (
        (charge, "clip_flws_charge.json", "风来吴山·蓄力"),
        (cast, "clip_flws_cast.json", "风来吴山·释放"),
    ):
        print("EXPORTED", export_world(ani, OUT_DIR / out_name, name=clip_name))
    print("DONE_WORLD_EXPORT")


if __name__ == "__main__":
    main()
