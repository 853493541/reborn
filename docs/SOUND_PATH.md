# Sound path — 风来吴山 (FLWS) in MovieEditor

How MovieEditor plays a sound together with the tani, and how our engine host
gets the same sound.

## Full chain

```
tani: f1s07cj重剑技能15_风来吴山红色hd.tani
  SoundTag event (GBK string @ byte 0x888):
    skillremake/cangjian/chengyouyou/m2s07cjzhongjianjineng15_fenglaiwushanHD
  -> KG3D_AnimationSoundTag_Group_Data::LoadSound / LoadAllSound   (KG3D_AnimationTagX64.dll)
  -> KG3D_EngineEventManager::_OnProcessApplySoundTag              (KG3DEngineAdapterX64.dll)
  -> KG3DSoundCLR (Wwise)                                          (MovieEngineCLR.dll)
  -> Wwise event: skillremake_cangjian_chengyouyou_m2s07cjzhongjianjineng15_fenglaiwushanHD
     event id 3378728138
  -> bank: data/Wwiseaudio/GeneratedSoundBanks/Windows/Base/skillremake.bnk
  -> streamed WEM: data/Wwiseaudio/GeneratedSoundBanks/Windows/Base/161340541.wem
     source name: m2s07cjzhongjianjineng15_fenglaiwushanHD.wav
```

Config (`C:\SeasunGame\MovieEditor\config.ini`):

```
[WwiseSetting]
UseWwise = 1
BasePath = data/wwiseaudio/GeneratedSoundBanks/Windows
```

## The sound file

| Item | Value |
|---|---|
| Logical path | `data\Wwiseaudio\GeneratedSoundBanks\Windows\Base\161340541.wem` |
| WEM id | 161340541 |
| Source name | `m2s07cjzhongjianjineng15_fenglaiwushanHD.wav` |
| Format | RIFF/WAVE, wFormatTag 0xFFFF (Wwise Vorbis), mono, 44100 Hz, ~2.05 s |
| Size | 22,067 bytes |
| Bank | `skillremake.bnk` (217,884 bytes) |
| Init bank | `Init.bnk` (6,741 bytes) |

Local copy (gitignored, proprietary): `assets/sound/`.

## How to reproduce the extraction

`PakV4SfxExtract.exe` (in `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64`) with a
**GBK-encoded** pathlist and cwd = `bin64`:

```
data\Wwiseaudio\PreLoadBankList.txt
data\Wwiseaudio\GeneratedSoundBanks\Windows\Base\Init.bnk
data\Wwiseaudio\GeneratedSoundBanks\Windows\Base\skillremake.bnk
data\Wwiseaudio\GeneratedSoundBanks\Windows\Base\161340541.wem
```

Command:

```powershell
& "$client\bin64\PakV4SfxExtract.exe" paths.txt out_dir
```

(The bank list is `PreLoadBankList.txt`: 33 banks incl. `skillremake`, `CangJian`,
`JX3_Skill`, `jian3_skill_short`.)

## Event index (pre-existing research artifacts)

- `...\jx3-web-map-viewer\log\wwise-soundbank-index.json` — 230 banks / 23,579 events
- `...\jx3-web-map-viewer\log\anim-sound-index.json` — maps each tani to its sound event
- `...\jx3-web-map-viewer\cache-extraction\wwise-pak-extract\Windows\base\` — extracted banks/wems

## Host implementation

`engine_host_spike/SpikeHost.cs`:

- `sound = new KG3DSoundCLR(); sound.Init(startupPath, hwnd);` (enabled by default,
  `SPIKE_SOUND=0` to disable)
- `sound.FrameMove()` every frame in the render loop
- the engine then fires the SoundTag event automatically when the tani plays

If Wwise is not initialized, the tani still plays but silently. Wwise DLLs
(`KG3D_WwiseX64.dll`, `fmodex64.dll`) load from `bin64`; banks come from the VFS
using `BasePath` above.
