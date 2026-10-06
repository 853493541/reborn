# Rendering / LOD / weather option reference (areas 1.8–1.10)

**Date:** 2026-10-04 · **Branch:** `agent/render-options` · **Status:** P0–P3 first cut
**DONE** (apply path = `KGEngineCLR.SetEngineOptionFromConfigFile`; client `RC_QUALITY` /
`RC_OPT_*` / `RC_WEATHER` implemented and proven on the engine); P4 partial (dynamic
weather API found, no visible effect with defaults); P5 initial numbers.
**Companion plan:** `RENDERING_OPTIONS_PLAN.md`.

Read-only research. Sources: game client `...\zhcn_hd\config`, `...\zhcn_hd\bin64`,
`...\MovieEditor\config.ini`, and xref disassembly under `proof/render/disasm/`.

## 1. Corpus census (P0.1, tool canonical)

Tool: `tools/render/preset_census.py` → `proof/render/option_matrix.tsv`,
`proof/render/varying.tsv`, `proof/render/gpu_switch_summary.tsv`.

| Metric | Value |
|---|---|
| Preset files | 15 (`config_1..9` + 6 `config_bd_*`) |
| Key entries per tier | 234 (tiers 1–7), 375 (t8), 241 (t9); `_bd_*` 472; `config.default` 343; `MovieEditor\config.ini` 376 |
| Unique keys, all sources | **490** (`option_matrix.tsv`) |
| Unique keys, 9 main tiers | **409** |
| Varying across the 9 main tiers | **72** (`varying.tsv`) |
| Tier selector key | `[ENGINEOPTION] nEngineGraphicsLevel=1..9` |
| GPU override table | `GpuSwitchOptionTab.tab`, 20 data rows × 32 columns |

The earlier plan's PowerShell pass counted 408 unique keys; the tool's parse (canonical,
dups counted) is **409** — count method differences only, both agree on 72 varying.

## 2. Runtime read chain (P0.3/P0.4 partial)

| Layer | Function / evidence | What it proves |
|---|---|---|
| Machine tier detection (game client) | `JX3ClientX64.exe` `InitMachineConfig` RVA `0x9A8A0`: opens `g_OpenIniFile`, reads `[Performance] GPUScore` then `VideoType` (`proof/render/disasm/client_VideoType.txt` L4-96); second path RVA `0xE1FF0` reads/writes `config/machine_config.ini` (`GPUScore`/`VideoType` L573-975) | machine config (GPU score/type) is local and re-written by the client |
| Engine option load | `KG3DEngineAdapterX64.dll` `KG3D_LoadJX3Config_From_DX9` RVA **`0x5F9F0`**: opens **`config.ini`** (L27) then reads ~150 `[KG3DENGINE]` keys into one config struct (`rsi` + offsets), each with a code default | **`config.ini` in the working dir is the runtime option file**; defaults and clamps live in code |
| Engine option save | adapter RVA **`0x67B10`**: writes `[KG3DENGINE]` keys back (`AAOPTION_DLSSOption`, `nShadowCullOpt`, `EnableFSR*`, `FSR2Option/Sharpnees`, `EnableFSR3Interpolation`, ...) | config round-trip exists (load + save) |
| Graphics level key | in the load fn: `nEngineGraphicsLevel` → cfg `+0x260`; default is `'0'` when `nRenderLevel == 100`, else `'3'` (L3857-3879) | level is a first-class config value; no per-level remap seen in this function |
| UI/video schema (game) | `JX3UIX64.dll` RVA **`0x118970`**: builds a descriptor map of `{key, type code, struct offset, default}` (e.g. `nWaterDetail` +0x1b0, `bPostEffectEnable` +0x60, `bBloomEnable` +0x64, `bSSAO` +0x7c, `nMDLRenderLimit` +0x218, `nClientSFXLimit` +0x21c, `bEnableRC_AmbientOcclusion` +0x2ac ...), xref at `0x11AD19`; more keys follow past the captured window | the game's video panel option surface, with types (`1`=bool, `2`=int, `5`=enum?, `8`=float) and offsets |
| Other consumers | `KG3DEngineDX11EX64.dll` has `fModelLodRadius`, `GraphicsLevel`; `KG_EngineEditorX64.dll`/`MovieEditorHD.exe`/`MovieEngineCLR.dll` contain the same keys | editor host exposes the same option set |

