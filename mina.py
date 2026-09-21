"""MINA (.mesh.ani) parser — R2e header + R2f bone-major tracks."""
from __future__ import annotations

import struct
from dataclasses import dataclass
from pathlib import Path
from typing import List, Tuple

BONE_NAME_LEN = 30
FLOATS_PER_KEY = 15


@dataclass
class MinaClip:
    path: Path
    mesh_name: str
    tex_hint: str
    version: int
    bone_names: List[str]
    frame_count: int
    tracks: list

    @property
    def bone_count(self) -> int:
        return len(self.bone_names)

    def key(self, bone: int, frame: int):
        return self.tracks[bone][frame]

    def positions_at(self, frame: int) -> List[Tuple[float, float, float]]:
        f = max(0, min(frame, self.frame_count - 1))
        return [self.tracks[b][f][0] for b in range(self.bone_count)]


def _read_cstring(data: bytes, off: int) -> tuple[str, int]:
    end = data.index(0, off)
    return data[off:end].decode("ascii", errors="replace"), end + 1


def _score_name_table(data: bytes, start: int, bone_count: int) -> tuple[int, list[str]] | tuple[None, None]:
    names: list[str] = []
    for i in range(bone_count):
        raw = data[start + i * BONE_NAME_LEN : start + (i + 1) * BONE_NAME_LEN]
        if len(raw) < BONE_NAME_LEN:
            return None, None
        n = raw.split(b"\x00", 1)[0]
        if n and not all(32 <= c < 127 for c in n):
            return None, None
        names.append(n.decode("ascii", errors="replace"))
    nonempty = sum(1 for n in names if n)
    if nonempty == 0:
        return None, None
    joined = " ".join(names).lower()
    score = nonempty
    if "bip01" in joined:
        score += 100
    if names[0].startswith(("add_", "bip", "bfb", "fbr", "bone", "fb2", "fbr4")):
        score += 20
    # penalize mostly-empty
    if nonempty < bone_count * 0.5:
        score -= 50
    return score, names


def _locate_counts_and_names(data: bytes, after_tex: int) -> tuple[int, int, int, list[str]]:
    """
    After mesh/tex cstrings, bone/frame u32s may be shifted (hand tex is 1 byte shorter).
    Scan a small window for (bone_count, frame_count) + marker skip that yields a good name table
    and track payload size matching EOF (±16).
    """
    best = None  # (score, bone_count, frame_count, name_off, names)
    for delta in range(0, 8):
        off = after_tex + delta
        if off + 8 > len(data):
            break
        bone_count, frame_count = struct.unpack_from("<II", data, off)
        if not (1 <= bone_count <= 512 and 1 <= frame_count <= 4096):
            continue
        need_names = bone_count * BONE_NAME_LEN
        need_tracks = bone_count * frame_count * FLOATS_PER_KEY * 4
        for skip in range(0, 8):
            name_off = off + 8 + skip
            end = name_off + need_names + need_tracks
            if end > len(data) or end < len(data) - 16:
                continue
            score, names = _score_name_table(data, name_off, bone_count)
            if score is None:
                continue
            # prefer exact EOF fit (trailer NaN = +4)
            score += max(0, 8 - abs((len(data) - end) - 4))
            if best is None or score > best[0]:
                best = (score, bone_count, frame_count, name_off, names)
    if best is None:
        raise ValueError("Could not locate MINA bone/frame table")
    _score, bone_count, frame_count, name_off, names = best
    return bone_count, frame_count, name_off, names


def load_mina(path: str | Path) -> MinaClip:
    path = Path(path)
    data = path.read_bytes()
    if data[:4] != b"MINA":
        raise ValueError(f"Not MINA: {path}")

    version = struct.unpack_from("<I", data, 8)[0]
    off = 0x10
    mesh_name, off = _read_cstring(data, off)
    tex_hint, off = _read_cstring(data, off)
    bone_count, frame_count, name_off, bone_names = _locate_counts_and_names(data, off)

    track_off = name_off + bone_count * BONE_NAME_LEN
    nfloat = bone_count * frame_count * FLOATS_PER_KEY
    floats = struct.unpack_from("<" + "f" * nfloat, data, track_off)

    tracks: list = []
    idx = 0
    for _b in range(bone_count):
        frames = []
        for _f in range(frame_count):
            chunk = floats[idx : idx + FLOATS_PER_KEY]
            idx += FLOATS_PER_KEY
            pos = (chunk[0], chunk[1], chunk[2])
            scale = (chunk[3], chunk[4], chunk[5])
            quat = (chunk[6], chunk[7], chunk[8], chunk[9])
            weight = chunk[10]
            extra = (chunk[11], chunk[12], chunk[13], chunk[14])
            frames.append((pos, scale, quat, weight, extra))
        tracks.append(frames)

    return MinaClip(
        path=path,
        mesh_name=mesh_name,
        tex_hint=tex_hint,
        version=version,
        bone_names=bone_names,
        frame_count=frame_count,
        tracks=tracks,
    )


_PARENT_RULES = [
    ("bip01 pelvis", "bip01"),
    ("bip01 spine", "bip01 pelvis"),
    ("bip01 spine1", "bip01 spine"),
    ("bip01 spine2", "bip01 spine1"),
    ("bip01 neck", "bip01 spine2"),
    ("bip01 neck1", "bip01 neck"),
    ("bip01 head", "bip01 neck1"),
    ("bip01 l clavicle", "bip01 neck"),
    ("bip01 r clavicle", "bip01 neck"),
    ("bip01 l upperarm", "bip01 l clavicle"),
    ("bip01 r upperarm", "bip01 r clavicle"),
    ("bip01 l forearm", "bip01 l upperarm"),
    ("bip01 r forearm", "bip01 r upperarm"),
    # Player MIN2 often has foretwist instead of forearm.
    ("bip01 l foretwist", "bip01 l upperarm"),
    ("bip01 r foretwist", "bip01 r upperarm"),
    ("bip01 l foretwist1", "bip01 l foretwist"),
    ("bip01 r foretwist1", "bip01 r foretwist"),
    ("bip01 l hand", "bip01 l foretwist"),
    ("bip01 r hand", "bip01 r foretwist"),
    # Prefer real forearm when present (wins over foretwist).
    ("bip01 l hand", "bip01 l forearm"),
    ("bip01 r hand", "bip01 r forearm"),
    ("bip01 l thigh", "bip01 pelvis"),
    ("bip01 r thigh", "bip01 pelvis"),
    ("bip01 l calf", "bip01 l thigh"),
    ("bip01 r calf", "bip01 r thigh"),
    ("bip01 l foot", "bip01 l calf"),
    ("bip01 r foot", "bip01 r calf"),
]


def stick_edges(bone_names: List[str]) -> List[Tuple[int, int]]:
    lower = [n.lower().strip() for n in bone_names]
    index = {n: i for i, n in enumerate(lower)}
    edges = []
    for child, parent in _PARENT_RULES:
        if child in index and parent in index:
            edges.append((index[parent], index[child]))
    # fingers → hand
    for side in ("l", "r"):
        hand = index.get(f"bip01 {side} hand")
        if hand is None:
            continue
        for i, n in enumerate(lower):
            if n.startswith(f"bip01 {side} finger"):
                edges.append((hand, i))
    # Do not star-connect unparented twist/helper bones to root — that
    # drew explode-looking spokes and is not a real hierarchy edge.
    return edges
