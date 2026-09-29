# JX3 绝境战场 — full match lifecycle (queue → accept → load → arrival → loot → combat → endgame)

**Branch:** main
**Date:** 2026-09-24
**Method:** consolidation of the existing static research (`JX3_MODE_LOAD_FLOW.md`,
`JX3_MODE_JUEJING.md`, `JX3_MODE_JUEJING_LOGIC.md`, `JX3_MODE_LOOT_SYSTEM.md`,
`JX3_LOOT_PROTOCOL_LAYOUTS.md`, `JX3_MODE_SPAWN_RULES_SEARCH.md`, `JX3_PROTOCOL_SPEC.md`,
`MAP_MINIMAP_RESEARCH.md`, `JX3_GRAVITY_RESEARCH.md`, `JX3_CAMERA_RESEARCH.md`,
`JX3_MODE_UI_FLOW.md`) plus new session evidence (PakV4 UI Lua corpus inventory,
mode-relevant symbol/xref string sweeps, `c2s_protocol_catalog.tsv`). **No new disassembly
has been run for this document**; the full-pass RE that closes the remaining gaps is
specified in §11. Screen-level detail (queue panel tabs/states, loading window art and
progress, first HUD frame) lives in the companion doc `JX3_MODE_UI_FLOW.md` + evidence
`proof/netcode/mode_ui/**`.

**Evidence tags used below**

| Tag | Meaning |
|---|---|
| `CODE` | recovered from disassembly / binary strings / xrefs |
| `DATA` | shipped client table (`.tab`, bytecode constants, bytecode dump) |
| `UI` | shipped UI Lua / `string.txt` / `.ini` text |
| `DOC` | claim already made in an existing (audited) research doc |
| `UNPROVEN` | hypothesis or non-local claim; **not evidence** |

---

## 1. Scope: the 绝境 map family

`MapList.tab` carries ten 绝境 rows / six unique map resources (`DATA`,
`JX3_MODE_LOAD_FLOW.md` §1, `proof/netcode/mode_juejing/juejing_maplist_rows.txt`):

| ID | Name / DisplayName | Players | Copies | Notes |
|---|---|---|---|---|
| 296 | 龙门寻宝 / 龙门绝境 | 118 | 512 | base loot set (no suffix) |
| 297 | 龙门寻宝_夜晚 / 龙门绝境·夜 | 118 | 512 | night variant |
| 410 | 海岛绝境 / 沧溟绝境 | 118 | 512 | `_sea` loot set |
| 512 | 白龙绝境 | 118 | 512 | `_bl` loot set |
| 532 | 天原绝境 | 118 | 512 | map mechanics (glide/ice/elephant) |
| 645 | 洱海绝境 | 108 | 1024 | grapple mechanic |
| 676 / 677 | 龙门寻宝 / 龙门绝境 | 108 | 512 | alternate copy configs of 296 |
| 709 | 林海绝境 | 118 | 512 | `CanSprint=0`, `BanUseItemMask=32` |
| 715 | 林海绝境 / 林海绝境·纷争 | 118 | 512 | contention variant |

Common flags across the rows (`DATA`): `IsBattlefield=1`, `CampType=5`, `Type=2`,
`MinPlayerCount=1`, `MaxLootRange=5`, `ReviveInSitu=0`, `RevieCycle=160`,
`BanSkillMask=512`, `BanUseItemMask=4` (林海 32), `InvalidBuffMask=8`,
`BanChangeTalent=1`, `QueueForSwitchWhenFull=1`, `OperationMask=7`,
`BanTradeItemMask=4` / `CanTradeMoney=0`, `BanEquipItemMask=2` / `CanExistItemMask=1`,
`BattleRelationMask=2`, `AllScenePlayerInFight=0`, `UseLastEntry=1`, `ScriptFile` empty
(no client map script — mode logic is server-driven, `DOC`). Evidence `pvpfield_and_scriptfile.txt`
confirms 70 non-BR maps set `ScriptFile`; the 绝境 rows do not.

Mode data label: the game data calls the loot system **沙漠风暴** (`DATA`,
`JX3_MODE_LOOT_SYSTEM.md` §0).

**Open item:** the queue UI tab set also lists map **322 沙漠风暴** next to the rows above
(`JX3_MODE_UI_FLOW.md` §1, MED). 322 is not present in the extracted MapList rows — either
an omitted row or a UI-side grouping label; re-check the table extract before relying on it.

---

## 2. Lifecycle at a glance

| # | Stage | Mechanism now known | Status |
|---|---|---|---|
| 0 | Queue | solo/guild/room queue APIs, gateway queue info, queue CD 2856 (3 s), countdown buff | `DOC`/`DATA`, UI found |
| 1 | Match found → accept | `AcceptJoinBattleField`, C→S **0x116**, room/chaos handlers | `DOC`/`CODE` |
| 2 | Transfer + load | `OnSwitchMap` field map, proto **2** ready, proto **3** enter scene, loader/progress | `CODE`, semantics partly MED |
| 3 | Arrival / staging / countdown | server-assigned placement, mode stance set, countdown UI asset, side/camp/relation flags, phase APIs | partly `CODE`/`DATA`, **staging trigger `UNPROVEN`** |
| 4 | Start / drop | glide/parachute symbols + GliderCamera + carrier refs, 天原 滑翔翼 skill, airdrop/storm map markers | `CODE`/`DATA`, **descent law `UNPROVEN`** |
| 5 | Loot & progression | 423 containers / 97 tables / probe layouts / take 0x4D, money 0x51 | `DOC` HIGH, anchors/rates SERVER |
| 6 | Combat replication | 182 mode skills, kits, map mechanics; self/competitor wire layouts not yet recovered | content `DATA` HIGH, layouts **STATIC-pending** |
| 7 | Phases / storm | StormLine assets, battlefield map overlays; **server→circle data source missing** | `DATA` partial, gate open |
| 7.5 | Death / ghost / revive / observer | revive request 0xB9, ghost model symbol, revive skills/items, observer UI (scope unknown) | `CODE`/`DATA`, behavior `UNPROVEN` |
| 8 | Endgame / results / rewards | stat flag, statistics, BF rank request, `STR_BF_*` labels, settlement PSS | handler names `CODE`, flow `UNPROVEN` |
| 8.5 | AFK / disconnect / reconnect | auto-battle report C2S 0x1C9, identity respond co-located with OnSwitchMap | `CODE`, behavior `UNPROVEN` |

---

## 3. Stage detail

### Stage 0 — Queue (town/lobby)

**Client API surface** (`CODE` strings, exe + logic dll; `DOC` `JX3_MODE_LOAD_FLOW.md` §2):

```
JoinBattleFieldQueue / LeaveBattleFieldQueue / AcceptJoinBattleField / LeaveBattleField
JoinTongBattleFieldQueue / LeaveTongBattleFieldQueue / AcceptJoinTongBattleField
DoQueryMapQueueInfo … OnSyncMapQueueInfo       (gateway queue state/ETA)
DoLeaveMapQueue / DoGiveupQueueRequest
BF_QUEUE_COUNTDOWN_BUFF                        (countdown buff while queued)
GetBattleFieldPQInfo                           (queue info for UI)
```

**Queue UI exists in shipped Lua** (`UI`): `NewBattleFieldQueue.decompiled.lua` and
`MapQueue.decompiled.lua` (`proof/minimap/ui/Config/Default/decompiled/`). Recovered UI
elements/strings: `Btn_LeaveSingleQueue` / `Btn_LeaveTeamQueue` / `Btn_LeaveRoomQueue`
(leave button selection by queue type), confirms `CreateRoomConfirm` / `JoinRoomConfirm` /
`StartGameConfirm` / `DisbandRoomConfirm` / `ExitRoomConfirm`, and the `LEAVE_BATTLE_FIELD`
/ `LEAVE_BATTLE_FIELD_QUEUE` events; leave goes through `DoLeaveBattleFieldQueue`
(lines 828–847, 2118–2139, 5604–5882, 6831, 6956). `STR_BATTLEFIELD_DGPAGE` = 战场排队
and `STR_BATTLEFIELD_LEAVE_QUEUE` (`UI` `string.txt:698`).

