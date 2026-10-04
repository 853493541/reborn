# Basic UI — HUD core deep research (session 2)

**Scope:** the always-on in-world HUD of the default client UI (tier 1 of
`BASIC_UI_INVENTORY.md`). **Verification: INI + authored state only** (user decision) — every viewer
entry built from this doc renders the authored INI state and is marked `PARTIAL` until GT captures
exist. Mechanism claims below carry their source path; Lua line numbers are from the unluac output.

**Sources**
- INIs: `ui-process-app/assets/ui/Config/Default/*.ini` (extracted 2026-10-03, see
  `BASIC_UI_INVENTORY.md`; Target family extracted in this session).
- Lua: extracted from PakV4 (`ui/Config/Default/**.lua`, 54 files for this group) and decompiled with
  unluac; local copies under the ignored `proof/ui/basic_ui/decompiled/`.
- Renderer semantics: `UI_SYSTEM_REPORT.md` (PosType/ImageType/LSH/etc.), plus the two engine
  behaviors fixed/used this session (below).

## 1. MainBarPanel — 主技能栏

**INI** `MainBarPanel.ini`, 93 sections. Root: `WndFrame 914x116 @477,357` under `Lowest1`,
`ShowModeID=27`, `ScriptFile=MainBarPanel.lua`.

| block | sections | role |
|---|---|---|
| slot strip A | `Handle_BG1` (LSH=1) → `Image_Bg1..16` | 16-slot row: left cap 157 + 14 tiles 46 + right cap 113 = **914 = bar width exactly**; art frames 0/1/2 of `MainBarPanel.UITex` |
| slot strip B | `Handle_BG2`/`Handle_BgS2` → `Image_Bg1_3`, `Image_Bg2_1..16_2` | alternate/dynamic background set toggled by the Lua (state skins) |
| kungfu box | `Animate_KfAni`, `Image_KFBright`, `Box_Kungfu`, `Image_CD`, `Text_Kungfu`, `Text_Page` | current 内功 icon + cooldown sweep + page label (authored sample 9) |
| pose row | `Handle_PoZhaos`/`Handle_PoZhao_1..5` | five stance/pose buttons |
| page/lock | `Btn_PageUp`, `Btn_PageDown`, `CheckBox_Lock`, `CheckBox_UpDown` | page switching + bar lock |
| settings | `Btn_setting`, `Wnd_CustomMode`/`Handle_CustomMode`, `CheckBox_Assisant` | settings, custom-mode chip, assistant toggle |
| shell | `Handle_Shell`, `Animate_Gear` | decorative shell + gear animation |

**Art truth (TGA probe):** slot frame 0 (left cap) is a semi-transparent blue plate
(RGBA ≈ (39,65,98), **alpha 67**); frames 1/2 (middle tiles, right cap) are **fully transparent** in
the shipped atlas — the visible slot cells/icons come from runtime skill buttons, not this strip.

**Lua** (`MainBarPanel.decompiled.lua`): toggles `Handle_BG1` vs `Handle_BG2/Handle_BgS1|S2` per bar
state, builds the slot buttons/icons from skill data, sets `Text_Page`/kungfu icon, drives the pose
handles. Slot **contents are runtime-only** — no authored sample exists.

**Viewer entry `main-bar` (2.1):** shows `Handle_BG1` (the canonical strip; LSH hides it by default),
hides `Handle_BG2`/`Handle_BgS2` (alternate/dynamic skins), adjusts `Image_Bg1/2/3` Left (0/157/203) so
the strip lays out from the bar origin — the authored images carry no `Left` and rely on item flow.
Backdrop `#33393E` (the dark plate art is invisible on `#101010`). Result: bar frame + kungfu box +
page controls; slot icons are a documented gap (runtime skill data).

## 2. Player — 玩家状态框

**INI** `Player.ini`, 88 sections. Root: `WndFrame 398x119 @301,700` under `Normal`.

