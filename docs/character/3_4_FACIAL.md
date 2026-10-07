# 3.4 Facial / morph (FaceLift) — deep research findings

**Area:** character · **Branch:** research/character-animation · **Index:** `docs/character/README.md`

Date: 2026-10-06 (research session). All analysis on read-only installs; copies in scratch.
Scope: character face creation/morph pipeline — new-face save format, converter, shipped tables,
mesh/bones, engine apply path, MovieEditor/CLR surface, selfie AI motion dir.

Key conclusion up front: the map's single "Facial/morph (FaceLift)" line hides **three distinct
systems** that share tables and enums:
1. **Simple face presets** (`data/public/face/{m1,m2,f1,f2}/*.ini`, plain INI, 49+2 params).
2. **New face / MetaFace** (捏脸): 39 face bones, 187 `KFACE_LIFT_BONE_TYPE_V2` TRS params +
   29 decals; saved by the UI as `newfacedata/New_Face_<Role>_<ts>_Create.ini` (CNDK Lua);
   converted to JSON meta-face definition by `FaceLiftDataConverterX64.exe KMETAFACE`.
3. **FaceLift (易容, saved faces)**: server-synced `KFACE_LIFT_DATA` (373 B) collection with
   price/voucher UI, `settings/FaceLift*/*.tab`; clamps edits using V1 (49) / V2 (187) bone tables.

---

## Verified facts

Each fact: statement — evidence — confidence.

### A. The user new-face save file (`newfacedata/*.ini`)

A1. Files exist and are the player's own new-face saves:
`C:\SeasunGame\Game\JX3\bin\zhcn_hd\newfacedata\New_Face_LittleGirl_20260203-095121_Create.ini`
(2890 B) and `...20260223-104015_Create.ini` (2933 B). Both last written 2026-02/2026-02.
— path listing; HIGH.

