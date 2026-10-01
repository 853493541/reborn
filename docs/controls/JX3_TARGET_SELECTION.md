# JX3 target selection — how a player targets someone in front

Research pass 2026-09-30 on the shipped targeting system: UI script
(`ui/script/target.lua`, b03), the engine Lua bindings (client exe disasm), and
the existing combat-controls model. Question answered: **how does a player pick
a target, especially one in front**. Companion: `JX3_COMBAT_CONTROLS.md` §1
(input table, authority model).

## 1. TL;DR

* Selection is **client-local**; there is no select-target opcode. The client
  tracks the target and sends it with cast intents (`JX3_COMBAT_CONTROLS.md` §4).
* **Tab** (`SEARCH_ENEMY`) runs a Lua search over up to **3 cone zones in front
  of the player's facing**, calls the engine per zone
  (`SearchForEnemy(player, nRadius, nAngle)`), filters/merges/sorts, then calls
  `SelectTarget(TARGET.NPC|PLAYER, id)` and cycles with an index + frame gate.
* **Mouse click** selects whatever the engine pick under the cursor hits (any
  direction, not just front).
* Skills can auto-select: `KSkill::AutoSelectTarget` for single/area cast modes;
  sprint dash uses `AimAtSprintDashTarget(1920, 1)`.

## 2. Search zones — the "in front" geometry (HIGH)

`proof/collision/ui_scripts/target_b03.utf8.lua:17-113` (decompiled shipped
script; bytecode dump `proof/controls/ui_lua/target_script.dump.txt`):

| zone `szArea` | `nRadius` (u) | `nAngle` | `nSelLevel` (priority) |
|---|---|---|---|
| `MidAxis` | 2560 (25.6 m) | 15 | 3 (highest) |
| `Inner` | 512 (5.12 m) | 85 | 2 |
| `Outer` | 1280 (12.8 m) | 114 | 1 |

Per-target-type defaults (`ENMEY`/`ALLY`/`EYASHA`): `nVersion=2`,
`bOnlyPlayer=false`, `bPlayerFirst=false`, `bOnlyNearDis=false`,
`bWeakness=false`, `bMidAxisFirst=false`, `bSureTarget=false`,
`nCoolTime=16` frames (1 s @16 fps), `bRedFirst=false`; `EYASHA` sets
`bPlayerFirst=true`, `bTeammate=true`.

`nAngle` is a per-zone angular limit from the facing axis; `0` is passed as
"no angle limit" (bird-move/joystick path, line 900). Whether the unit is
degrees or the legacy `1.40625`-per-degree unit is **MED** (see §7).

## 3. Engine API (HIGH names/signature, MED geometry)

Disassembly: `proof/pvp/disasm_targeting/` (7 functions, 2026-09-30).

* `KPlayer::LuaSearchForEnemy` @ `0x1403F03D0` — Lua method taking 2 numeric
  args: `player:SearchForEnemy(nRadius, nAngle)`. Marshals into a search struct
  (`+0x30`, defaults `0x7fffffff`/`0x100`), then calls the internal search
  `0x140242220`. The NPC variant takes 3 ints + an optional exclusion table
  (`LuaSearchForNpc`); pet/ally variants are `LuaSearchForPetEnemy` /
  `LuaSearchForAllies`.
* The internal search walks the **spatial region grid** around the player
  (`0x1403F09D6..0x1403F0A5D`: region bounds from the player record `+0x10/+0x14`,
  cell lookup `0x1401806B0`, linked-list walk with filter `0x14047DD60`).
* `KPlayer::LuaFollowSelectTarget` / `LuaStopFollow` — follow-target support.
* Call sites in Lua: per zone, `SearchForEnemy(L5_192, nRadius, nAngle)`
  (`target_b03.utf8.lua:900-905`); results are filtered with
  `CanSelectPlayer/Npc` and tagged with the zone's `nSelLevel` (lines 907-913).
* `SearchTarget_SetAreaSettting(areaName, radius, angle)` retunes a zone's
  radius/angle by name at runtime (lines 1014-1040).

## 4. Filter, weight, sort (HIGH, Lua)

* Selectable test (`L9_9`, lines 276-317): player → `CanSelectPlayer`,
  npc → `CanSelectNpc`; reject `SELECTABLE_NONE`; reject
  `SELECTABLE_NOT_ENEMY` when searching `ENMEY`.
* Axis weight (`L8_8`, lines 234-275): `nAxisDis ≈ sqrt(dx²+dy²) × sin(angle)`
  where `angle` = folded difference between the target bearing
  (`atan2(dx,dy)`) and the player facing (`2π × nFaceDirection / 255`).
  That is the **perpendicular distance from the facing axis** — "in front"
  means small axis offset, not merely small distance.
