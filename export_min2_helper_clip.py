# -*- coding: utf-8 -*-
"""Export MIN2 parent-local tracks + hierarchy for helper-skel → SkeletonUtils.retargetClip."""
from __future__ import annotations

import json
import math
from pathlib import Path
from typing import List, Optional, Tuple

from min2 import load_min2_stick

ROOT = Path(__file__).resolve().parent
OUT_DIR = ROOT / "web" / "runtime"


def _f(x: float) -> float:
    x = float(x)
    return 0.0 if (math.isnan(x) or math.isinf(x)) else x


def _qflip(prev, cur):
    if prev is None:
        return cur
    if prev[0] * cur[0] + prev[1] * cur[1] + prev[2] * cur[2] + prev[3] * cur[3] < 0:
        return (-cur[0], -cur[1], -cur[2], -cur[3])
    return cur


def _parents_for(names: List[str]) -> List[Optional[str]]:
    try:
        from mina import _PARENT_RULES
    except ImportError:
        _PARENT_RULES = []
    lower = [n.lower().strip() for n in names]
    index = {n: i for i, n in enumerate(lower)}
    parent_idx = [-1] * len(names)
    for child, par in _PARENT_RULES:
        if child in index and par in index:
            parent_idx[index[child]] = index[par]
    # fingers → hand (same as mina.stick_edges)
    for side in ("l", "r"):
        hand = index.get(f"bip01 {side} hand")
        if hand is None:
            continue
        for i, n in enumerate(lower):
            if n.startswith(f"bip01 {side} finger"):
                parent_idx[i] = hand
    out: List[Optional[str]] = []
    for i, p in enumerate(parent_idx):
        out.append(names[p] if p >= 0 else None)
    return out


def export_helper(ani_path: Path, out_path: Path, *, name: str) -> dict:
    clip = load_min2_stick(ani_path)
    fps = float(clip.fps or 30.0)
    fc = int(clip.frame_count)
    duration = (fc - 1) / fps if fc > 1 else 0.0
    times = [i / fps for i in range(fc)]
    parents = _parents_for(list(clip.bone_names))

    hierarchy = []
    tracks = []
    for bi, bname in enumerate(clip.bone_names):
        hierarchy.append({"name": bname, "parent": parents[bi]})
        pvals: List[float] = []
        qvals: List[float] = []
        prev_q = None
        for fi in range(fc):
            if clip._local_positions:
                px, py, pz = clip._local_positions[bi][fi]
            else:
                px, py, pz = clip.positions_at(fi)[bi] if bi == 0 else (0.0, 0.0, 0.0)
            if clip._local_quats:
                qx, qy, qz, qw = clip._local_quats[bi][fi]
            else:
                qx, qy, qz, qw = 0.0, 0.0, 0.0, 1.0
            n = math.sqrt(_f(qx) ** 2 + _f(qy) ** 2 + _f(qz) ** 2 + _f(qw) ** 2) or 1.0
            q = (_f(qx) / n, _f(qy) / n, _f(qz) / n, _f(qw) / n)
            q = _qflip(prev_q, q)
            prev_q = q
            pvals.extend((_f(px), _f(py), _f(pz)))
            qvals.extend(q)
        tracks.append(
            {"type": "vector", "name": f"{bname}.position", "times": times, "values": pvals}
        )
        tracks.append(
            {
                "type": "quaternion",
                "name": f"{bname}.quaternion",
                "times": times,
                "values": qvals,
            }
        )

    # Sample parent graph for diagnosis (core bones)
    core = (
        "bip01",
        "bip01 pelvis",
        "bip01 spine",
        "bip01 spine1",
        "bip01 spine2",
        "bip01 neck",
        "bip01 head",
        "bip01 l thigh",
        "bip01 r thigh",
        "bip01 l upperarm",
        "bip01 r upperarm",
        "bone_spine",
        "bone_l_thigh",
        "bone_r_thigh",
    )
    low_map = {h["name"].lower().strip(): h for h in hierarchy}
    diag_parents = {
        c: (low_map[c]["parent"] if c in low_map else None) for c in core
    }

    payload = {
        "name": name,
        "space": "min2HelperRetarget",
        "fps": fps,
        "duration": duration,
        "frame_count": fc,
        "quat_only": False,
        "hierarchy": hierarchy,
        "tracks": tracks,
        "source_ani": str(ani_path).replace("\\", "/"),
        "driver": "min2HelperSkeletonUtilsRetargetClip",
        "diag_parents": diag_parents,
        "bone_count": len(hierarchy),
        "note": "parent-local MIN2; positions_at/quats_at are WORLD — this export uses local_*",
    }
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(
        json.dumps(payload, allow_nan=False, separators=(",", ":")), encoding="utf-8"
    )
    return {
        "out": str(out_path),
        "bytes": out_path.stat().st_size,
        "bones": len(hierarchy),
        "tracks": len(tracks),
        "frames": fc,
        "duration": duration,
        "space": "min2HelperRetarget",
        "diag_parents": diag_parents,
    }


def main() -> None:
    charge = ROOT / "samples/player/moves/f1s07cj重剑技能15蓄力_奇穴.ani"
    cast = ROOT / "samples/player/moves/fenglaiwushan/f1s07cj重剑技能15.ani"
    for ani, out_name, clip_name in (
        (charge, "clip_flws_charge.json", "风来吴山·蓄力"),
        (cast, "clip_flws_cast.json", "风来吴山·释放"),
    ):
        if not ani.is_file():
            print("MISSING", ani)
            continue
        print("EXPORTED", export_helper(ani, OUT_DIR / out_name, name=clip_name))
    print("DONE_HELPER_EXPORT")


if __name__ == "__main__":
    main()
