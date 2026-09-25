# JX3 绝境战场 — edge systems (death/observer, AFK, reconnect, guild league, rooms, rewards)

**Branch:** main
**Date:** 2026-09-24
**Method:** static consolidation (binary string/xref inventories, shipped UI Lua, client
tables) as a companion to `JX3_MODE_MATCH_LIFECYCLE.md`; no new disassembly yet.
**Tags:** `CODE` / `DATA` / `UI` / `UNPROVEN` (see lifecycle doc legend).

These systems sit around the match flow and were previously unlisted. Each section states
what is proven and what the full-pass RE must still settle.

---

## 1. Death / ghost / revive

Proven symbols and data:

| Item | Evidence |
|---|---|
| C→S `DoPlayerReviveRequest`, **protocol 0xB9**, size 0xF, builder `0x180176abc` | `proof/netcode/c2s_protocol_catalog.tsv:282`; `KPlayerClient::DoPlayerReviveRequest` in `symbols_KPlayerClient.txt:229` |
| `DynamicReviveMinTime` string | exe `0x00802D08` (`JX3ClientX64_exe_net_strings.txt:736542`); grouped with mode feature symbols (`mode_load_symbols.txt:37`) |
| Revive-setting validation strings: `Intensity`, `ReviveTime`, `DynamicReviveMinTime`, "Npc (%u) ReviveMinTime:%d should not greater than ReviveTime:%d" | `proof/netcode/mode_juejing/poison_revive_strings.txt:30-32` (editor/logic binary — settings are authored per NPC/entity) |
| `ReviveInSitu=0`, `RevieCycle=160` | MapList rows (`DATA`, lifecycle §1) |
| Revive skill 天原绝境_冰封复活 31507, script `沙漠风暴/天原主动复活.lua` (script not in pak) | `mode_skills.txt:17`, `mode_kit_report.txt:18` |
| Revive item 流萤魂返花 (container 6973, script `拾取黑莲花.lua`) | `mode_doodad_inventory.txt:46` |
| 吃鸡 break-ice cooldown 2375 (60 s) | `JX3_MODE_JUEJING_LOGIC.md` §2 |
| Ghost model support `ApplyGenModelGhost` | Represent `0x00CA1A30` (`JX3RepresentX64_strings.txt:742162`) |
| 灵魂出窍02 animation ships next to the BR stances | `gbk_juejing_editor.txt:1` (`F1b02ty灵魂出窍02.ani` beside `F1b02ty龙门绝境_站姿01.ani`) |
| `Alive` / `IsAlive` mode-feature symbols | `mode_load_symbols.txt:13,67,286` |

**UNPROVEN:** post-death state in a BR copy (corpse-only vs ghost vs observer), what drives
`DoPlayerReviveRequest` (item vs map mechanic vs team revive), where revive points come from
(`MapReviveList` is server-side / absent locally), and how `RevieCycle=160` frames are
applied. The 灵魂出窍02 adjacency is suggestive but not proof of a BR death state.

---

## 2. Observer / spectate

Client UI and engine evidence exists, but no BR gating has been proven:

| Item | Evidence |
|---|---|
| Observer button + container + handler in the minimap window | `proof/minimap/ui/Config/Default/MiniMap.ini:1495-1543` (`[WndContainer_Observer]`, `[Btn_Observer]`, `[Handle_Observer]`) |
| Driver code: `UpdateObserverButton`, click branch on `Btn_Observer` | `proof/minimap/ui/Config/Default/decompiled/Minimap.decompiled.lua:635,3654,4629,7452` |
| Engine observer params `OnSetObserverParam` | Represent `0x00C83F78` (`JX3RepresentX64_strings.txt:731969`) |
| `GetAllObserver` in the global skill/script constants | `proof/netcode/skill_data/skill_lh_constants.json:2838` |
| Arena replay stack (`KVideoReplayer::*`) — includes `OnSyncArenaCompetitor*`, `OnArenaVideoStart`, `OnArenaEnd`, and also `OnSyncBattlefieldStatistics` | `JX3ClientX64_exe_net_strings.txt:2263-2298` |

**UNPROVEN:** whether `Btn_Observer` is shown in 绝境 (it may be the generic
arena/dungeon observer). Decision test: inspect the `Minimap.lua` branch conditions
(`IsInBattleField`, `IsInTreasureBattleFieldMap`) and the `KVideoReplayer` start trigger.
`ReviveInSitu=0` plus revive skills/items suggests revive-first, but does not exclude a
spectate window.

---

## 3. AFK / auto-battle report

| Item | Evidence |
|---|---|
| C→S `DoReportAutoBattle`, **protocol 0x1C9**, builder `0x18017777f` | `proof/netcode/c2s_protocol_catalog.tsv:299` |
| `KPlayerClient::DoReportAutoBattle` | `proof/netcode/symbols_KPlayerClient.txt:246` |
| Lua bridge `LuaReportAutoBattle` | exe `0x007FEA30` (`JX3ClientX64_exe_net_strings.txt`) |
| `ReportAutoBattle` string | exe `0x007F5760` (`JX3ClientX64_exe_net_strings.txt:730244`) |

**UNPROVEN:** payload layout, detection thresholds (who decides "auto-battle"), and the
punishment/response. The packet exists in the mode's symbol cluster
(`battlefield_api_symbols.txt:62`), so the feature is mode-relevant; the RE pass should
disassemble the builder and any S→C answer.

