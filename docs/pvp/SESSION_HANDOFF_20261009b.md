# Session handoff 2 — v6 ability system feature work (2026-10-09)

**Branch:** `agent/skillv6-sandbox` · **worktree:** `C:\Users\Zhibin Ren\Desktop\reborn-iso-skillv6-sandbox`
**Tree:** clean. **App:** not running. **Merge:** NOT done (a human runs `merge-finalize`).
Continues `SESSION_HANDOFF_20261009.md` (same day). Origin untouched (no push).

## 1. Build / run / verify
- Feature build: `$env:RC_CLIENT_EXE="reborn_client_skillv6.exe"; cmd /c client\build_client.cmd`
  → want `build exit=0` (stop any running `reborn_client_skillv6` first — a locked exe silently
  leaves a stale build). Runtime cwd `C:\SeasunGame\MovieEditor`; title `sandbox-skillv6`.
- Datasets the client reads live in `C:\SeasunGame\MovieEditor\bin64\ability_picker\` — the build
  does NOT copy them; after a `build_*.py` change, copy the `_f1.tsv` there manually.
- Gates (must stay green): `tools\netcode\reference\jx3_model.py` (10 PASS),
  `tools\gravity\verify_model.py`, `tools\netcode\loot\capture.py selftest`.
- Probes use a distinct `RC_MEM_NS=reborn_skillv6_probe.memory`; `RC_AUTORUN=<ms>` exits.

## 2. Done this session (all committed; verified)
- **FX4 target be-hit animation** (`5a6c1d31`): target plays the `_bat01` sibling of its idle anim
  (client truth: `KRLCharacter::BeHittedByPlayer` @0x1804d0220 literal `bat01.ani`; sibling builder
  `0x1804d8700` template `data/source/npc_source/%s/动作/%s_%s`; kind map in `BeHittedByNpc`
  @0x1804cfb50). A/B verified; `RC_BEHIT=0` disables. Proof `proof/netcode/skillv6_fx4_behit_20261009.txt`.
- **`dump_fn_disasm.py` fix** (`6bf726ab`): guard SKIPDATA `.byte` in annotate.
- **Buff durations** (`3f8112cf`, `7c608bc9`): `ability_picker/tools/build_buff_times.py` →
  `buff_times_f1.tsv` (Buff.tab `Count×Interval` @16 fps, from `LuaGetBuffTime` @0x1401c0730);
  self/target buffs expire; 3 exact-timing runs. Proof `proof/netcode/skillv6_buff_duration_20261009.txt`.
- **AoE geometry** (`51bd877b`): scripts/skills.tab carry only radius+height (no sector/rect);
  fixed the "nAreaRadius unset -> hit every entity" bug (client falls back to `nMaxRadius`).
  A/B verified. Proof `proof/netcode/skillv6_aoe_shape_20261009.txt`.
- **Full sweep regression green** (`proof/netcode/skillv6_sweep_regression_20261009.txt`):
  115 casts + 39 AV-skips = 154, 0 AV, clean DONE after all the above.

## 3. Closed / characterized (no work left)
- **Target CC duration**: server-authoritative; `skill_caster.LockControlTime` is the caster's
  self-lock (empty), no client table has the target CC time → keep the 2 s stand-in.
- **skill_chain.txt** = 链状特效 visual beam (`.Sfx`, W4-blocked) — not a combo; **skill_tag.txt** =
  SkillID→AnimationID (handled); **behit_shake.txt** = 3 trivial rows (0/1/2), selector unknown.
- **Missiles**: only 2 roster skills carry a `skill_caster` `MissileID` (28046→231, 65059→25), but
  the bullet visual `nSkillBulletType` is set in 16% of scripts → the mechanic is broader (see §4).
- **nMinRadius**: only 65120, which is TargetSingle (min cast range, not an AoE annulus).

## 4. Remaining (with the exact blocker / next probe)
- **W4 landing `.Sfx` (FX2, 16 abilities + chain beams)** — `native/sfx_shim.cpp`
  `RC_Shim_SfxPlay` still faults (rc=7, 0xC0000005) creating `KG3D_CreateSFXFromFile` on the ME
  host. The shim mirrors the engine caller @`0xE3412A` / tag caller @`0x76E51A` and forces
  `owner=scene`. Next probe: disasm `0xE3412A`/`0xBE4000` args + the SFX-pool global; recover the
  correct owner/context for the ME build. Deep engine RE.
- **W3 hit sound (FX5)** — names (`TianCe_Body_L01`, `l_LongYaBeiJi`, `f2sgb11BangFaGongJi07`) are
  absent from all 230 indexed banks; Wwise ids are authored (not hashed). Next: find/load the bank
  with those strings, then by-name `PostEvent` (sound_probe already hooks `PostEvent(const char*)`).
- **W1 39 AV abilities** — needs a debugger (not installed); fault is a bad-size memcpy via
  jemalloc from the engine anim code. Blocked.
- **Damage scaling (weapon%/AP)** — no player attack-power base; blocked/server-side.
- **Lua VM (46 `EXECUTE_SCRIPT`-only)** — most are no-op/template passives; large.
- **Missiles / projectiles** — scoped feature: `skill_caster` MissileID is only 2 roster skills but
  the bullet visual `nSkillBulletType` is in 189/1173 scripts (16%); roster scoping needs a
  `skills.tab` col-57 `ScriptFile` join. Non-channel multi-hit is unclear (boss
  `nSubsection`/`SetDelaySubSkill` paths).

## 5. Key files
- Client: `client/RebornClient.cs` (host; FX3/FX4 hit loop ~4200, buff expiry ~4350, AoE ~4196),
  `client/Targeting.cs` (`TargetEntity.BuffUntil/AnimPath`), `client/MechanicProgram.cs`.
- Builders: `ability_picker/tools/{build_mechanics,build_buff_times,build_hit_fx,build_cast_chain,...}.py`.
- Datasets (repo `ability_picker/data/`, runtime `bin64\ability_picker\`): `mechanics_f1`,
  `buff_times_f1`, `hit_fx_f1`, `apply_f1`, `roster_f1`, `av_blacklist_f1`, …
- RE tools: `tools/pvp/dump_fn_disasm.py`, `tools/pvp/disasm_fn.py`, `tools/camera/minidump_exc.py`.
- Proof: `proof/netcode/skillv6_*`.

## 6. Definition of done
Gate command run; area README index updated; `docs/EXPERIENCES.md` appended; `Verified:` line; end
with the game-design check. Proof under `proof/netcode/`.
