# JX3 绝境战场 — user-visible screen flow (queue → loading → match)

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-23
**Method:** static client analysis — decompiled UI Lua, PakV4/`settings` tables,
CDN `.hpkg` member extraction, full binary string scans, capstone xref/disasm.
**Evidence:** `proof/netcode/mode_ui/**`, `proof/netcode/mode_juejing/**`,
`proof/minimap/**` (main worktree), plus the live install paths quoted inline.

This is the **screen-level** companion to `JX3_MODE_LOAD_FLOW.md` (network/load
pipeline). It answers: after a player queues for 绝境战场, what appears on screen,
what is the loading screen made of, and what is visible when the match starts.

Confidence: **HIGH** = literal string/asset with path/offset; **MED** = inferred
from code shape; **LOW** = plausible, not pinned.

---

## 0. Flow at a glance

```
1. Entry      minimap button Btn_BattleField -> NewBattleFieldQueue panel
              (mode tabs; 沙漠风暴/绝境 = maps 296/297/322/410/512/532/645/709/715)
2. Queue      Btn_SingleQueue / Btn_TeamQueue / Btn_RoomQueue
              -> JoinBattleFieldQueue(mapID, camp, isTeam) or custom-room remote call
3. Waiting    buttons show 排队中 + elapsed time; MapQueue panel lists the queue
              (点击取消排队, 自动进入 checkbox); queue-position events update it
4. Pop        confirm dialog (or silent when 自动进入 checked)
              -> DoComfirmEnterQueueMap (C->S proto 0x116)
5. Transfer   server OnSwitchMap (map id) -> SwitchMapBegin -> scene loader
6. Loading    NSX3DEngine::KWindowsLoadingWnd shows art + progress bar + spinner
              (config: <game>/ui/loading/loading.ini, 18 random backgrounds)
              progress driven by KJX3StreamingModule::SetLoadingProgress
              progress handshake ComfirmSyncMapProgress / CancelSyncMapProgress
7. Enter      DoClientConfirmReady (2) -> DoApplyEnterScene (3)
              LOADING_END -> HUD appears (main bar, minimap, BF map, BR skill bar)
8. Match      competitor sync, storm countdown, loot/doodads, statistics
9. Exit       leave/queue end events (BATTLE_FIELD_NOTIFY); BF result columns
```

---

## 1. Entry panel (minimap → battlefield queue)

- Opened from the minimap window: `NewBattleFieldQueue.decompiled.lua:4`
  registers the module against `Normal/Minimap/Wnd_Minimap/Wnd_Over/Btn_BattleField`.
- Panel layout: `ui/Config/Default/NewBattleFieldQueue.ini` (1,129 sections),
  `PageSet_Total` carries the mode tabs:

| tab (checkbox) | mode |
|---|---|
| `CheckBox_Moba` | MOBA 战场 |
| `CheckBox_Zombie` | 僵尸战场 |
| `CheckBox_Battlefield` | 常规战场 |
| `CheckBox_Bomb` | 炸弹人 |
| `CheckBox_DesertStorm` | **沙漠风暴 / 绝境系列** |
| `CheckBox_DesertStorm_Single` | 绝境·单人 |
| `CheckBox_DesertStorm_Skill` | 绝境·技能向 |
| `CheckBox_DesertStorm_Takov` | 绝境·塔科夫向（林海） |
| `CheckBox_PleasantGoat` | 联动模式 |
| `CheckBox_CustomizeBattlefield` | 自定义战场（房间） |

- The 绝境 map set is the same as `MapList.tab` (see `JX3_MODE_LOAD_FLOW.md` §1):
  296/297 龙门寻宝(龙门绝境/夜), 322 沙漠风暴, 410 海岛绝境(沧溟绝境),
  512 白龙绝境, 532 天原绝境, 645 洱海绝境, 709/715 林海绝境.
- Custom-room map buttons ship dedicated selection art (HIGH):
  `ui\Image\DesertStormMaks\CustomizeDSMap\{410CangMing,512BaiLong,645ErHai,Random}.tga`
  — these are "map representative pictures" shown in the room UI, not the loading screen.

