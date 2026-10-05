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

## 5. Verdict

- **Client-binary-verified**: the draw/layout core we implemented (slicing, PosType set, anchors).
- **Client-data-verified**: inputs (INI/scripts/assets) and runtime state for the completed replays.
- **GT-verified**: the 绝境战场-era window set; the recheck confirmed they still hold after the
  runtime-state change (after the partial-skip fix).
- **Unmeasured**: every basic-UI panel's fidelity, and all interaction states. To move these, we need
  captures — the honest path is a capture session on the running client (user-driven; we may not
  drive/inject it), ideally of: bag (compact + normal + a category expanded), character (equipment +
  zhenjie pages), skill (kungfu/qixue), social, mail, auction, guild, system settings, and the same
  windows after a few clicks (tab switch, checkbox) to cover interaction states.

## 6. Reproduce

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
