# MovieEditor SFX ground-truth record

Status: data-path verified; visual capture still requires one manual MovieEditorHD interaction.

## Reproducible source

MovieEditorHD launches successfully only with the client-side working directory:

```powershell
$me = "C:\SeasunGame\Game\JX3\bin\zhcn_hd\MovieEditor"
Start-Process "$me\bin64\MovieEditorHD.exe" `
  -ArgumentList "NOTLAUCNER" `
  -WorkingDirectory $me
```

Recommended F1 plot:

```text
C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4_probe\seasun\editortool\movieeditor\source\plot\actor\经首道源岛\沈眠风海上偷袭\f1_沈眠风海上偷袭.kms
```

Static plot inspection found real PSS bindings including刀光, blood, smoke, and trail effects. The FLWS companion sample uses:

```text
TANI: data\source\player\f1\动作\F1s07cj重剑技能15_风来吴山_悟.tani
ANI:  data\source\player\f1\动作\f1s07cj重剑技能15.ani
PSS:  data\source\other\hd特效\技能\pss\发招\c_藏剑_风来吴山_刀光01_悟.pss
```

Measured timeline data:

```text
PSS start:    2000ms
PSS duration: 5000ms
timeline:     8000ms
```

The extracted PSS is a real `PAR` asset, 156,769 bytes, with 15 sprite emitters, 15 mesh emitters, and 21 texture dependencies in the cache analyzer.

## Capture gap

Passing `.kms` as an additional command-line argument leaves MovieEditorHD at an empty gray viewport. The custom DX UI does not expose a usable UI Automation tree, and local synthetic cursor/key input cannot open the project or timeline. Consequently, this record does not claim a MovieEditor screenshot as visual proof.

The companion smoke is separately recorded in `SFX_RUNTIME_STATUS.md`; its screenshot must not be labeled MovieEditor parity until the manual plot-open/play/scrub step is completed.
