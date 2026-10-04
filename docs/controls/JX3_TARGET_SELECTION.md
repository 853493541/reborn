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

**Left click = `CAMERAORSELECTORMOVE` (HIGH binding, MED clear branch).** The
client's hotkey table binds LMB to the action `CAMERAORSELECTORMOVE` (=1;
`proof/movement/extracted/ui_hotkey_default.txt:35-36`, RMB =
`CAMERAORSELECTORMOVESTICKY`), and `ui/hotkey/bindings.ini:309-313` maps
`down=CameraOrSelectOrMoveStart(0); up=CameraOrSelectOrMoveStop(0);` — i.e.
"rotate camera **or select under cursor**" (`docs/movement/JX3_COLLISION_SYSTEM.md`
§16.1). The Lua handlers are the client functions surfaced as
`Ctrl_CameraOrSelectOrMoveStart/Stop` (`ui/script/control.lua` stubs + wrappers
`ui/script/hotkeys.lua` protos 141/142; PakV4-extracted bytecode). Clearing uses
the same target setter as selection: `KTarget::SetTarget` accepts type 1 =
`NO_TARGET` (enum 1/3/4/5/6 = NO_TARGET/DOODAD/PLAYER/NPC/ITEM; `0x140241C00`)
and the client itself calls `SetTarget(player, NO_TARGET, 0)` at `0x140318C33`
(auto-clear when the target leaves the frontal cone). The exact empty-pick
branch of `CameraOrSelectOrMoveStop` is not pinned in the binaries (the handler
name is not a string in `JX3ClientX64.exe`/bin64 DLLs; it is registered at
runtime) — confidence MED; re-open if a script/IL dump shows otherwise.

Implemented in `client/RebornClient.cs` (2026-10-01): a left click without drag
runs the cursor pick; a hit selects, an empty pick **deselects** (`click:
deselect (nothing under cursor)`), mirroring the client's select-under-cursor +
NO_TARGET clear. Scripted verify: `RC_TAB_AT=3000 RC_CLICK_AT=5000,10,10`
(proof/controls/target_deselect_click_20261001.txt).

**Click hit volume (host approximation, MED).** The real client picks the
character model (`KCharacter::OnPickPrepare` / represent `PickDoodad`); the
host exposes no world→screen or model pick, so `TargetSelector.Pick` tests the
cursor ray against each entity's **vertical body cylinder** (radius 90 u,
height 220 u; `client/Targeting.cs`) and takes the nearest ray hit — any ray
that misses every body is an empty pick → deselect. This replaced an earlier
12° cone test that selected on near-misses (clicks ~1 m off the dummy).
Scripted verify: `RC_CLICK_AT=5000,620,430;7000,700,440` — 620,430 (~80 px off
the body) deselects, 700,440 (on the body) selects
(proof/controls/target_deselect_hit_test_20261001.txt). Re-open when the host
exposes a real model pick, then use it instead of the cylinder.

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

## 9. What HUD appears when a target is selected (HIGH)

The selected-target HUD is the window **`ui/Config/Default/TargetTarget.ini`**
(50 sections, `ScriptFile=UI\Config\Default\Target.lua`, authored 325x115 at
500,124; PakV4-extracted 2026-09-30, element list:
`proof/controls/target_frame_elements_20260930.txt`). `Player.ini` is the
player's own frame; the target window is TargetTarget (confirmed by the
`Text_Target` / `Handle_TarBg` names in Target.lua's bytecode).

| group | elements (TargetTarget.ini) | meaning |
|---|---|---|
| frame/back | `Image_TarBg`, `Image_TarBgF` (`UItimate/UICommon/TargetBg.UITex`), `Image_Target`, `Image_NewTarget` | target plate |
| avatar | `Handle_Avatar` (`Image_Avatar`, `Image_School`, `Animate_Avatar`), `Handle_BuyBg` (`Image_BuyBG1-3`, `CHGJ_1_DK_2.UITex`) | portrait + school icon + wardrobe bg |
| name/level | `Handle_Name` (`Text_Target`, `Image_Cloud`), `Text_Level`, `Image_Danger` | name, level, danger mark |
| health | `Image_Health` (fill, width = HP%), `Image_SubHealth` (shield overlay), `Text_Health` | HP bar + shield + text |
| mana | `Image_Mana`, `Text_Mana` | player targets only |
| camp/relation | `Image_Camp` (`CommonPanel2.UITex` frame 5) | faction color |
| marks/state | `Image_NPCMark`, `Image_Invincible` (`Baizhan.UITex`), `Image_HPmark` | marks, invincible |
| cast bar | `Handle_Bar` (`Image_Bg`, `Image_Progress`, `Image_FlashS/F`, `Text_Name`; `ProgressBar.UITex`) | target prepare/channel bar |
| custom | `Wnd_CustomMode` (`Image_CM*`, `Text_CMName`) | custom-UI label |

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

Implemented in `client/` (2026-09-30): `Targeting.cs` (Tab cone search + click
pick) and `UiClient.cs` (renders this window from the real `.UITex` atlases +
`ui/Font` through `ui/Scheme/Elem/{font.ini,fontlist.ini,color.txt}`), composited
by a per-pixel-alpha layered window. Client-side subset currently drawn: plate,
name/level/HP/shield/camp + real fonts; runtime-set portrait face, buff rows and
cast bar are skipped until their state exists (missing art draws nothing).

