# Session handoff — v6 ability system (2026-10-09)

**Branch:** `agent/skillv6-sandbox` · **worktree:** `C:\Users\Zhibin Ren\Desktop\reborn-iso-skillv6-sandbox`
**Local tip:** `07fce7a8` · **origin/agent/skillv6-sandbox:** `81203f0b` (3 commits behind — push `9d22dfa5 542d89e0 07fce7a8` if you want them on origin; only push when asked).
**Tree:** clean. **App:** not running. **Merge:** NOT done (a human runs `merge-finalize`; never merge/push unattended).

---

## 1. Build / run / verify (read first)

```powershell
# build a feature client (from the worktree root)
$env:RC_CLIENT_EXE="reborn_client_skillv6.exe"; cmd /c client\build_client.cmd   # want "build exit=0"
# run (working dir MUST be the engine root)
Start-Process "C:\SeasunGame\MovieEditor\bin64\reborn_client_skillv6.exe" -WorkingDirectory "C:\SeasunGame\MovieEditor"
```
- **GOTCHA: the build silently fails if the exe is locked** (a client still running) — it prints no
  `build exit` and leaves a STALE exe → tests look wrong. **Always `Stop-Process -Name reborn_client_skillv6`
  before building, and confirm `build exit=0`.** If in doubt, rebuild.
- Namespace: `reborn_client_skillv6.memory`; title `sandbox-skillv6`. Probes must use a **distinct
  `RC_MEM_NS=reborn_skillv6_probe.memory`** so they run alongside the user's session (the guard
  honors a distinct namespace).
- Gates (must stay green): `.venv\Scripts\python.exe tools\netcode\reference\jx3_model.py` (10 PASS),
  `tools\gravity\verify_model.py`, `tools\netcode\loot\capture.py selftest`.
- Console shows Chinese as mojibake; **files are UTF-8** (decode game data as GB18030/GBK).

## 2. What is DONE (working, verified)

- **Cast chain** (`client/SkillCast.cs` + `RebornClient.cs`): face target → authored anim →
  prepare/channel → dash → effect. Prepared casts (prepare anim), channel duration.
- **Mechanics runtime** (`client/MechanicProgram.cs`): damage/CC/buffs/child/pull/knockback/self-move;
  115 castables individually verified.
- **Panel + review tagging** (`client/AbilityPanel.cs`): 4 catalogs (测试中/未测试/需要修复/已完成);
  click selects (panel re-raised above the engine each frame); **F1/F2/F3** tag the selected ability
  via a **WH_KEYBOARD_LL hook** (`LowLevelKeyHook` — focus-independent); persisted to
  `bin64\ability_picker\ability_status_f1.txt` (probes use `_probe`).
- **Host fixes:** uniform 20-尺 cast range enforced; disposed-form crash guard (`CombatText/AbilityPanel/
  Targeting/HudOverlay` `PointToScreen`).
- **FX:** `build_hit_fx.py` → `hit_fx_f1.tsv` (+ `hit_target_sound/behit_sound_type/behit_shake`);
  **FX3 hit effect** spawns the authored `.pss` at each target, **one-shot** (unique instance +
  2 s removal; `.Sfx` skipped because `AddDummyModel(.Sfx)` AVs).
- **Cooldowns:** authored per-skill cooldown/GCD available via **`RC_AUTHORED_CD=1`** (default stays
  the locked uniform 3 s / 1.19 s).
- **Channel tick schedule:** channel skills apply their mechanic every `channelInterval` (27906 → 10
  applies over 4 s, was 1).

## 3. What is NOT done (missing) + exact next probe

Full inventory: `docs/pvp/ABILITY_FOLLOWUP_PLAN.md`; RE method: `docs/pvp/ENGINE_RE_BREAKTHROUGH_PLAN.md`.
Ordered by leverage:

1. **W1 — 39 AV-blacklisted abilities** (`ability_picker/data/av_blacklist_f1.txt`) can't cast.
   Root cause located: `memcpy` size `0xFFFFFFFA` via `jemallocX64.dll` from the engine anim code
   (`KG3DEngineDX11EX64.dll+0xE2E842` region). **BLOCKED: needs a debugger** (x64dbg/`cdb`) — the WER
   dumps don't contain the caller frames' code pages. Repro: empty the runtime blacklist, cast 65068.
2. **W2 — be-hit animation (FX4).** Rule recovered: kind-selected from the target anim set
   (`[rsi+0x8c/0x94/0xa4]`, kinds 1/2/6) or model-sibling template `data/source/npc_source/%s/动作/%s_%s`.
   Next: find the table filling the target's be-hit anims + the kind map, then play it on the target.
