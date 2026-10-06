# Host audio — step 1 (Wwise init + provisional skill sound)

**Date:** 2026-10-05 · **Branch:** `agent/audio-host` (worktree `Desktop\reborn-iso-audio`)
**Scope:** item 1.6 — runtime audio. Before this, the product client created
`KG3DSoundCLR` but never used it: no `Init`, no `FrameMove`, no sound at all.

## What was done

`client/RebornClient.cs`:

- `sound.Init(startupPath, form.Handle)` after `editor.Init` (the spike's call,
  now in the product client) and `sound.FrameMove()` each frame.
- On skill cast, plays the decoded FLWS WAV (`bin64\flws_sound.wav`, 180,302 B,
  decoded from Wwise `161340541.wem` — `SOUND_PATH.md`) via winmm
  `PlaySound(SND_FILENAME|SND_ASYNC|SND_NODEFAULT)`.

**Registered provisional (AGENTS §6):** the engine's tani SoundTag does not fire
in the host — `SOUND_PATH.md` Frida evidence: Wwise initializes and registers the
actor, but there is **no `LoadBank` and no `PostEvent`**. Until the native tag
path is recovered, the skill sound is played from the decoded WAV (game data, not
an invented asset). Re-open criteria: the tani SoundTag fires in the host with the
banks loaded (find the client-side init step the host is missing).

Env: `RC_SOUND=0` disables audio (default on).

## Evidence

Run `reborn_20261005_135524.log` (`reborn_client_audio.exe`, title
`sandbox-audio`, ns `reborn_client_audio.memory`, `RC_DEMO=1`):

```
sound: KG3DSoundCLR.Init ok
sound: skill wav C:\SeasunGame\MovieEditor\bin64\flws_sound.wav
sound: skill wav play rc=True
skill cast
```

- `Init ok` — Wwise initialized in the product host.
- `play rc=True` — winmm opened and started the WAV asynchronously.
- The log contains **no engine Wwise/bank/SoundTag lines** — the engine path
  stays silent (consistent with the Frida finding).

## Reproduce

```powershell
$env:RC_CLIENT_EXE = "reborn_client_audio.exe"; client\build_client.cmd
# cwd = C:\SeasunGame\MovieEditor
$env:RC_DEMO='1'; $env:RC_AUTORUN='22000'
& bin64\reborn_client_audio.exe
# log: bin64\reborn_out\reborn_<ts>.log — look for the sound: lines + skill cast
```

## Step 2 — native Wwise path (event/bank calls succeed, source does not render)

Status: **partial** (2026-10-05, `agent/audio-native`). The Wwise API chain works,
but no audio renders, so the client verifies rendering per cast and **falls back
to the WAV** — the user hears the skill sound via the registered provisional.

- What works (`native/sound_probe.cpp`, loaded when `RC_BANK` is set):
  `LoadBankMemoryView(Init.bnk)` → `LoadBankMemoryView(skillremake.bnk)` →
  `RegisterGameObj(1)` + `AddDefaultListener(1)` → `PostEvent(3378728138, 1)`.
  AKRESULT 1 = `AK_Success`; `playingId=1` returned; Wwise `IsInitialized=1`
  (48 kHz, 1024/帧).
- What fails: the bank's audio is **streamed** (`161340541.wem`, bank has 0
  embedded RIFF blocks), and the editor host's Wwise IO cannot resolve it —
  `GetSourcePlayPosition(playingId)` returns `AK_Fail` and position 0 in every
  configuration tried: staged media tree + cwd switch, `SetCurrentLanguage`
  (`Base`/empty), name and id file variants. No render → silence.
- Client behavior: after `PostEvent`, `SoundProbe.Diag(pid)` requires the source
  position to advance; if not, native is disabled and the WAV plays for that cast
  (`sound-native: no rendering (streamed media unresolved) - WAV fallback`).
- Root cause: the game client resolves streamed Wwise media through its own
  VFS/`IAkFileLocationResolver`; the MovieEditor engine (and our host) has no
  resolver for the install's `data/Wwiseaudio` paks. Closing it needs a custom
  file-location resolver (`AK::StreamMgr::SetFileLocationResolver`,
  `KG3D_WwiseX64.dll` export available) or the game's resolver instance.

- Env: `RC_BANK=<path to skillremake.bnk>` enables the native attempt;
  `RC_SOUND_MEDIA=<staged tree>` sets the media root; `RC_SOUND_NATIVE=0`
  disables; `RC_SOUND_EVENT=<id>` overrides the event (default 3378728138).

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| Wwise init succeeds in the product host | HIGH | run log `sound: KG3DSoundCLR.Init ok`; `IsInitialized=1`, 48 kHz |
| native banks load + event posts (API level) | HIGH | probe log rc=1 both banks, `playingId=1`, `reborn_20261005_1843*` |
| native source does **not** render (streamed media unresolved) | HIGH | `playPos rc=2/2 pos=0/0` in all tried configs (`sound_probe_streamed_media_diag_20261005.log`) |
| audible path is the WAV fallback | HIGH | run `reborn_20261005_1902*`: `no rendering … WAV fallback` then `skill wav play rc=True` |
| engine SoundTag dispatch silent in host | HIGH | instrumented hooks, `NATIVE_AUDIO_PROBE.md` |
