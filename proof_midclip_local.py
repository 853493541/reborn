#!/usr/bin/env python3
"""P0 mid-clip proof: 风来吴山·蓄力 local matrices → humanoid PNG.

Run on Andy:
  cd "C:\\Users\\Zhibin Ren\\jx3-ani-player"
  .venv\\Scripts\\python.exe proof_midclip_local.py
"""
from __future__ import annotations

import sys
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))

OUT_DIR = ROOT / "proof" / "compare"
OUT_DIR.mkdir(parents=True, exist_ok=True)
STAMP = date.today().strftime("%Y%m%d")
OUT_PNG = OUT_DIR / f"aniplayer_flws_midclip_human_{STAMP}.png"


def _find_flws_clip():
    try:
        from resolve_playable import resolve_playable_path, load_playable_clip
    except Exception:
        resolve_playable_path = None
        load_playable_clip = None

    # Catalog / resolve path first
    candidates = []
    cat = ROOT / "samples" / "player" / "catalog"
    if cat.is_dir():
        for p in cat.rglob("*.json"):
            try:
                text = p.read_text(encoding="utf-8", errors="ignore")
            except Exception:
                continue
            if "蓄力" in text or "风来吴山" in text:
                candidates.append(p)

    moves = ROOT / "samples" / "player" / "moves"
    ani_hits = []
    if moves.is_dir():
        for p in moves.rglob("*.ani"):
            n = p.name
            if "蓄力" in n or "风来" in n or "fenglai" in n.lower() or "flws" in n.lower():
                ani_hits.append(p)
        if not ani_hits:
            # broader: skill15 charge-ish
            for p in moves.rglob("*.ani"):
                if "15" in p.name and "蓄力" in p.name:
                    ani_hits.append(p)
        # Prefer 蓄力 / charge clips first
        ani_hits.sort(key=lambda p: (0 if "蓄力" in p.name else 1, p.name))

    clip = None
    path = None
    err = []

    if resolve_playable_path is not None:
        # Try row-like dicts from common catalog shapes
        for hint in ("蓄力", "风来吴山"):
            try:
                path = resolve_playable_path({"name": hint, "anim_name": hint})
                if path and Path(path).is_file():
                    from min2 import load_min2_stick
                    clip = load_min2_stick(path)
                    break
            except Exception as e:
                err.append(f"resolve({hint}): {e}")

    if clip is None:
        from min2 import load_min2_stick
        for p in ani_hits:
            try:
                clip = load_min2_stick(p)
                path = p
                break
            except Exception as e:
                err.append(f"load({p}): {e}")

    if clip is None:
        raise SystemExit(
            "BLOCKER: no 风来吴山·蓄力 .ani found under samples/player/.\n"
            + "\n".join(err[:8])
            + "\nClint must stage playable MIN2 under samples/player/moves (or catalog playable_path)."
        )
    return Path(path), clip