### 2.1 Active `config.ini` is a generated merge (P0.4 progress)

The game's **active** `zhcn_hd\config.ini` (present in the install, 471 keys,
`nEngineGraphicsLevel=1`) does not equal any preset file. Key/value comparison against
the `_bd_` family (all 467 keys): closest is `config_bd_1_zuijian` — 412 shared values
identical, 55 differ (machine/cache/user settings; low-tier values match: foliage
density 10, cull 10000, shadow type 0). So the applied option set is a **merge** of a
`_bd_<level>` preset with machine/UI overrides, materialized as `config.ini`.

- Full-install scan (17,843 files, whole `Game\JX3` + `MovieEditor` trees): the literals
  `config_1_zuijian` / `config_9_chenjin` / `GpuSwitchOptionTab` appear **nowhere** — the
  preset file names are never referenced by install binaries. The merge/selection owner
  is therefore external (launcher/patch tooling) or the video panel persists values
  directly through the adapter **save** function (`0x67B10`).
- Per-player display settings also persist in `userpreferences.jx3dat` (player state;
  candidate read path for a future "current user settings" probe).
- Our host's runtime file is `MovieEditor\config.ini` (tier-7-ish editor values,
  `nEngineGraphicsLevel=7`, `fFoliageCullDist=400000`) — it is an editor config, not a
  game tier, and it is install read-only.

## 3. Option groups with proven struct offsets (load fn)

| Group | Keys (examples) | Offset evidence |
|---|---|---|
| Render baseline | `nRenderLevel` | +0x348 (default `200`) |
| Graphics level | `nEngineGraphicsLevel` | +0x260 (default 0/3 rule above) |
| Shadows | `nShadowType` (+ validates: 0..3 or 7, else 0) | +0x5c |
| Camera | `CammeraDistance`, `fCameraDistance` (UI) | +0xed8 / UI +0x50 |
| SSR | `EnableSSR`, `bEnable_SSR` (same slot), `SSRQuality` | +0x5fc, +0x634 |
| Post FX | `bEnableRC_AmbientOcclusion`, `..._AtmosphericFog`, `..._Bloom`, `..._Depth`, `..._HeightFog`, `..._LightShaftBloom`, `..._LightShaftOcclusion`, `..._StingRayVolumetircCloud`, `..._SunLensflare`, `..._Vignette`, `..._EnvProbe`, `..._SSS`, `..._SSR`, `..._LowReflection`, `..._FoliageBlur`, `..._EyeProtectionMode`, `..._HighEnvironmentRender`, `..._ForceSkyCubeTex`, `..._ForceOitToSoftMask2`, `..._ForwardRender`, `..._ShockWave`, `..._SpotLight`, `..._DirectionLight`, `..._PointLight`, `..._{Spot,Direction,Point}LightShadow`, `..._SkyLight` | +0x740..+0x7bc (defaults `1` for lights/fog/bloom, `0` for shadows/low-reflection) |
| Day/night + environment | `bEnableDayNightCycle` +0x2f0, `bEnableDynamicEnvironment` +0x2f4, `bEnableIndirectLightVolume` +0x2f8, `bEnableOfflineIndirectLightVolume` +0x2fc, `bEnableSSPR` +0x300, `bEnableSeasonalVariation` +0x4c4, `fSeasonEffectIntensity` +0x4cc | same fn |
| True sky / weather | `bShowTrueSky` +0x50; `bWeatherOn` is EngineStaticConfig | same fn + config |
| Foliage | `bEnableFoliageCull` +0x990, `bEnableFoliageShadowRender`, `bEnableFoliageRender`, `bEnableSubFoliage{HC,ST,WJ,GM,TW}Render` +0x9ac.., `bEnableFoliageProjCull` +0x9c0, `bDisableFoliageLowMesh`, `bEnableFoliageBakeTerrainRVT`, `fBakeTerrainRVT*`, `nFoliageLoadOption` +0xa2c, `nFoliageDensity` +0x9d8 (**clamp ≤100**), `nFoliageDensityGrade%i` (5 grades), `nLandscapeFoliage` +0xa30 | same fn |
| SpeedTree | `nSpeedTreeDensity` +0x704 (**clamp ≤100**), `nSpeedTreeLeafScale` +0x708 (float scale), `bIsLoadSRTBillboard`, `nForceFoliageLodForEditor` (default -1) | same fn |
| LOD / distance cull | `bEnableModelDistCull` +0xa34, `bEnableModelProjNumCull`, `bEnableSpeedTreeDistCull`, `bEnableSimpleModelDistCull`, `bEnableParticleSystemDistCull`, `bEnablePointlightDistCull`, `bEnableFoliageDistCull`, `fSpeedTreeCullDist` +0xa40 (default 80000), `fSimpleModelCullDist` +0xa4c | same fn |

