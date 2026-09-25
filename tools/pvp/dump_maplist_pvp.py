#!/usr/bin/env python3
"""Categorize PvP-relevant rows of MapList.tab (GB18030).

Outputs UTF-8 TSV rows for each category + a summary text.
Usage: python tools/pvp/dump_maplist_pvp.py <MapList.tab> <outdir>
"""
from __future__ import annotations

import sys
from collections import Counter
from pathlib import Path

KEYWORDS = ["名剑", "竞技", "战场", "绝境", "阵营", "攻防", "帮会", "帮战", "沙暴",
            "吃鸡", "云湖", "九宫", "神农", "浮香", "三国", "尘归尘", "天池",
            "争霸", "据点", "城战", "龙门", "逐鹿", "会战", "盟", "谷", "侠", "秘境"]

OUT_COLS = ["ID", "Name", "DisplayName", "Type", "MaxCopyCount", "MinPlayerCount",
            "MaxPlayerCount", "KeepTime", "ReviveInSitu", "RevieCycle", "BanSkillMask",
            "BattleRelationMask", "CampType", "FightList", "NeedCampBuff", "bCanTongWar",
            "CanJoinBattleField", "BanUseItemMask", "CanJoinArena", "IsArenaMap",
            "NewCampFight", "CanJoinTongBattlefield", "InvalidBuffMask", "BanChangeTalent",
            "CanSprint", "QueueForSwitchWhenFull", "CampQueue", "IsTongWarMap",
            "IsBattlefield", "OperationMask", "IsTongLeagueMap", "bCanPK", "bCanDuel",
            "LimitTimes", "ResourcePath", "ScriptFile"]

INDICATORS = ["bCanPK", "bCanDuel", "CanJoinBattleField", "CanJoinArena", "IsArenaMap",
              "CanJoinTongBattlefield", "IsBattlefield", "IsTongWarMap", "IsTongLeagueMap",
              "NewCampFight", "bCanTongWar"]


def load(path: Path):
    text = path.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


def get(r, h, name):
    i = h.index(name)
    return r[i] if i < len(r) else ""


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    src, outdir = Path(sys.argv[1]), Path(sys.argv[2])
    outdir.mkdir(parents=True, exist_ok=True)
    header, rows = load(src)
    h = header
    cats: dict[str, list[list[str]]] = {k: [] for k in
        ["arena", "battlefield", "tongwar", "campfight", "openpk", "keyword", "nonzero_camp"]}

    for r in rows:
        name = get(r, h, "Name") + " " + get(r, h, "DisplayName")
        if get(r, h, "CanJoinArena") == "1" or get(r, h, "IsArenaMap") == "1":
            cats["arena"].append(r)
        if (get(r, h, "IsBattlefield") == "1" or get(r, h, "CanJoinBattleField") == "1"
                or get(r, h, "CanJoinTongBattlefield") == "1"):
            cats["battlefield"].append(r)
        if (get(r, h, "bCanTongWar") == "1" or get(r, h, "IsTongWarMap") == "1"
                or get(r, h, "IsTongLeagueMap") == "1"):
            cats["tongwar"].append(r)
        if (get(r, h, "NewCampFight") == "1" or get(r, h, "FightList") not in ("", "0")
                or get(r, h, "CampType") not in ("", "0")):
            cats["campfight"].append(r)
        if get(r, h, "bCanPK") == "1" or get(r, h, "bCanDuel") == "1":
            cats["openpk"].append(r)
        if any(k in name for k in KEYWORDS):
            cats["keyword"].append(r)
        if get(r, h, "CampType") not in ("", "0"):
            cats["nonzero_camp"].append(r)

    seen: set[str] = set()
    tsv = outdir / "maplist_pvp_rows.tsv"
    with tsv.open("w", encoding="utf-8") as f:
        cols = OUT_COLS
        f.write("CAT\t" + "\t".join(cols) + "\n")
        for cat, rs in cats.items():
            for r in rs:
                key = get(r, h, "ID") + "|" + cat
                if key in seen:
                    continue
                seen.add(key)
                f.write(cat + "\t" + "\t".join(get(r, h, c) for c in cols) + "\n")

    summ = outdir / "maplist_summary.txt"
    with summ.open("w", encoding="utf-8") as f:
        f.write(f"source: {src}\nrows={len(rows)} cols={len(header)}\n\n")
        for cat, rs in cats.items():
            f.write(f"[{cat}] {len(rs)} rows\n")
            for r in rs:
                f.write(f"   {get(r,h,'ID'):>5}  {get(r,h,'Name')} / {get(r,h,'DisplayName')}\n")
            f.write("\n")
        for col in ["Type", "CampType", "BattleRelationMask", "FightList", "OperationMask",
                    "BanSkillMask", "BanUseItemMask", "InvalidBuffMask", "IsBattlefield",
                    "CanJoinBattleField", "CanJoinArena", "IsArenaMap", "NewCampFight"]:
            c = Counter(get(r, h, col) for r in rows)
            f.write(f"dist {col}: {dict(sorted(c.items()))}\n")
    print(f"wrote {tsv}")
    print(f"wrote {summ}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