## 10. In-world target indicator (the visuals around the target) (HIGH config, MED host)

The HUD window in §9 is 2D; the marker drawn **around the selected target** is
the represent layer's `KRLTarget` (JX3RepresentX64.dll). Research 2026-10-01 on
the game client (`bin64\JX3RepresentX64.dll`, disasm in
`proof/controls/target_indicator_client_20261001.txt` companion notes):

* **Per-relation config**: `represent/common/force_relation_care.txt`
  (loaded as `ForceRelationCareTable`, `KTableList::LoadBinTextTab`
  `0x18082C598`, stride 0x2C, format `iiOiiiifpp`; PakV4-extracted). Columns:
  `CaptionType, ForceRelationType, Desc, 3DColor, 2DColor, TargetColor,
  DeadColor, SFXScale, SFXFile, SFXEn`. Rows per relation
  (`0=Invalid 1=Foe 2=Enemy 3=Neutrality 4=Party 5=Ally 6=Self 7=None 8=All`,
  CaptionType 0/1), `SFXScale=1.8`, e.g. Enemy →
  `data/source/other/HD特效/其他/Pss/选择特效a002_hd.pss` + `J_角色箭头面向.pss`.
  The six `选择特效a001..a006_hd.pss` share one texture
  (`QT_其他/选择特效A001.tga`) and differ only in colour params — the ring is
  tinted per relation by the row colours.
* **Who draws it**: `KRLTarget::Init` (`0x180567c40` game / `0x18057a0a0` show)
  loads `SelectionEnhance→Arrow→SFX` (the 10 `J_角色箭头面向*.pss` facing arrows);
  `KRLTarget::LoadFile` loads the 9 relation rows; `KRLTarget::Show(relation,..)`
  shows the relation's model; `KRLTarget::EnableBraceSfx(a,b,index)` attaches a
  `CommonCursorEffect` row (`represent/common/cursor_effect.txt` →
  `data/source/other/特效/系统/SFX/其他/鼠标移动.Sfx`, the ground ring) as the
  "brace"; the UI script `GlobalEventHandler.lua` (compiled, function at
  line 5486) calls `TargetSelection_ShowSFX(relation, flag)` →
  `KRepresentScriptTable::LuaTargetSelection_ShowSFX` (JX3UIX64) → represent
  event → `krlEventAdaptor::HandleShowTargetSelectedSFX` →
  `KGameWorldHandler::ShowTargetSelectionSFX` → `KRLTarget`.
* **Client implementation** (2026-10-01, `client/RebornClient.cs`): on selection
  change the sandbox spawns the game's own assets at the target through
  `AddDummyModel` (engine loads PSS + textures from the client VFS): the
  relation selection ring at the target base (Enemy/red `a002` by default,
  `RC_INDICATOR_SEL`) and the facing cone at the base yawed to the target's
  facing (`J_角色箭头面向.pss`, `RC_INDICATOR_ARROW*`). Removed on deselect.
  `RC_INDICATOR=0` disables; `RC_TARGET_HUD=1` re-enables the §9 HUD (default
  off — the request is the in-world marker, not the HUD).
* **Host limitation (registered deviation, re-open criteria)**: the native
  `KRLTarget` attach path needs the represent game world. In the
  MovieEditor-hosted engine the represent singleton is null
  (`JX3RepresentX64.dll` RVA 0xF06A50 reads 0), so `AttachSceneObject` cannot
  run; the cursor-ring "brace" is a compiled `.Sfx` and cannot be fed to
  `AddDummyModel`. Re-open when the host initializes the represent world (or
  `AddRepresentModel` exposes a usable character id), then call
  `AttachSceneObject → EnableBraceSfx → Show` for the exact game composition.
  Proof: `proof/controls/target_indicator_client_20261001.txt` (+ the
  before/after PNGs and per-region RGB in the same file).

## Reproduce

```powershell
python tools\pvp\dump_fn_disasm.py "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe" `
  --names names.txt --out-dir proof\pvp\disasm_targeting
# names.txt: KPlayer::LuaSearchForEnemy / _Allies / _Npc / _PetEnemy /
#            LuaFollowSelectTarget / LuaStopFollow / KSkill::AutoSelectTarget

python tools\netcode\ui\extract_target_frame.py
# -> assets\ui\targetframe\ (gitignored): TargetTarget.ini, TargetCommon.ini,
#    the .UITex atlases + .Tga textures, ui/Scheme/Elem scheme files
# parsed summary: proof/controls/target_frame_elements_20260930.txt

# in-world indicator (2026-10-01): ForceRelationCareTable + KRLTarget assets
#   set RC_CLIENT_EXE=reborn_client_target-dummy.exe && client\build_client.cmd
#   set RC_TITLE=targeting
#   set RC_TAB_AT=3000
#   set RC_SHOTS=2500,5000,9000
#   set RC_AUTORUN=11000
#   cd /d C:\SeasunGame\MovieEditor && bin64\reborn_client_target-dummy.exe
#   evidence: proof/controls/target_indicator_client_20261001.txt
```

Last verified: 2026-10-01
