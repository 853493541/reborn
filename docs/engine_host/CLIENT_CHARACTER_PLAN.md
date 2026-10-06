# Client character plan — visible player model on the real game client engine

Status: active. Last verified: 2026-10-04 (host probes, see `docs/EXPERIENCES.md` 2026-10-04 entries).

Goal: the player character (f1 model) visible in `client_host` (real game client DLLs:
`zhcn_hd\bin64\JX3RepresentX64.dll`, `Engine_Lua5X64.dll`, `KG3DEngineDX11EX64.dll`),
then ported to the sandbox app. MovieEditor is a reference/visual resource only.

## What is proven working (2026-10-04)

- Engine host boot: window, D3D11 device, map scene, camera, per-frame paint, screenshot.
- The engine renders the **static merged world** (terrain + props/skybox). Dynamic
  actors (NPC/player) are NOT drawn by this path.
- `keepCheck` (0xC4D430) gates render-data build: props/skybox = 1 (rendered),
  every character mesh (npc_source/player) = 0. Registry is PakV4-sourced
  (`KeepMeshData_FileList.tab`); loose overrides are ignored.
- Forcing `keepCheck=1` for `f1_3094` in the host hook builds the render data
  (`flag484=0x01`, kept buffers) — necessary but NOT sufficient for a draw.
- JSON-added worldObjects (`entities/sceneinfo_full/000_000.json`) create scene objects
  (visible in the SO list) but are never drawn, even with a full rendering-prop record —
  the render set comes from the map's `.SRScene` binary, not the editor JSON.
- Client represent (`JX3RepresentX64.dll`) standalone pieces working: `CreateRLLoader`
  (0xC0 env tag), `JX3ResourceConvertX64.dll` + `KG_GetConvertResource`,
  `LoadPlayerAllModel -> 1`, RL Lua state (`rep+0x5BC9F0(singleton+0x25BC0)`),
  HangPetWorld ctx (`0x42DAD0`), player unit `GetUnit("F1")` returns a KGRL unit ("RL00"),
  RL scene via `NewScene(mgr,1)` once `singleton+0xB0` (engine manager) is set.
- `SO3Represent::Init(Param)` (singleton vt[0], 0xD0 Param) requires 15 non-null objects
  (8 engine interfaces + pDispatcher + pRLUIHandler + pSO3World + pSO3WorldClient +
  pSO3UI + pEventCommonMgr + pLogicEventMgr + pRepresentEventMgr + pStepCtrl).
- `CreateRLScene` (0xB0B5C0) additionally needs `singleton+0x100` (m_pSO3World) and
  `[mgr+0x260]` (facade file bundle); it faults without them.
- `KSO3World::Init_ForEditor` (logic module 0x12B7E0, 2nd arg unused) is the editor
  world init — the intended non-game host path. `KMemory::Initialize` (Engine_Lua5X64
  export `?Initialize@KMemory@@YAHQEBD@Z`) is required before its 5.6 MB allocation.
  Current blocker: Init_ForEditor faults in a null hash-map lookup
  (logic module +0xE0998, `[rbp+0x30]` with rbp=NULL) — a logic-module global that its
  own module init normally sets.

## Plan

### Phase A — boot the logic module properly (SO3World + client world)
1. Call `CreateJX3LogicOperation` (logic export 0x8BFC0) with its two required args
   (both asserted non-null; disassemble its callers/strings to identify them) instead of
   hand-constructing SO3World — it runs the module's own init (0x8B6B0) and sets globals.
2. Verify `KSO3World::Init_ForEditor` completes and locate/instantiate the client world
   (`pSO3WorldClient`; class `KGSO3WorldClientInterface` in the logic module).
3. Keep `KMemory::Initialize` before any logic-module allocation.

### Phase B — fill the full represent Param and run the real Init
1. Build the 0xD0 Param with: the 8 engine interfaces (already live in-host), the
   logic worlds from Phase A, pSO3UI via `JX3UIX64!CreateSO3UI`, and the remaining
   managers (dispatcher, event managers, RLUIHandler, step ctrl) — find their creators
   in the exe's `KJX3RepresentModule::Initialize` (0xBC150) holder reads or the logic
   module; supply non-null placeholders only where the object is optional.
2. Call `singleton->vt[0](&param)` (SO3Represent::Init). The assert chain names any
   missing object; iterate until Init returns 1.
3. Remove the host adaptations (singleton+0xB0/+0x100 field writes) once Init runs.

### Phase C — create the character and prove it
1. `CreateRLScene` with the sandbox map + scene name (id 2), then `GetRLScene`.
2. Create the player: `LoadPlayerAllModel` + `GetUnit("F1")` + `CreateHangPet`
   (master = the real local character once the logic world exists; the fake-master
   deviation is retired), or the RL actor path (`RLActorNT` create + model from unit).
3. Numeric proof: `tools/proof/image_stats.py` region RGB before/after + screenshot;
   proof file under `proof/`; regression gates per AGENTS §12.

### Phase D — sandbox app port
1. Move the proven host sequence into `ability_sandbox`/`asset_sandbox` (client-engine
   build) with the feature title `sandbox-<slug>` and its own memory namespace.

## Current gates (2026-10-04, after Phase A/B done) — drive to the first visible character

Phase A (logic boot) and Phase B (`SO3Represent::Init -> 1`) are DONE; the resource
manager (`[singleton+0x1A0+0x260]` = MapConverter via `CreateRLFile` + rep file-IO),
the 3D-scene `vt[0x70]` bind (GBK map path), and the holder table loader
(`rep+0x82C3C0`, 18 entries) are solved. Remaining gates, in order:

- **Gate 1 — real `CreateRLScene` completes.** It now runs deep into the map load
  (`rep+0x58D800`) and ends in a C++ throw (VEH at `VCRUNTIME140+0x17B0`). Probe:
  log the exception record + the throw site (VEH `ExceptionRecord` / stack), identify
  the missing input, load it the game's way. **Checkpoint: `real CreateRLScene` clean +
  `GetRLScene(2)` non-null through the game's own path (3DScene attached).**
- **Gate 2 — char chain on the real scene.** `0x58CE20` (scene -> `0x924B(sceneId)` ->
  `[world+0xF29E8]` -> `0x1B9D7` -> `[x+0x20]+0x70`). Currently faults on the manual
  scene. **Checkpoint: the chain returns a non-null world/char on the real scene.**
- **Gate 3 — local player exists.** `[world+0xF29E8]` is 0 (the game's logic world
  creates the player). Find the logic-side creation (CreatePlayer/AddUnit in the logic
  module; `LuaCreateHangPet` 0x5BE120 needs a `pCharacter`). **Checkpoint:
  `[world+0xF29E8] != 0` and the player unit is in the scene.**
- **Gate 4 — character visible + proof.** Attach the model (RL loader/actor path),
  ensure the RL scene renders in the view, capture screenshot + `image_stats.py`
  region fingerprint into `proof/`. **Checkpoint: the f1 character is on screen
  (numeric proof + image).**
- **Gate 5 — port to the sandbox app** (`Skill.exe`, `sandbox-<slug>` title, own
  memory namespace).

Working agreement: drive to the next checkpoint autonomously and report only at
checkpoints (or a hard block with the exact missing evidence); no per-blocker reports.
Each gate may expose 1-2 sub-blockers; the count above is the honest known minimum.

## Evidence
- `docs/EXPERIENCES.md` 2026-10-04 entries; `host_char_rl*.out`, `host_f1keep*.out`,
  `host_so3world*.out`, `rl_sowinit.txt` (Init_ForEditor), `rl_initbody.txt` (Param).
