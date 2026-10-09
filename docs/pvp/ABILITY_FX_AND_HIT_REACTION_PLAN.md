# Ability FX beyond the cast: landing-zone SFX, hit effect, be-hit anim + sound

**Status:** plan (not yet implemented). Area: pvp / netcode. Last verified: 2026-10-08.
**Trigger:** 五方行尽 (id 65149, PointArea Charm) plays more than a cast animation + cast
effect: it also has a **landing-zone SFX** at the AoE, a **be-hit effect** on each target, a
**be-hit animation** and a **hit sound**.

## 1. Client truth (what is authored)

The per-skill visual program lives in `Represent/skill/skill_caster_f1.txt` (one row per
SkillID/SkillLevel; extracted with the official `bin64\PakV4SfxExtract.exe`). Columns (0-based):

| col | field | meaning |
|---|---|---|
| 5 | PrepareCastSkillAnimationID | prepare anim |
| 6..9 | CastSkillAnimationID0..3 | cast anim(s) |
| 10..14 | *Block*EffectResultID | block effects (physics/solar/neutral/lunar/poison) |
| 15 | MissEffectResultID | miss |
| **16** | **HitEffectResultID** | **effect when the hit lands (被击)** |
| 17/18/19 | Dodge/Parry/CriticalStrike EffectResultID | |
| 20..24 | *Damage*EffectResultID | damage-number effects (physics used today = cast effect) |
| 25..29 | Reflect/Heal/StealLife/Absorb/Shield EffectResultID | |
| **30** | **AOESelectionSFXScale** | landing-zone SFX scale |
| **31** | **AOESelectionSFXFile** | **landing-zone SFX (.Sfx) at the AoE** |
| **32** | **IsPlayBehitAnimation** | play the target's be-hit animation |
| 33 | ImmediatelyMissileID | |
| **34..38** | **BeHittedByF1/F2/M1/M2/Npc** | **be-hit animation path per body (.tani/.ani)** |
| 39..43 | BeHittedBy*_DeathAdjust | |
| 44/45 | HitstunAniByNpc / WeakAniByNpc | |
| **46** | **被击动作播放速度** | be-hit animation speed |
| 47/48/49 | bSkillChannel / Haste / ResetAni | |
| **50** | **HitSoundID** | **hit sound id -> hit_target_sound.txt** |

Resolution chains (all client tables, in `cache-extraction\pakv4-probe`):
- **EffectResultID -> EffectID -> file**: `skill-tables-out/.../skill_result.txt` (col0->col2)
  then `skill_effect.txt` (col0 -> col6 path, col5 bone).
- **HitSoundID -> SoundEvent**: `skill-tables-out/.../hit_target_sound.txt`
  (`SoundID | TargetType | SoundEvent`; TargetType 0=body,1=wood,2=metal).
- **target material**: `cast-sound-out/Represent/common/behit_sound_type.txt`
  (`ModelFilePath | HitSoundType` 0/1/2).
- **be-hit shake**: `skill-tables-out/.../behit_shake.txt`
  (`RepresentID | OscillationCount | MaxOffset | Duration | OscillationType`).

### Worked example — 五方行尽 (65149), `skills.tab` row `绝境_五方行尽`
skill_caster_f1 row: `CastSkillAnimationID0=649`, `HitEffectResultID=368`,
`AOESelectionSFXScale=1`, `AOESelectionSFXFile=Data\source\other\特效\技能\SFX\释放\释放_纯阳攻击17.Sfx`,
`IsPlayBehitAnimation=1`, `Haste=1`. `PhysicsDamageEffectResultID` (col 20) is **empty**.
- Hit effect 368 -> effectID 368 -> `data/source/other/HD特效/技能/Pss/被击/C_纯阳两仪爆01.pss`
  (a **.pss** be-hit particle).
- BeHittedByF1 empty and `skills.tab` `HitStiffSkillMoveID` empty -> the be-hit animation is a
  **default** (per body), gated on by `IsPlayBehitAnimation=1`.
