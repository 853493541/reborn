# Camera modes / animation / skill-FOV — status and blockers

**Date:** 2026-10-05 · **Branch:** `agent/camera-modes` (worktree `Desktop\reborn-iso-camera-modes`)
**Scope:** item 1.5 remainder — automatic per-mode switching, skill-move camera FOV,
`.mani`/KRLCameraAni camera tracks. Companion: `docs/camera/README.md`, `HOST_DEVIATIONS.md`.

## What the host has today

- Per-mode rows in `camera.json`/`CameraSystem` (character, sprint, carrier, air_combat,
  npc_dialog, god) with `SwitchMode` available; mode selection is driven by the
  `RC_CAM_MODE` test path or the operation-mode gating — no automatic triggers.
- Drag/follow/obstruction/shake/track (spring) all implemented (see the camera docs).

## 1. Automatic per-mode switching — BLOCKED (do not invent a trigger)

The WW (double-tap W) sprint trigger was **removed by user decision on 2026-09-30**
(`docs/EXPERIENCES.md` 2026-09-30: "per user decision the WW sprint trigger was removed
from the client ('for now'); the sprint camera row stays reachable only via the RC_CAM_MODE
test harness"; code comment at `client/RebornClient.cs` ~3012). The real gameplay triggers
(mount, dialog, air combat, spectate, engine Sprint state) **do not exist in the host** —
the engine `Sprint(true)`/skill 6754 state is unmodeled (M1 audit).

Re-open when: the engine state sources exist (sprint state from `KCharacter`, mount/glider
states, dialog/spectate modes) — then switch rows from those states, not from raw input.

## 2. Skill-move camera FOV — table decoded, BLOCKED on the skill runtime

`proof/netcode/camera_files/skill_move_camera.txt` (GBK, 9 lines = header + 8 rows):

| Column | Meaning |
|---|---|
| `SkillID` | skill id (rows: 3119, 20788, 21000, 25252, 100182, 124841, 200415) |
| `bAniTag` | animation-tag driven? (0/1) |
| 持续时间（毫秒） | duration ms |
| 持续时间（列持续） | hold ms |
| 最大旋转角速度（0~2PI）/固定角度（>=30°） | yaw rate or fixed angle |
| 保持时间 | keep time |
| 屏幕特效开关 / 边缘色（0~10） / 色域饱和度（0~1） | screen FX toggle / edge color / saturation |

The test skill (风来吴山, `f1s07cj重剑技能15`) has **no row** in this table. The consumer is
the SkillMove runtime (skill states 26/27, `REBORN_JUMP_FALL_SPEC.md` §3.10) which the
host does not implement yet. Re-open when the skill runtime + SkillID link land.

## 3. `.mani` / KRLCameraAni tracks — surface found, loader still open

Managed editor API (`MovieEngineCLR.KGMovieEditorCLR`, metadata dump): `ExportCameraTrack(string,int,int,int)`,
`SetCameraTrackPlaySpeedPerMS(float)`, `SetCameraTrackPlayMethod(uint)`, `SetTrackForEditor(int)`
— these are **editor playback controls**, not a map-camera-animation loader.

Next probe: find the `.mani`/KRLCameraAni consumer in the game client
(`JX3RepresentX64`/`KG3DEngineDX11EX64`), extract a sample `.mani`, decode its header;
only then decide whether a host playback path exists.

## Reproduce

```powershell
# table inventory
Get-Content proof\netcode\camera_files\skill_move_camera.txt -TotalCount 10
# managed surface
dotnet fsi tools\dump_managed_api.fsx > out.txt ; Select-String out.txt -Pattern 'CameraTrack'
```

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| auto-switch trigger intentionally removed (user decision) | HIGH | EXPERIENCES 2026-09-30 + code comment |
| skill-move table decoded; no row for the test skill | HIGH | `skill_move_camera.txt` |
| camera-track managed surface is editor-only | MED | metadata dump (no load/play-by-name method) |
