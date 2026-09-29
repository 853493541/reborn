# Engine Host Plan — JX3 MovieEditor playback core

Branch: `spike/engine-host`
Status: **Spike A PASSED (2026-09-21)** — engine host plays the real tani + SFX
Evidence: `proof/engine_host_spike/spike_t{0,2000,4000,7000}ms.png`, recipe in
`engine_host_spike/SPIKE_NOTES.md`
Status: **Spike B PASSED (2026-09-21)** — engine host loads and renders the real
龙门寻宝 map (separate app `map_spike_host.exe`; does not touch the actor spike)
Evidence: `proof/map_spike/map_tour_00.png` + `map_tour_07.png`, recipe in
`engine_host_spike/SPIKE_B_MAP_NOTES.md`

## Goal

Play real JX3 player animations and skill SFX the way MovieEditor does, by hosting
the MovieEditor engine DLLs directly in our own application (no web reimplementation,
no retargeting, no three.js skinning).

Reference target (user flow):

```
演员编辑器 -> 新建演员 -> 创建角色 -> 角色动作 -> 重剑技能15_风来吴山红色hd.tani
```

## Decisions (locked)

| Topic | Decision |
|---|---|
| Language | **C# (.NET Framework 4.8)**, built with `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, x64 |
| Why not C++ | `MovieEngineCLR.dll` is mixed-mode C++/CLI and only loads in a .NET Framework process; C++ would force manual vtable reconstruction of `IKG3D_*` interfaces |
| Engine install | **`C:\SeasunGame\MovieEditor`** (build 2026-09-14, newer than `...\zhcn_hd\MovieEditor` 2026-04-28; same version label 1.4.1346) |
| Product shape | Desktop app hosting the engine; web viewport kept only as optional catalog/preview |
| Cleanup | Archive research scripts/reports, keep parsers + staged assets + GT captures |

## Verified process chain (from binary analysis)

```
KPlayerAniform.advTree_Ani_SelectedIndexChanged
  -> new KGModelCLR(); AttachModel(m_MovieActor.GetModelHandle())
  -> KGModelCLR.PlayAnimation(KResourceInfo.Path)          [MovieEngineCLR.dll]
  -> KG3D_MovieActor::PlayAnimation                         [KG3DEngineAdapterX64.dll]
  -> VFS load (KGPK4_FileSystemX64.dll / KG_FileSysX64.dll)
  -> KG3D_AnimationTani_Data::LoadFromFile (GATA)           [KG3D_AnimationTagX64.dll]
  -> base .ani + tag groups (SFX/Sound/Motion/ForceField/Texture/CameraAni)
  -> KG3D_MovieActor::FrameMove -> KG3D_EngineEventManager::_OnProcessActiveSFXTag
  -> PSS spawn on sockets -> DX11 render
```

List source: `ResourcePack\tani.rt` / `foldertree.xml` (tani ID 5332,
shell `Root\官方资源\主角模型\小女孩\动作\`).

## Verified map (scene) display chain (Spike B, from IL)

```
MainForm::LoadMap(id, path)                     // 场景 menu entry
  -> SceneForm::LoadScene(id, path)
       m_Scene = new KGSceneCLR()               // bare instance, NO AttachScene
       r = m_Scene.LoadMap(path, EditorConfig.EnableMapAysncLoad)   // r < 0 = fail
       OnAfterLoadMap:
         SetSceneFullLoading(true)              // only if config enabled
         SetActiveEnvironment()
         KMovieCore.set_MainScene(m_Scene)
         AddView -> KGSceneCLR.AddOutputWindow  // MUST come after LoadMap
```

Map catalog: `ResourcePack\MapList.tab` (GBK TSV). 龙门寻宝 = ID 296 ->
`data\source\maps\龙门寻宝\龙门寻宝.jsonmap` (client PakV4 VFS). Per-map bundle:
`.jsonmap` (quality dirs + object counts), `.SRScene`, `.rcidx`, `_Setting.ini`,
`systemCamera.json`, `environment.json`, `playerEnvironment.json`, plus
`landscape/ foliage/ entities/ env_probe/ bd/` source folders in the pak.
Parsers/extractors: `maplist.py`, `_probe_map_files.py`.

Target content for the red FLWS tani:

| Item | Value |
|---|---|
| tani | `data\source\player\f1\动作\f1s07cj重剑技能15_风来吴山红色hd.tani` |
| base ani | `data\source\player\f1\动作\f1s07cj重剑技能15.ani` (MIN2, 182 bones, 19 keys @33fps) |
| SFX 1 | `...\pss\发招\c_藏剑刀光01b红色.pss` |
| SFX 2 | `...\pss\状态\c_藏剑风车范围_红色.pss` |
| timing | start 2000 ms, play 5000 ms, total 8000 ms (PSS-derived) |

## Phases

### Phase 1 — Spike A (go/no-go)

Minimal C# host (`engine_host_spike/`) run with cwd `C:\SeasunGame\MovieEditor`:

1. `KGEngineCLR.NewEmptyScene()`
2. `new KGMovieActorCLR()` -> `Init()` -> `LoadFromFile(<actor>)`
3. `KGSceneCLR.AppendModel(actor.GetModelHandle())`
4. `new KGModelCLR()` -> `AttachModel(handle)` -> `PlayAnimation(<tani path>)`
5. wait; capture PNG at t ~ 0 / 2 / 4 s

Exit criteria: PNG shows the character mid-风来吴山 with red blade + ring FX.

Failure handling: log init errors, try `CreateEngine`/`GetMovieEngine` P/Invoke,
then C++ host. Do not build the product before this gate passes.

### Phase 2 — Asset I/O

- P/Invoke `KG_PAKFS_OpenFileSystem` / `CreateFileSystem` for clean asset extraction
  (replaces PakV4SfxExtract for tani/ani/pss).
- GBK-safe path registry + encoding utilities (`tools/`).

### Phase 3 — Product

- Actor editor shell replicating `KPlayerCheckTool`: 演员列表 / 创建角色 / 角色动作.
- Timeline playback, PNG/GIF export, SFX inspection.
- Archive old experiments; keep parsers (`min2.py`, `mina.py`, `tani.py`) as validators.

## Key engine exports (for fallback / P/Invoke)

| DLL | Exports |
|---|---|
| `KG_EngineEditorX64.dll` | `CreateEngine`, `DestoryEngine`, `GetEngine` |
| `KG3DEngineAdapterX64.dll` | `GetMovieEngine`, `Get3DEngineInterface`, `SetEngineWorkingRootDirectory`, `KG3D_LoadFile`, ... |
| `KG3D_AnimationTagX64.dll` | `KG3D_CreateAnimationTagSystem` |
| `KG_FileSysX64.dll` | `CreateFileSystem` |
| `KGPK4_FileSystemX64.dll` | full `KG_PAKFS_*` API |
| `MovieEngineCLR.dll` | managed: `KGEngineCLR`, `KGSceneCLR`, `KGMovieActorCLR`, `KGModelCLR`, `KMovieCore` |

## Risks

- Engine init may require a window/device, config files, or load order.
- Launcher/license checks unknown at DLL level (MovieEditorHD runs with `NOTLAUCNER`).
- Mixed-mode assembly requires x64 .NET Framework host; no .NET Core.
