# JX3 battle floating UI — client catalog (what Reborn needs)

**Branch:** `agent/battle-floating-ui` (worktree `../reborn-iso-battle-floating-ui`)
**Date:** 2026-09-30
**Method:** PakV4 extraction of the battle UI modules + native caption configs/tables,
unluac decompilation of the shipped Lua 5.1 bytecode, and xref/disasm probes on the
installed client `bin64`. Research is copy-and-analyze only; the install was not written to.
**Evidence roots:** candidate lists `proof/ui/evidence/battle_hud/*.txt` (tracked);
extracted assets `proof/ui/battle_hud/` (ignored, local); decompiled Lua
`proof/ui/battle_hud/decompiled/` (ignored, local); `proof/gravity/number.krl.txt` (tracked);
loose install-root `caption.ini`; client binaries cited by symbol/RVA (2026-09-30 build).

Confidence: **HIGH** = shipped file/string/RVA cited · **MED** = inferred from code shape
or naming, not fully connected · **LOW** = plausible, unproven. `INFERRED` marks joins
that still need runtime confirmation.

**Scope:** what the client ships for in-combat floating/HUD UI, general-combat priority,
with a short "how Reborn carries it" note per entry. Feature work itself is out of scope.

---

## 0. TL;DR

The client has **two floating layers**, not one:

1. A **native world-space caption layer** (`KG3D_CaptionManager` in
   `KG3DEngineAdapterX64.dll`): nameplates / 头顶血条, head-top status icons, balloons,
   head-top buff rows, and the head-top cast text. Its data is fully shipped and local:
   `data/public/caption*.ini`, `Represent/common/caption_*`, `number.krl` layout rows.
2. A **KGUI screen layer** (1,700+ `ui/**` modules): player/target frames, buff bars,
   floating combat text, combo counter, kill feed, scoreboards, bars, warnings. Movable
   windows persist their position through `StorageServer` key `OnlineFrameAnchor`
   ("floating panel" persistence), and **world-anchored text gets its position per call
   through the `Scene_*ScreenPos` bindings** (`CombatText` is the damage-number renderer).

Everything needed to reproduce the list below exists locally (PakV4 UI + `data/public`
+ `settings` tables + `Represent` configs); the only missing pieces are the consumer
scripts listed in §4, all named with their next probe.

---

## 1. Layer model

### 1.1 Native world-space caption layer (nameplates, head-top icons, balloons)

The live manager is the engine adapter:

| component | symbol / RVA | source |
|---|---|---|
| config load (reads `data/public/caption_ui.ini`) | `KG3D_CaptionManager::_LoadCaptionConfig` `0x180058420` | `KG3DEngineAdapterX64.dll`, xref re-run 2026-09-30 |
| `[FontConfig]` parser | `_LoadCaptionConfigFromFile` `0x180058740` | same |
| HP-bar UI loader (`[HPBar]`, `Image_%d`, `UV*`, `IsPer`, `IsShield`) | `_LoadHPBarUI` (strings) | same |
| texture/SFX slots | `_SetCaptionTextureSlot`, `_SetCaptionSFXSlot`, `RenderCaptionTextureSlot`, `RenderIcon` | same |
| icons on/off / reload | `SetShowIcon` `0x180082B80`, `ReloadConfig` `0x180096E40`, `reload_caption` dispatch `0x180077300` | same |
| legacy D3D9 reader (still shipped) | `KG3DEngineX64.dll` fn `0x1802D5DE0` (`FadeStartDis` assert), `KG3DCaption2D::Render` `0x1802D71E0` | client `bin64` |

Shipped data (PakV4, extracted to `proof/ui/battle_hud/pakv4_native/`):

| file | content | probe |
|---|---|---|
| `data/public/caption.ini` | `[FontConfig]` (NewHPBar, HPBarWidth/Height, Size, PHBorder, Fade*, Min/MaxScale, Icon/W/H, Kungfu*, Title*/RightIcon*) + `[BHG0SP0..3]` distance→show table + `[OldVersion]` | HIT |
| `data/public/caption_ui.ini` | `[HPBar]` new bar atlas: `ui\Image\UICommon\PH.tga`, `TextureCount=4`, `Scale=0.8`, plus `[Image_0..3]` UV slices (back / `IsPer=1` fill / `IsShield=1` fill / border) | HIT |
| `data/public/captionTextureSlot.ini` | `[CaptionTextureSlot] SlotCount=5`; slots anchored `HPLeftTop`, `NameLeft`, `TextRightCenter`, `TitleIndexCenter`, `NameLeft` (W/H/Offset/`AffectNameTextLayout`/`ScaleByFont`) | HIT |
| `data/public/HPBar.tga` | the old bar texture referenced by `[FontConfig]` | HIT |
| `Represent/common/caption_color_group.txt` (+ `_care`) | `CaptionGroup × ForeceRelationType → 3DColor/2DColor/TargetColor/DeadColor` (Invalid/Foe/Enemy/Neutrality/Party/Ally/Self/None/All) | HIT |
| `Represent/common/caption_icon_type.txt` | `TypeID · IconPosType (0 left small icon, 1 right big icon, 2 second left icon) · IconFile` (`ui/Image/UITga/LittleIcon/*.tga`) | HIT |
| `Represent/common/number.krl.txt` | head-top layout row: `nNpcAdjustOffsetY=200`, `nNpcAdjustSlipOffsetY=64`, `Caption*Height/FloorSpace*`, `BufferHeightN/Y1`, `CaptionSkillBufferHeight`, `CastSkillResetPeriod`, `SkillEffectResultTimeout`, `TitleAdjust*`, `HitMissAdjustOffset=1000` | tracked copy `proof/gravity/number.krl.txt` |
| `data/public/caption_images.ini` | not referenced by this build — the string is absent from `KG3DEngineAdapterX64.dll` (xref re-run 2026-09-30) and all path variants MISS | CLOSED (no such file) |

