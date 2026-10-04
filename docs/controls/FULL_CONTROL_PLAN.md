# Full control plan — all 286 default bindings, subject by subject

**Status basis:** `CLASSIC_CONTROLS_AUDIT.md` §5b (18/286 handled),
`CONTROL_MODES_TRACEABILITY.md` (decode matrix), client
`client/HotkeyTable.cs` (loads/matches all 286 rows).

## 1. Scope and ground rules

- Input truth = shipped `ui/hotkey/default.txt` + `bindings.ini` (+ per-role
  `hotkey_newlast.txt` overrides, format `name \t context \t index \t key`,
  index 1/2, empty key = unbound; `RESEARCH_RESOLVED_GAPS.md` §1).
- Every command maps to its decoded handler chain (Lua → C binding → engine)
  where it has one; unmatched commands are counted, never guessed.
- Server-owned actions (casts, loot) stay intent-only in the client (M2).

## 2. Coverage map and dependencies

| Group | Rows | Dependency to apply |
|---|---|---|
| Movement + camera + base (done) | 18 | — |
| Input core (contexts, overrides, repeat) | — | this plan P0 (no new system) |
| Targeting | 10 | targeting system (Tab cone/LOS, F1–F5, target-of-target) |
| Action bars (static 78 + dynamic 28 + battle/vk ~18) | ~124 | action-bar system + server cast intent (C7/C9, M2) |
| UI panels | 60 | UI panel subject (C13; ~253 panel scripts) |
| Stance/talent/skill-direction/NPC/misc | 82 | stance (KUNG_FU_*) + skill system + NPC morph/summon bars + capture |
| Rogue/BR + minigame contexts | 10 | context systems + their bars |

## 3. Phases

- **P0 — input core completion (this change).** Context-aware matching
  (`Match(vk,…,context)`; only the active context's rows fire, so
  `MINIGAME_JUMP` cannot alias `MOVEFORWARD`), per-role override loading
  (`hotkey_newlast.txt` from `RC_HOTKEY_DIR`), `RC_HOTKEY_CTX` test switch,
  periodic coverage telemetry (`unhandledCmd`/`lastUnhandled`/event count).
  Verify: smoke ALL PASS; a `RC_HOTKEY_CTX=minigame` demo run must stop
  movement (W resolves to `MINIGAME_JUMP`, unhandled) with `dist=0`; a normal
  run keeps the movement fingerprints.
- **P1 — targeting (10).** Tab cone/LOS selection, F1–F5 self/team, attack
  target, target-of-target; unlocks click-select (S7) and follow/interact.
- **P2 — action bars (~124).** Assignment/pages/dynamic bars from the decoded
  storage (`RESEARCH_RESOLVED_GAPS.md` §6) + cast intent path; battle/VK bars
  as bar variants. Depends on P1 for target-grounded casts.
- **P3 — UI panels (60).** Panel toggles/layout from `custom.dat` and the
  decoded window-state keys; no invented anchors.
- **P4 — stance/talent/skill-direction/NPC/capture (82).** `KUNG_FU_*` stance
  switching, `TALENT_SET1-5`, skill-cast direction keys, NPC morph/summon
  bars, screenshot/kinscope; depends on P2 primitive (bar slot activation).
- **P5 — contexts (10).** Rogue/BR and minigame binding contexts with their
  bars; P0's context plumbing is the prerequisite.

## 4. Verification per phase

- Offline: extend the hotkey join (`proof/controls/hotkey_coverage.txt`) so a
  phase cannot regress the handled set; smoke ALL PASS each build.
- Engine: scripted `RC_*` runs with numeric fingerprints (position/yaw/
  selection/clip), logs attributed by build fingerprint.
- Definition of done per phase: area README index updated, EXPERIENCES entry,
  coverage count updated.

## 5. Evidence

`ui_hotkey_default.txt`, `ui_hotkey_bindings.ini`,
`hotkey_default_decoded.tsv`, `hotkey_coverage.txt`,
`CONTROL_MODES_LUA_ANNEX.md`, `CONTROL_MODES_P3_STATIC.md`,
`RESEARCH_RESOLVED_GAPS.md`, `CONTROLS_GAP_REGISTER.md`.

Last verified: 2026-10-02.