Authored blocks: fight-state art (`Image_FightBG1`, `Animate_FightBight`, `Animate_FightTop`), back
plates (`Handle_Back`/`Image_Back*`), portrait frame, and **sample texts baked into the INI** — the
render shows `玩家名的名`, `23424/54554` (HP sample), level `100`, buff anchors. `Player.lua` updates
HP/mana/energy values, portrait, buff rows and fight state at runtime.

**Viewer entry `player-frame` (2.2):** authored state as-is (sample texts included), backdrop
`#33393E`. Runtime values/buffs are a documented gap.

## 3. Target family — 目标框

**Composition (decoded):** `Target.lua` opens the per-target INI via
`Wnd.OpenWindow(<ini>, "Target")` (`Target.decompiled.lua:4652-4672`) where `<ini>` =
`ui/config/default/TargetCommon.ini` base selection (mobile variant when streaming) plus:
- **players:** `TargetPlayer10.ini` / `TargetPlayer11.ini` (10 = not enemy, 11 = enemy; `S` suffix =
  standard-target mode),
- **NPCs:** `Target<Intensity><Relation>.ini` (intensity 1..4 via the `nIntensity` map 2|6→4, 5→3,
  4→2 else 1; relation 2=enemy 1=neutral 0=ally), `+S` in standard mode.

Then it **appends the shared overlay**: `Handle_Energy:AppendItemFromIni(TargetCommonPath, <kungfu
handle>, nil, true)` per kungfu type (`Target.decompiled.lua:967-989`; map at :74-85:
TANG_MEN→`Handle_TM`, MING_JIAO→`Handle_MJ`, CANG_JIAN→`Handle_CJ`, CANG_YUN→`Handle_CangYun`, …).

| INI | sections | role |
|---|---|---|
| `TargetPlayer10` | 77 (root section `Target10`) | frame art: `Handle_TarBg` (`Image_TarBgL/C/CR`), `Handle_FBg`, buy-bg set, avatar + school icon, `Image_Health`/`Image_Mana`/`Image_SubHealth`, `Image_NPCMark`, `Text_Target`, cure shield |
| `TargetCommon` | **878** (root `0x0 @0,300` under `Normal`) | shared overlay: buff/debuff BG, cast bars (`Animate_Short`/`Image_Short`/`Text_Short`, `Animate_Long`/…), `Handle_TM`/`MJ`/`CJ`/`CangYun` class handles, WuSheng/CangYun SFX sets, `CY_*` per-class elements |

The Target10..42/…S family (29 INIs extracted this session) are the NPC/player variants of the same
frame; `TargetS.ini` never exists.

**Viewer entry `target-frame` (2.3):** `TargetPlayer10.ini` authored state (name sample `考`, HP sample
`200/3000`, level 100, bars). **Gap:** the `TargetCommon` overlay append is not replayed (viewer has
no multi-INI append); documented here for a later engine feature.

## 4. Remaining HUD windows — status register

