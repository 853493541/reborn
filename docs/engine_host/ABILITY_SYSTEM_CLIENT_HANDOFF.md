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
- 81 abilities load; **81 anim steps play the authored tani**; 122 PSS dummies.
- 18 abilities additionally spawn the engine's own tag effects from their tani
  (五蕴皆空, 凌太虚, 剑主天地, 圣明佑, 如意法, 徐如林, 怖畏暗刑, 抱残式, 极乐引,
  烈日斩, 玄水蛊, 百足, 花语酥心, 蛇影, 蛊虫献祭, 银月斩, 雷震子, 驱夜断愁).

## Open work (priority order)

1. **"Effects replay wrong"** (named ability: **如意法**; user: "特效反复从头重放 /
   整个特效期间一直重放"). Status 2026-10-07: **not reproduced** on the current build.
   Full-density engine captures (200 ms sampling across the whole 12.5 s effect,
   standing/moving/W-taps) show one smooth play (rise -> steady -> decay), no
   restart; an A/B of the PSS follow (`RC_PSS_FOLLOW=move` vs `always`, i.e. 1 vs
   3468 re-adds) is pixel-identical; a transform-jitter probe (`jitter`, dummy
   re-added at alternating +/-40u every frame) also does not restart the PSS.
   What the trace DID find (new, verified):
   - The matched tani carries the effect records itself: `F1smj10双刀buff04_清净心01.tani`
     embeds `m_明教清净心01.pss` **and** `m_明教圣火_落地.pss` plus the 4 `.Sfx` tags.
     The dataset also stages `m_明教清净心01.pss` as a dummy -> the engine's tag
     path and the dummy are two sources of the same PSS (58 of 61 dummy-carrying
     abilities have this). With the dummy present the tag burst is suppressed
     (shared PSS instance); without the dummy the tag burst renders for ~1 s and
     dies with the animation (the dummy is the long tail).
   - `ability_picker/data/tani_pss_tags.json` (built by
     `ability_picker/tools/scan_tani_tags.py`) records every ability's tani base
     `.ani` + embedded `.pss`/`.sfx` paths.
   - A dormant builder pass `avoid_tani_pss_dup` (`AVOID_TANI_PSS_DUP=False`)
     can drop the tani (play the base `.ani`) for those abilities to remove the
     duplicate — off because it also loses the authored early burst.
   Next probe needs the user: which ability + interaction (does it need movement,
   or casting a second ability?) and a capture at the moment the restart is seen;
   the report may predate the 2026-10-07 PSS revert (`9a536eb`).
2. **70 animation matches changed** vs the v2-era dataset (the "wrong moves"
   report): diff against `agent/skillv2-sandbox`'s
   `ability_picker/data/ability_candidates.json`. The 7 dash-sourced matches are
   movement-layer entries (suspect for cast animations).
3. **天绝地灭**: its tani AVs the engine → `TANI_BLACKLIST` (plays base anim).
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