**Queue cooldown** (`DATA`): cooldown id **2856** = 战场排队内置CD, 3 s
(`JX3_MODE_JUEJING_LOGIC.md` §2).

**Unknowns:** `OnSyncMapQueueInfo` payload; numeric id of `BF_QUEUE_COUNTDOWN_BUFF`;
matchmaking numbers (SERVER); solo→squad auto-forming (SERVER); room queue rules.

### Stage 1 — Match found → accept

- `AcceptJoinBattleField` / `AcceptJoinTongBattleField` are Lua-facing functions
  (`CODE` strings; `LuaAcceptJoinBattleField` too).
- Confirm is the C→S packet built by `KPlayerClient::DoComfirmEnterQueueMap`
  (`CODE` `proof/netcode/disasm/confirm_enter_queue.txt`). The C2S catalog lists **two**
  builder sites: opcode **0x00D9** at `0x1801716e2` and **0x0116** at `0x180171771`
  (`proof/netcode/c2s_protocol_catalog.tsv:157-158`). `DOC` `JX3_MODE_LOAD_FLOW.md` §2
  records proto **0x116**, 19 bytes, `u32 @+0xB`, `u32 @+0xF`. The 0xD9/0x116 pair must be
  disambiguated in the RE pass (two queue paths: solo vs guild are the obvious candidates).
- Rooms / chaos fight: `OnCreateBattlefieldRoomRespond` /
  `OnForceStartBattleFieldChaosFightRespond` (`CODE` strings, size asserts
  `S2C_BATTLE_FIELD_CREATE_ROOM_RESPOND`,
  `S2C_FORCE_START_BATTLE_FIELD_CHAOS_FIGHT_RESPOND`; `exe_mode_keywords.txt`).

**Unknowns:** accept timeout / decline path, `0xD9` vs `0x116` semantics, room payloads.

### Stage 2 — Transfer + load

`KPlayerClient::OnSwitchMap` (logic `0x1801911F0`, `CODE`
`proof/netcode/disasm/onswitchmap.txt:46-196`):

| Packet offset | What the client does | Where |
|---|---|---|
| `+0x07` | map id | → `dwSwitchMapID` + manager/global fields (`0x1801913a5-0x1801913b3`) |
| `+0x0B` | u32 | → global field (`0x1801913b3-0x1801913be`) |
| `+0x0F/+0x13/+0x17` | three u32 | → **client player object** `+0x10/+0x14/+0x18` (`0x1801913c1-0x1801913d3`); these are the character's **world position x/y/z** — proven by `JX3_CHARACTER_MOVEMENT_RESEARCH.md` §3.1 (`MoveTo` writes the same fields; 1 unit = 1 cm) |
| `+0x1B` | scene-related id | → player `+0xAB0` (`0x1801913ee-0x1801913f2`) |

The handler also zeroes player `+0xf88`, `+0xf98`, `+0x2bc`, `+0x2c8` (state reset,
`0x1801913d6-0x1801913e8`) and emits the perf marker **`SwitchMapBegin KPlayerClient::OnSwitchMap`**
(`0x180191478`). `DOC` `JX3_MODE_LOAD_FLOW.md` §3 labels `+0x0B/+0x0F/+0x13/+0x17` as
transfer values (position/heading/copy) at MED confidence — **the field semantics are an
open RE item** (trace the consumers of player `+0x10/+0x14/+0x18`).

Client→server handshake after transfer:

| Message | Proto | Size | Payload | Evidence |
|---|---|---|---|---|
| `DoClientConfirmReady` | **2** | 11 B | header only | `client_ready.txt:37-42` (`0x180170e56` writes proto 2, size 0xB) |
| `DoApplyEnterScene` | **3** | 15 B | `u32 @+0xB` = scene id from `pScene+0x788` | `apply_enter_scene.txt:51-58` |
| `ComfirmSyncMapProgress` / `CancelSyncMapProgress` | — | — | loading-progress handshake | `DOC` §3 |

Loading stack (`CODE` symbols): `KRecorderSceneLoaderNormal` / `Record` / `Replay`,
`LoadScene` / `LoadSceneData` / `LoadSceneSfxClient` / `LoadMapBaseInfo`,
`KJX3LoadingModule` (`SetLoadingProgress`, `LoadingComplete`, `NotifyEndLoading`,
`PostLoadingScene`), `PakV4PrefetchManager_OnPrepareEnterScene` / `_OnEnterScene`
(`scene_load_symbols.txt`).

**New symbol, unresolved:** `MaxSwitchMapMoveDistance` — shipping in both binaries
(exe `0x00805EB8`, logic `0x007AB7D8`; string lists `proof/netcode/mode_juejing/
scene_load_symbols.txt:63,176`) and grouped with the loading module. Its check is one of the
likely rules for "walk-in vs load-screen" transfer and must be disassembled.

**Sub-map streaming** (`DATA`/`DOC`): `settings\SubMapInfo.tab` (66 rows,
`SubMapID/MainMapID/MainPosX/MainPosY/SubRegionWidth/SubRegionHeight`); scene ids such as
`788` are the values passed in proto 3 (`submapinfo_dump.txt`).

**Co-located identity path** (`CODE`): the function immediately before `OnSwitchMap`
(`0x180191150`) logs its own name **`KPlayerClient::OnSwitchIdentityRespond`**
(`onswitchmap.txt:11-12`) — the relogin/identity path that runs next to map switching
(relevant to reconnect, §Stage 8.5).

**Unknowns:** transfer-value semantics; `MaxSwitchMapMoveDistance` rule; scene-id meaning;
progress handshake payloads; `DoSelectSwitchMapWindow` / `LuaSelectSwitchMapWindow`
(what window, when shown).

### Stage 3 — Arrival: where the player is, and what runs before the match

This is the question that started this document. What the client proves:

1. **Placement is server-assigned, as world x/y/z.** The `OnSwitchMap` transfer block
   (`+0x0F/+0x13/+0x17`) is written into the character object's position fields
   `+0x10/+0x14/+0x18`, which the movement research proves are world position x/y/z in
   centimetres (`CODE` Stage 2 table; `JX3_CHARACTER_MOVEMENT_RESEARCH.md` §3.1, `MoveTo`
   `0x140314230`). So on entry the client places the player at the server-sent x/y/z — the
   "before the match starts" position is exactly this packet. There is **no client
   spawn/anchor table** for the BR maps: `AnchorPointList.tab`, `CustomObject.tab`,
   `MapReviveList.tab`, `DoodadReviveList.tab`, `.land/.pland` were probed in every local
   store — 0 hits (`DOC` `JX3_MODE_SPAWN_RULES_SEARCH.md` §0-§3, CLOSED dead end), and none
   of the 77 `MapList` columns carries a position. The **values** per map/copy are
   SERVER-only; get them by capturing `OnSwitchMap` on entry (capture decoder route,
   §8.3/§11) or from server map logic.
2. **A mode-specific idle/pose set ships for this mode** (`DATA`): `Ani.rt` contains
   `F1/F2/M1/M2` × `b02ty龙门绝境_站姿01..05` (20 files,
   `proof/netcode/mode_juejing/editor_assets_juejing.txt:2-21`). A mode-only stance set is
   engine/data evidence that the mode displays characters in a dedicated state (waiting /
   staging pose), but **what state selects it is not yet proven** (`UNPROVEN` trigger).
3. **A countdown UI asset ships** (`DATA`): `UI_黑山绝境_倒计时通用底框.pss`
   (`editor_assets_juejing.txt:72-77`; `JX3_MODE_JUEJING.md` §3.3). The settlement
   counterpart `UI_黑山结算_折戟沉沙.pss` exists too (`gbk_juejing_editor.txt:41`).
3b. **Pre-match countdown is officially rendered from a server-written end time
   (duration STATIC-pending).** The official UI panel computes its clock from an engine
   getter: `BattleFieldMap.UpdateTime` calls `GetBattleFieldPQInfo()`, keeps its **4th
   return value as an absolute end time**, and prints `endTime - GetCurrentTime()` as
   h:m:s (`UI` `BattleFieldMap.decompiled.lua:1322-1325,1581-1625`; asset
   `proof/minimap/ui/Config/Default/BattleField/BattleFieldMap.lua` exists as shipped
   bytecode). The engine getter `KScriptFuncList::LuaGetBattleFieldPQInfo` returns four
   ints read straight from the client cache struct at
   `+0x1b480/+0x1b484/+0x1b488/+0x1b490` (`CODE`,
   `proof/netcode/disasm/LuaGetBattleFieldPQInfo.txt`). **How long the wait is and what
   starts it are server values — no official local file contains the duration.**
