# Path C Phase 1 — Engine-host spike (Steve)

**Date:** 2026-09-20 ~19:50 PT  
**Exit asked:** Seasun tool mid-frame PNG of F1 body + 蓄力 looking like client → Go/No-Go Track 1 vs 2.

## Launch results

| Recipe | Result |
|--------|--------|
| `C:\SeasunGame\MovieEditor\bin64\MovieEditorHD.exe NOTLAUCNER` cwd=that root (Clint assist script) | **FAIL** — `#32770` dialog: 编辑器初始化失败! |
| `C:\SeasunGame\Game\JX3\bin\zhcn_hd\MovieEditor\bin64\MovieEditorHD.exe NOTLAUCNER` cwd=`...\zhcn_hd\MovieEditor` | **PASS** — window title Movie Editor / 动画编辑器, ~2.7GB WS, empty gray viewport |
| Extra `.kms` argv | Process up; no evidence project auto-loaded |

**Correct launch for Andy:**

```powershell
$me = "C:\SeasunGame\Game\JX3\bin\zhcn_hd\MovieEditor"
Start-Process -FilePath "$me\bin64\MovieEditorHD.exe" -ArgumentList "NOTLAUCNER" -WorkingDirectory $me
```

## Host capability (static)

Binary exposes ResourceExplorer, AutoLoadAnimation, `*.ani` scan, AddActor, ImportAni-from-Fbx, `.kms` plots — so **HD can theoretically** host body+ani. HelperModel is gizmos only; AutoSave has `unKnow-0.kms` (unknown content).

## Blocker — cannot complete mid-frame smoke unattended

Local-exec on Andy **cannot drive the HD UI**:

- `SetCursorPos` returns **false** (cursor stuck)
- `SendKeys` / synthetic mouse do not open File/Resource Explorer
- UIA tree under Movie Editor is effectively empty (DX viewport / custom chrome)

So: **shell up = yes; load F1 + play 蓄力 = needs human click (or undiscovered IPC).**

Proof of empty shell: `proof/compare/path_c_phase1_hd_shell.png` (+ `_zh.png`).

## Go / No-Go (Steve recommendation)

| Track | Verdict |
|-------|---------|
| **Track 1 host** | **Conditional No-Go for automation** — HD launches and is the right Seasun host, but Phase 1 exit (mid PNG) needs Andy (or IPC) to load F1+蓄力 once. Not a reliable app backend without that. |
| **Track 2** | **Go if Track 1 stays manual** — VFS F1 mesh+skel + our viewer (Clint notes ready). |

**Ask Tony:** (A) Andy manually loads F1 + `f1s07cj重剑技能15蓄力_奇穴.ani` in the open HD for one mid PNG → Track 1 Go, or (B) accept Track 2 as primary now.

## Non-goals touched

Did not regress Path A loco. No FBX retarget work.