3. **W3 — hit sound.** `Behit.bnk` parsed (95 Event ids); the `hit_target_sound` name→id is NOT plain
   FNV-1/-1a (checked all 87 banks). Next: disasm the client's sound-name→id resolution.
4. **W4 — landing-zone `.Sfx`** (`AOESelectionSFXFile`): engine `.Sfx` spawn faults (`rc=7`,
   `0xC0000005`); needs the correct engine SFX-factory owner chain.
5. **Non-channel multi-hit** hit count/timing (channel is done).
6. **Missiles/projectiles** (`missile.txt`, `skill_missile.txt`) — not modeled.
7. **Buff durations** — source unknown (`Buff.tab` has no duration column).
8. **Skill tag/chain/shadow, hit-stiff** (`skills.tab` 112-116), **non-circular AoE shapes** — unmodeled.
9. **Damage scaling** (weapon%/AP) — no player stats; search for a base-attribute table.
10. **Lua VM** — 46 `EXECUTE_SCRIPT`-only abilities (helpers in `tools/pvp/lua51_disasm.py`).
11. **be-hit shake** (`behit_shake.txt`) — RepresentID selector unknown.

## 4. Key files

- **Client:** `client/RebornClient.cs` (host, ~6100 lines; dataset loaders ~380-470; cast block ~4009-4260;
  selectSlot ~490-542; F1/F2/F3 hook ~2440; panel loop ~5400). `client/SkillCast.cs`, `MechanicProgram.cs`,
  `AbilityPanel.cs`, `CombatText.cs`, `Targeting.cs`.
- **Datasets:** `ability_picker/data/{roster_f1,mechanics_f1,cast_chain_f1,cast_frames_f1,cooldowns_f1,
  apply_f1,hit_fx_f1,hit_target_sound,behit_sound_type,behit_shake,av_blacklist_f1}.tsv|json|txt`.
  Runtime copies: `C:\SeasunGame\MovieEditor\bin64\ability_picker\`.
- **Tools:** `ability_picker/tools/build_{cast_chain,roster,cast_frames,cooldowns,passives,mechanics,hit_fx}.py`;
  RE: `tools/pvp/{dump_fn_disasm,disasm_fn}.py`, `tools/netcode/{xref_va,xref_string}.py`,
  `tools/camera/minidump_exc.py`.
- **Docs:** `docs/pvp/{ABILITY_MECHANICS_PLAN,ABILITY_FX_AND_HIT_REACTION_PLAN,ABILITY_CORRECTNESS_PLAN,
  ENGINE_RE_BREAKTHROUGH_PLAN,ABILITY_FOLLOWUP_PLAN}.md`; `docs/netcode/JX3_SKILL_CAST_FLOW.md`;
  `docs/EXPERIENCES.md` (append-only).
- **Proof:** `proof/netcode/skillv6_*`.

## 5. Useful RC env knobs

`RC_CLIENT_EXE`, `RC_MEM_NS`, `RC_TITLE`, `RC_SLOTS`/`RC_ABILITY`, `RC_CAST_AT=ms[:slot]` (comma-separated
for several), `RC_TAB_AT`, `RC_SWEEP`/`RC_SWEEP_N`/`RC_SWEEP_START`/`RC_SWEEP_RANGE`, `RC_SKIP_IDS`,
`RC_DUMMY_N`, `RC_CAST_RANGE` (20), `RC_CD_MS`/`RC_GCD_MS`/`RC_AUTHORED_CD`, `RC_SET_STATUS=id:cat`,
`RC_PANEL_OPEN`/`RC_PANEL_DUMP`, `RC_CT_DUMP`, `RC_HITFX`, `RC_STATUS`, `RC_LY_FXE`, `RC_AUTORUN`,
`RC_NOLOADING`.

## 6. Environment gotchas

- No debugger installed → W1 blocked.
- WER LocalDumps (I set then removed the HKCU key) writes to `%LOCALAPPDATA%\CrashDumps\` (~118 MB each).
- Concurrent client + user session contend on the shared engine root (`C:\SeasunGame\MovieEditor`).
- Engine init ~35 s; each driven probe ~75-90 s. Set `RC_NOLOADING=1` for probes.
- Never write outside documented outputs under `C:\SeasunGame`; game installs read-only.

## 7. Definition of done (per change)

Run the gate command; update the area README index; append `docs/EXPERIENCES.md`; `Verified:` line in the
reply; end with the game-design check. Proof under `proof/netcode/`.
