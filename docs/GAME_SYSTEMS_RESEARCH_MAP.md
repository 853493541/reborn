# JX3 game systems — research map and backlog

**Branch:** `control-system-notes` · **Date:** 2026-09-25
**Purpose:** what exists in the game, what we have already researched, and what
to look into next. Status legend: **[DONE]** documented with evidence ·
**[PART]** partial · **[OPEN]** not researched · **[SERVER]** server-side only.

Count: **17 major systems, ~110 tracked areas**; the priority backlog is §18.

---

## 1. Engine host & rendering

| Area | Status | Artifacts / next questions |
|---|---|---|
| Engine host init (MovieEditor DLLs) | [DONE] | `docs/engine_host/ENGINE_HOST_PLAN.md`, `engine_host_spike/` |
| Scene/map loading (`jsonmap`, regions) | [DONE] | `docs/engine_host/M1_ACTOR_ON_MAP.md`, `TerrainSampler` |
| Terrain heightfield + collision | [DONE] | `docs/movement/FULL_MAP_COLLISION.md`, `REAL_CLIENT_MAP_COLLISION.md` |
| Baked structure/foliage collision | [DONE] | `tools/bake_map_collision.py`, `FoliageCollision.cs` |
| Camera (follow/modes/drag) | [DONE] | `docs/camera/`, this branch |
| SFX / PSS / movie editor path | [PART] | `PSS_FORMAT.md`, `HANDOFF_SFX.md`, `SFX_RUNTIME_STATUS.md` |
| Unit scale & character size | [DONE] | `docs/netcode/UNIT_SCALE_AND_CHARACTER_SIZE.md` |
| Rendering options / graphics presets | [OPEN] | `config/config_*.ini` presets (9 levels), per-option caps probe |
| Weather / day-night / post effects | [OPEN] | environment tables, `EnableRC_*` option flags |
| LOD / culling / performance | [OPEN] | `fModelLod*`, `TerrainLODGates`, streaming budgets |

## 2. Assets & pipeline

| Area | Status | Artifacts / next questions |
|---|---|---|
| PakV4 extraction | [DONE] | `tools/netcode/extract_pak_paths.py` (+ probe tree) |
| hpkg/CDN extraction | [DONE] | `tools/netcode/extract_hpkg_member.py` |
| Actor/mesh/texture formats | [PART] | `_dump_*.mjs`, FBX exports, `mesh.py` |
| Animation formats (.ani/.tani) | [PART] | `min2.py`, `tani.py`, `_tani_timing_probe.py`, catalogs in `samples/` |
| PSS particles | [PART] | `PSS_FORMAT.md`, `pss*.py` |
| UI textures / `.UITex` atlases | [PART] | `docs/ui/MAP_MINIMAP_RESEARCH.md` |
| Addon encryption/loader | [DONE] | `tools/addon_decrypt.py`, `proof/minimap/recon/kgui_*` |
| Fonts/strings (`string.txt`) | [PART] | needed for full UI text parity |

## 3. Character & animation

| Area | Status | Artifacts / next questions |
|---|---|---|
| Rig/body parts/head attach | [PART] | `_head_attach.jsfrag`, actor presets |
| Locomotion blend/kind map | [DONE] | `docs/movement/JX3_CHARACTER_MOVEMENT_RESEARCH.md` §4 |
| Motion tags / root motion | [PART] | `docs/netcode/SKILL_MOTION_METHOD.md` |
| Facial/morph (FaceLift) | [OPEN] | `FaceLiftDataConverter.exe`, face bone probes |
| Ragdoll/physics bodies | [PART] | `physic_character_param.krl.txt` |
| Mounts/vehicles/glider/parachute | [PART] | `number.krl`, JumpParam wall/horse rows |
| Swim/fly/轻功 states | [PART] | `docs/movement/REBORN_JUMP_FALL_SPEC.md` §3–§9 |

## 4. Controls & UI (this branch)

| Area | Status | Artifacts |
|---|---|---|
| Hotkey/input system | [DONE] | `controls/HOTKEY_SYSTEM_FULL.md` + annexes |
| Movement controls | [DONE] | `controls/JX3_MOVEMENT_CONTROLS.md` |
| Camera controls | [DONE] | `controls/JX3_CAMERA_CONTROLS.md` |
| Combat controls | [DONE] | `controls/JX3_COMBAT_CONTROLS.md` |
| UI customization | [DONE] | `controls/JX3_UI_CUSTOMIZATION.md` |
| Action bars | [DONE] | `controls/RESEARCH_RESOLVED_GAPS.md` §6 |
| Targeting bodies | [PART] | `target.lua` located, bodies not decoded |
| UI panels overall (~253 files) | [PART] | `proof/minimap/recon/ui_config_inventory.txt` |
| HUD/nameplates/damage numbers | [PART] | catalog `docs/ui/BATTLE_FLOATING_UI.md` (2026-09-30): native caption data (nameplate HP bar/slots/colors/icons) + 33 KGUI modules extracted/decompiled, general-combat must-have table; open: cast-bar consumer, target layout naming, UISetting_HeadTop.ini |