3c. **When the countdown reaches zero there is no client-side "battle start" event.**
   The official timer just stops updating (`BattleFieldMap.UpdateTime` returns when
   `endTime - now < 0`, `BattleFieldMap.decompiled.lua:1592-1593`). Match start is a server
   state change; what the client then reflects is the sync/overlay machinery: competitor
   list pull `0x164` + CD/buff/stat subscriptions (`DOC` `JX3_MODE_LOAD_FLOW.md` §8), the
   16-int objective cache (`+0x1b498..`), the stat-recording flag (`OnSyncBattleStatFlag`,
   Stage 8), and the storm overlay events (Stage 7). The only events the official mode
   skill-bar module registers are `UI_SCALED`, `HOT_KEY_RELOADED`, `LOADING_BEGIN`
   (`DynamicBattleRoyale.decompiled.lua:432-445`) — no start hook.
4. **Phase / objective APIs** (`CODE`): the panel clock is proven to come from
   `GetBattleFieldPQInfo` (see 3b). The phase/storm data is the **16-int objective array**
   from `LuaGetBattleFieldObjective` (`proof/netcode/disasm/LuaGetBattleFieldObjective.txt`),
   still unlabelled. Other candidate phase values: `KQuestList::GetQuestPhase` (exe
   `0x0082F608`; used by `MiddleMap.decompiled.lua:24127,24356,25026`), `OnSyncBattleStatFlag`
   (Stage 8).
5. **Side/camp/relation plumbing** (`CODE` strings + `DATA` flags): `OnSetBattleFieldSide`,
   `SetBattleFieldSide`, `BattleFieldSide`, `DoUpdateBattlefieldSide`, nameplate field
   `nBattleFieldSide`; MapList `CampType=5`, `BattleRelationMask=2`,
   `AllScenePlayerInFight=0`. What each mask changes (PvP/relation during staging, team
   grouping) is `UNPROVEN`.
6. **A mode buff is granted through shipped Lua** (`DATA`):
   `ReFreshTreasureBattleFieldMapBuff` calls `AddBuff(dwID=26524, nLevel=1800, custom)`
   (`CheckTreasureBattleFieldMap.dump.txt:14-16`) — the only mode buff id recovered so far.
   `CheckTreasureBattleFieldMap` also exposes `TreasureHunt_IsMap`,
   `IsTreasureBattleFieldMap`, `GetTreasureBattleFieldMap`, `GeTrafficOffSet`,
   `GetDesertHorseList`, `Tool_GetChickenMapItem` (same dump, line 3).
7. Competitor cache/sync starts once in scene (`DOC` `JX3_MODE_LOAD_FLOW.md` §4/§8):
   `KBattlefieldCache`, `m_BattlefieldCompetitorInfoMap`, pull `0x164`, CD subscriptions.

**Open (STATIC-pending):** what puts the player in the staging pose; whether the countdown
is a quest phase, objective packet, buff, or UI countdown; staging safety mechanism;
where the staging area is on each map (SERVER).

### Stage 4 — Start / drop-in

Client-side machinery that exists; the BR-specific wiring is `UNPROVEN`:

- **Parachute flag**: `bOnParachuteFlag` (exe `0x0084B508`) and `ON_PARACHUTE_FLAG`
  (exe `0x007D52B8`); per the gravity audit `bOnParachuteFlag` has **no code xref in the
  client exe** → set/read on the Represent/script side (`DOC` `JX3_GRAVITY_RESEARCH.md`
  §3.11; `REBORN_JUMP_FALL_SPEC.md` item 21).
- **Glider camera**: `GliderCamera`, `m_pGliderCamera`, `KTableList::GetGliderCameraTable`,
  `tabGliderCamera`, `SmoothToGliderCamera`, `UpdateGliderCameraYaw`,
  `pGliderCameraController`, `ClampMouseForGliderCamera`,
  `AUKRLGliderCameraControllerComponent` (`CODE` strings
  `JX3RepresentX64_camera_strings.txt:330-538`) and the config
  `GliderCamera=represent/camera/GliderCamera.krl.txt` (`proof/gravity/filepath.ini:245`).
  The glider controller is referenced from the **skill-move / carrier / sprint** code
  paths (`skillmove_xrefs.txt:3906,3911`; `carrier_smooth.txt:1289,1605,2353`;
  `sprint_update.txt:108,207`) — i.e. a glider camera is driven by a move/carrier state,
  not by a standalone BR handler.
- **Carrier system**: carrier-smooth disassembly already exists (`carrier_smooth.txt`);
  what entity the mode uses as the entry carrier (if any) is `UNPROVEN`.
- **Mode skill**: `天原绝境_滑翔翼` SkillID **29021** (`DATA` `mode_skills.txt:2`), script
  `沙漠风暴/天原绝境_滑翔翼.lua` (not shipped), plus 天原 滑索 doodads 9229/9230
  (`mode_doodad_inventory.txt:165-166`).
- **Mode `SkillMove` flags** (`CODE` strings): `SkillMoveOnlyFly`, `IngoreGravity`,
  `SkillMoveEndButKeepVelocity`, `SkillMoveDeath` — the engine vocabulary a descent skill
  would use (`exe_skillmove_strings.txt`).
- **Airborne/ground markers in UI** (`DOC`): minimap dynamic markers include loot, airdrops
  and storm (`MAP_MINIMAP_RESEARCH.md:67,400`); storm line art `Image/MiddleMap/StormLine/*.DDS`
  (41 files, `MAP_MINIMAP_RESEARCH.md:222`).
- **Starting kit** (`DATA`): `CheckTreasureBattleFieldMap`'s item-grant routine uses
  `GetItem` / `INVENTORY_INDEX` / `EQUIP` / `dwMapBanTradeItemMask` / `bBind` and box slots
  (`LIMITED_PACKAGE`, `GLOBAL`) (`CheckTreasureBattleFieldMap.dump.txt:17-19`); the mode's
  starter attack kits are skills 64889–64901 + 64903/64986/64989 (`DATA`
  `JX3_MODE_JUEJING_LOGIC.md` §1).
- **UI string lead**: `STR_BATTLEFIELD_MAPTIP2` = 点击出战车 (`UI` `string.txt:960`) —
  a battlefield-map tip for a war-chariot interaction; must be checked against the mode
  before claiming (possibly 天原 战象/战车) (`UNPROVEN`).

**Open (STATIC-pending):** what actually starts the descent; per-map routes/route choice;
whether `29021` is entry or in-match traversal; landing/fall-damage handling; airdrop
entities and their handlers.

### Stage 5 — Loot & progression (condensed; full detail in loot docs)

`DOC` `JX3_MODE_LOOT_SYSTEM.md` / `JX3_LOOT_PROTOCOL_LAYOUTS.md`:

- 423 container rows across `沙漠风暴` (255) + `沙漠风暴_寻宝模式` (168), 97 named drop
  tables; tiers 一/二/三/天 × 装备/武器, consumables, specials (匿踪宝盒, 觅踪窥影烟,
  流萤魂返丹/花, 驼铃, 飞艇, 楼兰/沧溟/白龙/天原神兵匣, 马匪的货物, 叹息风碑, 钥匙宝箱,
  吃鸡铁血宝箱, 技能拾取), gatherables (砂石/灌木/瓦罐/枯树), decoys
  (未知的宝箱 / 已知类型的宝箱 / 伪传的秘密), player-death bags (丢弃的*).
- Per-map drop sets (`DATA` `doodad_mapnames.txt:43`): `_sea` 18 refs (海岛/沧溟),
  `_bl` 15 (白龙), `_lw` 7 (林海); 林海 additionally has the skill-set line
  (`equipment*_skill`, `jewelry1..4_skill`, `equipment4_lw`, `weaponZ4`) and 天原's
  神兵匣/物资 rows reuse `_bl`/`ty_*` tables (`mode_doodad_inventory.txt`).
