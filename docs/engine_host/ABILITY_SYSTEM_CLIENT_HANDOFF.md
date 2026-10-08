# Ability system in the client sandbox — handoff (2026-10-07)

State: **main's client + the ported ability system**, branch `agent/skillv5-sandbox`
(worktree `C:\Users\Zhibin Ren\Desktop\reborn-iso-skillv5-sandbox`), built as
`reborn_client_skillv5.exe` (window title `skill v5`). The user rejected the
MovieEditor `Skill.exe` sandbox line — do not revive it; the target is this client.

## What it is (files)

| Piece | Where |
|---|---|
| The whole ability system | `client/AbilitySystem.cs` (dataset load, P panel, cast state machine, tani playback, PSS follow, warm-up, guards) |
| Hooks | `client/RebornClient.cs`: `AbilitySystem.Init(...)` after the model setup; `1`/`P` in KeyDown; `AbilitySystem.Tick(now,px,py,pz,curYaw)` in the frame loop; `AbilitySystem.AnimActiveAt(now)` in the state-clip chain (do not remove — otherwise the state machine overwrites the cast anim) |
| Build | `client/build_client.cmd` includes `AbilitySystem.cs`; build with `RC_CLIENT_EXE=reborn_client_skillv5.exe` |
| Launcher | `tools/sandbox/run_skillv5.cmd` (RC_MAP + `RC_STARTUP=nodb` + title; optional `RC_ABILITY=<name>`) |
| Runtime data | `bin64\ability_picker\`: `ability_candidates.json` (processes), `skill_data.json`+`icons`, `sound\*.wav`, `sfx\*.sfx` (61 staged tags), `sfx_tags.json` |
| Data generator | `ability_picker/tools/build_candidates.py` (authoritative chain skills.tab→skill_tag/dash→player_animation_f1 + curated dig layer; `apply_tani_anim`; `TANI_BLACKLIST`; dormant `avoid_tani_pss_dup`) |
| Tani tag scan | `ability_picker/tools/scan_tani_tags.py` → `ability_picker/data/tani_pss_tags.json` (per ability: tani base `.ani` + embedded `.pss`/`.sfx` paths) |

Controls: **P** = picker panel (scales with the window), **1** = cast selected,
click an icon = select + cast. `RC_ABILITY=<name>` preselects (testing).
Diagnostics: `RC_PSS_FOLLOW=move|off|always|jitter` (PSS follow mode; default
`move` = re-add when the caster moved >32 u), `RC_PSS_SKIP=1` (skip the staged
PSS dummy — inspect what the tani's own tags render), `RC_CAST_CYCLE=<ms>`
(sweep: select + cast the next tani-playing ability every <ms>; the cast guard
serializes casts — used for the cross-cast effect probe, ran 45 s / 4 abilities
with no AV and no cross-cast replay observed).

## Verified working

- Cast: `reborn_20261007_064153.log` — 如意法: tani + PSS + sound, process survives.
- Warm-up: `sfx warm: 61/61 cached` (fixes the engine's first-time tag-create AV).
- 81 abilities load; **22 anim steps play the authored tani** (the other 58 whose
  tani already carries the staged PSS play the base `.ani` — see open item 1);
  122 PSS dummies.
- Abilities whose tani does NOT duplicate a staged PSS additionally spawn the
  engine's own tag effects from their tani (e.g. 五蕴皆空, 凌太虚, 剑主天地,
  圣明佑, 徐如林, 抱残式, 极乐引, 烈日斩, 百足, 花语酥心, 蛇影, 蛊虫献祭,
  银月斩, 雷震子, 驱夜断愁, 天绝地灭).

## Open work (priority order)

1. **"Effects replay wrong"** (named ability: **如意法**; user: "特效反复从头重放 /
   整个特效期间一直重放"). **Fixed 2026-10-07** — the replay source was a duplicated
   effect, not a timer:
   - The matched tani carries the effect records itself: `F1smj10双刀buff04_清净心01.tani`
     embeds `m_明教清净心01.pss` **and** `m_明教圣火_落地.pss` plus the 4 `.Sfx` tags.
     The dataset *also* staged `m_明教清净心01.pss` as a dummy -> playing the tani
     made the engine spawn the same PSS a second time (tag instance) next to the
     dummy. 58 of 61 dummy-carrying abilities had this duplicate; the game's own
     process is the tani alone, so the effect came from two sources.
   - Fix: `avoid_tani_pss_dup` (`AVOID_TANI_PSS_DUP=True`) plays the tani's base
     `.ani` for those abilities -> the staged PSS is the single visible source
     (game-truth process, no duplicate). Verified: 如意法 now = base `.ani` +
     dummy, one smooth 12.5 s play (`reborn_20261007_174334.log`). Trade-off: the
     tani's partial `.Sfx` sparks/trail are dropped for those 58 abilities (the
     PSS stays the main layer per the user directive); re-open if a fuller
     authored burst is wanted.
   - Not-a-restart evidence (recorded for re-open): follow re-add A/B (`move` vs
     `always`, 1 vs 3468 re-adds) is pixel-identical; a transform-jitter probe
     does not restart the PSS; full-density 200 ms captures show no timer replay.
   - `ability_picker/data/tani_pss_tags.json` (built by
     `ability_picker/tools/scan_tani_tags.py`) records every ability's tani base
     `.ani` + embedded `.pss`/`.sfx` paths (re-run after a game-data update).
   Next: user compares against the real game (the duplicate is gone; the tani
   `.Sfx` sparks/trail for those abilities are the known cost).
2. **70 animation matches changed** vs the v2-era dataset (the "wrong moves"
   report) — **verified 2026-10-07, no change needed**. Diff vs
   `agent/skillv2-sandbox`: only 5 abilities changed with both sides non-empty
   (玄水蛊, 跃潮斩波, 千蝶吐瑞, 太阴指, 蛊虫献祭); every v5 value is exactly the
   game's own table value (skill_tag/skill_dash → player_animation_f1), while the
   v2 values were `matchSource=name` (filename guessing — banned by AGENTS §6).
   Example: 玄水蛊 skill 3702 -> skill_tag anim 810 =
   `F1swd08蛊攻击01_万蛊蚀心.tani` (v5) vs the v2 name guess
   `f1sqg08wd蛊攻击01_玄水蛊hd.tani`. The dash-sourced ones (跃潮斩波 20053,
   千蝶吐瑞 2235, 太阴指 228, 鹤归孤山 1596) have **no skill_tag row** — the dash
   entry is the only authored animation, so it is the correct match, not a
   movement-layer mistake. The other ~65 diffs are v2 name-guesses vs v5
   unmatched/table matches.
3. **天绝地灭**: its tani AV'd the **ME Skill.exe** host (2026-10-06) → was
   `TANI_BLACKLIST`. **Fixed 2026-10-07**: re-tested on the client (the product)
   — single and back-to-back casts of 天绝地灭 with its tani complete cleanly
   (`reborn_20261007_180820.log`, `_181004.log`, no AV), so the ME-host AV does
   not reproduce on the client; `TANI_BLACKLIST` is now empty and 天绝地灭 plays
   its authored tani (verified `reborn_20261007_181151.log`). Re-add an entry
   only if a tani AVs the client (with repro log + cast context).
4. **Sequential-tani AV**: casting many different abilities in a row AVs the
   engine's tag manager (cast guard + restart exists; root cause unfixed).
5. **Keep the PSS dummy**: the tani's tags render only part of the effects
   (sparks/trail); the PSS is the main visible layer. Dropping it (2026-10-07
   regression) removed the visible effects — reverted.
6. The ME `Skill.exe` sandbox = parked (user rejected); `ability_sandbox/*`
   leftovers = WIP only.

## User directives (must honor)

- Never change the client's window size or camera settings in launches
  (no `RC_FULLSCREEN`, no `RC_CAM_DIST`).
- No MovieEditor-sandbox advances; the product = main's client + abilities.
- The user compares against the real game; "wrong" reports need a named ability.

## Commands

```powershell
# build + launch
$env:RC_CLIENT_EXE='reborn_client_skillv5.exe'; client\build_client.cmd
tools\sandbox\run_skillv5.cmd                       # or: RC_ABILITY=如意法 for a test

# dataset regen (after editing build_candidates.py)
.venv\Scripts\python.exe ability_picker\tools\build_candidates.py
Copy-Item ability_picker\data\ability_candidates.json C:\SeasunGame\MovieEditor\bin64\ability_picker\ -Force
```

- Logs: `C:\SeasunGame\MovieEditor\bin64\reborn_out\reborn_<ts>.log` (client),
  `MovieEditor\bin64\Skill\out\Skill_*.log` (parked ME sandbox).
- **Env caveat 2026-10-07**: the cropped sandbox map (`C:\jx3tmp\...龙门寻宝_s`)
  started crashing the client at startup in `KGEngineCLR.Render()`
  (`KG3DEngineDX11EX64.dll+0x12282b3`, WER APPCRASH) while other agents' clients
  ran; the **full map** (`RC_MAP` unset) runs clean, so ability work can be
  tested there. Not a code issue (same exe ran the sandbox clean at 17:43).
- Shim: `bin64\sfx_shim.dll` (source `native/sfx_shim.cpp`, build
  `native/build_sfx_shim.cmd`) — the warm-up path.
- Scripted key post: PostMessage WM_KEYDOWN/UP to the client window (the capture
  tool `-Key 1` works; the synthetic key can repeat-cast — the guard blocks it).
- Captures: `tools/proof/capture_window.ps1` — in this multi-agent machine
  CopyFromScreen returns whatever window is on top; prefer the app's own engine
  screenshots (`RC_SHOTS` on the ME sandbox) or the user's eyes.

## Branch / commits

`agent/skillv5-sandbox` (main `066c6c8` merged in). Latest ability commits:
`244790e` (port), `a40cf4e` (warm-up/guards/panel), `9644d83` (panel host),
`9a536eb` (PSS kept), `ebb978a` (panel scaling). Never push; merge to main only
when the subject is complete (`merge-finalize` skill).

Last verified: 2026-10-07.