Exact captures: `proof/render/disasm/adapter_nEngineGraphicsLevel.txt` (load fn) and
`proof/render/disasm/jx3ui_nEngineGraphicsLevel.txt` (UI schema fn).

## 4. Apply path — DECIDED (P1, managed API)

- **Proven reader:** the engine opens `config.ini` from the process working directory
  (`KG3D_LoadJX3Config_From_DX9`); our cwd's `MovieEditor\config.ini` is install
  read-only, so file replacement is not an option.
- **Chosen route:** the engine's own managed API —
  `MovieEngineCLR.KGEngineCLR.SetEngineOptionFromConfigFile(System.String)` (reflection
  dump of `MovieEngineCLR.dll`, 2026-10-04; sibling calls `GetEngineOption`,
  `GetEngineOptionFromConfigFile`, `SetEngineOption`). It parses a **config.ini-format
  file we control** and applies it at runtime, no install write.
- **Implementation:** `client/VideoOptions.cs`, called right after `Init3DEngine`
  (before map load). Switches:
  - `RC_QUALITY=<1..9|bd<1..7>|default>` → picks `zhcn_hd\config\config_*.ini` (read-only read);
  - `RC_OPT_FILE=<path>` → any ini;
  - `RC_OPT_<KEY>=<value>` → merged overrides, written to `bin64\reborn_out\renderopts_<hhmmss>.ini`;
  - `RC_WEATHER=0|1` and `RC_WEATHER_PARAMS=<12 floats>` → `EnableDynamicWeather` /
    `SetDynamicWeatherParameters`.
- Native DX11 exports (`GetOption`, `CompareEngineOption`, `BuildRenderCache`) and the
  adapter save fn `0x67B10` are further native handles; not needed for the chosen route.

## 4b. Runtime proof (P2/P3/P5, 2026-10-04)

Feature build `reborn_client_renderopts.exe` (title `sandbox-renderopts`, namespace
`reborn_client_renderopts.memory`); fixed pose `(18991,962,33853)` unless noted, 龙门寻宝,
no input, shot t=15 s. Screenshots `proof/render/runs/`, per-run logs `proof/render/logs/`.

### Tier application (P3)

| Run | Config | Screenshot | Frame mean | fps (t=18 s) |
|---|---|---|---|---|
| A | tier 1 | `quality1.png` | `#BAB197` | 536 |
| A' | tier 1 repeat | `quality1b.png` | `#BAB197` (single-digit PNG byte delta) | 597* |
| B | tier 9 | `quality9.png` | `#C1BBA6` (+7 on every 4×4 cell) | 391 |

Tier 1 vs tier 9 differ in every region; repeat A vs A' is noise → **the tier delta is
causal**. (A vs B measured before any other client started; * = fps contaminated by the
concurrent `reborn_client_predraw.exe` of another agent, own namespace — root AGENTS §2.)

### Per-option caps probe (P2) — isolated, one key per run on tier 9

