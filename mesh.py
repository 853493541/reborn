"""JX3 / Kingsoft HD .mesh reader (HSEM magic at 0x54).

Layout verified on m2_1018_body_hd.mesh:
  header @0x8C: meshCount, vertCount, triCount, ..., section offsets
  positions: AABB float3×2 + uint16×3 quantized (vertOff)
  indices: uint32 triangle list (triOff)
  bones @boneOff: count, then per-bone name(30)/parent(30)/children + 3×mat44
                 + nvert, vert_ids[n], weights[n]
"""
from __future__ import annotations

import struct
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import numpy as np


@dataclass
class MeshBone:
    name: str
    parent: str
    children: List[str]
    # bind matrices (row-major 4×4); mat_bind is typically the last of three
    matrices: List[np.ndarray]  # each (4,4)
    vert_ids: np.ndarray  # uint32
    weights: np.ndarray  # float32


@dataclass
class Jx3Mesh:
    path: Path
    positions: np.ndarray  # (V,3) float64 bind-pose
    faces: np.ndarray  # (F,3) uint32
    bones: List[MeshBone]
    # per-vertex influence lists built at load
    influences: List[List[Tuple[int, float]]] = field(default_factory=list)

    @property
    def vertex_count(self) -> int:
        return int(self.positions.shape[0])

    @property
    def face_count(self) -> int:
        return int(self.faces.shape[0])

    @property
    def bone_names(self) -> List[str]:
        return [b.name for b in self.bones]


def _read_name30(data: bytes, off: int) -> str:
    raw = data[off : off + 30]
    n = raw.split(b"\x00", 1)[0]
    if not n:
        return ""
    for enc in ("ascii", "gb18030", "latin-1"):
        try:
            return n.decode(enc)
        except UnicodeDecodeError:
            continue
    return n.decode("latin-1", errors="replace")


def _is_name_field(data: bytes, off: int) -> Tuple[bool, str]:
    raw = data[off : off + 30]
    n = raw.split(b"\x00", 1)[0]
    if len(n) < 2:
        return True, ""
    if not all(32 <= c < 127 for c in n):
        return False, ""
    s = n.decode("ascii")
    if any(c in s for c in "<>{}[]"):
        return False, ""
    return True, s


def _mat_ok(data: bytes, p: int) -> bool:
    if p + 64 > len(data):
        return False
    m = struct.unpack_from("<16f", data, p)
    if any(x != x or abs(x) > 1e5 for x in m):
        return False
    return abs(m[15] - 1.0) < 0.02


def _norm_bone_key(name: str) -> str:
    """Match mesh 'Bip01 R Thigh' to stick 'bip01 r thigh'."""
    return " ".join(name.lower().replace("_", " ").split())


