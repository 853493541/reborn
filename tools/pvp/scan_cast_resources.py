#!/usr/bin/env python3
"""Scan JX3 skill scripts for cast/channel/resource/GCD fields (comment-aware).

- Plaintext Lua: strips -- line comments and --[[ ]] blocks, then captures active
  `skill.<Field> = value` assignments plus `tSkillData` per-level entries.
- Bytecode Lua: identifier presence only.

Output TSV: script, school, kind, fields_json, levels_json, calls, attrs
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path

FIELDS = [
    "nPrepareFrames", "nMinPrepareFrames", "nChannelFrame", "nMinChannelFrame",
    "nChannelInterval", "nMinChannelInterval", "bInstantChannel", "bIgnorePrepareState",
    "nCostMana", "nCostManaBasePercent", "nCostRage", "nCostEnergy", "nCostSunEnergy",
    "nCostMoonEnergy", "nCostSprintPower", "nCostLife", "nCostStamina",
    "nNeedRage", "nNeedEnergy", "nNeedSunEnergy", "nNeedMoonEnergy",
    "bIsAccumulate", "nNeedAccumulateCount", "nWeaponDamagePercent",
    "nMinRadius", "nMaxRadius", "nAreaRadius", "nAngleRange",
    "nMaxOverDraftCount", "nBeatBackRate", "nBrokenRate", "nBreakRate", "nDismountingRate",
    "nSkillBulletType", "nBulletVelocity", "bIsFormationSkill", "nBaseThreat",
    "nNeedPoseState", "nAttackAttenuationCof",
]
LEVEL_FIELDS = [
    "nDamage", "nDamageBase", "nDamageRand", "nCostMana", "nCostManaMaxPercent",
    "nCostRage", "nAddRage", "nCostEnergy", "nAddEnergy", "nCostSunEnergy",
    "nAddSunEnergy", "nCostMoonEnergy", "nAddMoonEnergy", "nCostSprintPower",
    "nPrepareFrames", "nChannelFrame", "nChannelInterval", "nMaxRadius", "nMinRadius",
    "nCostStamina", "nCostLife", "nCostItemType", "nCostItemIndex",
]
ASSIGN_RE = re.compile(r"skill\.([A-Za-z0-9_]+)\s*=\s*([^;\r\n]*)")
CALL_RE = re.compile(r"skill\.(SetPublicCoolDown|SetNormalCoolDown|SetCheckCoolDown)\s*\(([^)]*)\)")
ATTR_RE = re.compile(r"ATTRIBUTE_TYPE\.([A-Z0-9_]+)")


def strip_comments(text: str) -> str:
    text = re.sub(r"--\[\[.*?\]\]", " ", text, flags=re.S)
    text = re.sub(r"--[^\r\n]*", "", text)
    return text


def parse_levels(text: str) -> list[dict]:
    m = re.search(r"tSkillData\s*=\s*\{(.*?)\n\};", text, re.S)
    if not m:
        return []
    levels = []
    for line in m.group(1).splitlines():
        if "{" not in line:
            continue
        entry = {}
        for mm in re.finditer(r"([A-Za-z0-9_]+)\s*=\s*([^,}]+)", line):
            k, v = mm.group(1), mm.group(2).strip()
            if k in LEVEL_FIELDS:
                entry[k] = v
        if entry:
            levels.append(entry)
    return levels


def read_text(path: Path) -> str | None:
    b = path.read_bytes()
    if b[:4] == b"\x1bLua":
        return None
    try:
        return b.decode("gb18030")
    except UnicodeDecodeError:
        return None


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--root", type=Path, required=True)
    ap.add_argument("--out", type=Path, required=True)
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    rows = []
    plain = bytecode = 0
    for dirpath, _dirs, files in os.walk(args.root):
        for fn in files:
            if not fn.lower().endswith(".lua"):
                continue
            p = Path(dirpath) / fn
            rel = str(p.relative_to(args.root))
            school = rel.split(os.sep)[0] if os.sep in rel else ""
            raw = read_text(p)
            if raw is None:
                bytecode += 1
                b = p.read_bytes()
                calls = [c for c in ["SetPublicCoolDown", "SetNormalCoolDown", "SetCheckCoolDown",
                                     "OverDraft", "SetCoolDown", "ModifyCoolDown", "AddCDTime",
                                     "GetCDLeft", "ClearCDTime"] if c.encode() in b]
                flags = [f for f in FIELDS if f.encode() in b]
                rows.append({"script": rel, "school": school, "kind": "bytecode",
                             "fields": {}, "levels": [], "calls": calls, "attrs": flags})
                continue
            plain += 1
            text = strip_comments(raw)
            fields = {}
            for m in ASSIGN_RE.finditer(text):
                name = m.group(1)
                if name in FIELDS and name not in fields:
                    fields[name] = m.group(2).strip()
            levels = parse_levels(text)
            calls = [f"{m.group(1)}({m.group(2).strip()})" for m in CALL_RE.finditer(text)]
            attrs = sorted(set(ATTR_RE.findall(text)))
            rows.append({"script": rel, "school": school, "kind": "plain",
                         "fields": fields, "levels": levels, "calls": calls, "attrs": attrs})

    with args.out.open("w", encoding="utf-8") as f:
        f.write("script\tschool\tkind\tfields_json\tlevels_json\tcalls\tattrs\n")
        for r in rows:
            f.write(f"{r['script']}\t{r['school']}\t{r['kind']}\t"
                    f"{json.dumps(r['fields'], ensure_ascii=False)}\t"
                    f"{json.dumps(r['levels'], ensure_ascii=False)}\t"
                    f"{';'.join(r['calls'])}\t{';'.join(r['attrs'])}\n")
    print(f"plain={plain} bytecode={bytecode} rows={len(rows)} -> {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
