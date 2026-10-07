# UI fidelity vs the real client — evidence assessment

**Question:** what have we verified against the **real client**, what is only self-assessed, and what
can't be measured yet? **Date:** 2026-10-04. Companion to `UI_SYSTEM_COVERAGE.md` (which is
implementation coverage; this doc is *evidence class*).

## 1. What "against the real client" can mean here

Locked rules allow: reading/copying the client install, disassembling the engine binaries, running
the extracted data, and **observing** the client — but no hijack/injection/memory reads and no packet
capture. So "real client" evidence comes in three usable classes:

1. **Client binary semantics** — disassembled `KGUIX64.dll` / client IL (proves *how* the engine
   behaves, e.g. the ImageType dispatch).
2. **Client shipped data** — the INIs, scripts (executed in our Lua VM), string tables, atlases
   (proves the *inputs*; running the scripts is executing client logic, not our notes).
3. **Real-client captures (GT)** — screenshots of the running client, for pixel fidelity.

There is **no GT capture for the basic-UI panels** (bag/character/mail/...); the available GT is the
绝境战场-era set. Fidelity for the basic UI is therefore **unmeasured** — implementation coverage and
evidence-class confidence are the only honest metrics until captures exist.

## 2. GT inventory (real-client captures in the repo)

| window | capture | validation history | recheck 2026-10-04 |
|---|---|---|---|
| queue panel (五人模式) | `proof/minimap/screenshots/Screenshot-given-1.png` (1720x1101) | matched 2026-09-29: per-region RGB fingerprint `proof/ui/evidence/queue_panel_gt_vs_app_fingerprint.txt` (title/tabs/score/currency/skill/crests/bottom deltas) | the GT-matched entry was replaced by the deliberate 真传模式 **design variant** (`zhenzhuan-queue`, marked DESIGN) — not re-measurable as client truth |
| minimap | `proof/minimap/screenshots/05_minimap_ingame_crop.png` (560x560) | matched 2026-10-02 (lens map/zoom from map config, `Wnd_Over` ring, per-page names) | re-rendered: 40 sections, `runtime=0`; partial replay no longer applied (see §3) |
| middlemap | `proof/minimap/screenshots/01_bigmap_middlemap.png` / `01b_..._night_...` (2048x1792) | matched 2026-09-29 (composite WorldMap behind, placement 27/43, mask/rows) | re-rendered: 52 sections, `runtime=0` |
| battlefield map | `proof/minimap/screenshots/06_battlefieldmap_ingame_crop.png` (790x680) | matched 2026-09-30 (five battlefield pages, mode gating) | re-rendered: 25 sections on page 296 |
| loading screens | map art + `proof/ui/evidence/loading_panel_*_render.png` | built from shipped map textures (not captures) | unchanged |
| ready-confirm / staging / loot / main-message-line / treasure-final / leave-menu | `proof/ui/evidence/*` (mode-era captures) | matched 2026-09-29..10-02 per EXPERIENCES | runtime=0 (no complete replay) → GT-matched overrides intact |

## 3. Recheck finding: partial replay broke GT-matched windows (fixed)

Applying *partial* replay state blindly was wrong: a replay that errors mid-init records `Hide`
mutations for sections the engine shows later. Rechecking the GT-matched set exposed it — the minimap
render collapsed from its full subtree to **5 sections**. Fix (committed): `ApplyRuntimeState` now
applies a window's state **only when `replay_summary.tsv` marks the replay OK**; partials fall back to
the authored/GT-matched state. After the fix: minimap 40 sections `runtime=0`, bigbagpanel 145
sections `runtime=284` (complete replay still applied).

## 4. Evidence-class matrix (system features)

| feature | strongest evidence | class |
|---|---|---|
| ImageType diced path (10/11/12/17-19), glass 16 | disassembled client `KGUIX64.dll` (RVA 0x117D7C dispatch) | **client binary** |
| PosType 0/1/2/6/7/8/9/10/11/12 semantics | client binary + GT-matched mode windows | client binary + GT |
| AnchorArgs/AnchorDst (root/client/relative) | client binary + mode windows | client binary |
| LSH / page chain / tabs (mode) | client scripts + GT captures | client data + GT |
| Script runtime state (79 windows) | **the client's own compiled scripts executed in a real Lua 5.1 VM** | client data (executed) |
| Textures / atlases / fonts | extracted from client paks; 98.5% of image instances resolve | client data |
| String tables | extracted; 84.6% of text instances resolve | client data |
| Basic-UI panel fidelity | — | **unmeasured (no GT)** |
| Page-set/list/tree semantics, PosType 3/4/5, HandleType 1/2/4/5, FirstItemPosType 1-9 | census counts only | **self-assessed** |
| Interaction | handler dispatch proven on the bag checkbox; viewer wiring not built | client data (partially) |
| Animations/SFX/3D scenes/web/native bars | out of viewer scope | not attempted |

