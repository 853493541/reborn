#!/usr/bin/env python3
"""Build the v6 cast-chain dataset from the shipped client tables.

Client truth (docs/netcode/JX3_SKILL_CAST_FLOW.md / SKILL_CAST_FLOW.md):
  skills.tab                     skill id -> name / cast mode / range(in script)
  Represent/skill/skill_caster_<body>.txt
        SkillID, PrepareCastSkillAnimationID, CastSkillAnimationID0..3,
        PhysicsDamageEffectResultID ...            (the per-skill cast anim + effect)
  Represent/player/player_animation_<body>.txt   AnimationID -> .tani path
  Represent/skill/skill_result.txt               EffectResultID -> EffectID
  Represent/skill/skill_effect.txt               EffectID -> .Sfx path (+ bone)

Emits ability_picker/data/cast_chain_f1.json:
  { "body":"f1", "abilities":[ {skillId,name,animTani,prepareAnim,effectSfx,effectBone,animId,effectResultId}, ... ] }

skill_caster_*.txt are NOT in the map-viewer cache; pass --casters <dir> (already
extracted) or let the tool extract them from the paks via the official
bin64\\PakV4SfxExtract.exe into a temp dir (never into C:\\SeasunGame).
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile

CACHE_DEFAULT = (
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer"
    r"\cache-extraction\pakv4-probe"
)
PKV4 = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PakV4SfxExtract.exe"

TABLES = ["skill_caster_model.ini", "skill_caster_f1.txt", "skill_caster_m1.txt",
          "skill_caster_m2.txt", "skill_caster_f2.txt", "skill_caster_m3.txt",
          "skill_caster_invalid.txt", "skill_caster_npc.txt"]


def read_gbk(path):
    return open(path, "rb").read().decode("gb18030", "replace")


def rows(path):
    return [l.split("\t") for l in read_gbk(path).splitlines()]


def extract_casters(dest, body):
    os.makedirs(dest, exist_ok=True)
    paths = "Represent/skill/skill_caster_model.ini\r\n"
    for f in TABLES:
        paths += "Represent/skill/" + f + "\r\n"
    plist = os.path.join(dest, "pathlist.txt")
    open(plist, "wb").write(paths.encode("gb18030", "replace"))
    subprocess.run([PKV4, plist, dest], cwd=os.path.dirname(PKV4),
                   capture_output=True, timeout=300)
    return os.path.join(dest, "Represent", "skill")


def load_anim_map(cache, body):
    p = os.path.join(cache, "player-animation-out", "Represent", "player",
                     "player_animation_%s.txt" % body)
    m = {}
    if not os.path.exists(p):
        return m
    for c in rows(p)[1:]:
        if len(c) >= 7 and c[0] and c[6]:
            m[c[0]] = c[6].replace("/", "\\")
    return m


def load_result_map(cache):
    p = os.path.join(cache, "skill-tables-out", "Represent", "skill", "skill_result.txt")
    m = {}
    for c in rows(p)[1:]:
        if len(c) >= 3 and c[0]:
            m[c[0]] = c[2]          # EffectResultID -> EffectID
    return m


def load_effect_map(cache):
    p = os.path.join(cache, "skill-tables-out", "Represent", "skill", "skill_effect.txt")
    m = {}
    for c in rows(p)[1:]:
        if len(c) >= 7 and c[0]:
            m[c[0]] = (c[6].replace("/", "\\"), c[5] if len(c) > 5 else "")
    return m


def load_names(cache):
    p = os.path.join(cache, "ad-desc-probe-out", "settings", "skill", "skills.tab")
    m = {}
    for c in rows(p)[1:]:
        if len(c) > 1 and c[1]:
            m[c[1]] = c[0]
    return m


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", default=CACHE_DEFAULT)
    ap.add_argument("--body", default="f1")
    ap.add_argument("--casters", default="", help="dir containing skill_caster_<body>.txt")
    ap.add_argument("--out", default=os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data", "cast_chain_f1.json"))
    args = ap.parse_args()

    caster_dir = args.casters
    tmp = None
    if not caster_dir:
        tmp = tempfile.mkdtemp(prefix="skill_cast_")
        caster_dir = extract_casters(tmp, args.body)
    caster_file = os.path.join(caster_dir, "skill_caster_%s.txt" % args.body)
    if not os.path.exists(caster_file):
        print("skill_caster_%s.txt not found in %s" % (args.body, caster_dir), file=sys.stderr)
        return 2

    anim = load_anim_map(args.cache, args.body)
    result = load_result_map(args.cache)
    effect = load_effect_map(args.cache)
    names = load_names(args.cache)

    abilities = []
    for c in rows(caster_file)[1:]:
        if len(c) < 21 or not c[0]:
            continue
        sid = c[0]
        def cell(i):
            return c[i] if i < len(c) else ""
        cast0 = cell(6)
        prep = cell(5)
        erid = cell(20)
        anim_tani = anim.get(cast0, "")
        prep_tani = anim.get(prep, "")
        eid = result.get(erid, "")
        efx, bone = effect.get(eid, ("", ""))
        # keep rows that actually carry a client animation or effect
        if not anim_tani and not efx:
            continue
        abilities.append({
            "skillId": sid,
            "name": names.get(sid, ""),
            "animId": cast0,
            "animTani": anim_tani,
            "prepareAnimId": prep,
            "prepareAnim": prep_tani,
            "effectResultId": erid,
            "effectSfx": efx,
            "effectBone": bone,
        })

    out = {"body": args.body, "cache": args.cache, "count": len(abilities),
           "abilities": abilities}
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)
    # TSV sidecar (the C#5 client parses this without a JSON library)
    tsv = os.path.splitext(args.out)[0] + ".tsv"
    with open(tsv, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("skillId\tname\tanimTani\teffectSfx\teffectBone\n")
        for a in abilities:
            fh.write("\t".join([a["skillId"], a["name"], a["animTani"],
                                a["effectSfx"], a["effectBone"]]) + "\n")
    print("wrote %s: %d abilities (+ %s)" % (args.out, len(abilities), os.path.basename(tsv)))
    if tmp:
        import shutil
        shutil.rmtree(tmp, ignore_errors=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