---

## 4. Disconnect / reconnect / identity

| Item | Evidence |
|---|---|
| `KPlayerClient::OnSwitchIdentityRespond` handler co-located with `OnSwitchMap` (fn at `0x180191150`, self-referencing log string) | `proof/netcode/disasm/onswitchmap.txt:11-12` |
| `SyncTempData` MapList column (mode rows carry it in the full schema) | `proof/netcode/mode_juejing/maplist_parse.txt:2` |
| `UseLastEntry=1`, `KeepTime=2` on all 绝境 rows | `juejing_maplist_rows.txt` |
| Our own resume model (reference, not JX3 evidence) | `REBORN_SERVER_SPEC.md` §4 |

**UNPROVEN:** the identity-switch packet flow (relogin vs server identity change vs bot
takeover), what happens to a BR player who drops mid-match, and whether a reconnect returns
them to the same copy/position (`UseLastEntry` is the candidate flag; semantics MED).

---

## 5. Guild league (BFTongLeague)

A separate sign-up/league surface exists beside the solo/guild queue:
`DoSignUpBFTongLeague`, `DoCancelBFTongLeagueSignUp`, `DoApplyBFTongLeagueJoinInfo`,
`DoApplyBFTongOnlineMemberCount`, and the matching `On*Respond` handlers
(`CODE` `battlefield_api_symbols.txt:48-51,63,89-90,94,103`). UI explains the rules:
`STR_BATTLEFIELD_GUILDTIP1-5` = 约战开始后方可排队 / 同盟帮会 >72h / 外交权限发起排队 /
预备期成员不得参战 / 约战时间与对战方见帮会外交界面 (`UI` `string.txt:941-945`).

**UNPROVEN:** whether this league uses the 绝境 maps or the classic battlefield maps, and
whether it is in scope for the BR reproduction at all. The lifecycle doc treats it as
peripheral until the pass resolves the map binding.

---

## 6. Custom rooms / ChaosFight force-start

| Item | Evidence |
|---|---|
| `OnCreateBattlefieldRoomRespond` with size assert `S2C_BATTLE_FIELD_CREATE_ROOM_RESPOND` | `proof/netcode/mode_juejing/exe_mode_keywords.txt:1,11,18,28` |
| `OnForceStartBattleFieldChaosFightRespond` with size assert `S2C_FORCE_START_BATTLE_FIELD_CHAOS_FIGHT_RESPOND` | same |
| Room UI: `Bool_CreateRoom/JoinRoom/StartGame/DisbandRoom/ExitRoom` confirm dialogs; leave buttons per queue type | `UI` `NewBattleFieldQueue.decompiled.lua:5604-5882,828-847` |

**UNPROVEN:** room payloads (room id, map, rules, member list), how a forced-start match
proceeds through stages 2–4, and whether rooms can pick maps/copies.

---

## 7. Rewards / report chain

| Item | Evidence |
|---|---|
| `KPlayerBattleStat::AddData` `0x008134F0`, `ReportBattleStat` `0x00813510` | `JX3ClientX64_exe_net_strings.txt:6351-6352` |
| `OnSyncBattleStatFlag` | exe `0x007C9368`, logic `0x0077A418` |
| `ApplyBattleFieldStatistics`, `GetBattleFieldStatistics` + Lua twins | `mode_load_symbols.txt:129,140,154,158,345,354,368,372` |
| Rank: `DoGetBFRankRequest`, `OnGetBFRankRespond`, `GetBFRankRoleInfo/Score`, `ClearBFRank{,List}`, `LuaClearBFRankList` | `mode_load_symbols.txt:34,40-43,88,91-92,125,259-262,309,312-313` |
| Reward multipliers `MultipleRankPoint`, `...BeginTime`, `...EndTime` | `mode_load_symbols.txt:117-119` |
| Income rule `BattleFieldIncomePunish` | `mode_load_symbols.txt:131,346` |
| Scoreboard/reward labels `STR_BF_REWARD/MONEY/EXP/PRESTAGE` (奖励/奖励金钱/奖励经验/威望) | `UI` `string.txt:285-291,321-326` |

**UNPROVEN:** when the report fires (death? match end?), the statistics payload, reward
formulas and weekly caps (SERVER), and whether `ReportBattleStat` is tied to
`OnSyncBattleStatFlag` or to the settlement screen (lifecycle stage 8 gate).

---

## 8. Planned proof work (feeds lifecycle §11)

1. Disassemble builders/handlers: `DoPlayerReviveRequest` (0xB9),
   `DoReportAutoBattle` (0x1C9), `OnSwitchIdentityRespond`, BF-league set, room set.
2. Decompile remaining relevant UI (restore a working unluac first):
   `Minimap.lua` observer gating, settlement panel (locate via PakV4 `ui/` enumeration),
   `FightingStatistic`/`FightingNum`, `MainMessageLine`.
3. Resolve map binding for the guild league (绝境 vs classic battlefield) before writing it
   into the lifecycle.
4. Decide observer scope: BR spectate vs generic arena observer, by tracing
   `KVideoReplayer` start conditions.

Linked: `JX3_MODE_MATCH_LIFECYCLE.md` (§9 lists all evidence paths),
`JX3_MODE_UI_FLOW.md`, `JX3_MODE_GAP_REGISTER.md`.
