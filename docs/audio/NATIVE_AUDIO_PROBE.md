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

## 4. Outcome — native chain runs, media does not render (partial, 2026-10-05)

The Wwise API chain executes: banks load (`rc=1`), game object + listener register,
event `3378728138` posts (`playingId=1`) — but the **source never renders**
(`GetSourcePlayPosition` → `AK_Fail`, position 0) because the bank's audio is
**streamed** (`161340541.wem`; 0 embedded RIFF blocks in the bank) and the editor
host's Wwise IO cannot resolve it. Tried without effect: staging the `.wem` tree
(name/id variants, `Base`/`English(US)`/`SFX`/root), switching the process cwd,
`SetCurrentLanguage(Base)`, `RenderAudio` ticks. The client therefore verifies the
source position after posting and **falls back to the WAV** for that cast
(`HOST_AUDIO_STEP1.md` Step 2).

Root cause (completed via HIRC parse + `CreateFileW` hook): the event is a plain
Play of Sound `0x9253BB` referencing media `161340541`; the bank ships only
`BKHD`+`HIRC` (no `DIDX`/`DATA`), so the media is streamed. The stream manager and
resolver are non-null, yet **zero media opens reach the OS** (`KernelBase
CreateFileW` hook silent) — the host's stream device has no usable low-level IO
hook (the game client supplies it; the editor install does not). Closing it =
recover/reuse the game's `IAkLowLevelIOHook`/media-delivery path with evidence,
never by guessing the interface ABI.

Recorded engine gap (unchanged): the engine's own tani SoundTag dispatch never calls
PostEvent in the host (§3); the identified suspects are
`KG3D_EngineEventManager::_OnProcessApplySoundTag` and the model gate
`KG3DModel::EnableSfxSoundTag` (`0x1800BA760`).

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
