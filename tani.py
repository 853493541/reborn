#!/usr/bin/env python3
"""GATA .tani parser — ported from jx3-web-map-viewer player-anim parseTaniBinary."""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class TaniInfo:
    path: Path
    version: int
    ani_path: str
    pss_paths: list[str] = field(default_factory=list)
    sfx_tags: list[str] = field(default_factory=list)
    sound_events: list[str] = field(default_factory=list)
    other_paths: list[str] = field(default_factory=list)
    error: str | None = None


def _decode_gb(data: bytes) -> str:
    for enc in ("gb18030", "gbk", "gb2312"):
        try:
            return data.decode(enc)
        except UnicodeDecodeError:
            continue
    return data.decode("latin1", errors="replace")


def _is_gb_start(b: int) -> bool:
    return 0xA1 <= b <= 0xFE


def parse_tani(path: Path | str) -> TaniInfo:
    path = Path(path)
    buf = path.read_bytes()
    if len(buf) < 8:
        return TaniInfo(path=path, version=0, ani_path="", error="buffer too small")
    magic = buf[:4].decode("latin1")
    if magic != "GATA":
        return TaniInfo(path=path, version=0, ani_path="", error=f"wrong magic {magic!r}")
    version = int.from_bytes(buf[4:8], "little")
    null_pos = 8
    while null_pos < len(buf) and buf[null_pos] != 0:
        null_pos += 1
    ani_path = _decode_gb(buf[8:null_pos])

    pss_paths: list[str] = []
    sfx_tags: list[str] = []
    sound_events: list[str] = []
    other_paths: list[str] = []

    i = null_pos + 1
    while i < len(buf):
        b = buf[i]
        if (0x20 <= b < 0x7F) or (_is_gb_start(b) and i + 1 < len(buf) and buf[i + 1] >= 0x40):
            end = i
            while end < len(buf):
                if 0x20 <= buf[end] < 0x7F:
                    end += 1
                elif _is_gb_start(buf[end]) and end + 1 < len(buf) and buf[end + 1] >= 0x40:
                    end += 2
                else:
                    break
            if end - i >= 6:
                s = _decode_gb(buf[i:end])
                low = s.lower()
                if low.endswith(".pss") and (low.startswith("data\\") or low.startswith("data/") or "\\pss\\" in low or "/pss/" in low):
                    if s not in pss_paths:
                        pss_paths.append(s)
                elif s.startswith("New SFX Tag"):
                    if s not in sfx_tags:
                        sfx_tags.append(s)
                elif s.startswith("JX3_Skill") or s.startswith("JX3_"):
                    if s not in sound_events:
                        sound_events.append(s)
                elif low.endswith((".ani", ".mesh", ".mtl", ".tga", ".dds", ".wav")):
                    if s not in other_paths:
                        other_paths.append(s)
            i = max(end, i + 1)
        else:
            i += 1

    return TaniInfo(
        path=path,
        version=version,
        ani_path=ani_path,
        pss_paths=pss_paths,
        sfx_tags=sfx_tags,
        sound_events=sound_events,
        other_paths=other_paths,
    )


def resolve_local_ani(tani: TaniInfo, search_dirs: list[Path]) -> Path | None:
    """Map tani.ani_path basename onto a local .ani under search_dirs."""
    if not tani.ani_path:
        return None
    name = Path(tani.ani_path.replace("\\", "/")).name
    name_l = name.lower()
    for d in search_dirs:
        if not d.is_dir():
            continue
        for p in d.rglob("*.ani"):
            if p.name.lower() == name_l:
                return p
    # soft match: stem without body prefix variants
    stem = Path(name).stem.lower()
    for d in search_dirs:
        if not d.is_dir():
            continue
        for p in d.rglob("*.ani"):
            if stem in p.stem.lower():
                return p
    return None
