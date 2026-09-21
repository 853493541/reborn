# -*- coding: utf-8 -*-
"""MIN2 world poses → FBX-parent-local AnimationClip JSON (Mixer-safe)."""
from __future__ import annotations

import json
import math
from pathlib import Path

from min2 import load_min2_stick

ROOT = Path(__file__).resolve().parent
SKEL = ROOT / "web" / "runtime" / "fbx_skel_bind.json"
OUT_DIR = ROOT / "web" / "runtime"


def _qnorm(q):
    x, y, z, w = q
    n = math.sqrt(x * x + y * y + z * z + w * w) or 1.0
    return (x / n, y / n, z / n, w / n)


def _qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    )


def _qconj(q):
    x, y, z, w = q
    return (-x, -y, -z, w)


def _qrot(q, v):
    qv = (v[0], v[1], v[2], 0.0)
    return _qmul(_qmul(q, qv), _qconj(q))[:3]


def _mat_from_pq(p, q, s=(1, 1, 1)):
    x, y, z, w = _qnorm(q)
    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z
    sx, sy, sz = s
    return [
        [(1 - 2 * (yy + zz)) * sx, 2 * (xy - wz) * sy, 2 * (xz + wy) * sz, p[0]],
        [2 * (xy + wz) * sx, (1 - 2 * (xx + zz)) * sy, 2 * (yz - wx) * sz, p[1]],
        [2 * (xz - wy) * sx, 2 * (yz + wx) * sy, (1 - 2 * (xx + yy)) * sz, p[2]],
        [0.0, 0.0, 0.0, 1.0],
    ]


def _matmul(a, b):
    out = [[0.0] * 4 for _ in range(4)]
    for i in range(4):
        for j in range(4):
            out[i][j] = sum(a[i][k] * b[k][j] for k in range(4))
    return out


def _mat_inv_affine(m):
    # Inverse of TRS assuming uniform-ish affine (R|t with scale in R columns)
    r = [[m[i][j] for j in range(3)] for i in range(3)]
    # For orthogonal+scale, use adjugate / det of 3x3
    def det3(a):
        return (
            a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1])
            - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
            + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0])
        )

    d = det3(r) or 1.0
    inv_r = [
        [
            (r[1][1] * r[2][2] - r[1][2] * r[2][1]) / d,
            (r[0][2] * r[2][1] - r[0][1] * r[2][2]) / d,
            (r[0][1] * r[1][2] - r[0][2] * r[1][1]) / d,
        ],
        [
            (r[1][2] * r[2][0] - r[1][0] * r[2][2]) / d,
            (r[0][0] * r[2][2] - r[0][2] * r[2][0]) / d,
            (r[0][2] * r[1][0] - r[0][0] * r[1][2]) / d,
        ],
        [
            (r[1][0] * r[2][1] - r[1][1] * r[2][0]) / d,
            (r[0][1] * r[2][0] - r[0][0] * r[2][1]) / d,
            (r[0][0] * r[1][1] - r[0][1] * r[1][0]) / d,
        ],
    ]
    t = [m[0][3], m[1][3], m[2][3]]
    it = [
        -(inv_r[0][0] * t[0] + inv_r[0][1] * t[1] + inv_r[0][2] * t[2]),
        -(inv_r[1][0] * t[0] + inv_r[1][1] * t[1] + inv_r[1][2] * t[2]),
        -(inv_r[2][0] * t[0] + inv_r[2][1] * t[1] + inv_r[2][2] * t[2]),
    ]
    return [
        [inv_r[0][0], inv_r[0][1], inv_r[0][2], it[0]],
        [inv_r[1][0], inv_r[1][1], inv_r[1][2], it[1]],
        [inv_r[2][0], inv_r[2][1], inv_r[2][2], it[2]],
        [0.0, 0.0, 0.0, 1.0],
    ]


def _decompose(m):
    # Extract pos + quat from 4x4 (ignore scale / assume positive)
    px, py, pz = m[0][3], m[1][3], m[2][3]

    def nrm(v):
        L = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]) or 1.0
        return [v[0] / L, v[1] / L, v[2] / L]

    def ncol(i):
        v = [m[0][i], m[1][i], m[2][i]]
        return nrm(v)

    x, y, z = ncol(0), ncol(1), ncol(2)
    # ensure right-handed
    # quat from rotation matrix (columns x,y,z)
    trace = x[0] + y[1] + z[2]
    if trace > 0:
        s = 0.5 / math.sqrt(trace + 1.0)
        w = 0.25 / s
        qx = (y[2] - z[1]) * s
        qy = (z[0] - x[2]) * s
        qz = (x[1] - y[0]) * s
    elif x[0] > y[1] and x[0] > z[2]:
        s = 2.0 * math.sqrt(1.0 + x[0] - y[1] - z[2])
        w = (y[2] - z[1]) / s
        qx = 0.25 * s
        qy = (y[0] + x[1]) / s
        qz = (z[0] + x[2]) / s
    elif y[1] > z[2]:
        s = 2.0 * math.sqrt(1.0 + y[1] - x[0] - z[2])
        w = (z[0] - x[2]) / s
        qx = (y[0] + x[1]) / s
        qy = 0.25 * s
        qz = (z[1] + y[2]) / s
    else:
        s = 2.0 * math.sqrt(1.0 + z[2] - x[0] - y[1])
        w = (x[1] - y[0]) / s
        qx = (z[0] + x[2]) / s
        qy = (z[1] + y[2]) / s
        qz = 0.25 * s
    return (px, py, pz), _qnorm((qx, qy, qz, w))


