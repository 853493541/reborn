# Asset Sandbox

Browse and display the 绝境战场 runtime doodad assets on the real map with the
real engine (MovieEditor DLLs).

The client ships the display table `represent/doodad/doodad.txt` (global
`RepresentID` -> model / material / idle-revive-die-hit animations / SFX / scale /
minimap flags). `DoodadTemplate.RepresentID` joins straight into it. The sandbox
lists every unique RepresentID used by the `沙漠风暴` / `沙漠风暴_寻宝模式` loot
sets (tiers, consumables, death bags, decoys, disguises, event chests, 天原 props)
and spawns the selected ones on 龙门寻宝. 林海 rows are excluded from the index
entirely; the set checkboxes default to 白龙 only (none checked = all).

## Data (once)

```powershell
python tools\netcode\mode\extract_doodad_represent.py
# -> assets\mode\doodad\{doodad.txt, DoodadTemplate.tab, doodad_index.tsv}
```

## Build + run

```powershell
asset_sandbox\build_asset_sandbox.cmd
asset_sandbox\run_asset_sandbox.cmd
```

The run script sets `AS_DATA` to `assets\mode\doodad` and starts the exe with cwd
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
| double-click list | spawn selected asset at the stage (replaces previous) |
| Spawn / Spawn x5 | spawn selection as a grid / a row of five |
| Remove last / Clear | despawn |
| Shot | screenshot to `bin64\asset_sandbox_out\` |

Spawn uses `AddDummyModel` with the model path from `doodad_index.tsv` and plays
the idle `.ani` when present. The row's `IdleSFXFile` (`.pss`, the colored
green/blue/purple/orange loot light) is spawned as a second dummy model at the
same transform with scale `EffectScale * AS_GLOW_SCALE` — the engine's model
loader handles PSS particle systems, so the real effect plays.

`AS_REPRESENT=1` switches to `KGSceneCLR.AddRepresentModel`; note the MovieEditor
host resolves that ID space to actors/NPCs (the game's doodad table
`represent/doodad/doodad.txt` is loaded by `JX3DoodadRepresent` in the game
logic), so the result is a different model.

## Env

| var | meaning |
|---|---|
| `AS_DATA` | data dir (default `<exeDir>\asset_sandbox_data`) |
| `AS_MAP` | map jsonmap (default 龙门寻宝) |
| `AS_SMOKE=1,46396,...` | spawn these RepresentIDs, screenshot, exit |
| `AS_SHOTS=ms,...` | screenshot times |
| `AS_AUTORUN=ms` | exit after N ms |
| `AS_REPRESENT=1` | use the host represent API (actor space) |
| `AS_SPACING` | stage grid spacing, world units |
| `AS_GLOW_SCALE` | multiplier on the table `EffectScale` for the glow PSS |
| `AS_STAGE=x,y,z` | explicit stage center |

Smoke example:

```powershell
$env:AS_SMOKE='46396,46457,47182,46536,46428,48473,50157,48371'
asset_sandbox\run_asset_sandbox.cmd
```
