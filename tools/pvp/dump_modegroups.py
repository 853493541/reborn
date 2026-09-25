#!/usr/bin/env python3
"""Dump verified PvP mode groups from MapList.tab (GB18030).

Usage: python tools/pvp/dump_modegroups.py <out.tsv>
"""
from __future__ import annotations

import sys
from pathlib import Path

MAPLIST = Path(
    r"C:\Users\Zhibin Ren\Desktop\reborn-netcode\proof\netcode\mode_juejing"
    r"\pak_out2\MapList.tab"
)
COLS = ["ID", "Name", "DisplayName", "Type", "MaxCopyCount", "MinPlayerCount",
        "MaxPlayerCount", "KeepTime", "ReviveInSitu", "RevieCycle", "BanSkillMask",
        "BattleRelationMask", "CampType", "FightList", "NeedCampBuff", "bCanTongWar",
        "CanJoinBattleField", "BanUseItemMask", "CanJoinArena", "IsArenaMap",
        "NewCampFight", "CanJoinTongBattlefield", "InvalidBuffMask", "BanChangeTalent",
        "CanSprint", "QueueForSwitchWhenFull", "CampQueue", "IsTongWarMap",
        "IsBattlefield", "OperationMask", "IsTongLeagueMap", "bCanPK", "bCanDuel",
        "ResourcePath", "ScriptFile"]
GROUPS = [
    ("ARENA_INSTANCE", "IsArenaMap", "1"),
    ("BATTLEFIELD_INSTANCE", "IsBattlefield", "1"),
    ("TONGWAR_INSTANCE", "IsTongWarMap", "1"),
    ("TONGLEAGUE_INSTANCE", "IsTongLeagueMap", "1"),
    ("NEWCAMPFIGHT", "NewCampFight", "1"),
    ("CAN_TONGWAR", "bCanTongWar", "1"),
    ("CAN_ARENA_QUEUE", "CanJoinArena", "1"),
    ("CAN_BATTLEFIELD_QUEUE", "CanJoinBattleField", "1"),
]


def load(p: Path):
    text = p.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


def c(r, i):
    return r[i] if i < len(r) else ""


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    hdr, rows = load(MAPLIST)
    out = [open(sys.argv[1], "w", encoding="utf-8")] if len(sys.argv) > 1 else [sys.stdout]
    f = out[0]
    f.write("GROUP\t" + "\t".join(COLS) + "\n")
    for gname, flag, val in GROUPS:
        i = hdr.index(flag)
        n = 0
        for r in rows:
            if c(r, i) == val:
                f.write(gname + "\t" + "\t".join(c(r, hdr.index(col)) for col in COLS) + "\n")
                n += 1
        print(f"# {gname} ({flag}={val}): {n} rows", file=sys.stderr)
    for s in out[1:]:
        s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
