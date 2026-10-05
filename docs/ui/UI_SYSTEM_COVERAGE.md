# UI rendering system — coverage audit vs the official client

**Question:** how much of the official client's UI rendering system does the viewer reproduce?
**Date:** 2026-10-04. **Method:** census every shipped window INI, audit every render, replay every
window script, then measure each axis of the client's system. Tools/commands in §6.

## 1. What the official UI rendering system is

The client's UI = **KGUI engine** (layout/render, `KGUIX64.dll`) + **window scripts** (Lua 5.1
bytecode, run by the engine's Lua VM) + **assets** (atlases/`.UITex`/string tables/fonts) + the
**native-driven** surfaces (progress bars, nameplates) and 3D/web/scene windows. A window's runtime
appearance = authored INI prototype → script mutations (size/pos/visibility/text/frame/list rows) →
engine draw (slicing/masks/anchors).

## 2. Coverage by axis

| axis | measurement | coverage | evidence |
|---|---|---|---|
| **Window catalog** | all local `ui/Config/Default` INIs extracted and rendered | **100%** (1,240/1,240) | `--selftest` 1240/0/0 |
| **KGUI constructs** (weighted by section usage) | WndType 112,453/113,321 exact + 868 approximate; PosType 85,210/85,280; ImageType 18,104/18,104; HandleType 26,302/26,439; FirstItemPosType 23,954/24,052; AnchorDst 1,674/1,674 (mechanism) | **≈99.7%** | `tools/ui/ini_construct_census.py` |
| **Art (image instances)** | 40,802 Image sections; 899 placeholder instances of which 278 are the intentional `TextureName=no` (runtime-assigned) → ~602 real missing | **≈98.5%** | `--audit`; `ui-process-app/assets/uitex` = 2,479 files |
| **Text (text instances)** | 21,191 Text sections; 3,254 unresolved string ids (2,911 unique — a long tail of module tables not extracted) | **≈84.6%** | `--audit` (top ids: `STR_MEN` ×31, `STR_POINTSGET1` ×10) |
| **Runtime state (scripted windows)** | 122/1,240 windows ship a script (9.8%); replay 79 full + 43 partial; the viewer consumes the state (`ApplyRuntimeState`) | **65% of scripted windows fully** (6.4% of all windows); T-A (liked+recommended, 59 scripted): 35 full / 23 partial | `replay_summary.tsv`, `--render` `runtime=N` |
| **Interaction / behavior** | events, hover/checked transitions, animations (`Animate`/`SFX`), 3D scenes (`WndScene`), web (`WndWebCef`), native bars | **0% in the viewer** (static review render); out of scope for the viewer — the product runs the real engine for these | census types; `UI_RUNTIME_REPLAY.md` |

**Per-construct gaps (counts):** PosType 3/4/5 = 70 · HandleType 1/2/4/5 = 137 · FirstItemPosType
1-9 = 98 · approximate WndTypes = 868 sections (WndPage/WndPageSet 513, list/tree 152, scene/web/
movie ~90, FlexHandle/FlexContainer ~110). AnchorDst: 848 special (root/client) + 826 relative-name
(all resolved by name; unresolvable fall back to the parent).

**Runtime coverage by tier** (scripted windows): T-A 59 → 35 OK / 23 partial / 1 no-entry; T-B 27 →
18 / 7 / 2; T-C 36 → 26 / 10 / 0. Top recorded states: BigBagPanel 793, Player 179, TopMenu 106,
MailPanel 101, SoundSettingPanel 93, MiniMap 92, SocialPanel 91, MainBarPanel 89.

## 3. Verdict

- **Static layout + art: ≈98–99%** of the client's system (constructs 99.7% weighted, art 98.5% of
  image instances). The visible remaining layout gap is small and enumerated above.
- **Text: ≈85%** — the weakest asset axis; ~2,900 unique unresolved ids need their module string
  tables extracted (mostly per-module `ui/Scheme/Case/*.txt` not listed in the INI's `StringTable=`).
- **Runtime state: 65% of scripted windows fully replayed**; the rest carry partial state. Windows
  without a script (1,118) are authored-complete and don't need replay.
- **Behavior/interaction: 0% in the viewer** — deliberately out of scope; the product's UI runs in
  the real engine where events/animation/3D are native.

**Aggregate:** for the viewer's purpose (a faithful *static* review render) the system is **~95%**
overall — high-90s layout/art, mid-80s text, ~65% runtime replay of the scripted subset. The path to
higher numbers is concrete: (1) extract the remaining module string tables, (2) stub-tune the 43
partial replays, (3) close the census list (PosType 3/4/5, HandleType 1/2/4/5, FirstItemPosType 1-9,
page-set/list/tree semantics).

## 4. Gap list (prioritized)

1. **String tables** — extract per-module `ui/Scheme/Case/*.txt` for the ~2,900 unresolved ids
   (largest single fidelity gain; text is in every window).
2. **Partial replays** — 43 scripts; error classes: index-a-number (container fields), table-vs-number
   comparisons, nil arithmetic, for-limit (method-as-value). Class fixes flip several windows each.
3. **KGUI conformance** — PosType 3/4/5 (70), HandleType 1/2/4/5 (137), FirstItemPosType 1-9 (98),
   WndPage/WndPageSet tab flow (513), WndList/tree (152), scene/web surfaces (~90).
4. **Real missing art** — ~444 unique (section, texture) pairs; extend the extraction to the
   remaining atlas frames (non-ASCII names, unreferenced-but-needed frames).

## 5. Out of scope for the viewer (by design)

Events/interaction, hover/checked animation, SFX/Animate playback, WndScene 3D content, WndWebCef
pages, native-driven progress bars/nameplates — these run in the real engine in the product; the
viewer renders their authored/runtime *state*, not their behavior.

## 6. Reproduce

```powershell
# construct census (per-axis usage vs viewer coverage)
.venv\Scripts\python.exe tools\ui\ini_construct_census.py
# render audit: placeholders / unresolved strings / out-of-bounds per window
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --audit
# status scan: shells / runtime hosts / no-default pages
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --status
# script replay coverage
.venv\Scripts\python.exe tools\ui\replay_all.py
# gate
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 1240/0/0
```

**Confidence:** catalog/constructs/art HIGH (measured over all 1,240 windows); text HIGH (audit
counts; the unique-id split is approximate due to the 40-entry listing cap); runtime HIGH (replay
summary); behavior scope statement HIGH.

Last verified: 2026-10-04 (`--audit` placeholders=899 unresolved=3254 outOfBounds=6584;
`--status` placed=1240 shell=136 runtime-hosts=615; replay 79 OK/43 partial; selftest 1240/0/0).