* Comparator (`L11_11`, lines 335-423), in order: `bPlayerFirst` →
  `bIsInScreen` → `bPet` (when `g_nTabPlayerPriority`) → `bOnlyNearDis`
  (`nDis` asc) → `bRedFirst` (`nRed` desc) → `bWeakness` (`nLife` asc) →
  **`nSelLevel` desc** → `bMidAxisFirst` (`nAxisDis` asc) → `nCount` desc →
  `nIndex` asc.

## 5. Tab cycle (HIGH)

`SearchEnemy` (v2) walks the merged zone list with `g_nSearchEnemyIndex`,
skipping the current target; `g_nLastTabDownFrame = GetLogicFrameCount()` +
`nCoolTime=16` forms the re-press gate; `bSureTarget` re-selects the current
target after the gate. Selection itself: `SelectTarget(TARGET.PLAYER, id)` for
players, `SelectTarget(TARGET.NPC, id)` otherwise (lines 862-876, 906-913);
`SelectPrevTarget` (Ctrl+Tab) selects index-1 without the cooldown write.

## 6. Click and interact (MED)

The click path uses the engine pick (cursor ray) rather than the cone:
`KCharacter::DoPickPrepare/OnPickPrepare/OnBreakPicking` and represent
`PickDoodad` (`docs/movement/JX3_COLLISION_SYSTEM.md`). `GetFitObject`
(lines 529-590) then prefers, in order: lootable corpse doodad → npc/doodad →
player, respecting `g_nTabPlayerPriority` and `dwEmployer`. `InteractTarget`
dispatches `InteractPlayer/Npc/Doodad/LandObject`.

## 7. Open items

1. **Angle unit / half-angle** — MED. Candidates: degrees as written (MidAxis
   ±15°, Inner ±85°, Outer ±114°) or legacy `×1.40625` units. Next probe:
   disassemble the internal search `0x140242220` and inspect the angle
   comparison (`cos`/dot with facing).
2. **Mouseover-cast** — does the real client cast at the mouseover target
   without selecting? (`JX3_COMBAT_CONTROLS.md` open item 4.)
3. `nCoolTime=16` is frames (GetLogicFrameCount); verify the global frame rate
   here is 16 (netcode constants say GAME_FPS=16).

## 8. Implementation plan (reborn client, M1/M2)

1. Entity list from server AOI snapshots (client-side for now: our spawned
   dummies/actors).
2. `TargetSelector` module: facing from `camSys`/player yaw; zone config from
   §2 (data-driven); cone test `dist ≤ nRadius && |angleToTarget − facing| ≤
   nAngle/2` (unit per §7); filter by relation/selectable flags; sort by §4
   (axis offset weight).
3. Input: Tab = next enemy (index + 16-frame gate), Ctrl+Tab = prev, click via
   `EngineRay` screen ray vs entity capsules (nearest hit wins).
4. UI: target ring/frame + name (client-local), `SelectTarget`-equivalent state.
5. Cast intents carry the target id (our `OP_CAST_INTENT`); server validates
   range/angle/LOS per `REBORN_PVP_BATTLE_SPEC` / `JX3_COMBAT_CONTROLS.md` §5.

## Evidence

* `proof/collision/ui_scripts/target_b03.utf8.lua` (decompiled b03 script)
* `proof/controls/ui_lua/target_script.lua` + `.dump.txt` (bytecode + summary)
* `proof/pvp/disasm_targeting/KPlayer__LuaSearchForEnemy.txt` (+6 more)
* `docs/controls/JX3_COMBAT_CONTROLS.md` §1/§4/§5

## 9. What HUD appears when a target is selected (HIGH elements, MED window identity)

The selected-target HUD is the target frame family: layout `ui/config/default/Player.ini`
(script `Player.lua`, TargetPanel textures) driven by the target module
`ui/script/Target.lua` + its element library `ui/config/default/TargetCommon.ini`
(extracted + parsed 2026-09-30; element list:
`proof/controls/target_frame_elements_20260930.txt`).

