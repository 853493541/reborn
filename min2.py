"""MIN2 .ani reader — layout from legacy EXPERIENCES.md §6.1 + plot samples.

Paths:
  - bone_count==1: single-bone vertex morph (PSS effects). Full track parse.
  - name-table (plot multi-bone, header bone_count typically 13):
      first 50-byte record at 0x10 holds vc / vc2 / fc / fps / vc3;
      4-byte tag at 0x42; then vc × 30-byte names at 0x46;
      remainder is payload: face weights (opaque) or player-action skel keys.
      kind is face_nametable | body_nametable | skel_nametable from names.
  - Player-action skel (samples/player): decode_skel_stick() / load_min2_stick().

Not the same as MINA (FbxCmd .mesh.ani).
"""
from __future__ import annotations

import struct
from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Optional, Tuple


# Tag immediately after the first 50-byte record on plot face clips.
_FACE_NAME_TABLE_TAG = 0x86BB9016


@dataclass
class Min2BoneTrack:
    name: str
    vertex_count: int
    vertex_count2: int
    frame_count: int
    fps: float
    has_normals: bool
    # positions[frame][vertex] = (x,y,z) — empty for name-table slots
    positions: List[List[Tuple[float, float, float]]] = field(default_factory=list)
    normals: Optional[List[List[Tuple[float, float, float]]]] = None


@dataclass
class Min2Clip:
    path: Path
    version: int
    declared_size: int
    bones: List[Min2BoneTrack]
    kind: str = "vertex_morph"  # vertex_morph | face_nametable | body_nametable | skel_nametable
    header_bone_count: int = 0
    root_name: str = ""
    # name-table: raw bytes after the name table (weights / keys TBD)
    payload: bytes = b""
    payload_offset: int = 0

    @property
    def bone_count(self) -> int:
        return len(self.bones)

    @property
    def frame_count(self) -> int:
        return self.bones[0].frame_count if self.bones else 0

    @property
    def morph_names(self) -> List[str]:
        return [b.name for b in self.bones]


def _read_name30(data: bytes, off: int) -> str:
    raw = data[off : off + 30]
    n = raw.split(b"\x00", 1)[0]
    for enc in ("gb18030", "utf-8", "latin-1"):
        try:
            return n.decode(enc)
        except UnicodeDecodeError:
            continue
    return n.decode("latin-1", errors="replace")


def _read_bone_record(data: bytes, off: int) -> tuple[Min2BoneTrack, int]:
    name = _read_name30(data, off)
    vc, vc2, fc = struct.unpack_from("<III", data, off + 30)
    fps = struct.unpack_from("<f", data, off + 42)[0]
    vc3 = struct.unpack_from("<I", data, off + 46)[0]
    has_n = vc3 > 0
    p = off + 50
    p += vc * 8
    npos = vc * fc * 3
    floats = struct.unpack_from("<" + "f" * npos, data, p)
    p += npos * 4
    positions: List[List[Tuple[float, float, float]]] = []
    idx = 0
    for _f in range(fc):
        frame = []
        for _v in range(vc):
            frame.append((floats[idx], floats[idx + 1], floats[idx + 2]))
            idx += 3
        positions.append(frame)
    normals = None
    if has_n:
        nfloat = struct.unpack_from("<" + "f" * npos, data, p)
        p += npos * 4
        normals = []
        idx = 0
        for _f in range(fc):
            frame = []
            for _v in range(vc):
                frame.append((nfloat[idx], nfloat[idx + 1], nfloat[idx + 2]))
                idx += 3
            normals.append(frame)
    track = Min2BoneTrack(
        name=name,
        vertex_count=vc,
        vertex_count2=vc2,
        frame_count=fc,
        fps=fps,
        has_normals=has_n,
        positions=positions,
        normals=normals,
    )
    return track, p


def _name_table_ascii_score(data: bytes, table_off: int, count: int) -> int:
    ok = 0
    need = table_off + count * 30
    if need > len(data):
        return 0
    for i in range(count):
        n = data[table_off + i * 30 : table_off + i * 30 + 30].split(b"\x00", 1)[0]
        if n and all(32 <= c < 127 for c in n):
            ok += 1
    return ok


