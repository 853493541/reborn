#!/usr/bin/env python3
"""Player animation catalog — same player_animation_*.txt tables as map-viewer.

Builds a searchable index for Ani Player lists (Anim Table / Tani Catalog / Serial)
and links staged samples under samples/player/moves/.
"""
from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Iterable, Optional


ROOT = Path(__file__).resolve().parent
DEFAULT_CATALOG_DIR = ROOT / "samples" / "player" / "catalog"
DEFAULT_MOVES_DIR = ROOT / "samples" / "player" / "moves"
DEFAULT_INDEX_PATH = ROOT / "samples" / "player" / "catalog" / "index.json"


@dataclass
class AnimEntry:
    id: int
    kind_id: int
    sheath_type: int
    anim_ratio: str
    anim_speed: str
    is_loop: int
    anim_file: str
    body: str
    badge: str = ""  # ANI | TANI | LOCAL
    local_path: str = ""  # relative path under samples/player if staged
    source: str = "table"  # table | local | serial

    @property
    def filename(self) -> str:
        return Path(self.anim_file.replace("\\", "/")).name

    @property
    def kind_badge(self) -> str:
        if self.badge:
            return self.badge
        n = self.filename.lower()
        if n.endswith(".tani"):
            return "TANI"
        if n.endswith(".ani"):
            return "ANI"
        return "FILE"


def _read_gb(path: Path) -> str:
    data = path.read_bytes()
    for enc in ("utf-8-sig", "gb18030", "gbk", "utf-8"):
        try:
            return data.decode(enc)
        except UnicodeDecodeError:
            continue
    return data.decode("latin1", errors="replace")


def load_player_animation_table(catalog_dir: Path, body: str) -> list[AnimEntry]:
    body = body.lower()
    path = catalog_dir / f"player_animation_{body}.txt"
    if not path.is_file():
        return []
    lines = _read_gb(path).splitlines()
    out: list[AnimEntry] = []
    for line in lines[1:]:
        if not line.strip():
            continue
        parts = line.split("\t")
        try:
            anim_id = int(parts[0])
        except (ValueError, IndexError):
            continue
        anim_file = (parts[6] if len(parts) > 6 else "").strip()
        if not anim_file:
            continue
        badge = "TANI" if anim_file.lower().endswith(".tani") else (
            "ANI" if anim_file.lower().endswith(".ani") else "FILE"
        )
        out.append(
            AnimEntry(
                id=anim_id,
                kind_id=int(parts[1]) if len(parts) > 1 and parts[1].isdigit() else 0,
                sheath_type=int(parts[2]) if len(parts) > 2 and parts[2].isdigit() else 0,
                anim_ratio=parts[3] if len(parts) > 3 else "",
                anim_speed=parts[4] if len(parts) > 4 else "",
                is_loop=int(parts[5]) if len(parts) > 5 and parts[5].isdigit() else 0,
                anim_file=anim_file,
                body=body.upper(),
                badge=badge,
                source="table",
            )
        )
    return out


def load_serial_table(catalog_dir: Path) -> list[dict]:
    path = catalog_dir / "player_serial_animation_table.txt"
    if not path.is_file():
        return []
    rows: list[dict] = []
    for line in _read_gb(path).splitlines()[1:]:
        if not line.strip():
            continue
        parts = line.split("\t")
        try:
            serial_id = int(parts[0])
        except (ValueError, IndexError):
            continue
        rows.append(
            {
                "serial_id": serial_id,
                "desc": parts[1] if len(parts) > 1 else "",
                "phase_a": parts[2] if len(parts) > 2 else "",
                "phase_b": parts[3] if len(parts) > 3 else "",
                "phase_c": parts[4] if len(parts) > 4 else "",
                "haste": parts[5] if len(parts) > 5 else "",
                "source": "serial",
            }
        )
    return rows


def _guess_body_from_name(name: str) -> str:
    n = name.lower()
    for b in ("f1", "f2", "m1", "m2"):
        if n.startswith(b) or f"_{b}" in n or f"/{b}/" in n.replace("\\", "/"):
            return b.upper()
    # prefixed copies like f1_F1s07...
    m = re.match(r"^(f1|f2|m1|m2)[_-]", n)
    if m:
        return m.group(1).upper()
    return "F1"


