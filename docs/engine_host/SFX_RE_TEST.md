# .Sfx per-file re-test on the MovieEditor host (v5, 2026-10-06)

Re-test of the claim "the MovieEditor engine cannot play `.Sfx`" with the
**synced sandbox** (`ability_sandbox` on main's client sources + the
owner-chain `sfx_shim.dll`), scripted through `Skill.exe`.

## Method

`Skill.exe` with `RC_SFX_ENGINE=1` + `RC_SFX_TEST_PATH=<absolute .sfx>` casts
如意法 (a dataset ability with a `dummy` step); the cast's effect path then calls
`sfx_shim.dll!RC_Shim_SfxPlay(path, x, y, z)` - the shim mirrors the engine's own
tag-spawn owner chain (`singleton->vt[8]()`, engine caller @0x76E51A), calls
`KG3D_CreateSFXFromFile`, resolves the play interface and plays. On failure the
app falls back to the staged PSS dummy.

```powershell
$env:RC_MAP = 'C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap'
$env:RC_AUTORUN = '12000'; $env:SB_CAST_MS = '3000'; $env:SB_ABILITY = '如意法'
$env:RC_SFX_ENGINE = '1'; $env:RC_SFX_TEST_PATH = '<abs .sfx>'
& C:\SeasunGame\MovieEditor\bin64\Skill.exe       # log: bin64\Skill\out\Skill_*.log
```

## Results (ME engine 2026-09-14 build, synced sandbox, 2026-10-06)

| `.Sfx` (extracted from the PakV4 store) | create `rc` | play | fallback dummy |
|---|---|---|---|
| `m明教元素18.sfx` | **0** (obj non-null, `exc=0`) | **rc=0** (`play=0xE5420`) | skipped |
| `m明教元素19.sfx` | **0** | rc=0 | skipped |
| `释放_气场聚集03.sfx` | **0** | rc=0 | skipped |
| `释放_范围选择01.Sfx` | **0** | rc=0 | skipped |
| `d001014伞开灰.sfx` | **0** | rc=0 | skipped |
| `g光晕02.sfx` | 7 (create NULL) | - | PSS dummy |
| `c纯阳坐忘.sfx` | 7 (create NULL) | - | PSS dummy |
| `鼠标移动.Sfx` | 7 (create NULL) | - | PSS dummy |

**No AVs.** 5/8 files create **and play through the engine's own SFX system**
(no host dummy); 3/8 fail gracefully (NULL -> staged-PSS fallback).

Evidence lines (log): `engine sfx play rc=0 status=sfx play owner=... iface=...
... -> obj=0x... exc=0x00000000 fault_rva=0x0 ... | model=0x... mvt0=0xC068C4
play=0xE5420 rc=0x00000000`; failure case: `rc=7 status=sfx play ... poolRc=-1`
with no model/play tail.

## What this changes

- The earlier ME AVs (per-file isolation 2026-09-29: 元素19/光晕02/气场聚集03)
  are **gone** with the owner-chain shim: the same files now either create
  (19, 03) or fail gracefully (02). The 2026-09-29 AV isolation used the
  pre-owner-chain call path.
- "MovieEditor cannot play `.Sfx`" is **false as a general statement**; the
  remaining gap is per-file (3/8 here, all graceful) plus the tag/socket-binding
  semantics (the app has no represent tag consumer; the shim plays at the cast
  point).
- Open items: m明教元素19/g光晕02/鼠标移动/c纯阳坐忘 create-NULL cause (build
  vs file format - the client 09-27 engine creates `c纯阳坐忘` cleanly, so the
  NULLs are suspected ME-build format gaps); socket/bone binding; wiring the
  engine-SFX step into the dataset processes (the app hook exists).

## Wired into the dataset (如意法) - 2026-10-06

The dataset's `sfx` step kind now plays authored `.Sfx` through the engine:

- `ability_picker/tools/build_candidates.py` `PROCESS["如意法"]`: the staged PSS
  stand-in was **replaced by the four authored tani tags** (`m明教元素18.sfx`,
  `m明教元素19.sfx`, `释放_气场聚集03.sfx`, `g光晕02.sfx`) as `kind: "sfx"` steps.