def _flip(prev, cur):
    if prev[0] * cur[0] + prev[1] * cur[1] + prev[2] * cur[2] + prev[3] * cur[3] < 0:
        return (-cur[0], -cur[1], -cur[2], -cur[3])
    return cur


def _topo(bones: list[dict]) -> list[dict]:
    by = {b["name"]: b for b in bones}
    low = {b["name"].lower(): b for b in bones}
    order = []
    seen = set()

    def visit(name):
        if not name or name in seen:
            return
        b = by.get(name) or low.get(name.lower())
        if not b:
            return
        if b.get("parent"):
            visit(b["parent"])
        if b["name"] not in seen:
            seen.add(b["name"])
            order.append(b)

    for b in bones:
        visit(b["name"])
    return order


def export_retarget(ani_path: Path, out_path: Path, *, name: str) -> dict:
    skel = json.loads(SKEL.read_text(encoding="utf-8"))
    bones = skel["bones"]
    order = _topo(bones)
    bind_local = {
        b["name"]: {
            "pos": tuple(b["pos"]),
            "quat": tuple(b["quat"]),
            "parent": b.get("parent"),
        }
        for b in bones
    }
    clip = load_min2_stick(ani_path)
    min2_idx = {n.lower(): i for i, n in enumerate(clip.bone_names)}
    fps = float(clip.fps or 30.0)
    fc = int(clip.frame_count)
    times = [i / fps for i in range(fc)]
    duration = (fc - 1) / fps if fc > 1 else 0.0

    # Collect per-bone series
    series_pos = {b["name"]: [] for b in order}
    series_quat = {b["name"]: [] for b in order}
    matched = 0

    for fi in range(fc):
        world_pos = clip.positions_at(fi)
        world_quat = clip.quats_at(fi)
        # MIN2 world matrices by lowercase name
        min2_world = {}
        for n, i in min2_idx.items():
            min2_world[n] = _mat_from_pq(world_pos[i], world_quat[i])

        world_acc = {}
        for b in order:
            bname = b["name"]
            parent = b.get("parent")
            key = bname.lower()
            if key in min2_world:
                W = min2_world[key]
                if fi == 0:
                    matched += 1
            else:
                # bind local → world via parent
                bp = bind_local[bname]["pos"]
                bq = bind_local[bname]["quat"]
                Lbind = _mat_from_pq(bp, bq)
                if parent and parent in world_acc:
                    W = _matmul(world_acc[parent], Lbind)
                else:
                    W = Lbind
            if parent and parent in world_acc:
                L = _matmul(_mat_inv_affine(world_acc[parent]), W)
            else:
                L = W
            pos, quat = _decompose(L)
            world_acc[bname] = W
            series_pos[bname].append(pos)
            series_quat[bname].append(quat)

    tracks = []
    used = 0
    for b in order:
        bname = b["name"]
        if bname.lower() not in min2_idx:
            continue  # leave bind; don't override unmatched
        used += 1
        pvals = []
        qvals = []
        prev = None
        for fi in range(fc):
            px, py, pz = series_pos[bname][fi]
            pvals.extend((float(px), float(py), float(pz)))
            q = series_quat[bname][fi]
            if prev is not None:
                q = _flip(prev, q)
            prev = q
            qvals.extend(q)
        tracks.append({"type": "vector", "name": f"{bname}.position", "times": times, "values": pvals})
        tracks.append({"type": "quaternion", "name": f"{bname}.quaternion", "times": times, "values": qvals})

    payload = {
        "name": name,
        "fps": fps,
        "duration": duration,
        "frame_count": fc,
        "quat_only": False,
        "tracks": tracks,
        "source_ani": str(ani_path).replace("\\", "/"),
        "driver": "min2RetargetFbxLocal",
        "matched_bones": used,
    }
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(payload, separators=(",", ":")), encoding="utf-8")
    return {
        "out": str(out_path),
        "bytes": out_path.stat().st_size,
        "tracks": len(tracks),
        "matched_bones": used,
        "frames": fc,
        "duration": duration,
    }


def main() -> None:
    if not SKEL.is_file():
        raise SystemExit(f"missing skel dump {SKEL}")
    charge = ROOT / "samples/player/moves/f1s07cj重剑技能15蓄力_奇穴.ani"
    cast = ROOT / "samples/player/moves/fenglaiwushan/f1s07cj重剑技能15.ani"
    for ani, out_name, clip_name in (
        (charge, "clip_flws_charge.json", "风来吴山·蓄力"),
        (cast, "clip_flws_cast.json", "风来吴山·释放"),
    ):
        meta = export_retarget(ani, OUT_DIR / out_name, name=clip_name)
        print("EXPORTED", meta)
    print("DONE_RETARGET")


if __name__ == "__main__":
    main()