| window (INI) | sections | root (authored) | what it is / next step |
|---|---|---|---|
| `BuffList` / `DebuffList` | 13 / 10 | 0x0 @600,50 `Normal` | player buff/debuff rows (runtime clones of a row template; anchor container) |
| `TargetBuff` / `TargetDeBuff` | 13 / 9 | 388x45 @560,120 / 160 | target buff/debuff rows |
| `ExpLine` | 28 | 1280x22 @100,400 | experience bar (runtime value) |
| `CompassPanel` | 57 | 236x259 @500,100 | compass ring + markers |
| `QuestTraceList` | 98 | 300x550 @500,150 | quest tracker list (rows runtime); pages QuestTraceList/Achi/PQ — viewer default `Page_QuestTraceList` |
| `ChatPanel_Bg/_Game/_Normal/_Recent`, `ChatButton`, `ChatSettingPanel` | 3/44/44/44/4/187 | 27x27 @0,500 `Lowest1`; 400x300 @20,520 `Lowest2`; … | chat frame + tabs + settings (largest single HUD surface) |
| `TeamSwitchBtn` | 17 | 24x138 @300,300 | team/raid switch tab |
| `ComboPanel` | 22 | 471x182 @100,200 | combo counter art |
| `FullScreenWarning` | 6 | 0x0 @0,0 `Topmost2` | low-HP red-edge flash; opened by `DynamicCarrierBar.lua` (`ratio<=0.15`, see `proof/ui/evidence/battle_hud/sweep_findings.md`) |
| `ProgressBar`, `GeneralProgressBar`, `PQprogressbar` | 17 / 9 / 15 | PQ: 375x83 @300,300 | cast/generic/PQ progress bars — **native-driven**: `CASTINGBAR_START/END` has no Lua consumer (sweep_findings); render INI art only, engine host later |
| `ActionBar` / `ActionSmallBar` / `DynamicActionBar` | 17 / 5 / 40 | 1024x50 @0,100 `Lowest`; 190x58 @740,645; 965x75 @143,541 `Lowest1` | alternate action-bar surfaces (modes/pets/vehicles) |
| `BuffFold` / `TopBuff` | 23 / 5 | 300x296 @80,165; 14x0 @-10000,-10000 | foldable buff area / top-center buff strip |
| `AccelerateBall` | 57 | 366x108 @500,200 | dash/accelerate charge ball |
| `Announce` / `Balloon` / `Bullet` / `EnterAreaTip` | 10/10/12/9 | 800x125 @350,100 `Topmost2`; … | announcements, balloons, bullet screen, area-entry tip |
| `CDProcess` / `BuffMonitor`(+DaoZong/General/YaoZong) | 45 / 17 | 280x624 @800,100 | cooldown process list / buff monitors |
| `TargetTarget` / `TargetSkill` / `TargetFaceSet` / `TargetResourcebar` | 50/9/39/29 | 325x115 @500,124; … | target-of-target, target skill row, face set, target resource bar |

## 5. Native-driven elements (engine-host later)

- Cast bar `ProgressBar` and the generic progress bars are driven by native `CASTINGBAR_*` events with
  **no Lua consumer anywhere** in the 1,615-file Lua sweep (`proof/ui/evidence/battle_hud/`).
- Nameplates/damage numbers are engine-drawn (no KGUI INI surface found for them in this pass).
- Both classes: the INI art renders the layout; the animated behavior needs the engine host.

## 6. Viewer entries + engine changes (this session)

- Entries added to the 基础界面 stage: **2.1 `main-bar`**, **2.2 `player-frame`**, **2.3
  `target-frame`** (all `PARTIAL`, backdrop `#33393E`).
- Engine: `UiLayout` case 8 (PosType 8) now keeps an **explicitly authored/overridden `Left`** (even
  0) instead of parking the element at the window's right edge — the main bar's slot strip authors no
  `Left` on its first image and relies on item flow; the BR window-right validation still holds for
  elements without a `Left` key.
- Gap register: slot icons (runtime skill data), Player runtime values/buffs, TargetCommon overlay
  append (`AppendItemFromIni` multi-INI), ChatPanel rows, progress-bar behavior.

## Reproduce

```powershell
# extract the HUD group INIs/Lua (client PakV4, read-only)
.venv\Scripts\python.exe tools\netcode\extract_pak_paths.py --list <paths.txt> --out-dir <dir> --batch 60
# decompile (unluac)
java -jar unluac.jar <file.lua> > <file>.decompiled.lua
# render the trio
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --render main-bar --out mb.png --dump mb.txt
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --render player-frame --out pf.png
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --render target-frame --out tf.png
```

**Confidence:** composition/append chain HIGH (decompiled `Target.lua`); per-window structure HIGH
(INI); visual fidelity LOW until GT captures exist (authored state only, per user decision).

Last verified: 2026-10-03 (renders `mb_v4`/`pf_v2`/`tf_v2`; `--selftest` 20/0/0).
