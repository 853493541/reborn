# .Sfx per-file re-test on the MovieEditor host (v5, 2026-10-06)

## BREAKTHROUGH (2026-10-06): play the tani - the engine renders the tags

The engine draws only effects spawned by its animation-tag manager. That
manager takes the tag records from the **animation being played** - so playing
the ability's own `.tani` (instead of its base `.ani`) makes the engine spawn,
position and render the embedded `.Sfx` tags itself. Verified: 如意法/百足/
五蕴皆空 tani casts render flames/sparks/energy trails with **no AV**
(`rc_01_3200ms.png`, wide-frame effect deltas; logs `Skill_20261006_2025*`).
The dataset now plays the matched tani for **79 anim steps** (build pass
`apply_tani_anim`); the PSS stand-in is dropped for abilities whose tani embeds
`.Sfx` tags (the authored effects are the visible layer). Caveat: some tag
positions sit off the character (socket/bone binding still approximate).

The sections below (shim create/play, warm-up, per-file rc table) are the
earlier research that led here; the shim path creates but never rendered.

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

## Wired into the dataset (broad pass, 2026-10-06)

Every staged ability whose matched tani embeds `.Sfx` tags now plays them
through the engine instead of the staged PSS stand-in:

- Tani-tag pass: extracted the 53 missing matched tanis (PakV4SfxExtract),
  parsed the `.sfx` path strings from all 82 unique matched tanis -> **20
  abilities** embed tags, **61 unique tag files** (60 extracted from the paks;
  `抖动1.sfx` is a dangling reference but was already staged). Manifest:
  `ability_picker/data/sfx_tags.json`.
- `build_candidates.py` `apply_sfx_tags()`: for those abilities the `dummy`
  steps are dropped and the tags appended as `kind: "sfx"` steps (19 wired;
  幻蛊 has no staged process).
- App (`ability_sandbox/rb/RebornClient.cs`): `sfx` step kind (bare names
  resolve against `bin64\ability_picker\sfx`, `SB_SFX_DIR` override) calls
  `engineSfxPlay` (owner-chain shim).
- Local staging: the 61 tags live in `MovieEditor\bin64\ability_picker\sfx\`
  (runtime dir, not tracked).

**Startup warm-up (required).** The ME engine's *first* create of ~27 of the
61 tags AVs (`exc=0xC0000005`, shim-guarded -> NULL) once the scene has
settled (~3 s in), while the same creates at t~0 all pass (61/61). A startup
warm-up pass (`RC_SFX_WARM`, default on) creates every staged tag once at a
far position (player +20000/-2000/+20000): the resources get cached and
cast-time creates then succeed. Measured before/after (same ability):
五蕴皆空 2/4 -> **4/4**, 如意法 3/4 -> **4/4**; 驱夜断愁 **6/6**, 百足 **2/2**
(`Skill_20261006_170936/171018/171059.log`). `RC_SFX_WARM=0` disables;
`RC_SFX_BATCH=1` + `RC_SFX_BATCH_DELAY=<ms>` re-runs the rc table.
Residual transient NULLs are retried up to 3x 250 ms apart (`cast sfx retry`).

Run evidence (`Skill_20261006_170936.log`, `SB_ABILITY=五蕴皆空 SB_CAST_MS=3000`):

```
cast: 五蕴皆空 steps=6                     <- no dummy step remains
cast anim -> ...\f1ssl04袈裟攻击05.ani
cast sound -> 157383905
cast sfx -> 释放_袈裟03_01.sfx ok=1        <- engine sfx play rc=0, obj non-null
cast sfx -> 释放_袈裟03_02.sfx ok=1
cast sfx -> 释放_袈裟03_03.sfx ok=1
cast sfx -> 释放_袈裟03_04.sfx ok=1
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
$env:RC_AUTORUN='14000'; $env:SB_CAST_MS='3000'; $env:SB_ABILITY='五蕴皆空'
$env:RC_SHOTS='2600,3200,3800,4400,5200,6500,8500'   # engine renders -> out
& C:\SeasunGame\MovieEditor\bin64\Skill.exe
```

`ability_sandbox\build.cmd` rebuilds the app; `build_candidates.py` regenerates
the tracked dataset.

Tag re-extraction (next pass): collect the matched tani VFS paths from the
dataset, `PakV4SfxExtract.exe <path-list.txt(gb18030)> <out>` for the missing
tanis, regex the GBK `.sfx` path strings (normalize truncated ones to
`data\source\other\特效\技能\sfx\...`), extract them the same way, refresh
`ability_picker/data/sfx_tags.json`, regenerate, copy to the runtime dir.

Last verified: 2026-10-06 (v5, `agent/skillv5-sandbox`).