## 5. Combat — skills & resources

| Area | Status | Artifacts / next questions |
|---|---|---|
| Skill tables (`skills.tab`) | [DONE] | `docs/netcode/SKILL_DATA_RESEARCH.md` |
| Skill scripts/opcodes | [DONE] | `SKILL_DATA_EXTRACTION.md`, `proof/pvp` |
| Cast/channel/GCD/cooldowns | [DONE] | `docs/pvp/REBORN_PVP_BATTLE_SPEC.md` §3.7 |
| Resources (内力/剑气/墨意/…) | [DONE] | same, §5.4 |
| Talents/recipes | [PART] | `TenExtraPoint.tab`, `recipeSkill.tab` — runtime apply open |
| Skill motion/dashes (`SkillMove.tab`) | [DONE] | jump/fall spec §3.10 |
| Missiles/projectiles | [OPEN] | `KParabolaMissileProcessor` |

## 6. Combat — stats & mitigation

| Area | Status | Next questions |
|---|---|---|
| Attribute taxonomy (694 engine attrs) | [DONE] | `proof/pvp/attributes_*` |
| Rating→% equations | **[OPEN — top]** | decompile `KPlayer::GetAttributeValue` (`JX3ClientX64.exe+0x839248`) |
| Damage pipeline (9 layers) | [PART] | ordering inferred; absorption/reflection open |
| PvP stats (化劲/御劲) | [DONE] | research doc §1–2 |
| Damage-type map | [DONE] | `damage_type_map.tsv` |
| Weapon/set bonuses | [PART] | item tables |

## 7. Buffs, control, DR

| Area | Status | Next questions |
|---|---|---|
| Buff.tab semantics (61k rows) | [DONE] | `proof/pvp/buff_control_system.md` |
| Control categories / immunity sets | [DONE] | research §4 |
| DecayType windows | [DONE] | 6/10/20 s table |
| DR per-application ladder | **[SERVER/OPEN]** | capture or engine internals |
| Dispel/steal/transfer | [DONE] | script ops |
| MoveStateMask semantics | [OPEN] | `KBuffList::CheckValidity` |

## 8. AI & NPCs

| Area | Status | Next questions |
|---|---|---|
| NPC templates/spawns | [PART] | `npc.tab` extraction |
| NPC dialogue/quest AI | [OPEN] | dialog camera + script ops |
| Pet/vehicle control | [PART] | dynamic action bars decoded |
| Server bots for reborn | [OPEN] | design work, M5 |

## 9. Game modes & meta

| Area | Status | Artifacts |
|---|---|---|
| BR 绝境 (first target mode) | [DONE] | `docs/netcode/JX3_MODE_*.md` (load, UI flow, loot, lifecycle) |
| Arena / battleground / PvP camps | [DONE] | `docs/pvp`, `proof/pvp/pvp_modes_rules.md` |
| MOBA / minigames | [PART] | mode UI flow references |
| Quest/achievement/collections | [OPEN] | `activity.tab`, quest scripts |
| Homeland/housing | [OPEN] | own scene system |
| Mounted/siege vehicles | [PART] | vehicle bars decoded |

## 10. Items, economy, loot

| Area | Status | Artifacts |
|---|---|---|
| Item tables/bags/equip | [PART] | `proof/pvp` attr catalogs |
| Doodads/loot/BR drops | [DONE] | `docs/netcode/JX3_MODE_LOOT_SYSTEM.md`, `JX3_DROPS_RESEARCH.md` |
| Crafting/recipes | [PART] | `Craft.tab`, recipe tables |
| Vendors/auction/trade/coinshop | [OPEN] | economy design for reborn |
| Mail | [OPEN] | protocol only |

## 11. World & maps

| Area | Status | Artifacts |
|---|---|---|
| MapList / BR rows / sub-maps | [DONE] | `JX3_MODE_LOAD_FLOW.md` |
| Minimap/big map/markers/fog | [DONE] | `docs/ui/MAP_MINIMAP_RESEARCH.md` |
| Map collision (the 5 baked) | [DONE] | `collision_data/`, bake tools |
| Scene animations | [PART] | `SceneCameraAni.tab` |
| Region streaming/loading screens | [DONE] | `JX3_MODE_UI_FLOW.md` §4 |

## 12. Social & persistence

| Area | Status | Artifacts |
|---|---|---|
| Chat / channels / voice refs | [PART] | keybinds decoded; protocol open |
| Teams/raid/guild | [OPEN] | protocol + UI |
| Friends/blacklist/SNS | [OPEN] | protocol |
| Persistence (`custom.dat`, `config.ini`, userdata) | [DONE] | camera/controls/hotkey docs |
| Addon system | [PART] | loader + decrypt done; API surface partial |

## 13. Netcode & server