- Runtime override: the loose **install-root `caption.ini`** (`C:\SeasunGame\Game\JX3\bin\zhcn_hd\caption.ini`,
  `NewHPBar=1`, `HPBarWidth=51`, `BorderWeight=2`) is the user/settings overlay; the UI writes it
  (`PVPShowPanel.lua` reads/writes `caption.ini` + `data/public/caption.ini` values and issues
  `k3dcmd('reload_caption')`; `UISetting_HeadTop.lua` drives `HT_*` StorageServer keys and
  `Global_SetCaptionParam`). HIGH.
- Lua controls in `JX3UIX64.dll`: `LuaGlobal_SetTopHeadFlag` `0x1800B0E60`,
  `LuaScene_GetCharacterSkillEffectTextPos` `0x1800BBF60` (re-verified 2026-09-30),
  `Scene_GetCharacterTop*`, `Scene_SetCharacterTopBufferScreenPos`.
- Renderer side (`JX3RepresentX64.dll`): `KRLCharacter::UpdateTitleMarkPosition` `0x1805129E0`,
  `GetHeadTopBufferHeight` `0x1804DA270` (re-verified), `UpdateBalloonPosition` `0x1805020A0`
  (re-verified), `PlaySkillEffectText` `0x18059FA10` (re-verified); `krlEventAdaptor::HandleUpdateHeadTop`
  and the `STKREPRESENT_EVENT_UPDATE_HEADTOP` protobuf carry per-player head-top param updates.

**Reborn note:** the host already hosts these DLLs; the native nameplate is an engine
billboard that should be driven through the adapter's own config path rather than
reimplemented. The static data (bar UVs, slots, colors, icon ids, distance table) is
complete locally, so a first parity pass can be data-only.

### 1.2 KGUI screen layer + floating anchors

Every HUD window is a `WndFrame` in a UI layer (`Lowest*`, `Normal`, `Topmost*`).
Movable windows persist their anchor through `StorageServer` key `OnlineFrameAnchor`
(`GetFrameAnchor` / `SetData` + `SetPoint`) and fire a `*_ANCHOR_CHANGED` event;
`IsCustomDragable` + `Moveable` + custom-UI mode (`SNYC_?`/`CUSTOM_UI_MODE_SET_DEFAULT`)
is the client's "floating panel" system.

Windows in this catalog that persist an anchor: `FightingNum`, `BuffList`, `DeBuffList`,
`TargetBuff`, `TargetDeBuff`, `TeamBuff`, `ComboPanel`, `MainBarPanel`, `MainMessageLine`,
`KillMessage`, `Playerbar`, `HatredPanel`, `TimeBuff`, `FightingStatistic`, `WhoSeeMe`
(TOPCENTER), `ProgressBar`/`PQprogressbar`/`GeneralProgressBar` (generic bars). HIGH.

### 1.3 World→screen bridge (how "floating" text is placed)

