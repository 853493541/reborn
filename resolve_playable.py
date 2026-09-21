"""Resolve catalog rows / .tani → local skeletal .ani paths (MIN2 playable).

Dev2 surface for Dev3/Dev4::

    from resolve_playable import resolve_playable_path, load_playable_clip

    path = resolve_playable_path(row)          # or a Path / str
    clip = load_playable_clip(path)           # Min2SkelClip (MIN2 only)
"""
from __future__ import annotations

from pathlib import Path
from typing import Any, Mapping, Optional, Union

import tani
from min2 import load_min2_stick, sniff_magic

ROOT = Path(__file__).resolve().parent
PLAYER_ROOT = ROOT / "samples" / "player"
MOVES_DIR = PLAYER_ROOT / "moves"

RowOrPath = Union[Mapping[str, Any], str, Path]


class ResolveError(FileNotFoundError):
    """No local skeletal .ani could be resolved for the given row/path."""


class UnsupportedClipError(ValueError):
    """Clip magic is not MIN2 (e.g. ANIM / MINA) — Dev2 surface is MIN2-only."""


def _as_mapping(row_or_path: RowOrPath) -> tuple[Optional[dict], Optional[Path]]:
    if isinstance(row_or_path, Mapping):
        return dict(row_or_path), None
    return None, Path(row_or_path)


def _player_root(root: Path) -> Path:
    """Accept repo root or samples/player as ``root``."""
    root = Path(root).resolve()
    if (root / "samples" / "player").is_dir():
        return root / "samples" / "player"
    if root.name == "player" and (root / "moves").is_dir():
        return root
    if (root / "moves").is_dir() and (root / "catalog").is_dir():
        return root
    return root / "samples" / "player"


def _search_dirs(player_root: Path) -> list[Path]:
    moves = player_root / "moves"
    return [
        moves / "fenglaiwushan",
        moves,
        player_root,
    ]


def _find_ani_by_name(name: str, search: list[Path]) -> Optional[Path]:
    if not name:
        return None
    name_l = Path(name).name.lower()
    if not name_l.endswith(".ani"):
        name_l = name_l + ".ani" if "." not in name_l else name_l
    for d in search:
        if not d.is_dir():
            continue
        for p in d.rglob("*.ani"):
            if p.name.lower() == name_l:
                return p
    # strip body prefix f1_
    bare = Path(name).name
    if bare.lower().startswith("f1_"):
        bare = bare[3:]
        return _find_ani_by_name(bare, search)
    return None


def _resolve_existing(path: Path) -> Optional[Path]:
    if path.is_file():
        return path.resolve()
    return None


def _from_tani(tp: Path, search: list[Path]) -> Optional[Path]:
    try:
        info = tani.parse_tani(tp)
    except Exception:
        return None
    return tani.resolve_local_ani(info, search)


def _heuristic_flws(row: dict, search: list[Path]) -> Optional[Path]:
    raw = (
        row.get("filename")
        or row.get("anim_file")
        or row.get("label")
        or row.get("local_path")
        or ""
    )
    name = Path(str(raw).replace("\\", "/")).name
    prefer_charge = "蓄力" in str(raw) or "charge" in str(raw).lower()
    body = (row.get("body") or "F1").lower()
    cands: list[Path] = []
    for d in search:
        if not d.is_dir():
            continue
        for p in d.rglob("*.ani"):
            nl = p.name.lower()
            if body and body not in nl:
                continue
            if "s07cj" in nl and "15" in nl:
                cands.append(p)
    if not cands:
        # FLWS label without body prefix match
        blob = str(raw)
        if "风来吴山" in blob or "s07cj" in blob.lower():
            for d in search:
                if not d.is_dir():
                    continue
                for p in d.rglob("*.ani"):
                    if "s07cj" in p.name.lower() and "15" in p.name:
                        cands.append(p)
    if not cands:
        return None

    def score(p: Path) -> tuple:
        n = p.name
        nl = n.lower()
        s = 0
        if prefer_charge and "蓄力" in n:
            s += 100
        if (not prefer_charge) and "蓄力" not in n and nl.endswith("15.ani"):
            s += 90
        if "奇穴" in n:
            s += 5
        if "fenglaiwushan" in str(p).lower():
            s += 2
        return (-s, len(n))

    return sorted(cands, key=score)[0]


