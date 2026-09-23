# M1 status — one player on the real map

**Date:** 2026-09-23
**Product:** `client/RebornClient.cs` -> `C:\SeasunGame\MovieEditor\bin64\reborn_client.exe`
**Proof:** `C:\SeasunGame\MovieEditor\bin64\reborn_out\` (PNG + `reborn.log`)

## Done

| Item | Evidence |
|---|---|
| M1.2 animated character on a real map (gate) | `docs/M1_ACTOR_ON_MAP.md`, `actor_map_out/*.png` |
| M1.1 client scaffold: 龙门寻宝 + terrain sampler + animated dummy player + follow camera + HUD stub | `reborn_out/rc_*.png`, `reborn.log` |
| Movement: walk 200 / run 667 / gravity -1289 / jump 703, slope blocking, ledge fall | demo log: walk z 201->1010, run ->3236, jump clips, strafe blocked=True |
| M1.4 clip state machine (idle/walk/run/jump/fall) with real VFS clips | log clip switches; walk/skill screenshots |
| M1.5 input (WASD, Shift, Space, 1) + mouse orbit + wheel zoom + follow camera | `RC_DEMO=1` scripted run |
| M1.6 one skill cast: FLWS tani + blade/ring SFX | `reborn_out/rc_04_20000ms.png` |

Clips used (VFS, F1):
- idle `data\source\player\f1\动作\f1b01ty普通待机01.ani`
- walk `...\f1b02yd行走.ani`
- run `...\f1b02yd奔跑.ani`
- jump `...\f1b02yd小跳b.ani`
- fall `...\f1b02yd小跳c.ani` (placeholder until the real fall clip is mapped)
- skill `...\f1s07cj重剑技能15_风来吴山红色hd.tani`

## Build / run

```
client\build_client.cmd
# cwd = C:\SeasunGame\MovieEditor
reborn_client.exe            # RC_AUTORUN=0 -> run until window closed
RC_DEMO=1 RC_AUTORUN=22000   # scripted walk/run/jump/strafe/skill
```

Env: `RC_MAP`, `RC_SPAWN`, `RC_AUTORUN`, `RC_SHOTS`, `RC_CLIP_*`, `RC_SKILL_MS`,
`RC_YAW_OFFSET`, `RC_SCALE`, `RC_DEMO`.

## Remaining M1

1. **M1.3 shared movement model** — port the exact integer jump/gravity/fall model
   (`docs/REBORN_JUMP_FALL_SPEC.md`, `tools/gravity/verify_model.py`) to C#; current
   movement is the continuous approximation from the map host.
2. **M1.7 HUD + 5-min proof** — the WinForms label is hidden behind the engine output
   window; use an overlay (separate top-level transparent form or engine-side draw).
   Then a 5-minute solo run with screenshots/log.
3. Refinements: real fall clip, camera orbit verification, yaw calibration
   (`RC_YAW_OFFSET`), walk/run animation speed vs ground speed.

## Notes

- Repositioning uses `AddDummyModel` with the same name (same handle, animation
  continues); only when position/facing changed.
- FPS in the demo run: 230-340 with one animated character.
- No object collision yet (terrain slope blocking only); `FoliageCollision.cs` can be
  copied in next.
