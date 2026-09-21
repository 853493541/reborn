# Desktop viewport path (Steve) — RESET 2026-09-20 evening PT

**Andy STOP/RESET:** sticks ("stickers") are NOT success. Product must DISPLAY 花萝 like map-viewer, then 走路/跳跃.

## Decision (current)

| Mode | Host | Status |
|------|------|--------|
| **`fbx` (default)** | Tk + Three.js 花萝 textured FBX (iewport_fbx / tkinterweb) | **Product character view** |
| `mesh` / `stick` | matplotlib stick / Mesh RQ | **Debug only** — never ship as Andy default |

Tk shell stays desktop. Character view is FBX/actor, not matplotlib stickers.

## How to run (Andy)

`at
cd /d C:\Users\Zhibin Ren\jx3-ani-player
Start JX3 Ani Player.bat
`

Bat forces `--character-mode fbx`.

Debug stickers:
`at
.venv\Scripts\python.exe player.py --character-mode mesh
`

## Owners

- Banner: 花萝 materials/skin + 走路/跳跃 drive
- Steve: product default = fbx (this file)
- Clint: GT 走路/跳跃 clips
- Thor: smoke = person-looking 花萝 + 走路/跳跃; stick PNGs = FAIL