def _classify_nametable(names: List[str], root_name: str) -> str:
    # Prefer early-name signal: mesh-deform parts vs bip skeleton.
    # Player-action clips often have an empty root name but bip01 in the table —
    # do not treat empty root as face.
    head = names[: min(12, len(names))]
    mesh_head = sum(1 for n in head if n.startswith("FB") or "body" in n.lower())
    skel_head = sum(
        1
        for n in head
        if n.lower().startswith(("bip", "bone_", "add_"))
        or (n.startswith("B_") and "body" not in n.lower())
    )
    face_like = sum(
        1
        for n in names[:24]
        if n.lower() in {"tongue", "smile_r", "smile_l", "ridge_r", "pupil_r", "pupil_l"}
        or "smile" in n.lower()
        or "brow" in n.lower()
    )
    if skel_head > 0 and skel_head >= mesh_head:
        return "skel_nametable"
    if mesh_head > 0 and mesh_head >= skel_head:
        return "body_nametable"
    if not root_name and face_like > 0:
        return "face_nametable"
    if not root_name:
        return "face_nametable"
    return "body_nametable"


def _looks_like_nametable(data: bytes, bone_count: int) -> bool:
    """Plot multi-bone clips: record0 + tag + vc×30 name table."""
    if bone_count <= 1 or len(data) < 0x46:
        return False
    vc, vc2, fc = struct.unpack_from("<III", data, 0x10 + 30)
    if not (1 <= vc <= 512 and 1 <= fc <= 8192):
        return False
    table_off = 0x46
    if table_off + vc * 30 > len(data):
        return False
    score = _name_table_ascii_score(data, table_off, vc)
    if score < max(1, vc // 2):
        return False
    name0 = data[0x10 : 0x10 + 30].split(b"\x00", 1)[0]
    tag = struct.unpack_from("<I", data, 0x42)[0]
    # face: empty root + known tag; body/skel: non-empty root name
    if not name0:
        return tag == _FACE_NAME_TABLE_TAG or True  # empty root + good table
    return True


def _load_nametable(
    path: Path,
    data: bytes,
    version: int,
    declared_size: int,
    header_bone_count: int,
) -> Min2Clip:
    root_name = _read_name30(data, 0x10)
    vc, vc2, fc = struct.unpack_from("<III", data, 0x10 + 30)
    fps = struct.unpack_from("<f", data, 0x10 + 42)[0]
    vc3 = struct.unpack_from("<I", data, 0x10 + 46)[0]
    table_off = 0x46
    end = table_off + vc * 30
    if end > len(data):
        raise ValueError(
            f"name-table overrun: vc={vc} need={end} size={len(data)} ({path})"
        )
    bones: List[Min2BoneTrack] = []
    names: List[str] = []
    for i in range(vc):
        name = _read_name30(data, table_off + i * 30)
        names.append(name)
        bones.append(
            Min2BoneTrack(
                name=name,
                vertex_count=0,
                vertex_count2=vc2,
                frame_count=fc,
                fps=fps,
                has_normals=vc3 > 0,
                positions=[],
                normals=None,
            )
        )
    kind = _classify_nametable(names, root_name)
    return Min2Clip(
        path=path,
        version=version,
        declared_size=declared_size,
        bones=bones,
        kind=kind,
        header_bone_count=header_bone_count,
        root_name=root_name,
        payload=data[end:],
        payload_offset=end,
    )


def load_min2(path: str | Path) -> Min2Clip:
    path = Path(path)
    data = path.read_bytes()
    if data[:4] != b"MIN2":
        raise ValueError(f"Not MIN2: {path} magic={data[:4]!r}")
    declared_size, version, bone_count = struct.unpack_from("<III", data, 4)
    if version != 1:
        pass
    if not (1 <= bone_count <= 256):
        raise ValueError(f"implausible bone_count={bone_count}")

    if _looks_like_nametable(data, bone_count):
        return _load_nametable(path, data, version, declared_size, bone_count)

    if bone_count == 1:
        track, _off = _read_bone_record(data, 0x10)
        return Min2Clip(
            path=path,
            version=version,
            declared_size=declared_size,
            bones=[track],
            kind="vertex_morph",
            header_bone_count=bone_count,
            root_name=track.name,
        )

    name0 = _read_name30(data, 0x10)
    raise ValueError(
        f"MIN2 unrecognized multi-bone layout "
        f"(header_bones={bone_count} name0={name0!r} declared={declared_size}) in {path}"
    )



# --- player-action skel payload (tag 0xd17d22b0): bind TRS + i16 quats ---

def _qnorm(q: Tuple[float, float, float, float]) -> Tuple[float, float, float, float]:
    n = (q[0] * q[0] + q[1] * q[1] + q[2] * q[2] + q[3] * q[3]) ** 0.5
    if n <= 1e-12:
        return (0.0, 0.0, 0.0, 1.0)
    return (q[0] / n, q[1] / n, q[2] / n, q[3] / n)


def _qmul(
    a: Tuple[float, float, float, float], b: Tuple[float, float, float, float]
) -> Tuple[float, float, float, float]:
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    )


