# Character & animation (system 3) — research home

**Branch:** `research/character-animation` · **Date:** 2026-10-06
**Scope:** the 17-system map's §3 — rig/sockets, locomotion, motion tags, facial, ragdoll,
mounts/vehicles/glider, swim/fly/轻功. Deep-research pass done 2026-10-06 (five parallel
dives, all read-only against `C:\SeasunGame\Game\JX3\bin\zhcn_hd` + `C:\SeasunGame\MovieEditor`).

## Status after the deep pass

| # | Point | State | Key result | True remaining |
|---|---|---|---|---|
| 3.1 | Rig / body parts / head attach | **RESEARCHED** | actor = GBK INI `[ROOT]+[PartN]` (花萝 16 slots); f1 182 / m2 191 bones; `Socket.tab` 19 + `SocketToParentBone.ini` 54 sockets; engine getters located (`KG3D_Actor::FindSocket` 0x18081E5D0, `GetSocketMatrixLocal` 0x1808204D0, `GetBoneMatrixLocal` 0x18081F090); managed has **no** getter (reflection route `m_pModel`) | one shim probe (handle→object type) then socket export pair; camera C1 anchor (`Bip01 Head` bone vs `s_face` socket) |
| 3.2 | Locomotion blend/kind map | **SOLVED (data)** | the "84-byte table" was `CommonCharacterSFX`; real table = **`PlayerRush` → `Represent/player/player_rush.txt`** (0x170 B rows, 73 fields, key role/school/weapon, school-999 mount rows); tier rule + thresholds 35/40/50 decoded | speed unit of `[+0xD0]`; slot/pass mapping; host wiring of the selection |
| 3.3 | Motion tags / root motion | **STRUCTURE DECODED + container PROVEN + host wired** | MotionTag v1 stream: 0x188 keyframe record + 12 type-prefixed payloads (sizes tabled); container = `.tani` GATA (`KG3DAnimationTagDataContainer::_Load` 0x180291490), factory/RTTI mapped, motion block byte-exact to EOF; root motion = whitelist `player_animation_use_originroot_trans.txt` (F1 only swim clips); host applies the authored vector on skill cast (太阴指 driven: moved 157.0 u, 0.02 % vs authored 157.033, 0.18 % vs measured 156.72); jump clips partially in-place (corrected) | sub-tag type→semantic names (W5.2; selector/direction stopgap); SFX-block float attribution (vt slot 2 0x18029E0B0); v0 block interior |
| 3.4 | Facial / morph (FaceLift) | **RESEARCHED + partial impl** | three systems: simple presets (49 V1 params), new-face/MetaFace (CNDK save, 187 V2 params/39 face bones, converter → JSON verified by running it), FaceLift 易容 (server 373 B struct); engine apply symbols located (`SetFaceBoneParams`, `LoadMetaFaceDefinitionJson`); CLR lacks apply. **Implemented (agent/3x-face):** offline tool + client `RC_FACE_JSON` load + proxy resolve; shim JSON entry fixed (0x47010; 0x46FB0 is the INI loader); negative-handle attach AV fixed. **Apply blocked** — engine face subsystem E_FAIL (SD `new_face.mesh` init cannot load; actor face part absent on 花萝) — boundary registered in `3_4_FACIAL.md` | re-open: editor app IL sequence, INI→JSON order probe, HD suffix mapping for the face-lift mesh init |
| 3.5 | Ragdoll / physics bodies | **VERIFIED** | `KPhysicsRagdoll` symbols confirmed (PhysicsEngineX64, corrected RVAs); PhysX articulation; deadline+manager lifecycle; 11-body presets; blend consumer still untraced; SIMWorld has no ragdoll API | host wiring (physics actor + activation + bodies); blend consumer trace |
| 3.6 | Mounts / vehicles / glider / parachute | **RESEARCHED + HOST PH.1** | horse flags/handlers (`RideHorse` 0x14036C210, `DownHorse` 0x140365C60), ride tables extracted (rides/ride_rush/ride_link/mannedspace), 999-sentinel mount anim; manned space = 神机车/摧城车 (`s_hs`); glider = AUTOFLY + nav-fly ride 1152 + GliderCamera (not a move state); parachute = `KCharacter+0x214`, op 9, consumers traced | war-elephant/airship binding; ride-speed consumers; GliderCamera values (CDN); parachute visual |
| 3.7 | Swim / fly / 轻功 | **VERIFIED + CORRECTED** | swim integrator = `ProcessVerticalMove` 0x140318C50 (G-15's 0x14031B640 was RunTo); water surface from terrain cell; 轻功 chain data + End-phase guard decoded (server move-record flag drives it); fly/bird/auto-fly/parkour/sprint confirmed | 轻功 offline segment length (registered provisional); swim MoveTo path; cell water semantics; fly state semantics |

## Suggested workstream split (next step)

| W | Workstream | Point(s) | Size | Why this order |
|---|---|---|---|---|
| W1 | **Actor sockets + head anchor shim** | 3.1 | S | unblocks camera C1 (head bone / `s_face`), one shim export pair, proven `camera_shim` pattern |
| W2 | **Face pipeline** | 3.4 | M | self-contained: CNDK→KMETAFACE JSON offline + native apply shim; visible payoff |
| W3 | **Mount core (horse)** | 3.6 | M | data + state machines fully decoded; movement/speeds/jump/anims all specified (**phase 1 done 2026-10-06, `agent/3x-mount`**) |
| W4 | **Locomotion selection wiring** | 3.2 | S-M | table + rule decoded; host uses tier clips + thresholds |
| W5 | **Motion tooling (MotionTag/root)** | 3.3 | M | **done 2026-10-06** (container + tool + host skill-cast integration; residual: sub-tag naming) |
| W6 | **Swim / fly / 轻功 states** | 3.7 | M-L | needs the 15 Hz integrator port (G-14); 轻功 segment length provisional |
| W7 | **Ragdoll wiring** | 3.5 | L | needs player physics actor; blend consumer trace first |

## Docs

| Doc | Content |
|---|---|
| `3_1_RIG_SOCKETS.md` | actor composition, bone/socket maps, engine API (managed + native RVAs), host feasibility |
| `3_2_3_3_LOCOMOTION_MOTION.md` | PlayerRush table identification + thresholds/blend semantics; MotionTag structure; root-motion whitelist |
| `3_4_FACIAL.md` | CNDK/new-face/MetaFace/FaceLift pipeline, converter run, face mesh/bones, engine + CLR surface |
| `3_5_3_7_RAGDOLL_SWIM_FLY.md` | claim-by-claim verification of ragdoll/swim/轻功/fly docs + true gaps + host wiring notes |
| `3_6_MOUNTS_GLIDER.md` | mount/vehicle/glider/parachute state machines, 15 extracted ride tables, symbols/RVAs |
| `tools/character/water_surfaces.py` | extract the 5 maps' `watersurfacelist.json` + generate `client/WaterSurfaces.cs` (SPEC_STATES P1) |
| `SPEC_MOUNT.md` | **redefined mount spec** (C re-task): lifecycle + record interface, jump rules (idle skill 13618 / moving 44565 / sprint-branch triple), facing/seat, animations, acceptance criteria |
| `SPEC_MOTION.md` | **redefined motion spec** (D re-task): `SkillMove.tab` displacement chain (`OnSkillMove`), heading-at-start rule, MotionTag container settled, camera rule, acceptance criteria |
| `SPEC_STATES.md` | **redefined states spec** (E re-task): 轻功 grant (passive skill 18 踏云 `MAX_JUMP_COUNT+1`), per-map water surfaces, waterline correction, water AV root cause, fly/bird triggers, acceptance criteria |
| `SPEC_MOTION_P2.md` | **addendum (client truth)**: per-skill motion - 太阴指 = `DASH_BACKWARD` primitive (960 u / 16 f, `KCharacter::Dash`), 风来吴山 = channel with walk allowed (no displacement); camera = positional follow only, yaw dead-zone 0.45 |
| `SPEC_STATES_P2.md` | **addendum (client truth)**: water entry - no swim key; walk/fall/jump in; depth gate ~627 u (shallow = wade); jump = state 5; region = terrain cell flag (replace radius heuristic) |

## Tools

| Tool | Content |
|---|---|
| `tools/character/motion_tag.py` | `.tani` GATA container parser + MotionTag stream dump (selftest 11/11; `--tsv`/`--json`) |

## Corrections applied to older docs (2026-10-06)

The deep pass corrected several stale claims; each affected doc carries a dated correction
line pointing here: `CONTROL_MODES_P5_ANIM.md` (§2/§2b), `CONTROL_MODES_TRACEABILITY.md`
(A3/A10), `JX3_DOUBLE_JUMP_RESEARCH.md` (in-place clips), `JX3_COLLISION_SYSTEM.md`
(G-15/G-16/AddPhysicsBone), `REBORN_JUMP_FALL_SPEC.md` (rows 11/21/29, 192-unit label),
`JX3_GRAVITY_RESEARCH.md` (+0x2FC = nRunSpeed, parachute xref, EndFlyJump address),
`COLLISION_SYSTEM_STATUS.md` (`bAddPlayerPhysicsActor` wording), `JX3_MODE_MATCH_LIFECYCLE.md`
(滑翔翼 naming + descent), `SKILL_DATA_EXTRACTION.md` / `SKILL_MOTION_METHOD.md` (MotionTag),
`HOTKEY_SYSTEM_FULL.md` (CNDK header), `HOST_DEVIATIONS.md` C1 (head bone vs `s_face`).

## Tools

| Tool | Purpose |
|---|---|
| `tools/character/face_data.py` | Face 3.4 pipeline: CNDK new-face parse (crc32), tBone/tDecal validation vs `settings/FaceLiftV2/Bone/*.tab`, FaceLift clamp check, `FaceLiftDataConverterX64 KMETAFACE` wrapper. `selftest` = offline gate (synthetic + real saves + converter) |

## Notes

- Evidence: all claims cite the game client / MovieEditor installs (read-only) with
  symbol+RVA or path:line; extracted game tables live in session scratch
  (`%TEMP%\opencode\char3x\`) and are **not** committed (game assets stay private).
  Every doc ends with a Reproduce section using repo tools.
- The map's §3 statuses were updated to match this pass (`GAME_SYSTEMS_RESEARCH_MAP.md`).

## Tools

| Tool | Role |
|---|---|
| `tools/character/drive_mount.ps1` | driven real-input mount scenario (OS keys; original) |
| `tools/character/drive_mount2.ps1` | same scenario with verified foreground activation + PostMessage fallback (use this one) |
