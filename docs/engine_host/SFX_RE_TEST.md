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

## Reproduce

The env block above, one run per file (~75 s each); read
`bin64\Skill\out\Skill_*.log` for the `engine sfx play` line. Shims:
`native/sfx_shim.cpp` + `native/build_sfx_shim.cmd` (built DLL staged in
`MovieEditor\bin64`).

Last verified: 2026-10-06 (v5, `agent/skillv5-sandbox`).
