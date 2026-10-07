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
| 3.3 | Motion tags / root motion | **STRUCTURE DECODED** | MotionTag v1 stream: 0x188 keyframe record + 12 type-prefixed payloads (sizes tabled); root motion = whitelist `player_animation_use_originroot_trans.txt` (F1 only swim clips); 太阴指 reproduction exact; jump clips partially in-place (corrected) | container file; sub-tag type→semantic names; v0 block interior |
| 3.4 | Facial / morph (FaceLift) | **RESEARCHED** | three systems: simple presets (49 V1 params), new-face/MetaFace (CNDK save, 187 V2 params/39 face bones, converter → JSON verified by running it), FaceLift 易容 (server 373 B struct); engine apply symbols located (`SetFaceBoneParams`, `LoadMetaFaceDefinitionJson`); CLR lacks apply | KFACE/KBODY/KHAIR `.dat` layout; native shim for JSON/param apply; who emits `CNDK` |
| 3.5 | Ragdoll / physics bodies | **VERIFIED** | `KPhysicsRagdoll` symbols confirmed (PhysicsEngineX64, corrected RVAs); PhysX articulation; deadline+manager lifecycle; 11-body presets; blend consumer still untraced; SIMWorld has no ragdoll API | host wiring (physics actor + activation + bodies); blend consumer trace |
| 3.6 | Mounts / vehicles / glider / parachute | **RESEARCHED + HOST PH.1** | horse flags/handlers (`RideHorse` 0x14036C210, `DownHorse` 0x140365C60), ride tables extracted (rides/ride_rush/ride_link/mannedspace), 999-sentinel mount anim; manned space = 神机车/摧城车 (`s_hs`); glider = AUTOFLY + nav-fly ride 1152 + GliderCamera (not a move state); parachute = `KCharacter+0x214`, op 9, consumers traced | war-elephant/airship binding; ride-speed consumers; GliderCamera values (CDN); parachute visual |
| 3.7 | Swim / fly / 轻功 | **VERIFIED + CORRECTED** | swim integrator = `ProcessVerticalMove` 0x140318C50 (G-15's 0x14031B640 was RunTo); water surface from terrain cell; 轻功 chain data + End-phase guard decoded (server move-record flag drives it); fly/bird/auto-fly/parkour/sprint confirmed | 轻功 offline segment length (registered provisional); swim MoveTo path; cell water semantics; fly state semantics |

## Suggested workstream split (next step)

| W | Workstream | Point(s) | Size | Why this order |
|---|---|---|---|---|
| W1 | **Actor sockets + head anchor shim** | 3.1 | S | unblocks camera C1 (head bone / `s_face`), one shim export pair, proven `camera_shim` pattern |
| W2 | **Face pipeline** | 3.4 | M | self-contained: CNDK→KMETAFACE JSON offline + native apply shim; visible payoff |
| W3 | **Mount core (horse)** | 3.6 | M | data + state machines fully decoded; movement/speeds/jump/anims all specified |
| W4 | **Locomotion selection wiring** | 3.2 | S-M | table + rule decoded; host uses tier clips + thresholds |
| W5 | **Motion tooling (MotionTag/root)** | 3.3 | M | finish container/type semantics; feeds skills (3.3 → combat) |
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

## Corrections applied to older docs (2026-10-06)

The deep pass corrected several stale claims; each affected doc carries a dated correction
line pointing here: `CONTROL_MODES_P5_ANIM.md` (§2/§2b), `CONTROL_MODES_TRACEABILITY.md`
(A3/A10), `JX3_DOUBLE_JUMP_RESEARCH.md` (in-place clips), `JX3_COLLISION_SYSTEM.md`
(G-15/G-16/AddPhysicsBone), `REBORN_JUMP_FALL_SPEC.md` (rows 11/21/29, 192-unit label),
`JX3_GRAVITY_RESEARCH.md` (+0x2FC = nRunSpeed, parachute xref, EndFlyJump address),
`COLLISION_SYSTEM_STATUS.md` (`bAddPlayerPhysicsActor` wording), `JX3_MODE_MATCH_LIFECYCLE.md`
(滑翔翼 naming + descent), `SKILL_DATA_EXTRACTION.md` / `SKILL_MOTION_METHOD.md` (MotionTag),
`HOTKEY_SYSTEM_FULL.md` (CNDK header), `HOST_DEVIATIONS.md` C1 (head bone vs `s_face`).

## Notes

- Evidence: all claims cite the game client / MovieEditor installs (read-only) with
  symbol+RVA or path:line; extracted game tables live in session scratch
  (`%TEMP%\opencode\char3x\`) and are **not** committed (game assets stay private).
  Every doc ends with a Reproduce section using repo tools.
- The map's §3 statuses were updated to match this pass (`GAME_SYSTEMS_RESEARCH_MAP.md`).
