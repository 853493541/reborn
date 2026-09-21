# REPORT — MovieEditor SFX path (Phase X1)

**Date:** 2026-09-20 ~20:26 PT  
**Author:** Steve  
**Status:** **BLOCKER** on unattended mid-frame (ME shell launches; project/SFX load needs human or undiscovered IPC)

## Launch (works)

```powershell
$me = "C:\SeasunGame\Game\JX3\bin\zhcn_hd\MovieEditor"
Start-Process "$me\bin64\MovieEditorHD.exe" -ArgumentList "NOTLAUCNER" -WorkingDirectory $me
```

- Do **not** use `C:\SeasunGame\MovieEditor` as cwd alone → `编辑器初始化失败!`
- Extra `.kms` argv: process stays up; **no evidence** the plot auto-opens (empty gray viewport after wait).

Proof of empty shell after argv attempt: `proof/compare/sfx_x1_me_f1_shenmianfeng.png`

## Where real skill SFX live (ME / plot)

Official plot library (probe tree):

`C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4_probe\seasun\editortool\movieeditor\source\plot\actor\`

| Fact | Count / example |
|------|-----------------|
| F1 `.kms` plots | 89 |
| `.kms` containing `.pss` binds | **495** |
| Local 花萝 (no SFX) | `C:\SeasunGame\MovieEditor\source\花萝无动作.kms` (+ `.actor` / FBX) |

### Example F1 plot with skill PSS (recommended Andy open)

`...\plot\actor\经首道源岛\沈眠风海上偷袭\f1_沈眠风海上偷袭.kms` (~7.0 MB)

Embedded PSS paths (UTF-8 XML attrs `EPT_Action_Bind_BindMeshName`):

- `data\source\other\hd特效\技能\pss\发招\g_丐帮棍刀光02.pss`
- `data\source\other\新特效\技能\Pss\发招\D_刀光02.pss`
- `data\source\other\新特效\技能\Pss\发招\D_刀光蓝02.pss`
- `data\source\other\新特效\技能\Pss\状态\x_鲜血拖尾01.pss`
- `data\source\other\新特效\技能\Pss\被击\S_闪光烟雾02.pss`
- plus blood/qilang/dragon tint helpers (full list: `proof/f1_shenmianfeng_pss.txt`)

ME binary also exposes **AddSfxItem / 全局特效 / Particle System** (not automatable here).

## Blocker (same as Path C Phase 1)

Local-exec **cannot** drive ME UI:

- `SetCursorPos` returns false
- Synthetic keys / PostMessage menus don’t open Resource / timeline
- Win32 `GetMenu` count = 0 (custom `MainMenuBar.dll`)
- UIA under Movie Editor ≈ empty (DX viewport)

So X1 exit (**mid PNG with visible skill SFX**) needs either:

1. **Andy** (or human): File → Open the F1 plot above → play/scrub to a 刀光/血雾 beat → save ScreenShot, or  
2. Undiscovered cmdline/IPC to load `.kms` + playhead, or  
3. Fall through to **Track X2 client mid-SFX** (Thor) while ME path stays manual.

## Skill name for Thor (X2)

Prefer client GT for **风来吴山** (藏剑重剑技能15) — same skill family as prior companion work. Alternate clear FX: any 刀光 skill from the PSS list above.

## Decision ask (@Tony)

- **A)** Andy opens `f1_沈眠风海上偷袭.kms` once in the running HD → Steve grabs mid PNG → X1 PASS  
- **B)** Declare X1 blocked for automation; Thor client GT leads; ME host remains manual Track X4 later  

Map-viewer SFX remains **not GT** (per Andy).