`JX3UIX64.dll` bindings/events (strings; addresses in the DLL's table):
`Scene_GameWorldPositionToScreenPoint`, `Scene_GameWorldPositionListToScreenPointList`,
`Scene_ScenePointToScreenPoint`, `Scene_GetCharacterTop`, `Scene_GetCharacterTopScreenPos`,
`Scene_GetCharacterSkillEffectTextPos`, plus the event ids `CHARACTER_TOP_2_SCREEN_POS`,
`CHARACTER_POS_2_SCREEN_POS`, `DOODAD_POS_2_SCREEN_POS`, `GAME_WORLD_2_SCREEN_POS`,
`CHARACTER_HAT_2_SCREEN_XYZ`, `GET_VIEW_SPACE_DEPTH_FROM_SCREEN_POS`.

`CombatText` uses `PostThreadCall(OnUpdateTextPos, text, "Scene_GetCharacterSkillEffectTextPos", id)`
and keeps the world track with `Scene_ScenePointToScreenPoint` (HIGH, decompiled).
`TopBuff` is engine-anchored per character head through `MultiFrame`/`ClassName`
(INFERRED, see below).

---

## 2. Catalog

Format per entry: **module** — mechanism · root/anchors · update events · status.
All extract hits are `proof/ui/battle_hud/pakv4/*` unless stated; decompiles are
`proof/ui/battle_hud/decompiled/*.decompiled.lua`.

### 2.1 World-floating / head-top

#### Nameplate / 头顶血条 — native caption layer
See §1.1. Data complete: HP bar atlas + 4 UV slices, 5 texture slots with anchors,
relation colors (2 groups × care variant), icon atlas ids, `number.krl` height/floor-space
layout, per-distance show table `[BHG0SP0..3]`.
**Reborn:** engine billboard via adapter; first step is a data-parity checklist (colors,
sizes, slots) against the configs.

#### TopBuff — head-top buff icon row (P1)
- Module `TopBuff` (`DefaultScript54`, `load=true`, layer 2) + settings `TopBuffSet` (`load=true`)
  and `UISetting_HeadTop` (head-top caption settings page).
- INI: `[TopBuff]` `WndFrame`, `._Parent=Lowest`, `MultiFrame=1`, `ClassName=TopBuff_Base`,
  `RenderEvent=1`, `ShowModeID=36`; children `Handle_List` (HandleType=3), `Handle_Buff`,
  `Shadow`, `Shadow_Level` (`TopBuff.ini`, 5 sections).
- Instancing: `TopBuff_CreateTopBuff(playerID)` opens one window per player
  (`Wnd.OpenWindow("TopBuff", "TopBuff"..id)`); icon x math in `TopBuff_AddBuffIcon`
  (x = `nLoadBuffCount*(w+6) - nBuffCount*(w+6)/2 + 3`, y = `-h + shadowOffset + TopBuffSet.GetOffsetYOriginNum()`).
- Persistence: `TopBuffSize1`, `TopBuffOffsetY1`, `UISetting_BoolValues2/TOP_BUFF_CLOSE`.
- Events: per-instance `BUFF_UPDATE`; statics `TARGET_CHANGE`, `LOADING_END`,
  `TOPBUFF_UISETTING_CHANGE`, `PVP_SHOW_PLAYER_UPDATE`, `PLAYER_ENTER_SCENE`, `PLAYER_LEAVE_SCENE`.
- Data: native `settings/TopBuff.tab` (HIT; 2,047 rows; fields `ID · ShowLevel · Desc ·
  IconFrame1..3 · StrengthLevel1..3 · LevelFrame1..3 · nVKPathNum1..3`), bindings
  `LuaIsHaveTopBuff` / `LuaGetTopBuffInfo` (`KScriptFuncList::…`, `JX3ClientX64.exe`
  strings `0x7F6DA2`/`0x7F6E37`), loader `KBuffManager::LoadTopBuffInfo` (string `0x83CBE8`).
- `TopBuffSet` window: `[TopBuffSet]` `WndFrame` `_,_Parent=Normal`,
  `AnchorArgs=TOPCENTER,TOPCENTER,80,0`, drag+close/sure buttons, `GetOffsetYOriginNum` default `-70`.
- `UISetting_HeadTop.lua` drives `HT_*` keys (`HT_NewPH`,
  `HT_PHPercent`, `HT_Lod`, `HT_Fade`, `HT_FadeSDis`, `HT_Size`, `HT_Border`, `HT_Span`,
  `HT_PHHeight`, `HT_PHBorder`, `HT_PHWidth`, `HT_Zoom`, `HT_BorderColor`), calls
  `Global_SetCaptionParam`, and toggles care mode (`rlcmd "enable care mode 1/0"`).
  There is **no `UISetting_HeadTop.ini`**: the page lives inside `UISetting.ini`
  (HIT, 505 KB / 1,749 sections: `WndContainer_HeadTop`, `HI_HeadTop`, `Text_HeadTop*`,
  `Scroll_List_HeadTop`, `Btn_Up/Down_HeadTop`) — closed 2026-09-30.
**Reborn:** KGUI window per player + world→screen anchor; icon atlas + `TopBuff.tab` are local.

#### CombatText — floating combat text (damage/heal/miss/dodge/block/immunity) (P1)
- Module `CombatText` (`load=true`, layer 2). INI: `[CombatText]` `WndFrame`,
  `._Parent=Lowest`, `ShowModeID=27,15`, `MousePenetrable=1`, 0×0 root;
  `Handle_Total` 1440×1050 with `Handle_Level0` / `Handle_Level1` (4 sections).
- Pools: Level0 ≤30 texts, Level1 ≤20 (decompiled 879-885); font scheme 19.
- Track system: `m_tTrackList` with `POINT_TYPE_MOVE / QUEUE / MERGE` (1/2/3),
  per-text `nOffsetX/nOffsetY`, `CreateText` parks at `SetAbsPos(0,-1000)` then
  `PostThreadCall(OnUpdateTextPos, text, "Scene_GetCharacterSkillEffectTextPos", characterID)`;
  `AdjustToOriginalPos` projects via `Scene_ScenePointToScreenPoint`; `RENDER_FRAME_UPDATE`
  drives motion/merge; merge/queue algorithms 254-720.
- Events (exact registrations): `UI_SCALED`, `COMMON_HEALTH_TEXT`, `SKILL_EFFECT_TEXT`,
  `SKILL_MISS`, `SKILL_DODGE`, `SKILL_BLOCK`, `SKILL_BUFF`, `BUFF_IMMUNITY`,
  `PLAYER_LEAVE_SCENE`, `FIGHT_HINT`, `ON_EXP_LOG`, `COMBATROUND_CHANGE`,
  `REPRESENT_DODGE_TEXT`, `REPRESENT_BLOCK_TEXT`, `REPRESENT_IMMUNITY_TEXT`,
  `REPRESENT_MISS_TEXT`, `REPRESENT_SKILL_EFFECT_TEXT`, `UPDATE_ACTIVITYAWARD`.
  Text comes from `g_tStrings.COMBAT_STATE_TEXT[<event>]` (state ids 103/104/105 for
  miss/dodge/immunity).
- Native producer: `KRLSkillEffectResult::Activate` → `PlaySkillEffectText` (`0x18059FA10`)
  → `KGameWorldUIHandler::OnSkillEffectLog` (`SYS_MSG_UI_OME_SKILL_*`, `REPRESENT_FIGHT_LOG`,
  `REPRESENT_DODGE_TEXT`, `REPRESENT_MISS_TEXT`; `JX3UIX64.dll` strings around `0x393B00-0x394620`).
**Reborn:** this is the damage-number system; overlay window + billboard tracks. The
merge/queue behavior lives in the decompiled Lua (port or re-derive against captures).

#### Cast/channel bar (施法条) (P1) — driver identified (2026-09-30)
- Native event ids `CASTINGBAR_START` / `CASTINGBAR_END` (`JX3UIX64.dll` strings
  `0x393CC0`/`0x393CD8`) — no Lua consumer found in ANY extracted corpus
  (`proof/netcode/ui_scripts` 229 scripts, the 144-file map corpus, the battle-HUD
  decompiles, local install ui): they are for a native listener, not a Lua module.
- **The bar driver is the native represent layer via `REPRESENT_CALL`:**
  `ui/script/representcommand.lua` registers `REPRESENT_CALL` and maps
  `CreateProgressBar` → `GeneralProgressBar_Create(arg1..arg7)` and `CloseProgressBar`
  → `GeneralProgressBar_Close(arg1)` (decompiled 2026-09-30). So the engine calls
  into Lua with a bar name + preset id; the Lua side creates the window.
- Presets: `ui/Scheme/Case/ProgressBar.tab` (HIT; 161 rows: `ID · Side · OffsetX ·
  OffsetY · Left_Right · Way · ImagePath · Frame · SFXPath · FullSFXPath · Tip ·
  szIniName · szMobileProgressColor`, e.g. row 0 = TOPRIGHT/-50/350, `PqUI1.UITex`
  frame 23, `szIniName=GeneralProgressBar`). `GeneralProgressBar_Create(name, tableID,
  title, describe, molecular, denominator, …)` looks the row up via
  `Table_GetProgressBar` and opens window `"GPB_"..name` using the row's `szIniName`.
  `ProgressBarPlus.txt` gives the fill direction (`left_to_right`/`right_to_left`);
  `AutoProgressBarInfo.txt` maps auto-bar table ids to per-id INIs + icon atlases.
- The separate generic `ProgressBar` module (`[Frame_ProgressBar]`,
  `AnchorArgs=BOTTOMCENTER,BOTTOMCENTER,0,-195`, `ShowModeID=6`, API `Start/Finish`) is
  the CangYun/OT-style action bar; `PQprogressbar` is the stage bar.
- Head-top cast text: `CaptionSkillBufferHeight` + `CastSkillResetPeriod` in the
  `number.krl` layout row. Target frame cast art: `TargetCommon.ini` carries
  `Animate_Long/Image_Long/Text_Long` (+ `Animate_Short`) — the target cast/channel
  indicator (MED).
- Status: driver + presets HIGH; which `ProgressBar.tab` row a given skill/action uses
  (and the `CASTINGBAR_*` native listener) still open — next probe: breakpoint the
  `REPRESENT_CALL` dispatch during a cast, or scan for `Table_GetProgressBar` callers
  outside the extracted subset.

#### Balloons (doodad/NPC bubble text) (P3) — native
`KRLCharacter::UpdateBalloon` / `UpdateBalloonPosition` (`0x1805020A0`, re-verified),
`KRLDoodad::UpdateBalloon`, `KGameWorldHandler::ShowDoodadBalloon`, caption setter
`OnUpdateCharacterBalloon`. Existence HIGH; renderer behavior MED.

### 2.2 Self and target frames

| UI | root / anchor | mode | notes |
|---|---|---|---|
| `Player` | `[Player]` `AnchorArgs=TOPLEFT,TOPLEFT,5,13` | 27,15 | self portrait/HP panel (`Player.area`), `Player.lua`: `PLAYER_STATE_UPDATE`, `PLAYER_LEVEL_UPDATE`, `UI_ON_DAMAGE_EVENT`, `UPDATE_SLAY_KILL_COUNT`, `SET_SHOW_*`, `BUFF_UPDATE`, force/pose/proxy events; drag anchor |
| `Playerbar` | `[Playerbar]` `TOPLEFT,-10,73` | 27,15 | per-school resource/energy handles (`Handle_SL/CY/CJ/TM/QX/CangYun/CG`), `ScriptFile=Player.lua`, `OnlineFrameAnchor` |
| `Resourcebar` | `[Resourcebar]` `TOPLEFT,-10,73` | 15 | 7 qi dots (`Handle_QiControlList`), same script |
| `TargetResourcebar` | `[TargetResourcebar]` `TOPLEFT,-10,73` | 28,15 | target qi/resource bar, same script + `TARGET_CHANGE` |
| `TargetTarget` | `[TargetTarget]` no AnchorArgs | 17 | target-of-target, `ScriptFile=Target.lua`, `TargetTarget.area`; fires `TARGET_TARGET_ANCHOR_CHANGED` |
| `Target` | layout composed per type | — | `Target.ini`/`TargetS.ini` do **not** exist: `Target.lua` builds the name and appends `S` only in standard-target mode — players `TargetPlayer10` (not enemy) / `TargetPlayer11` (enemy), NPCs `"Target"..GetNpcIntensity(npc)..relation` (intensity `2|6→4, 5→3, 4→2, else 1`; relation 2 enemy / 1 neutral / 0 ally); opens instance `Normal/Target`; `TARGET_CHANGE`, `TARGET_ANCHOR_CHANGED`, damage/relation/party events |

Target layout probes (2026-09-30): `TargetCommon.ini` **HIT** (878 sections; root
`[TargetCommon]`, buff/debuff BG, `[Box]`, short/long cast art); the complete shipped
set **HIT**: `TargetPlayer10/11`, `TargetPlayer10S/11S`, `Target{10,11,12,20,21,22,30,
31,32,40,41,42}` and all 12 `S` variants (28 layouts). `TargetS.ini` **MISS** — it
never exists (the `S` suffix attaches to the base name).
**Reborn:** standard KGUI frames; target layout selection logic must be ported
(`Target.lua` naming rules above), buff boxes are dynamic (`BuffMgr`).

### 2.3 Buff / debuff overlays

| UI | root / anchor | mode | updates |
|---|---|---|---|
| `BuffList` (self buffs) | `[BuffList]` `AnchorArgs=TOPLEFT,TOPLEFT,30,142` | 27,15 | `BUFF_SET_*`, collapse, proxy-skill; `BuffMgr.RefreshBuffs`; `<box>` icon templates |
| `DeBuffList` (self debuffs) | `[DebuffList]` `TOPLEFT,30,240` | 27 | same pattern, red announce click |
| `TargetBuff` | `[TargetBuff]` `TOPLEFT,500,142` | 28,27,5,15 | `TARGET_CHANGE`, collapse, `bVisibleWhenHideUI` |
| `TargetDeBuff` | `[TargetDeBuff]` `TOPLEFT,500,248` | 28,27,5,15 | `bShowSelfDebuff`, `dwFullyType` TARGET/NPC |
| `BuffMgr` (script) | `ui/Script/BuffMgr.lua` + `TargetCommon.ini` | — | manager: `Register/Unregister/Modify/RefreshBuffs/ClearBuffs/GetInfo/InitCollapseInfo`; registers `LOADING_END`+`BUFF_UPDATE`; positions each icon box inside the owner handle (`SetAbsPos` at 207/484/584/606/713) |
| `BuffFold` | `[BuffFold]` no anchor | 27 | fold-out all-buffs panel (`STR_BUFFFOLDTIPS`) |
| `PopupBuffList` | `[PopupBuffList]` `Topmost1` | — | hover-popup list; `LEVEA_HOT_AREA` + 1000 ms fade; clamps to hot-area rect |
| `TimeBuff` | `[TimeBuff]` no anchor | — | 时效状态 timed-buff panel, default `TOPRIGHT,-400,400` |
| `TeamBuff` | `[TeamBuff]` no anchor | — | party buff-status rows + overflow popup, drag anchor, `load="false"` |

**Reborn:** all KGUI; icon art from `jx3_box.xml` + buff icon ids; updates mirror the
buff-sync netcode already documented in `docs/pvp/`.

### 2.4 Combat feedback

| UI | root / anchor | mode | role / updates |
|---|---|---|---|
| `ComboPanel` | `[ComboPanel]` `TOPRIGHT,-100,200` | 27 | combo counter (连击) with hit/crit stamps; `LOCAL_CHARACTER_HIT_RESULT`; 丐帮 special path |
| `ComboWinEffect` | `[ComboWinEffect]` `Topmost`, `SetPoint CENTER 0,250` | — | transient "combo win" SFX counter; API `Open(count)`; fade 3 s / close 5 s; caller external (INFERRED) |
| `FightingNum` | `[FightingNum]` `Normal`, `OnlineFrameAnchor` | — | combat-score HUD (attack/toughness/healing + equip score); SFX `Z_战斗出现/消失`; prefs `bShowFightingNum/bShowPVP` |
| `FightingStatistic` | `[FightingStatistic]` | 28 | 伤害统计 damage meter (`STR_FIGHTINGSTATISTIC_TITLE`); `QuerySkillStatData`; history `userdata/fight_stat/log_*.jx3dat` |
| `KillMessage` | `[KillMessage]` default `TOPRIGHT,-354,110`, `RenderEvent=1` | — | kill feed list, ≤6 entries, 5000/4000 ms, `Tween=ui\Animation\KillMessage_Ani.ini`, SFX `UI_击杀_*.pss`; `OnRemotekillMessage` |
| `KillInformation` | `[KillInformation]` `TOPCENTER,0,250` | — | single kill banner (killer → killed), fades; fires `DESERTSTROM_KILLMESSAGE` |
| `MainMessageLine` | `[MainMessageLine]` `Lowest2` top strip, `OnlineFrameAnchor` | — | top counters (currency/money/camp) + system messages; 14 events (`SYNC_COIN`, `MONEY_UPDATE`, `LOADING_END`, …) |
| `FullScreenWarning` | `[FullScreenWarning]` `Topmost2` | — | full-screen red-edge flash (`CampMaps/red.tga`); API `Open(duration,color)/Close`; auto-fade |

`PVPShowPanel` / `PVPShowFinal` (battlefield stats/scoreboard) are already covered by
`docs/netcode/JX3_MODE_UI_INVENTORY.md` §6-§7; `PVPShowPanel.dump.txt` shows the same
head-top controls used here (`Global_UpdateHeadTopPosition`, `SetGlobalTopHeadFlag`,
`TopBuff_SetOpenPVPTop`, caption.ini writes + `reload_caption`).

### 2.5 Battle flow / mode UI (cross-refs, not re-documented)

`EndOfBattle` (`[EndOfBattle]` `CENTER,CENTER,0,0`, 207 sections, 浩气/恶人 scoreboard,
external opener INFERRED) · `LootList` · `DynamicBattleRoyale` (BR bar
`BOTTOMCENTER,0,-7`: 8 default + 6 dynamic slots, `BATTLEACTIONBAR_BUTTON<n>`) ·
`MainBarPanel` · `WhoSeeMe`/`HatredPanel` · queue/loading/settlement: see
`docs/netcode/JX3_MODE_UI_INVENTORY.md` and `docs/netcode/JX3_MODE_UI_FLOW.md`.

---

## 3. Must-have table (general combat HUD)

Priority: **P1** = needed for any fight to be readable · **P2** = standard combat HUD ·
**P3** = situational/other modes.

| # | UI | Layer | Source status | Reborn carries it as |
|---|---|---|---|---|
| 1 | Nameplate/head-top (HP/name/title/icons) | native | data complete; adapter hosted | engine billboard via `KG3D_CaptionManager` path |
| 2 | Floating combat text (`CombatText`) | KGUI + world→screen | Lua decompiled; events known | overlay window + world tracks (port merge/queue) |
| 3 | Cast bar (`GeneralProgressBar` + presets) | KGUI + native `REPRESENT_CALL` | driver + `ProgressBar.tab` extracted | native call → `representcommand` → bar (§2.1) |
| 4 | Self frame (`Player`, `Playerbar`, `Resourcebar`) | KGUI | extracted | UiLayout render; anchors |
| 5 | Target + target-of-target (`Target`, `TargetTarget`, `TargetCommon`) | KGUI | extracted (28 target layouts HIT) | port target layout-name selection |
| 6 | Self buffs/debuffs (`BuffList`, `DeBuffList`) | KGUI | extracted | UiLayout + `BuffMgr` box math |
| 7 | Target buffs/debuffs (`TargetBuff`, `TargetDeBuff`) | KGUI | extracted | same |
| 8 | Head-top buffs (`TopBuff`, `TopBuffSet`) | KGUI per-player | extracted + `TopBuff.tab` | world anchor + icon row math |
| 9 | Combo counter (`ComboPanel`) | KGUI | extracted | UiLayout + hit-result event |
| 10 | Combat score (`FightingNum`) | KGUI | extracted | UiLayout; anchor persistence |
| 11 | Kill feed (`KillMessage` + `KillInformation`) | KGUI | extracted | UiLayout + kill event |
| 12 | Message line (`MainMessageLine`) | KGUI | extracted | UiLayout + 14 events |
| 13 | Warnings (`FullScreenWarning`) | KGUI | extracted | `Open/Close` + tween |
| 14 | Damage meter (`FightingStatistic`) | KGUI | extracted | UiLayout + stat queries |
| 15 | End of battle (`EndOfBattle`) | KGUI | extracted | UiLayout; opener TBD |
| 16 | Loot (`LootList`) | KGUI | extracted | already in inventory |
| 17 | BR skill bar (`DynamicBattleRoyale`) | KGUI | extracted | mode work (see inventory) |
| 18 | Threat/aggro + who-sees-me (`HatredPanel`, `WhoSeeMe`) | KGUI | extracted | UiLayout + threat list |
| 19 | Buff fold/popup/time (`BuffFold`, `PopupBuffList`, `TimeBuff`) | KGUI | extracted | UiLayout |
| 20 | Balloons | native | existence only | later; caption side |

---

## 4. Gaps and open questions

1. **Cast-bar row mapping** — driver identified (native `REPRESENT_CALL` →
   `representcommand.CreateProgressBar` → `GeneralProgressBar_Create` + `ProgressBar.tab`
   presets, see §2.1); `CASTINGBAR_START/END` have no Lua consumer. Open: which
   `ProgressBar.tab` row each cast/action uses, and the native `CASTINGBAR_*` listener.
2. ~~Target layout INIs~~ **CLOSED (2026-09-30)** — the full set is shipped and
   extracted: `TargetPlayer10/11(+S)` + `Target{10..42}` (12) and all 12 `S` variants,
   plus `TargetCommon.ini`. `TargetS.ini` never exists; naming rule in §2.2.
3. ~~`UISetting_HeadTop.ini`~~ **CLOSED (2026-09-30)** — it is not a file: the page is
   `WndContainer_HeadTop` inside `UISetting.ini` (HIT, 1,749 sections).
4. ~~`caption_images.ini`~~ **CLOSED (2026-09-30)** — not referenced by this build
   (string absent from `KG3DEngineAdapterX64.dll`; all variants MISS).
5. ~~WhoSeeMe script conflict~~ **CLOSED (2026-09-30)** — `WhoSeeMe.lua` registers the
   window itself (`ui/Config/Default/WhoSeeMe.ini`, `Normal/WhoSeeMe`,
   `WhoSeeMe.DefaultAnchor/bCheck`, `SYNC_SELECT_ME_PLAYER_NOTIFY`); `HatredPanel.lua`
   only drives its own window. The INI `ScriptFile=HatredPanel.lua` is stale; the module
   registration binds `WhoSeeMe.lua`.
6. **`ShowModeID` semantics (partially decoded 2026-09-30)** — the window decoder
   (KGUIX64 `0x1800CD780`, key read at `0x1800CDE77`) parses a comma list (≤128 ids,
   ≤127 each) into a 128-bit mask at `window+0xC58`; right after parsing it
   **sets/clears bit 0 (`or/and [rbx+0xC58]` at `0x1800CE034`) from `ShowWhenHideUI != 0`**
   — so Default-mode visibility is governed by `ShowWhenHideUI`, not by the list.
   `[Balloon] ShowModeID` in the config uses the same mask format (fn `0x18020E250`).
   Open: the actual `IsVisibleInShowMode` test (current-mode vs mask, and what "no
   current mode" means) — next probe: `KWndStation::InitShowModeInfos` /
   `IsVisibleInShowMode` implementation.
7. ~~`REPRESENT_*` alias table~~ **CLOSED (2026-09-30)** — the `CombatText` alias table is
   dead code; `OnEvent` dispatches by explicit string comparison
   (`REPRESENT_MISS_TEXT`/`DODGE`/`IMMUNITY` → `RepresentNewStateText`, at
   decompiled lines 1104-1113). No action needed.
8. **External openers** for `EndOfBattle`, `FullScreenWarning`, `ComboWinEffect`,
   `ProgressBar.Start/Finish` are outside the extracted subset (INFERRED callers).
9. **Head-top buffs render owner** — Lua `TopBuff` window vs native caption texture slots:
   both exist; which one shows the row needs a runtime check (MED).

## 5. Reproduce

```powershell
# inside the worktree; .venv from the main checkout is used as the interpreter
$py = "..\reborn\.venv\Scripts\python.exe"     # adjust if needed

# 1) UI modules (INI + Lua) from PakV4
& $py tools\netcode\extract_pak_paths.py --list proof\ui\evidence\battle_hud\pakv4_candidates_ui.txt --out-dir proof\ui\battle_hud\pakv4
& $py tools\netcode\extract_pak_paths.py --list proof\ui\evidence\battle_hud\pakv4_candidates_castbar.txt --out-dir proof\ui\battle_hud\pakv4

# 2) native caption configs + TopBuff.tab
& $py tools\netcode\extract_pak_paths.py --list proof\ui\evidence\battle_hud\pakv4_candidates_native.txt --out-dir proof\ui\battle_hud\pakv4_native

# 2b) target-frame layout probes
& $py tools\netcode\extract_pak_paths.py --list proof\ui\evidence\battle_hud\pakv4_candidates_target.txt --out-dir proof\ui\battle_hud\probe

# 3) decompile (the repo jar is a placeholder; build unluac from source once)
javac -encoding UTF-8 -d %TEMP%\unluac_build "@sources.txt"   # see SOURCES.txt in proof/ui/battle_hud
jar cfe %TEMP%\unluac.jar unluac.Main -C %TEMP%\unluac_build .
java -Dstdout.encoding=UTF-8 -jar %TEMP%\unluac.jar proof\ui\battle_hud\pakv4\CombatText.lua > proof\ui\battle_hud\decompiled\CombatText.decompiled.lua

# 4) native xrefs (read-only on the install)
& $py tools\netcode\xref_string.py C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineAdapterX64.dll "_LoadCaptionConfig"
& $py tools\netcode\xref_string.py C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3RepresentX64.dll "PlaySkillEffectText"
& $py tools\netcode\xref_string.py C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3UIX64.dll "LuaScene_GetCharacterSkillEffectTextPos"
```

## 6. Evidence index

| path | content |
|---|---|
| `proof/ui/evidence/battle_hud/pakv4_candidates_ui.txt` | 67 module paths (65 HIT) |
| `proof/ui/evidence/battle_hud/pakv4_candidates_native.txt` | 11 native config/table paths (8 HIT) |
| `proof/ui/evidence/battle_hud/pakv4_candidates_castbar.txt` | bar modules + `ProgressBar.tab` / `AutoProgressBarInfo.txt` / `ProgressBarPlus.txt` (9 HIT) |
| `proof/ui/evidence/battle_hud/pakv4_candidates_target.txt` | target layouts + `UISetting.ini` (29/30 HIT; only `TargetS.ini` MISS by design) |
| `proof/ui/battle_hud/SOURCES.txt` | exact commands + result counts (local, ignored) |
| `proof/ui/battle_hud/pakv4/`, `pakv4_native/`, `probe/` | extracted INIs/Lua/art/tables (local, ignored) |
| `proof/ui/battle_hud/decompiled/` | 33 unluac outputs + probe decompiles (local, ignored) |
| `proof/gravity/number.krl.txt` | head-top layout + cast-text params (tracked) |
| `docs/netcode/JX3_MODE_UI_INVENTORY.md`, `JX3_MODE_UI_FLOW.md` | mode UI inventory/flow (cross-ref) |
| `docs/ui/UI_SYSTEM_REPORT.md` | KGUI system, formats, reproduction procedure (cross-ref) |
| `docs/pvp/` buff/CC research | buff data semantics (cross-ref) |

**Verified (2026-09-30):** PakV4 extraction 65/67 UI + 8/11 native + 9/9 bar incl.
`ProgressBar.tab` + target set 28/28 + `UISetting.ini` HIT; 33 modules + probe files
decompiled with a locally built unluac; `representcommand.lua` shows
`REPRESENT_CALL` → `GeneralProgressBar_Create`; xrefs re-run →
`KG3D_CaptionManager::_LoadCaptionConfig 0x180058420`,
`PlaySkillEffectText 0x18059FA10`, `LuaScene_GetCharacterSkillEffectTextPos 0x1800BBF60`,
`UpdateBalloonPosition 0x1805020A0`, `GetHeadTopBufferHeight 0x1804DA270`.

**Game-design check:** Does this follow the client's own truth — no invented fixes or
band-aids? **Yes** — every entry is sourced from the shipped PakV4 assets, shipped native
configs/tables, or the installed binaries; missing files are reported as MISS with a next
probe, never filled in by guesswork.