- Wire chain (`CODE` layouts): `OnSyncNewDoodad` → `OnSyncDoodadState` →
  `OnSyncLootList` (rolled contents) → take `DoApplyLootList` **0x4D** / money
  `DoLootMoney` **0x51** → inventory (`OnAddItemNotify`/`OnSyncItemData`).
  Interaction wind-up is `OpenPrepareFrame` (`DATA` `DoodadTemplate.tab`).
- **Open:** which handler spawns death bags (templates exist; earlier note says
  `OnSyncDropItem` is the coin-shop handler — real channel unproven); container respawn
  (`ReviveDelay`); disguise/identification behavior; anchors/rates (SERVER, dead end).

### Stage 6 — Combat replication

- **Content** (`DATA` `JX3_MODE_JUEJING_LOGIC.md`): 182 mode skills; kungfu roots 64888
  (绝境战场技能) / 64902 (绝境战场子技能) / 10516 (沙漠风暴); starter weapon kits
  64889–64901; qinggong set 37681–37686 + mobile 38381/38382; mode abilities
  (回风扫叶 37687, 渊 37717, 雾暗迷云 39483, 截阳 39492, 引窍 39493, 风流云散 39494,
  雷霆震怒 65156 …); class-adapted skills (春泥护花_绝境战场 33285, 林海 进战/脱战
  41291/41292); gear passives (18878–18882); mode cooldowns (`CoolDownList.tab`:
  2856 queue, 2875, 2897, 2898, 2989, 2990 …).
- **HUD competitor model** (`DOC`): `KBattlefieldCache` + per-competitor base/variable
  info, skill-CD state, buff list, statistics, positions (`GetBattlefieldPlayersPosInfo`),
  rank (`GetBFRank*`), objectives (`GetBattleFieldObjective`), income penalty.
- **Wire layouts still missing** (STATIC-pending, gap register §6): self
  `OnSkillPrepare/Cast/EffectResult`, `OnSyncMoveState/MoveCtrl/MoveParam`,
  `OnSyncBuffList`, cooldown sync; competitor base/variable/CD/buff/stat record layouts.
- **Open:** server validation and damage numbers (SERVER).

### Stage 7 — Phases, storm, map mechanics

- **Rendering side proves a storm exists** (`DOC`/`DATA`): storm line segments
  `Image/MiddleMap/StormLine/*.DDS` (41), battlefield map window
  `UI/Config/Default/BattleField/BattleFieldMap.ini` + `.lua`, minimap storm/airdrop
  `KMapMark` types, `MiddleMap`/`WorldMap` drivers (`MAP_MINIMAP_RESEARCH.md` §UI inventory).
- **HUD labels for the phase exist (official UI strings).**
  `proof/minimap/ui/Scheme/Case/string.txt` (GBK; PakV4 `ui/Scheme/Case/string.txt`):
  `STR_TIMEDESERT` = 风暴倒计时： (line 2425), `STR_LEFTPEPLE` = 剩余人数： (2426),
  `STR_STORM`/`STR_STORM2` = 风暴/风暴状态 (2427/2429), `STR_SETTING210` =
  小地图增加风暴圈范围提示 (2251). No shipped Lua in the extracted corpus references these
  ids (the renderer is a not-yet-extracted mode panel) — PakV4 `ui/` enumeration item.
- **Official phase/economy data model** (`CODE`): `LuaGetBattleFieldObjective` returns a
  **16-int array** read from the client cache (`+0x1b498 .. +0x1b4d8`, 4-byte stride;
  `proof/netcode/disasm/LuaGetBattleFieldObjective.txt`); `LuaGetBattleFieldStatistics`
  walks a statistics list exposing `Name`, `ForceID`, `BattleFieldSide`,
  `ClientVersionType`, `GlobalID` and **25 qword stat values** per record
  (`proof/netcode/disasm/LuaGetBattleFieldStatistics.txt`). The objective array is the
  prime candidate for storm/phase state, but its **field semantics are not yet labelled** —
  that is the remaining static work for this chapter.
- **Storm overlay is official UI and map-gated** (`UI`): `BattleFieldMap` registers
  `ON_BATTLE_FIELD_MAKR_DATA_NOTIFY`, `ON_BATTLE_FIELD_GAIN_DATA_NOTIFY`,
  `ON_BATTLE_FIELD_SFX_DATA_NOTIFY` and `LOADING_END`
  (`BattleFieldMap.decompiled.lua:9362-9405`), and when `IsInTreasureBattleFieldMap()` is
  true it shows `Handle_Map/Handle_StormLine` and registers `MapCircle` + `SFX_CircleNew`
  (`:5303, 5316-5341`). So storm lines/circles are drawn on the battlefield map panel from
  server notify events; the **notify payload layouts / engine producers are the missing
  piece** (candidates: the `OnSync...` families, not yet decoded).
- **Safe-zone shrink schedule is NOT in any official local file** (re-verified 2026-09-24):
  `Buff.tab` / `TopBuff.tab` / `Activity.tab` / `CoolDownList.tab` contain **0** entries
  matching 风暴/缩圈/沙暴/安全区/风沙/全毒 (GBK scan); `MapList` carries no schedule field
  (`RefreshCycle=0` on all 绝境 rows); the five ids referenced by
  `CheckTreasureBattleFieldMap.lua` (22495–22497, 22622–22623) resolve to **skills**
  (kungfu 10597/10572/10592), not zone config; mode scripts/logical maps are absent from
  the client pak (748-path probe = 0). The client only *receives* the schedule: objective
  arrays (`+0x1b498/+0x1b4b8`) and PQ end time (`+0x1b490`) written by the S2C handlers
  below, ring art drawn from `StormLine`/`MapCircle`/`SFX_CircleNew`, and outside-zone
  warnings `STR_SAFEZONE` (安全区, string.txt:2428) + `STR_SETTING262`
  (小地图安全区外警告闪烁及距离提示, :2796). **Timing values are capture/server-only.**
- **The objective/phase cache writer is recovered** (`CODE`, 2026-09-24). The cache struct
  (`0x1809C1320` base) is filled by two S2C paths:
  - **incremental objective update** at `0x180193FD0`: packet = `u8 index` (<8) + two `u32`
    values → stored to `+0x1b498 + index*4` and `+0x1b4b8 + index*4`, then a UI vtable
    notify (`+0x918`);
  - **PQ block writer** at `0x180194020`: sets `+0x1b480/+0x1b484` (u32), `+0x1b488`
    (qword), `+0x1b490` (qword = the absolute end time the UI clock reads) and fires a UI
    notify.
  Neighbouring handlers in the same code block: `OnSyncBaseInfoFromBattlefieldCompetitorList`
  (`0x180193BA8`), `OnSyncVariableInfoFromBattlefieldCompetitorList` (`0x1801A9468`),
  `OnSyncBattlefieldCompetitorCDState` (`0x180194488`); the full-sync path at `0x180193E30`
  (size assert **0x7C** = `S2C_SYNC_BASE_INFO_FROM_DUNGEON_PLAYER_LIST`) also writes the
  shared cache. Proof: `proof/netcode/disasm/bf_cache_writers.txt`,
  `proof/netcode/disasm/bf_runtime_writers.txt`.
- **Protocol IDs + payloads recovered (CODE, 2026-09-24).** The KPlayerClient S2C dispatch
  table was parsed from its registration function (`0x180163540`–`0x180168280`; handler base
  `this+0x16460`, size base `this+0x17F28`; calibration: arena competitor CD state `0x22A`).
  Mode-relevant packets:
  - **`0x11A` `OnSyncBFObjectiveIncrement`, 16 B** — `u8 index (<8)`, `u32 A`, `u32 B` →
    `cache[+0x1b498+idx*4]=A`, `cache[+0x1b4b8+idx*4]=B`, then UI notify. Two 8-slot arrays.
  - **`0x11B` `OnSyncBFPQInfo`, 23 B** — `u32 A → +0x1b480`, `u32 B → +0x1b484`,
    `s32 startOffset → +0x1b488 = (clientNow + serverOffset) − startOffset`,
    `s32 duration → +0x1b490 = base + duration` (`-1` ⇒ 0 = no timer). **This is the official
    phase/countdown clock**: the server sends offsets/durations; the client converts them to
    the absolute end time that `GetBattleFieldPQInfo` returns and the UI counts down.
  - **`0x119` `OnSyncBattlefieldStatistics` (264 B fixed)** — builds the statistics records
    (id/force/side/name/global-id/version + 25 qword stats).
  - **Remaining gap:** the index→meaning mapping for the two 8-slot objective arrays (which
    slot is phase / storm countdown / center / radius). Values are server-sent; capture one
    match with opcodes `0x11A`/`0x11B` and the labels fall out.
