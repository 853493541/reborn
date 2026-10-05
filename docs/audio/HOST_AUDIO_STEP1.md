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

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| Wwise init succeeds in the product host | HIGH | run log `sound: KG3DSoundCLR.Init ok` |
| skill WAV plays (winmm accepted) | HIGH | run log `play rc=True` (audibility not machine-verifiable) |
| engine SoundTag still silent | MED | no engine Wwise lines in the run; Frida evidence in `SOUND_PATH.md` |
| native tag path needs the missing bank load | LOW | hypothesis; next probe = find the client's bank-load call |
