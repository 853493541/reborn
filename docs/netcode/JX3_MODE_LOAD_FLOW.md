# How JX3 loads the 绝境战场 mode — client-side flow

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Method:** static client analysis + PakV4 config extraction (`settings\` tables).
**Evidence:** `proof/netcode/mode_juejing/*` (tables, disassembly, symbol scans).

This answers "how does the game load the mode" from the client's side: the queue →
scene-transfer → map-load pipeline, the config table that parameterises the mode,
and the runtime machinery that only exists while the mode is active.

For the **screen-level** flow (queue panel, loading window art/progress, first
in-match HUD frame, result) see `JX3_MODE_UI_FLOW.md`.

---

## 1. Where the mode is defined: `settings\MapList.tab` (HIGH)

The client ships its own map/scene table — extractable from PakV4 with the
`settings\` prefix (case-insensitive):

```powershell
python tools\netcode\extract_pak_paths.py --list <paths.txt> --out-dir <dir>
# path: settings\MapList.tab
```

`MapList.tab`: 678 rows × **77 columns**. Ten 绝境 rows:

| ID | Name | DisplayName | ResourcePath |
|---|---|---|---|
| 296 | 龙门寻宝 | **龙门绝境** | `data\source\maps\龙门寻宝\龙门寻宝.jsonmap` |
| 297 | 龙门寻宝_夜晚 | 龙门绝境·夜 | `...\龙门寻宝_夜晚\...` |
| 410 | 海岛绝境 | **沧溟绝境** | `...\海岛绝境\...` |
| 512 | 白龙绝境 | 白龙绝境 | `...\白龙绝境\...` |
| 532 | 天原绝境 | 天原绝境 | `...\天原绝境\...` |
| 645 | 洱海绝境 | 洱海绝境 | `...\洱海绝境\...` |
| 676 / 677 | 龙门寻宝 | 龙门绝境 | (alternate copy configs) |
| 709 | 林海绝境 | 林海绝境 | `...\林海绝境\...` |
| 715 | 林海绝境 | 林海绝境·纷争 | `...\林海绝境\...` |

Full per-row config (`proof/netcode/mode_juejing/juejing_maplist_rows.txt`). Mode flags
that are identical across the BR maps:

| Column | Value | Meaning (inferred) |
|---|---|---|
| `IsBattlefield` | **1** | scene is a battlefield-type map |
| `CampType` | **5** | camp/team scheme used by the mode |
| `Type` | 2 | instance type |
| `MaxPlayerCount` | **118** (洱海/龙门副本 108) | players per copy |
| `MaxCopyCount` | 512 / 1024 | room copies |
| `MinPlayerCount` | 1 | |
| `BanSkillMask` | 512 | banned skill group (mode-restricted skills) |
| `BanUseItemMask` | 4 (林海 32) | banned item groups |
| `ReviveInSitu` | 0 | revive at corpse disabled |
| `RevieCycle` | 160 | revive cycle value |
| `InvalidBuffMask` | 8 | buffs invalidated in mode |
| `QueueForSwitchWhenFull` | 1 | queue instead of refusing when full |
| `BanChangeTalent` | 1 | talent changes disabled |
| `CanSprint` | 1 (林海 0) | sprint allowed |
| `OperationMask` | 7 | allowed operations mask |
| `BanTradeItemMask` / `CanTradeMoney` | 4 / 0 | economy restrictions |
| `BanEquipItemMask` / `CanExistItemMask` | 2 / 1 | equipment restrictions |
| `MaxLootRange` | 5 | loot range |
| `ScriptFile` | *(empty)* | **no client map script for 绝境 maps** — mode logic is server-driven |

Contrast: 70 other maps (e.g. 伊丽川 camps) do set `ScriptFile`
(`scripts/Map/...lua`). The BR maps do not, so their rules are not client-scripted.

**Related mode content tables** (also extractable, see earlier mode doc):
`Doodad\沙漠风暴.tab` (tier 1-3 loot props), `沙漠风暴_寻宝模式.tab` (disguises/stealth box),
`settings\DoodadTemplate.tab`, `settings\NpcTemplateList.tab`, `settings\CharacterAction.tab`.

## 2. Entry: queue → confirm → transfer (HIGH)

Client-side entry API surface (from `JX3ClientX64.exe` strings):

```
JoinBattleFieldQueue / LeaveBattleFieldQueue / AcceptJoinBattleField / LeaveBattleField
JoinTongBattleFieldQueue / LeaveTongBattleFieldQueue / AcceptJoinTongBattleField
BF_QUEUE_COUNTDOWN_BUFF                    ← countdown buff while queued
DoQueryMapQueueInfo … OnSyncMapQueueInfo   (gateway: queue state/ETA)
DoComfirmEnterQueueMap                     ← user confirms entry
DoLeaveMapQueue / OnSyncMapQueueInfo       (leave queue)
GetBattleFieldPQInfo                       ← queue info for UI
KGPQ::LuaGetScene                          ← queue hands the scene to the client
```

Protocol (from disassembly of `KPlayerClient::DoComfirmEnterQueueMap`, logic `0x180171740`):

```
C→S  protocol 0x116, 19 bytes: u32 @+0xB, u32 @+0xF   (map/queue params)
```

## 3. Scene transfer and load pipeline (HIGH)

Server→client `KPlayerClient::OnSwitchMap` (logic `0x1801911F0`) reads the packet fields:

| Packet offset | Use |
|---|---|
| `+0x07` | map id (stored to `dwSwitchMapID` global) |
| `+0x0B`, `+0x0F`, `+0x13`, `+0x17` | transfer values (position/heading/copy) |
| `+0x1B` | scene-related id (stored at player+0xAB0) |

then marks `SwitchMapBegin`, resets scene state, and drives the scene loader.

Client→server confirmations (protocol IDs recovered from builders):

| Message | Protocol | Size | Payload |
|---|---|---|---|
| `DoClientConfirmReady` | **2** | 11 B | none |
| `DoApplyEnterScene` | **3** | 15 B | u32 @+0xB = scene id (`pScene+0x788`) |
| `DoComfirmEnterQueueMap` | 0x116 | 19 B | u32 @+0xB, u32 @+0xF |

Loading-progress handshake: `ComfirmSyncMapProgress` / `CancelSyncMapProgress`
(Lua: `LuaComfirmSyncMapProgress`), plus `DoComfirmOrCancelSyncMapProgess`.

Loading stack (symbols in `JX3ClientX64.exe` / `JX3LogicEditOperationX64.dll`):

```
SwitchMapBegin / SwitchMapEnd
KRecorderSceneLoaderNormal / Record / Replay  (CreateSceneLoader factory)
LoadScene / LoadSceneData / LoadSceneSfxClient / LoadMapBaseInfo
KJX3LoadingModule (SetLoadingProgress, LoadingComplete, NotifyEndLoading, PostLoadingScene)
PakV4PrefetchManager_OnPrepareEnterScene / _OnEnterScene
```

Map metadata: `settings\SubMapInfo.tab` (66 rows: `SubMapID, MainMapID, MainPosX,
MainPosY, SubRegionWidth, SubRegionHeight`) is the sub-region table used with
`KMapListFile::LoadSubMapInfo`; scene ids there (e.g. 788) are the values passed in
protocol 3.

## 4. Mode runtime machinery (active only while the mode is loaded) (HIGH)

- Scene owner type: `pqotBattleField` (vs `pqotQueuedDungeon`) in `KGPQ`.
- Teams/sides: `ApplyBFPlayerTeamGroupID` / `GetAllBFPlayerTeamGroupID`,
  `KCharacter::SetBattleFieldSide` (`INVALID_BATTLE_SIDE < side < MAX_BATTLE_SIDE`),
  nameplate field `nBattleFieldSide`.
- Competitor state: `KBattlefieldCache` + periodic `AddNextSyncTime`;
  `DoSyncBattlefieldCompetitorsListRequest`,
  `OnSyncBaseInfoFromBattlefieldCompetitorList`,
  `OnSyncVariableInfoFromBattlefieldCompetitorList`,
  `DoSyncBattlefieldCompetitorSkillCDStateRequest` / cancel, `OnSyncBattlefieldCompetitorCDState`,
  `OnSyncBattlefieldCompetitorBuffList`, `OnSyncBattlefieldStatistics`,
  `GetBattlefieldPlayersPosInfo`, `m_pScene->m_BattlefieldCompetitorInfoMap`.
- HUD: `UpdateBFPlayerHeadTopParam`,
  `KREPRESENT_EVENT_GET_ALLBFPLAYER_KUNGFUID` / `_PARAM`,
  `HandleGetBFPlayerKungfuID` / `HandleGetBFPlayerParam`.
- Objectives/economy: `GetBattleFieldObjective`, `BattleFieldIncomePunish`.
- Rooms/rapid start: `OnCreateBattlefieldRoomRespond` (+`S2C_BATTLE_FIELD_CREATE_ROOM_RESPOND`),
  `OnForceStartBattleFieldChaosFightRespond` (+`S2C_FORCE_START_BATTLE_FIELD_CHAOS_FIGHT_RESPOND`).

## 5. Reconstructed load sequence

```
1. UI        JoinBattleFieldQueue / JoinTongBattleFieldQueue
             gateway: DoQueryMapQueueInfo -> OnSyncMapQueueInfo (position/ETA)
2. Confirm   user accepts -> DoComfirmEnterQueueMap            (C→S proto 0x116)
3. Transfer  server picks map (e.g. 512 白龙绝境), sends OnSwitchMap
             client stores dwSwitchMapID, marks SwitchMapBegin
4. Ready     DoClientConfirmReady                              (C→S proto 2)
5. Enter     DoApplyEnterScene (scene id)                      (C→S proto 3)
6. Load      KRecorderSceneLoaderNormal -> LoadSceneData (+SFX client)
             KJX3LoadingModule::SetLoadingProgress / LoadingComplete / NotifyEndLoading
             PakV4PrefetchManager prefetches during the transition
             progress handshake: ComfirmSyncMapProgress / CancelSyncMapProgress
7. Spawn     server spawns player in the battlefield scene (pqotBattleField)
             battlefield side assigned, competitor cache starts syncing
8. Runtime   competitor list/buffs/cooldowns/positions + objectives; HUD head-top params
```

## 6. What is still server-side (not in the client)

- The mode's actual rule script/timers (zone phases, respawn rules, scoring) — the 绝境
  rows have **no `ScriptFile`**; they are driven by server logic and synced protocol.
- Queue matchmaking numbers and copy assignment.
- The poison/storm/zone values (the `Poison*` strings found in the exe are damage-type
  stat names, not the BR zone).

To go further: capture the runtime competitor/stat sync messages (server → client
protocols listed above), or enumerate the UI strings inside PakV4 (`ui/...`) where the
mode HUD labels live.

## 7. Reproduce

```powershell
# extract client config tables (settings\ prefix is case-insensitive)
# list: proof/netcode/mode_juejing/pak_candidates*.txt
python tools\netcode\extract_pak_paths.py --list proof\netcode\mode_juejing\pak_candidates.txt  --out-dir proof\netcode\mode_juejing\pak_out
python tools\netcode\extract_pak_paths.py --list proof\netcode\mode_juejing\pak_candidates2.txt --out-dir proof\netcode\mode_juejing\pak_out2
```

Evidence: `proof/netcode/mode_juejing/juejing_maplist_rows.txt`,
`pvpfield_and_scriptfile.txt`, `scene_load_symbols.txt`, `mode_load_symbols.txt`,
`tab_paths.txt`, `disasm/onswitchmap.txt`, `disasm/apply_enter_scene.txt`,
`disasm/client_ready.txt`, `disasm/confirm_enter_queue.txt`.

## 8. Post-enter data flow — is everything sent at once? **No** (HIGH)

The mode state arrives as a hybrid of sectioned initial sync + pull + subscriptions:

| Mechanism | Evidence | Meaning (confidence) |
|---|---|---|
| **Sectioned initial sync** | client sends protocol **4** (11-byte, header only) from `0x1801A3FD0`; server closes with `OnSyncRoleDataOver` logging `Sync role data over !` (`0x1801A3DE3`) | role data is chunked and terminated by an "over" message (HIGH for the facts; "acks per chunk" MED) |
| **Mode role data** | `OnSyncBFRoleData` (`0x180193AC0`): `+0x0F` passed to entity lookup, `+0x13` u32, `+0x17` pointer, `+0x6F` count byte → manager vtable `+0x13C8` | per-player BR data applied incrementally (offsets HIGH; "player id"/camp-side meaning MED) |
| **Competitor list is pull-based** | `DoSyncBattlefieldCompetitorsListRequest` (`0x1801A9470`) sends protocol **0x164** (11-byte) when `scene+0xF0` is below the packet byte `+0x11` | compare-and-request logic HIGH; labels "cached version" MED |
| **Competitor cooldowns are subscribed** | `DoSyncBattlefieldCompetitorSkillCDStateRequest` / `DoCancelSync...` | per-competitor CD sync on/off (HIGH: message names) |
| **Periodic refresh** | `KBattlefieldCache::AddNextSyncTime` | timer-driven re-sync (HIGH: name; interval unknown) |
| **Continuous world replication** | `OnSyncNewPlayer` / `OnSyncNewNpc` / `OnSyncEntity` / `OnSyncSimpleObject` | entity/AOI streams independently (HIGH: handlers exist) |

Expected order after `DoClientConfirmReady` (proto 2) / `DoApplyEnterScene` (proto 3):

```
sectioned role data (proto 4 acks) -> OnSyncRoleDataOver
  -> continuous entity/AOI streams
  -> pull competitor list (0x164) when stale
  -> subscribe competitor CD states as needed
  -> periodic KBattlefieldCache refresh + OnSync* deltas
  -> containers spawn/despawn via OnSyncNewDoodad / OnSyncDoodadState (loot chain)
```

Mode path protocol IDs known so far: **2** ready, **3** enter scene, **4** role-data
section check, **0x116** confirm queue entry, **0x164** competitors list request,
**0x4D** apply loot, **0x51** loot money, **0x6E** routine sync, **6** ping.