def load_mesh(path: str | Path) -> Jx3Mesh:
    path = Path(path)
    data = path.read_bytes()
    if len(data) < 0x120:
        raise ValueError(f"mesh too small: {path}")

    magic_off = data.find(b"HSEM")
    if magic_off < 0:
        # little-endian fourcc stored as HSEM
        raise ValueError(f"no HSEM/MESH magic in {path}")

    # Counts / offsets at absolute file offset 0x8C (Noesis-style HD header)
    hdr = 0x8C
    (
        _num_mesh,
        num_verts,
        num_tris,
        _unk1,
        vert_off,
        _norm_off,
        _unk2,
        _uv_off,
        _u3,
        _u4,
        tri_off,
        _pad_off,
        bone_off,
    ) = struct.unpack_from("<13I", data, hdr)

    if num_verts == 0 or num_tris == 0:
        raise ValueError(f"empty mesh header in {path}")
    if vert_off + 24 + num_verts * 6 > len(data):
        raise ValueError(
            f"vert section OOB: vert_off={vert_off} nv={num_verts} size={len(data)}"
        )
    if tri_off + num_tris * 12 > len(data):
        raise ValueError(f"tri section OOB: tri_off={tri_off}")

    mins = np.array(struct.unpack_from("<fff", data, vert_off), dtype=np.float64)
    maxs = np.array(struct.unpack_from("<fff", data, vert_off + 12), dtype=np.float64)
    q = np.frombuffer(
        data[vert_off + 24 : vert_off + 24 + num_verts * 6], dtype="<u2"
    ).reshape(num_verts, 3)
    positions = mins + (q.astype(np.float64) / 65535.0) * (maxs - mins)

    faces = np.frombuffer(
        data[tri_off : tri_off + num_tris * 12], dtype="<u4"
    ).reshape(num_tris, 3)
    if int(faces.max()) >= num_verts:
        raise ValueError(f"face index {faces.max()} >= vert count {num_verts}")

    if bone_off == 0 or bone_off >= len(data):
        raise ValueError(
            f"bone section missing/blocked: bone_off={bone_off} (expected after tris)"
        )

    bone_count = struct.unpack_from("<I", data, bone_off)[0]
    off = bone_off + 4
    bones: List[MeshBone] = []
    for i in range(bone_count):
        ok, name = _is_name_field(data, off)
        if not ok:
            raise ValueError(
                f"bone[{i}] bad name at offset {off} (0x{off:x}); "
                f"bytes={data[off:off+16].hex()}"
            )
        off += 30
        ok, parent = _is_name_field(data, off)
        if not ok:
            raise ValueError(
                f"bone[{i}] bad parent at offset {off} after name={name!r}; "
                f"bytes={data[off:off+16].hex()}"
            )
        off += 30
        nchild = struct.unpack_from("<I", data, off)[0]
        if nchild > 64:
            raise ValueError(
                f"bone[{i}] {name!r} nchild={nchild} at 0x{off:x} (parse blocker)"
            )
        off += 4
        children = []
        for _ in range(nchild):
            ok, ch = _is_name_field(data, off)
            if not ok:
                raise ValueError(f"bone[{i}] bad child name at 0x{off:x}")
            children.append(ch)
            off += 30

        def _try_weight_block(p: int):
            nvert = struct.unpack_from("<I", data, p + 128)[0]
            if nvert > num_verts:
                return None
            if nvert == 0:
                return (p, 0, np.zeros(0, dtype=np.uint32), np.zeros(0, dtype=np.float32))
            ids = np.frombuffer(
                data[p + 132 : p + 132 + nvert * 4], dtype="<u4"
            ).copy()
            if ids.size and int(ids.max()) >= num_verts:
                return None
            ws = np.frombuffer(
                data[p + 132 + nvert * 4 : p + 132 + nvert * 8], dtype="<f4"
            ).copy()
            if not np.all((ws >= -0.01) & (ws <= 1.05)):
                return None
            return (p, nvert, ids, ws)

        found = None
        # Pass 1: classic 2×mat44 (m2 / f1_2227).
        for adj in range(0, 256, 4):
            p = off + adj
            if not (_mat_ok(data, p) and _mat_ok(data, p + 64)):
                continue
            hit = _try_weight_block(p)
            if hit is not None:
                found = hit
                break
        # Pass 2: denser F1 (f1_1008…) — mat0 ok, mat1 slot is opaque 64B gap.
        if found is None:
            for adj in range(0, 256, 4):
                p = off + adj
                if not _mat_ok(data, p):
                    continue
                if _mat_ok(data, p + 64):
                    continue  # would have matched pass 1
                hit = _try_weight_block(p)
                if hit is not None:
                    found = hit
                    break
        if found is None:
            raise ValueError(
                f"bone[{i}] {name!r}: no mat44×2+weights after children "
                f"(scan from 0x{off:x}) — parse blocker"
            )
        p, nvert, ids, ws = found
        # capture up to 3 matrices ending at weight block (skip may be 64 → 3 mats)
        mats = []
        mat_start = p - 64 if (p >= off + 64 and _mat_ok(data, p - 64)) else p
        for mi in range(3):
            mp = mat_start + mi * 64
            if mp + 64 > p + 128:
                break
            if _mat_ok(data, mp):
                mats.append(np.array(struct.unpack_from("<16f", data, mp), dtype=np.float64).reshape(4, 4))
        bones.append(
            MeshBone(
                name=name,
                parent=parent,
                children=children,
                matrices=mats,
                vert_ids=ids,
                weights=ws.astype(np.float64),
            )
        )
        off = p + 128 + 4 + nvert * 8

    # Build per-vertex influences
    influences: List[List[Tuple[int, float]]] = [[] for _ in range(num_verts)]
    for bi, bone in enumerate(bones):
        for vi, w in zip(bone.vert_ids.tolist(), bone.weights.tolist()):
            if w > 1e-8:
                influences[int(vi)].append((bi, float(w)))

    return Jx3Mesh(
        path=path,
        positions=positions,
        faces=faces,
        bones=bones,
        influences=influences,
    )