- App (`ability_sandbox/rb/RebornClient.cs`): a `sfx` cast step resolves a bare
  name against `bin64\ability_picker\sfx` (`SB_SFX_DIR` override) and calls
  `engineSfxPlay(...)` (the owner-chain shim, create + play).
- Local staging: copy the extracted tags from
  `%TEMP%\opencode\skillv2\out_sfx\data\source\other\特效\技能\sfx\释放\` into
  `MovieEditor\bin64\ability_picker\sfx\` (runtime dir, not tracked).

Run evidence (`Skill_20261006_160240.log`, `SB_ABILITY=如意法 SB_CAST_MS=3000`):

```
cast: 如意法 steps=6 animMs=939            <- no dummy step remains
cast anim -> ...\f1smj10双刀buff04.ani
cast sound -> 75054615
engine sfx play rc=0 ... -> obj=0x16E652900 exc=0x0   cast sfx -> m明教元素18.sfx ok=1
engine sfx play rc=0 ...                              cast sfx -> m明教元素19.sfx ok=1
engine sfx play rc=0 ...                              cast sfx -> 释放_气场聚集03.sfx ok=1
engine sfx play rc=7 ...                              cast sfx -> g光晕02.sfx ok=0
```

Visual fingerprint (engine `RC_SHOTS` renders, same run; character small at the
default 600u camera - attribution caveat below):

| frame | changed pixels vs pre-cast (>24/255) | bright delta (+60) |
|---|---|---|
| `rc_00_2600ms` (pre-cast) | - | - |
| `rc_01_3200` (cast+0.2s) | 0.86% | - |
| `rc_02_3800` (cast+0.8s) | **1.03%** | **1076 px** |
| `rc_03_4400` (cast+1.4s) | 0.38% | - |
| `rc_04_5200` (cast+2.2s) | 0.27% | 187 px |
| `rc_06_8500` (cast+5.5s) | 0.21% | 131 px |

A transient burst peaking at cast+0.8 s then decaying = the cast read (pose +
effects). Proof frames: `proof/netcode/sfx_ruyi_cast_{pre_2600,peak_3800,post_5200}ms.png`.
The pose animation (939 ms) also contributes changed pixels at this camera
distance, so the effect-only attribution needs a closer camera (open item).

Open items from this wiring: tag play **times** are `t=0` (the tani tag frame
times are not parsed yet); `g光晕02` is the known ME NULL; a close-camera visual
confirmation pass.

Capture-tool notes (`tools/proof/capture_window.ps1`): extended with `-AnyClass`
+ console-class skip + all-process-pids matching (the Skill host owns several
windows/processes). `CopyFromScreen` grabs the **screen region**, so an
unfocused window is captured only where it is visibly uncovered - prefer the
app's `RC_SHOTS` engine path for sandbox visuals.

## Reproduce

The env block above, one run per file (~75 s each); read
`bin64\Skill\out\Skill_*.log` for the `engine sfx play` line. Shims:
`native/sfx_shim.cpp` + `native/build_sfx_shim.cmd` (built DLL staged in
`MovieEditor\bin64`).

Dataset-path run (no `RC_SFX_ENGINE` needed; the process carries the `sfx`
steps): stage the tags into `MovieEditor\bin64\ability_picker\sfx\`, copy the
regenerated `ability_picker\data\ability_candidates.json` to the runtime
`bin64\ability_picker\`, then

```powershell
$env:RC_MAP='C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap'
$env:RC_AUTORUN='14000'; $env:SB_CAST_MS='3000'; $env:SB_ABILITY='如意法'
$env:RC_SHOTS='2600,3200,3800,4400,5200,6500,8500'   # engine renders -> out
& C:\SeasunGame\MovieEditor\bin64\Skill.exe
```

`ability_sandbox\build.cmd` rebuilds the app; `build_candidates.py` regenerates
the tracked dataset.

Last verified: 2026-10-06 (v5, `agent/skillv5-sandbox`).
