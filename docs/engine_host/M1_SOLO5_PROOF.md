# M1.7 — HUD overlay + five-minute solo run (proof)

**Date:** 2026-10-02
**Build:** `reborn_client_m1-final.exe` (git `97f0ce6` + HUD worktree, title `sandbox-m1-final`)
**Worktree:** `Desktop\reborn-iso-m1-final` (branch `agent/m1-final`)
**Driver:** `tools/proof/run_solo5min.ps1` (posted keys + external window captures)

## What was wrong

The engine renders into a child window of the host form, so the old WinForms `Label`
HUD sat *behind* the 3D output (invisible in every capture). `client/HudOverlay.cs`
replaces it with a separate top-level **layered** window owned by the host form:

- `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`
  (per-pixel alpha via `UpdateLayeredWindow`, click-through so input still reaches
  the game, never activates),
- positioned over the viewport at the form origin + (10,10) each HUD update,
- the old clickable "I" toggle is now the **I** key (info box starts collapsed).

Verified live (window enumeration of the running client):

```
hwnd=0x4A0824 class=WindowsForms10... ex=0x080900A0 [LAYERED,TRANSPARENT,TOOLWINDOW,NOACTIVATE]
             rect=(660,354) 1510x310        <- overlay, info expanded after "I"
hwnd=0x2E08D8 class=WindowsForms10... ex=0x00050100  rect=(624,272) 2592x1518   <- main form
```

## Five-minute solo run

`RC_DEMO=0 RC_AUTORUN=330000`; driver posts `I` (HUD), holds `W` (run, 320 u/s) from
t=36 s, alternates `Space` (jump) / `1` (skill) every 25 s, and saves an external
window capture every 30 s (the engine's own screenshot path renders the 3D scene only,
so the HUD needs an external capture).

Log facts (`reborn_out\reborn_20261002_141253.log`, t-lines 158):

| Metric | Value |
|---|---|
| Session length | 316 s logged (driver 320 s), client alive, no crash |
| Movement | start (23334,24224) -> end (11353,14303); path span **15555 u = 243 尺**; run 320 u/s |
| Jump/fall | **18** jump-clip switches (`f1b02yd小跳b.ani` -> `小跳c.ani` -> `奔跑.ani`), 6 airborne frames |
| Skill | **5** casts (`f1s07cj重剑技能15_风来吴山红色hd.tani`) |
| Collision | colCalls 70035, colBlocked 46755 (blocked against terrain/objects while running) |
| fps | min 131 / max 287 / avg 221 |

Captures (`reborn_out\capture_solo5c_*`, external window captures incl. the overlay),
sha256 (16 hex) and HUD-region white-pixel count (region 20,20-960,420):

| # | t (ms) | sha256 | hud white | note |
|---|---|---|---|---|
| 0 | 45028 | `a8b05c6774ba8759` | 18625 | scene + HUD |
| 1 | 75050 | `3b6712180c325792` | 10882 | scene + HUD |
| 2 | 105083 | `a776b5e522992634` | 14094 | scene + HUD |
| 3 | 135145 | `e82bfc0a5d2718cb` | 3495 | scene + HUD |
| 4 | 165217 | `6b797fb80c2a8d77` | 372855 | **white anomaly** (foreground race; not cited) |
| 5 | 195231 | `51fb0c878eb463fc` | 6017 | scene + HUD |
| 6 | 225315 | `6ba08df258d4ddd3` | 47044 | scene + HUD |
| 7 | 255365 | `69af5acd7b6d6a68` | 14731 | scene + HUD |
| 8 | 285377 | `b5f4715d1ae143f6` | 372855 | **white anomaly** (foreground race; not cited) |
| 9 | 315431 | `e6420aa71d1ce4ed` | 8213 | scene + HUD |

All ten captures have distinct fingerprints (scene evolves); eight show the 3D scene
with the HUD overlay text present; two are all-white captures where the foreground
race lost to another window (kept for the record, not used as scene evidence).

## M1 exit

One 花萝, real 龙门寻宝 map, animated, walks/runs/jumps/falls with real game values
(walk 96 / run 320 u/s, gravity -1289, jump apex 191 u per `REBORN_JUMP_FALL_SPEC.md`),
one skill casts with animation + SFX, follow camera, **stable 5 minutes** — met.

M1.3 (exact integer 15 Hz movement model port) remains the M2 prerequisite
(prediction parity); the continuous model reproduces the same table values and is
what this proof exercises.

## Reproduce

```powershell
# build the feature client (worktree root)
$env:RC_CLIENT_EXE = "reborn_client_m1-final.exe"; client\build_client.cmd
# five-minute solo proof (~5.5 min; posts I/W/Space/1 and captures every 30 s)
powershell -ExecutionPolicy Bypass -File tools\proof\run_solo5min.ps1 `
    -Exe C:\SeasunGame\MovieEditor\bin64\reborn_client_m1-final.exe `
    -OutDir C:\SeasunGame\MovieEditor\bin64\reborn_out -Tag solo5c
# fingerprints (no image attachments)
.venv\Scripts\python.exe tools\proof\image_stats.py C:\SeasunGame\MovieEditor\bin64\reborn_out\capture_solo5c_*.png --grid 4x4
```

Note: `reborn_out` is shared by concurrent clients — select the log whose first lines
carry `build=reborn_client_m1-final.exe`.