- **Poison damage strings** `MinPoisonDamage` / `MaxPoisonDamage` (exe `0x007F6C00`/`0x007F6C10`)
  sit next to `LuaGetSkillInfo` in the string dump (`poison_revive_strings.txt:2`) — likely
  skill/tooltip stat names rather than the BR zone; xref pending (`UNPROVEN`).
- **Server-only:** phase durations, shrink schedule, damage curve, final full-screen storm,
  win condition.

### Stage 7.5 — Death / ghost / observer / revive (summary; detail in `JX3_MODE_EDGE_SYSTEMS.md`)

`CODE`/`DATA`: `DoPlayerReviveRequest` C2S **0xB9** (size 0xF, builder `0x180176abc`;
`c2s_protocol_catalog.tsv:282`); `DynamicReviveMinTime` (exe `0x00802D08`) and
editor-side revive validation strings (`poison_revive_strings.txt:30-32`);
`ApplyGenModelGhost` (Represent `0x00CA1A30`); 灵魂出窍02 animation adjacent to the BR
stances (`gbk_juejing_editor.txt:1`); revive skill 天原绝境_冰封复活 31507; revive item
流萤魂返花 (container 6973); cooldown 2375 (吃鸡天原绝境破冰). Whether dead players
spectate at all is `UNPROVEN` (observer UI exists but may be non-BR — see edge doc).

### Stage 8 — Endgame / results / rewards

- Handlers/symbols (`CODE`): `OnSyncBattleStatFlag`, `ApplyBattleFieldStatistics`,
  `GetBattleFieldStatistics`/`LuaGetBattleFieldStatistics`, `OnSyncBattlefieldStatistics`,
  `KPlayerBattleStat::AddData` (`0x008134F0`) / `ReportBattleStat` (`0x00813510`),
  `DoGetBFRankRequest` / `OnGetBFRankRespond`, `GetBFRank*`, `ClearBFRank`,
  `MultipleRankPoint{Begin,End}Time`, `BattleFieldIncomePunish`.
- Scoreboard labels exist (`UI` `string.txt:285-291,321-326`): `STR_BF_NAME` 名字,
  `STR_BF_KILL1` 协助击伤, `STR_BF_KILL2` 击伤, `STR_BF_DEAD` 受重伤, `STR_BF_DAMAGE` 伤害量,
  `STR_BF_HEALTH` 治疗量, `STR_BF_INJURED` 受伤量, `STR_BF_PRESTAGE` 威望,
  `STR_BF_REWARD/MONEY/EXP` 奖励/奖励金钱/奖励经验.
- **Statistics record layout recovered** (`CODE`): the handler at `0x1801940A0` builds
  0x128-byte records inserted into the list at cache `+0x1b470`: `+0x20` id, `+0x100`
  force id, `+0x104` battle-field side, `+0x30` qword global id, `+0x28` client-version
  byte, `+0x108` name (`strncpy` 0x20), `+0x38..` 25–26 qword stat values — exactly what
  `LuaGetBattleFieldStatistics` and `PVPShowPanel` read
  (`proof/netcode/disasm/bf_runtime_writers.txt`).
- **Statistics panel is official and recovered** (`UI`): `PVPShowPanel.lua` consumes
  `GetBattleFieldStatistics` with `PQ_STATISTICS_INDEX` slices `INJURY` / `HARM_OUTPUT` /
  `TREAT_OUTPUT` and the `BATTLE_FIELD_SYNC_STATISTICS` event
  (`proof/netcode/ui/PVPShowPanel.dump.txt:40,70-72`). `ReportBattleStat` posts telemetry
  (`eventId=battle_stat`, `JX3ClientX64_exe_net_strings.txt`); its lifecycle (when stat
  recording starts) is carried by `OnSyncBattleStatFlag` (Stage 3c).
- **Settlement panel located and partly decoded (official UI, 2026-09-24):**
  `ui/Config/Default/PVPShowFinal.lua` + `PVPShowFinalL/R.ini`, extracted to
  `proof/netcode/ui/pak_candidates/` (dump `proof/netcode/ui/PVPShowFinal.dump.txt`).
  It registers `BATTLE_FIELD_SYNC_STATISTICS` → `OnSyncPQ` → `GetBattleFieldStatistics`
  with `PQ_STATISTICS_INDEX` slices `DECAPITATE_COUNT` / `HARM_OUTPUT` / `TREAT_OUTPUT` /
  `SPECIAL_OP_7`, then `MergeData` / `UpdateList` / `UpdateOneSideList`; opens with
  `ApplyBattleFieldStatistics`; per-player rows use `Text_Name` / `Text_Kill` /
  `Text_Damage` / `Text_Health` / `Text_NearDeathNum` and the labels
  `STR_FBLIST_DUIYUANMINGZI`, `STR_BF_DAMAGE/HEALTH/KILL2`, `STR_NEAR_DEATH`
  (`string_PVPAcount.txt`, GB18030; UTF-8 copy in `proof/netcode/ui/`). The mode-specific
  settlement art remains `UI_黑山结算_折戟沉沙.pss`.
- Leave flow UI is proven (`UI` `NewBattleFieldQueue.decompiled.lua`: `LEAVE_BATTLE_FIELD`,
  `LEAVE_BATTLE_FIELD_QUEUE`); `LeaveBattleField` / `LuaLeaveBattleField` (`CODE` strings).
- 吃鸡 markers (`DATA`/`CODE`): `Tool_GetChickenMapItem` (CheckTreasure bytecode),
  吃鸡铁血宝箱 doodads + scripts, 吃鸡专用霸刀 (41293), cooldown 2375.
- **Open:** win condition, placement/kill credit, reward formulas (SERVER).

### Stage 8.5 — AFK / disconnect / reconnect

- **AFK report** (`CODE`): `KPlayerClient::DoReportAutoBattle` = C2S **0x1C9**
  (`c2s_protocol_catalog.tsv:299`, builder `0x18017777f`), `LuaReportAutoBattle`,
  `ReportAutoBattle` (`0x007F5760`). Thresholds/punishment unknown (`UNPROVEN`).
- **Identity/reconnect** (`CODE`): `OnSwitchIdentityRespond` co-located with `OnSwitchMap`
  (Stage 2); `SyncTempData` MapList flag; resume model documented in
  `REBORN_SERVER_SPEC.md` §4 (our own protocol, not JX3 evidence).
- **Open:** leave penalties, reconnect window handling in a BR copy, state restoration.

---

## 4. Per-map chapters

### 4.1 龙门绝境 (296 / 297 夜 / 676 / 677 copies)

- Flags: 118 players, 512 copies (676/677: 108); night variant 297; `UseLastEntry=1`.
- Loot: base `沙漠风暴` tables (no suffix); containers 6816–6995 base set
  (`mode_doodad_inventory.txt:1-50`); horse/luxingniao/wugui rows appear with `_sea` for
  海岛, so 龙门's own horse tables are `horse1..3` (base).
- Mode stance set is named after this map (`龙门绝境_站姿01..05`) — the only map with a
  stance set in `Ani.rt`.
- Map→tier grouping: `CheckTreasureBattleFieldMap.dump.txt:4` clusters `{296,297}` as the
  first group; semantics `UNPROVEN`.
- Map render/minimap: 龙门寻宝 minimap art extracted (`MAP_MINIMAP_RESEARCH.md`);
  jsonmap descriptor cached (`longmen_jsonmap.txt`).
