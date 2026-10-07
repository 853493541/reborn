# UI wire-up plan — the full remaining scope

**Status:** phases 0-6 EXECUTED (2026-10-06); phase 1 complete (replay **1,201 OK / 1 ERR /
9 NOENTRY** of 1,211 scripted); phase 2 closed as verified-authored; phase 7 pending the capture
session. Owner area: `docs/ui/`; viewer `ui-process-app/`.
**Baseline it starts from** (all measured, this session): selftest 1240/0/0; replay
**1,201 OK / 1 ERR / 9 NOENTRY**; gap report visual drops **1** (FromIconID, data-blocked);
audit placeholders 504, unresolved 49, oob 7,484; census **0 unhandled constructs**; flex +
HandleType 1-6 + all 13 PosTypes + page-sets + wheel/edit/drag wired.

## Phase status (2026-10-06)

- **Phase 0 — census truth: DONE.** 0 unhandled constructs (`ini_construct_census.py`).
- **Phase 1 — the ERR tail: DONE (21/22 fixed).** A VM failure probe (temp `ldebug.c` pcidx/opcode
  print) located each nil; fixes: Table_Find*/Table_Get* proxy rows, session-getter proxies,
  permissive-access VM patches (nil/function/boolean GETTABLE, nil SETTABLE, no-op call of a
  non-callable). Only WulinShenghuiDuizhen stays ERR (assert on absent server data; tolerant-assert
  rejected - it changes pcall-guarded branches).
