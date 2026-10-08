#!/usr/bin/env python3
"""Extract each ability's authored mechanics program from its skill script.

Client truth (docs/pvp/JX3_PVP_BATTLE_RESEARCH.md, proof/pvp/attributes_and_damage.md):
the skill script (Lua, text or 5.1 bytecode) holds the level table and the
`skill.AddAttribute(mode, ATTRIBUTE_TYPE.<X>, args...)` program plus cast meta
(cost, weapon %, cooldown rows, prepare/channel frames).  This is what an ability
is "designed to do" (damage / buff / CC / movement / child skill).

Emits ability_picker/data/mechanics_f1.tsv:
  skillId name costMana dmg dmgRand weaponPct gcdRow normalCd prepareFrames
  channelFrame channelInterval ops
`ops` = "MODE|TYPE|arg0;..." (the ordered AddAttribute program).

Reproduce:
  .venv\\Scripts\\python.exe ability_picker\\tools\\build_mechanics.py
"""
from __future__ import annotations

import glob
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "data")
CACHE = (r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
         r"\jx3-web-map-viewer\cache-extraction\pakv4-probe")


def find_skills_tab():
    cands = glob.glob(os.path.join(CACHE, "**", "skills.tab"), recursive=True)
    for pref in ("ad-desc-probe-out", "logic-skill"):
        for c in cands:
            if pref in c:
                return c
    return cands[0] if cands else None


def script_index():
    idx = {}
    for p in glob.glob(os.path.join(CACHE, "**", "scripts"), recursive=True):
        sk = os.path.join(p, "skill")
        if not os.path.isdir(sk):
            continue
        for dp, _dn, fn in os.walk(sk):
            for f in fn:
                if f.lower().endswith(".lua"):
                    idx.setdefault(f.lower(), os.path.join(dp, f))
    return idx


def strip_comments(t):
    # remove block comments --[[ ... ]] / --[==[ ... ]==] first, then line comments.
    t = re.sub(r"--\[=*\[.*?\]=*\]", "", t, flags=re.S)
    return "\n".join(re.sub(r"--.*", "", l) for l in t.splitlines())


def safe_num(expr):
    expr = expr.strip()
    if re.fullmatch(r"[0-9eE.+\-*/ ()]+", expr):
        try:
            return round(float(eval(expr)), 4)   # digits/operators only (guarded)
        except Exception:
            return -1
    return -1


def first_num(text, name):
    m = re.search(r"(?<![A-Za-z])" + name + r"\s*=\s*([0-9][0-9eE.+\-*/ ]*)", text)
    return safe_num(m.group(1)) if m else -1


def call_arg(text, name):
    m = re.search(name + r"\s*\(\s*([0-9]+)", text)
    return int(m.group(1)) if m else -1


def call_arg2(text, name):
    """second numeric arg (SetNormalCoolDown(posi, id) -> id)."""
    m = re.search(name + r"\s*\(\s*[0-9]+\s*,\s*([0-9]+)", text)
    return int(m.group(1)) if m else -1


LENGTH_BASE = 64.0   # 1 尺 = 64 units (docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md)


def length_units(text, name):
    """`name = N * LENGTH_BASE` -> N*64 units; or a bare number."""
    m = re.search(r"(?<![A-Za-z])" + name + r"\s*=\s*([0-9.]+)\s*\*\s*LENGTH_BASE", text)
    if m:
        return round(float(m.group(1)) * LENGTH_BASE, 1)
    m = re.search(r"(?<![A-Za-z])" + name + r"\s*=\s*([0-9.]+)", text)
    return round(float(m.group(1)), 1) if m else -1


def parse_ops(text):
    ops = []
    for m in re.finditer(r"AddAttribute\s*\(([^)]*)\)", text):
        args = [a.strip() for a in m.group(1).split(",")]
        if len(args) < 2:
            continue
        mode = args[0].split(".")[-1]
        typ = args[1].split(".")[-1]
        arg0 = args[2] if len(args) > 2 else ""
        ops.append("%s|%s|%s" % (mode, typ, arg0))
    return ops