- Unknowns: per-map staging/drop coordinates; drop-table rows.

### 4.2 沧溟绝境 (海岛绝境, 410)

- Flags: 118 players, 512 copies; `_sea` loot set (18 table refs).
- Containers: 7720–7783 `_sea` block (`equipment*_sea`, `mabubengdai_sea`,
  `hide_sea`, 石马/石雕/石龟, `luxingniao_sea`, `wugui_sea`, 沧溟神兵匣 7820,
  劫掠的货物 7769) (`mode_doodad_inventory.txt:58-99`).
- Renders in the map spike (bundle + tour), `JX3_MODE_JUEJING.md` §2.
- Unknowns: same server-side items.

### 4.3 白龙绝境 (512)

- Flags: 118 players, 512 copies; `_bl` loot set (15 refs).
- Containers: 8645–8651 (`equipment1..3_bl`, `chengzhuang_bl`, 白龙神兵匣 8649,
  劫掠的货物 8650, 匿踪宝盒 `hide_bl`) (`mode_doodad_inventory.txt:101-107`).
- Renders in the map spike.
- Note: 天原's 天原神兵匣 9139 also uses `_bl` tables and the 9175/9176 物资 rows reuse
  `_bl` equipment3.
- Unknowns: route/landing specifics (`UNPROVEN`).

### 4.4 天原绝境 (532)

- Flags: 118 players, 512 copies.
- Map mechanics (skills/cooldowns, `DATA` `JX3_MODE_JUEJING_LOGIC.md` §1-2): 滑翔翼 29021,
  照明火把 29043/29044 (CD 2173 90 s), 燧石 29051, 冰雹aoe 29055, 剑宗深寒 29056,
  武家炽血 29061, 橙色下装被击降攻击 29068, 极寒掉血 29342, 战象践踏 29146 / 冲锋 29264
  (CD 2174 12 s) / 下车 29267 / 践踏母 29283 / 推人母 29476, 天玄冰 29637 (CD 2192 10 s),
  冰封复活 31507, 破冰 CD 2375 60 s.
- Doodads: 神兵匣 9139, 绳池剑宗物资 9175 / 武家物资 9176 (Prepare=48), 滑索 9229/9230.
- Renders in the map spike.
- Unknowns: whether 滑翔翼 is the entry descent or an in-map traversal tool
  (the 滑索 doodads suggest in-map traversal) — STATIC-pending.

### 4.5 洱海绝境 (645)

- Flags: **108 players**, **1024 copies** — the outlier copy config.
- Mechanics: 飞爪 cooldown 2617 (60 s ×3) + protection 2630 (1 s); 脱战附近aoe 35149.
- Unknowns: which loot suffix set it uses (no `_rh`/洱海 suffix observed — likely base
  tables); revive/geo table absent (server).

### 4.6 林海绝境 (709) / 林海绝境·纷争 (715)

- Flags: `CanSprint=0` (no sprint), `BanUseItemMask=32`, `DoNotGoThroughRoof=1`,
  `MiniMapResourcePath` set; 118 players.
- Skills: 林海绝境进战/脱战 41291/41292; 林海 variants of qinggong 41281-41284.
- Loot: `_lw` set + the skill-jewellery line (`equipment3_lw`, `equipment4_lw`,
  `jewelry1..4_skill`, `weaponZ4`, `chengzhuang_skill`,
  `mode_doodad_inventory.txt:238-244,343-348`); 楼兰神兵匣 at 10726/10727 mixes
  `equipment3_lw`/`equipment4_lw`/`jewelry4_skill`.
- Props/effects: `WJ_lhjj*` 林海 assets incl. 毒蘑菇/血蛊巢穴/恐龙
  (`JX3_MODE_JUEJING.md` §3.2).
- Unknowns: what 纷争 (715) changes vs 709 (predictive constant `5.0` group in the
  CheckTreasure dump; semantics `UNPROVEN`).

---

## 5. Progression & economy (mode-wide)

- **Tier ladder**: 一/二/三阶 + 天阶 (equipment/weapons), jewellery tiers for the
  skill-set maps; weapon pickup grants the matching starter attack kit (64889–64901);
  gear passives 18878–18882 (`DATA`).
- **Consumables/specials**: 金疮药, 止血草, 麻布绷带, 鬼黄藤, 千里镜, 行气散, 定安散,
  回蓝道具, 腊八粥; 匿踪宝盒, 觅踪窥影烟, 流萤魂返花, 驼铃, 飞艇, 神兵匣, 马匪货物,
  叹息风碑, 钥匙宝箱, 铁血宝箱, 技能拾取 (`DOC`).
- **Economy restrictions**: `BanTradeItemMask=4`, `CanTradeMoney=0`,
  `BanEquipItemMask=2`, `CanExistItemMask=1`, `BattleFieldIncomePunish` (`DATA`+`CODE`).
- **Death bags**: dynamic 丢弃的* containers (templates `DATA`; spawn channel `UNPROVEN`).

---

## 6. Protocol appendix

Base frame (our own spec, from JX3 evidence): 11-byte header
`u16 id; u8 flags; u16 send_seq; u16 ack_seq; u32 field7`, +`u32 param @+0xB` when used,
max payload 0x8000 (`DOC` `JX3_PROTOCOL_SPEC.md`, `JX3_NETCODE_RESEARCH.md` item 20).

| ID | Dir | Name / role | Payload | Evidence |
|---|---|---|---|---|
| 2 | C→S | `DoClientConfirmReady` | none (11 B) | `client_ready.txt` |
| 3 | C→S | `DoApplyEnterScene` | u32 scene id @+0xB (15 B) | `apply_enter_scene.txt` |
| 4 | C→S | role-data section check/ack | header only | `JX3_MODE_LOAD_FLOW.md` §8 |
| 6 | both | ping | — | `JX3_PROTOCOL_SPEC.md` |
| 0x4D | C→S | `DoApplyLootList` | u32 doodad/entity id @+0xB | `JX3_LOOT_PROTOCOL_LAYOUTS.md` §4 |
| 0x51 | C→S | `DoLootMoney` | u32 @+0xB | §5 |
| 0xB9 | C→S | `DoPlayerReviveRequest` | size 0xF (payload pending disasm) | `c2s_protocol_catalog.tsv:282` |
| 0xD9 | C→S | `DoComfirmEnterQueueMap` (2nd builder) | 19 B? | `c2s_protocol_catalog.tsv:157` |
| 0x116 | C→S | `DoComfirmEnterQueueMap` | u32 @+0xB, u32 @+0xF (19 B) | `confirm_enter_queue.txt`, `DOC` §2 |
| 0x164 | C→S | `DoSyncBattlefieldCompetitorsListRequest` | 11 B | `JX3_MODE_LOAD_FLOW.md` §8 |
| 0x1C9 | C→S | `DoReportAutoBattle` | pending | `c2s_protocol_catalog.tsv:299` |
| 0x6E | C→S | routine sync | size-prefixed | `JX3_PROTOCOL_SPEC.md` |

### S→C protocol IDs (recovered 2026-09-24)

Method: parse the KPlayerClient protocol registration function (logic DLL
`0x180163540`–`0x180168280`). For each entry the function does
`lea rax, <handler>` / `mov [this+0x16460+id*8], rax` / `mov dword [this+0x17F28+id*4], size`
(`-1` = variable length). IDs were calibrated against the arena competitor CD assert
(`s2c_sync_arena_competitior_cd_state` = `0x22A`, emitted as an immediate in the handler).
Tool: `tools/netcode/parse_protocol_registration.py`; full table:
`proof/netcode/protocol_table_s2c_annotated.tsv` (810 handlers).