| Option (set to) | Log | Screenshot | Effect vs tier 9 (`#C1BBA6`) | fps | Verdict at this pose |
|---|---|---|---|---|---|
| `bEnableRC_Bloom=0` | `p2_bloom2` | `p2_bloom2.png` | `#BAB198`, every cell changes | 371 | **effective (dominant brightness)** |
| `bEnableRC_AmbientOcclusion=0` | `p2_ao_off` | `p2_ao_off.png` | cells identical (2 × 1 unit) | 376 | no-op / below noise |
| `bEnableRC_SSR=0` | `p2_ssr2` | `p2_ssr2.png` | cells identical | 364 | no-op at this pose (no reflective surfaces) |
| `nShadowType=0` | `p2_shadow0` | `p2_shadow0.png` | ~identical (3 × 1 unit) | 372 | no-op / subtle |
| `nFoliageDensity=5` | `p2_fol5b` | `p2_fol5b.png` | ~identical (1 × 1 unit) | 370 | no-op at this pose (no foliage in view) |
| `nFoliageDensity=999` | `p2_fol999b` | `p2_fol999b.png` | **identical to tier 9** | 372 | clamp consistent with adapter `≤100` |

**Correction note (honesty):** the first bloom/SSR/density batch ran with leaked
`RC_OPT_*` env vars inside one PowerShell process (AO ran into bloom, etc.); those four
artifacts were deleted and re-run isolated. The table above is the clean set. Foliage
options must be probed at a foliage-rich pose (the house pose has none).

### Second pose (P5 groundwork) — real spawn dune `(23334,761,24224)`

| Run | Config | Screenshot | Frame mean | fps (t=18 s) |
|---|---|---|---|---|
| `p5b_tier1b` | tier 1 | `p5b_tier1b.png` | `#B7B09C` | **562** |
| `p5b_tier9b` | tier 9 | `p5b_tier9b.png` | `#B1AD9E`, large local deltas (dark regions: near foliage/shadows rendered) | **369** |

Tier 9 costs ~**−34 % fps** here and changes whole regions → culling/LOD/post differences
are real at a vista pose. Per-LOD-option isolation still open (P5 remainder).

## 4c. Weather/day-night (P4) — API + map data decoded, effect open

**Engine APIs** (`KGEngineCLR`): `EnableDynamicWeather(int)`, `IsEnableDynamicWeather(ref int)`,
`Set/GetDynamicWeatherParameters(12 floats)`. Config keys applicable through the file route:
`bEnableDayNightCycle` (cfg +0x2f0), `bEnableDynamicEnvironment` (+0x2f4),
`bEnableIndirectLightVolume` (+0x2f8), `bShowTrueSky` (+0x50), `bWeatherOn` (EngineStaticConfig).

**Map data decoded** (loose extracts under `C:\jx3tmp`; summary:
`proof/render/environment_summary.txt`): every map ships **quality variants** of the
environment, not just meshes:

| Variant | Keys | Notable values |
|---|---|---|
| HD root `environment.json` (龙门寻宝) | 31 | **`enableDayNightCycle=1`**, `lightSource=0`, `sunlight.diffuseIntensity=6.0`, `moonlight.diffuseIntensity=0.37`, `speedTreeEnv` wind/LOD, `oldSkyWeather` (rain/snow particles: density/speed/alpha/splash, `stainForDyWeather.dds`), `cloudList`, `farMountainList`, `lensflares` (sun+moon) |
| `bd/environment.json` | 35 | day-night off, adds `enableGameTimeAffectLights`, `preferedGameTime`, `enableEnergyCompensation`, `sceneSupplementaryLightMgr` (2 authored lights) |
| `low/environment.json` | 22 | day-night off, `enableCovermap=1`, `enableWssm`, sunlight 2.0 (vs 6.0), different sky/weather/lensflare set |
| `playerEnvironment.json` | 4 | `applySetIndex` + `set0`/`set1` player+camera light rigs (diffuse/ambient/sky intensities, camera light radius/length), `playerEnvProbe` dds paths |

