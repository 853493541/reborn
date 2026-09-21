#!/usr/bin/env python3
"""Patch/extend catalog with playable_path resolve + default clip list for 花萝 FBX."""
from __future__ import annotations

import json
import re
from pathlib import Path

# Import after path setup
ROOT = Path(__file__).resolve().parent
import sys
sys.path.insert(0, str(ROOT))
import catalog


def _stem_key(name: str) -> str:
    n = Path(name).stem.lower()
    # strip body prefix f1_
    if len(n) > 3 and n[2] == "_" and n[:2] in ("f1", "f2", "m1", "m2"):
        n = n[3:]
    # normalize
    n = n.replace("风来吴山", "").replace("奇穴", "").replace("悟", "").replace("皮肤", "")
    n = re.sub(r"_+", "_", n).strip("_")
    return n


def build_ani_index(moves_dir: Path) -> dict[str, Path]:
    """Map normalized stems -> preferred local .ani paths."""
    by: dict[str, list[Path]] = {}
    for p in moves_dir.rglob("*.ani"):
        if not p.is_file():
            continue
        keys = {
            p.stem.lower(),
            _stem_key(p.name),
            Path(p.name).stem.lower().replace("f1_", "").replace("f2_", ""),
        }
        # skill family keys
        m = re.search(r"(s07cj.?重剑技能15[^.]*)", p.stem, re.I)
        if m:
            keys.add(m.group(1).lower())
        if "蓄力" in p.name and "s07cj" in p.name.lower():
            keys.add("flws_charge")
            keys.add("风来吴山_蓄力")
        if "s07cj" in p.name.lower() and "15" in p.name and "蓄力" not in p.name:
            keys.add("flws_cast")
            keys.add("风来吴山_释放")
        for k in keys:
            by.setdefault(k, []).append(p)

    # prefer shorter path / fenglaiwushan / from_mapviewer f1
    def rank(p: Path) -> tuple:
        s = str(p).lower()
        return (
            0 if "fenglaiwushan" in s else 1,
            0 if "from_mapviewer" in s and "f1" in s else 1,
            0 if "蓄力" in p.name else 1,
            len(s),
        )

    out: dict[str, Path] = {}
    for k, paths in by.items():
        out[k] = sorted(paths, key=rank)[0]
    return out


def resolve_playable(anim_file: str, local_path: str, ani_index: dict[str, Path], player_root: Path) -> str:
    """Return relative playable .ani under samples/player, or ''."""
    # already ani local
    for cand in (local_path, anim_file):
        if not cand:
            continue
        name = Path(cand.replace("\\", "/")).name
        if name.lower().endswith(".ani"):
            # find on disk
            for p in (player_root / "moves").rglob(name):
                return p.relative_to(player_root).as_posix()
            # try without f1_ prefix
            if name.lower().startswith("f1_"):
                bare = name[3:]
                for p in (player_root / "moves").rglob(bare):
                    if p.suffix.lower() == ".ani":
                        return p.relative_to(player_root).as_posix()

    name = Path((local_path or anim_file).replace("\\", "/")).name
    stem = Path(name).stem.lower()
    # direct
    for key in (stem, _stem_key(name), "flws_charge" if "蓄力" in name else "", "flws_cast" if "s07cj" in stem and "15" in stem else ""):
        if not key:
            continue
        hit = ani_index.get(key)
        if hit:
            return hit.relative_to(player_root).as_posix()

    # FLWS family: any 蓄力 ani for 风来/s07cj tani
    if "风来吴山" in name or ("s07cj" in stem and "15" in stem):
        if "蓄力" in name:
            hit = ani_index.get("flws_charge") or ani_index.get("f1s07cj重剑技能15蓄力_奇穴".lower())
        else:
            hit = ani_index.get("flws_cast") or ani_index.get("f1s07cj重剑技能15".lower())
        if hit:
            return hit.relative_to(player_root).as_posix()
        # last resort charge
        hit = ani_index.get("flws_charge")
        if hit:
            return hit.relative_to(player_root).as_posix()
    return ""


