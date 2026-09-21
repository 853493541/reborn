#!/usr/bin/env python3
"""F1 body × 风来吴山蓄力 RQ LBS proof — honest gate; no M2 mesh substitute."""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from mesh import bone_name_map, load_mesh, normalize_influences  # noqa: E402
from min2 import load_min2_stick  # noqa: E402

PROOF = ROOT / "proof"
MESH_PATH = ROOT / "samples" / "mesh" / "f1_1008_body_hd.mesh"
HEAD_PATH = ROOT / "samples" / "mesh" / "f1_1000_head_hd.mesh"
ANI = ROOT / "samples" / "player" / "moves" / "f1s07cj重剑技能15蓄力_奇穴.ani"
BIP_MAP_MIN_FRAC = 0.40  # Lead: <~40% bip mapped → stop


def _sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main() -> int:
    if not MESH_PATH.exists():
        print(f"BLOCKER missing mesh: {MESH_PATH}")
        return 2
    if not ANI.exists():
        print(f"BLOCKER missing ani: {ANI}")
        return 2

    try:
        mesh = load_mesh(MESH_PATH)
    except Exception as e:
        print(f"BLOCKER parse_error: {type(e).__name__}: {e}")
        return 3

    normalize_influences(mesh)
    clip = load_min2_stick(ANI)
    mapping = bone_name_map(mesh, clip.bone_names)
    stick_bip = [n for n in clip.bone_names if n.lower().startswith("bip")]
    mesh_bip = [b.name for b in mesh.bones if b.name.lower().startswith("bip")]
    bip_mapped = sum(
        1 for mi, si in mapping.items() if clip.bone_names[si].lower().startswith("bip")
    )
    frac = (bip_mapped / len(stick_bip)) if stick_bip else 0.0

    head_err = None
    if HEAD_PATH.exists():
        try:
            load_mesh(HEAD_PATH)
        except Exception as e:
            head_err = f"{type(e).__name__}: {e}"

    meta = {
        "status": "BLOCKED" if frac < BIP_MAP_MIN_FRAC else "OK",
        "blocker": "bone_map_too_poor" if frac < BIP_MAP_MIN_FRAC else None,
        "blocker_detail": (
            f"mesh_bip={len(mesh_bip)} mapped={len(mapping)} "
            f"bip_mapped={bip_mapped}/{len(stick_bip)} ({frac*100:.1f}%). "
            f"Need >={BIP_MAP_MIN_FRAC*100:.0f}% stick bip mapped. "
            "Refused m2_1018_body_hd.mesh substitute."
        ),
        "mesh": str(MESH_PATH.relative_to(ROOT)),
        "mesh_bytes": MESH_PATH.stat().st_size,
        "mesh_sha256": _sha(MESH_PATH),
        "vertex_count": mesh.vertex_count,
        "face_count": mesh.face_count,
        "bone_count": len(mesh.bones),
        "bones_with_weights": sum(1 for b in mesh.bones if len(b.vert_ids) > 0),
        "mesh_bip_count": len(mesh_bip),
        "bone_map_count": len(mapping),
        "bip_mapped": bip_mapped,
        "stick_bip_count": len(stick_bip),
        "bip_mapped_fraction_of_stick_bip": frac,
        "skinning": "rq_matrix_LBS",
        "skinning_attempted": False,
        "m2_mesh_used": False,
        "clip": {
            "ani": str(ANI.relative_to(ROOT)),
            "stick_bones": clip.bone_count,
            "stick_frames": clip.frame_count,
            "fps": clip.fps,
        },
        "optional_head_parse_error": head_err,
        "sample_mesh_bones": [b.name for b in mesh.bones[:15]]
        + [b.name for b in mesh.bones[-3:]],
    }

    out_json = PROOF / "skinned_flws_charge_meta.json"
    out_json.write_text(json.dumps(meta, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"WROTE {out_json}")

    if frac < BIP_MAP_MIN_FRAC:
        print(f"BLOCKER bone_map_too_poor: {meta['blocker_detail']}")
        print("PNG proofs NOT written (honest stop).")
        return 4

    # Success path: reuse render_skinned_proof helpers when a real F1 body lands.
    from render_skinned_proof import render_composite, render_single  # type: ignore

    frames = [0, min(25, clip.frame_count - 1), clip.frame_count - 1]
    render_composite(
        mesh,
        clip,
        mapping,
        frames,
        PROOF / "skinned_flws_charge.png",
        title="F1 body skinned · FLWS 蓄力 · RQ matrix LBS",
    )
    for fi in frames:
        render_single(
            mesh,
            clip,
            mapping,
            fi,
            PROOF / f"skinned_flws_charge_f{fi}.png",
            title=f"FLWS charge f={fi} · RQ matrix LBS",
        )
    meta["status"] = "OK"
    meta["skinning_attempted"] = True
    meta["frame_indices"] = frames
    meta["blocker"] = None
    out_json.write_text(json.dumps(meta, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
