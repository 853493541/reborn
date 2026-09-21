#!/usr/bin/env python3
"""Headless stick-figure proof frames for mina f1_2227 and optional MIN2 walk/run."""
from __future__ import annotations

import sys
import traceback
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D  # noqa: F401

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from mina import load_mina, stick_edges  # noqa: E402

PROOF = ROOT / "proof"
SAMPLES = ROOT / "samples"
STATUS_LINES: list[str] = []


def _render_stick(
    positions,
    edges,
    *,
    out: Path,
    title: str,
    bone_count: int,
    frame_index: int,
):
    xs = [p[0] for p in positions]
    ys = [p[1] for p in positions]
    zs = [p[2] for p in positions]

    fig = plt.figure(figsize=(8, 8), dpi=120)
    ax = fig.add_subplot(111, projection="3d")
    ax.set_facecolor("#1a1a1a")
    fig.patch.set_facecolor("#111111")
    ax.scatter(xs, ys, zs, c="#4FC3F7", s=22, depthshade=True)
    for pi, ci in edges:
        ax.plot(
            [positions[pi][0], positions[ci][0]],
            [positions[pi][1], positions[ci][1]],
            [positions[pi][2], positions[ci][2]],
            color="#B0B0B0",
            linewidth=1.4,
        )
    allv = xs + ys + zs
    if allv:
        lo, hi = min(allv), max(allv)
        pad = (hi - lo) * 0.12 + 1.0
        ax.set_xlim(lo - pad, hi + pad)
        ax.set_ylim(lo - pad, hi + pad)
        ax.set_zlim(lo - pad, hi + pad)
    ax.set_xticks([])
    ax.set_yticks([])
    ax.set_zticks([])
    ax.xaxis.pane.fill = False
    ax.yaxis.pane.fill = False
    ax.zaxis.pane.fill = False
    ax.set_title(
        f"{title}\nbones={bone_count}  frame={frame_index}",
        color="white",
        fontsize=11,
        pad=12,
    )
    fig.tight_layout()
    out.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out, facecolor=fig.get_facecolor(), bbox_inches="tight")
    plt.close(fig)
    print(f"WROTE {out} ({out.stat().st_size} bytes)")


def render_mina():
    ani = SAMPLES / "f1_2227_body_hd.mesh.ani"
    clip = load_mina(ani)
    edges = stick_edges(clip.bone_names)
    fc = clip.frame_count
    frames = {
        "f00": 0,
        "fmid": fc // 2,
        "flast": fc - 1,
    }
    STATUS_LINES.append(
        f"MINA: loaded {ani.name} bones={clip.bone_count} frames={fc} edges={len(edges)}"
    )
    outs = []
    for tag, fi in frames.items():
        out = PROOF / f"mina_f1_2227_body_{tag}.png"
        _render_stick(
            clip.positions_at(fi),
            edges,
            out=out,
            title=f"MINA stick · f1_2227_body_hd · {tag}",
            bone_count=clip.bone_count,
            frame_index=fi,
        )
        outs.append(out)
    return outs


def render_min2():
    try:
        from min2 import load_min2_stick
    except Exception as e:
        STATUS_LINES.append(f"MIN2: load_min2_stick import failed: {e}")
        return []

    player_dir = SAMPLES / "player"
    anis = sorted(player_dir.glob("*.ani")) if player_dir.is_dir() else []
    if not anis:
        STATUS_LINES.append("MIN2: no samples/player/*.ani found — skip")
        return []

    outs = []
    for ani in anis:
        name = ani.stem
        # map Chinese names to walk/run tags when possible
        lower = name.lower()
        if "行" in name or "walk" in lower:
            tag = "walk"
        elif "奔" in name or "run" in lower:
            tag = "run"
        else:
            tag = name.replace(" ", "_")[:24]
        try:
            clip = load_min2_stick(ani)
            edges = stick_edges(clip.bone_names)
            fi = clip.frame_count // 2
            out = PROOF / f"min2_{tag}_stick.png"
            _render_stick(
                clip.positions_at(fi),
                edges,
                out=out,
                title=f"MIN2 stick · {ani.name} · mid",
                bone_count=clip.bone_count,
                frame_index=fi,
            )
            STATUS_LINES.append(
                f"MIN2: OK {ani.name} bones={clip.bone_count} frames={clip.frame_count} -> {out.name}"
            )
            outs.append(out)
        except Exception as e:
            STATUS_LINES.append(f"MIN2: FAIL {ani.name}: {e}")
            traceback.print_exc()
    return outs


def copy_archives():
    candidates = [
        SAMPLES / "b1_stick_f10.png",
        SAMPLES / "thor_b1_stick_f10.png",
        # also accept workspace-level interim if samples copies absent
        Path("/workspace/proof/b1_stick_f10.png"),
        Path("/workspace/proof/thor_b1_stick_f10.png"),
    ]
    copied = []
    for src in candidates:
        if not src.is_file():
            continue
        dst = PROOF / src.name
        if src.resolve() == dst.resolve():
            STATUS_LINES.append(f"ARCHIVE: already in proof/ {dst.name}")
            copied.append(dst)
            continue
        dst.write_bytes(src.read_bytes())
        STATUS_LINES.append(f"ARCHIVE: copied {src} -> {dst}")
        copied.append(dst)
    if not any(n.name.startswith("b1_") or n.name.startswith("thor_") for n in copied):
        STATUS_LINES.append(
            "ARCHIVE: samples/b1_stick_f10.png and samples/thor_b1_stick_f10.png NOT present under samples/"
        )
    return copied


def main():
    PROOF.mkdir(parents=True, exist_ok=True)
    mina_outs = render_mina()
    min2_outs = render_min2()
    arch = copy_archives()

    status = PROOF / "STATUS.txt"
    lines = [
        "JX3 Ani Player proof render status",
        "=================================",
        "",
        *STATUS_LINES,
        "",
        f"MINA proof frames: {len(mina_outs)}",
        f"MIN2 stick proof possible: {'YES' if min2_outs else 'NO'}",
        f"MIN2 frames written this run: {len(min2_outs)}",
        f"Archived interim frames: {len(arch)}",
        "",
        "Outputs:",
    ]
    for p in mina_outs + min2_outs + arch:
        lines.append(f"  {p.resolve()}")
    status.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(status.read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