- HitSoundID empty -> default SoundID 0 -> `f2sgb11BangFaGongJi07`.

## 2. Current state (reborn v6)

`build_cast_chain.py` extracts only col 5/6/20 (prepare/cast anim + the *damage* effect) and the
client (`SkillCast`) plays the cast anim + one effect. Everything in §1 marked bold is **not
extracted and not played** — no landing-zone SFX, no hit effect, no be-hit animation, no hit sound.

## 3. Fix plan (phases)

- **FX1 — extract. DONE (2026-10-08).** `ability_picker/tools/build_hit_fx.py` emits
  `ability_picker/data/hit_fx_f1.tsv` (per roster skill: `aoeSfxScale`, `aoeSfxFile`,
  `hitEffectResultId`->`hitEffectPath`, `isPlayBehit`, `behitF1..Npc`, `behitSpeed`, `hitSoundId`)
  and copies the helper tables `hit_target_sound.tsv`, `behit_sound_type.tsv`, `behit_shake.tsv`
  into `data/`. Result: 154 abilities — 16 landing-SFX, 43 hit-effect, 28 play-behit, 0 hit-sound.
  65149: `aoeSfxFile=释放_纯阳攻击17.Sfx`, `hitEffectPath=C_纯阳两仪爆01.pss`, `isPlayBehit=1`.
- **FX2 — landing-zone SFX.** On a PointArea/TargetArea cast, spawn `aoeSfxFile` (scaled by
  `aoeSfxScale`) at the AoE centre when the cast commits (same point the damage resolves at).
  Reuse the existing effect-spawn path; add an "aoe" effect slot on `SkillCast`.
- **FX3 — hit effect. DONE (2026-10-08).** Client loads `hit_fx_f1.tsv` and, for each affected
  target in the mechanic apply loop, spawns `hitEffectPath` (a **.pss**) via `AddDummyModel` at
  the target. Verified: 65149 (PointArea, `RC_DUMMY_N=3`) -> `hit fx 65149 -> <target>
  ...\被击\C_纯阳两仪爆01.pss` on all 3 targets, no AV. (`.pss` plays via AddDummyModel; only
  `.Sfx` faults — see FX2.)
- **FX4 — be-hit animation.** When `isPlayBehit` is set, play `behitAnim<body>` (or the engine
  default when empty) on the target for the hit duration at `behitSpeed`. Wire via the target
  actor's animation state (target dummy in `Targeting.cs`).
- **FX5 — hit sound.** Play the `hit_target_sound` SoundEvent for `hitSoundId` (material-aware
  via `behit_sound_type`), default SoundID 0 when empty. Use the existing Wwise extraction
  (`fetch_sounds.py` -> `.wem`) if in-host playback is available.
- **FX6 — be-hit shake (optional).** Apply `behit_shake.txt` (count/offset/duration/type) to the
  target/camera on hit.

## 4. Open items / probes needed

- **Default be-hit animation** (BeHittedByF1 + HitStiffSkillMoveID both empty): find the engine
  default per body (probe the running client, or a represent table not yet extracted).
- **`.pss` playback** in the host (particle vs `.Sfx`); confirm before FX3.
- **Wwise sound playback** path in the host (do we have an audio player wired?).
- **behit_shake RepresentID** source (which column/global selects a shake row).

## Reproduce

```
# extract the caster table (never into C:\SeasunGame; use a temp dir)
.venv\Scripts\python.exe ability_picker\tools\build_cast_chain.py --body f1
# (FX1) .venv\Scripts\python.exe ability_picker\tools\build_hit_fx.py
```
Evidence for this plan: `skill_caster_f1.txt` (65149 row), `skill_result.txt`/`skill_effect.txt`
(hit effect 368), `hit_target_sound.txt`, `behit_sound_type.txt`, `behit_shake.txt`,
`skills.tab` (65149 cols 112-116 empty), `docs/netcode/JX3_SKILL_CAST_FLOW.md`.
