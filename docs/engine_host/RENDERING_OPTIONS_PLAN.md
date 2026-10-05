# Rendering / LOD / weather options — improvement plan (item 1, areas 1.8–1.10)

**Date:** 2026-10-04 · **Branch:** `agent/render-options` (worktree `reborn-iso-render-options`)
**Status:** PLAN — no client code in this branch yet. Corpus census done (this doc).
**Scope:** close the three [OPEN] rows of system 1 in `docs/GAME_SYSTEMS_RESEARCH_MAP.md`:

- **1.8** Rendering options / graphics presets
- **1.9** Weather / day-night / post effects
- **1.10** LOD / culling / performance

**Boundaries with parallel work (do not cross):**

| Other track | Owns | This plan must not touch |
|---|---|---|
| `agent/predraw` | `PreDrawSetting.ini`, startup/init override | PreDraw keys, engine init timing |
| `agent/terrain-stream-holes` | terrain streaming + holes (1.3) | `TerrainSampler`/movement/collision code |
| `agent/queue-ui-compare` | mode UI compare | `ui-process-app/` |
| `agent/skillv2-sandbox` | skill sandbox | ability sandbox apps |

---

## 1. Evidence corpus (verified 2026-10-04, read-only)

Sources: `C:\SeasunGame\Game\JX3\bin\zhcn_hd\config\` (game client), `C:\SeasunGame\MovieEditor\config.ini`
(what our host actually loads: cwd = MovieEditor). All counts from PowerShell key=`value` regex
(see Reproduce). Confidence HIGH for counts, LOW for role hypotheses not yet xref-proved.

| File | key= entries | Role (hypothesis, to verify in P0) |
|---|---:|---|
| `config.default.ini` | 343 | base/default template |
| `config_1_zuijian` … `config_7_jizhi` | 234 each | main quality tiers 1–7 (最简→极致) |
| `config_8_tansuo.ini` | 375 | tier 8 (探索; extra keys) |
| `config_9_chenjin.ini` | 241 | tier 9 (沉浸) |
| `config_bd_1..7_*.ini` | 472 each (6 files) | second profile family (`_bd_`); mapping unknown |
| `EngineStaticConfig.ini` | 3 sections + flags | static engine flags; per its header, read after `config.ini` and wins per key |
| `GpuSwitchOptionTab.tab` | 30+ capability columns | device-matched option overrides (win/ios/android rows) |
| `userconfig.ini`, `machine_config.ini` | 2 / 11 | machine detection (`GPUScore=36911`, `GPUType=2`, `VideoType=7`, `BDType=8`, `DLSS=2`, RTX 5080) |
| `MovieEditor\config.ini` | 376 | **runtime config of our host** — has all `bEnableRC_*`; NO `nEngineGraphicsLevel` (grep) |
| `MovieEditor\configHttpFile.ini` | 224 | `Init3DEngine(..., "./configHttpFile.ini")` arg; role TBD |
| `MovieEditor\MovieEngine.ini` | 33 | editor shell config |

**Tier selector found:** `[ENGINEOPTION] nEngineGraphicsLevel=1..9` in `config_1..9`
(9 distinct values across tiers). This is the key the selection path likely keys on.

**Census across the 9 main tiers:** 409 unique option keys (tool canonical,
`RENDERING_OPTIONS.md` §1; an earlier PowerShell pass counted 408); **72 keys vary across tiers**
(rest are shared boilerplate). Top varying keys (distinct values):

```
nEngineGraphicsLevel 9 · fNodeLodLowLimit 6 · fSpeedTreeAngleCull 6
fSpeedTreeShadowCullDist 5 · fSimpleModelShadowCullDist 5 · MDLRenderLimit 5
ClientSFXLimit 5 · fSimpleModelCullDist 5 · MDLRenderNpcLimit 5 · nShadowType 4
fFoliageCullDist 4 · fParticleSystemShadowCullDist 4 · fParticleSystemCullDist 4
fPointLightCullDist 4 · nRC_EnvProbeLevel 4 · fNodeLodHighLimit 4 · fCameraDistanceHD 4
nSpeedTreeDensity 4 · fSpeedTreeGlobalLodScaleTimes 4 · fAngleCull 4 · nFoliageDensity 4
fModelLodRadius 3 · nWaterEffectLevel 3 · fSceneNodeClusterLoadDistance 3 · nSpeedTreeLeafScale 3
```

Observed option groups (examples, not exhaustive): post-FX `bEnableRC_*` (~30: AO, atmospheric/
height fog, bloom, SSR, SSS, vignette, light shafts, volumetric cloud, env probe, eye protection);
LOD (`fModelLodRadius`, `nMinimumModelLod`, `fNodeLodLow/HighLimit`, `MDLRenderLimit`,
`bEnableModelLodViewAngle`); culling (`fFoliageCullDist`, `fSpeedTree*`, `fSimpleModelCullDist`,
`fParticleSystem*CullDist`, `fPointLightCullDist`, `fAngleCull`); shadows (`nShadowType`,
`*ShadowCullDist`, shadow render switches); density (`nFoliageDensity`, `nSpeedTreeDensity`,
`nSpeedTreeLeafScale`); water (`nWaterEffectLevel`); weather/day-night (`bEnableDayNightCycle`,
`bShowTrueSky`, `bEnableRC_AtmosphericFog/HeightFog`, volumetric cloud); terrain bake
(`nTerrainBakeMethod`, `bEnableRealTimeTerrainBake`, `nTerrainBakeScaleRate`); scene streaming
(`fSceneNodeClusterLoadDistance`); model counts (`MDLRenderLimit`, `MDLRenderNpcLimit`,
`ClientSFXLimit`).

Known static flags in `EngineStaticConfig.ini` (read order: `config.ini` first, static file wins):
`bEnableRagdoll=1`, `bAddPlayerPhysicsActor=0`, `bEnableSceneCollision=1`,
`bUseLODMeshCollision=1`, `bEnableDX11_1Version=1`, `bWeatherOn=1`, `bEnableJobSystemOptimise=1`.

---

## 2. Open questions (to answer, in priority order)

- **Q1 (selection path):** who reads `config_N` / `nEngineGraphicsLevel`? Game client binary xref
  candidates: `JX3ClientX64.exe` (primary), `JX3RepresentX64.dll`; is the preset applied by merging
  the file into a user config, or mapped internally by the engine from the level value?
- **Q2 (apply path):** does the engine expose runtime option setters (managed `KGEngineCLR` /
  adapter exports), or is the config file the only path? Our host loads `MovieEditor\config.ini`,
  which is install read-only — a writable route must be found or a deviation registered (P1.2).
- **Q3 (caps):** which options are clamped/no-op in the editor host, and which are shadowed by
  `GpuSwitchOptionTab` rows for this GPU? (Backlog §18 item 13: per-option caps probe.)
- **Q4 (weather data):** `environment.json` / `playerEnvironment.json` / `env_probe` semantics,
  time-of-day source, weather state machine; night map (`MapList` 297 `龙门寻宝_夜晚`) as the
  authored reference for what changes.
- **Q5 (BD family):** what `config_bd_*` (472 keys) is and when it is used (do not guess).
- **Q6 (LOD scope):** rendering-side LOD only; `bUseLODMeshCollision` is collision (1.4 owner).

---

## 3. Phases

### P0 — Static corpus & semantics (docs + tool; no engine runs)

| Task | Deliverable | Verify |
|---|---|---|
| P0.1 `tools/render/preset_census.py` (stdlib) | parses all 15 presets + `MovieEditor\config.ini` + `EngineStaticConfig.ini` + `GpuSwitchOptionTab.tab`; emits `proof/render/option_matrix.tsv` + `varying.tsv` | re-run reproduces 408 unique / 72 varying / 241–472 per-file counts; exit 0 |
| P0.2 `docs/engine_host/RENDERING_OPTIONS.md` | option reference: key, meaning, tier values, consumer (RVA/string), caps, notes | every group has ≥1 cited xref or data-path evidence |
| P0.3 Consumer scan | xref each key/group in `KG3DEngineDX11EX64.dll` / `KG3DEngineX64.dll` / `KG3DEngineAdapterX64.dll` / `JX3ClientX64.exe` with `tools/netcode/xref_string.py`, `dump_va.py`, `search_tree.py` | classify: init read / runtime setter / editor-only / static |
| P0.4 Selection path (Q1) | find `config_N` + `nEngineGraphicsLevel` readers; determine merge/write behavior | HIGH evidence: RVA + disasm transcript in `proof/render/` |

### P1 — Apply path decision

- P1.1 Inventory setter APIs: extend the client's `RC_API_DUMP` list to the engine/option wrappers;
  enumerate adapter exports (`Get3DEngineInterface`, option-related); resolve `configHttpFile.ini`.
- P1.2 Probe writable routes **without touching install files**: existing documented outputs
  (`bin64\scene_init_param.txt`), engine arguments, managed setters. If none exists → register a
  *provisional* route or deviation in `docs/EXPERIENCES.md` with re-open criteria (§6 rule).
- P1.3 Implement `GpuSwitchOptionTab` matching rule (platform/GPU/CPU/model substring, row
  precedence — verify in disasm), compute the effective override set for this machine (RTX 5080).

Exit: apply-path decision recorded in `RENDERING_OPTIONS.md` §apply-path + probe log.

### P2 — Option caps probe (runtime)

- P2.1 Feature build `RC_CLIENT_EXE=reborn_client_renderopts.exe` (title `sandbox-renderopts`,
  namespace `reborn_client_renderopts.memory`); `RC_QUALITY=<1..9>` + `RC_OPT_<key>` switches;
  log requested vs engine-effective values.
- P2.2 Probe shortlist (~12 options): `fModelLodRadius`, `nMinimumModelLod`, `fFoliageCullDist`,
  `nFoliageDensity`, `nShadowType`, `bEnableRC_AmbientOcclusion`, `bEnableRC_Bloom`,
  `bEnableRC_SSR`, `nWaterEffectLevel`, `MDLRenderLimit`, `fCameraDistanceHD`, `bShowTrueSky`.
  For each: extreme values → per-region RGB fingerprint (`tools/proof/image_stats.py`) + fps from log.

Exit: `proof/render/caps_probe/` table (option, values, fingerprint delta, fps delta, clamp/no-op).

### P3 — Host implementation: graphics preset application

- P3.1 `client/VideoOptions.cs` (C# 5): load tier preset (from the chosen route) + granular overrides.
- P3.2 wire at init (`RebornClient.cs` init block ~319–500; movement loop untouched).
- P3.3 Verify: fixed pose on sandbox map; per-tier fingerprints + fps; gates §12 green
  (`camera_smoke`, collision selftest, `jx3_model`); title/namespace per parallel rules.

Exit: `RC_QUALITY` demo with before/after numeric evidence; docs updated.

### P4 — Weather / day-night / post

- P4.1 Decode `environment.json` / `playerEnvironment.json` + `env_probe` (pak files; the local
  mini-sandbox copy under `C:\jx3tmp\reborn_sandbox` is readable) — document fields + consumers.
- P4.2 Apply/toggle `bEnableDayNightCycle`, `bShowTrueSky`, volumetric cloud (asset absent in
  install — document as boundary), fog/bloom/AO/SSR toggles.
- P4.3 Reference: authored night map 297 vs toggled day map — fingerprint comparison only
  (reference, not parity claim).

Exit: weather/post section in `RENDERING_OPTIONS.md` + demo fingerprints.

### P5 — LOD / culling / performance

- P5.1 Apply LOD/cull/density/shadow keys per tier.
- P5.2 Measure at fixed poses (spawn, house interior, dune vista): fps distribution + visual
  fingerprints per tier; flag no-ops in the editor host.
- P5.3 Groundwork only for M6 scale tests (no milestone crossing).

Exit: measurement table + recommended default host preset.

---

## 4. Verification & gates

- After any client change (P2+): feature build via `RC_CLIENT_EXE`; scripted demo (`RC_DEMO`);
  numeric fingerprints via `tools/proof/image_stats.py`; §12 gates green from this worktree.
- Docs/tool phases: `preset_census.py` deterministic counts (exit 0) as the repeatable check.
- Engine runs use our own namespace; no shared state writes (root AGENTS §2).

## 5. Risks & constraints

1. **Install read-only** — cannot write `MovieEditor\config.ini`; if no setter API exists the
   feature needs a registered provisional route + re-open criteria.
2. **Static options** — some keys are static by design (`EngineStaticConfig.ini` header); do not
   force runtime toggles for those.
3. **Device overrides** — `GpuSwitchOptionTab` may silently override options (TAA/SSGI/GSR2/
   TressFX/cull flags); effective value = preset ∩ device row.
4. **Shared engine root** — ShaderListUpload/dxvk writes; concurrent-run flakiness is a
   shared-write suspect first.
5. **`_bd_` family unknown** — census only, no guessing.
6. **Missing assets** — e.g. `volumetricCloud.json` absent in install (non-fatal, documented
   in `MINI_SANDBOX_CLIENT.md`); do not invent substitutes.
7. **Don't touch** other agents' files (see boundaries table).

## 6. Deliverables summary

- `docs/engine_host/RENDERING_OPTIONS.md` (semantics + consumers + apply path + caps)
- `tools/render/preset_census.py` + `proof/render/*` evidence
- `client/VideoOptions.cs` + `RC_QUALITY`/`RC_OPT_*` (P2+) with fingerprints
- `docs/EXPERIENCES.md` entries per phase

## Reproduce (census numbers in §1)

```powershell
$cfg = 'C:\SeasunGame\Game\JX3\bin\zhcn_hd\config'
# per-file key counts
foreach($f in Get-ChildItem $cfg -Filter 'config*.ini' | Sort-Object Name){
  $c=0; foreach($l in Get-Content $f.FullName){ if($l -match '^\s*[A-Za-z_0-9]+\s*='){$c++} }
  "{0,-28} {1,4}" -f $f.Name, $c }
# unique/varying keys across the 9 main tiers
$names = 1..9 | ForEach-Object { Get-ChildItem $cfg -Filter "config_${_}_*.ini" | Select-Object -First 1 }
$map=@{}; foreach($f in $names){ foreach($l in Get-Content $f.FullName){
  if($l -match '^\s*([A-Za-z_0-9]+)\s*=\s*(.*?)\s*$'){
    $k=$Matches[1]; $v=$Matches[2]
    if(-not $map.ContainsKey($k)){ $map[$k]=New-Object System.Collections.Generic.HashSet[string] }
    [void]$map[$k].Add($v) } } }
"unique: $($map.Count)"; "varying: $(@($map.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 }).Count)"
# runtime file of the host
Select-String -Path 'C:\SeasunGame\MovieEditor\config.ini' -Pattern 'nEngineGraphicsLevel|bEnableRC_' | Select-Object -First 5
```

## Confidence

| Claim | Conf. | Source |
|---|---|---|
| Corpus counts (§1 table) | HIGH | PowerShell census 2026-10-04 (commands above) |
| `nEngineGraphicsLevel` is the tier selector | HIGH | key present 1..9 in `config_1..9` `[ENGINEOPTION]`; selection reader TBD (P0.4) |
| MovieEditor `config.ini` is the host's loaded config | HIGH | cwd = MovieEditor + keys present; consumption xref TBD (P0.3) |
| Option roles/groups | MED | key names + values only; xrefs TBD (P0.3) |
| `_bd_` family role | LOW | unknown; census only |
