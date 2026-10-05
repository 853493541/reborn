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

## 4b. Runtime proof (P2/P3/P5 first cut, 2026-10-04)

Feature build `reborn_client_renderopts.exe` (title `sandbox-renderopts`, namespace
`reborn_client_renderopts.memory`); fixed pose `(18991,962,33853)`, 龙门寻宝, no input,
shot at t=15 s. Screenshots in `proof/render/runs/`; logs `bin64\reborn_out\reborn_*.log`.

| Run | Config | Log | Screenshot | Frame mean | fps (t=18 s) |
|---|---|---|---|---|---|
| A | tier 1 (最简) | `reborn_20261004_231520.log` | `quality1.png` | `#BAB197` | 536 |
| A' | tier 1 repeat | `reborn_20261004_231852.log` | `quality1b.png` | `#BAB197` (11-byte delta) | 597* |
| B | tier 9 (沉浸) | `reborn_20261004_231631.log` | `quality9.png` | `#C1BBA6` (+7 on every 4×4 cell) | 391 |
| C | tier 9 + `nFoliageDensity=5`, `fFoliageCullDist=5000`, `bEnableRC_Bloom=0` | `reborn_20261004_231943.log` | `quality9_override.png` | `#BAB198` (tier-1-like) | 288* |
| D | tier 1 + `RC_WEATHER=1` | `reborn_20261004_232104.log` | `weather1.png` | `#BAB197` (identical to A) | 580* |

- **P3 apply path works**: A vs B differ in every region (post-FX/fog/CSS changes); repeat
  A vs A' differs by a single 11-byte PNG delta → the tier delta is causal, not noise.
- **P2 per-option overrides work**: C (three `RC_OPT_*` merged into a generated file)
  lands near tier 1 despite the tier-9 base.
- **P5 initial**: at this pose tier 9 costs ~27 % fps vs tier 1 (391 vs 536, both runs
  before other clients started).
- **P4 partial**: `EnableDynamicWeather(1)=0` (success code) but run D is pixel-identical
  to A at this pose → the toggle needs params/time/scene support; params API exists
  (`SetDynamicWeatherParameters`, 12 floats) — semantics still open.
- \* fps of A'/C/D are contaminated by a concurrently initializing `reborn_client_predraw.exe`
  (another agent, own namespace — allowed by §2); use A vs B for the tier signal.

## 4c. Weather/day-night (P4) — found API + open semantics

- `KGEngineCLR.EnableDynamicWeather(int)`, `IsEnableDynamicWeather(ref int)`,
  `Set/GetDynamicWeatherParameters(12 floats)` — the engine's authored weather entry point.
- Config keys `bEnableDayNightCycle` (cfg +0x2f0), `bEnableDynamicEnvironment` (+0x2f4),
  `bShowTrueSky` (+0x50), `bWeatherOn` (EngineStaticConfig) can be applied through the same
  config-file route.
- Open: parameter semantics + what makes weather visible (scene/time); `environment.json` /
  `playerEnvironment.json` decode; volumetricCloud asset absent in this install (non-fatal).

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
4. Caps probe first cut done visually (§4b); device-row overrides (P1.3) and
   `environment.json` decode + weather param semantics (P4) remain.

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
| Weather toggle API exists, no visible effect w/ defaults | MED | run D pixel-identical; params semantics open |