def normalize_influences(mesh: Jx3Mesh) -> None:
    for i, inf in enumerate(mesh.influences):
        if not inf:
            continue
        s = sum(w for _, w in inf)
        if s > 1e-12:
            mesh.influences[i] = [(b, w / s) for b, w in inf]


def bone_name_map(mesh: Jx3Mesh, stick_names: List[str]) -> Dict[int, int]:
    """Map mesh bone index → stick bone index (normalized name match)."""
    stick_index = {_norm_bone_key(n): i for i, n in enumerate(stick_names)}
    mapping: Dict[int, int] = {}
    for i, b in enumerate(mesh.bones):
        key = _norm_bone_key(b.name)
        if key in stick_index:
            mapping[i] = stick_index[key]
        else:
            # try without spaces (Bone_R_ThighTwist vs bone_r_thightwist typo variants)
            key2 = key.replace(" ", "")
            for sn, si in stick_index.items():
                if sn.replace(" ", "") == key2:
                    mapping[i] = si
                    break
            else:
                # thigh twist typo: thightwist in ani
                if "thightwist" in key2 or "thightwist" in key:
                    pass
                alt = key2.replace("thightwist", "thightwist")
                for sn, si in stick_index.items():
                    a = sn.replace(" ", "")
                    if a.replace("thightwist", "thightwist") == alt:
                        mapping[i] = si
                        break
                # bone_r_thightwist <-> bone_r_thightwist
                if i not in mapping:
                    for sn, si in stick_index.items():
                        a = sn.replace(" ", "").replace("thightwist", "thightwist")
                        b = key2.replace("thightwist", "thightwist")
                        # map thighTwist mesh <-> thightwist ani
                        if a.replace("thightwist", "X") == b.replace("thightwist", "X"):
                            mapping[i] = si
                            break
                        if "twist" in a and "twist" in b and a[:8] == b[:8]:
                            mapping[i] = si
                            break
    # Compact / twist aliases: LThighTwist ↔ lthightwist ↔ l thigh twist
    def _aliases(key: str):
        c = key.replace(" ", "")
        out = {key, c}
        # bip01lthightwist ↔ bip01 l thigh twist
        for a, b in (
            ("lthightwist", "l thigh twist"),
            ("rthightwist", "r thigh twist"),
            ("lforetwist", "l fore twist"),
            ("rforetwist", "r fore twist"),
            ("lupperarm", "l upper arm"),
            ("rupperarm", "r upper arm"),
        ):
            if a in c:
                out.add(c.replace(a, b.replace(" ", "")))
                out.add(key.replace(a, b) if a in key else key)
                spaced = c.replace(a, " " + b + " ")
                out.add(" ".join(spaced.split()))
        return out

    stick_alias: Dict[str, int] = {}
    for sn, si in stick_index.items():
        for a in _aliases(sn):
            stick_alias.setdefault(a, si)
            stick_alias.setdefault(a.replace(" ", ""), si)

    for i, b in enumerate(mesh.bones):
        if i in mapping:
            continue
        k = _norm_bone_key(b.name)
        for a in _aliases(k):
            if a in stick_alias:
                mapping[i] = stick_alias[a]
                break
            if a.replace(" ", "") in stick_alias:
                mapping[i] = stick_alias[a.replace(" ", "")]
                break
    return mapping