| group | elements (Player.ini) | meaning |
|---|---|---|
| frame/back | `Handle_Back` (`Image_BackL/C/R/RR/R2`) | target bar background |
| avatar | `Handle_Avatar` (`Image_Avatar`, `Image_School`, `Animate_Avatar`, `SFX_Avatar`), `Handle_BuyBg` | portrait + school icon |
| name/level | `Handle_Name` (`Text_Player`, `Image_Cloud`), `Text_Level`, `Text_Others` | name, level, misc text |
| health | `Image_Health`, `Image_Flash` (hit flash), `Handle_C_Blood` + `SFX_BloodScale`, `Handle_BloodTip`, `Image_SubHealth`, `Text_Health` | HP bar + damage flash + animated fill + text |
| mana | `Image_Mana`, `Handle_C_Mana` + `SFX_ManaScale`, `Handle_NoManaBG`, `Text_Mana`, `Image_DefenseShield`/`Image_DefenseShieldLight` | resource bar, no-mana state, defence/减伤 icon |
| absorb/shield | `Handle_CureShield` (`Image_CureShield*`), `Handle_CureShieldCount` + `Text_CureShieldCount`, `Image_HealthShield*` + `Text_HealthShield` | heal-absorb and shield overlays |
| camp/relation | `Image_Camp`, `Image_TeamCamp` + `Animate_TeamCamp` | faction/team color |
| marks/state | `Image_Mark` (party mark), `Image_NPCMark`, `Image_Flag`, `Image_Boss`, `Image_Center`, `Image_Invincible`, `Image_Fight`/`Animate_Fight*` | marks, boss frame variant, invincible, in-combat glow |
| custom | `Wnd_CustomMode` (`Image_CM*`, `Text_CMName`), `Btn_RoleChange` | custom-UI label + role-change button |

Script-driven behavior (`Target.lua`):
* `UpdateState` = `UpdateLM/Name/Level/Action/Head/Kungfu/TargetMark/Camp/Invincible`;
  per-frame `UpdateEnergy/UpdateAction/UpdateSubHealth`.
* **Buff/debuff rows**: `UpdateBuffParam` registers target buffs (`Handle_Buff`) and
  debuffs (`Handle_Debuff`) in `BuffMgr` with timers (`showtime`, `show_pausecd`),
  dispel highlight, enemy-debuff light-up; options `bShowSelfDebuff`, filtering.
* **Action/cast progress bar**: `ACTION_STATE` (`NONE/PREPARE/DONE/BREAK/FADE`) and
  `PROGRESS_BAR_TYPE` (`NORMAL`/`UNBREAKABLE`) — target prepare/channel bar with
  break/fade transitions; `OT_ACTION_PROGRESS_BREAK` event.
* **Per-school target handles** (`TargetCommon.ini`, under `Handle_tot`):
  `Handle_TM/CJ/MJ/CY/TC/GB/BaDao/DZ/YZ/WH/WL/Wx/DS/CG/CangYun/SL/WD/YT` — the
  target player's school resource/state displays (e.g. 明教 `Text_Sun/Text_Moon`,
  藏剑 `Text_Short/Text_Long`, 苍云 `Text_Rang`, 天策 `Text_TC`, 丐帮 `Text_GBNum`,
  衍天 `Handle_100/200`) plus `Image_BuffBG`/`Image_DebuffBG`.
* Display options (custom data): `bShowStateValue`, `bShowSimpleBlood`,
  `bShowPlayerSimpleBlood`, `bShowSelfDebuff`, `bStandard`, `Anchor`, `nVersion`;
  `Target.nCurrentVersion = 2`; frame drag updates `TARGET_ANCHOR_CHANGED`.
* Events that refresh/hide it: `NPC/PLAYER_STATE_UPDATE`, `NPC/PLAYER_LEAVE_SCENE`,
  `UPDATE_RELATION`, `PLAYER_LEVEL_UP`, `CHANGE_CAMP`, `BUFF_UPDATE`,
  `SET_SHOW_STANDARD_TARGET`, `UI_ON_DAMAGE_EVENT`, mini-avatar events.

MED: the exact window instantiation (the frame is the `Player` window family;
whether self and target instantiate the same INI or a sibling `Target` INI is not
pinned — the module + element names above are the target path).

## Reproduce

```powershell
python tools\pvp\dump_fn_disasm.py "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe" `
  --names names.txt --out-dir proof\pvp\disasm_targeting
# names.txt: KPlayer::LuaSearchForEnemy / _Allies / _Npc / _PetEnemy /
#            LuaFollowSelectTarget / LuaStopFollow / KSkill::AutoSelectTarget
```

Target-frame HUD layout (read-only, official PakV4SfxExtract via
`pss_assets.run_pakv4`): `ui/config/default/Player.ini` +
`ui/config/default/TargetCommon.ini`, parsed summary
`proof/controls/target_frame_elements_20260930.txt`.

Last verified: 2026-09-30
