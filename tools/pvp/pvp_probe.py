#!/usr/bin/env python3
"""Read-only probes over JX3 PvP-relevant tables (skills/CoolDownList/MapList).

Usage: python tools/pvp/pvp_probe.py <subcommand> [args]
Subcommands:
  maplist                 arena/battlefield/banmask overview
  maskbits                list maps with non-zero BanSkillMask
  skillmask <bit>         skills whose MapBanMask has <bit> set
  skill <name>            dump selected columns for a skill name match
  distribution            distributions of key skills.tab columns
  row <table> <id>        print row with matching first column
"""
from __future__ import annotations

import collections
import sys
from pathlib import Path

PROBE = Path(
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
    r"\jx3-web-map-viewer\cache-extraction\pakv4-probe"
)
SKILLS = PROBE / "logic-skill-prefixed-out" / "settings" / "skill" / "skills.tab"
CDLIST = Path(
    r"C:\Users\Zhibin Ren\Desktop\reborn-netcode\proof\netcode\mode_juejing\pak_out2\CoolDownList.tab"
)
MAPLIST = Path(
    r"C:\Users\Zhibin Ren\Desktop\reborn-netcode\proof\netcode\mode_juejing\pak_out2\MapList.tab"
)


def load(path: Path):
    text = path.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


def c(row: list[str], i: int) -> str:
    return row[i] if i < len(row) else ""


def maplist() -> None:
    hdr, rows = load(MAPLIST)
    print("== IsArenaMap=1 or CanJoinArena=1")
    for r in rows:
        if c(r, 42) == "1" or c(r, 41) == "1":
            print(f"id={c(r,0):>6} name={c(r,1)[:24]:<26} BanSkillMask={c(r,23):<8} "
                  f"IsArena={c(r,42)} CanJoinArena={c(r,41)} Type={c(r,8)} IsBF={c(r,66)}")
    print("\n== IsBattlefield=1")
    for r in rows:
        if c(r, 66) == "1":
            print(f"id={c(r,0):>6} name={c(r,1)[:24]:<26} BanSkillMask={c(r,23):<8} "
                  f"Type={c(r,8)} CanJoinBF={c(r,39)}")
    print("\n== non-zero BanSkillMask")
    for r in rows:
        if c(r, 23) not in ("", "0"):
            print(f"id={c(r,0):>6} name={c(r,1)[:26]:<28} BanSkillMask={c(r,23):<10} "
                  f"IsArena={c(r,42)} IsBF={c(r,66)} Type={c(r,8)}")


def skillmask(bit: int) -> None:
    hdr, rows = load(SKILLS)
    n = 0
    for r in rows:
        m = c(r, 67)
        if not m:
            continue
        try:
            v = int(m)
        except ValueError:
            continue
        if v & (1 << bit):
            print(f"{c(r,1):>8} {c(r,0)[:40]:<42} MapBanMask={m} CastMode={c(r,10)} "
                  f"Kind={c(r,5)} Func={c(r,6)} BelongSchool={c(r,9)}")
            n += 1
    print(f"# total {n} skills with bit {bit}")


def skill(name: str) -> None:
    hdr, rows = load(SKILLS)
    cols = [0, 1, 5, 6, 10, 19, 20, 24, 32, 33, 34, 35, 40, 41, 62, 67, 79, 81, 92, 100, 107, 110, 111]
    names = [hdr[i] for i in cols]
    print("\t".join(names))
    for r in rows:
        if name in c(r, 0):
            print("\t".join(c(r, i) for i in cols))


def distribution() -> None:
    hdr, rows = load(SKILLS)
    for name in ["CastMode", "IsChannelSkill", "IsPassiveSkill", "IsAutoTurn", "UseCastScript",
                 "KindType", "FunctionType", "UIType", "PlatformType", "MapBanMask", "CastMask",
                 "IgnoreSilence", "IgnoreControl", "IgnoreImmunityCast", "IgnoreRangeBlock",
                 "Use3DObstacle", "CheckReachable", "IsCheckStealth", "IgnoreCamp",
                 "TargetTypePlayer", "TargetTypeNpc", "AddPosture"]:
        i = hdr.index(name)
        d = collections.Counter(c(r, i) for r in rows)
        print(f"-- {name}[{i}]: {dict(sorted(d.items(), key=lambda x: -x[1])[:16])}")


def row(table: str, rid: str) -> None:
    p = Path(table)
    hdr, rows = load(p)
    for r in rows:
        if c(r, 0) == rid:
            for h, v in zip(hdr, r):
                print(f"{h}\t{v}")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    cmd = sys.argv[1] if len(sys.argv) > 1 else "maplist"
    if cmd == "maplist":
        maplist()
    elif cmd == "skillmask":
        skillmask(int(sys.argv[2]))
    elif cmd == "skill":
        skill(sys.argv[2])
    elif cmd == "distribution":
        distribution()
    elif cmd == "row":
        row(sys.argv[2], sys.argv[3])
    else:
        print(__doc__)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