## 2. Queue states

Solo / team / room entry (`NewBattleFieldQueue.decompiled.lua`):

```
OperateSigleQueue(mapID)  -> EnterBattleFieldQueue(mapID)                 :2714
OperateTeamQueue(mapID)   -> EnterBattleFieldQueue(mapID, true)           :2729
OperateRoomQueue(mapID)   -> EnterBattleFieldQueue(mapID, false, true)    :2745
EnterBattleFieldQueue     -> JoinBattleFieldQueue(mapID, side, team)      :3062
                             or RemoteCallToServer("On_Zhanchang_RoomBattlefield", ...)
```

Validation before queueing (HIGH): mode mutex over `{322, 296, 645}` (`:2761-2818`),
treasure-hunt party/leader checks, preset/mana-item gates
(`GDAPI_TbfWarePreValue`, `GDAPI_TbfWareCheckManaItem`).

In-queue UI:

| element | evidence |
|---|---|
| button text `排队中...` (`STR_MAP_QUEUE`) + elapsed time | `UpdateJoinTime` `:2590`, `FormatBattleFieldTime`; `GetJoinBattleQueueTime` |
| per-map queue rows with cancel menu `点击取消排队` (`MAP_QUEUE_CANCEL`) | `MapQueue.decompiled.lua:1351-1408` |
| `自动进入` checkbox — "勾选后排队结束时不再弹出提示框，直接进入目标地图" | `MapQueue.decompiled.lua:1427-1455`, `STR_MAP_QUEUE_AUTOSURE_TIP` |
| queue position/ETA updates | `OnQueuePosUpdate`, gateway `DoQueryMapQueueInfo`/`OnSyncMapQueueInfo` |
| countdown buff while queued | `BF_QUEUE_COUNTDOWN_BUFF` (exe string) |

Custom rooms (`On_JueJing_CreateRoom` / `On_JueJing_JoinRoom` / `On_JueJing_StartGame`,
`:5600-5920`) show a room lobby with map select, room code, OB slot and a
`StartGameConfirm` dialog. `OnCreateBFRoomNotify` / `OnStartBFRoomNotify` /
`OnCustomRoomStateUpdate` drive the panel.

## 3. Confirm → scene transfer

- Queue pop → user confirm (or auto-enter) → `DoComfirmEnterQueueMap`
  (C→S protocol **0x116**, 19 B).
- Server picks map/copy → `OnSwitchMap` (map id @+0x07) → client marks
  `SwitchMapBegin`, resets scene, starts `KRecorderSceneLoaderNormal`.
- Client replies `DoClientConfirmReady` (proto **2**) and `DoApplyEnterScene`
  (proto **3**, scene id). Full packet details: `JX3_MODE_LOAD_FLOW.md` §3/§5.

## 4. The loading screen (what the player actually sees)

### 4.1 Implementation — `NSX3DEngine::KWindowsLoadingWnd` (X3DEngine.dll)

This is a **GDI window**, not a fullscreen scene overlay (HIGH).
Strings and functions (image base `0x180000000`):

| item | VA / address |
|---|---|
| `NSX3DEngine::KWindowsLoadingWnd::Init` | str `0x180025140` |
| `NSX3DEngine::KWindowsLoadingWnd::LoadSource` | str `0x180025268`, fn `0x1800183c0` |
| `NSX3DEngine::KWindowsLoadingWnd::ThreadFunction` | str `0x1800252c0`, fn `0x180018cb0` |
| `_LoadingClass` | `0x180025168` |
| `UI\Loading\background.bmp` / `progress.bmp` / `sprite.bmp` | `0x1800251a0/1c0/1d8` |
| `Loading`, `Loading_%d`, `background`, `progress`, `sprite`, `ProgressX`, `ProgressY` | `0x1800251f8 … 0x180025258` |
| `m_hBg` / `m_hProgress` / `m_hSprite` / `m_hWnd` | `0x1800252xx` |
| export `?GetLoadingWnd@NSX3DEngine@@YAPEAVILoadingWindow@1@XZ` | exe import from `X3DEngine.dll` |