def parse_script(data):
    if data[:4] == b"\x1bLua":
        return None                      # bytecode: not decoded here (counted)
    t = strip_comments(data.decode("gb18030", "replace"))
    rec = {
        "costMana": first_num(t, "nCostMana"),
        "dmg": first_num(t, "nDamage"),
        "dmgRand": first_num(t, "nDamageRand"),
        "weaponPct": first_num(t, "nWeaponDamagePercent"),
        "gcdRow": call_arg(t, "SetPublicCoolDown"),
        "normalCd": call_arg2(t, "SetNormalCoolDown"),
        "prepareFrames": first_num(t, "nPrepareFrames"),
        "channelFrame": first_num(t, "nChannelFrame"),
        "channelInterval": first_num(t, "nChannelInterval"),
        "minRadius": length_units(t, "nMinRadius"),
        "maxRadius": length_units(t, "nMaxRadius"),
        "areaRadius": length_units(t, "nAreaRadius"),
        "height": length_units(t, "nHeight"),
        "ops": parse_ops(t),
    }
    return rec


def main():
    tab = find_skills_tab()
    if not tab:
        print("skills.tab not found", file=sys.stderr)
        return 2
    idx = script_index()
    roster = []
    for line in open(os.path.join(DATA, "roster_f1.tsv"), encoding="utf-8").read().splitlines()[1:]:
        c = line.split("\t")
        if c and c[0]:
            roster.append((c[0], c[1]))
    lines = open(tab, "rb").read().decode("gb18030", "replace").splitlines()
    sid_script = {}
    for line in lines[1:]:
        c = line.split("\t")
        if len(c) > 57 and c[1].isdigit():
            sid_script[c[1]] = c[57]

    rows = []
    textn = bcn = missn = 0
    for sid, name in roster:
        rel = (sid_script.get(sid, "") or "").replace("\\", "/").split("/")[-1].lower()
        path = idx.get(rel)
        rec = None
        if path:
            data = open(path, "rb").read()
            rec = parse_script(data)
            if rec is None:
                bcn += 1
            else:
                textn += 1
        else:
            missn += 1
        r = rec or {"costMana": -1, "dmg": -1, "dmgRand": -1, "weaponPct": -1,
                    "gcdRow": -1, "normalCd": -1, "prepareFrames": -1,
                    "channelFrame": -1, "channelInterval": -1, "ops": [],
                    "minRadius": -1, "maxRadius": -1, "areaRadius": -1, "height": -1}
        rows.append((sid, name, r))

    tsv = os.path.join(DATA, "mechanics_f1.tsv")
    with open(tsv, "w", encoding="utf-8", newline="\n") as f:
        f.write("skillId\tname\tcostMana\tdmg\tdmgRand\tweaponPct\tgcdRow\tnormalCd\t"
                "prepareFrames\tchannelFrame\tchannelInterval\tops\t"
                "minRadius\tmaxRadius\tareaRadius\theight\n")
        for sid, name, r in rows:
            f.write("\t".join([sid, name, str(r["costMana"]), str(r["dmg"]), str(r["dmgRand"]),
                               str(r["weaponPct"]), str(r["gcdRow"]), str(r["normalCd"]),
                               str(r["prepareFrames"]), str(r["channelFrame"]),
                               str(r["channelInterval"]), ";".join(r["ops"]),
                               str(r["minRadius"]), str(r["maxRadius"]),
                               str(r["areaRadius"]), str(r["height"])]) + "\n")
    print("mechanics: %d abilities (%d text, %d bytecode, %d missing) -> %s"
          % (len(rows), textn, bcn, missn, tsv))
    for sid in ("65029", "65076"):
        for s, n, r in rows:
            if s == sid:
                print("  %s %s cost=%s dmg=%s weaponPct=%s gcd=%s prepare=%s ops=%d"
                      % (s, n, r["costMana"], r["dmg"], r["weaponPct"], r["gcdRow"],
                         r["prepareFrames"], len(r["ops"])))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