## 5. Issue register (what the rechecks found)

**Fixed in this pass (6):**
1. Partial replay state broke GT-matched windows — minimap collapsed 5 sections; `ApplyRuntimeState`
   now applies only completed replays.
2. Coverage percentages were self-referential — `UI_SYSTEM_COVERAGE.md` §0 states they measure our
   tools, not the client.
3. Bag hand `adjust` (440-wide root/backgrounds) contradicted the script's 594x624 — removed.
4. A root `Hide` in a replay dropped the whole window — ignored.
5. Scripts failed without Lua 5.1 `module()` env chaining — fixed (39 → 78 replay OK).
6. Entry chain missed `OnCreate`/`Init`/`OnOpen` — added (no-entry 6 → 3).

**Open (counted):**
1. ~~Text: 3,254 unresolved string instances~~ **fixed**: 136 extracted module tables added to
   `Data/text/ui/Scheme/Case` (145 total) → **3,254 → 49** unresolved (the tail is dev/unreached
   tables: `STR_COLLECTION*`, `STR_TESTTEXT_TIME`, `STR_MICROT`, ... — each ×1-4).
2. Art: real missing placeholders **602 → 256** instances. Pair extraction of `.UITex` + `.Tga`
   siblings, the zero-size-frame rule (33 authored empty frames draw nothing as the engine does), and
   the engine-derived **v1 atlas layout fix** (below). Breakdown of the 256: **231 files absent from
   the scanned paks** (e.g. `mpbj.tga`, `Cloud.tga`), **16 authored Frames beyond the atlas** (e.g.
   `UI_LuckyPerson.UITex` has 5 frames, `Image_Num_5` asks frame 6), 9 misc. 278 `TextureName=no` are
   intentional.

   **UITex v1/v2 layout (engine truth, KGUIX64.dll `UI::KImageInfoMgr::LoadUITexFile` RVA 0xE9240):**
   the loader reads an 88-byte `UITEXFILEHEADER` (frame count @0x0C, animate count @0x10, version =
   header dword 0 >> 16), then n 20-byte `UITEXFRAMEDATASTRUCTURE` records, then the animate/group
   table (`u32 count; count × (frame, interval)`). **Version 1** files start the frames at offset 88
   (record = `flag,x,y,w,h`) so the group table sits at `88 + n*20` and the last frame shares its flag
   word with the table's first word; **version 2** files start at 92 (record = `x,y,w,h,flag`) with the
   table at `92 + n*20`. The viewer read every v1 atlas at the v2 offset, desyncing 111/131 v1 group
   tables (QuestPanelButton's `NormalGroup=3` fell back to the bogus authored `Frame=35`). Fixed
   2026-10-05; the 240/261 previously desynced v1 atlases now resolve their groups.
3. Constructs: PosType 3/4/5 = 70 · HandleType 1/2/4/5 = 137 · FirstItemPosType 1-9 = 98 ·
   approximate WndTypes = 868 sections (page-set/list/tree/scene/web/flex).
   **New lead (2026-10-04):** the client `KGUIX64.dll` exports the **Yoga layout API**
   (`YGNodeCalculateLayout`, `YGNodeStyleSet*`, `YGNodeLayoutGet*`) — so `WndFlexContainer`/`FlexHandle`
   are flexbox (Yoga) containers, not plain canvases. Implementing flex semantics against those
   exported functions is the evidence-backed path for the ~117 flex sections.
4. Partial replays: 43 windows (T-A: 23) — stub-tune by error class.
5. Out-of-bounds elements: 6,584 instances (mix of legitimate overhang and wrong sizes).
6. No-entry replays: 3 (Balloon, TradingSure, UISetting).
7. Shell windows: 136 (authored chrome, runtime content) — status-flagged, not errors.
8. Basic-UI fidelity: unmeasured (no GT) — capture wishlist in §6.
9. Interaction: dispatch core proven; viewer wiring not built.

## 5b. Full-flow check (2026-10-05): four questions, chain order

1. **Do we have the full UI system?** System definition: `ui/module_info.xml` = 1,016 modules /
   1,783 script entries (tracked). Locally: all 1,244 Default INIs (1,240 catalog windows render),
   but **scripts 314/1,783 (18%)** — 1,461 `.lua` missing (1,235 under `Config/Default`, 96
   `ui/Script`, 73 `scripts/Include/UIscript`, ...). Probe 2026-10-05: 5/5 sampled missing scripts
   (`CoinShop_Main.lua`, `HouseUpgrade.lua`, `module.lua`, `UIscriptDanmu.lua`,
   `TwoDimensionalLogin.lua`) extract from PakV4 by exact manifest path. **Answer: No** — the layout
   half is in the tree; the behavior half is not (extraction gap, not availability).
2. **Do we have full resources for each UI?** Art: 40,802 image instances, 534 placeholders
   (278 intentional `TextureName=no`; 256 real = 231 files + 16 frames beyond the atlas + 9 misc)
   → ~99.4%; text 49/3,303 unresolved (99.85%); fonts 5/5; all 1,121 scanned atlases parse (v1/v2
   fix). Re-probe 2026-10-05: `QuestPanelButton.UITex` + `.Tga` **do extract** (the 2026-10-04
   "not extractable by path today" claim is stale); `Cloud.tga`, `mpbj.tga` MISS at their authored
   paths. **Answer: mostly** — the 231-file tail is mixed (recoverable + genuinely absent) and needs
   a systematic re-probe.
3. **Does every UI fully decode/import correctly?** INI 1,240/1,240 load and render
   (`--selftest` 1240/0/0); scripts 138/143 replays OK (client bytecode in a real Lua 5.1 VM);
   constructs: 14 unhandled variants (305 occurrences — PosType 3/4/5 = 70, HandleType 1/2/4/5 =
   137, FirstItemPosType 1-9 = 98) + 868 sections under approximated WndTypes
   (page-set/list/tree/scene/flex) ≈ 1.3% of sections. **Answer: No** for that 1.3%; yes for the rest.
4. **Are we using everything correctly — wired up?** Runtime consumption: dropped calls 671 → 26
   (`tools/ui/runtime_gap_report.py`); A/B: 6 windows fuller than static, no collapse; guards
   documented. But runtime applies to 143 windows only, and **1,075 catalog windows have a manifest
   script that is not extracted at all**, so their behavior is not wired; constructs approximated;
   interaction partial; basic-UI fidelity unmeasured. **Answer: the wired part is correct
   (evidence-backed); coverage is the bottleneck.**

Chain-fix order: (1) extract the 1,461 missing scripts (probe-proven) → (2) replay batch over the
expanded corpus → (3) wire/verify runtime for the newly completed replays (A/B) → (4) systematic
art re-probe of the 231-file tail → (5) construct decode (Yoga/flex lead).

**Update (same day, steps 1-3 done):** extracted 1,420/1,469 missing scripts (PakV4 HIT log) →
manifest now **1,742/1,783 (97.7%)**; the 41 remaining are 22 `ui/Traits/mobilestreaming` (mobile
config root), 4 `mainbar_*`, ~15 CJK/corrupted manifest paths. `engine_base.txt` 181 → 207 manifest
base files. Replay batch: **1,148 OK / 63 ERR** of 1,211 scripted windows (from 138/7 of 143);
1,210/1,240 catalog windows now ship a script. A/B: 20 windows render fuller than static
(tongbaogift +752, creditspanel +112, charge +110, selectmacroiconpanel +64, buffmonitor +53, ...),
29 state-driven reductions, none collapses (guards hold). Selftest 1240/0/0; audit ph=538,
oob=8698.

## 6. Verdict
- **Client-binary-verified**: the draw/layout core we implemented (slicing, PosType set, anchors).
- **Client-data-verified**: inputs (INI/scripts/assets) and runtime state for the completed replays.
- **GT-verified**: the 绝境战场-era window set; the recheck confirmed they still hold after the
  runtime-state change (after the partial-skip fix).
- **Unmeasured**: every basic-UI panel's fidelity, and all interaction states. To move these, we need
  captures — the honest path is a capture session on the running client (user-driven; we may not
  drive/inject it), ideally of: bag (compact + normal + a category expanded), character (equipment +
  zhenjie pages), skill (kungfu/qixue), social, mail, auction, guild, system settings, and the same
  windows after a few clicks (tab switch, checkbox) to cover interaction states.

## 7. Reproduce

```powershell
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --render minimap --out mm.png
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --render middlemap --out mmap.png
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --render battlefield-map --out bf.png
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --render bigbagpanel --out bag.png
# GT: proof/minimap/screenshots/05_minimap_ingame_crop.png etc.
```

**Confidence:** GT inventory HIGH (files listed); recheck finding HIGH (minimap 5→40 sections
measured); evidence-class assignments HIGH for binary/data classes, MED for the "unmeasured" labels
(no GT exists).

Last verified: 2026-10-04 (minimap 40 sections `runtime=0`, bigbagpanel 145 sections `runtime=284`;
partial-skip fix committed).