`LoadSource` behaviour (disasm `proof/netcode/mode_ui/xref/x3d_loading_section.txt`,
`x3d_loadingwnd_init.txt`):

1. defaults `UI\Loading\background.bmp`, `UI\Loading\progress.bmp`,
   `UI\Loading\sprite.bmp`;
2. opens the ini passed by the caller (`this+0xc`) via
   `Engine_Lua5X64.dll!g_OpenIniFile`;
3. opens `config.ini` and reads `[Login] SceneType`;
4. reads `[Loading] count` (default 1) and, **only when `SceneType == 1`**,
   `[Loading] version1` (default -1);
5. picks the background section:
   - `version1 == -1` → `srand(_time64(0)); idx = rand() % count` (**random**),
   - else `idx = version1`;
6. reads `Loading_%d` → `background` / `progress` / `sprite` (override defaults
   when non-empty) and `ProgressX` / `ProgressY` (ints stored at `this+0x298/0x29c`);
7. loads the three bitmaps and stores their sizes (`m_hBg` w/h at `+0x280/284`,
   `m_hProgress` w at `+0x288`, `m_hSprite` w/h at `+0x290/294`).

Render function `0x180018960` (GDI): `BitBlt` background → `BitBlt` progress
fill (fill width = progress image width × current ratio at `this+0x130`) →
`AlphaBlend` the spinner sprite at the **leading edge of the fill**
(`x = ProgressX + fill − spriteW/2`, vertically centred on the progress bar).
`ThreadFunction` registers `_LoadingClass` and creates a centered
`CreateWindowExA` window sized to the background bitmap (600×333 for the
shipped assets), i.e. the classic small loading panel, not a fullscreen screen.

### 4.2 Config — `<game>/ui/loading/loading.ini` (loose install tree)

`KJX3LoadingModule::OnInitialize` (JX3ClientX64.exe, fn `0x1400af1e0`) formats
`%s/ui/loading/loading.ini` (string VA `0x140956748`), gets the window via
`GetLoadingWnd`, calls its vtable (`Init/SetText/Show`) and checks
`!pLoadingWindow->IsCanceled()`. PakV4 does **not** ship this ini (probe MISS,
`proof/netcode/mode_ui/pakv4_loading_probe.log`), so the file is read from the
install tree: `C:\SeasunGame\Game\JX3\bin\zhcn_hd\ui\Loading\Loading.ini`.

```
[Loading]
count=18
version1=-1

[Loading_0]
background=UI\Loading\background1.bmp
progress=UI\Loading\progress.bmp
sprite=UI\Loading\sprite.bmp
ProgressX=54
ProgressY=280
... Loading_1 .. Loading_17
```

- 18 backgrounds, each 600×333 32-bit BMP. They are **class/character splash
  art** (18 distinct character portraits, each with a JX3 event/anniversary
  logo), not map art — see
  `proof/netcode/mode_ui/loading/client_ui_loading/contact_sheet.png`.
- `progress.bmp` 441×3 (bar), `sprite.bmp` 28×28 (spinner).
- With `version1=-1` the background is chosen randomly per load (see §4.1 step 5).
  The map being entered does **not** select the art in this build.

### 4.3 Progress plumbing (HIGH)

```
Lua SetLoadingProgress(%f)  ->  KJX3StreamingModule::SetLoadingProgress
    (exe strings 0x958618 / 0x9633f0; xref proof/.../client_setloadingprogress.txt)
KJX3LoadingModule::SetLoadingProgress / LoadingComplete / NotifyEndLoading
server handshake: ComfirmSyncMapProgress / CancelSyncMapProgress
UI events: LOADING_BEGIN -> FIRST_LOADING_END -> LOADING_END
    consumers: MainBarPanel, Minimap, MiddleMap, NewBattleFieldQueue,
               DynamicBattleRoyale, JX addon (LoadingEnd)
```

### 4.4 Per-map loading art — shipped, extracted, but not read by the PC client

Every 绝境 map ships a "map representative picture" under
`data/source/maps/<map>minimap/` (or `_mb/`), declared in that map's
`config.ini` `[loading]` section. Extracted from the local CDN `.hpkg` cache
with the new `tools/netcode/extract_hpkg_member.py` into
`proof/netcode/mode_ui/loading/`:

| id | map | art (minimap) | art (_mb) | config `[loading]` | hpkg (minimap / _mb) |
|---|---|---|---|---|---|
| 296 | 龙门寻宝 (龙门绝境) | `loadinglmxb.dds` 1920×1080 | `loadinglmxb.png` 1920×1080 | image=loadinglmxb.dds 1920×1080 | `122_cmzluljblmjok` / `105_dhirli24xvjuv` |
| 297 | 龙门寻宝_夜晚 | `loadinglmjj2.dds` 1920×1080 | `loadinglmjj2.png` 1920×1080 | image=loadinglmjj2.dds 1920×1080 | `50_dy2otnkgvmmji` / `103_naguofwk6qauw` |
| 410 | 海岛绝境 (沧溟绝境) | `loading.dds` 1920×1080 | `loading.png` 1920×1080 | image=loading.dds 1920×1080 | `115_h4swyvjt4rrdy` / `117_76ucqz6f4akw` |
| 512 | 白龙绝境 | — | `bgblk.png` 1920×1080 | image=bgblk.dds 1024×640 | — / `51_hi3jgdxxatvn5` |
| 532 | 天原绝境 | `loadingtyjj.dds` 1920×1080 | `loadingtyjj.png` 1920×1080 | image=loadingtyjj.dds 1024×640 | `55_n27xt43cinqsz` / `108_jglifvkwxi3tm` |
| 645 | 洱海绝境 | `loading_ehjj.dds` 1920×1080 | `loading_ehjj.png` 1920×1080 | image=loading_ehjj.dds 1024×640 | `105_gci66wwa57vhk` / `111_jmowtulthvz2w` |
| 709 | 林海绝境 | `loadingstory.tab` | `loadinghslh.png` 1920×1080 | *(no loading image key)* | `54_n46zvzbl46ixd` / `100_d5dtppcj24fki` |

Notes:

- All DDS/PNG decode to **1920×1080** (DDS magic + Pillow check); the
  `[loading] width/height` fields are logical sizes and do not match the shipped
  pixel size for 天原/洱海/白龙 (1024×640 in ini, 1920×1080 on disk).
- 白龙绝境 has no `loading*` file; its `[loading]` points at `bgblk.dds`
  ("background block") and the shipped `_mb` `bgblk.png` is a real 1920×1080
  image (mean RGB ≈ 104/136/142), i.e. it does have a map picture.
- 林海绝境 ships `loadingstory.tab` (279 B, GBK rich text) — the only 绝境 map
  with per-map loading story text in the index:
  `侠士可在场景中搜寻拾取宝藏、参与区域事件，并在30分钟内，通过接应点逃离…`
  (a 30-minute treasure-hunt extract description).
- **Consumer scan (HIGH):** a byte scan of every DLL/EXE in
  `bin64` (≤150 MB) for `middlemap`, `%sminimap`, `[loading]`,
  `loadingstory`, `loadingtip`, `bgblk`, `loadinglmxb`, `ProgressX`,
  `background.bmp` (`proof/netcode/mode_ui/consumer_scan_bin64.txt`) finds only:
  - `X3DEngine.dll` / `JX3BenchmarkX64.exe`: `ProgressX` + `background.bmp`
    (the loading window itself),
  - `JX3RepresentX64.dll`: `%sminimap` (`data\source\maps\%sminimap\RegionLayer.tab`).
  The map tooling `kg3dsceneresapi.dll` (editor/launcher bundle) reads
  `%s\%sminimap\config.ini` + `middlemap%d` but has no `loading` key.
  → In this PC build the per-map `[loading]` art has **no reader**; it is map
  content (mobile `_mb` variants / tooling / future or alternate consumer), while
  the on-screen loading panel uses the 18 class-art backgrounds from
  `ui/loading/loading.ini`.

## 5. First in-match frame

`LOADING_END` fires after `KRecorderSceneLoaderNormal::PostLoadingScene`; the
mode HUD registers on it and appears immediately:

