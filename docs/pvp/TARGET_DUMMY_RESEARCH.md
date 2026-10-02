# Target dummies (木桩) — 主城木桩 zone + generic dummies

Scope: the attackable dummies recreated by the client sandbox
(`client/RebornClient.cs`, `RC_DUMMY`). Research is static/read-only against the
local client; live spawn stats come from the shipped
`settings\NpcTemplate\<zone>\sNpcTemplate.tab` tables. The 试炼教官 instructor
NPC is documented but **not** a dummy (excluded from the extraction index).

## 1. Inventory (HIGH)

Extract/join:

```powershell
python tools\netcode\mode\extract_target_dummies.py
# -> assets\mode\dummy\dummy_index.tsv  (gitignored local game data)
```

| Group | Name | RepresentID | NPCID | Level | MaxLife | Def (phys/magic) |
|---|---|---|---|---|---|---|
| 江湖木桩 | 江湖木桩 | 6641 | 7599 | 70 | 3,878 | 34 / 3 |
| 江湖木桩 | 初入江湖木桩 | 37026 | 25597 | 20 | 5,000,000 | 0 / 0 |
| 江湖木桩 | 初级江湖木桩 | 37023 | 25594 | 50 | 5,000,000 | 0 / 0 |
| 江湖木桩 | 中级江湖木桩 | 37024 | 25595 | 70 | 5,000,000 | 0 / 0 |
| 江湖木桩 | 高级江湖木桩 | 37025 | 25596 | 110 | 5,000,000 | 2,544 / 2,544 |
| 试炼木桩 | 初级试炼木桩 | 35901 | 24535 | 131 | 500,000,000 | 33,338 / 33,338 |
| 试炼木桩 | 中级试炼木桩 | 35902 | 24536 | 132 | 500,000,000 | 46,901 / 46,901 |
| 试炼木桩 | 高级试炼木桩 | 35903 | 24537 | 133 | 500,000,000 | 79,721 / 79,721 |
| 试炼木桩 | 极境试炼木桩 | 35904 | 24538 | 134 | 500,000,000 | 83,679 / 83,679 |
| 试炼木桩 | 极境试炼木桩·斩 | 91454 | 128490 | 134 | 500,000,000 | 83,679 / 83,679 |
| 其他木桩 | 初试木桩 | 50 | 5619 | – | 1,000,000 | 0 / 0 |
| 其他木桩 | 秘境木桩 | 6643 | 7600 | 73 | 223,916 | 75 / 7 |
| 其他木桩 | 化境木桩 | 6644 | 7601 | 73 | 24,879,587 | 119 / 10 |
| 其他木桩 | 次极境·可控木桩 | 27609 | 17733 | 80 | 31,787,476 | 130 / 11 |

Sources: `settings\NpcTemplate\ZhuChengMuZhuang\sNpcTemplate.tab` (main-city dummies,
12 rows incl. 2 instructor rows) and `settings\NpcTemplate\GongNengTongYongNPC\sNpcTemplate.tab`
(generic dummies). All 主城 dummy rows carry `ScriptName =
scripts\Map\长安\Npc\木桩不死脚本.lua` (dummy immortal script). The 主城 zone is
listed by `settings\NpcTemplateList.tab` (`NpcTemplateList.tab` in
`proof/netcode/mode_juejing/pak_out/`).

The same zone table also contains **试炼教官** (RepresentID 35905 / NPCID 24543 and
RepresentID 37029 / NPCID 25602, script `主城木桩指导师*.lua`) — the trial
instructor NPC, not an attackable dummy. Its exact service/dialog is not decoded
(MED; game logic scripts are not in the base paks).

## 2. Model resolution (HIGH, this build)

The editor host resolves the dummy RepresentIDs in actor space (same resolver the
Asset Sandbox probe showed for NPC IDs, `asset_sandbox_out\asset_sandbox.log`):

```
probe rid=37023 '初级江湖木桩' model='data\source\npc_source\练功木桩001\模型\wj_练功木桩002.mdl'
probe rid=6641  '江湖木桩'     model='data\source\npc_source\练功木桩001\模型\wj_练功木桩001.mdl'
ani='data\source\npc_source\练功木桩001\动作\wj_练功木桩001_st02.ani'
```

Two meshes exist (`wj_练功木桩001.mdl` for 初试/世界江湖木桩, `wj_练功木桩002.mdl`
for all 主城 grade dummies); grades differ by stats/heart, not by mesh.
Capture: `proof/pvp/target_dummy_sandbox_smoke_20260930.txt`.