def scan_local_moves(moves_dir: Path, player_root: Path) -> list[AnimEntry]:
    if not moves_dir.is_dir():
        return []
    out: list[AnimEntry] = []
    next_id = 900000
    for path in sorted(moves_dir.rglob("*")):
        if not path.is_file():
            continue
        if path.suffix.lower() not in (".ani", ".tani"):
            continue
        if path.name.upper() == "SOURCES.TXT":
            continue
        rel = path.relative_to(player_root).as_posix()
        body = _guess_body_from_name(path.name)
        # try also parent path
        if body == "F1":
            body = _guess_body_from_name(str(path.relative_to(moves_dir)))
        badge = "TANI" if path.suffix.lower() == ".tani" else "ANI"
        out.append(
            AnimEntry(
                id=next_id,
                kind_id=0,
                sheath_type=0,
                anim_ratio="",
                anim_speed="",
                is_loop=0,
                anim_file=str(path),  # absolute for loaders; UI shows filename
                body=body,
                badge=badge,
                local_path=rel,
                source="local",
            )
        )
        next_id += 1
    return out


def link_locals(entries: list[AnimEntry], moves_dir: Path) -> None:
    """Attach local_path when a table row's basename exists under moves/."""
    if not moves_dir.is_dir():
        return
    by_name: dict[str, Path] = {}
    for p in moves_dir.rglob("*"):
        if p.is_file() and p.suffix.lower() in (".ani", ".tani"):
            by_name.setdefault(p.name.lower(), p)
            # also strip body prefix f1_
            n = p.name
            if len(n) > 3 and n[2] == "_" and n[:2].lower() in ("f1", "f2", "m1", "m2"):
                by_name.setdefault(n[3:].lower(), p)
    player_root = moves_dir.parent
    for e in entries:
        if e.local_path:
            continue
        key = Path(e.anim_file.replace("\\", "/")).name.lower()
        hit = by_name.get(key)
        if hit:
            e.local_path = hit.relative_to(player_root).as_posix()
            e.badge = "TANI" if hit.suffix.lower() == ".tani" else "ANI"


def find_moves(
    catalog_dir: Path,
    *,
    body: Optional[str] = None,
    needle: str = "风来吴山",
    moves_dir: Optional[Path] = None,
) -> list[AnimEntry]:
    bodies = [body.lower()] if body else ["f1", "f2", "m1", "m2"]
    needle_l = needle.lower()
    hits: list[AnimEntry] = []
    for b in bodies:
        for e in load_player_animation_table(catalog_dir, b):
            f = e.anim_file
            if needle in f or needle_l in f.lower():
                hits.append(e)
    if moves_dir and moves_dir.is_dir():
        for e in scan_local_moves(moves_dir, moves_dir.parent):
            blob = f"{e.filename} {e.local_path}"
            if needle in blob or needle_l in blob.lower() or "s07cj" in blob.lower() and "15" in blob:
                if needle in blob or needle_l in blob.lower() or "风来" in blob:
                    hits.append(e)
    seen: set[str] = set()
    uniq: list[AnimEntry] = []
    for e in hits:
        key = (e.anim_file or e.local_path).lower()
        if key in seen:
            continue
        seen.add(key)
        uniq.append(e)
    if moves_dir:
        link_locals(uniq, moves_dir)
    return uniq


def preferred_fenglaiwushan(catalog_dir: Path, body: str = "f1") -> Optional[AnimEntry]:
    hits = find_moves(catalog_dir, body=body, needle="风来吴山", moves_dir=DEFAULT_MOVES_DIR)
    if not hits:
        hits = []
        for e in load_player_animation_table(catalog_dir, body):
            fl = e.anim_file.lower()
            if "s07cj" in fl and "15" in fl and fl.endswith((".tani", ".ani")):
                hits.append(e)
    if not hits:
        return None

    def score(e: AnimEntry) -> tuple:
        n = (e.anim_file or e.filename).lower()
        return (
            0 if "风来吴山" in (e.anim_file or e.filename) else 1,
            0 if e.local_path else 1,
            0 if n.endswith(".tani") else 1,
            0 if "hd" in n else 1,
            0 if "蓄力" in (e.anim_file or e.filename) else 1,
            len(n),
        )

    return sorted(hits, key=score)[0]


def body_counts(catalog_dir: Path, moves_dir: Optional[Path] = None) -> dict[str, int]:
    counts = {}
    for b in ("f1", "f2", "m1", "m2"):
        counts[b.upper()] = len(load_player_animation_table(catalog_dir, b))
    if moves_dir and moves_dir.is_dir():
        for e in scan_local_moves(moves_dir, moves_dir.parent):
            counts[e.body] = counts.get(e.body, 0)  # don't inflate official counts
            counts[f"LOCAL_{e.body}"] = counts.get(f"LOCAL_{e.body}", 0) + 1
    return counts