def _as_mat4_from_3x4(row12) -> np.ndarray:
    """Stick matrices_at entry (12 floats, row-major 3x4 R|t) → 4x4 column-vector mat."""
    m = np.asarray(row12, dtype=np.float64).reshape(3, 4)
    out = np.eye(4, dtype=np.float64)
    out[:3, :] = m
    return out


def _mesh_ibm_and_bind(bone: MeshBone) -> Tuple[np.ndarray, np.ndarray]:
    """Column-vector inverse-bind and bind from mesh bone mat0 (row-stored inv-bind)."""
    if not bone.matrices:
        eye = np.eye(4, dtype=np.float64)
        return eye, eye
    # Parser stores row-vector inv-bind (translation in last row). Column IBM = mat0.T.
    ibm = np.asarray(bone.matrices[0], dtype=np.float64).T.copy()
    bind = np.linalg.inv(ibm)
    return ibm, bind


def _looks_like_matrices(rows) -> bool:
    if not rows:
        return False
    first = rows[0]
    try:
        n = len(first)
    except TypeError:
        return False
    return n == 12


def skin_positions_rq(
    mesh: Jx3Mesh,
    stick_bind_mats: List,
    stick_pose_mats: List,
    mesh_to_stick: Dict[int, int],
) -> np.ndarray:
    """Full RQ matrix LBS: stick R|t deltas about mesh bind bone origins.

    Per mapped bone (column-vector form)::

        Rd = R_pose @ R_bind.T          # from stick matrices_at / quats_at
        td = t_pose - t_bind            # from stick matrices_at / positions_at
        bt = mesh_bind_translation      # from mesh bone mat0
        skin = T(bt + td) @ Rd @ T(-bt) # == pose @ inv(bind) with bind=T(bt)

    Equivalently classic LBS ``pose @ inv(bind) @ v`` where bind is the mesh
    bone origin and pose carries stick rotation+translation deltas. Rest pose
    (pose mats == bind mats) yields identity. Non-finite / collapsed stick
    samples (pelvis-height style runaway) skip that bone (identity).
    """
    if len(stick_bind_mats) != len(stick_pose_mats):
        raise ValueError(
            f"bind/pose matrix count mismatch {len(stick_bind_mats)} vs {len(stick_pose_mats)}"
        )
    n_bones = len(mesh.bones)
    skin_mats = [np.eye(4, dtype=np.float64) for _ in range(n_bones)]
    for mi, si in mesh_to_stick.items():
        if si < 0 or si >= len(stick_pose_mats):
            raise ValueError(
                f"stick index {si} OOB for mesh bone {mi} ({mesh.bones[mi].name})"
            )
        try:
            Gs0 = _as_mat4_from_3x4(stick_bind_mats[si])
            Gsf = _as_mat4_from_3x4(stick_pose_mats[si])
        except Exception as exc:  # noqa: BLE001
            raise RuntimeError(
                f"RQ LBS matrix parse fail mesh_bone={mesh.bones[mi].name!r} stick_i={si}: {exc}"
            ) from exc
        if not (np.isfinite(Gs0).all() and np.isfinite(Gsf).all()):
            continue
        t0 = Gs0[:3, 3]
        tf = Gsf[:3, 3]
        td = tf - t0
        # Reject collapsed / runaway poses (same band as prior translation path).
        if abs(float(td[2])) > 40.0 or float(np.max(np.abs(td))) > 80.0:
            continue
        R0 = Gs0[:3, :3]
        Rf = Gsf[:3, :3]
        try:
            Rd = Rf @ R0.T
        except Exception as exc:  # noqa: BLE001
            raise RuntimeError(
                f"RQ LBS R-delta fail mesh_bone={mesh.bones[mi].name!r} stick_i={si}: {exc}"
            ) from exc
        _ibm, bind = _mesh_ibm_and_bind(mesh.bones[mi])
        bt = bind[:3, 3].copy()
        # skin = T(bt+td) @ Rd @ T(-bt)
        M = np.eye(4, dtype=np.float64)
        M[:3, :3] = Rd
        M[:3, 3] = bt + td - Rd @ bt
        if not np.isfinite(M).all():
            continue
        skin_mats[mi] = M

    out = mesh.positions.copy()
    for vi, inf in enumerate(mesh.influences):
        if not inf:
            continue
        v = np.array(
            [mesh.positions[vi, 0], mesh.positions[vi, 1], mesh.positions[vi, 2], 1.0],
            dtype=np.float64,
        )
        acc = np.zeros(4, dtype=np.float64)
        for bi, w in inf:
            acc += w * (skin_mats[bi] @ v)
        out[vi] = acc[:3]
    return out