def _qrot(
    q: Tuple[float, float, float, float], v: Tuple[float, float, float]
) -> Tuple[float, float, float]:
    x, y, z = v
    qx, qy, qz, qw = q
    tx = 2.0 * (qy * z - qz * y)
    ty = 2.0 * (qz * x - qx * z)
    tz = 2.0 * (qx * y - qy * x)
    return (
        x + qw * tx + qy * tz - qz * ty,
        y + qw * ty + qz * tx - qx * tz,
        z + qw * tz + qx * ty - qy * tx,
    )


def _find_type2_bind_offsets(payload: bytes) -> List[int]:
    """Offsets of type=2 bind records with unit quaternions."""
    hits: List[int] = []
    for off in range(0, len(payload) - 60, 4):
        if struct.unpack_from("<I", payload, off)[0] != 2:
            continue
        q = struct.unpack_from("<4f", payload, off + 4 + 24)
        n2 = q[0] * q[0] + q[1] * q[1] + q[2] * q[2] + q[3] * q[3]
        if 0.96 <= n2 <= 1.04:
            hits.append(off)
    return hits


@dataclass
class Min2SkelClip:
    """Stick-ready view of a player-action skel_nametable clip."""

    path: Path
    bone_names: List[str]
    frame_count: int
    fps: float
    # world-space positions[bone][frame]
    _positions: List[List[Tuple[float, float, float]]]
    # world-space rotations[bone][frame] as (x,y,z,w) — for RQ/LBS
    _quats: List[List[Tuple[float, float, float, float]]] = field(default_factory=list)
    # parent-space rotations[bone][frame]
    _local_quats: List[List[Tuple[float, float, float, float]]] = field(
        default_factory=list
    )
    # parent-space translations[bone][frame] (root=anim/bind; children=rest offset)
    _local_positions: List[List[Tuple[float, float, float]]] = field(
        default_factory=list
    )
    tex_hint: str = "MIN2-skel"

    @property
    def bone_count(self) -> int:
        return len(self.bone_names)

    def positions_at(self, frame: int) -> List[Tuple[float, float, float]]:
        f = max(0, min(frame, self.frame_count - 1))
        return [self._positions[b][f] for b in range(self.bone_count)]

    def quats_at(self, frame: int) -> List[Tuple[float, float, float, float]]:
        """World-space bone rotations (x,y,z,w) for full RQ matrix LBS."""
        f = max(0, min(frame, self.frame_count - 1))
        if not self._quats:
            return [(0.0, 0.0, 0.0, 1.0)] * self.bone_count
        return [self._quats[b][f] for b in range(self.bone_count)]

    def local_quats_at(self, frame: int) -> List[Tuple[float, float, float, float]]:
        """Parent-space bone rotations (x,y,z,w)."""
        f = max(0, min(frame, self.frame_count - 1))
        if not self._local_quats:
            return [(0.0, 0.0, 0.0, 1.0)] * self.bone_count
        return [self._local_quats[b][f] for b in range(self.bone_count)]


    def local_matrices_at(self, frame: int) -> List[List[float]]:
        """3x4 row-major (R|t) per bone in PARENT space for FBX bone.decompose.

        Rotation from local_quats_at; translation from _local_positions
        (root = animated/bind world root; children = constant parent-space rest).
        Scale is identity (no extra column beyond R|t).
        """
        f = max(0, min(frame, self.frame_count - 1))
        quats = self.local_quats_at(frame)
        if self._local_positions:
            pos = [self._local_positions[b][f] for b in range(self.bone_count)]
        else:
            # Fallback: root from world positions; children zero (bind-relative missing)
            world = self.positions_at(frame)
            pos = []
            for i in range(self.bone_count):
                if i == 0:
                    pos.append(world[i])
                else:
                    pos.append((0.0, 0.0, 0.0))
        out: List[List[float]] = []
        for (x, y, z, w), (px, py, pz) in zip(quats, pos):
            xx, yy, zz = x * x, y * y, z * z
            xy, xz, yz = x * y, x * z, y * z
            wx, wy, wz = w * x, w * y, w * z
            out.append(
                [
                    1 - 2 * (yy + zz),
                    2 * (xy - wz),
                    2 * (xz + wy),
                    px,
                    2 * (xy + wz),
                    1 - 2 * (xx + zz),
                    2 * (yz - wx),
                    py,
                    2 * (xz - wy),
                    2 * (yz + wx),
                    1 - 2 * (xx + yy),
                    pz,
                ]
            )
        return out

    def matrices_at(self, frame: int) -> List[List[float]]:
        """3x4 row-major (R|t) per bone for classic LBS."""
        pos = self.positions_at(frame)
        quats = self.quats_at(frame)
        out: List[List[float]] = []
        for (x, y, z, w), (px, py, pz) in zip(quats, pos):
            xx, yy, zz = x * x, y * y, z * z
            xy, xz, yz = x * y, x * z, y * z
            wx, wy, wz = w * x, w * y, w * z
            out.append(
                [
                    1 - 2 * (yy + zz),
                    2 * (xy - wz),
                    2 * (xz + wy),
                    px,
                    2 * (xy + wz),
                    1 - 2 * (xx + zz),
                    2 * (yz - wx),
                    py,
                    2 * (xz - wy),
                    2 * (yz + wx),
                    1 - 2 * (xx + yy),
                    pz,
                ]
            )
        return out