- main bar / action bar (`MainBarPanel`), minimap lens (`Minimap`),
- battlefield map panel (`BattleFieldMap`, draggable, heat map; `BattleFieldMap.*`
  state in `custom.dat`),
- battle-royale skill bar (`DynamicBattleRoyale`): default weapon skills +
  dynamic looted skills, hotkeys `BATTLEACTIONBAR_BUTTON<n>`, drag-to-drop with
  `szDropSkillRemoteCall`,
- mode character stance set `F1/F2/M1/M2b02ty龙门绝境_站姿01..05.ani`
  (mode-only lobby/staging poses; `Ani.rt`, `JX3_MODE_JUEJING.md` §3.1).

Live reference frame (龙门绝境 gameplay, 2026-04-18 capture):
`C:\SeasunGame\Game\JX3\bin\zhcn_hd\ScreenShot\2026-04-18_15-53-50-000.jpg`
shows 剩余人数, 风暴倒计时, 安全区 phase `已刷新/总圈数 8/8`, 黄龙饱食度,
自动喂马/自动拾取, 伤害统计, team frames, and the draggable battlefield map
(crops in `proof/minimap/screenshots/05_*` and `06_*`, main worktree).

## 6. In-match HUD data model (client cache)

Per `JX3_MODE_JUEJING.md` §4.4/§6 and `JX3_MODE_LOAD_FLOW.md` §8:
competitor list (base/variable info), skill cooldown subscription, buff lists,
statistics, positions, rank/score (`GetBFRank*`, `KBattlefieldCache::GetRoleMapScore`),
objectives, head-top class/param. Storm/safe-zone/airdrop markers use the
`KMapMark` record (`nType` enum still unnamed).

## 7. Exit / result screen

- Leaving the match emits `BATTLE_FIELD_NOTIFY` (leave/queue-end variants) and
  `DoLeaveBattleFieldQueue`; `NewBattleFieldQueue` resets room state.
- Result/scoreboard evidence (HIGH for existence):
  `BATTLEFIELD_RANK_TYPE`, `ApplyBattleFieldStatistics` / `GetBattleFieldStatistics`
  (+ Lua bindings `LuaApply/GetBattleFieldStatistics`), `DoGetBFRankRequest`,
  `GetBFRankList` / `GetBFRankRoleInfo` / `GetBFRankRoleScore`,
  `ClearBFRankList`, `KBattlefieldCache::GetRoleMapScore`.
- Battlefield result table columns in `ui/Scheme/Case/string.txt`:
  `STR_BF_NAME` 名字, `STR_BF_KILL1` 协助击伤, `STR_BF_KILL2` 击伤,
  `STR_BF_DEAD` 受重伤, `STR_BF_DAMAGE` 伤害量, `STR_BF_HEALTH` 治疗量,
  `STR_BF_PRESTAGE` 威望, `STR_BF_REWARD`/`STR_BF_MONEY`/`STR_BF_EXP` 奖励;
  `STR_ENDOFBATTLE_EWAI` 额外奖励.
- The client's battlefield statistics/scoreboard panel is `PVPShowPanel`
  (MED): `ui/Config/Default/PVPShowPanel.ini` (369 sections; team score
  `Text_ScoreL/R`, `Text_TeamNameL/R`, `STR_BF_KILL2`, `STR_PVP_*`) driven by
  compiled `PVPShowPanel.lua`, which calls `ApplyBattleFieldStatistics` /
  `GetBattleFieldStatistics` and exposes `PVPShowFinal` / `PVPShowHarm` /
  `PVPShowSkill` views and `nBattleFieldSide`. Referenced player lists
  (`PVPShowPlayerListL/R.ini`) were not in the extracted UI set.
- In-match damage meter is the separate `FightingStatistic` panel
  (`STR_FIGHTINGSTATISTIC_TITLE` 伤害统计).
- **Open:** whether BR end-of-match uses `PVPShowFinal` or a native/addon
  settlement window; MOBA has `STR_MOBA_OPEN_STATS_PANEL` 打开结算界面 as a
  named counterpart. The JX addon (decrypted bytecode) contains no `STR_BF_*` /
  `BattleFieldStatistics` constants, so the result UI is base-UI/native, not the
  addon.

