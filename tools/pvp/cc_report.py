#!/usr/bin/env python3
"""Generate PvP buff/control/DR evidence tables from Buff.tab + DecayType.tab + MapList.tab.

Usage: python tools/pvp/cc_report.py <Buff.tab> <DecayType.tab> <MapList.tab> <outdir>
"""
from __future__ import annotations

import collections
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from decode_movestate import IDX1, bits  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BUFF, DECAY, MAPLIST, OUT = (Path(a) for a in sys.argv[1:5])
OUT.mkdir(parents=True, exist_ok=True)


def load(p: Path):
    text = p.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


bhead, brows = load(BUFF)
bcol = {h: i for i, h in enumerate(bhead)}
dhead, drows = load(DECAY)
mhead, mrows = load(MAPLIST)
mcol = {h: i for i, h in enumerate(mhead)}


def c(r, name):
    i = bcol[name]
    return r[i] if i < len(r) else ""


def masknames(v: str) -> str:
    try:
        return "|".join(IDX1.get(i, f"b{i}") for i in bits(int(v)))
    except ValueError:
        return ""


def num(v: str):
    try:
        return int(v)
    except ValueError:
        return None


# ---------------------------------------------------------------- decay table
lines = ["DecayType\tName\tDecayFrame\tImmunityFrame\tSeconds\tBuffCount\tExampleBuffs\tNotes"]
for dr in drows:
    dt = dr[0]
    n = sum(1 for r in brows if c(r, "DecayType") == dt)
    exs = []
    for r in brows:
        if c(r, "DecayType") == dt:
            exs.append(f"{r[0]}:{c(r, 'Name')}")
        if len(exs) >= 12:
            break
    dec = num(dr[1]) or 0
    lines.append("\t".join([
        dt, dr[3], dr[1], dr[2], f"{dec/16:.1f}", str(n), " ; ".join(exs),
        "DecayFrame=ImmunityFrame for all rows",
    ]))
# buffs with DecayType -1 / empty are non-decaying controls or no control
n_neg = sum(1 for r in brows if c(r, "DecayType") == "-1")
lines.append(f"-1\t(none)\t-\t-\t-\t{n_neg}\tID=101 策划默认项\tno DR")
(OUT / "decay_types.tsv").write_text("\n".join(lines), encoding="utf-8")

# ------------------------------------------------------------- CC family table
FAMILIES: dict[str, list[str]] = {
    "眩晕 Stun/Daze": ["眩晕", "晕眩", "击晕", "震晕"],
    "击倒 Knockdown": ["击倒", "倒地", "击飞"],
    "定身 Root(Halt)": ["定身", "定足"],
    "锁足 Root(Charm)": ["锁足", "刖足", "禁锢", "无法移动"],
    "减速 Slow": ["减速", "迟滞", "滞足"],
    "沉默 Silence": ["沉默", "封内", "不能施展内功"],
    "缴械 Disarm": ["缴械"],
    "恐惧 Fear": ["恐惧"],
    "嘲讽 Taunt": ["嘲讽", "强迫", "强制"],
    "昏睡/眠 Sleep": ["昏睡", "眠蛊", "深度睡眠"],
    "冰环/冰冻 Freeze": ["冰环", "冰封", "冻结"],
    "击退/拉 Knockback/Pull": ["击退", "拉拽", "被拉", "拖拽", "牵引"],
    "封轻功 Fly-ban": ["封轻功"],
    "混乱 Chaos": ["混乱"],
}
fam_lines = ["Family\tFunctionType(s)\tDecayType(s)\tMoveStateMask(s)\tMoveStateMask2(s)\tExampleBuffs"]
for fam, words in FAMILIES.items():
    sel = []
    for r in brows:
        nm = c(r, "Name")
        if any(w in nm for w in words):
            sel.append(r)
    fts = collections.Counter(c(r, "FunctionType") for r in sel)
    dts = collections.Counter(c(r, "DecayType") for r in sel)
    ms = collections.Counter(c(r, "MoveStateMask") for r in sel)
    ms2 = collections.Counter(c(r, "MoveStateMask2") for r in sel)
    top = [f"{r[0]}:{c(r, 'Name')}" for r in sel[:10]]
    fam_lines.append("\t".join([
        fam,
        ",".join(f"{k or '-'}({v})" for k, v in fts.most_common(6)),
        ",".join(f"{k or '-'}({v})" for k, v in dts.most_common(8)),
        ",".join(f"{k}({v})" for k, v in ms.most_common(5)),
        ",".join(f"{k}({v})" for k, v in ms2.most_common(5)),
        " ; ".join(top),
    ]))
(OUT / "cc_families.tsv").write_text("\n".join(fam_lines), encoding="utf-8")

