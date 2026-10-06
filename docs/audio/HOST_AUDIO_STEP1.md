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

## Step 2 — native Wwise playback (DONE, 2026-10-05, `agent/audio-native`)

The host now plays the skill sound through the engine's **own Wwise engine**
(`KG3D_WwiseX64.dll`, the same one the editor shell uses) with the game's own
bank and event id — no winmm WAV in the default path.

- Mechanism (`native/sound_probe.cpp`, loaded when `RC_BANK` is set):
  `LoadBankMemoryView(Init.bnk)` → `LoadBankMemoryView(skillremake.bnk)` →
  `RegisterGameObj(1)` + `AddDefaultListener(1)` → `PostEvent(3378728138, 1)`
  on skill cast. Files come from the extracted game banks
  (`assets/sound/`, gitignored, provenance `SOUND_PATH.md`).
- Env: `RC_BANK=<path to skillremake.bnk>` enables native (default when set);
  `RC_SOUND_NATIVE=0` disables; `RC_SOUND_EVENT=<id>` overrides the event
  (default 3378728138 = FLWS). WAV via winmm remains the fallback when no bank
  is provided.
- Evidence (`reborn_20261005_184354.log` + `sound_probe.log`):
  `Init.bnk rc=1`, `bank ok id/rc=1` (AKRESULT 1 = `AK_Success` in Wwise's enum),
  `sound: native post id=3378728138 playing=1`, clean `DONE`.
- The engine's own tani-SoundTag dispatch still does not fire in this host
  (instrumented proof in `NATIVE_AUDIO_PROBE.md`); the client posting the
  skill event is the product-side path, with the engine event id and bank from
  game data.

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| Wwise init succeeds in the product host | HIGH | run log `sound: KG3DSoundCLR.Init ok` |
| native playback: banks load + event posts | HIGH | probe log (rc=1 both banks, playingId=1) `reborn_20261005_1843*` |
| skill WAV fallback plays (winmm accepted) | HIGH | run log `play rc=True` (audibility not machine-verifiable) |
| engine SoundTag dispatch silent in host | HIGH | instrumented hooks, `NATIVE_AUDIO_PROBE.md` |
