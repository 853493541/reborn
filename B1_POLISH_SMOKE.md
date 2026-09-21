# B1-POLISH SMOKE — R4b chrome + body path

**Owner:** Thor  
**Date:** 2026-09-19 PT  
**App:** `/workspace/jx3-ani-player/player.py` (Natasha R4b polish)  
**Overall:** **PASS**  
**Hand follow-up:** see B1b_SMOKE.md (PASS 57×46)

## Accept bar

| # | Check | Result |
|---|--------|--------|
| 1 | Body chips F1/F2/M1/M2 present; F1 default selected (accent) | **PASS** |
| 2 | Character 角色 list inferred (2227 under F1) | **PASS** — 1 character |
| 3 | 动作 Clip list (body + hand basenames) | **PASS** — 2 clips |
| 4 | Accent **Play** (`#60CDFF`) | **PASS** |
| 5 | **Loop** default **on** | **PASS** |
| 6 | Body path: load body clip, scrub, play/pause/stop | **PASS** — 33 bones / 46 frames |
| 7 | Fluent dark shell tokens (`#202020` / `#2C2C2C`) | **PASS** (code + Natasha shell shot) |

## Shots

| File | Notes |
|------|--------|
| `samples/natasha_polish_shell.png` | Natasha before/after polish reference |
| `samples/thor_b1_polish_shell.png` | Thor smoke window grab (1100×720) |

## Re-run

```bash
cd /workspace/jx3-ani-player
DISPLAY=:11 .venv/bin/python player.py
```

## Notes

Hand clip load now PASS after Clint B1b parse fix (57×46).

## Follow-up B1b (same day)

Hand load re-smoked: **PASS** — `f1_2227_hand_hd.mesh.ani` = **57×46**; body still **33×46**. See `B1b_SMOKE.md`.