# --------------------------------------------------------------- PvP keyword
PVP = ["减疗", "禁疗", "减伤", "无敌", "免伤", "免控", "反伤", "反弹", "破防",
       "化劲", "御劲", "名剑", "竞技", "战场", "绝境", "沙暴", "追命箭", "重创",
       "伤情", "疗伤", "命中", "闪避"]
pvp_lines = ["Keyword\tID\tName\tFunctionType\tDecayType\tMoveStateMask\tMoveStateMask2\tMapBanMask\tMapInvalidMask\tBeginAttrib1\tBeginValue1A\tCount\tInterval\tMaxStackNum\tScriptFile"]
seen = set()
for kw in PVP:
    n = 0
    for r in brows:
        if kw in c(r, "Name"):
            key = (kw, r[0])
            if key in seen:
                continue
            seen.add(key)
            pvp_lines.append("\t".join([
                kw, r[0], c(r, "Name"), c(r, "FunctionType"), c(r, "DecayType"),
                c(r, "MoveStateMask"), c(r, "MoveStateMask2"), c(r, "MapBanMask"),
                c(r, "MapInvalidMask"), c(r, "BeginAttrib1"), c(r, "BeginValue1A"),
                c(r, "Count"), c(r, "Interval"), c(r, "MaxStackNum"), c(r, "ScriptFile"),
            ]))
            n += 1
            if n >= 60:
                break
(OUT / "pvp_keyword_buffs.tsv").write_text("\n".join(pvp_lines), encoding="utf-8")

# ------------------------------------------------------ map masks correlation
map_lines = ["# MapList maps with InvalidBuffMask != 0 (id, name, mask, set bits)"]
for r in mrows:
    v = r[mcol["InvalidBuffMask"]] if mcol["InvalidBuffMask"] < len(r) else ""
    if v not in ("", "0"):
        name = r[1]
        mask_lines = [str(i) for i in bits(int(v))]
        map_lines.append(f"{r[0]}\t{name}\t{v}\t{','.join(mask_lines)}")
# buff mask value -> how many buffs, and which map bits intersect
ban_counter = collections.Counter(c(r, "MapBanMask") for r in brows)
inv_counter = collections.Counter(c(r, "MapInvalidMask") for r in brows)
map_lines.append("\n# Buff.tab MapBanMask values (top)")
for k, v in ban_counter.most_common(60):
    map_lines.append(f"{k!r}\t{v}")
map_lines.append("\n# Buff.tab MapInvalidMask values (top)")
for k, v in inv_counter.most_common(60):
    map_lines.append(f"{k!r}\t{v}")
(OUT / "mapmask_correlation.txt").write_text("\n".join(map_lines), encoding="utf-8")

# ------------------------------------------------------- stacking/periodic
STACK_IDS = ["418", "655", "631", "122", "367", "399", "203", "682", "556",
             "2522", "3224", "27038", "11605", "8067", "445", "422", "554",
             "558", "139", "104", "103", "244", "12465", "3466", "14928"]
st_lines = ["ID\tName\tFunctionType\tBuffType\tDecayType\tIsStackable\tMaxStackNum\tIsCountable\tCanAccumulate\tIsIntensityStackable\tIntensity\tCount\tInterval\tMinInterval\tMaxInterval\tActiveCoefficient\tCostSingleStack\tBeginAttrib1\tBeginValue1A\tBeginValue1B\tScriptFile"]
for r in brows:
    if r[0] in STACK_IDS:
        st_lines.append("\t".join(c(r, k) for k in st_lines[0].split("\t")))
(OUT / "stacking_examples.tsv").write_text("\n".join(st_lines), encoding="utf-8")

# ------------------------------------------------ dispel/steal/transfer flags
flag_lines = ["Column\tValue\tCount"]
for col in ["CanBeSteal", "CanTransfer", "Exclude", "GlobalExclude", "UniqueTarget",
            "Coexist", "CanAccumulate", "CostSingleStack", "Save", "OnFight"]:
    cnt = collections.Counter(c(r, col) for r in brows)
    for k, v in cnt.most_common():
        flag_lines.append(f"{col}\t{k!r}\t{v}")
steal = [r for r in brows if c(r, "CanBeSteal") == "1"]
flag_lines.append("\n# CanBeSteal=1 samples")
for r in steal[:40]:
    flag_lines.append(f"{r[0]}\t{c(r, 'Name')}\t{c(r, 'FunctionType')}\t{c(r, 'BuffType')}\t{c(r, 'DecayType')}")
trans = [r for r in brows if c(r, "CanTransfer") == "1"]
flag_lines.append("\n# CanTransfer=1 samples")
for r in trans[:40]:
    flag_lines.append(f"{r[0]}\t{c(r, 'Name')}\t{c(r, 'FunctionType')}\t{c(r, 'BuffType')}")
(OUT / "flags_catalog.txt").write_text("\n".join(flag_lines), encoding="utf-8")

print("done")