| ID | Handler (registered VA) | Size | Role |
|---|---|---|---|
| `0x08` | `OnSwitchMap` `0x1801911F0` | 31 | map id + 5 params (x/y/z placement, scene) |
| `0x0C` | `OnSyncNewDoodad` `0x18019D510` | 68 | container/object spawn |
| `0x10` | `OnSyncDoodadState` `0x1801975F0` | 14 | container state |
| `0x19` | `OnSyncMoveState` `0x18019CF90` | 52 | movement state |
| `0x82` | `OnSyncLootList` `0x18019BA60` | var | rolled loot contents |
| `0xAA` | `OnSetBattleFieldSide` `0x18018DC50` | 15 | side/camp assignment |
| `0x119` | `OnSyncBattlefieldStatistics` `0x1801940A0` | 264 | statistics record |
| `0x11A` | `OnSyncBFObjectiveIncrement` `0x180193FD0` | 16 | objective array update |
| `0x11B` | `OnSyncBFPQInfo` `0x180194020` | 23 | phase clock (start/end offsets) |
| `0x22A` | `OnSyncArenaCompetitorCDState` `0x180192EE0` | 27 | calibration anchor |
| `0x245` | `OnSyncBFRoleData` `0x180193AC0` | 112 | BR role data |
| `0x25B` | `OnGetBFRankRespond` `0x180187800` | var | rank response |
| `0x25F` | `OnSyncBaseInfoFromBattlefieldCompetitorList` `0x180193BB0` | var | competitor base |
| `0x260` | `OnSyncVariableInfoFromBattlefieldCompetitorList` `0x1801A9470` | var | competitor vars |
| `0x261` | `OnSyncBattlefieldCompetitorCDState` `0x180194490` | 27 | competitor cooldowns |
| `0x262` | `OnSyncBattlefieldCompetitorBuffList` `0x180194390` | var | competitor buffs |
| `0x323` | `OnCreateBattlefieldRoomRespond` `0x180184480` | 27 | room create |
| `0x324` | `OnForceStartBattleFieldChaosFightRespond` `0x180186C90` | 19 | force start |
| `0x330` | `OnSyncBattleStatFlag` `0x180194310` | 15 | stat flag |
| `0x34C` | `OnSyncBaseInfoFromDungeonPlayerList` `0x180193E30` | 124 | shared full-sync path |

Recovered S→C layouts (offsets HIGH, labels MED) live in `JX3_MODE_LOAD_FLOW.md` §3/§8
(`OnSwitchMap`, `OnSyncBFRoleData`) and `JX3_LOOT_PROTOCOL_LAYOUTS.md` §1–3
(`OnSyncNewDoodad`, `OnSyncDoodadState`, `OnSyncLootList`). Handler symbol groups per stage:
`proof/netcode/mode_juejing/battlefield_api_symbols.txt`,
`mode_load_symbols.txt`, `scene_load_symbols.txt`.

---

## 7. UI corpus relevant to the mode (new finding, 2026-09-24)

- **Stage-by-stage UI inventory:** `JX3_MODE_UI_INVENTORY.md` (all windows/labels per
  stage, evidence, and the missing-renderer hunt list, incl. the storm HUD panel).
- **Screen-level companion:** `JX3_MODE_UI_FLOW.md` (merged to main, commit `f82f15a`)
  covers the queue panel tabs/states, custom-room lobby, the X3DEngine loading window with
  per-map loading art (`proof/netcode/mode_ui/loading/*`, maps 296/297/410/512/532/645/709),
  progress handshake xrefs (`proof/netcode/mode_ui/xref/*`), and the first HUD frame. This
  document references it instead of duplicating those details.