**Tests:** `RC_WEATHER=1` (run D) is pixel-identical to tier 1 → `EnableDynamicWeather` with
default params is a no-op at this pose. `RC_OPT_bEnableDayNightCycle=1` on tier 1
(`p4_daynight_*`) shifts 4 of 16 cells by 1–2 units vs tier 1 and does **not** evolve over
8→19 s → subtle at most; the HD env authors day-night, so the gate is likely the engine
option + game-time source. Open: 12-float param semantics, time-of-day source,
`bShowTrueSky` (KG3D_TrueSkyX64.dll absent from the editor install — likely no-op here),
volumetricCloud asset absent (non-fatal).

### 4d. Day-night API decode + boundary (A1, 2026-10-05, `agent/weather-daynight`)

Managed surface recovered via `RC_ENV_PROBE` (reflection, no guessing):

- `KGSceneCLR`: `SetTrueSkyDayTime(float)` / `GetTrueSkyDayTime()` — the getter is stuck
  at **0.5** and the setter is a no-op (TrueSky module absent); `Update/GetSeasonRelativeYearTime`
  (values **stick**: 0.25 → 0.25), `Set/GetSeasonParam(bool,float,float)` (stick),
  `CreateGDBTimelineCurveFromFile(dir)` / `SetGlobalDynamicEnvTimelineInterpolationForAllTimelineKey`
  (**E_FAIL** — no timeline file ships), `EnableSunLightArcBall`, `ResetEnvironment(dir)`,
  `GetEnvironment()`.
- `KG_EnvironmentCLR` (225 methods): `SetRealSystemDayTime`/`Timezone`/`MaxSunLightIntensity`/
  `MaxMoonLightIntensity` (values **stick**, initial sunMax/moonMax = 0),
  `SetEnvDirectionalLightParameters`, `SetWindParameters` (16), `SetFogVolumeParam`,
  `SetCloudParameters`, lens flares, indirect-light volumes.
- Map data: `environment.json` has a full `dayNightCycle` object (`CurrentDate`, `Latitude`,
  `Longitude`, `time`, `Timezone`, `StarTrailsEnable`, `MaxMilkyWayIntensity`,
  `MaxSunLightIntensity`, `MaxMoonLightIntensity`, `MaxStarDensity/Twinkle/Intensity`,
  `MaxMoonOpacity`) — the shipped HD env authors **all Max\* intensities = 0**.

**Boundary (HIGH): day-night is inert in the editor host.**
1. authored `dayNightCycle.Max*` intensities are zero;
2. `KG3D_TrueSkyX64.dll` exists only in the game client (`zhcn_hd\bin64`), not in
   MovieEditor — install read-only forbids copying it;
3. no GDB timeline file ships (0/56 probe paths) → interpolation call E_FAILs;
4. day-time sweeps (TrueSky + real-system), season params, and an `RC_ENV_DIR` override
   (`ResetEnvironment` rc=0, MaxSun=6) all leave the rendered frame identical
   (`proof/render/daynight/`, per-region RGB within noise).

Knobs kept: `RC_DAYTIME=<0..1>` (real-system + TrueSky setters, logged) and
`RC_ENV_DIR=<dir>` (host-side environment override). Re-open when TrueSky ships in the
editor install, a map authors nonzero dayNightCycle intensities, or a GDB timeline exists.

**A2 dynamic weather (2026-10-05):** `RC_WEATHER=1` applies (`EnableDynamicWeather(1)=0`)
and `RC_WEATHER_PARAMS` accepts 12 floats, but sweeps (all-1, all-100) are pixel-identical
to the dry baseline (`proof/render/daynight/weather_w1.png`, `weather_w100.png`) — the
12-float semantics remain undecoded and the effect is inert at the tested pose. Next
probe: disasm the native consumer (`KG3DEngineDX11EX64` dynamic-weather path) + a
rain-authored map state; assets referenced by `oldSkyWeather` (`stainForDyWeather.dds`,
rain/snow particle set) are the likely render dependency.

**A3 sky/cloud boundaries (2026-10-05, verified):** `KG3D_TrueSkyX64.dll` ships only in
the game client (`zhcn_hd\bin64`), not in MovieEditor (install read-only) — `bShowTrueSky`
and TrueSky-driven sky/stars stay non-functional; `volumetricCloud.json` ships in no map
tier (0/16 probe; only `focus_face_env_params.json` in `bd/`), so the StingRay volumetric
cloud option stays non-functional.

