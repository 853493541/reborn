# `.mani` camera track format (ACON) - decoded 2026-10-06

**Area:** camera · **Branch:** `agent/camera-tracks` · **Status:** cameradata variant
decoded and verified; rush variant deferred. Companion: `CAMERA_TRACKS_PLAN.md` (P0/P1).

## 1. Container framing - HIGH

`.mani` files are an **ACON** object stream (KG3DMovieX64.dll serializer):

```
section := { u32 magic 'ACON' (0x4E4F4341), u32 classId, 32 zero bytes } + payload
file    := section+
```

Evidence (game client `bin64\KG3DMovieX64.dll`, read-only disasm in
`proof/camera_tracks/disasm/`):

| Fact | RVA | Source |
|---|---|---|
| 40-byte header validator: reads 0x28 bytes, checks magic, checks classId vs expected | `0x1238d0` (cmp magic @ `0x123995`) | `acon_validator.txt` |
| 40-byte header writer: magic + id + 32 zeros | `0x123d20` (`mov dword ptr [rax], 0x4e4f4341`) | `acon_movie.txt` |
| class factory switch (ids 0..12) - id 10 allocates 0x250-byte object, ctor `0x19b8d0` sets `+0xb8 = 10` | `0x1ba7a0`, table `0x1bad54` | `acon_factory.txt`, `acon_case10.txt`, `acon_ctor10.txt` |
| object-graph loader: read id -> factory -> validate header -> `vt[+0xc0]` Load | `0x1bb170` | `acon_loader_entry.txt` |

Only classes **25** (camera-ani set) and **10** (camera track) appear in the shipped
`.mani` corpus. `SceneCameraAni.tab` (337 rows) references
`\represent\camera\cameradata\<id>_<n>.mani`.

## 2. Cameradata grammar - HIGH (verified on 10/10 shipped samples)

```
class-25 payload: 8 zero bytes + { u32 1, f32 duration, u32 0, u32 1 }
class-10 payload: 8 zero bytes
  + meta  { u32 1, f32 duration, u32 1, u32 0 }
  + A hdr { u32 nA, u32 0, f32 z0, f32 y0 }          # implicit frame-0 key (x=0,z0,y0)
  + (nA-1) x A-key { f32 x, u32 frame, f32 z, f32 y }
  + B hdr { f32 x0, u32 0, u32 nB, u32 0 }
  + nB x B-key { f32 x, f32 a, f32 b, u32 frame }    # last key is a loop closure at frame 1
```

- `duration` (frames) = last A frame + 1 (e.g. `13_0`: dur 175, last frame 174;
  `30_1`: dur 1201, last frame 1200). A/B keys are **sparse** (gaps allowed).
- A track = primary camera position, world space, cm, Y up (**structure HIGH**,
  axis semantics MED - matches the host's world space in `client/TerrainSampler.cs`).
- B track = secondary track (**structure HIGH**, semantics MED - treated as the
  look-at target by the host player; the tab's rotation-type column is the consumer).
- The final B key repeats **frame 1** with different values (loop closure), verified
  on all 10 samples.

Verification: `python tools\camera\mani_probe.py --verify <cameradata dir>` ->
`10/10 parsed` with exact payload consumption (no trailing bytes). Key dump:
`proof/camera_tracks/mani_keys.tsv` (13_0, 21_2, 30_1; from the temp extraction).

## 3. Rush variant - NOT decoded (deferred)

`data/movie/camera/16.mani` / `17.mani` (from `player_rush_camera.txt`) are class-10
only; the payload meta marker is `{1, dur, 0, 1}` (swapped vs cameradata) and the key
grammar differs (40-byte transform samples `{pos(3), A(3), quat(4)}` with quaternion
norms == 1 at stride 40, plus `{f32 value, u32 frame}` scalar pairs - see the probe
notes in the plan). **Not used by P1** (rush/dialog triggers need runtime state that is
absent; registered boundary in `CAMERA_TRACKS_PLAN.md` §6).

## 4. Reproduce

```powershell
# offline selftest (round-trip + negative cases)
.venv\Scripts\python.exe tools\camera\mani_probe.py --selftest          # 14/14 PASS

# extraction (game assets stay in the ignored temp dir)
#   run_pakv4 ... -> %TEMP%\opencode\camtracks_mani\out\represent\camera\cameradata
.venv\Scripts\python.exe tools\camera\mani_probe.py --verify "$env:TEMP\opencode\camtracks_mani\out\represent\camera\cameradata"
# -> 10/10 parsed

# regenerate the proof table (delete first; appends)
.venv\Scripts\python.exe tools\camera\mani_probe.py --tsv proof\camera_tracks\mani_keys.tsv <13_0.mani> <21_2.mani> <30_1.mani>
```

Evidence: `proof/camera_tracks/disasm/*.txt` (disassembly; supporting dumps:
`acon_movie.txt` header writer region, `acon_frame1b/2b.txt` + `acon_save_helpers.txt`
chunk save/load callers, `acon_class10_vt.txt` class-10 vtable method region),
`proof/camera_tracks/mani_keys.tsv` (decoded keys),
`proof/camera_tracks/skill_move_camera.txt` (skill-FOV table copy).

Last verified: 2026-10-06.
