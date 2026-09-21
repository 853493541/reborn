# B1 SMOKE — JX3 Ani Player (MINA MVP)

**Owner:** Thor  
**Date:** 2026-09-19 PT  
**App:** `/workspace/jx3-ani-player/` (`player.py` + `mina.py`)  
**Overall:** **PASS** (primary body clip + transport + stick viz). **WARN** on second sample (hand) — load fails (parser header misread).

## Environment

- Box display `DISPLAY=:11`
- `.venv/bin/python player.py` exercised via in-process `AniPlayer` (same code path as GUI)
- Headless Agg export of frame 10 for stick comparison

## Checklist

| # | Check | Result |
|---|--------|--------|
| 1 | Clips list shows `samples/*.mesh.ani` | **PASS** — 2 entries: `f1_2227_body_hd.mesh.ani`, `f1_2227_hand_hd.mesh.ani` |
| 2 | Load body MINA | **PASS** — 33 bones, 46 frames, mesh `f1_2227_body_hd.mesh`, 13 stick edges |
| 3 | Scrub to frame 10 | **PASS** |
| 4 | Play / Pause / Stop + Loop | **PASS** — play sets playing; pause clears; stop resets to frame 0 |
| 5 | Stick viz ~ matches `samples/b1_stick_f10.png` | **PASS** — thorax/limb stick + scatter; Z span ≈ −3…135 on f10 (same ballpark as Clint proof) |
| 6 | Load hand MINA | **FAIL / WARN** — `load_mina` reads nonsense `bones=14616 frames=11776` → `MemoryError` / UI “Load failed:” (hand layout ≠ body R2e offset heuristic) |

## Shots

| File | Notes |
|------|--------|
| `samples/b1_stick_f10.png` | Clint proof (reference) |
| `samples/thor_b1_stick_f10.png` | Thor Agg export f10 body |
| `samples/thor_b1_gui_f10.png` | Thor GUI canvas save f10 body |

## How to re-run

```bash
cd /workspace/jx3-ani-player
.venv/bin/python player.py
# or headless load smoke:
.venv/bin/python -c "from mina import load_mina; c=load_mina('samples/f1_2227_body_hd.mesh.ani'); print(c.bone_count, c.frame_count)"
```

## Notes for Clint / Tony

- B1 bar met for the **body** FbxCmd sample (the intended MVP path).
- Hand sample needs a follow-up parser ticket (different pad/marker after counts); not a transport/UI miss.
- No Pak / MovieEditor involved this ticket.
