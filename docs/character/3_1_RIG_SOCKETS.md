# 3.1 Rig / body parts / head attach — full research (2026-10-06)

**Area:** character · **Branch:** research/character-animation · **Index:** `docs/character/README.md`
Evidence: game client + MovieEditor installs (read-only), symbol/RVA or path:line citations, HIGH/MED/LOW tags. Key evidence commands in "Reproduce".

Scope: player actor composition, skeleton, attach points/sockets, body-part slots, and the
engine API a host can call. Builds referenced:

- Game client: `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64` (truth for game mechanisms).
- MovieEditor: `C:\SeasunGame\MovieEditor\bin64` (truth for the host: the reborn client
  loads MovieEditor engine modules; `client/EngineRay.cs:99` uses
  `GetModuleHandleA("KG3DEngineDX11EX64.dll")`).
- Scratch evidence: `C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\` (this task).

Confidence tags: HIGH = disassembled/decoded data reproduced here; MED = string/struct
evidence without full behaviour trace; LOW = single indirect hint.

---

## Verified facts

### Actor format and body-part composition

1. **`.actor` is a GBK text INI** with `[ROOT]` + one `[PartN]` section per body-part slot.
   Root keys seen: `DependModel`, `FaceDefIni`, `MetaFaceDefJson`, `BodyDefIni`,
   `HairDyeingIni`, `bReDress`, `PartNum`, `BindNum`; part keys: `Have`, `Mesh`, `Mtl`,
   `Detail`. Evidence: `C:\SeasunGame\MovieEditor\source\花萝无动作.actor` (1892 B; decoded
   in scratch `hualuo.actor`), downloader copies
   `...\SeasunDownloaderV2.4\seasun\editortool\movieeditor\source\plot\actor\*.actor`,
   `...\source\actor\loli1.actor`, `...\source\主角替换模型\*.actor` (all same shape). HIGH.
2. **花萝 (F1) actor composition** (`花萝无动作.actor`): `DependModel=
   data\source\player\f1\部件\mdl\f1.mdl`, `PartNum=16`, `BindNum=0`; active parts:
   P1 head `f1_1004_head_hd`, P2 body `f1_2227_body_hd`, P3 leg `f1_2227_leg_hd`,
   P4 hand `f1_2227_hand_hd`, P5 belt `f1_2227_belt_hd`, P6 plait `f1_1004_plait_hd`,
   P7 bang `f1_1004_bang_hd`, P8 face `f1_1004_face_hd`, P9 hat `f1_2227_hat_hd`,
   P12 lglove `f1_1004_lglove_hd`, P13 rglove `f1_1004_rglove_hd`; P10/11/14/15/16 `Have=0`.
   `Detail=0,0` on all. HIGH (exact file bytes).
3. **Slot semantics across bodies** (observed): P1=head, P2=body, P3=leg, P4=hand,
   P5=belt, P6=plait, P7=bang, P8=face, P9=hat, P12=lglove, P13=rglove. Cross-check:
   `m2_1018_沈眠风cl.actor` (P2 body, P4 hand, P5 belt, P8 `m2_1018沈眠风_face_hd` — a
   named meta-face), `pmode1.actor` (P1 head `f2_5071_head_hd`, P2 body, P8
   `f2_new_face_hd`; `PartNum=11`), `f1主角模.actor` (P8 `f1_new_face_hd`,
   `FaceDefIni=data\public\Face\F1\F1_001.ini`, P2 `Detail=-1,0`). Unused slots 10/11/14/15/16
   semantics unknown. Slot names P1–P13 HIGH from content; 10/11/14/15/16 LOW.
4. **`Have=0` wins over a listed Mesh**: m2 actor lists `m2_2201_leg.mesh` under P3 with
   `Have=0`; the mesh is ignored. HIGH (bytes + `samples/actors/SOURCES.txt` note
   `status=MISSING_LOOSE / Have=0_in_actor_but_path_listed`).
5. **`DependModel` is the per-role skeleton/model stub**: `f1.mdl` (283 B, staged at
   `samples/mesh/m2.mdl`); `player.txt` names the runtime MDL per role
   (`Data\source\player\F1\部件\Mdl\F1.mdl` for rtLittleGirl). Extracted at
   `C:\jx3tmp\common_probe\out\Represent\player\player.txt` (GBK TSV, 7 rows). HIGH.
6. **Game-client side has no `.actor` at runtime**: actor composition is editor-side
   (all `.actor` refs are under MovieEditor/downloader `editortool` trees; no `.actor`
   under client paks extracted). The runtime side composes from `Represent/player/*`
   tables: `player.txt`, `player_part_animation.txt`, `player_socket_offset.txt`,
   `player_animation_socket_hide_config.krl.txt`, `player_socket_dynamic_object.txt`,
   `player_socket_dynamic_sfx.txt`, `link\slot_link.txt`, `link\object_link_config.krl.txt`,
   `player_weapon_animation.txt`, `player_hair_color.txt`. Evidence: full listing of
   `C:\jx3tmp\common_probe\out\Represent\player\`; negative: no `.actor` found under
   `C:\jx3tmp` or the downloader tree's Represent output. MED-HIGH.
7. **Editor managed API for parts/bind** (MovieEngineCLR + MovieEditorHD.exe):
   `ActorEditorCommandHelper::ChangePartMeshAndMtl(KGMovieActorCLR, string, EnumResourceType,
   EnumPartType, int)`, `BindToSocket(KGMovieActorCLR, long, string, float, float, float)`,
   `UnBind`, `ClearBind`; `SceneForm::LoadObjInHand(string, EnumResourceType, int)`.
   Evidence: `engine_host_spike/recon_managed_api.txt` (UTF-16; scratch UTF-8 copy
   `recon_managed_api_utf8.txt` lines 1528-1538, 1968). HIGH for the signature, MED for runtime use
   (these live in `MovieEditorHD.exe`, not in the host's referenced assemblies).

### Skeleton / bones

8. **Bone naming is `bip01 <part>` with spaces, lowercase**, plus auxiliary bones. MIN2
   skel clips decoded with `min2.load_min2_stick`:
   - `samples/player/m2b02yd奔跑.ani`: **191 bones**, fps 33; idx0 `bip01`, 1 `bip01 pelvis`,
     7/15/24 `bip01 spine`/`spine1`/`spine2`, 28 `bip01 neck`, 40 `bip01 neck1`, 49
     `bip01 head`, 65/67 `bip01 l/r hand`, 74 `b_lh`, 80 `b_rh`, 58 `b_hat`, 8 `b_lp`,
     9 `b_rp`, 30/31 `b_lc`/`b_rc`, 18 `b_spine`, 32 `b_spine2`, 33 `b_long`, 51/53
     `bone_shd_l/r`, 61/64 `bone_l/r_armtwist`, 26/28 `bone_l/r_thightwist`, then
     `add_*`, `cbr4_*`/`cb3_*` hair bones.
   - `samples/player/moves/from_mapviewer/f1/动作/f1b02yd行走.ani`: **182 bones**, fps 28;
     idx38 `bip01 head`, 47 `b_hat`, 67 `b_lh`, 73 `b_rh`, 19 `b_spine`, 36 `b_spine2`,
     37 `b_long`.
   HIGH (reproduced decodes; script: `min2.py` at repo root).
9. **The 182-bone number in the docs is clip/body-specific**: m2 action clips 191, f1 182.
   (Correction row below.) HIGH.
10. **`ResourcePack\BoneNameMap.ini`** maps 215 bone names to readable CN/EN names
    (`bip01 head` → 头/Head, `bip01 neck1` → 脖子/Neck, `bip01 spine2` → 胸椎/Thoracic
    Spine, `bone_shd_l` etc.). Used by the editor's bone selector
    (`LoadBoneNameMap`/`GetBoneReadableName`, recon_managed_api lines 1314-1315). HIGH.
11. **FBX evidence (legacy)**: `samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx`
    contains the same names (`bip01 head`, `bip01 pelvis`, `bone_l_armtwist`,
    `bone_shd_l/r`, `b_*`), proving the naming survived the map-viewer conversion.
    MED (converted artifact, player-era).

### Sockets / attach points

12. **Socket vocabulary**: `C:\SeasunGame\MovieEditor\ResourcePack\Socket.tab`
    (`SocketName\tSocketText`, 19 rows): `s_fxtop` 头顶特效, `s_fxmid` 胸口特效,
    `s_hat` 头部, `s_face` 颈部, `s_lc` 左肩, `s_rc` 右肩, `s_spine2` 背部, `s_long` 长柄,
    `s_spine` 后腰, `s_lp` 左腰, `s_rp` 右腰, `s_lh` 左手, `s_rh` 右手, `s_epee` 重剑,
    `s_flute` 笛子, `s_fire` 枪口火焰, `s_bow` 千机匣背挂, `s_hs` 马背, `s_bale` 马背偏后. HIGH.
13. **Socket → parent bone map**: `ResourcePack\SocketToParentBone.ini` (`[Dummy] Count=54`,
    each `Name/Parent/Type/Matrix`; read by `KActorBone::InitSocketNode`, string at
    MovieEngineCLR 0x2E7AB0): `s_face → Bip01 Head`,
    `s_hat → B_Hat` (the `b_hat` bone), `s_rh → B_RH`, `s_lh → B_LH` (+`s_lh02 → B_LH`),
    `s_lp/s_rp → B_LP/B_RP`, `s_lc/s_rc → B_LC/B_RC`, `s_spine → B_Spine`,
    `s_spine2/s_cloak/s_movespine2 → B_Spine2`, `s_long → B_Long`, `s_epee → B_Spine`,
    `s_epee`/weapon-back sockets → spine bones, `s_shoubao/s_fan → B_RP`, etc. Matrix
    linear part is ±0.393701 (=1/2.54) with translation in cm — unit conversion not yet
    traced (MED on the scale). HIGH for the mapping.
14. **`s_fxtop`/`s_fxmid`/`s_hs`/`s_bale` are not in `SocketToParentBone.ini`**; they are
    engine/mesh-provided attach points (`s_fxmid` is the universal missile anchor, `s_hs`
    the mount seat used by `slot_link.txt`). NEGATIVE (searched all 54 entries). MED.
15. **Mesh-level sockets**: the engine exposes per-mesh socket data with a parent bone:
    `KG3DMesh::GetSocketName` (game KG3DEngineX64 string RVA 0x006ABCD8),
    `KG3DMesh::GetSocketParentBoneName` (0x006ABD30), `KG3DMesh::GetSocketCount` (0x006ABD98),
    plus `KG3DModel::GetSocketName` (game build function @0x1801F17E0). MED (API proven by
    strings; per-mesh socket contents not yet extracted).
16. **Skeleton "dummies" are a second attach mechanism**: `KG3DSkeleton::LoadDummyFromIni`
    (game string RVA 0x006A7FB0), `Dummy%d` (0x006A7FF0), `%s Dummy %d Parent bone %s not
    found.` (0x006A8008), `m_pDummyMatrices` (0x006AAA10) — attach points loaded from an
    ini and parented to bones. MED.
17. **`buff_bullet.txt` (182 rows, 34 cols)**: decoded header (proof) col6 `绑定插槽` (bind
    socket) = **`S_spine` in 180/180 data rows**; col7 `目标插槽/骨骼` (target socket/bone) =
    **`S_fxmid` in 180/180**. Evidence: `proof/collision/missile/buff_bullet.txt` +
    `buff_bullet_header_utf8.txt`. HIGH.
18. **Skill-chain binds `S_rh` → `S_fxmid`**: 孤风飒踏/临时飞爪 chain (skill_chain 28032)
    starts at the caster's `S_rh` (right hand) and ends at the device NPC's `S_fxmid`;
    `KRLSfx::Init` + two-point bind, per-frame `UpdateSkillChain` rebind. Evidence:
    `ability_picker/data/ability_candidates.json` line 41 (engine-decoded notes). HIGH for
    this chain; MED generalising to all chains.
19. **Face binding is by socket name at the represent layer**:
    `m_prlFaceDefinition->BindTo(m_rlActorHelper.GetRLModel(), "s_face")` and
    `m_prlModel->BindTo(m_pCharacter->m_rlActor.GetRLModel(), "s_face")`
    (JX3RepresentX64 strings VA 0x00CB2240 / 0x00CB2820). HIGH (string + call site
    structure; bind objects not traced).
20. **Per-role socket offsets** `Represent/player/player_socket_offset.txt`: columns
    体型/表现位/表现ID/插槽名称/偏移值X-Y-Z; e.g. RoleType 1 `S_Epee`(0,5,0)+`S_Spine2`(0,0,-10)
    at 表现位5 ID553; RoleType 6 (F1) `S_Epee`(0,10,0)+`S_Spine2`(0,0,-6) ID553,
    `S_movejian01`(0,-12,0) ID73. HIGH (extracted table).
21. **Socket hiding is per-animation** `player_animation_socket_hide_config.krl.txt`:
    scheme IDs → hidden socket sets (e.g. 4={s_lh,s_rh}, 12=hides nearly all weapon/back
    sockets, 16 adds `s_fan`). HIGH (extracted file).
22. **Socket extension renames** `player_extend_socket_adjust.krl.txt`:
    `s_qin01 → s_qin02 (BackExtend), s_moveqin01 (Cloak), s_moveqin02 (BackExtend:Cloak)`;
    same for `s_jian01`, `s_bigbrush`. HIGH.
23. **Link/dummy objects use the same sockets**: `link\slot_link.txt` BindSocket values
    `s_hs` (mount), `s_rh`, `s_lh`, `b_rp`, CaptionSocket `Bip01`; `link\object_link_config.krl.txt`
    `PlayerHideSocket.Normal` list (s_lh, s_rh, s_spine, s_spine2, s_cloak, s_movespine2,
    s_long, s_epee, s_lc, s_rc, s_lp, s_rp, weapons, s_wheel …) and `.Force` list. HIGH.
24. **Socket dynamic SFX/objects tables** keyed by RoleType+RepresentID with explicit socket
    columns: `player_socket_dynamic_sfx.txt` (Socket, Scale, OffsetXYZ, idle/not-idle tanis)
    and `player_socket_dynamic_object.txt` (SheathEquipPos/SheathSocket/UnsheathSocket, e.g.
    `RL_WEAPON_RH` + `s_ornament01`). HIGH (extracted heads).
25. **Ragdoll sockets** (already documented, unchanged): 4 groups × 11 rigid bodies with
    `socket / rigid_id / radius / length / pos / yaw-pitch-roll`; sockets
    `bip01 l/r foretwist`, `upperarm`, `calf`, `thigh`, `foot`, `pelvis`; radii 5–8 u,
    lengths 10–12 u (`proof/gravity/physic_character_param.krl.txt:7-546`,
    `docs/movement/JX3_COLLISION_SYSTEM.md` §14.1). High confidence via existing proof; not
    re-disassembled here.

### Camera anchor / head attach (existing claims re-verified)

26. **`Bip01 Head` is a literal in the represent camera path**:
    `KRLCharacter::SetFreeze Get m_CameraAniPauseControl.vObjectPosition From [Bip01 Head]
    Fail!` (game `JX3RepresentX64.dll` string VA 0x00CAF530) and
    `AdjustCharacterCameraObjectY` @0x180AC8980 loads `Bip01 Head` @0x180AC8C3D
    (`proof/netcode/disasm/camera_set_core2.txt:160`). The `s_face` look-at is a separate
    branch in `SetCharacterCameraPosition` (`0x180B0F177`, `proof/netcode/disasm/camera_set.txt:534`).
    Both HIGH.
27. **`s_face` is the face socket, not the head bone** — it is parented to `Bip01 Head`
    (fact 13) and labelled 颈部/neck in `Socket.tab` (fact 12); the camera *anchor* is the
    head bone while the follow-action *look-at* uses `s_face`. Distinction missing in C1's
    "head/socket (Bip01 Head, s_face)" shorthand. HIGH.

### Engine API (symbols already resolved)

28. **MovieEditor engine module `KG3DEngineDX11EX64.dll` (1955 exports) contains the actor
    class**: exports include `?GetBoneTransform@KG3D_AnimationController@...`,
    `?CreateActorFromFile@KG3D_Engine...`, `?KG3D_CreateAnimationControllerOnlyBoneRTS...`;
    `KG3D_Actor` methods are internal (not exported) but present with assert strings and
    xrefs (below). HIGH.
29. **Game build `KG3DEngineX64.dll` (20 exports) and game `KG3DEngineDX11EX64.dll` (1959
    exports) mirror the same classes**; the adapter (`KG3DEngineAdapterX64.dll`) carries
    UTF-16 assert strings `KG3DModel::GetSocketMatrix` / `GetBoneMatrix` / `FindSocket` /
    `FindBone` / `GetSocketName` but **no code xrefs were found to them in the ME adapter**
    (scanned all exec sections + imm/abs forms) — treat as unreferenced/forwarded. MED.
30. **Managed surface has no bone/socket transform** (negative, verified): full
    `KGModelCLR` method list (recon dump lines 784-813) contains only `GetModelBoneNum(uint)`
    for bones; no `GetSocket*`/`GetBone*` transform getter. `MovieEngineCLR.dll` string scan
    (237,500 extracted strings) shows only machinery names
    (`KMovieObject::BindToSocket`, `KActorBone::InitSocketNode`,
    `%s\ResourcePack\SocketToParentBone.ini`, `KGRL_SOCKET`, `bExportSocketAsBone`) — no
    managed getter. The only CLR model transform access today is
    `KGSceneCLR.AddDummyModel` (returns the handle) + `KGModelCLR.AttachModel(handle)`.
    HIGH for the negative.

---

## Bone & socket map

### Key bones (f1 clip `f1b02yd行走.ani`, 182 bones)

| Bone name | f1 idx | m2 idx | Role |
|---|---|---|---|
| `bip01` | 0 | 0 | root |
| `bip01 pelvis` | 1 | 1 | hips |
| `bip01 spine` / `spine1` / `spine2` | 2 / 5 / 18 | 7 / 15 / 24 | torso chain |
| `bip01 neck` / `neck1` | 28 / 31 | 29 / 40 | neck |
| `bip01 head` | 38 | 49 | **head (camera anchor bone)** |
| `bip01 l/r clavicle` | 32 / 33 | 41 / 42 | shoulders |
| `bip01 l/r upperarm` | 34 / 35 | 50 / 52 | arms |
| `bip01 l/r hand` | 48 / 50 | 65 / 67 | hands |
| `b_hat` | 47 | 58 | hat attach (parent of `s_hat`) |
| `b_lh` / `b_rh` | 67 / 73 | 74 / 80 | hand attach (parents of `s_lh`/`s_rh`) |
| `b_lp` / `b_rp` | 8 / 9 (m2) | 8 / 9 | waist attach (`s_lp`/`s_rp`, `s_shoubao`, `s_fan`) |
| `b_lc` / `b_rc` | 30 / 31 (m2) | 30 / 31 | shoulder attach (`s_lc`/`s_rc`, `s_modao`, `bddagger`/`bdknife`) |
| `b_spine` | 19 | 18 | back attach (`s_epee`, `s_bow`, `s_spine`, `s_tangdao`, `s_longbow`, `s_dorlach`) |
| `b_spine2` | 36 | 32 | back attach (`s_spine2`, `s_cloak`, `s_movespine2`, `s_qin/jian*`) |
| `b_long` | 37 | 33 | long-weapon attach (`s_long`, `s_ltulwar`, `s_rtulwar`) |
| `bone_shd_l/r` | 40 / 42 | 51 / 53 | shoulder pads |
| `bone_l/r_armtwist`, `bone_l/r_thightwist` | m2 only | 61/64, 26/28 | twist bones |
| `cbr4/cb3_*hair*` | f1 136-167 | m2 145-190 | hair chain (simulation) |

Sources: `min2.load_min2_stick` decodes (this task); `ResourcePack\BoneNameMap.ini`
(215 names); `samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx`. f1/m2 idx from the two
decoded clips, so indices are clip-layout, not engine skeleton indices.

### Sockets

| Socket | Meaning (Socket.tab) | Parent (SocketToParentBone.ini) | Consumers (verified) |
|---|---|---|---|
| `s_fxtop` | 头顶特效 head-top FX | not in ini (mesh) | FX system |
| `s_fxmid` | 胸口特效 chest FX | not in ini (mesh) | buff_bullet target (180/180), chain end |
| `s_hat` | 头部 head | `B_Hat` (`b_hat`) | hat; hide scheme 12/13/… |
| `s_face` | 颈部 neck | `Bip01 Head` | face bind (`BindTo(...,"s_face")`), camera follow-action look-at, lookatconfig `FaceSocket` |
| `s_lc` / `s_rc` | 左肩/右肩 | `B_LC` / `B_RC` | shoulder items, `s_modao`, knives |
| `s_spine2` | 背部 back | `B_Spine2` | back items, `s_cloak` family |
| `s_spine` | 后腰 back waist | `B_Spine` | buff_bullet caster bind (180/180) |
| `s_lp` / `s_rp` | 左腰/右腰 | `B_LP` / `B_RP` | pouches (`s_shoubao`), `s_fan` |
| `s_lh` / `s_rh` | 左手/右手 | `B_LH` / `B_RH` | hand weapons; chain start `S_rh`; slot_link |
| `s_long` | 长柄 long pole | `B_Long` | long weapons, `s_ltulwar/rtulwar` |
| `s_epee` | 重剑 heavy sword | `B_Spine` | back greatsword (socket_offset table) |
| `s_bow` | 千机匣背挂 | `B_Spine` | back device |
| `s_hs` | 马背 horse back | not in ini (mount system) | slot_link mount seat (`s_hs2` alt) |
| `s_bale` | 马背偏后 | not in ini | mount cargo |
| `s_fire` | 枪口火焰 muzzle | `B_RH` | gun FX |
| `s_flute` | 笛子 flute | `b_rc` (matrix entry) | flute |
| `s_lh02` | (hand alt) | `B_LH` | second hand slot |
| others | `s_coat`-family in ini: `s_cloak`, `s_movespine2`, `s_movebigbrush`, `s_lshoulder/rshoulder`, `s_qin01/02`, `s_jian01/02`, `s_movejian01/02`, `s_moveqin01/02`, `s_bddagger`, `s_bdknife`, `s_bdshell`, `s_bdshelf`, `s_knife`, `s_shell`, `s_medkit`, `s_tangdao`, `s_longbow`, `s_dorlach`, `s_fan`, `s_pet01-05`, `s_ornament*` (NPC dynamic-object table) | per ini | back/hand/waist accessories |

---

## Engine API path

### Managed (MovieEngineCLR assembly, used by the host)

| Entry | Signature | Notes / evidence |
|---|---|---|
| `KGSceneCLR.AddDummyModel` | `long(string name, string path, CLRfloat3 pos, CLRfloat4 rot, CLRfloat3 scale)` | returns the dummy handle; same name → same handle (M1 doc + `client/RebornClient.cs:1334`). Values are pointer-like: live log `player handle=5755102904` (0x15710A2B8-range), `handle=4858914552` (`reborn_out/reborn_20261006_165452.log`). |
| `KGSceneCLR.RemoveDummyModel/ClearDummyModel` | `int(string)` / `int()` | handle map field `m_pMapDummyModel` (recon line 752). |
| `KGModelCLR.AttachModel` | `long(long handle)` | binds the dummy to the animatable model; `client/RebornClient.cs:1345`. |
| `KGModelCLR.GetModelBoneNum` | `uint(long)` | the only bone-related managed method; proves the CLR resolves a model from the handle. |
| `KGModelCLR` fields | `m_pModel`, `m_pMovieObjectHolder` | private native pointers; the reflection route (same trick as `KGSceneCLR.m_pScene` in `camera_shim`) would read `m_pModel`. |
| `KGMovieActorCLR.GetModelHandle` | `long()` | editor actor path (used with `KGModelCLR.AttachModel`). |
| `ActorEditorCommandHelper.BindToSocket` | `static void(KGMovieActorCLR, long, string, float, float, float)` | MovieEditorHD.exe only — not callable from the host build. |

### Native — MovieEditor build (host truth), `KG3DEngineDX11EX64.dll` (image base 0x180000000)

| Symbol | RVA | Decoded signature / semantics |
|---|---|---|
| `KG3D_Actor::FindSocket` | `0x18081E5D0` | `(KG3D_Actor* this, const char* name, BindExtraInfo* out, int flags)`; hashes name via `KGCommonX64.dll!KG3D_ConvertToStandardHashString`; searches attached child actors (`[this+0x7E0]` list, type `[+0x2A0]==4`, `vt[+0xF8]`) then base model socket list (`[this+0x358]->vt[+0x408]()` → `{count +0x38, entries +0x40, stride 0x50, nameHash at +0}`). `out = {pActor @+0, socketIndex @+8, fromBaseModel @+0xC}`. |
| `KG3D_Actor::GetSocketMatrixLocal` | `0x1808204D0` | `(this, int socketIndex, XMFLOAT4X4* out)`; `piModel=[this+0x358]` (`m_piCurModel`); socket entry `+8` = bone index → bone matrix `piModel->vt[+0x1C8]()[idx*0x40]`; composes bind-extra/flexible data (`[rbp+0x9C4]` path). Returns `S_OK`/`E_FAIL`; asserts `uSocketIndex != -1`, `pRetMatrix`, `piModel`. |
| `KG3D_Actor::GetBoneMatrixLocal` | `0x18081F090` | `(this, int boneIndex, XMFLOAT4X4* out)`; `boneIndex==-1` writes identity; `piModel=[this+0x358]`. |
| `KG3D_Actor::GetBoneTransform` | `0x180862660` | `(this, int boneIndex, XMFLOAT4X4* out)`; dispatch mode `[this+0x3C4]`; `vt[+0x180](idx, out)` on `[this+0x408/0x410/0x418]` model objects. |
| `KG3D_Actor::FindBones` | `0x18081E3C0` | `(this, hash, out)`; recursive into child actors + `0x18082F0A0`. |
| `KActorBone::InitSocketNode` | string 0x002E7B08 | editor bone tree loads `%s\ResourcePack\SocketToParentBone.ini` (string 0x002E7AB0). |
| `MovieEngineCLR::KGSceneCLR::AddDummyModel` | native symbol string 0x002BAA58 | `.rdata` only; no direct code xref found (mixed-mode; method body not located). |

### Native — game client truth (`C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64`)

| Symbol | RVA | Notes |
|---|---|---|
| `KG3DModel::GetSocketMatrixLocal` | body `0x1801F1660` (thunk 0x1801F1640 → `vt[+0xA90]`) | `(this, uint idx, XMFLOAT4X4* out, const FVECTOR3* offset)`; sockets `[this+0x430]`, count `[this+0x438]`, stride 0x40; if `offset` non-null adds it to the matrix translation; `pidx < m_dwNumSockets`/`m_pSocketMatrix` asserts. Evidence: `KG3DEngineX64.dll`, scratch xref file. |
| `KG3DModel::GetSocketName` | `0x1801F17E0` | `(this, const char** out)` → internal 0x1802548F0. |
| `KG3DModel::GetBoneMatrixLocal` | `0x1801EE980` | `(this, uint idx, XMFLOAT4X4* out, ...)`; cycles via child model; `vt[+0x578]` thunk at 0x1801EE960. |
| `KG3DModel::FindBone` | string `0x00671088` ("Failed to Call ... with bone name %s") | exact function body not isolated (xref asserted in a log helper). |
| `KG3DSkeleton::FindBone` | string `0x006A8158`; `LoadDummyFromIni` `0x006A7FB0`, `Dummy%d` `0x006A7FF0`, `m_pDummyMatrices` `0x006AAA10` | skeleton-level bone/dummy attach API. |
| `KG3DMesh::GetSocketName` / `GetSocketParentBoneName` / `GetSocketCount` | strings `0x006ABCD8` / `0x006ABD30` / `0x006ABD98` | per-mesh sockets with parent bone. |
| `JX3RepresentX64.dll` `RendererComponent::GetSocketPosition` | `0x180405D30` | `(this, const char* pszSocket, XMFLOAT3* outPos, XMFLOAT3* outOffset)`; calls `m_rlActor.GetBonePosition(pszSocket, rvPos, vOffset)` (internal 0x1800202F7; assert string VA 0x00C9DC98). This is the gameplay-layer socket→world resolver. |
| `JX3RepresentX64.dll` face bind | strings 0x00CB2240 / 0x00CB2820 | `m_prlFaceDefinition->BindTo(GetRLModel(), "s_face")`, `m_prlModel->BindTo(..., "s_face")`. |
| camera anchor | `0x180AC8980` (`AdjustCharacterCameraObjectY`), `Bip01 Head` load `0x180AC8C3D`, `s_face` look-at `0x180B0F177` | head bone = anchor; `s_face` = follow-action look-at. |

### Native — adapter (proxy layer)

`KG3DEngineAdapterX64.dll`:
- ME build: UTF-16 asserts `KG3DModel::FindSocket` (VA 0x1801152E8/3C0/460), `KG3DModel::GetSocketMatrix` (0x180113B18/BC8/FE8), `KG3DModel::FindBone` (0x180110B78/CE8/E28), `KG3DModel::GetBoneMatrix` (0x180110F28/1018/11F8/13E8) — **no code xrefs found** (unreferenced strings / forwarded thunks). MED.
- Game build: `KG3DModelProxy::GetBoneMatrix` (RVA 0x2B32C0), `GetSocketMatrix` (0x2B32E0), `GetBoneMatrixLocal` (0x2B3300), `GetSocketName` (0x2B3930), `FindBone` (0x2B3B48), `FindSocket` (0x2B3F58), `GetBoneTransform` (0x2B4320) — from `proof/netcode/adapter_strings.txt`. The proxy mirrors the model API; the CLR likely talks to the proxy. MED (strings; call graph unproven).

---

## Corrections to existing docs

| Doc / claim | Correction | Evidence |
|---|---|---|
| `docs/netcode/SKILL_DATA_EXTRACTION.md:57` "182-bone `Min2SkelClip`" | Bone count is body/clip-specific: m2 action clips decode to **191** bones (`m2b02yd奔跑.ani`), f1 to **182**. Root bone index 0 `bip01` still holds. | `min2.load_min2_stick` decode, this task |
| `docs/movement/JX3_COLLISION_SYSTEM.md:1951` "spawn at caster socket (S_fxmid/S_rh pattern for chains, ability_candidates.json:41)" | Two different tables: `buff_bullet` itself uses caster bind `S_spine` + target `S_fxmid` (180/180); `S_rh → S_fxmid` is the **skill_chain** bind (孤风/飞爪 chain), from the ability notes, not buff_bullet. | `proof/collision/missile/buff_bullet.txt`; `ability_picker/data/ability_candidates.json:41` |
| `docs/camera/HOST_DEVIATIONS.md` C1 "anchor = head/socket (Bip01 Head, s_face)" | They are two distinct things: anchor bone = `Bip01 Head`; `s_face` is a socket **parented to** `Bip01 Head` and is the follow-action look-at (Socket.tab labels it 颈部/neck). A head-anchor fix needs the bone, a follow-action fix needs the socket. | `SocketToParentBone.ini` Dummy35; `camera_set.txt:534`; `camera_set_core2.txt:160` |
| `docs/netcode/JX3_CAMERA_RESEARCH.md:473` `FaceSocket=s_face` | Consistent; add that the face socket's parent bone is `Bip01 Head` and its translation is ~(0, −0.0000019, 0) in the ini matrix (scale 0.393701). | `SocketToParentBone.ini` |
| `docs/neticode/UNIT_SCALE_AND_CHARACTER_SIZE.md` §4 `player.txt` | No correction; the extracted copy at `C:\jx3tmp\common_probe\out\Represent\player\player.txt` matches (7 rows, same heights/scales). Note the per-role MDL path convention `Data\source\player\<Body>\部件\Mdl\<X>.mdl`. | extraction |
| `docs/GAME_SYSTEMS_RESEARCH_MAP.md:44` cites `_head_attach.jsfrag` as [PART] evidence | `_head_attach.jsfrag` is **legacy/player-era**: added by commit `1dee82f` (2026-09-21, "Add Reborn JX3 animation player"), removed by `9e00e2c` (2026-09-29 cleanup, map-viewer leftovers; theory banned by AGENTS §7). It is a three.js fragment that assumes `bip01_head`/`bip01_pelvis` bones and re-parents head/hand meshes to the primary skeleton. Keep only as a historical pointer. | `git show 1dee82f:_head_attach.jsfrag` |
| Any doc implying `.actor` exists in the game client runtime | Actor files are editor-side only (MovieEditor source / downloader editortool); runtime composition uses `Represent/player/*` tables + `player.txt`/MDLs. | full-tree scan; see fact 6 |

---

## Open questions + next probes

1. **Handle → object type (blocking for the socket call).** `AddDummyModel` returns a
   pointer-like `long`. Determine what it points at (KG3D_Actor? adapter KG3DModel proxy?
   KGMovieObject?). Probe: in a scratch build, log `*(void**)handle` and compare the vtable
   against loaded-module ranges (KG3DEngineDX11EX64 / adapter), and `[handle+0x358]` for the
   model pointer used by the `KG3D_Actor` methods above. Do **not** dereference blindly
   without SEH.
2. **`m_pModel` reflection route**: read `KGModelCLR.m_pModel` (private) like
   `KGSceneCLR.m_pScene` in `camera_shim`; log its vtable. If it is the adapter
   `KG3DModelProxy`, the adapter's `FindSocket/GetSocketMatrix` (game-build semantics) apply;
   if it is the engine actor, the `KG3D_Actor` RVAs above apply.
3. **World vs local matrix**: `GetSocketMatrixLocal` is model-space (bone matrix + local
   socket transform); confirm the actor's world matrix multiply route (scene object
   transform) — decode the tail of 0x1808204D0 (after 0x1808207EE) or A/B in the shim.
4. **Mesh socket extraction**: parse the `.mesh` socket section (names + parent bones +
   matrices) with a `tools/` reader; verifies `s_fxtop/s_fxmid/s_hs/s_bale` parents that are
   absent from `SocketToParentBone.ini`.
5. **Bone-name hash**: `KG3D_ConvertToStandardHashString` (`KGCommonX64.dll` export) converts
   names to the u64 used by `FindSocket`/`FindBones`; either call the export or RE it, so
   offline tools can precompute hashes.
6. **`SocketToParentBone.ini` matrix units**: linear ±0.393701 = 1/2.54 suggests inch→cm
   conversion at load; confirm in `KActorBone::InitSocketNode` disasm (ME, string
   0x2E7B08).
7. **Actor slot 10/11/14/15/16 semantics and `Detail`**: no shipped actor in the scanned
   set has them active; check the editor's `EnumPartType` values (metadata enum, values not
   in the API dump) and `ChangePartMeshAndMtl` IL.
8. **`.ActMtl` 76-byte sidecar** (material-instance pack reference) format — unparsed.
9. **Mount socket `[pRLCharacter+0x3A08]`**: which socket/bone does it hold (likely `s_hs`)?
   Needed to finish camera C1's mount branch.

---

## Host feasibility

**Feasible now (needs one new shim export + one live probe):**

- The host already loads `KG3DEngineDX11EX64.dll` (`client/EngineRay.cs:99`) and already
  reaches private engine objects by reflection (`KGSceneCLR.m_pScene` → `camera_shim.dll`).
  The same pattern gives `KGModelCLR.m_pModel` (or the `AddDummyModel` handle), and the
  engine exposes concrete socket/bone getters:
  - `KG3D_Actor::FindSocket` @ `0x18081E5D0` (name → socket index; SEH-guard the call),
  - `KG3D_Actor::GetSocketMatrixLocal` @ `0x1808204D0` (index → 4×4, model space),
  - `KG3D_Actor::GetBoneMatrixLocal` @ `0x18081F090` / `GetBoneTransform` @ `0x180862660`.
- This is exactly the camera-shim pattern (read field → call native RVA → SEH):
  `RC_ActorFindSocket(void* actor, const char* name, int* idx)` +
  `RC_ActorSocketMatrix(void* actor, int idx, float* m16)`. For the C1 anchor, resolve
  `Bip01 Head` as bone (`GetBoneMatrixLocal` with the bone index from `FindBones`, or the
  `FindSocket("s_face")`/head-bone route); for follow-action/look-at use `s_face`.
- Engine-first ordering: call after the engine frame update (the host re-adds the dummy
  every frame; querying mid-frame may read stale bone matrices).
- Crash risk is the known D6 class; keep the calls SEH-guarded and do not run two
  namespaces (AGENTS §2).

**Blocked / unproven:**

- Nothing in the *managed* API exposes the transform; `GetModelBoneNum(long)` is the
  closest and only proves handle→model resolution. There is no supported managed getter.
- The adapter route (`KG3DModelProxy::*` in `KG3DEngineAdapterX64.dll`) mirrors the API in
  the game build but has no code xrefs in the ME build's scanned sections; only pursue it if
  the handle turns out to be a proxy object.
- Game-client represent path (`RendererComponent::GetSocketPosition`, `KRLCharacter::
  GetBonePosition`) is the gameplay truth but lives in `JX3RepresentX64.dll`'s gameplay
  representation, which the MovieEditor host does not drive; it is the reference for what
  the shim should reproduce, not a call target.

**Estimate:** one feature branch (`#iso`), one shim export pair + reflection wiring, one
driven run logging `socket=<name> idx=<n> pos=(...)` for `Bip01 Head`/`s_face` on the 花萝
dummy, compared against the known chest+90 anchor; then the camera C1 exits its
"actor/bone API" criterion.

---

## Host wiring update (2026-10-06, agent/3x-rig)

- **Open question 1 resolved:** the `KGSceneCLR.AddDummyModel` handle is a
  **`KG3DModelProxy`** (vtable in `KG_EngineEditorX64.dll`); `proxy+0x18` is the live
  `KG3D_Actor` (RTTI `.?AVKG3D_Actor@@`).
- **Sockets are NOT initialized on the dummy path** (FindSocket misses `s_face`/
  `bip01 head`; the actor's own `+0x358` model pointer is null). The models live on
  8 child actors (type `[+0x2A0]==4`, linked from `[actor+0x7E0]`, node = child+0x7F0),
  each with its model at `+0x358`.
- **Bone route works:** `KGCommonX64!KG3D_ConvertToStandardHashString` (export 0x50230)
  -> helper `KG3D_Actor` 0x82F0A0(child, hash, out16) (bone idx at +8, model bone list
  at model->vt[+0x408]: count @+0, hash array @+0x48, index array @+0x50)
  -> `GetBoneMatrixLocal` 0x81F090(child, idx, m16). Head bind `t=(1.9,96.7,1.0)` u.
- **Camera C1 wired:** anchor = model placement (rpx,rpy,rpz,yaw+yawOffset,scale) x
  bone-local translation, per frame; `RC_ANCHOR_BONE=0` -> chest+90 fallback.
  Verified `anchorbone t=(1.9,96.7,1.0) yRaw=1058.7 chest=1052.0` (py=962).
- **Proxy method RVAs** (`KG_EngineEditorX64.dll`): FindBone 0x49160, FindSocket
  0x4BA80, GetBoneMatrix 0x45760, GetBoneMatrixLocal 0x459A0, GetSocketMatrix 0x45A40.
- **Boundary:** `s_face` look-at is unavailable on the dummy path until sockets are
  initialized (no `InitSocketNode` on the CLR dummy route) - anchor uses the head bone
  for both anchor and aim; re-open when a socket init route exists.

## Reproduce (commands used)

```powershell
# actor decode (GBK)
.venv\Scripts\python.exe -X utf8 -c "d=open(r'C:\SeasunGame\MovieEditor\source\花萝无动作.actor','rb').read(); print(d.decode('gb18030'))"

# bone names (f1 182 / m2 191)
.venv\Scripts\python.exe -X utf8 -c "import sys; sys.path.insert(0,r'C:\Users\Zhibin Ren\Desktop\reborn'); import min2; c=min2.load_min2_stick(r'samples\player\moves\from_mapviewer\f1\动作\f1b02yd行走.ani'); print(c.bone_count); print(c.bone_names)"

# socket tables
.venv\Scripts\python.exe -X utf8 -c "print(open(r'C:\SeasunGame\MovieEditor\ResourcePack\Socket.tab','rb').read().decode('gb18030'))"
.venv\Scripts\python.exe -X utf8 -c "print(open(r'C:\SeasunGame\MovieEditor\ResourcePack\SocketToParentBone.ini',encoding='utf-8').read())"

# buff_bullet socket columns
.venv\Scripts\python.exe -X utf8 -c "t=open(r'proof\collision\missile\buff_bullet.txt','rb').read().decode('gb18030').splitlines(); import collections; print(collections.Counter(r.split(chr(9))[6] for r in t[1:] if r.strip()))"

# disasm xrefs (loads ~40 MB; writes to scratch)
.venv\Scripts\python.exe tools\netcode\xref_string.py "C:\SeasunGame\MovieEditor\bin64\KG3DEngineDX11EX64.dll" "KG3D_Actor::GetSocketMatrixLocal" --out <scratch>\xref_me_dx11_actorsocketmatrixlocal.txt
.venv\Scripts\python.exe tools\netcode\xref_string.py "C:\SeasunGame\MovieEditor\bin64\KG3DEngineDX11EX64.dll" "KG3D_Actor::FindSocket" --out <scratch>\xref_me_dx11_actor_findsocket.txt
.venv\Scripts\python.exe tools\netcode\xref_string.py "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineX64.dll" "KG3DModel::GetSocketMatrix" --out <scratch>\xref_game_engine_socketmatrix.txt
```

Generated evidence in scratch: `xref_me_dx11_actorsocketmatrixlocal.txt`,
`xref_me_dx11_actor_findsocket.txt`, `xref_me_dx11_actor_findbone.txt` (FindBones),
`xref_me_dx11_actor_getbonetransform.txt`, `xref_me_dx11_actorbonematrixlocal.txt`,
`xref_game_engine_socketmatrix.txt`, `xref_game_engine_bonematrixlocal.txt`,
`xref_game_engine_findbone.txt`, `xref_game_represent_getsocketpos.txt`,
`xref_game_represent_anchor.txt`, `recon_managed_api_utf8.txt`,
`rep_*` (Represent/player tables), `actor_*.txt`.