def main():
    path, clip = _find_flws_clip()
    assert hasattr(clip, "local_matrices_at"), "local_matrices_at missing — sync min2.py"
    mid = max(0, int(clip.frame_count * 0.45))
    local = clip.local_matrices_at(mid)
    world = clip.matrices_at(mid)

    # Sanity: local translations human-scale (bip* is the character gate)
    names = list(clip.bone_names)
    max_local_t = 0.0
    max_bip_local_t = 0.0
    max_world_t = 0.0
    for i, m in enumerate(local):
        mt = max(abs(m[3]), abs(m[7]), abs(m[11]))
        max_local_t = max(max_local_t, mt)
        if names[i].lower().startswith("bip"):
            max_bip_local_t = max(max_bip_local_t, mt)
    for m in world:
        max_world_t = max(max_world_t, abs(m[3]), abs(m[7]), abs(m[11]))
    print(f"clip={path}")
    print(f"bones={clip.bone_count} frames={clip.frame_count} mid={mid}")
    print(f"max |t| local={max_local_t:.2f} bip_local={max_bip_local_t:.2f} world={max_world_t:.2f}")
    if max_bip_local_t > 500:
        raise SystemExit(f"FAIL: bip local translations still huge ({max_bip_local_t})")
    if max_local_t > 5000:
        raise SystemExit(f"FAIL: local translations still huge after sanitize ({max_local_t})")

    # Transport sample must carry local_matrices
    from transport import PlaybackClock
    clock = PlaybackClock(fps_fallback=float(clip.fps or 33.0))
    clock.set_clip(clip)
    clock.seek(mid)
    sample = clock.sample(with_pose=True)
    assert sample.local_matrices is not None, "transport Sample.local_matrices empty"
    assert sample.matrices is not None, "world matrices should still populate for LBS"

    # Matplotlib stick proof (native path — Steve default)
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d import Axes3D  # noqa: F401

    pos = clip.positions_at(mid)
    # Core body only — fingers often sit at bind origin and fake a second cluster.
    _CORE = {
        "bip01", "bip01 pelvis", "bip01 spine", "bip01 spine1", "bip01 spine2",
        "bip01 neck", "bip01 neck1", "bip01 head",
        "bip01 l clavicle", "bip01 r clavicle",
        "bip01 l upperarm", "bip01 r upperarm",
        "bip01 l forearm", "bip01 r forearm",
        "bip01 l foretwist", "bip01 r foretwist",
        "bip01 l hand", "bip01 r hand",
        "bip01 l thigh", "bip01 r thigh",
        "bip01 l calf", "bip01 r calf",
        "bip01 l foot", "bip01 r foot",
        "bip01 l toe0", "bip01 r toe0",
    }
    bip_idx = [i for i, n in enumerate(names) if n.lower().strip() in _CORE]
    if not bip_idx:
        bip_idx = [i for i, n in enumerate(names) if n.lower().startswith("bip") and "finger" not in n.lower()]
    xs = [pos[i][0] for i in bip_idx]
    ys = [pos[i][1] for i in bip_idx]
    zs = [pos[i][2] for i in bip_idx]
    remap = {old: new for new, old in enumerate(bip_idx)}

    # Build edges from mina parent rules if available (bip subset)
    edges = []
    try:
        from mina import stick_edges
        for a, b in stick_edges(clip.bone_names):
            if a not in remap or b not in remap:
                continue
            # Skip finger spokes — finger bind rests are often junk in MIN2.
            na, nb = names[a].lower(), names[b].lower()
            if "finger" in na or "finger" in nb:
                continue
            edges.append((remap[a], remap[b]))
    except Exception:
        pass

    fig = plt.figure(figsize=(8, 10), facecolor="#0b1020")
    ax = fig.add_subplot(111, projection="3d", facecolor="#0b1020")
    ax.scatter(xs, zs, ys, c="#7ec8ff", s=18, depthshade=False)
    for a, b in edges:
        ax.plot(
            [xs[a], xs[b]],
            [zs[a], zs[b]],
            [ys[a], ys[b]],
            color="#c8e7ff",
            lw=1.6,
        )
    ax.set_title(
        f"FLWS mid-clip · f={mid}/{clip.frame_count} · bip|t|_loc≤{max_bip_local_t:.0f}",
        color="white",
        fontsize=11,
    )
    ax.set_xlabel("X"); ax.set_ylabel("Z"); ax.set_zlabel("Y")
    ax.tick_params(colors="#8899aa")
    # Autoscale with equal aspect-ish
    span = max(
        max(xs) - min(xs),
        max(ys) - min(ys),
        max(zs) - min(zs),
        1.0,
    )
    cx, cy, cz = (min(xs)+max(xs))/2, (min(ys)+max(ys))/2, (min(zs)+max(zs))/2
    ax.set_xlim(cx - span/2, cx + span/2)
    ax.set_ylim(cz - span/2, cz + span/2)
    ax.set_zlim(cy - span/2, cy + span/2)
    fig.tight_layout()
    fig.savefig(OUT_PNG, dpi=140)
    plt.close(fig)
    print(f"PROOF={OUT_PNG}")
    print("PATH=matplotlib_stick")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
