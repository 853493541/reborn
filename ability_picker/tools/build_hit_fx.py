#!/usr/bin/env python3
"""FX1: extract the per-skill FX program beyond the cast (landing SFX, hit effect,
be-hit animation, hit sound) from the shipped client tables.

Client truth (docs/pvp/ABILITY_FX_AND_HIT_REACTION_PLAN.md):
  Represent/skill/skill_caster_f1.txt   one row per SkillID/SkillLevel; columns:
      16 HitEffectResultID             -> hit (被击) effect
      30 AOESelectionSFXScale          -> landing-zone SFX scale
      31 AOESelectionSFXFile           -> landing-zone SFX (.Sfx at the AoE)
      32 IsPlayBehitAnimation          -> play the target's be-hit animation
      34..38 BeHittedByF1/F2/M1/M2/Npc -> be-hit animation path per body
      46 被击动作播放速度               -> be-hit animation speed
      50 HitSoundID                    -> hit sound id (hit_target_sound.txt)
  Represent/skill/skill_result.txt      EffectResultID -> EffectID
  Represent/skill/skill_effect.txt      EffectID -> path (+ bone)

Also copies the helper tables into data/:
  hit_target_sound.tsv   SoundID -> TargetType -> SoundEvent
  behit_sound_type.tsv   ModelFilePath -> HitSoundType (0/1/2)
  behit_shake.tsv        RepresentID -> count/offset/duration/type

Emits ability_picker/data/hit_fx_f1.tsv:
  skillId name aoeSfxScale aoeSfxFile hitEffectResultId hitEffectPath
  isPlayBehit behitF1 behitF2 behitM1 behitM2 behitNpc behitSpeed hitSoundId

Reproduce:
  .venv\\Scripts\\python.exe ability_picker\\tools\\build_hit_fx.py
"""
from __future__ import annotations

import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "data")
CACHE = (r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
         r"\jx3-web-map-viewer\cache-extraction\pakv4-probe")
BIN64 = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64"
PKV4 = os.path.join(BIN64, "PakV4SfxExtract.exe")

# skill_caster_f1.txt column indices
C_HIT_RESULT = 16
C_AOE_SCALE = 30
C_AOE_FILE = 31
C_PLAY_BEHIT = 32
C_BEHIT = {34: "f1", 35: "f2", 36: "m1", 37: "m2", 38: "npc"}
C_BEHIT_SPEED = 46
C_HIT_SOUND = 50


def rows(path):
    return [l.split("\t") for l in
            open(path, "rb").read().decode("gb18030", "replace").splitlines()]


def extract_caster_f1(dest):
    os.makedirs(dest, exist_ok=True)
    plist = os.path.join(dest, "pathlist.txt")
    open(plist, "wb").write(
        "Represent/skill/skill_caster_f1.txt\r\n".encode("gb18030", "replace"))
    subprocess.run([PKV4, plist, dest], cwd=BIN64, capture_output=True, timeout=300)
    return os.path.join(dest, "Represent", "skill", "skill_caster_f1.txt")


def load_result_map():
    p = os.path.join(CACHE, "skill-tables-out", "Represent", "skill", "skill_result.txt")
    m = {}
    for c in rows(p)[1:]:
        if len(c) >= 3 and c[0]:
            m[c[0]] = c[2]                    # EffectResultID -> EffectID
    return m


def load_effect_map():
    p = os.path.join(CACHE, "skill-tables-out", "Represent", "skill", "skill_effect.txt")
    m = {}
    for c in rows(p)[1:]:
        if len(c) >= 7 and c[0]:
            m[c[0]] = c[6].replace("/", "\\")  # EffectID -> path
    return m


def copy_helper(rel, out_name):
    src = os.path.join(CACHE, rel)
    if not os.path.exists(src):
        print("  missing helper: " + src, file=sys.stderr)
        return False
    raw = open(src, "rb").read().decode("gb18030", "replace")
    with open(os.path.join(DATA, out_name), "w", encoding="utf-8", newline="\n") as f:
        f.write(raw)
    return True


def main():
    roster = []
    for line in open(os.path.join(DATA, "roster_f1.tsv"), encoding="utf-8").read().splitlines()[1:]:
        c = line.split("\t")
        if c and c[0]:
            roster.append((c[0], c[1]))

    tmp = tempfile.mkdtemp(prefix="hit_fx_")
    caster = extract_caster_f1(tmp)
    if not os.path.exists(caster):
        print("skill_caster_f1.txt not extracted", file=sys.stderr)
        return 2
    result = load_result_map()
    effect = load_effect_map()

    # SkillID -> first row (level 1)
    by_sid = {}
    for c in rows(caster)[1:]:
        if c and c[0] and c[0] not in by_sid:
            by_sid[c[0]] = c

    def cell(c, i):
        return c[i] if i < len(c) else ""

    out = []
    for sid, name in roster:
        c = by_sid.get(sid)
        if not c:
            out.append([sid, name] + [""] * 12)
            continue
        erid = cell(c, C_HIT_RESULT).strip()
        eid = result.get(erid, "")
        hpath = effect.get(eid, "")
        out.append([sid, name,
                    cell(c, C_AOE_SCALE).strip(), cell(c, C_AOE_FILE).strip(),
                    erid, hpath, cell(c, C_PLAY_BEHIT).strip(),
                    cell(c, 34).strip(), cell(c, 35).strip(),
                    cell(c, 36).strip(), cell(c, 37).strip(),
                    cell(c, 38).strip(), cell(c, C_BEHIT_SPEED).strip(),
                    cell(c, C_HIT_SOUND).strip()])

    tsv = os.path.join(DATA, "hit_fx_f1.tsv")
    with open(tsv, "w", encoding="utf-8", newline="\n") as f:
        f.write("skillId\tname\taoeSfxScale\taoeSfxFile\thitEffectResultId\thitEffectPath\t"
                "isPlayBehit\tbehitF1\tbehitF2\tbehitM1\tbehitM2\tbehitNpc\tbehitSpeed\thitSoundId\n")
        for r in out:
            f.write("\t".join(r) + "\n")

    copy_helper(r"skill-tables-out\Represent\skill\hit_target_sound.txt", "hit_target_sound.tsv")
    copy_helper(r"cast-sound-out\Represent\common\behit_sound_type.txt", "behit_sound_type.tsv")
    copy_helper(r"skill-tables-out\Represent\skill\behit_shake.txt", "behit_shake.tsv")

    n_aoe = sum(1 for r in out if r[3])
    n_hit = sum(1 for r in out if r[5])
    n_behit = sum(1 for r in out if r[6] and r[6] != "0")
    n_snd = sum(1 for r in out if r[13])
    print("hit_fx: %d abilities (%d landing-SFX, %d hit-effect, %d play-behit, %d hit-sound) -> %s"
          % (len(out), n_aoe, n_hit, n_behit, n_snd, tsv))
    for r in out:
        if r[0] == "65149":
            print("  65149 %s aoe=%s hitFx=%s playBehit=%s sound=%s"
                  % (r[1], r[3].split("\\")[-1], r[5].split("\\")[-1], r[6], r[13]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
