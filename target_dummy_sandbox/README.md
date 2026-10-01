# Target Dummy Sandbox

Browse and display the real JX3 木桩 (target dummy) NPCs on a real map with the
real engine (MovieEditor DLLs). Dummies only — the 试炼教官 instructor rows are
excluded by the extractor.

> This is the browse/verification tool. The requested in-client dummy (one
> 试炼木桩 next to the player spawn) is in `client/RebornClient.cs` —
> `RC_DUMMY`/`RC_DUMMY_DIST`, see `docs/pvp/TARGET_DUMMY_RESEARCH.md` §5.

## Data (once)

```powershell
python tools\netcode\mode\extract_target_dummies.py
# -> assets\mode\dummy\{sNpcTemplate_ZhuChengMuZhuang.tab,
#                       sNpcTemplate_GongNengTongYongNPC.tab, dummy_index.tsv}
```

The zone tables are the shipped `settings\NpcTemplate\<zone>\sNpcTemplate.tab`
rows the real client spawns. `dummy_index.tsv` joins them (14 dummies, 3 groups):

| group | dummies (RepresentID / NPCID) |
|---|---|
| 江湖木桩 | 江湖木桩 6641/7599, 初入 37026/25597, 初级 37023/25594, 中级 37024/25595, 高级 37025/25596 |
| 试炼木桩 | 初级 35901/24535, 中级 35902/24536, 高级 35903/24537, 极境 35904/24538, 极境·斩 91454/128490 |
| 其他木桩 | 初试 50/5619, 秘境 6643/7600, 化境 6644/7601, 次极境·可控 27609/17733 |

Each row carries the live spawn stats (Level / MaxLife / PhysicsShieldBase /
NeutralMagicDefence) and the dummy control script
`scripts\Map\长安\Npc\木桩不死脚本.lua`. The 主城 试炼木桩 run at
`MaxLife=500,000,000` with 33k–83k defences; the PvP damage-test framing is in
`docs/pvp/TARGET_DUMMY_RESEARCH.md`.

## Build + run

```powershell
target_dummy_sandbox\build_target_dummy_sandbox.cmd
target_dummy_sandbox\run_target_dummy_sandbox.cmd
```

The run script sets `TD_DATA` to `assets\mode\dummy` and starts the exe with cwd
`C:\SeasunGame\MovieEditor`.

## Controls

| input | action |
|---|---|
| right-drag | rotate camera |
| Alt/Shift + right-drag | pan camera |
| wheel | zoom |
| W/A/S/D/Q/E | move camera, Shift = fast |
| R | reset camera |
| F | focus |
| double-click list | spawn selected dummy (replaces previous) |
| Spawn / Spawn x5 | spawn selection as a grid / a row of five |
| Rot 45 | rotate the next spawn yaw by 45° |
| Remove last / Clear | despawn |
| Shot | screenshot to `bin64\target_dummy_sandbox_out\` |
| Ref | spawn the 花萝 actor (~1.7 m) as a scale reference |

## Display path

1. `KGSceneCLR.AddRepresentModel(RepresentID)` — the editor's own actor/NPC
   spawn (the path the MovieEditor NPC palette uses).
2. Fallback `AddDummyModel(name, GetRepresentModelPath(RepresentID))` +
   `KGModelCLR.PlayAnimation(GetRepresentAniPath(RepresentID))`.

Both resolve against the host's actor space; the probe log records every ID:

```
probe rid=37023 '初级江湖木桩' model='data\source\npc_source\练功木桩001\模型\wj_练功木桩002.mdl'
ani='data\source\npc_source\练功木桩001\动作\wj_练功木桩001_st02.ani'
```

Two meshes exist: `wj_练功木桩001.mdl` (初试木桩 50 / 江湖木桩 6641) and
`wj_练功木桩002.mdl` (all 主城 dummies + the 秘境/化境/次极境 rows). Use
`TD_PATH=1` to force the `AddDummyModel` path.

## Env

| var | meaning |
|---|---|
| `TD_DATA` | data dir (default `<exeDir>\target_dummy_sandbox_data`) |
| `TD_MAP` | map jsonmap (default 龙门寻宝) |
| `TD_SMOKE=id,...` | spawn these RepresentIDs, screenshot, exit |
| `TD_SHOTS=ms,...` | screenshot times (smoke default 2500,5000,8000) |
| `TD_AUTORUN=ms` | exit after N ms |
| `TD_PATH=1` | skip AddRepresentModel; use the model-path fallback |
| `TD_SCALE` | scale for the fallback path (default 1) |
| `TD_YAW=<deg>` | initial spawn yaw (default 180) |
| `TD_REF=1` | 花萝 actor (~1.7 m) scale reference |
| `TD_STAGE=x,y,z` / `TD_FLAT=1` | explicit stage center / flat pad |
| `TD_SPACING` | stage grid spacing (default 250) |
| `TD_CAM_DIST` / `TD_CAM_UP` | initial camera distance / height (default 1100/350) |

Smoke example:

```powershell
$env:TD_FLAT='1'; $env:TD_SMOKE='37023,35901,35904,91454,6644'
target_dummy_sandbox\run_target_dummy_sandbox.cmd
```

Runtime isolation: memory namespace `TargetDummySandbox.memory`, logs and
screenshots in `bin64\target_dummy_sandbox_out\` (own runtime dir; the shared
engine root caveat still applies).