## 5. Open items

1. Which component performs the merge into the active `config.ini` (external
   launcher/patch tooling vs panel-triggered adapter save); `GpuSwitchOptionTab.tab`
   consumer not found anywhere in the install (see §2.1).
2. Full key extraction from the adapter load fn + UI schema fn (a generator can turn the
   disasm captures into the complete key ↔ offset table); `KGEngineOptionProxyCLR` has no
   public fields (reflection), so per-key read-back needs the native `GetOption` export or
   a config-file round-trip.
3. `configHttpFile.ini` (224 keys): its name is **not** present in the MovieEditor adapter
   binary (xref negative) — role still unknown; `Init3DEngine` passes it as an argument.
4. Caps probe done for 6 options at the house pose (§4b); remaining: foliage options at a
   foliage-rich pose, per-LOD-option isolation (P5), weather param semantics + game-time
   source (P4).
5. `GpuSwitchOptionTab.tab` consumer: its header column names (`bEnableGpuCullDynamic`,
   `GpuSwitch`) appear in **no** install binary → consumer is external (launcher/device
   tooling), same boundary as the preset-file selection writer (§2.1). Re-open if a
   launcher binary becomes available locally.

## Reproduce

```powershell
# P0.1 census (stdlib; writes proof/render/*)
.venv\Scripts\python.exe tools\render\preset_census.py --out proof\render

# P0.3 xref evidence (pefile+capstone from .venv)
.venv\Scripts\python.exe tools\netcode\xref_string.py `
  "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineAdapterX64.dll" nEngineGraphicsLevel `
  --out proof\render\disasm\adapter_nEngineGraphicsLevel.txt --after 1200
.venv\Scripts\python.exe tools\netcode\xref_string.py `
  "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3UIX64.dll" nEngineGraphicsLevel `
  --out proof\render\disasm\jx3ui_nEngineGraphicsLevel.txt --after 700
.venv\Scripts\python.exe tools\netcode\xref_string.py `
  "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe" VideoType `
  --out proof\render\disasm\client_VideoType.txt --after 900
```

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| Census counts (490/409/72; 15 files) | HIGH | `preset_census.py` run 2026-10-04, outputs committed |
| Engine reads `config.ini` from cwd | HIGH | adapter load fn disasm (`config.ini` L27, per-key reads) |
| Adapter has load + save functions | HIGH | RVAs `0x5F9F0` / `0x67B10`, disasm |
| `nEngineGraphicsLevel` default rule (0/3 vs nRenderLevel==100) | HIGH | load fn L3857-3879 |
| Clamps (`nShadowType`, `nFoliageDensity`, `nSpeedTreeDensity`) | HIGH | load fn |
| UI schema function key/offset list | HIGH (sampled) | `JX3UIX64.dll` `0x118970` xref; list not yet exhaustive |
| Active `config.ini` = generated merge; owner external/panel | MED | 471-key active file vs `_bd_1` (412/467 same); full-install scan 17,843 files |
| Preset *file selection* owner (final writer) | LOW / open | preset names referenced nowhere in install; P1 probe |
| `GpuSwitchOptionTab.tab` consumer | LOW / open | string absent from the whole install |
| Runtime apply via `SetEngineOptionFromConfigFile` | HIGH | reflection dump + runs A/B/C (§4b), logs cited |
| Per-option `RC_OPT_*` merge effective | HIGH | run C vs B fingerprints |
| Isolated caps matrix (bloom effective; AO/SSR/shadow/density no-op at pose; 999 clamps to 100) | HIGH | clean reruns in §4b (`proof/render/logs/p2_*`) |
| Tier fps gap at vista pose (−34 %) | MED | single pose, one run each; concurrent agents can skew fps |
| Environment quality variants (bd/low differ; HD day-night authored) | HIGH | `environment_summary.txt`, 15 extracted files |
| Weather/day-night visible effect | LOW / open | toggle runs pixel-stable; params/time source unresolved |
| `GpuSwitchOptionTab` consumer external | MED | header column names absent from the whole install |
