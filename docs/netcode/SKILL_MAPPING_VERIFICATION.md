# Skill mapping verification (v5, 2026-10-05)

Per-ability verification of the staged processes (`ability_picker/data/ability_candidates.json`)
against the client's own tables. Control set: FLWS (风来吴山 1645/27967),
临时飞爪 (28031/28032/28033), 如意法 (32247/3968), and the rows where the
authoritative resolver disagrees with the removed heuristics.

Tables used (all extracted read-only from the client paks, see
`SKILL_DATA_RESEARCH.md` "Authoritative ability → animation mapping" for paths):

- `skills.tab` — name/id, `EffectPlayType`, col 57 `ScriptFile` (the skill's own Lua)
- `skill_tag.txt` / `skill_dash.txt` → `player_animation_f1.txt` (animation)
- `skill_chain.txt` — chain representation (source/dest bone, delay, PSS path)
- `cast-sound-out\Represent\common\character_sfx.txt` — `SkillID | ID | FilePath_Cast | FilePath_Target | ...`
- `Buff.tab` — `RepresentID` for buff-sourced representations
- the skill scripts (`...\ability-matcher\extracted\scripts\skill\**`)

## Verdicts

| Ability | Item | Authoritative evidence | Verdict |
|---|---|---|---|
| 风来吴山 | matched tani | `skill_tag` 1645 → anim 719 → `player_animation_f1` → `F1s07cj重剑技能15_风来吴山HD.tani` | **PROVEN** |
| 风来吴山 | anim step (base `f1s07cj重剑技能15.ani`) | same clip as the tani, tags stripped (tani = base + `.Sfx` tags) | **PROVEN variant** — the staged base `.ani` is the registered deviation for the ME `.Sfx` AV |
| 风来吴山 | sound 161340541 | tani SoundTag → Wwise event `...fenglaiwushanHD` → wem (`docs/audio/SOUND_PATH.md` Frida chain) | **PROVEN** (host WAV playback = registered deviation) |
| 风来吴山 | PSS `c_藏剑刀光01b.pss` | `character_sfx.txt` has 藏剑刀光 rows id 9933/10615/11002/12357/12358 (`c_藏剑刀光15/11/光01.pss`) — no `01b` row in the extracted table | **UNPROVEN** (staged; source not yet located) |
| 临时飞爪 | anim chain 释放/03b/缓冲 | `skill_dash` 28033 → 91076 → `F1s16lxg链技能03b_hd.tani` (dash); cast 28032/落地 via the script's `CastSkill`/`CastSkillXYZ` (28031 script read) | **PROVEN** |
| 临时飞爪 | chain PSS `s_锁链01.pss` | `skill_chain` 28032: `S_rh → S_fxmid`, 1000 ms, `data\source\other\hd特效\技能\pss\状态\s_锁链01.pss` | **PROVEN** |
| 临时飞爪 | sounds 62588785 / 697798714 | review-confirmed wems with source wav names (`skill-tani-sound-review.json`) | **PROVEN** (wem review layer) |
| 如意法 | script chain | `道具_如意法.lua`: `BindBuff(1, 4421)`, `AddBuff 4422/4432/4433/4434`; `Buff.tab` → those buffs `RepresentID = 10208` | **PARTIAL** — buff chain proven; represent 10208 data not yet extracted |
| 如意法 | anim step (base `f1smj10双刀buff04.ani`) | base clip of the dig-matched tani `F1smj10双刀buff04_清净心01.tani` | **UNPROVEN** until represent 10208 is read |
| 如意法 | PSS `m_明教清净心01.pss` | `character_sfx.txt` has `m_明教清净心爆发01.pss` (id 12564) — a different variant; no exact row | **UNPROVEN** (staged) |

## Resolver caveat found (dash layer)

`skill_dash.txt` maps a skill id to its movement (dash) animation. For the
authoritative-vs-heuristic conflicts the dash row is a **movement clip**, not the
cast clip:

| Ability | dash match | note |
|---|---|---|
| 跃潮斩波 | `dash:20053/85100` → `F1s15PL掌技能空03a_掌法HD.tani` | generic palm movement clip |
| 千蝶吐瑞 | `dash:2235/2789` → `f1swd08飞行01.tani` | flight clip (千蝶吐瑞 is a heal) |
| 太阴指 | `dash:228/347` → `F1s01wh点穴19_太阴HD.tani` | consistent with the named clip |
| 蛊虫献祭 / 玄水蛊 | tag rows → 粉身碎骨 / 万蛊蚀心 | the game's own tag rows; clip names differ from the ability names (plausible, needs per-ability confirmation) |

So dash-sourced matches must be read as "the id's dash clip", not "the cast
clip" until the per-ability represent chain (script → buff → represent) is read.
The dataset keeps the source label (`dash:<id>/<anim>`), and the staged process
is unaffected.

## Negative findings (recorded so they are not redone)

- `Represent\player\player_buff_animation_adjust.txt` (RL represent table, extracted
  from the paks via the documented `PakV4SfxExtract.exe`): 8092 rows,
  `Part | BuffID | AnimationID | AdjustAnimationID` - **no rows** for the 如意法
  buffs 4421/4422/4432/4433/4434. The buff representation is not in the RL
  buff-anim adjust layer.
- `Represent\RLDataLayer.json` is render settings (distances/LODs/counts), not a
  represent-id table.
- The logic-side `RepresentID 10208` table path is not among the strings of
  `JX3LogicEditOperationX64.dll` or any current extraction; it still needs to be
  located/extracted.

## Remaining extraction (to close the UNPROVEN rows)

1. `settings/represent/*` tables — resolve `RepresentID 10208` (如意法 buff
   representation) and the FLWS cast effect. Not in any current extraction dir.
2. `character_sfx.txt` skill→id binding — the table's `SkillID` column is 0 in
   all sampled rows; the binding likely goes through the skill scripts'
   effect ids (`SkillRealization.tab` / `recipeSkill.tab` are already extracted
   and unread).
3. The staged PSS files' locality in the ME host VFS (the earlier runs played
   them, so the ME host resolves them; the loose sandbox root does not).

## Reproduce

```powershell
# authoritative mapping regen (v5)
.venv\Scripts\python.exe ability_picker\tools\build_candidates.py
# control-set facts: scripts + tables (read-only probes, temp only)
#   skills.tab col 57 ScriptFile; skill_chain rows for 28032;
#   character_sfx.txt 藏剑刀光/锁链/明教清净 rows; Buff.tab 4421/4422/4432-4434
```

Last verified: 2026-10-05 (v5, `agent/skillv5-sandbox`).
