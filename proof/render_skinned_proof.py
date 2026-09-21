#!/usr/bin/env python3
"""Skinned body-mesh proof frames driven by Dev2 load_min2_stick RQ matrices_at LBS."""
from __future__ import annotations

import json
import sys
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from mpl_toolkits.mplot3d.art3d import Poly3DCollection

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from mesh import bone_name_map, load_mesh, normalize_influences, skin_positions  # noqa: E402
from min2 import load_min2_stick  # noqa: E402

PROOF = ROOT / "proof"
MESH_PATH = ROOT / "samples" / "mesh" / "m2_1018_body_hd.mesh"
WALK_ANI = ROOT / "samples" / "player" / "m2b02yd行走.ani"
RUN_ANI = ROOT / "samples" / "player" / "m2b02yd奔跑.ani"


def _frame_indices(clip) -> list[int]:
    """Pick 3 stable frames; skip collapsed pelvis / runaway stick samples."""
    fc = clip.frame_count
    names = [n.lower() for n in clip.bone_names]
    try:
        pelvis = names.index("bip01 pelvis")
    except ValueError:
        pelvis = 0
    rest = clip.positions_at(0)
    rest_z = rest[pelvis][2]
    bind_mats = clip.matrices_at(0)
    good = []
    for fi in range(fc):
        p = clip.positions_at(fi)[pelvis]
        if not all(map(lambda x: x == x, p)):
            continue
        if abs(p[2] - rest_z) > 25.0:
            continue
        # Reject wrap/collapse frames (walk f18 etc.): large stick |Δt| vs bind.
        pose_mats = clip.matrices_at(fi)
        runaway = False
        for si in range(min(len(bind_mats), len(pose_mats))):
            t0 = bind_mats[si][3], bind_mats[si][7], bind_mats[si][11]
            tf = pose_mats[si][3], pose_mats[si][7], pose_mats[si][11]
            dx, dy, dz = tf[0] - t0[0], tf[1] - t0[1], tf[2] - t0[2]
            if (dx * dx + dy * dy + dz * dz) ** 0.5 > 25.0:
                runaway = True
                break
        if runaway:
            continue
        good.append(fi)
    if not good:
        good = list(range(fc))
    if len(good) <= 2:
        return good
    return [good[0], good[len(good) // 2], good[-1]]


def _draw_mesh(ax, positions: np.ndarray, faces: np.ndarray, *, alpha: float = 0.92):
    tris = positions[faces]  # (F,3,3)
    coll = Poly3DCollection(
        tris,
        linewidths=0.05,
        edgecolors=(0.15, 0.15, 0.18, 0.25),
        facecolors=(0.55, 0.72, 0.88, alpha),
    )
    ax.add_collection3d(coll)
    mins = positions.min(axis=0)
    maxs = positions.max(axis=0)
    center = (mins + maxs) * 0.5
    span = float((maxs - mins).max()) * 0.55 + 1.0
    ax.set_xlim(center[0] - span, center[0] + span)
    ax.set_ylim(center[1] - span, center[1] + span)
    ax.set_zlim(center[2] - span, center[2] + span)
    ax.set_box_aspect((1, 1, 1))


def render_composite(
    mesh,
    clip,
    mesh_to_stick: dict,
    frame_idxs: list[int],
    out: Path,
    title: str,
):
    bind_mats = clip.matrices_at(0)
    n = len(frame_idxs)
    fig = plt.figure(figsize=(4.2 * n, 5.2), dpi=120)
    fig.patch.set_facecolor("#111111")
    for col, fi in enumerate(frame_idxs):
        ax = fig.add_subplot(1, n, col + 1, projection="3d")
        ax.set_facecolor("#1a1a1a")
        pose_mats = clip.matrices_at(fi)
        verts = skin_positions(mesh, bind_mats, pose_mats, mesh_to_stick)
        # Swap to Y-up display: mesh is Y-up already; view from +Z
        view = verts.copy()
        # matplotlib 3d: plot x,z as ground-ish, y as up by remapping
        disp = np.column_stack([view[:, 0], view[:, 2], view[:, 1]])
        _draw_mesh(ax, disp, mesh.faces)
        ax.set_xticks([])
        ax.set_yticks([])
        ax.set_zticks([])
        ax.xaxis.pane.fill = False
        ax.yaxis.pane.fill = False
        ax.zaxis.pane.fill = False
        ax.view_init(elev=12, azim=-60)
        ax.set_title(f"f={fi}", color="white", fontsize=10, pad=4)
    fig.suptitle(title, color="white", fontsize=12, y=0.98)
    fig.tight_layout()
    out.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out, facecolor=fig.get_facecolor(), bbox_inches="tight")
    plt.close(fig)
    print(f"WROTE {out} ({out.stat().st_size} bytes)")


def render_single(mesh, clip, mesh_to_stick, fi: int, out: Path, title: str):
    bind_mats = clip.matrices_at(0)
    pose_mats = clip.matrices_at(fi)
    verts = skin_positions(mesh, bind_mats, pose_mats, mesh_to_stick)
    disp = np.column_stack([verts[:, 0], verts[:, 2], verts[:, 1]])
    fig = plt.figure(figsize=(7, 8), dpi=120)
    fig.patch.set_facecolor("#111111")
    ax = fig.add_subplot(111, projection="3d")
    ax.set_facecolor("#1a1a1a")
    _draw_mesh(ax, disp, mesh.faces)
    ax.set_xticks([])
    ax.set_yticks([])
    ax.set_zticks([])
    ax.xaxis.pane.fill = False
    ax.yaxis.pane.fill = False
    ax.zaxis.pane.fill = False
    ax.view_init(elev=12, azim=-60)
    ax.set_title(title, color="white", fontsize=11, pad=10)
    fig.tight_layout()
    fig.savefig(out, facecolor=fig.get_facecolor(), bbox_inches="tight")
    plt.close(fig)
    print(f"WROTE {out} ({out.stat().st_size} bytes)")


def main():
    mesh = load_mesh(MESH_PATH)
    normalize_influences(mesh)

    meta = {
        "mesh": str(MESH_PATH.relative_to(ROOT)),
        "mesh_bytes": MESH_PATH.stat().st_size,
        "vertex_count": mesh.vertex_count,
        "face_count": mesh.face_count,
        "bone_count": len(mesh.bones),
        "bones_with_weights": sum(1 for b in mesh.bones if len(b.vert_ids) > 0),
        "skinning": "rq_matrix_LBS",
        "skinning_note": (
            "Full RQ matrix LBS: per bone "
            "skin = T(mesh_bind_t + (t_f - t_0)) @ (R_f @ R_0.T) @ T(-mesh_bind_t). "
            "R|t from Dev2 matrices_at (R from quats_at, t from positions_at); "
            "mesh_bind_t from mesh bone mat0. Rest frame is identity; collapsed "
            "stick samples skipped."
        ),
        "stick_api": {
            "loader": "load_min2_stick",
            "pose": "matrices_at / quats_at",
            "bind": "matrices_at(0) R|t deltas about mesh bone mat0 origin",
        },
        "parse": {
            "magic": "HSEM@0x54",
            "header_at": "0x8C",
            "vert_quantization": "AABB_f32x2 + u16x3",
            "bone_record": "name30+parent30+children+mat44x3+nvert+ids+weights",
        },
        "clips": {},
        "limitations": [
            "m2.mdl is DependModel path stub (283B), not binary skeleton — ignored for bind",
            "Body mesh only (face/hand/leg/belt parts not staged)",
            "Parent name fields often empty; hierarchy taken from stick FK via Dev2",
            "Collapsed run frames skipped when pelvis height jumps >25 vs rest",
        ],
    }

    for tag, ani in (("walk", WALK_ANI), ("run", RUN_ANI)):
        clip = load_min2_stick(ani)
        mapping = bone_name_map(mesh, clip.bone_names)
        frames = _frame_indices(clip)
        bip_mapped = sum(
            1
            for mi, si in mapping.items()
            if clip.bone_names[si].lower().startswith("bip")
        )
        meta["clips"][tag] = {
            "ani": str(ani.relative_to(ROOT)),
            "stick_bones": clip.bone_count,
            "stick_frames": clip.frame_count,
            "fps": clip.fps,
            "frame_indices": frames,
            "mesh_bones_mapped": len(mapping),
            "bip_mapped": bip_mapped,
            "mapped_names": sorted(
                {
                    mesh.bones[mi].name: clip.bone_names[si]
                    for mi, si in mapping.items()
                    if mesh.bones[mi].name.lower().startswith("bip")
                    or mesh.bones[mi].name.lower().startswith("bone")
                }.items()
            )[:40],
        }
        # influence summary
        mapped_weight_mass = 0.0
        total_mass = 0.0
        for inf in mesh.influences:
            for bi, w in inf:
                total_mass += w
                if bi in mapping:
                    mapped_weight_mass += w
        meta["clips"][tag]["mapped_weight_fraction"] = (
            mapped_weight_mass / total_mass if total_mass else 0.0
        )

        render_composite(
            mesh,
            clip,
            mapping,
            frames,
            PROOF / f"skinned_{tag}.png",
            title=f"Shenmianfeng body skinned · {tag} · RQ matrix LBS",
        )
        mid = frames[len(frames) // 2]
        render_single(
            mesh,
            clip,
            mapping,
            mid,
            PROOF / f"skinned_{tag}_f{mid}.png",
            title=f"skinned {tag} f={mid} · RQ matrix LBS",
        )
        # Canonical singles expected by MANIFEST / Dev handoff.
        canon = {"walk": 9, "run": 11}.get(tag)
        if canon is not None and canon != mid and 0 <= canon < clip.frame_count:
            render_single(
                mesh,
                clip,
                mapping,
                canon,
                PROOF / f"skinned_{tag}_f{canon}.png",
                title=f"skinned {tag} f={canon} · RQ matrix LBS",
            )

    # global influence mapping summary
    walk_map = bone_name_map(mesh, load_min2_stick(WALK_ANI).bone_names)
    meta["bone_influence_summary"] = {
        "mesh_bones": len(mesh.bones),
        "mapped_to_stick": len(walk_map),
        "unmapped_mesh_bones": [
            b.name for i, b in enumerate(mesh.bones) if i not in walk_map
        ][:30],
        "sample_influences": [
            {
                "bone": mesh.bones[i].name,
                "vert_count": int(len(mesh.bones[i].vert_ids)),
                "stick": load_min2_stick(WALK_ANI).bone_names[walk_map[i]]
                if i in walk_map
                else None,
            }
            for i in list(walk_map.keys())[:15]
        ],
    }

    out_json = PROOF / "skinned_bind.json"
    out_json.write_text(json.dumps(meta, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"WROTE {out_json}")

    # refresh MANIFEST skinned section (replace prior Dev4 skinned blocks)
    man = PROOF / "MANIFEST.txt"
    import hashlib
    import re

    def sha16(path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()[:16]

    raw = man.read_text(encoding="utf-8") if man.exists() else "# proof MANIFEST (this box)\n"
    kept = []
    for line in raw.splitlines():
        if re.match(r"^OK skinned_", line):
            continue
        if line.startswith("# skinned proofs"):
            continue
        kept.append(line)
    while kept and not kept[-1].strip():
        kept.pop()
    extra = ["", "# skinned proofs (Dev4) skinning=rq_matrix_LBS matrices_at+quats_at"]
    for path in sorted(PROOF.glob("skinned_*.png")) + [PROOF / "skinned_bind.json"]:
        if path.exists():
            extra.append(f"OK {path.name} bytes={path.stat().st_size} sha16={sha16(path)}")
    man.write_text("\n".join(kept + extra) + "\n", encoding="utf-8")
    print("updated MANIFEST.txt")


if __name__ == "__main__":
    main()