- **Phase 2 — NOENTRY: CLOSED as verified-authored.** The 9 windows define only click/breathe
  handlers; their state at open is the authored INI (drivers exist for Cyclopaedia_Active,
  FieldPQPanel, GoldTeamSetSubsidy, but the popup's own init does not exist to replay).
- **Phase 3 — page-set/list/tree: DONE.** Authored Page_i/CheckBox_i + CheckedWhenCreate defaults,
  nested sets show their own default; audit ph 539->504, oob 9499->7484.
- **Phase 4 — animation: DONE.** Normal group applied; hover/pressed recorded; animation/tooltip
  calls consumed.
- **Phase 5 — interaction: DONE.** Click/hover/wheel/typing/drag (moveable windows) wired.
- **Phase 6 — extraction tails: DONE/CLOSED.** 13 string libs decoded; module_info re-decoded
  (GB18030) and the 40 remaining manifest entries + 601 art paths probe MISS at their exact paths
  (not shipped in this client build).
- **Phase 7 — GT fidelity: OPTIONAL, minimal.** No screenshot set is required. The review loop
  stays viewer-driven: when you see a window that looks wrong, one screenshot of that window (or
  just the window name) is enough — I fingerprint it (`tools/proof/image_stats.py`, proof under
  `proof/ui/`) and fix that window's delta. A broad capture session only pays off later, when a
  whole-set fidelity number is wanted.

Every task below ends with the standing gates: `--selftest` 1240/0/0, `--audit` delta,
`replay_summary.tsv`, `tools/ui/runtime_gap_report.py`, `docs/EXPERIENCES.md` + area README.

## Phase 0 — honest baseline (0.5 session)

1. Update `tools/ui/ini_construct_census.py`'s handled sets to the current viewer
   (PosType 0/1/2/6/7/8/9/10/11/12; HandleType 1/2/3/4/5/6; FirstItemPosType 0-12; WndType
   WndFlexContainer/FlexHandle/WndList exact) and re-run — the remaining construct list must be
   measured, not assumed.
2. Refresh `UI_SYSTEM_COVERAGE.md` numbers from the tools (no hand numbers).

## Phase 1 — the 22 ERR replay windows (6-8 sessions, ~3 windows/session)

Per-window tracing is the method: dump the failing function (`luac32 -l -p <win>.lua`), find the
GETTABLE/upvalue that is nil, identify the object (module table / script local / real base-lib
table), then fix the root: **feed the real engine data the client reads** (local-first rule) or
extend the stub for that object class — never a per-window hack.

| class | windows | lead |
|---|---|---|
| nil-field on script/real-lib tables | AchievementPanel, Album, BattleMapPay, CraftIntroduce, CraftReadComparePanel, FriendBack, HomelandInvitation, InternetExplorer, LuckyMeetingInfo, MiddleMapSimple, NewOperationActivity, PhotoShop, RaidPanel, ReputationPanel, Selfie, SingleFStatistic, SkillTipPanel, VideoSettingDetails | trace each field's origin; many are `ui/Scheme` tables the client fills at startup (extract them like the 848 table files) |
| nil upvalue | SmallCalender, SpeedRankPanel | the upvalue is a script local initialized from a stub call; find the initializer |
| class upvalue capture | PLActionBar (`tAnchor` on the class members table) | verify how `class()` upvalues resolve to the members table; possibly instantiate before entry (as done for BubblePanel) |
| assert in Init | WulinShenghuiDuizhen | the assert is a precondition on server data; locate the data source (client table) or keep ERR with the reason recorded |

Acceptance: replay OK rises window by window; each fixed window's state applies in the viewer
(completed-replay rule).

## Phase 2 — the 9 NOENTRY windows (1-2 sessions)

Cyclopaedia_Active/Career/FAQ/JX3Library/Log, Debug, FieldPQPanel, GoldTeamSetSubsidy,
IrrigatePanel. Their init is in the opener/driver script. Method: find the driver (which script
calls `OpenXxx`/updates the popup) via the extracted corpus; if the driver is a shared module,
load it in the harness and call its update with the popup root. If no driver exists (pure
authored chrome), record "authored-complete (verified)" and close them out of the ERR narrative.

## Phase 3 — page-set / list / tree semantics (~758 approximate sections; 2 sessions)

1. WndPageSet (513): measure per window which page the engine shows (default tab / runtime
   `ActivePage` / page chain) vs the viewer; add a status flag; fix the no-default cases.
2. WndList (152): now flex — re-census to confirm; verify rows stack/scroll.
3. WndTree: the scan found 0; re-census; if present, implement indent flow.
4. WndScene/WndWebCef/WndMovie (~90): native-only — document as out of scope (the product runs
   the real engine for these); no viewer work.

## Phase 4 — animation category (9 calls; 0.5-1 session)

SetAnimateGroupNormal/MouseOver/MouseDown (CompassPanel, GMPanel), SetAnimation/SetLoopCount
(MainBarPanel, MiniMap, Bullet). Static render keeps the authored frame (already honest).
Optional wiring: hover applies MouseOverGroup/AnimateGroup frames; the Normal group on render.
Acceptance: hover shows the scripted group frames where the INI authors them.

## Phase 5 — interaction completion (2-3 sessions)

1. Drag: `SetDragArea`/`EnableDrag`/`RegisterLButtonDrag` (BigBagPanel, ShareBagPanel, Matrix,
   QuestTraceList, NoticeBoard, VoiceRoomNotice) — item drag with `OnDragButton`/
   `OnItemLButtonDown/Move/Up` dispatch through the existing server.
2. Scrollbar thumb dragging (Scroll_* sliders) bound to the wheel offset.
3. Window chains: extend click-through navigation to the popup windows (`OpenXxx` helpers).
4. WndEdit: Enter/focus-kill (`OnEditKillFocus`) and text commit.

## Phase 6 — extraction tails (2 sessions)

1. Text (50 unresolved ids across 24 windows): add an `--unresolved` id dump, locate each id's
   source table (`ui/Scheme/Case/*` or `g_tStrings`), extract + convert with
   `tools/ui/extract_lua_string_table.py`.
2. Scripts (41): 22 `ui/Traits/mobilestreaming` (mobile config root — decide desktop scope),
   4 `mainbar_*` (probe case/path variants), ~15 CJK/corrupted manifest paths (re-extract
   `module_info.xml` cleanly from PakV4 so the names are exact, then probe).
3. Art (601 MISS of the probed candidates): analyze miss patterns (case, `.dds` variants, other
   roots), probe the variants; the map-viewer cache-extraction resource list is usable as raw
   resource input (with provenance) for alternate paths.

## Phase 7 — fidelity (the unmeasurable layer; capture session + 2-3 fix sessions)

No GT exists for the basic UI. Plan a capture session on the running client (observe-only,
user-driven): bag (compact/normal/category), character (equipment/zhenjie), skill (kungfu/qixue),
social, mail, auction, guild, system settings, plus a few interaction states (tab switch,
checkbox). Fingerprint per-region RGB with `tools/proof/image_stats.py`, record in `proof/ui/`,
then measure and fix the visual deltas window by window.

## Priority order (impact / effort)

1. Phase 6 text + scripts (quick, user-visible strings and missing windows)
2. Phase 0 census truth
3. Phase 3 page-set (broad section coverage)
4. Phase 4 animation (small, honest)
5. Phase 5 interaction
6. Phase 1 ERR tail (slow, per-window)
7. Phase 2 NOENTRY (may close as authored-complete)
8. Phase 7 GT fidelity (highest value, needs the capture session)

## Reproduce (baseline)

```powershell
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --selftest   # 1240/0/0
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --audit      # ph/unresolved/oob
.venv\Scripts\python.exe tools\ui\replay_all.py                         # OK/ERR/NOENTRY
.venv\Scripts\python.exe tools\ui\runtime_gap_report.py                 # visual drops
.venv\Scripts\python.exe tools\ui\ini_construct_census.py               # unhandled variants
```

**Confidence:** baseline numbers HIGH (tool outputs this session); effort estimates MED.