## 8. Open questions

1. Live-client vs installed `ui/loading/loading.ini`: the live file can be
   hot-updated by the launcher; this doc describes the installed file
   (`count=18`, class art). A future patch could bind backgrounds per scene.
2. Who consumes `<map>minimap/[loading]` on other builds (mobile `_mb`,
   launcher/editor). No PC binary does; a runtime screenshot of a map load is the
   fastest confirmation of what players see.
3. `ProgressX/ProgressY` are relative to the 600×333 window (54,280 = lower-left
   area), not the screen; verify against a live capture if the panel layout ever
   matters for reborn.
4. `loadingstory.tab` / `loadingtip.tab` renderer: only 林海 ships story text in
   the index; format is `<text>text="…" font=N</text>` after a `Story` header.

## 9. Reproduce

```powershell
# 1) loading-window config + assets (loose install tree)
#    C:\SeasunGame\Game\JX3\bin\zhcn_hd\ui\Loading\{Loading.ini,background*.bmp,progress.bmp,sprite.bmp}
# 2) per-map loading art from the local CDN hpkg cache
python tools/netcode/extract_hpkg_member.py <pkg>.hpkg --list
python tools/netcode/extract_hpkg_member.py <pkg>.hpkg --match loading --out-dir proof/netcode/mode_ui/loading/<map> --report <map>/report_loading.json
# 3) DDS -> PNG (Pillow)
#    Image.open(x.dds).convert("RGBA").save(x.png)
# 4) wiring xrefs (pefile + capstone)
python tools/netcode/xref_string.py <binary> "KWindowsLoadingWnd::Init" --out proof/netcode/mode_ui/xref/x3d_loadingwnd_init.txt
python tools/netcode/xref_string.py <binary> "%s/ui/loading/loading.ini" --out proof/netcode/mode_ui/xref/client_loading_ini.txt
# 5) PakV4 probe for the loading ini (expected MISS)
python tools/netcode/extract_pak_paths.py --list proof/netcode/mode_ui/pakv4_loading_candidates.txt --out-dir proof/netcode/mode_ui/pakv4
```

CDN hpkg cache root:
`C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\cache-extraction\online-cdn\downloads\`

## 10. Evidence index

| file | content |
|---|---|
| `proof/netcode/mode_ui/loading/client_ui_loading/*.png` | 18 client loading backgrounds + `contact_sheet.png`, `progress.png`, `sprite.png` |
| `proof/netcode/mode_ui/loading/<map>/{config.ini,loading*,report_*.json}` | per-map `[loading]` config + extracted art (DDS/PNG) |
| `proof/netcode/mode_ui/strings/X3DEngine_strings.txt` | loading window strings/offsets |
| `proof/netcode/mode_ui/strings/kg3dsceneresapi_strings.txt` | map tooling `config.ini`/`middlemap` strings |
| `proof/netcode/mode_ui/xref/x3d_loading_section.txt` | `LoadSource` disasm (art selection, ProgressX/Y) |
| `proof/netcode/mode_ui/xref/x3d_loadingwnd_init.txt` | `Init` + window/thread + GDI render disasm |
| `proof/netcode/mode_ui/xref/client_loading_ini.txt` | `KJX3LoadingModule::OnInitialize` disasm |
| `proof/netcode/mode_ui/xref/client_setloadingprogress.txt` | Lua→streaming progress chain |
| `proof/netcode/mode_ui/consumer_scan_bin64.txt` | bin64 scan proving no per-map `[loading]` reader |
| `proof/netcode/mode_ui/pakv4_loading_probe.log` | PakV4 probe: all 5 loading candidates MISS |
| `tools/netcode/extract_hpkg_member.py` | hpkg LZHAM index/member extractor (raw + skip-20 variants) |

Linked docs: `JX3_MODE_LOAD_FLOW.md` (network/load pipeline),
`JX3_MODE_JUEJING.md` (mode data map), `JX3_MODE_GAP_REGISTER.md` (gaps),
`MAP_MINIMAP_RESEARCH.md` (main worktree; minimap/big-map art + in-game crops).
