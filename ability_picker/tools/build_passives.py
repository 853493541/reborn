#!/usr/bin/env python3
"""Extract passive-ability effects from each skill script's `Apply()` function.

The 39 "script-only" roster abilities are passives (function Apply); their designed
behaviour runs when the skill is active. Parse the Apply body for the actions it
performs: child casts (CastSkill/CastSkillXYZ/CastSkillByXYZ), buffs (AddBuff/
SetBuff/player.AddBuff), and attribute adds.

Emits ability_picker/data/passives_f1.tsv: skillId name childCasts buffs attrs.

Reproduce:
  .venv\\Scripts\\python.exe ability_picker\\tools\\build_passives.py
"""
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


def script_path(sf):
    root = os.path.join(CACHE, "ability-matcher", "extracted", "scripts", "skill")
    p = os.path.join(root, sf.replace("\\", "/").replace("/", os.sep))
    return p if os.path.exists(p) else None


def apply_body(t):
    m = re.search(r"function\s+Apply\s*\(([^)]*)\)", t)
    if not m:
        return ""
    s = m.end()
    depth = 1
    # openers function/if/for/while (do pairs with for/while); closer end.
    for tok in re.finditer(r"\b(function|if|for|while|end)\b", t[s:]):
        if tok.group(1) == "end":
            depth -= 1
            if depth == 0:
                return t[s:s + tok.start()]
        else:
            depth += 1
    return t[s:]


def first_numeric_arg(call):
    for p in call.split(","):
        p = p.strip()
        if p.isdigit():
            return p
    return None


def main():
    tab = find_skills_tab()
    sfmap = {}
    for l in open(tab, "rb").read().decode("gb18030", "replace").splitlines()[1:]:
        c = l.split("\t")
        if len(c) > 57 and c[1].isdigit():
            sfmap[c[1]] = c[57]

    ids = [l.split("\t")[0] for l in open(os.path.join(DATA, "script_only_f1.tsv"),
                                          encoding="utf-8").read().splitlines()[1:] if l.strip()]
    rows = []
    tot_child = tot_buff = 0
    for sid in ids:
        sf = sfmap.get(sid)
        p = script_path(sf) if sf else None
        if not p:
            rows.append((sid, "", "", "", "", "", "", ""))
            continue
        t = open(p, "rb").read().decode("gb18030", "replace")
        body = apply_body(t)
        child = re.findall(r"CastSkill(?:XYZ|ByXYZ|ByDirection)?\s*\(\s*(\d+)", body)
        adds, dels = [], []
        for m in re.finditer(r"\.?(AddBuff|DelBuff\w*)\s*\(([^)]*)\)", body):
            b = first_numeric_arg(m.group(2))
            if not b:
                continue
            (adds if m.group(1) == "AddBuff" else dels).append(b)
        # heal: PCustomTherapy(...) with a `* <fraction>` amount (e.g. nMaxLife * 0.2)
        heal = ""
        for m in re.finditer(r"PCustomTherapy\s*\(([^)]*)\)", body):
            fm = re.findall(r"\*\s*(0?\.\d+|\d+\.\d+)", m.group(1))
            for x in fm:
                if 0.0 < float(x) < 1.0:
                    heal = x
                    break
            if heal:
                break
        attrs = len(re.findall(r"AddAttribute\s*\(", body))
        npcs = re.findall(r"CreateNpc\w*\s*\(\s*(\d+)", body)
        tot_child += len(child)
        tot_buff += len(adds)
        rows.append((sid, "", ";".join(dict.fromkeys(child)),
                     ";".join(dict.fromkeys(adds)), ";".join(dict.fromkeys(dels)), heal,
                     ";".join(dict.fromkeys(npcs)), str(attrs)))

    tsv = os.path.join(DATA, "passives_f1.tsv")
    with open(tsv, "w", encoding="utf-8", newline="\n") as f:
        f.write("skillId\tname\tchildCasts\taddBuffs\tdelBuffs\thealPct\tnpcs\tattrs\n")
        for r in rows:
            f.write("\t".join(r) + "\n")
    heals = sum(1 for r in rows if r[5])
    summons = sum(1 for r in rows if r[6])
    print("passives: %d rows (%d child casts, %d add-buffs, %d heals, %d summons) -> %s"
          % (len(rows), tot_child, tot_buff, heals, summons, tsv))
    for r in rows[:8]:
        print("  %s child=%s add=%s del=%s heal=%s npc=%s" % (r[0], r[2] or "-", r[3] or "-", r[4] or "-", r[5] or "-", r[6] or "-"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
