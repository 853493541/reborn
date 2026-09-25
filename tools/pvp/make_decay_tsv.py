#!/usr/bin/env python3
"""Build proof/pvp/decay_and_controls.tsv from DecayType.tab + Buff.tab.

Usage: python tools/pvp/make_decay_tsv.py <Buff.tab> <DecayType.tab> <out.tsv>
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BUFF, DECAY, OUT = (Path(a) for a in sys.argv[1:4])


def load(p: Path):
    text = p.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


bhead, brows = load(BUFF)
bcol = {h: i for i, h in enumerate(bhead)}
dhead, drows = load(DECAY)


def c(r, name):
    i = bcol[name]
    return r[i] if i < len(r) else ""


def by_id(bid: str):
    for r in brows:
        if r[0] == bid:
            return r
    return None


def desc(bid: str) -> str:
    r = by_id(bid)
    if not r:
        return f"{bid}:<missing>"
    return f"{bid}:{c(r, 'Name')}"


# Representative player-facing examples chosen manually after scanning names.
EXAMPLES = {
    "0": ["424", "453", "14928", "2755", "5876", "533", "464"],
    "1": ["558", "706", "2289", "3466", "679"],
    "2": ["554", "675", "685", "686", "749", "13052"],
    "3": ["556", "852", "2832", "2833"],
    "4": ["682"],
    "5": ["445", "534", "557", "726", "50379", "4053"],
    "6": ["2522", "2523"],
    "7": ["3224", "4871", "9756"],
    "8": ["749", "1931", "1936", "5694", "51858", "2799"],
    "9": ["27038", "20346"],
    "-1": ["101"],
}

lines = [
    "# JX3 control / diminishing-returns catalog",
    "# sources:",
    "#   PROBE/logic-skill-prefixed-out/settings/skill/DecayType.tab",
    "#   PROBE/logic-skill-prefixed-out/settings/skill/Buff.tab",
    "#   PROBE/ability-matcher/extracted/scripts/skill/**  (DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE args,",
    "#     player.DelMultiGroupBuffByFunctionType(n), atImmunity n)",
    "# Frame unit = 1/16 s (160=10s, 320=20s, 96=6s).",
    "# Columns: DecayType | Name | DecayFrame | ImmunityFrame | seconds | example buffs (ID:Name) | notes",
]
for dr in drows:
    dt, dec, imm, name = dr[0], dr[1], dr[2], dr[3]
    ex = " ; ".join(desc(b) for b in EXAMPLES.get(dt, []))
    note = "no DR" if dt == "-1" else "DecayFrame == ImmunityFrame in this build"
    lines.append("\t".join([dt, name, dec, imm, f"{int(dec)/16:.0f}", ex, note]))

lines += [
    "",
    "# ---- control-category IDs used by atImmunity and DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE ----",
    "# (numeric enum; NOT the Buff.tab FunctionType string column)",
    "# id | inferred meaning | confidence | evidence",
    "2\t减速 Slow\tHIGH\t孤影化双 comment; 少林_被打免疫减速(7173) atImmunity=2; Buff.tab FunctionType Slow",
    "3\t恐惧 Fear\tHIGH\t无惧(8247) tooltip '免疫恐惧和控制效果' atImmunity={2,3,4,7,8,11}; absent from movement-only sets",
    "4\t定身 Root\tHIGH\t孤影化双 comment; 冰心诀-冻逝-免疫定身(700) atImmunity=4; Buff.tab FunctionType Halt",
    "5\t沉默 Silence\tHIGH\t免疫沉默(763) atImmunity=5; 转乾坤/锻骨诀 tooltips (免疫封内)",
    "6\t雷霆震怒 / special\tLOW\t孤影化双 comment '解除雷霆'; present in 天策 charge/mount immunity sets; 镇山河 actually deletes buff 682 directly",
    "7\t锁足 Root\tHIGH\t孤影化双 comment; 任驰骋 tooltip '解除自身锁足'; Buff.tab 锁足 buffs FunctionType Charm",
    "8\t眩晕 Stun\tHIGH\t孤影化双 comment; 短暂免疫眩晕(18246) atImmunity=8; Buff.tab FunctionType Stun/Daze",
    "9\t嘲讽 Taunt\tHIGH\tNPC通用_免疫嘲讽(4344)/通用_嘲讽免疫(8832) atImmunity=9",
    "11\t击倒 Knockdown\tHIGH\t摧蕊免疫击倒效果(20704)/风止韧性及免疫击倒(27102) atImmunity=11",
    "14\tunknown (newer)\tLOW\t月影护盾(17973) {5,14}; 寂灭蛊免疫(24353)/孔雀引免控(24414)",
    "15/16/17\tunknown (newer)\tLOW\t衍天宗荧惑守心生效效果(26065) per level",
    "-\t击退/被拉 displacement\t-\thandled by atKnockedBackRate / atRepulsedRate / atPullRate, not atImmunity",
]
OUT.write_text("\n".join(lines), encoding="utf-8")
print(f"written {OUT} rows={len(lines)}")
