#!/usr/bin/env python3
"""Verify the interrupted agent's PvP map/skill evidence against its sources.

Read-only on sources; writes a verification report to stdout.
Usage: python tools/pvp/verify_pvp_evidence.py
"""
from __future__ import annotations

import sys
from collections import Counter
from pathlib import Path

MAPLIST = Path(
    r"C:\Users\Zhibin Ren\Desktop\reborn-netcode\proof\netcode\mode_juejing"
    r"\pak_out2\MapList.tab"
)
SKILLS = Path(
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
    r"\jx3-web-map-viewer\cache-extraction\pakv4-probe"
    r"\logic-skill-prefixed-out\settings\skill\skills.tab"
)
MODES = Path("proof/pvp/modes")


def load(p: Path):
    text = p.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


def c(r: list[str], i: int) -> str:
    return r[i] if i < len(r) else ""


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    mh, maps = load(MAPLIST)
    by_id = {c(r, 0): r for r in maps}
    print(f"# MapList.tab rows={len(maps)} cols={len(mh)}")
    idx = {n: mh.index(n) for n in mh}

    flag_cols = ["IsArenaMap", "IsBattlefield", "IsTongWarMap", "IsTongLeagueMap",
                 "NewCampFight", "bCanTongWar", "CanJoinTongBattlefield", "CanJoinArena",
                 "CanJoinBattleField"]
    print("# flag distributions")
    for n in flag_cols:
        d = Counter(c(r, idx[n]) for r in maps)
        print(f"  {n}: {dict(sorted(d.items()))}")

    # 1) verify maplist_mode_flags.tsv rows against MapList.tab
    flags_path = MODES / "maplist_mode_flags.tsv"
    fh = flags_path.read_text(encoding="utf-8").splitlines()
    fcols = fh[0].split("\t")
    bad = 0
    ids = set()
    for line in fh[1:]:
        parts = line.split("\t")
        cats = parts[0].split(",")
        row = dict(zip(fcols, parts))
        mid = row["ID"]
        ids.add(mid)
        mr = by_id.get(mid)
        if mr is None:
            print(f"  MISSING map id {mid}")
            bad += 1
            continue
        for col in fcols[2:]:
            if col in ("ResourcePath", "ScriptFile", "CATS"):
                continue
            if col in ("bCanTongWar", "CanJoinTongBattlefield", "IsTongWarMap",
                       "IsTongLeagueMap", "OperationMask", "IsBattlefield"):
                pass
            mv = c(mr, idx[col])
            if row[col] != mv:
                print(f"  MISMATCH id={mid} {col}: tsv={row[col]!r} maplist={mv!r}")
                bad += 1
        # resource path may differ by .map/.jsonmap; only check basename
        rp = row.get("ResourcePath", "")
        if rp and Path(rp).stem not in Path(c(mr, idx["ResourcePath"])).stem:
            print(f"  MISMATCH id={mid} ResourcePath tsv={rp!r} maplist={c(mr, idx['ResourcePath'])!r}")
            bad += 1
        # category membership sanity
        if "ARENA" in cats and c(mr, idx["IsArenaMap"]) != "1":
            print(f"  BAD CAT ARENA id={mid}")
            bad += 1
        if "BF" in cats and c(mr, idx["IsBattlefield"]) != "1":
            print(f"  BAD CAT BF id={mid}")
            bad += 1
        if "TONGWAR" in cats and c(mr, idx["IsTongWarMap"]) != "1":
            print(f"  BAD CAT TONGWAR id={mid}")
            bad += 1
        if "TONGLEAGUE" in cats and c(mr, idx["IsTongLeagueMap"]) != "1":
            print(f"  BAD CAT TONGLEAGUE id={mid}")
            bad += 1
        if "CAMPFIGHT" in cats and c(mr, idx["NewCampFight"]) != "1":
            print(f"  BAD CAT CAMPFIGHT id={mid}")
            bad += 1
    print(f"# mode_flags rows={len(fh)-1} unique ids={len(ids)} mismatches={bad}")

    # 2) verify mapban tsv files against skills.tab
    sh, skills = load(SKILLS)
    s_by_id = {c(r, 1): r for r in skills}
    sidx = {n: sh.index(n) for n in sh}
    checks = [
        ("skills_mapban_arena_bit8.tsv", {8}),
        ("skills_mapban_bit2.tsv", {2}),
        ("skills_mapban_bit504.tsv", {504}),
        ("skills_mapban_bit520_8_512.tsv", {520}),
        ("skills_mapban_juejing_bit512.tsv", {512}),
    ]
    for fname, masks in checks:
        lines = (MODES / fname).read_text(encoding="utf-8").splitlines()
        header = lines[0].split("\t")
        bad = 0
        n = 0
        for line in lines[1:]:
            row = dict(zip(header, line.split("\t")))
            sid = row["SkillID"]
            n += 1
            sr = s_by_id.get(sid)
            if sr is None:
                print(f"  {fname}: MISSING skill {sid}")
                bad += 1
                continue
            for col, s_col in [("SkillName", "SkillName"), ("BelongSchool", "BelongSchool"),
                               ("KindType", "KindType"), ("FunctionType", "FunctionType")]:
                if c(sr, sidx[s_col]) != row[col]:
                    print(f"  {fname}: MISMATCH skill {sid} {col}: tsv={row[col]!r} tab={c(sr, sidx[s_col])!r}")
                    bad += 1
            m = c(sr, sidx["MapBanMask"])
            try:
                mv = int(m)
            except ValueError:
                mv = None
            if row["MapBanMask"] != m:
                print(f"  {fname}: MISMATCH skill {sid} MapBanMask tsv={row['MapBanMask']} tab={m}")
                bad += 1
            if mv is not None and masks and not any(mv & mask for mask in masks):
                print(f"  {fname}: BAD MASK skill {sid} MapBanMask={m} not any of {masks}")
                bad += 1
            if "IgnoreCamp" in row and row["IgnoreCamp"] and c(sr, sidx["IgnoreCamp"]) != row["IgnoreCamp"]:
                print(f"  {fname}: MISMATCH skill {sid} IgnoreCamp tsv={row['IgnoreCamp']!r} tab={c(sr, sidx['IgnoreCamp'])!r}")
                bad += 1
        print(f"# {fname}: rows={n} mismatches={bad}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