def decode_skel_stick(clip: Min2Clip) -> Min2SkelClip:
    """Decode player-action skel payload into per-frame stick positions.

    Layout after name table (tag 0xd17d22b0):
      - bind poses: either typed (u32=2/3 + floats) or dense 15-float TRS
      - i32[vc] hierarchy/order table (negatives are markers)
      - u8[vc2] channel flags (1=quat, 2=quat+pos3f, 8=quat+extra)
      - 4-byte header
      - bone-major tracks: fc×i16[4] quat (/32767); flag 2 adds fc×f32[3] pos
    """
    names = [b.name for b in clip.bones]
    vc = len(names)
    fc = clip.frame_count
    fps = clip.bones[0].fps if clip.bones else 30.0
    payload = clip.payload

    bind_pos: List[Tuple[float, float, float]] = [(0.0, 0.0, 0.0)] * vc
    bind_quat: List[Tuple[float, float, float, float]] = [(0.0, 0.0, 0.0, 1.0)] * vc

    # Locate order table: vc int32s with exactly the non-negative ids 0..vc2-ish
    order_off = None
    order: List[int] = []
    for off in range(0, max(0, len(payload) - vc * 4), 4):
        vals = list(struct.unpack_from("<" + "i" * vc, payload, off))
        pos = [v for v in vals if v >= 0]
        if len(pos) < 8:
            continue
        if min(pos) != 0 or len(set(pos)) != len(pos):
            continue
        if max(pos) >= vc:
            continue
        # plausible: mostly dense ids
        if len(pos) >= max(8, vc // 4):
            order_off = off
            order = pos
            break
    if order_off is None:
        raise ValueError(f"order table not found in {clip.path}")
    vc2 = len(order)

    # Binds: prefer typed type=2 records; else dense 15f before order table
    hits = _find_type2_bind_offsets(payload[:order_off] if order_off else payload)
    if hits:
        if struct.unpack_from("<I", payload, 0)[0] == 3 and len(payload) >= 60:
            rf = struct.unpack_from("<13f", payload, 8)
            bind_pos[0] = (0.0, 0.0, float(rf[0]))
            bind_quat[0] = _qnorm((rf[5], rf[6], rf[7], rf[8]))
        for i, off in enumerate(hits):
            if i >= vc2:
                break
            fl = struct.unpack_from("<14f", payload, off + 4)
            bid = order[i]
            bind_pos[bid] = (fl[0], fl[1], fl[2])
            bind_quat[bid] = _qnorm((fl[6], fl[7], fl[8], fl[9]))
    else:
        # dense 15-float TRS: pos3 + pad + scale3 + quat4 + extra4
        nbind = order_off // 60
        for i in range(min(nbind, vc2, vc)):
            fl = struct.unpack_from("<15f", payload, i * 60)
            q = _qnorm((fl[7], fl[8], fl[9], fl[10]))
            bid = order[i] if i < vc2 else i
            if bid >= vc:
                continue
            bind_pos[bid] = (fl[0], fl[1], fl[2])
            bind_quat[bid] = q

    body = payload[order_off + vc * 4 :]
    if len(body) < vc2 + 4:
        raise ValueError(f"channel flag table missing in {clip.path}")
    flags = list(body[:vc2])
    track = body[vc2 + 4 :]
    anim_q: List[List[Optional[Tuple[float, float, float, float]]]] = [
        [None] * fc for _ in range(vc)
    ]
    anim_p: List[List[Optional[Tuple[float, float, float]]]] = [
        [None] * fc for _ in range(vc)
    ]
    o = 0
    for i, flag in enumerate(flags):
        bid = order[i]
        for fr in range(fc):
            if o + 8 > len(track):
                raise ValueError(f"quat track truncated bone={bid} in {clip.path}")
            s = struct.unpack_from("<4h", track, o)
            o += 8
            anim_q[bid][fr] = _qnorm(
                (s[0] / 32767.0, s[1] / 32767.0, s[2] / 32767.0, s[3] / 32767.0)
            )
        if flag == 2:
            for fr in range(fc):
                if o + 12 > len(track):
                    raise ValueError(f"pos track truncated bone={bid} in {clip.path}")
                p = struct.unpack_from("<3f", track, o)
                o += 12
                anim_p[bid][fr] = (float(p[1]), float(p[2]), float(p[0]))
        elif flag == 8:
            # quat already read; 4 extra bytes per frame
            o += 4 * fc

    try:
        from mina import _PARENT_RULES
    except ImportError:
        _PARENT_RULES = []
    index = {n.lower().strip(): i for i, n in enumerate(names)}
    parent = [-1] * vc
    for child, par in _PARENT_RULES:
        if child in index and par in index:
            parent[index[child]] = index[par]

    rest = [(0.0, 0.0, 0.0)] * vc
    for i in range(vc):
        if parent[i] < 0:
            rest[i] = bind_pos[i]
            continue
        bp = bind_pos[i]
        mag = (bp[0] * bp[0] + bp[1] * bp[1] + bp[2] * bp[2]) ** 0.5
        if mag < 50.0:
            rest[i] = bp
        else:
            pp = bind_pos[parent[i]]
            rest[i] = (bp[0] - pp[0], bp[1] - pp[1], bp[2] - pp[2])

    positions: List[List[Tuple[float, float, float]]] = [
        [(0.0, 0.0, 0.0)] * fc for _ in range(vc)
    ]
    world_quats: List[List[Tuple[float, float, float, float]]] = [
        [(0.0, 0.0, 0.0, 1.0)] * fc for _ in range(vc)
    ]
    local_quats: List[List[Tuple[float, float, float, float]]] = [
        [(0.0, 0.0, 0.0, 1.0)] * fc for _ in range(vc)
    ]
    local_positions: List[List[Tuple[float, float, float]]] = [
        [(0.0, 0.0, 0.0)] * fc for _ in range(vc)
    ]
    for fr in range(fc):
        world_p: List[Optional[Tuple[float, float, float]]] = [None] * vc
        world_q: List[Optional[Tuple[float, float, float, float]]] = [None] * vc
        local_q_fr: List[Tuple[float, float, float, float]] = [
            (0.0, 0.0, 0.0, 1.0)
        ] * vc
        for _pass in range(vc):
            progressed = False
            for i in range(vc):
                if world_p[i] is not None:
                    continue
                lq = anim_q[i][fr] or bind_quat[i]
                local_q_fr[i] = lq
                if parent[i] < 0:
                    world_p[i] = anim_p[i][fr] or bind_pos[i]
                    world_q[i] = lq
                    progressed = True
                elif world_p[parent[i]] is not None and world_q[parent[i]] is not None:
                    pq = world_q[parent[i]]
                    pp = world_p[parent[i]]
                    if (
                        lq[0] * bind_quat[i][0]
                        + lq[1] * bind_quat[i][1]
                        + lq[2] * bind_quat[i][2]
                        + lq[3] * bind_quat[i][3]
                    ) < 0:
                        lq = (-lq[0], -lq[1], -lq[2], -lq[3])
                        local_q_fr[i] = lq
                    world_q[i] = _qnorm(_qmul(pq, lq))
                    r = _qrot(pq, rest[i])
                    world_p[i] = (pp[0] + r[0], pp[1] + r[1], pp[2] + r[2])
                    progressed = True
            if not progressed:
                break
        for i in range(vc):
            p = world_p[i] or bind_pos[i]
            # runaway FK (bad quat / missing parent) → fall back to bind
            if abs(p[0]) > 1e4 or abs(p[1]) > 1e4 or abs(p[2]) > 1e4:
                p = bind_pos[i]
                world_q[i] = bind_quat[i]
            positions[i][fr] = p
            world_quats[i][fr] = world_q[i] or bind_quat[i]
            local_quats[i][fr] = local_q_fr[i]
            # Parent-space translation: root = animated/bind; children = rest offset
            if parent[i] < 0:
                local_positions[i][fr] = anim_p[i][fr] or bind_pos[i]
            else:
                local_positions[i][fr] = rest[i]

    return Min2SkelClip(
        path=clip.path,
        bone_names=names,
        frame_count=fc,
        fps=float(fps),
        _positions=positions,
        _quats=world_quats,
        _local_quats=local_quats,
        _local_positions=local_positions,
    )



def load_min2_stick(path: str | Path) -> Min2SkelClip:
    """Load player-action MIN2 stick: positions_at + quats_at + matrices_at (RQ/LBS)."""
    clip = load_min2(path)
    if clip.kind != "skel_nametable":
        # still try — player bridge was misclassified as face before classify fix
        if not any(b.name.lower().startswith("bip") for b in clip.bones):
            raise ValueError(f"not a skel clip: kind={clip.kind} path={path}")
    return decode_skel_stick(clip)

def sniff_magic(path: str | Path) -> str:
    data = Path(path).read_bytes()[:4]
    return data.decode("ascii", errors="replace")


if __name__ == "__main__":
    import sys

    if len(sys.argv) < 2:
        print("usage: min2.py <file.ani>")
        raise SystemExit(2)
    p = Path(sys.argv[1])
    print("magic", sniff_magic(p))
    clip = load_min2(p)
    print(
        f"kind={clip.kind} bones={clip.bone_count} frames={clip.frame_count} "
        f"ver={clip.version} header_bones={clip.header_bone_count} root={clip.root_name!r}"
    )
    if clip.kind.endswith("nametable"):
        print(f"payload_off={clip.payload_offset:#x} payload_len={len(clip.payload)}")
        for b in clip.bones[:8]:
            print(f"  {b.name!r} fc={b.frame_count} fps={b.fps:.3f}")
        if len(clip.bones) > 8:
            print(f"  ... +{len(clip.bones) - 8} more")
    else:
        for b in clip.bones[:5]:
            print(
                f"  {b.name!r} vc={b.vertex_count} fc={b.frame_count} "
                f"fps={b.fps:.3f} normals={b.has_normals}"
            )