## 3. PvP damage-test framing (MED–HIGH)

* The dummy heart buff **木桩心法属性 (ID 28496)** carries the PvP mitigation
  stats: `atMaxLifeBase(2849050)`, `atDecriticalDamagePowerBase(49183)` (化劲),
  `atToughnessBase(3957)` (御劲), `atPhysicsShieldBase(16000)` /
  `atMagicShield(16000)`, `atDecriticalDamagePowerBaseKiloNumRate(102)`
  (`proof/pvp/attributes/buff_pvp_rows.txt:49`, `buff_unit_check.txt:45`).
  化劲/御劲 are the PvP-only mitigation ratings (`attributes_and_damage.md` §5.1/§5.2).
* Buff **28487 木桩切磋中** appears in player fight-stat records as
  `{"对抗伤害测试中"}` (adversarial damage test in progress) while the recorded
  target is `对抗木桩@太原木桩X` / `初级/中级/高级/极境试炼木桩` /
  `初级/中级/高级/初入江湖木桩` (`proof/netcode/ui_skill_text_hits.txt:758,1150,1585,…`;
  player `userdata\fight_stat` — **interface-addon observation, LOW mechanism**,
  usage corroboration only).
* Caveat: the repo's stale `proof/netcode/mode_juejing/pak_out4/Buff.tab` copy
  (bundled 2026-04-28 MovieEditor data) does not contain ID 28496; the values
  above are from the PvP extraction. When combat lands, re-extract `skill\Buff.tab`
  from the live client and re-verify the row. Re-open criteria: live Buff.tab row
  for 木桩心法属性 differs.

## 4. Sandbox verification

The original browse/display app (`target_dummy_sandbox/`) was removed on
2026-10-01 — the client sandbox in §5 is the vehicle now. Its evidence stays
here as historical proof: smoke run (2026-09-30)
`TD_FLAT=1 TD_SMOKE=37023,35901,35904,91454,6644` — all spawns via
`AddRepresentModel` with valid handles; screenshots show the wooden cross-arm
dummies rendered with shadows on 龙门寻宝. Numeric fingerprint:
`proof/pvp/target_dummy_sandbox_shots_20260930.txt`. Fallback path
(`TD_PATH=1`) loads the `.mdl` through `AddDummyModel` + idle `.ani`
(`proof/pvp/target_dummy_sandbox_fallback_20260930.txt`). The smoke capture log
also contains live interactive double-click spawns from the same run (list
selection verified in-session). For the in-client spawn (one 试炼木桩 near the
player), see §5.

## 5. Client feature: one 试炼木桩 at the player spawn

`client/RebornClient.cs` spawns one dummy right after the player is placed (the
requested in-client sandbox target):

* `RC_DUMMY=<representID>` — default 35901 (初级试炼木桩); `0` disables.
* `RC_DUMMY_DIST=<units>` — distance along the measured view direction
  (default 400 u); terrain height from `TerrainSampler`.
* model from `scene.GetRepresentModelPath(rid)`, idle clip from
  `scene.GetRepresentAniPath(rid)` via `KGModelCLR.PlayAnimation`.

Build/run (feature client; canonical `reborn_client.exe` untouched):

```powershell
$env:RC_CLIENT_EXE='reborn_client_target-dummy.exe'; client\build_client.cmd
# cwd C:\SeasunGame\MovieEditor:
C:\SeasunGame\MovieEditor\bin64\reborn_client_target-dummy.exe
```

Title `sandbox-target-dummy`, namespace `reborn_client_target-dummy.memory`.
Run 2026-09-30 (`RC_AUTORUN=15000 RC_SHOTS=4000,9000,14000`): player spawn
`(23334,761,24224)` view `(0.00,1.00)`, dummy handle valid at `(23334,740,24624)`
(`proof/pvp/target_dummy_client_run_20260930.txt`); `rc_02_14000ms.png` shows the
player and the dummy standing together
(`proof/pvp/target_dummy_client_shots_20260930.txt`).

## Reproduce

```powershell
python tools\netcode\mode\extract_target_dummies.py
$env:RC_CLIENT_EXE='reborn_client_target-dummy.exe'; client\build_client.cmd
# cwd C:\SeasunGame\MovieEditor: bin64\reborn_client_target-dummy.exe
# in-world indicator + selection: docs/controls/JX3_TARGET_SELECTION.md §10
```

Last verified: 2026-10-01
