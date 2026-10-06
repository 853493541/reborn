# Native audio probe — why the tani SoundTag is silent (1.6)

**Date:** 2026-10-05 · **Branch:** `agent/audio-native` (worktree `Desktop\reborn-iso-audio-native`)
**Scope:** item 1.6 — find where the engine's tani SoundTag chain stops in the host,
with in-process instrumentation (supersedes the Frida-only evidence in `SOUND_PATH.md`).

## 1. Architecture recovered (read-only recon)

| Layer | Finding | Evidence |
|---|---|---|
| Managed API | `KG3DSoundCLR` exposes only `Init(string,long)`, `UnInit()`, `FrameMove()` (no bank/event calls) | reflection dump of `MovieEngineCLR.dll` |
| Host shell | `KG3DSoundCLR.Init` loads **`KG3DSoundX64.dll`** (editor shell, export `Create3DSoundShell`, RVA 0xDB20) | `MovieEngineCLR.dll` strings (`KG3DSoundX64.dll`, `Create3DSoundShell`) |
| Game shell | `KG3DWwiseSoundX64.dll` is a thin shell (`Create3DSoundShell` + `PlayPcmSound`), delegates to `KG3D_WwiseX64.dll`; **not present in MovieEditor** | exports dump; shell strings (`GetWwiseManager`, `use wwise soundshell`) |
| Editor shell | dual FMOD + Wwise; strings `EnableWwise`, `WwiseSetting`, `KG3D_WwiseX64.dll`, `GetWwiseManager` | `KG3DSoundX64.dll` strings |
| Wwise engine | `KG3D_WwiseX64.dll` is the full Wwise 2021.1 SDK (145k strings; exports `AK::SoundEngine::PostEvent` x3, `LoadBank` x6, `GetWwiseManager` RVA 0xD130) | export table |
| Config | `MovieEditor\config.ini` `[WwiseSetting] UseWwise=1`, `IsProfile=0`, `BasePath=data/wwiseaudio/GeneratedSoundBanks/Windows` | file read |
| Tag pipeline | adapter contains `KG3D_EngineEventManager::_OnProcessApplySoundTag` (+V2/V4/motion/loop) and the model gate `KG3DModel::EnableSfxSoundTag` (fn @ `0x1800BA760`) | adapter strings + xref |

In-host module dump (`RC_SOUND_DBG=1`): `KG3DSoundX64.dll`, `fmodex64.dll`,
`fmod_event64.dll`, **`KG3D_WwiseX64.dll`** all load; `KG3DSoundCLR.m_pSoundShell`
non-null. The native setup is therefore complete at init.

## 2. Instrumentation

`native/sound_probe.cpp` + `native/build_sound_probe.cmd` (output
`bin64\sound_probe.dll`, loaded only by `RC_SOUND_HOOK=1` clients):

- inline hooks (12-byte `mov rax,detour; jmp rax`) on `AK::SoundEngine::PostEvent`
  id/ANSI/wchar overloads and `LoadBank(wchar)`; stolen prologues are version-guarded
  (15/16 bytes) and forwarded through trampolines; detours tail-jump (verified in the
  `/FAs` listing: `rex_jmp [g_tPostEventId]`).
- every call is appended to `bin64\reborn_out\sound_probe.log`.

Client loader: `RC_SOUND_HOOK=1` (see `RebornClient.SoundProbe`).

## 3. Result (HIGH)

Run `reborn_20261005_183218.log` (`reborn_client_audionative.exe`, nodb, `RC_DEMO=1`,
skill cast at t=18.5 s):

```
sound-hook: init rc=0 status=postId=0 postStr=0 postWStr=0 loadBankW=0
```

`proof/audio/sound_probe_20261005.log`: after the three init lines, **zero
`PostEvent` and zero `LoadBank` lines** for the entire run, including the skill cast.
So the engine's tag path never dispatches a Wwise event (and never loads a bank
through the wchar API) in the host — the earlier Frida finding is now instrumented
in-process with the same conclusion.

Note: PSS **particle** SFX tags do fire in the host (M1.6 blade/ring), so the
animation-tag system itself works; the stop is specific to the sound path.

## 4. Next probes (ordered, not yet done)

1. Hook `KG3D_EngineEventManager::_OnProcessApplySoundTag` (adapter; INT3/VEH logger)
   — decide whether the sound-tag callback fires at all during the skill.
2. Hook `KG3DModel::EnableSfxSoundTag` (`0x1800BA760`) to see whether the host model
   ever enables sound tags; if it never does, capture the model pointer (via the
   `PlayAnimation` hook) and call it — the likely gate, given the editor's actor path.
3. Extend `LoadBank` hooks to the by-ID / ANSI / memory-view overloads to classify
   bank loading; then retest the tag path.
4. Product state stays: `flws_sound.wav` via winmm is the **registered provisional**
   (`HOST_AUDIO_STEP1.md`); retire it only when a PostEvent is observed.

## Reproduce

```powershell
native\build_sound_probe.cmd
$env:RC_CLIENT_EXE='reborn_client_audionative.exe'; client\build_client.cmd
# cwd = C:\SeasunGame\MovieEditor
$env:RC_SOUND_HOOK='1'; $env:RC_DEMO='1'; $env:RC_STARTUP='nodb'; $env:RC_AUTORUN='22000'
& bin64\reborn_client_audionative.exe
Get-Content bin64\reborn_out\sound_probe.log
```

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| host shell is KG3DSoundX64.dll + Wwise/FMOD, all loaded | HIGH | module dump run `reborn_20261005_182551.log` |
| zero PostEvent/LoadBank(wchar) calls across init + skill cast | HIGH | hooks rc=0 + empty probe log (instrumented) |
| stop is upstream of Wwise, not in Wwise init | HIGH | `GetWwiseManager` resolved; hooks installed after init |
| `EnableSfxSoundTag` is the likely gate | MED | string/xref only; probe planned |