- **Evidence rule for this corpus:** only the PakV4-shipped `ui/...` entries are used as
  evidence. The JX addon builds (`proof/minimap/addons/JX/**`, sourced from the `interface\`
  addon tree) are listed for inventory context only and are **excluded as evidence**.
- 144 base-UI Lua bytecode files were extracted from PakV4 into
  `proof/minimap/ui/Config/Default/` as part of the minimap research
  (`DOC` `MAP_MINIMAP_RESEARCH.md` §203-259), plus JX addon packs under
  `proof/minimap/addons/JX/`.
- Already decompiled (`UI`, readable): `NewBattleFieldQueue`, `MapQueue`,
  `DynamicBattleRoyale` (dynamic/default skill bar of the mode),
  `BattleField/BattleFieldMap`, `BattleField/MobaEnemyPanel`, `MiddleMap`, `Minimap`,
  `WorldMap`, `MainBarPanel`; addons `JX`, `JX.UI`, `JX.XGUI`, `JX_LootPlus`,
  `JX_MiddleMapMark`, `JX_Moba`.
- **Newly extracted panels (2026-09-24):** `PVPShowFinal.lua` + `PVPShowFinalL/R.ini` +
  `string_PVPAcount.txt` (`proof/netcode/ui/`), recovered by PakV4 candidate extraction —
  the settlement panel was missing from the 144-file dictionary corpus.
- **Toolchain caveat:** `tools/netcode/bin/unluac.jar` is a **9-byte "Not Found"
  placeholder** in every worktree (verified 2026-09-24); the committed `*.decompiled.lua`
  came from a locally-built unluac (per `MAP_MINIMAP_RESEARCH.md` §432). Restoring/building
  unluac is a prerequisite for decompiling the remaining mode panels; `lua51_dump.py`
  (constants) is the fallback.
- Mode strings: `ui-process-app/Data/text/ui/Scheme/Case/string.txt` carries `STR_BF_*`
  (scoreboard), `STR_BATTLEFIELD_*` (queue/map/guild tips), `STR_SETTING195/197/201`
  (loot-filter UI for the mode).
- **Corpus completeness risk:** the extraction used window names as a dictionary; a
  settlement panel or BR HUD may be missing — enumerate PakV4 `ui/` before declaring any UI
  stage complete.

---

## 8. Unknowns register

### 8.1 Static-pending (close with the RE pass, §11)

| Item | Target |
|---|---|
| `OnSwitchMap` transfer-value semantics | **resolved for `+0x0F/+0x13/+0x17` = world x/y/z** (movement research §3.1); `+0x0B` global and `+0x1B`→`+0xAB0` scene id remain open |
| `MaxSwitchMapMoveDistance` | xref + rule decode |
| `0xD9` vs `0x116` confirm paths | disasm both builders |
| Staging pose trigger | who selects `龙门绝境_站姿01..05` |
| Countdown/phase source | `GetQuestPhase` xrefs, `GetBattleFieldObjective`, `OnSyncBattleStatFlag`, buff 26524 |
| Entry descent | `bOnParachuteFlag` setter, GliderCamera/carrier conditions, SkillMove rows (29021) |
| Storm data source | server→circle center/radius channel (UI `BattleFieldMap`/`MiddleMap` + engine getter) |
| Poison damage strings | xref `Min/MaxPoisonDamage` |
| Death-bag spawn channel | templates → handler |
| Self/competitor combat layouts | handlers listed in `JX3_MODE_GAP_REGISTER.md` §6/§3 |
| Revive behavior | `DoPlayerReviveRequest` payload, 31507/流萤魂返花, post-death state |
| Observer scope | `Btn_Observer` gating in `Minimap.lua` (BR or not) |
| AFK thresholds | `DoReportAutoBattle` payload/punishment |
| Settlement screen | locate panel Lua/ini (PakV4 `ui/`) |
| Revive tables runtime use | `LoadDoodadReviveGroups` etc. — editor vs runtime |

### 8.2 Server-only (not observable locally)

**Pre-match spawn/staging position** — the exact entry location is a server value delivered
in `OnSwitchMap` (`+0x0F/+0x13/+0x17` → world x/y/z, cm). No client table or map file
carries it; stated explicitly because it is a common question.

**Countdown value and start gate** — the client only receives an absolute end time
(`GetBattleFieldPQInfo` 4th value, client cache `+0x1b490`) and renders `end - now`; the
duration and what starts the countdown are server logic and not in any client file.

Matchmaking/copy assignment, start threshold, phase durations + shrink schedule, storm
damage curve, scoring/placement/kill credit, spawn anchors, drop-table rows/rates, revive
point tables, AFK thresholds, reward formulas.

### 8.3 Runtime-capture route (decoders ready, 2026-09-24)

Both decoders consume the same JSONL format
(`{"dir":"S2C","opcode":<dec>,"buf":"<hex>"}`):

- `tools/netcode/loot/capture.py` — spawns / loot / takes (`0x0C`, `0x10`, `0x82`,
  takes `0x4D`/`0x51`).
- `tools/netcode/mode/capture.py` — mode state keyed on the recovered opcodes:
  `0x08` switch map (placement x/y/z cm + scene), `0xAA` side, `0x11A` objective
  increments (index + two values), `0x11B` phase clock (state A/B, start offset,
  duration), `0x119` statistics records (name/side/25 stats), `0x330` stat flag,
  `0x245` BR role data, plus hex logs for `0x25B/0x25F/0x260/0x261/0x262/0x323/0x324`.
  Selftest 7/7; synthetic end-to-end decode verified.

Only a live capture bridge is missing (`REBORN_SERVER_SPEC.md` model); one capture of
`0x11A`/`0x11B` labels the objective-array indices and yields the actual phase/zone
schedule values.

---

## 9. Evidence & reproduce (paths used by this doc)

```
docs/netcode/JX3_MODE_LOAD_FLOW.md            # entry/load sequence, §8 post-enter flow
docs/netcode/JX3_MODE_JUEJING.md              # client-side data map, stances, UI assets
docs/netcode/JX3_MODE_JUEJING_LOGIC.md        # skills, cooldowns, map mechanics
docs/netcode/JX3_MODE_LOOT_SYSTEM.md          # loot containers/tables/dead ends
docs/netcode/JX3_LOOT_PROTOCOL_LAYOUTS.md     # doodad/loot wire layouts
docs/netcode/JX3_MODE_SPAWN_RULES_SEARCH.md   # closed dead end: anchors/refresh/rates
docs/netcode/JX3_MODE_UI_FLOW.md              # screen-level flow: queue panel, loading art/progress, HUD
docs/MAP_MINIMAP_RESEARCH.md                  # UI corpus, storm/battlefield map, markers
docs/JX3_GRAVITY_RESEARCH.md                  # parachute partial, fly/glide states
docs/netcode/JX3_CAMERA_RESEARCH.md           # GliderCamera/carrier camera
proof/netcode/mode_ui/**                      # loading art/progress xrefs, string scans
proof/minimap/ui/Scheme/Case/string.txt       # official UI strings: STR_TIMEDESERT/STR_LEFTPEPLE/STR_SETTING210 (GBK, lines 2251/2425-2429)
proof/netcode/disasm/LuaGetBattleFieldPQInfo.txt      # 4-int panel clock (+0x1b480..90), 4th = absolute end time
proof/netcode/disasm/LuaGetBattleFieldObjective.txt   # 16-int objective array (+0x1b498..4d8)
proof/netcode/disasm/LuaGetBattleFieldStatistics.txt  # statistics list records (name/side/25 qwords)
proof/netcode/disasm/OnSyncBattleStatFlag.txt         # stat-flag handler stores 2 packet u32s
proof/netcode/disasm/bf_cache_writers.txt     # xref trace: all code refs to the BF cache base (0x1809C1320)
proof/netcode/disasm/bf_runtime_writers.txt   # writer handlers: objective update, PQ clock, statistics record builder
proof/netcode/protocol_table_s2c_annotated.tsv  # 810 KPlayerClient S2C handlers with IDs+sizes (annotated)
proof/netcode/protocol_names_s2c.tsv            # VA→name map used for annotation
tools/netcode/parse_protocol_registration.py    # parser: registration function → ID/size/handler table
proof/netcode/ui/pak_candidates/PVPShowFinal.lua       # settlement panel driver (PakV4)
proof/netcode/ui/pak_candidates/PVPShowFinalL/R.ini     # settlement panel layouts
proof/netcode/ui/PVPShowFinal.dump.txt                  # constants dump of PVPShowFinal.lua
proof/netcode/ui/string_PVPAcount.utf8.txt              # settlement/result string table (UTF-8 copy)
tools/netcode/mode/capture.py                 # mode-state capture decoder (recovered opcodes) + selftest 7/7
proof/netcode/ui/PVPShowPanel.dump.txt        # stats panel bytecode constants (GetBattleFieldStatistics + PQ_STATISTICS_INDEX)
proof/netcode/disasm/onswitchmap.txt          # OnSwitchMap + OnSwitchIdentityRespond
proof/netcode/disasm/client_ready.txt         # proto 2 builder
proof/netcode/disasm/apply_enter_scene.txt    # proto 3 builder
proof/netcode/disasm/confirm_enter_queue.txt  # 0x116 builder
proof/netcode/c2s_protocol_catalog.tsv        # 0xB9, 0xD9, 0x116, 0x1C9
proof/netcode/mode_juejing/*                  # symbols, MapList rows, doodad inventory
proof/minimap/ui/Config/Default/**            # UI Lua bytecode + 8 decompiled panels
ui-process-app/Data/text/ui/Scheme/Case/string.txt  # STR_BF_* / STR_BATTLEFIELD_*
```

Decompile command (once a working unluac is restored):

```powershell
java -jar tools\netcode\bin\unluac.jar <ui\Config\Default\Panel.lua> > Panel.decompiled.lua
```

---

## 10. Non-local hypotheses (NOT evidence)

These claims come from official/community guides and **must not be treated as proof**;
they are listed only as things the RE pass should confirm or refute:
airborne/glider entry and a pre-match safe staging countdown; player-visible storm shrink
count; "organizer weapon" starting kit. Everything in §3–§7 above is local evidence or
explicitly tagged.

**Excluded evidence (2026-09-24):** the 30 s/20 s/10 s start announcements, the
"起飞倒计时" timer, and the storm/bandit/chest/ice schedule previously read from a
team-monitor addon's userdata (`interface\MY#DATA\...\userdata\team_mon\remote\*.jx3dat`)
are **player-side addon configuration, not official code or data**. They were removed from
the stage text and the extracted file `proof/netcode/mode_juejing/addon_monitor_juejing.txt`
was deleted. If kept anywhere, they are hypotheses only, to be tested against a packet
capture — never cited.

Research rule going forward: evidence comes from client binaries, PakV4-shipped assets
(`ui/...`, tables, scripts), or server protocol only. The `interface\` addon/userdata tree
is out of scope.

---

## 11. Full-pass RE plan (gap-closing order)

**Corpus build** — decompile all mode-relevant UI Lua (restore unluac first); enumerate
PakV4 `ui/` for missing panels (settlement, BR HUD); extract `Buff.tab`, `CharacterAction.tab`,
full MapList rows, targeted `string.txt` entries.

**Entry pipeline (stages 0–4)** — `OnSwitchMap` consumers + `MaxSwitchMapMoveDistance`;
0xD9/0x116; staging pose; **the packet writer that fills `+0x1b480..0x1b4d8`**
(panel end time + 16-int objective array); glide/parachute/carrier conditions;
starting-kit routine.

**Match runtime (5–7)** — death-bag channel; self/competitor combat layouts; **storm
data-source proof (gate)**; poison xref; revive payload; per-map mechanics.

**Death/end/edge (7.5–8.5)** — ghost/observer gating, revive flow, settlement panel,
AFK protocol 0x1C9, identity/reconnect path.

**Peripheral (9)** — BFTongLeague sign-up/join (`DoSignUpBFTongLeague`,
`OnSignUpBFTongLeagueRespond`, `DoApplyBFTongLeagueJoinInfo`,
`DoApplyBFTongOnlineMemberCount`, `DoCancelBFTongLeagueSignUp`,
`OnCancelBFTongLeagueSignUpRespond`, `OnApplyBFTongLeagueJoinInfoRespond`,
`OnApplyBFTongOnlineMemberCountRespond`; `STR_BATTLEFIELD_GUILDTIP1-5`), custom
rooms/ChaosFight entry.

**Acceptance criteria** — every behavioral claim has a `CODE`/`DATA`/`UI` citation or sits
in §8; all recovered packet layouts appended to §6; per-map chapters complete for all ten
rows; reproduce commands updated.

Linked docs: `JX3_MODE_EDGE_SYSTEMS.md`, `JX3_MODE_UI_FLOW.md`, `JX3_MODE_GAP_REGISTER.md`,
`JX3_MODE_LOAD_FLOW.md`, `JX3_MODE_LOOT_SYSTEM.md`, `JX3_LOOT_PROTOCOL_LAYOUTS.md`,
`JX3_MODE_SPAWN_RULES_SEARCH.md`, `MAP_MINIMAP_RESEARCH.md`.
