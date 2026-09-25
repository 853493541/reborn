#!/usr/bin/env python3
"""Write raw evidence dumps for the PvP cast/cooldown research."""
from __future__ import annotations

import sys
from collections import Counter, defaultdict
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
OUT = Path("proof/pvp/cast")


def load(p: Path):
    text = p.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


def c(r, i):
    return r[i] if i < len(r) else ""


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    OUT.mkdir(parents=True, exist_ok=True)
    hdr, skills = load(SKILLS)
    _ch, cds = load(CDLIST)
    _mh, maps = load(MAPLIST)

    # 1) GCD-relevant cooldown rows
    want = {"16", "503", "590", "1403", "1559", "2437", "2502", "938", "836", "3086",
            "444", "343", "2989", "2617", "2113", "2114"}
    lines = ["# CoolDownList.tab rows (ID, Duration, MinDuration, note, Usage, MaxCount, "
             "MaxDuration, CanBackup, MaxOverDraftCount, CanAccelerate, NeedSyncOB)"]
    for r in cds:
        if c(r, 0) in want:
            lines.append("\t".join(r))
    (OUT / "cdlist_gcd_rows.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")

    # 2) MapBanMask bits: counts + examples, cross-ref MapList BanSkillMask
    bit_ex: dict[int, list] = defaultdict(list)
    counts = Counter()
    for r in skills:
        v = c(r, 67)
        if not v:
            continue
        try:
            m = int(v)
        except ValueError:
            continue
        for b in range(32):
            if m & (1 << b):
                counts[b] += 1
                if len(bit_ex[b]) < 6:
                    bit_ex[b].append((c(r, 1), c(r, 0)[:34], v))
    lines = ["# MapBanMask bits (skills.tab col 67) with example skills"]
    for b in sorted(counts):
        lines.append(f"bit {b} (value {1<<b}): {counts[b]} skills")
        for sid, name, m in bit_ex[b]:
            lines.append(f"    {sid}\t{name}\tMapBanMask={m}")
    lines.append("")
    lines.append("# MapList.tab maps with non-zero BanSkillMask (col 23)")
    for r in maps:
        if c(r, 23) not in ("", "0"):
            lines.append(f"map {c(r,0)}\t{c(r,1)[:28]}\tBanSkillMask={c(r,23)}\t"
                         f"IsArenaMap={c(r,42)}\tIsBattlefield={c(r,66)}\tType={c(r,8)}")
    (OUT / "mapban_bits.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")

    # 3) hit-stiff examples
    idx = {n: hdr.index(n) for n in
           ["CauseBeatBreak", "CauseBeatBack", "HasCriticalStrike", "HitStiffDelayFrame",
            "HitStiffSkillMoveID", "HitStiffVelocityXY", "HitStiffAccelerateXY"]}
    lines = ["# skills with HitStiff* / CauseBeat* flags (skills.tab cols 26,27,112-115)"]
    n = 0
    for r in skills:
        if c(r, idx["HitStiffSkillMoveID"]) or c(r, idx["HitStiffDelayFrame"]) or c(r, idx["HitStiffVelocityXY"]):
            lines.append(f"{c(r,1)}\t{c(r,0)[:30]}\tkind={c(r,5)}\tfunc={c(r,6)}\t"
                         f"delay={c(r,idx['HitStiffDelayFrame'])}\tmove={c(r,idx['HitStiffSkillMoveID'])}\t"
                         f"vel={c(r,idx['HitStiffVelocityXY'])}\tacc={c(r,idx['HitStiffAccelerateXY'])}\t"
                         f"beatback={c(r,idx['CauseBeatBack'])}\tbeatbreak={c(r,idx['CauseBeatBreak'])}")
            n += 1
    (OUT / "hitstiff_examples.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")

    # 4) targeting-flag examples
    flag_names = ["Use3DObstacle", "CheckReachable", "IgnorePositiveShield", "IgnoreNegativeShield",
                  "IgnoreCamp", "IsCheckStealth", "IgnoreSilence", "IgnoreRangeBlock",
                  "IgnoreImmunityCast", "IgnoreControl", "NeedOutOfFight"]
    lines = ["# special cast-validation flags (skills.tab)"]
    for name in flag_names:
        i = hdr.index(name)
        d = Counter(c(r, i) for r in skills)
        lines.append(f"-- {name}[{i}] {dict(d.most_common(6))}")
        n = 0
        for r in skills:
            if c(r, i) == "1":
                lines.append(f"   {c(r,1)}\t{c(r,0)[:34]}\tcast={c(r,10)}\tscript={c(r,57)[:44]}")
                n += 1
                if n >= 8:
                    break
    (OUT / "targeting_flags.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("dumps written to", OUT)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