def enrich_and_write():
    player_root = catalog.DEFAULT_MOVES_DIR.parent
    moves_dir = catalog.DEFAULT_MOVES_DIR
    catalog_dir = catalog.DEFAULT_CATALOG_DIR
    ani_index = build_ani_index(moves_dir)

    # rebuild base index
    info = catalog.build_index()
    ui_path = catalog_dir / "ui_lists.json"
    ui = json.loads(ui_path.read_text(encoding="utf-8"))

    def enrich_row(row: dict) -> dict:
        playable = resolve_playable(row.get("anim_file") or "", row.get("local_path") or "", ani_index, player_root)
        row["playable_path"] = playable
        row["playable"] = bool(playable)
        return row

    for key in ("f1_list", "f2_list", "m1_list", "m2_list"):
        ui[key] = [enrich_row(r) for r in ui.get(key, [])]
    ui["local_moves"] = [enrich_row(r) for r in ui.get("local_moves", [])]

    # default clip list for 花萝 F1 Play bar
    defaults = []
    prefer = [
        ("风来吴山·蓄力", "moves/f1s07cj重剑技能15蓄力_奇穴.ani", "charge"),
        ("风来吴山·释放", "moves/fenglaiwushan/f1s07cj重剑技能15.ani", "cast"),
        ("普通待机", "moves/from_mapviewer/f1/动作/f1b01ty普通待机01.ani", "idle"),
        ("行走", "moves/from_mapviewer/f1/动作/f1b02yd行走.ani", "walk"),
        ("奔跑", "moves/from_mapviewer/f1/动作/f1b02yd奔跑.ani", "run"),
        ("攻击03", "moves/from_mapviewer/f1/动作/f1b05ty攻击03.ani", "attack"),
    ]
    for label, rel, kind in prefer:
        p = player_root / rel
        if not p.is_file():
            # try alternate
            alt = list((player_root / "moves").rglob(Path(rel).name))
            if not alt:
                continue
            p = alt[0]
            rel = p.relative_to(player_root).as_posix()
        defaults.append(
            {
                "label": label,
                "kind": kind,
                "body": "F1",
                "playable_path": rel,
                "filename": Path(rel).name,
                "badge": "ANI",
                "preset": "花萝",
            }
        )

    # FLWS searchable rows with playable_path
    flws = []
    for r in ui["f1_list"]:
        fn = r.get("filename") or ""
        af = r.get("anim_file") or ""
        if "风来吴山" in fn or "风来吴山" in af:
            flws.append(enrich_row(dict(r)))
    for r in ui["local_moves"]:
        blob = f"{r.get('filename','')}{r.get('local_path','')}"
        if "风来" in blob or "s07cj" in blob.lower():
            flws.append(enrich_row(dict(r)))

    ui["default_clips_f1_hualuo"] = defaults
    ui["fenglaiwushan_playable"] = flws
    ui["resolve_note"] = (
        "TANI/ANI catalog rows include playable_path when a local skeletal .ani exists under samples/player/moves/. "
        "HD 风来吴山 .tani → f1s07cj重剑技能15蓄力_奇穴.ani (charge) or f1s07cj重剑技能15.ani (cast)."
    )

    ui_path.write_text(json.dumps(ui, ensure_ascii=False), encoding="utf-8")

    # dedicated small feed for Dev2/3/4
    feed = {
        "version": 2,
        "preset": "花萝",
        "body": "F1",
        "fbx_preset_dir": "samples/actor_presets/f1_hualuo",
        "default_clips": defaults,
        "fenglaiwushan": [
            {
                "id": r.get("id"),
                "filename": r.get("filename"),
                "badge": r.get("badge"),
                "anim_file": r.get("anim_file"),
                "local_path": r.get("local_path"),
                "playable_path": r.get("playable_path"),
                "kind_id": r.get("kind_id"),
                "sheath_type": r.get("sheath_type"),
                "is_loop": r.get("is_loop"),
            }
            for r in flws
            if r.get("playable_path")
        ],
        "first_bar": defaults[0] if defaults else None,
    }
    feed_path = catalog_dir / "playable_f1_hualuo.json"
    feed_path.write_text(json.dumps(feed, ensure_ascii=False, indent=2), encoding="utf-8")

    (catalog_dir / "SOURCES.txt").write_text(
        "\n".join(
            [
                "NOTE=Catalog + playable resolve for 花萝 FBX action playback.",
                "UI=ui_lists.json (rows now include playable_path / playable)",
                "FEED=playable_f1_hualuo.json — default_clips + 风来吴山 rows with skeletal .ani",
                "FIRST_BAR=风来吴山·蓄力 → moves/f1s07cj重剑技能15蓄力_奇穴.ani",
                "REBUILD=python catalog.py && python catalog_resolve_patch.py",
                "",
            ]
        ),
        encoding="utf-8",
    )

    print(json.dumps({
        "ui_lists": str(ui_path),
        "feed": str(feed_path),
        "default_clips": len(defaults),
        "flws_playable": sum(1 for r in flws if r.get("playable_path")),
        "first_bar": defaults[0] if defaults else None,
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    enrich_and_write()
