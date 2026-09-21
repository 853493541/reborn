# -*- coding: utf-8 -*-
"""Export MIN2 skel .ani → THREE.AnimationClip JSON (parent-local pos+quat tracks).

No bone.decompose path — viewport plays via AnimationMixer + ?clip=.
"""
from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Iterable

from min2 import load_min2_stick

ROOT = Path(__file__).resolve().parent
OUT_DIR = ROOT / "web" / "runtime"


def _flip_quat_hemisphere(
    prev: tuple[float, float, float, float],
    cur: tuple[float, float, float, float],
) -> tuple[float, float, float, float]:
    # Keep consecutive quats in the same hemisphere for KeyframeTrack.
    if prev[0] * cur[0] + prev[1] * cur[1] + prev[2] * cur[2] + prev[3] * cur[3] < 0:
        return (-cur[0], -cur[1], -cur[2], -cur[3])
    return cur


def _norm_quat(q: tuple[float, float, float, float]) -> tuple[float, float, float, float]:
    x, y, z, w = q
    n = math.sqrt(x * x + y * y + z * z + w * w) or 1.0
    return (x / n, y / n, z / n, w / n)


def export_clip(
    ani_path: Path,
    out_path: Path,
    *,
    name: str | None = None,
    include_scale: bool = True,
    skip_substrings: Iterable[str] = (),
) -> dict:
    clip = load_min2_stick(ani_path)
    fps = float(clip.fps or 30.0)
    fc = int(clip.frame_count)
    duration = (fc - 1) / fps if fc > 1 else 0.0
    times = [i / fps for i in range(fc)]
    skip = tuple(s.lower() for s in skip_substrings)

    tracks: list[dict] = []
    for bi, bone in enumerate(clip.bone_names):
        bl = bone.lower()
        if any(s in bl for s in skip):
            continue
        # positions
        if clip._local_positions:
            pvals: list[float] = []
            for fi in range(fc):
                px, py, pz = clip._local_positions[bi][fi]
                pvals.extend((float(px), float(py), float(pz)))
            tracks.append(
                {
                    "type": "vector",
                    "name": f"{bone}.position",
                    "times": times,
                    "values": pvals,
                }
            )
        # quaternions (xyzw)
        qvals: list[float] = []
        prev_q: tuple[float, float, float, float] | None = None
        for fi in range(fc):
            q = _norm_quat(clip.local_quats_at(fi)[bi])
            if prev_q is not None:
                q = _flip_quat_hemisphere(prev_q, q)
            prev_q = q
            qvals.extend(q)
        tracks.append(
            {
                "type": "quaternion",
                "name": f"{bone}.quaternion",
                "times": times,
                "values": qvals,
            }
        )
        if include_scale:
            svals = [1.0, 1.0, 1.0] * fc
            tracks.append(
                {
                    "type": "vector",
                    "name": f"{bone}.scale",
                    "times": times,
                    "values": svals,
                }
            )

    payload = {
        "name": name or ani_path.stem,
        "fps": fps,
        "duration": duration,
        "frame_count": fc,
        "quat_only": False,
        "tracks": tracks,
        "source_ani": str(ani_path).replace("\\", "/"),
        "driver": "min2AnimationClip",
    }
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(payload, separators=(",", ":")), encoding="utf-8")
    return {
        "out": str(out_path),
        "bytes": out_path.stat().st_size,
        "tracks": len(tracks),
        "bones": clip.bone_count,
        "frames": fc,
        "duration": duration,
        "fps": fps,
    }


def main() -> None:
    charge = ROOT / "samples/player/moves/f1s07cj重剑技能15蓄力_奇穴.ani"
    cast = ROOT / "samples/player/moves/fenglaiwushan/f1s07cj重剑技能15.ani"
    metas = []
    for ani, out_name, clip_name in (
        (charge, "clip_flws_charge.json", "风来吴山·蓄力"),
        (cast, "clip_flws_cast.json", "风来吴山·释放"),
    ):
        if not ani.is_file():
            raise SystemExit(f"missing {ani}")
        meta = export_clip(ani, OUT_DIR / out_name, name=clip_name)
        print("EXPORTED", meta)
        metas.append(meta)
    print("DONE_EXPORT")


if __name__ == "__main__":
    main()