def build_index(
    catalog_dir: Path = DEFAULT_CATALOG_DIR,
    moves_dir: Path = DEFAULT_MOVES_DIR,
    out_path: Path = DEFAULT_INDEX_PATH,
) -> dict:
    tables: dict[str, list[dict]] = {}
    all_entries: list[AnimEntry] = []
    for b in ("f1", "f2", "m1", "m2"):
        entries = load_player_animation_table(catalog_dir, b)
        link_locals(entries, moves_dir)
        tables[b.upper()] = [asdict(e) for e in entries]
        all_entries.extend(entries)

    locals_ = scan_local_moves(moves_dir, moves_dir.parent)
    serial = load_serial_table(catalog_dir)
    flws = find_moves(catalog_dir, body="f1", needle="风来吴山", moves_dir=moves_dir)

    index = {
        "version": 1,
        "catalog_dir": str(catalog_dir),
        "moves_dir": str(moves_dir),
        "body_counts": body_counts(catalog_dir, moves_dir),
        "tables": tables,
        "local_moves": [asdict(e) for e in locals_],
        "serial": serial,
        "search_demo": {
            "needle": "风来吴山",
            "body": "F1",
            "hits": [asdict(e) for e in flws[:50]],
            "preferred": asdict(preferred_fenglaiwushan(catalog_dir, "f1"))
            if preferred_fenglaiwushan(catalog_dir, "f1")
            else None,
        },
    }
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(index, ensure_ascii=False, indent=2), encoding="utf-8")
    # slim list-only JSON for UI (no full tables dump optional)
    slim = {
        "version": 1,
        "body_counts": index["body_counts"],
        "f1_list": [
            {
                "id": e["id"],
                "filename": Path(e["anim_file"].replace("\\", "/")).name,
                "badge": e.get("badge") or ("TANI" if e["anim_file"].lower().endswith(".tani") else "ANI"),
                "kind_id": e["kind_id"],
                "sheath_type": e["sheath_type"],
                "is_loop": e["is_loop"],
                "anim_file": e["anim_file"],
                "local_path": e.get("local_path") or "",
                "body": e["body"],
            }
            for e in tables["F1"]
        ],
        "f2_list": [
            {
                "id": e["id"],
                "filename": Path(e["anim_file"].replace("\\", "/")).name,
                "badge": e.get("badge") or ("TANI" if e["anim_file"].lower().endswith(".tani") else "ANI"),
                "kind_id": e["kind_id"],
                "sheath_type": e["sheath_type"],
                "is_loop": e["is_loop"],
                "anim_file": e["anim_file"],
                "local_path": e.get("local_path") or "",
                "body": e["body"],
            }
            for e in tables["F2"]
        ],
        "m1_list": [
            {
                "id": e["id"],
                "filename": Path(e["anim_file"].replace("\\", "/")).name,
                "badge": e.get("badge") or ("TANI" if e["anim_file"].lower().endswith(".tani") else "ANI"),
                "kind_id": e["kind_id"],
                "sheath_type": e["sheath_type"],
                "is_loop": e["is_loop"],
                "anim_file": e["anim_file"],
                "local_path": e.get("local_path") or "",
                "body": e["body"],
            }
            for e in tables["M1"]
        ],
        "m2_list": [
            {
                "id": e["id"],
                "filename": Path(e["anim_file"].replace("\\", "/")).name,
                "badge": e.get("badge") or ("TANI" if e["anim_file"].lower().endswith(".tani") else "ANI"),
                "kind_id": e["kind_id"],
                "sheath_type": e["sheath_type"],
                "is_loop": e["is_loop"],
                "anim_file": e["anim_file"],
                "local_path": e.get("local_path") or "",
                "body": e["body"],
            }
            for e in tables["M2"]
        ],
        "local_moves": index["local_moves"],
        "serial": serial,
        "search_demo_fenglaiwushan": index["search_demo"],
    }
    slim_path = catalog_dir / "ui_lists.json"
    slim_path.write_text(json.dumps(slim, ensure_ascii=False), encoding="utf-8")
    return {
        "index_path": str(out_path),
        "ui_lists_path": str(slim_path),
        "body_counts": index["body_counts"],
        "flws_hits": len(flws),
        "local_moves": len(locals_),
        "f1_rows": len(tables["F1"]),
    }


def search_list(entries: Iterable[dict], needle: str) -> list[dict]:
    n = needle.strip().lower()
    if not n:
        return list(entries)
    out = []
    for e in entries:
        blob = f"{e.get('id','')} {e.get('filename','')} {e.get('anim_file','')} {e.get('local_path','')}".lower()
        if n in blob or needle in str(e.get("anim_file", "")) or needle in str(e.get("filename", "")):
            out.append(e)
    return out


if __name__ == "__main__":
    info = build_index()
    print(json.dumps(info, ensure_ascii=False, indent=2))
    pref = preferred_fenglaiwushan(DEFAULT_CATALOG_DIR, "f1")
    if pref:
        print("preferred:", pref.filename, pref.badge, pref.local_path or pref.anim_file)