def _resolve_playable_rel(rel: str, player_root: Path, search: list[Path]) -> Optional[Path]:
    rel = (rel or "").replace("\\", "/").lstrip("/")
    if not rel:
        return None
    candidates = [
        player_root / rel,
        ROOT / rel,
        ROOT / "samples" / "player" / rel,
        MOVES_DIR / Path(rel).name,
        MOVES_DIR / "fenglaiwushan" / Path(rel).name,
    ]
    for cand in candidates:
        hit = _resolve_existing(cand)
        if hit is not None and hit.suffix.lower() == ".ani":
            return hit
    # basename search
    return _find_ani_by_name(Path(rel).name, search)


def resolve_playable_path(row_or_path: RowOrPath, root: Path = ROOT) -> Path:
    """Resolve a catalog row or path to an on-disk skeletal ``.ani``.

    Rules
    -----
    * Mapping with ``playable_path`` → that path under ``samples/player`` (or repo).
    * ``.ani`` path that exists → return as-is.
    * ``.tani`` → parse linked ani / catalog heuristics under ``moves/``.
    * Otherwise raise ``ResolveError`` with a clear message.
    """
    player_root = _player_root(root)
    search = _search_dirs(player_root)
    row, path = _as_mapping(row_or_path)

    if row is not None:
        # 1) Dev1 playable_path
        playable = row.get("playable_path") or ""
        if playable:
            hit = _resolve_playable_rel(str(playable), player_root, search)
            if hit is not None:
                return hit
            raise ResolveError(
                f"playable_path set but missing on disk: {playable!r} "
                f"(looked under {player_root})"
            )

        # 2) local_path / path field
        for key in ("local_path", "path", "anim_file"):
            raw = row.get(key) or ""
            if not raw:
                continue
            rel = str(raw).replace("\\", "/")
            name = Path(rel).name
            # absolute / relative file
            for cand in (
                Path(rel),
                player_root / rel,
                player_root / "moves" / name,
                MOVES_DIR / "fenglaiwushan" / name,
            ):
                if cand.is_file():
                    if cand.suffix.lower() == ".ani":
                        return cand.resolve()
                    if cand.suffix.lower() == ".tani":
                        hit = _from_tani(cand, search)
                        if hit is not None:
                            return hit
            if name.lower().endswith(".ani"):
                hit = _find_ani_by_name(name, search)
                if hit is not None:
                    return hit

        # 3) filename heuristics (FLWS / s07cj)
        hit = _heuristic_flws(row, search)
        if hit is not None:
            return hit

        label = row.get("label") or row.get("filename") or row.get("id") or row
        raise ResolveError(
            f"no skeletal .ani for catalog row {label!r} — "
            f"need playable_path or a local moves/*.ani"
        )

    assert path is not None
    path = Path(path)
    # bare relative under player
    if not path.is_file():
        hit = _resolve_playable_rel(str(path), player_root, search)
        if hit is not None:
            return hit
        raise ResolveError(f"path not found: {path}")

    path = path.resolve()
    suf = path.suffix.lower()
    if suf == ".ani":
        return path
    if suf == ".tani":
        hit = _from_tani(path, search)
        if hit is not None:
            return hit
        # FLWS tani without embedded match → heuristic from stem
        hit = _heuristic_flws({"filename": path.name, "body": "F1"}, search)
        if hit is not None:
            return hit
        raise ResolveError(
            f"tani has no linked local .ani: {path.name} "
            f"(ani_path from GATA not found under moves/)"
        )
    raise ResolveError(f"not a playable ani/tani: {path}")


def load_playable_clip(path: Union[str, Path]):
    """Load ``path`` as ``Min2SkelClip`` via ``min2.load_min2_stick``.

    MIN2 only — ANIM / other magics raise ``UnsupportedClipError``.
    """
    path = Path(path)
    if not path.is_file():
        raise ResolveError(f"clip file missing: {path}")
    magic = sniff_magic(path)
    if magic == "ANIM":
        raise UnsupportedClipError(
            f"ANIM clips are not supported on the Dev2 pose surface yet: {path.name}"
        )
    if magic == "MINA":
        raise UnsupportedClipError(
            f"MINA (.mesh.ani) is not supported here — use MIN2 skeletal .ani: {path.name}"
        )
    if magic != "MIN2":
        raise UnsupportedClipError(
            f"unsupported ani magic {magic!r} (need MIN2): {path.name}"
        )
    return load_min2_stick(path)


__all__ = [
    "ROOT",
    "ResolveError",
    "UnsupportedClipError",
    "resolve_playable_path",
    "load_playable_clip",
]