| Area | Status | Artifacts |
|---|---|---|
| Login/gateway/handshake | [DONE] | `JX3_PROTOCOL_SPEC.md`, `REBORN_SERVER_SPEC.md` |
| Movement sync | [DONE] | movement research §6 |
| Combat sync | [DONE] | `proof/pvp/combat_netcode.md` |
| Loot/doodad sync | [DONE] | loot layouts doc |
| Matchmaking/queue/rooms | [DONE] | mode docs |
| Observer/competitor sync | [DONE] | PvP research §7 |
| Reborn server implementation | [OPEN] | M2+ plan in `PLAN_REBORN_ONLINE.md` |

## 14. Audio

| Area | Status | Artifacts |
|---|---|---|
| SFX runtime | [PART] | `SFX_RUNTIME_STATUS.md`, `sfx_runtime.py` |
| Music/ambience | [OPEN] | `Sound.tab`-class tables |
| Voice/chat audio | [OPEN] | — |

## 15. Camera/cinematic (beyond controls)

| Area | Status | Artifacts |
|---|---|---|
| Mode cameras (carrier/glider/air/dialog) | [PART] | `docs/camera/CONFIG_FILES.md` §6 |
| Camera animations / tracks | [PART] | `KRLCameraAni`, `.mani` files |
| Skill-move camera FOV | [PART] | `skill_move_camera.txt` |
| Camera shake | [DONE] | `JX3_CAMERA_RESEARCH.md` §8 |

## 16. Tooling & verification rigs

| Area | Status | Artifacts |
|---|---|---|
| Pak/hpkg extractors | [DONE] | `tools/netcode/*` |
| Bytecode dumper/probe | [DONE] | `tools/netcode/lua51_dump.py`, `tools/controls/lua51_probe.py` |
| Disasm helpers | [DONE] | `tools/netcode/xref_string.py`, `dump_va.py`, `tools/movement/find_xrefs.py` |
| Table tools | [DONE] | `catalog.py`, `tools/pvp/tab.py` |
| Runtime probes (client) | [PART] | `RC_*` env rig, camera smoke, screenshots |
| Per-run logging | [OPEN] | logs currently overwritten (gap V1) |

## 17. Anti-cheat / policy notes

| Area | Status |
|---|---|
| Server validation rules | [DONE] | reason codes in PvP spec |
| Client trust (no rollback) | [DONE] | research §7 |
| Reborn anti-cheat design | [OPEN] | out of current scope |

---

## 18. Priority research backlog (next passes)

| # | Topic | Why now | Evidence target |
|---|---|---|---|
| 1 | **Rating→% equation** | blocks all combat balance | `KPlayer::GetAttributeValue` decompile |
| 2 | Targeting/cast bodies (`target.lua`, `skill.lua`) | completes controls | Lua probe of the two files |
| 3 | UI hotkey panel grouping (`HotkeyPanel.ini` + runtime) | rebind UI parity | ini parse (72 sections) |
| 4 | Engine conflict/repeat semantics (`KHotkeyMgr`) | exact rebind behaviour | KGUI disasm |
| 5 | Movement engine bridge (`ResponseWASDKey`) | exact movement commit | Represent disasm |
| 6 | `OnAdjustPlayerMove` / `OnSyncRunSpeedLimit` | netcode movement accuracy | client exe disasm |
| 7 | `CharacterYawTurnSpeed` consumer | turn model fidelity | client exe xref |
| 8 | DR ladder + immunity reset | control feel | server capture / engine |
| 9 | NPC AI + spawn tables | PvE/bots | npc/tab extraction |
| 10 | Item/equip/crafting tables | economy/M5 | tab extraction |
| 11 | Quest/act content pipeline | M4+ | quest tables |
| 12 | Audio tables + runtime | polish | `Sound.tab`, SFX runtime |
| 13 | Per-option engine caps probe | camera/graphics parity | live host probe |
| 14 | Addon API surface | community parity | `addonapi.lua` decode |
| 15 | Server implementation milestones | product | `PLAN_REBORN_ONLINE.md` |

## 19. How to work the map

1. Everything researched lands as a doc under `docs/` (+ `proof/` evidence) and
   a status update in a register (e.g. `controls/CONTROLS_GAP_REGISTER.md`).
2. Mark **[DONE]** only with a file/offset citation; **[PART]** lists exactly
   what is missing.
3. Prefer decoding shipped data/UI Lua before binary RE; prefer binary RE
   before inventing values.
4. Keep per-run logs and screenshots for anything behavioural.

## 20. Current research coverage (estimate)

| Domain | Research |
|---|---|
| Controls / camera / movement / hotkeys | **~95%** |
| Combat data (skills/attributes/buffs) | ~85% (rating formula missing) |
| Modes / netcode | ~85% |
| World / maps / collision | ~85% |
| Assets / animation | ~60% |
| UI (full panel surface) | ~40% |
| Audio | ~30% |
| Economy / quests / social | ~20% |
| AI | ~10% |
| **Overall** | **~70%** |
