# Path C Phase 1 — Clint assist (launch + assets)

For @Steve engine-host spike. Do not wait on VFS notes below to smoke HD.

## Launch recipe (reuse R5c)

Primary ME root on Andy: C:\SeasunGame\MovieEditor

`powershell
# PASS — HD up, title Movie Editor (~2.7GB WS)
Start-Process -FilePath "C:\SeasunGame\MovieEditor\bin64\MovieEditorHD.exe" 
  -ArgumentList "NOTLAUCNER" 
  -WorkingDirectory "C:\SeasunGame\MovieEditor"
`

Also OK: MovieEditorLauncher.exe cwd=MovieEditor root → click tile **JX3 Movie Editor**.

**Do not** run MovieEditorHD.exe with no args (NOTLAUCNER guard → instant exit).

Alt root (same tree under client): C:\SeasunGame\Game\JX3\bin\zhcn_hd\MovieEditor\

Script copy from research: R5c_launch_unblock.md / start_movie_editor_hd.ps1 (if present under research tree).

## Candidate hosts (Phase 1 list)

| Binary | Role | Notes |
|--------|------|-------|
| in64\MovieEditorHD.exe NOTLAUCNER | Preferred | Resource Explorer / Ani play if Pak mounts |
| MovieEditorLauncher.exe | Front door | UI click needed for tile |
| QModelEditor | Alt preview | R5b: V5/Pak issues — prefer HD first |

## 蓄力 assets already on disk (for host load / compare)

| Role | Local path |
|------|------------|
| 蓄力 MIN2 | samples\player\moves\f1s07cj重剑技能15蓄力_奇穴.ani |
| 释放 MIN2 | samples\player\moves\fenglaiwushan\f1s07cj重剑技能15.ani |
| Logical (Pak) | data\source\player\f1\动作\f1s07cj重剑技能15蓄力_奇穴.ani |

If HD Resource Explorer needs game logical path, use Pak-mounted path above; local file is for offline drop / compare mid-frame.

## Exit for Steve

Mid-frame PNG: F1 body + 蓄力 looking like client → ping Tony PASS. Else hard blocker / No-Go Track 2.