def skin_positions(
    mesh: Jx3Mesh,
    stick_rest,
    stick_pose,
    mesh_to_stick: Dict[int, int],
) -> np.ndarray:
    """Skin mesh verts for a stick pose.

    Preferred path (RQ matrix LBS): pass ``matrices_at(0)`` / ``matrices_at(f)``
    as stick_rest / stick_pose (each bone a 12-float 3x4 R|t). Play and proofs
    use this so rotations from ``quats_at``/``matrices_at`` drive deformation.

    Legacy path: 3-float world positions → pelvis-relative translation blend
    (kept only for callers that have not switched to matrices_at yet).
    """
    if _looks_like_matrices(stick_pose):
        bind = stick_rest if _looks_like_matrices(stick_rest) else stick_pose
        if not _looks_like_matrices(stick_rest):
            raise ValueError(
                "RQ LBS requires stick_rest=matrices_at(0); got position tuples"
            )
        return skin_positions_rq(mesh, bind, stick_pose, mesh_to_stick)

    # --- legacy pelvis-relative translation LBS ---
    rest = np.asarray(stick_rest, dtype=np.float64)
    pose = np.asarray(stick_pose, dtype=np.float64)
    if pose.shape != rest.shape:
        raise ValueError(f"pose/rest shape mismatch {pose.shape} vs {rest.shape}")
    bad = ~np.isfinite(pose).all(axis=1)
    if bad.any():
        pose = pose.copy()
        pose[bad] = rest[bad]
    out = mesh.positions.copy()
    deltas = np.zeros((len(mesh.bones), 3), dtype=np.float64)
    pelvis_mi = None
    for mi, b in enumerate(mesh.bones):
        if _norm_bone_key(b.name) == "bip01 pelvis":
            pelvis_mi = mi
            break
    root_si = mesh_to_stick.get(pelvis_mi) if pelvis_mi is not None else None
    root_delta = np.zeros(3, dtype=np.float64)
    if root_si is not None:
        rd = pose[root_si] - rest[root_si]
        if np.isfinite(rd).all() and abs(rd[2]) < 40.0 and np.max(np.abs(rd)) < 80.0:
            root_delta = rd
    for mi, si in mesh_to_stick.items():
        if root_si is not None:
            d = (pose[si] - pose[root_si]) - (rest[si] - rest[root_si])
        else:
            d = pose[si] - rest[si]
        if not np.isfinite(d).all() or np.max(np.abs(d)) > 200.0:
            continue
        deltas[mi] = d
    for vi, inf in enumerate(mesh.influences):
        if not inf:
            continue
        acc = np.zeros(3, dtype=np.float64)
        for bi, w in inf:
            acc += w * deltas[bi]
        out[vi] = mesh.positions[vi] + acc + root_delta
    return out