A2. Container format = `CNDK`:
`magic "CNDK" (4) | uint32 LE crc32(payload) | uint32 LE payloadSize | uint32 LE payloadSize |
payload (GB18030 Lua source starting "return {")`.
crc32 verified for both files, for all 17393 `*.jx3dat` under `interface\` (e.g.
`interface\LM\LM_Craft\data\shop\remake.jx3dat` 13778 B), and for `userdata\...\hotkey.data` /
`custom.dat`. Both size fields equal (uncompressed).
— scratch parser `verify_tbone.py`, `crc` checks; HIGH. (Repo docs partially describe CNDK; see
"Corrections".)

A3. Payload is a Lua table (GB18030/ASCII):
`return {tDecoration={{nShowID=0,nColorID=0},[0]={...}}, bNewFace=true,
 tBone={118,-40,...186 ints...,[0]=-15}, tDecal={{fValue1=...,nColorID=...,bUse=...,
 fValue3=...,nShowID=...,fValue2=...} x29}, nMajorVersion=1, nRoleType=6, nVersion=1}`
— decoded both files; HIGH.

A4. `tBone` = **187 signed bytes (int8, -128..127), indices 0..186**: Lua literal gives 1..186,
slot `[0]` is written last. Index i is `enum KFACE_LIFT_BONE_TYPE_V2` value i
(`flbtForeHeadTY … flbtEyeBallRX`), i.e. per-face-part T/R/S offsets for the 39 face bones.
Proof: (a) client enum `KFACE_LIFT_BONE_TYPE_V2` has 188 entries = 187 types + `flbtTotal`
(`JX3ClientX64.exe` strings `EnumReflect::GetName<enum KFACE_LIFT_BONE_TYPE_V2,flbt*>`);
(b) `settings/FaceLiftV2/Bone/LittleGirl.tab` has exactly 187 rows `Type ValueMin ValueMax`
(Type 0..186); (c) **all 187 values of both user files fall inside those ranges** (0 violations,
`verify_tbone.py`); (d) the converter's name list (`FaceLiftDataConverterX64.exe` fn VA
0x140020530) enumerates the same names in the same order. — HIGH.

A5. `tDecal` = **29 slots (0..28)** = `enum KFACE_LIFT_DECAL_TYPE_V2` order
(`fldtBase, IrisLeft, IrisRight, Brow, Blush, Moustache, EyeLight, Decal1..3, LipGlossR,
LipFlashG, LipOverlapB, LipLightA, Mouth, EyeShadowR/G/B/A, EyeShadowFlashR/G/B/A, Eyelash,
EyeWhiteLeft, EyeWhiteRight, Shadow, AegyoSal, DoubleEyelid`; + `fldtTotal` = 30 enum entries).
Each slot `{nShowID, nColorID, bUse, fValue1, fValue2, fValue3}`.
— client enum strings + converter name list (indices 373..401) + converter JSON output (29 keys);
HIGH.

A6. `tDecoration` = 2 slots `{nShowID, nColorID}` (index 0 and `[0]`); maps to `FacePart` in the
converter JSON (`{"Mouth":{"ColorID":0,"ID":0},"Nose":{...}}`). — HIGH.

A7. `nRoleType=6` = **LittleGirl = F1** body. Role order 1..6 =
`StandardMale, StandardFemale, StrongMale, SexyFemale, LittleBoy, LittleGirl`
(`JX3ClientX64.exe` strings @0x804E08; `settings/FaceLift/Bone/<name>.tab`; `NewFaceData.lua`
constants `StandardMale/StandardFemale/LittleBoy/LittleGirl` = 1/2/5/6; `Represent/common/
FaceLift.krl.txt` `RoleType_FaceModel`/`RoleType_MetaFaceModel` list only 1/2/5/6 →
M2/F2/M1/F1). — HIGH (1,2,5,6); MED for 3=StrongMale, 4=SexyFemale (by order).

A8. File name is produced by the UI Lua module `ui\Script\NewFaceData.lua` (`SaveFaceData`):
`GetFilePath('NewFaceDataDir')` + role name + `GetCurrentTime`/`TimeToDate` + suffix; the
`_Create` suffix is passed from character creation (`LoginCustomRoleNext.lua`, constant
`Create`; `RecordFaceData` / `CoinShop_FaceSave` / `CoinShop_NewFaceShop` flows).
— extracted Lua 5.1 bytecode from PakV4, dumped with `tools/netcode/lua51_dump.py`; MED-HIGH
(naming construction inferred from constants; observed file name is HIGH).

### B. Simple face presets (old/simple face system)

B1. `data/public/face/f1/f1_054.ini` (2147 B, extracted from PakV4) is a plain INI:
`[Face]` 51 keys (`cheekY..ridgeY` = 49 params + `eyeSideL`,`eyeSideR`), `[Base]`
(`RoleType=6`, `TextureID=4`, `DecorationID=0`), `[Decal]` (`<Name>=<ShowID>`,
`<Name>_color=<ColorID>`), `[CustomDetail]` (`<Name>_valueEnable/_value1/_value2/_value3`).
— HIGH.

B2. The 49 `[Face]` keys are the **V1 bone types 0..48**, in the converter's lowercase name
order. Proof: all 49 values of f1_054.ini fall inside `settings/FaceLift/Bone/LittleGirl.tab`
(49 rows) ranges (0 violations, `verify_v1.py`); UI `ui/scheme/case/facelift/facebones.tab`
covers BoneType 0..48 with Chinese labels (眼睛凸出/眼睛位置/…/下颚倾斜). — HIGH.

B3. Shipped preset inventory (PakV4 list `626.list` + extraction):
`data/public/face/{m1,m2,f1,f2}/*.ini` (presets; f1_040..f1_054, m2_068, plus one CN-named
file), `data/public/face/facebasemesh{,_classic,_hd}.tab`, `facedecals{,_classic,_hd}.tab`,
`facedecaldetail{,_classic,_hd}.tab`, `facedecaldetailforui.tab`,
`facematerialparamdefinition{,_classic}.tab`, `facepart{,_classic,_hd}.tab`,
`data/public/face/subsetmaker/{materialparamrule,texturemixingrule}_{classic,hd,sd}.tab`,
`subsetdefinition_*.json`, `data/public/facemeta/facemdl.tab`. — HIGH.

### C. FaceLift settings / enums / server sync (game client)

C1. `JX3ClientX64.exe` and `JX3LogicEditOperationX64.dll` contain the FaceLift subsystem:
`KFaceLiftSettings::{Init,Load,GetBoneInfo,GetDecalInfo,GetDecorationInfo,LoadBoneInfo{,V1,V2},
LoadDecalInfo{,V1,V2},LoadDecorationInfo{,V1,V2},LoadBoneInfoLine,...}`,
`KFaceLiftManager::{Buy,CheckValid,CheckCanBuy,Lua*}` (Luna-bound to Lua),
`KFaceLiftBox::{Init,Add,Replace,Delete,Equip,GetFreeChance,SetVouchers,...}`,
`KPlayerClient::{DoFaceLiftBuyRequest,DoFaceLiftEquipRequest,DoFaceLiftDeleteRequest,
DoApplyEquipLiftedFaceData,OnAddLiftedFace,OnEquipLiftedFace,OnSyncFaceLiftFreeChance,
OnSyncFaceLiftVouchers,OnFaceLiftBuyRespond}`,
`KWorldEventAdaptor::HandleApplyFaceLiftData`, `KRLLocalCharacter::UpdateFaceLiftSetting`
(represent layer). — HIGH (strings, file offsets; e.g. settings paths @0x815800-0x816100).

C2. Table paths (client): `settings/FaceLift/BasePrice.tab`, `settings/FaceLift/Decal.tab`,
`settings/FaceLift/Decoration.tab`, `settings/FaceLift/Bone/{StandardMale,StandardFemale,
StrongMale,SexyFemale,LittleBoy,LittleGirl}.tab`; V2: `settings/FaceLiftV2/Decal.tab`,
`settings/FaceLiftV2/Decoration.tab`, `settings/FaceLiftV2/Bone/<role>.tab`.
All extracted from PakV4 to scratch (see C3/C4). — HIGH.

C3. Extracted tables:
- `settings/FaceLiftV2/Bone/LittleGirl.tab` (2075 B): 187 rows `Type ValueMin ValueMax`
  (0: -40..40 … 186: -80..90). `StandardFemale` 2091 B, 187 rows.
- `settings/FaceLift/Bone/LittleGirl.tab` (558 B): 49 rows (V1).
- `settings/FaceLiftV2/Decal.tab` (260460 B): header `RoleType Type ShowID CanUseInCreate
  RewardsPrice CoinPrice Discount DisStartTime DisEndTime ColorID0 ColorID1 MaxParamID0
  MaxParamID1 MaxParamID2`.
- `settings/FaceLift/Decal.tab` (194623 B, V1): same minus `MaxParamID*`.
- `settings/FaceLift/Decoration.tab` (3950 B): `RoleType DecorationID CoinPrice Discount
  DisStartTime DisEndTime`; `settings/FaceLiftV2/Decoration.tab` (356 B) adds `ColorID0/1`.
- `settings/FaceLift/BasePrice.tab` (140 B): `BasePrice` / `ChangePrice` rows.
— extracted with `tools/netcode/extract_pak_paths.py`; parsed; HIGH.

C4. `data/public/Face/FaceDecals.tab` (178804 B) = decal art definitions
`RoleType Type ShowID FileName Annotate PosX PosY Width Height NeedMirror MirrorAxis
MirrorValue PasteMethod NeedShadow NeedFlip SecondaryTexture` (paths like
`data\source\Player\M2\部件\Texture\face\...tga`). `FaceDecalDetail.tab` (692761 B) /
`FaceDecalDetailForUI.tab` (869970 B) = decal parameter definitions
`RoleType Type ShowID ColorID szName fMax fMin fScale fColorR fColorG fColorB fColorA`.
— extracted/parsed; HIGH.

C5. `KFACE_LIFT_DATA` (server-synced lifted-face struct) size = **0x175 = 373 bytes**:
`cmp rbp, 0x175` immediately before the assert `uFaceDataSize == 0 || uFaceDataSize ==
sizeof(KFACE_LIFT_DATA)` in `KPlayerClient::OnSyncEquipLiftedFace`
(`JX3LogicEditOperationX64.dll` RVA 0x197FCF; also `JX3ClientX64.exe`). Client sends
buy/equip/delete requests and receives the binary face data (repo already catalogued the
opcodes: `proof/netcode/c2s_protocol_catalog.tsv:191-195`). — HIGH (size), HIGH (client side).

C6. Lua-facing engine API (JX3UIX64.dll):
`K3DEngineScriptTable::Lua3DEngine_GetFaceDefinitionFromINIFile` and
`..._GetFaceDefinitionFromJsonBuffer`; `NSX3DEngine::GetFilePath()` (path table used by UI:
`NewFaceDataDir`, `FaceDataDir`). — HIGH.

C7. UI Lua modules (compiled Lua 5.1 bytecode extracted from PakV4 and dumped):
- `ui\Script\NewFaceData.lua`: `SaveFaceData`/`LoadFaceData`/`ExportData`; keys
  `tBone,tDecal,tDecoration,nVersion,nMajorVersion,nRoleType,bNewFace`; `.ini`;
  `GetFilePath('NewFaceDataDir')`; role-name constants; export CD string
  `FACE_DATA_EXPORT_CD`, button `facelift_export_buy`.
- `ui\Script\FaceData.lua`: `.dat`, `GetFilePath('FaceDataDir')`, `LoadLUAData`.
- `ui\Config\Default\Facelift.lua`: import `(*.dat)` via `GetOpenFileName` +
  `STR_FACE_LIFT_CHOOSE_DAT` → `LoadFaceData` → `GetGameCodePage`/`ConvertCodePage` (ACP) →
  `KG3DEngine.GetFaceDefinitionFromJsonBuffer`; UI uses `GetFaceLiftManager.GetBoneInfo`,
  `UpdateBoneParams/UpdateDecalsPrice`, `GetFaceData/ExportData`, `SetFaceBoneParams`,
  `SetFaceDecals`, `bNewFace`, `tBone/tDecal`.
- `ui\Config\Default\LoginCustomRoleNext.lua`: `CreateFaceLift`, `InitNewFace`, `Create`,
  `ui/Config/Default/NewFaceLift.ini` layout.
- `ui\Script\RecordFaceData.lua` / `OldRecordFaceData.lua`, `CoinShop_FaceSave.lua`.
— HIGH.

C8. CNDK writer/reader chain (repo-decompiled + now verified header):
`customdata.decompiled.lua` `SaveLUAData` = `"return " .. var2str(table)` then
`EncodeData(text, crc, compress)` then `SaveDataToFile`; `LoadLUAData` = `LoadDataFromFile` +
`IsEncodedData`/`DecodeData` + `str2var`. The observed CNDK header (crc32 + size) matches the
`crc` option. — HIGH (repo doc `proof/ui/evidence/decompiled/customdata.decompiled.lua:144-205`).

### D. FaceLiftDataConverter

D1. Binaries: `C:\SeasunGame\MovieEditor\bin64\FaceLiftDataConverterX64.exe` (317,440 B,
2026-09-14) and `FaceLiftDataConverterShellX64.exe` (208,896 B), plus 32-bit
`C:\SeasunGame\MovieEditor\FaceLiftDataConverter.exe` (215,040 B). Older copies:
`SeasunDownloaderV2.4\seasun\editortool\movieeditor\bin64\faceliftdataconverterx64.exe`
(303,616 B, 2026-03-31) and `...\qmodeleditor\tools\faceliftdataconverter\` (same size).
— file listing; HIGH.

D2. CLI (X64): `FaceLiftDataConverterX64.exe <input> <output> <KFACE|KBODY|KMETAFACE|KHAIR>`
— usage string `参数非法，必须输入“输入文件名”，“输出文件名”，“KFACE/KBODY/KMETAFACE/KHAIR”
三个参数。` at file offset 0x3C5F0; argc==4 check and category dispatch at VA 0x1400253d0.
Errors: `读取输入dat文件错误。`, `写入ini文件错误。` (0x3C658/0x3C670). — HIGH.

D3. It is a native MFC-free exe that imports **Engine_Lua5X64.dll**
(`g_SetRootPath`, `g_OpenIniFile`, `g_OpenTabFile`, `g_UnitePathAndName`, `KGLogPrintf`, ...)
and sets its root path to the current working directory. RTTI:
`KFaceLiftDataConverter`, `KBodyDataParamConverter`, `KHairDataParamConverter`,
`KMetaFaceDataConverter`, `KG3DFaceDefinition`. PDB path
`...\EditorTools\FaceLiftDataConverter\x64\Release\FaceLiftDataConverterX64.pdb`. — HIGH.

D4. **Ran the X64 converter on copies in scratch** (CWD=scratch, install untouched):
- `KMETAFACE in_newface.ini out3.ini` → **valid JSON** meta-face definition (9329 B):
  `{"BodyType":6, "Bone":{187 T/R/S names→int}, "Decal":{"Base":{"ColorID","CustomDetail":
  {"bEnable","fValue1","fValue2","fValue3"},"ShowID"}, ... 29 types}, "FacePart":{"Mouth":
  {"ColorID","ID"},"Nose":{...}}}`. JSON parses cleanly (`json.loads`).
- `KFACE`/`KBODY`/`KHAIR` with `.ini`/`.tab` inputs → `读取输入dat文件错误。` (their loaders
  expect a `.dat`; KFACE loader VA 0x14002a8d0 reads text and keys
  `nRoleType,nVersion,nDecorationID,bUse`; KBodyDataParamConverter reads
  `data/public/BodyReshaping/{m1,m2,f1,f2}/BodyParam.tab`).
— empirical run; HIGH for KMETAFACE, HIGH for the error behaviour, MED for the `.dat` layout.

D5. `FaceLiftDataConverterShellX64.exe` = GUI batch wrapper (`CFaceLiftDataConverterShellApp`,
`CSimpleIniTempl`): file dialogs `FaceDecalDetail_*.tab` (KFACE), `*.dat`, `*.ini`
(KMETAFACE), reads `FaceMaterialParamDefinition*.tab` (error `找不到FaceMaterialParamDefinition*
文件，无法正确转换。`), then invokes `"FaceLiftDataConverterX64.exe" "<in>" "<out>" ...` per
file; labels `捏脸dat文件`/`体型dat文件`/`meta脸ini文件`; decal-id validation message
`错误偏色id组合：体型:%d, 类型:%d, ShowID:%d, ColorID:%d`. — HIGH (strings @0x18690-0x18B20).

D6. `KG3DFaceDefinition` vtable at VA 0x14003EE80 (raw 0x3DA80): `LoadFromFile` RVA 0x2CD30,
`SaveToFile` RVA 0x2D660, `SetContent` RVA 0x2E2F0; Load/Save use `g_OpenIniFile` and
sections `Face`/`Base`/`Decal` (the simple INI format); assert
`nFaceParamCount > 0 && nDecalDefCount > 0`, `nRoleType >= 1 && nRoleType <= 6`.
— disassembly; HIGH.

### E. Face mesh, bones, meta-face models

E1. `f1_new_face_hd.mesh` extracted from PakV4 (`data/source/player/f1/部件/f1_new_face_hd.mesh`,
293,506 B): **4652 verts, 8041 faces, 39 bones** (parsed with repo `mesh.py`):
`Cheek_L/R, Eye_L/R, Eye_L_Deform/Eye_R_Deform, Eyebrow_L/R, Eyecrow_L/R,
Eyelid_Lower_L/R, Eyelid_Upper_L/R, Face_L/R, Faceroot, Glabella_L/R, Jaw, Jaw_End,
Lip_Lower_L/R, Lip_Upper_L/R, Mouth_L/R, Mouthopen, Nose, Nose_L/R, Nosehead, Pupil_L/R,
Ridge_L/R, Smile_L/R, Tongue`. Hierarchy root `Faceroot` → `Nosehead, Eye_*_Deform,
Mouthopen, Lip_Upper_*, Face_*, Glabella_*, Eyebrow_*, Smile_*, Cheek_*, Mouth_*, Nose_*`.
Same 39 names appear in the converter's bone list. — HIGH.
Note: the mesh has no blend-shape block; deformation is bone TRS driven (187 `flbt` params).

E2. `data/public/facemeta/facemdl.tab` (685 B): `BodyType SkeletonFile MeshFile TPoseAniFile`;
BodyType 1/2/5/6 → `data/source/player/{M2,F2,M1,F1}/部件/{M2,F2,M1,F1}_meta_face.txt` +
`{...}_meta_face_body_HD.mesh` + `.../face/{...}_meta_face_hd_standby.ani`.
`data/public/face/facebasemesh_hd.tab`: RoleType 1/2/5/6 → `{m2,f2,m1,f1}_new_face_HD.mesh`
+ eye/lip masks etc. `Represent/common/FaceLift.krl.txt` (1423 B):
`RoleType_FaceModel` 1/2/5/6 → `<role>_new_face.Mesh` + `.jsoninspack`;
`RoleType_MetaFaceModel` 1/2/5/6 → `<role>_Meta_face_HD.mdl`; `MeshScale`, `UpdateParam_1..3`.
— HIGH.

E3. New-face static/animation assets ship per role (626.list): `<role>_new_face.mesh` +
`.mesh.ini` + `.jsoninspack` (+`_hd`, `_hd_bd`), `<role>_new_face_static/close/standby.ani`,
textures `{role}_new_face.{dds,tga}` + `_nor/_mrs/_detailnormal`, `m2_new_face_bd.jsoninspack`.
— HIGH.

### F. Engine apply path

F1. Game client `bin64\KG3DEngineX64.dll`: `KG3DModel::{SetFaceDefinition,InitFaceDefinition,
LoadFaceDefinitionINI,SetFaceBoneParams,SetFaceDecals,ExportFaceLiftTexture}`,
`KG3DFaceDefinition::{LoadFromFile,SaveToFile,SetContent}`, table paths
`data\public\Face\{FaceDecals.tab,FaceDecalColor.ini,FaceDecalDetail.ini,FaceDecalDetail.tab}`,
`KG3DFaceMakerManager::LoadFaceDefinitionFromINIFile`,
`m_SimpleFaceDefinition.m_FaceBase.nRoleType > 0 && <= 6`; RTTI `.?AVKG3DFaceDefinition@@`.
Function starts (heuristic): `SetFaceBoneParams` ~VA 0x1801FAA00 (RVA 0x1FAA00),
`LoadFaceDefinitionINI` ~VA 0x1801F4790, `KG3DFaceDefinition::LoadFromFile` ~VA 0x1804FDDE0.
— strings HIGH; RVAs MED.

F2. `bin64\KG3DEngineAdapterX64.dll` (also in MovieEditor):
`KG3DFaceDefinition::{GetFaceDecals,GetFaceDecalsHD,SetFaceParam,LoadFaceConfig,
LoadFaceConfigFromJsonBuffer}`, `KG3DFaceMakeManager::LoadFaceDefinitionFromINIFile/
FromJsonBuffer/DeleteFaceDefinition`, `KG3DFaceMakerManagerMeta::...`,
`KG3DModel::{SetFaceDefinitionHD,SetFaceDecalsHD,LoadFaceDefinitionFileHD,
SaveFaceDefinitionFileHD,LoadFaceDefinitionJSONMeta,SetFaceDefinitionMeta,SetFaceBoneParamsMeta,
SetFaceBonePartParamsMeta,SetFaceDecalsMeta}`,
`KG3DModelProxy::{LoadMetaFaceDefinitionJson,LoadFaceDefinitionINI,GetFaceLiftParams,
SetFaceLiftParams,GetFaceLiftParamTotalCount}`,
RTTI `KG3D_FaceLiftBoneDefinitionMeta@FaceMakerMeta`, `KG3D_FaceLiftDecalDefinitionMeta`,
`KG3DFaceDefinitionMeta`, `data\public\Face\FaceDecalDetailForUI.tab`,
`bRecalculateFaceLiftVertexNormal`. Function starts (heuristic):
`LoadMetaFaceDefinitionJson` ~VA 0x18012F740, `SetFaceLiftParams` ~VA 0x18012FE30,
`SetFaceBoneParamsMeta` ~VA 0x18008A1F0. — strings HIGH; RVAs MED.

F3. Exports: both engine DLLs export only `Get3DEngineInterface`,
`Get3DEngineXLogicInterface` (+ `GetAsyncTaskSystemInterface` on adapter); the face methods are
virtual interface methods, not exports. — HIGH.

F4. MovieEditor CLR surface (`MovieEditor\bin64\MovieEngineCLR.dll`, reflection-only dump):
- `MovieEngineCLR.KGModelCLR`: `PlayFaceAnimation(name,playType,speed,offset)`,
  `PlayFaceAnimationByMotionType(suffix,...)`, `GetFaceAnimation(suffix,out)`,
  `GetFaceModelName(out)`, `GetFaceModelHandle()`, `ExportFaceLiftTexture(path)`,
  `ExportFaceMesh(path)`, `LoadModel`, `GetModelBoneNum`.
- `KGMovieActorCLR`: `Init/UnInit/GetModelHandle/SetCompressFaceTexture(bool)/...`.
- `KGMovieEditorCLR`: `ChangePlayerFaceType(index,faceType)`, `RefreshFaceAnimationTable`.
There is **no CLR method to set face params / load a face definition** — only animation +
export. — HIGH (reflection).

F5. Native face code inside MovieEngineCLR.dll (strings): `KGModel_Utility::{GetFaceModel,
GetFaceLiftParams,SetFaceLiftParams,SetMetaBoneParams,GetMetaBoneParams}`,
`KGRepresentHelper::SetFaceModel` with paths `\%s_new_face_hd.mesh`, `\%s_meta_face_hd.mdl`,
`\%s_new_face_hd.jsoninspack`, `\face\`;
`KMovieActionFaceLiftAnimation::{LoadFromFile}`, `...RunTime::{PreRun,SetModelFaceParam}`,
`KMovieActionMetaFaceAnimation`, `KMovieActionMetaFacePoseAnimation`,
`LoadFaceDefinitionINI %s is Failed`, `FaceDefIni`, `MetaFaceDefJson`,
`EnumFaceParamType`, `EnumMetaBoneParamType`, `EnumFaceType`, `CHARACTER_FACELIFT_STATE`.
— HIGH.

F6. MovieEditorHD.exe (managed host) already drives face editing: forms
`MetaFaceLiftForm`, `FacePoseUserControl`, `UpdateFaceParametersToUI`, constants
`META_FACE_POSE_BONE_NUM`, `FACE_PART_NUM`, `m_MetaBoneNameArray_CN/EN`,
`m_MetaFacePoseNameArray_EN`; actor action enum `EAT_FaceLiftAnimation = 42` and 26
`EPT_Action_FaceLift_*TimeLine` params (e.g. `YanjingKaihe`, `ZuibaWeizhi`).
— HIGH (strings; repo `proof/engine_host/recon_actor_il3.txt:545-570,1359`).

F7. Face animation subsystem (separate from morph): `data/public/TagEditor/
FaceMotionSettings.ini` (21 motions: 微笑/大笑/愤怒/说话/哭泣..., suffixes `_smile,_laugh,
_rage,_talk,_cry`, `StaticFaceMotionSuffix=_static`), `data/public/MovieEditor/
FaceAniTable.txt` (52 KB), `KAnimationManager::InitFaceAnimationTable/GetFaceAnimationName`,
`KMovieActionFaceMotion::LoadFromFile` v0..v3. — HIGH.

F8. Our own host already runs the engine's face init path:
`proof/engine_host/startup_nodb_2026_10_04/*_engine_init.txt` contain
`WARN KGLOG_PROCESS_ERROR(m_pMeshFileData) at line 101 in KG3D_FaceLiftMeshData::Init`
while trying `data/source/player/{M2,F2,M1,F1}/部件/<role>_new_face.mesh` (SD names missing
from the MovieEditor resource root; HD names ship). — HIGH (repo logs).

### G. `selfiedata\AiBodyMotion`

G1. `C:\SeasunGame\Game\JX3\bin\zhcn_hd\selfiedata\AiBodyMotion\{custom,session}` exist but
are **empty** (no shipped files, no format on disk). — HIGH.

G2. The feature is the **AI motion-capture selfie** module `ui\Script\AiBodyMotionData.lua`
(22,858 B, compiled Lua 5.1, extracted from PakV4): records a selfie video, uploads it to
`/api/v1/ai/bodymotion/{presigned_upload,process_v2,querystate_by_upload,cancel,download,
apply_share,...}` (server AI), receives body + face animation, saves to
`GetFilePath('/AiBodyMotion')` with `GetSessionDir`/`GetCustomDir`;
file name patterns `BodyAct_<globalID>_<ts>`, `FaceAct_<globalID>_<ts>`, timestamp
`%d%02d%02d_%02d%02d%02d`, `.json` + downloaded motion files (`TransformMotionData`);
stages `BEGIN/PROCESSING/UPLOADING/UPLOADED/FINISHED/FAILED/UPLOAD_FAILED`; `WipeSessionDir`
clears `session`, `custom` keeps user motions. — HIGH (bytecode constants; UI manifest lists
`ui\Script\AiBodyMotionData.lua` and `Minimap.lua` global `AiBodyMotionData`).
`selfiedata` files are runtime-only and server-assisted; no local format is shipped.

G3. Selfie UI/config ships: `ui/config/default/selfie.lua(.ini)`, `selfienav.lua(.ini)`,
`ui/scheme/case/{selfiefilter,selfielightparams,selfieresolution}.tab`,
`ui/scheme/setting/selfielights.json`, `ui/scheme/case/string_selfie.txt`. — HIGH.

---

## Pipeline

**Create (in-game, Lua UI)**
`LoginCustomRoleNext` / `CoinShop_NewFaceShop` / `FaceLift.lua` sliders →
`GetFaceLiftManager:GetBoneInfo(role, FACE_LIFT_BONE_TYPE_V2)` (min/max from
`settings/FaceLiftV2/Bone/<role>.tab`) → live preview via engine
`GetFaceDefinitionFromINIFile` / `GetFaceDefinitionFromJsonBuffer` (JX3UIX64 bindings) →
`NewFaceData.SaveFaceData` builds `tBone` (187 int8) + `tDecal` (29) + `tDecoration` (2) +
`nVersion/nMajorVersion/nRoleType/bNewFace` → `SaveLUAData`/native encoder writes
**CNDK + crc32 + sizes + `return {...}`** to
`GetFilePath('NewFaceDataDir')` = `zhcn_hd\newfacedata\` as
`New_Face_<RoleTypeName>_<YYYYMMDD-HHMMSS>_Create.ini`. (Simple/old face: `FaceData.lua`
uses `.dat` in `FaceDataDir`.) — A1-A8, C7-C8.

**Convert (editor tool, offline)**
`FaceLiftDataConverterX64.exe <newface.ini> <out.ini> KMETAFACE` →
**JSON meta-face definition** `{BodyType, Bone{187}, Decal{29}, FacePart{Mouth,Nose}}`
(verified by running). The shell GUI (`FaceLiftDataConverterShellX64.exe`) batch-drives it
from `FaceDecalDetail_*.tab` + `FaceMaterialParamDefinition*.tab` for decals and wraps
`*.dat` for KFACE/KBODY/KHAIR. — D1-D6.

**Load (engine)**
Engine (game: `KG3DEngineX64.dll`/`KG3DEngineAdapterX64.dll`; MovieEditor: same adapter +
`MovieEngineCLR.dll`) loads `<role>_new_face.mesh` (`RoleType_FaceModel` in
`Represent/common/FaceLift.krl.txt`) / `<role>_Meta_face_HD.mdl` + `_meta_face_body_HD.mesh`
(`data/public/facemeta/facemdl.tab`), then applies the face definition via
`KG3DModelProxy::LoadMetaFaceDefinitionJson` or `KG3DModel::LoadFaceDefinitionINI` +
`KG3DModel::SetFaceBoneParams(SetFaceBoneParamsMeta)` + `SetFaceDecals(SetFaceDecalsMeta)`.
Values are clamped by `settings/FaceLift[V2]/Bone/<role>.tab`. — C1-C6, E, F1-F2.

**Apply / runtime**
39 face bones (E1) get per-param TRS from the 187 `flbt` values; decals are composited from
`data/public/Face/FaceDecals.tab` + `FaceDecalDetail*.tab`; face idle/animation from
`FaceMotionSettings.ini`/`FaceAniTable.txt`; export via
`KGModelCLR.ExportFaceLiftTexture/ExportFaceMesh`. Saved faces (FaceLift 易容) travel as
373-byte `KFACE_LIFT_DATA` through `KPlayerClient::DoFaceLift*Request` /
`On*LiftedFace*` and are applied with `KWorldEventAdaptor::HandleApplyFaceLiftData` /
`KRLLocalCharacter::UpdateFaceLiftSetting`. — C1-C5, F1-F7.

---

## Corrections to existing docs

1. `docs/controls/HOTKEY_SYSTEM_FULL.md:115` — "CNDK + 12 header bytes (two `0x11E` = 286
   dwords) + the Lua text" is wrong. Actual: `CNDK` + **uint32 crc32(payload)** + uint32
   payloadSize + uint32 payloadSize + payload. `0x11E` (=286) is the **byte** size, repeated;
   the doc omits the crc dword. Verified on hotkey.data (302 B), custom.dat and 17k `.jx3dat`
   files (crc matches 100%).
2. `docs/ui/UI_SYSTEM_REPORT.md:198` — "16-byte CNDK header" is fine if counted with magic;
   add the crc32/size layout above for precision.
3. `docs/GAME_SYSTEMS_RESEARCH_MAP.md:47` — "Facial/morph (FaceLift) [OPEN]" is now answered
   (this doc). Also line 44 cites `_head_attach.jsfrag`, which does **not** exist anywhere in
   the repo tree (only that map line mentions it).
4. The map conflates three systems; see "Key conclusion" above. FaceLift proper (易容) is a
   server-synced saved-face system; the new-face (捏脸/MetaFace) is the local morph system;
   simple presets are the legacy face.
5. Repo `samples/mesh/SOURCES.txt` labels `f1_meta_face_body_hd.mesh` "REJECT_FACE_MORPH"
   (player-era). Current GT: that mesh is the *meta-face body* mesh referenced by
   `data/public/facemeta/facemdl.tab` for BodyType 6 (F1) — it is part of the real pipeline,
   not a rejected artifact; the label should be revisited if that sample is used again.

---

## Open questions + next probes

Q1. **KFACE/KBODY/KHAIR `.dat` binary layout.** Only known: KFACE loader
(`FaceLiftDataConverterX64.exe` VA 0x14002a8d0) reads text and keys
`nRoleType,nVersion,nDecorationID,bUse`; UI imports `(*.dat)` and validates
`FACE_LIFT_DATA_VAILD`; server struct `KFACE_LIFT_DATA` = 373 B. Probe: dump
`KFaceLiftDataConverter::Load` fully (`tools/netcode/dump_va.py ... 0x14002a8d0 0x14002b200`)
and find a sample `.dat` (game `FaceDataDir`; search userdata/caches for `*.dat` face exports
or generate one via the in-game export button).
Q2. **Who emits the literal `CNDK`?** Not present in any `bin64` binary. Probe: xref
`EncodeData`/`SaveDataToFile`/`LoadDataFromFile` natives in Engine_Lua5X64.dll (script
function table) and dump their code; or breakpoint the write while the game saves a face.
Q3. **Meta-face JSON load path in the MovieEditor host.** `KG3DModelProxy::
LoadMetaFaceDefinitionJson` exists (adapter ~VA 0x18012F740) but is not exposed to CLR.
Probe: build a small native shim (camera_shim pattern) that obtains the model proxy from
`KGMovieActorCLR.GetModelHandle()` and calls it; verify a converted `f1_054`-style JSON
changes the rendered face.
Q4. **RoleType 3/4** (StrongMale/SexyFemale) body mapping — all shipped tables list only
1/2/5/6 models; confirm 3→M2/4→F2 with different bone tables (probe: extract
`settings/FaceLift/Bone/StrongMale.tab` / `SexyFemale.tab`, compare with Standard*; and
`facebasemesh.tab` rows).
Q5. **AiBodyMotion downloaded animation format** (`TransformMotionData` output, likely `.ani`
or a JSON+ani pair). Probe: decompile `AiBodyMotionData.lua` around `TransformMotionData`
and inspect the `BodyAct_*`/`FaceAct_*` files after a feature run (server-assisted).
Q6. **`tDecoration` vs `FacePart` semantics** (2 slots vs Mouth/Nose entries) — probe: create
faces with decorations in-game and diff the tDecoration/JSON.

---

## Host feasibility (Reborn)

What works **today** (no native work), via existing CLR (`MovieEngineCLR.dll`):
- Load actor/face model; `KGModelCLR.PlayFaceAnimation` /
  `PlayFaceAnimationByMotionType` / `GetFaceAnimation` (FaceMotionSettings/FaceAniTable);
- `KGModelCLR.ExportFaceLiftTexture(path)` / `ExportFaceMesh(path)`;
- `KGMovieActorCLR.SetCompressFaceTexture`, `KGMovieEditorCLR.ChangePlayerFaceType`;
- read/clamp face data offline with the shipped `.tab` tables and the documented formats.

What needs **native work** (shim; no invented formats — real engine calls):
- Applying a face definition: `KG3DModelProxy::SetFaceLiftParams` /
  `LoadMetaFaceDefinitionJson` / `GetFaceLiftParamTotalCount` and
  `KG3DModel::SetFaceBoneParamsMeta/SetFaceDecalsMeta` in
  `MovieEditor\bin64\KG3DEngineAdapterX64.dll` (obtain `IKG3DModelProxy` from the actor's
  model handle). This is the natural follow-up to the existing `camera_shim.dll` pattern.
- Loading the new-face mesh + meta-face body mesh through the engine model loader if the CLR
  actor path does not already bring `_facepart_`/`s_face` in (MovieEngineCLR has
  `KGRepresentHelper::SetFaceModel` and `_new_face.mesh`/`_meta_face.mesh` paths).
- The offline chain is fully local and reproducible today: CNDK parse → (KMETAFACE) JSON →
  (planned) engine JSON load. No server is needed for face creation/preview; only the FaceLift
  (易容) buy/equip feature and the AI selfie mocap are server-backed.

Server caveat (cited negative): `KPlayerClient::DoFaceLift*Request` + `m_LiftedFaceData`
asserts are the only server coupling in this area; the face data itself is produced locally
(UI save + converter). No packet capture was performed or needed.

---

## Implementation status (2026-10-06, agent/3x-face)

- **Offline pipeline: DONE.** `tools/character/face_data.py` parses the CNDK save
  (crc32 verified on both real saves), validates all 187 `tBone` values against the
  extracted `settings/FaceLiftV2/Bone/LittleGirl.tab` (0 violations), and wraps
  `FaceLiftDataConverterX64.exe KMETAFACE` (valid JSON: BodyType/Bone=187/Decal=29/
  FacePart, 9329 B). `selftest` = 4-stage gate, PASS.
- **Client load path: DONE.** `client/FaceData.cs` + `RC_FACE_JSON=<json>` after
  `model.AttachModel`; resolves `KGModelCLR.m_pModel` (`IKG3DModelProxy*`) via a
  DynamicMethod `ldfld` (the CLR object cannot be pinned - it holds `List<long>`;
  `FieldInfo.GetValue` rejects pointer fields). Verified in-engine:
  `face: metaface json=metaface_01.json bytes=9329 model=0x15CE92BF8` +
  `face: apply pending agent A shim export RC_ModelLoadMetaFaceJson`.
- **Engine apply: BLOCKED (boundary registered 2026-10-06).** After merging
  `agent/3x-rig` the shim export existed but called the wrong wrapper
  (0x46FB0 = `LoadFaceDefinitionINI`); fixed to 0x47010
  (`LoadMetaFaceDefinitionJson`) and the shim rebuilt. With the correct entry,
  both candidate proxies (`m_pModel`, the RTTI-verified AddDummyModel handle) and
  both MetaFace-capable actors (`source\主角替换模型\*Meta脸.actor`) return
  E_FAIL from the engine's face subsystem. Strong precondition evidence:
  `KG3D_FaceLiftMeshData::Init` requests the SD mesh
  `data/source/player/<role>/部件/<role>_new_face.mesh`, which ships nowhere
  (only `_hd` exists) -> face-lift data never initializes. Fixed en route:
  negative `AddDummyModel` handles no longer AV at `AttachModel` (crash guard).
  Re-open criteria: editor app (`MovieEditorHD.exe` MetaFaceLiftForm) IL call
  sequence; INI->JSON order probe; HD suffix mapping for the face mesh init.
  Full chain: `proof/character/face_apply_investigation_20261006.txt`.
- Evidence: `proof/character/face_pipeline_20261006.txt` +
  `proof/character/face_apply_investigation_20261006.txt`; gates: build exit 0,
  `camera_smoke_3x_face` ALL PASS, collision 36/36, `d6=seed`.

## Reproduce (commands used)

```
# scratch dir
C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\

# 1. CNDK + tBone/tDecal decode / range verification
.venv\Scripts\python.exe char3x\verify_tbone.py      # 0 out-of-range on both files
.venv\Scripts\python.exe char3x\verify_v1.py         # 0 out-of-range on f1_054.ini

# 2. table extraction from PakV4 (official extractor wrapper)
.venv\Scripts\python.exe tools\netcode\extract_pak_paths.py --list char3x\face_candidates.txt \
    --out-dir char3x\face_out --work char3x\face_work

# 3. converter empirical run (copies only, CWD=scratch)
char3x\fltool\FaceLiftDataConverterX64.exe in_newface.ini out3.ini KMETAFACE   # -> valid JSON

# 4. mesh bones
.venv\Scripts\python.exe char3x\mesh_bones.py        # 39 bones, 4652 verts

# 5. UI Lua bytecode (extracted via extract_pak_paths.py, then)
.venv\Scripts\python.exe tools\netcode\lua51_dump.py char3x\ui2_out\NewFaceData.lua

# 6. CLR surface
powershell: [Reflection.Assembly]::ReflectionOnlyLoadFrom('MovieEditor\bin64\MovieEngineCLR.dll')
```
