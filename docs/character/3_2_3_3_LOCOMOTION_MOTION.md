# JX3 Reborn — system points 3.2 (locomotion blend/kind map) & 3.3 (motion tags / root motion)

**Area:** character · **Branch:** research/character-animation · **Index:** `docs/character/README.md`

Deep-research report, 2026-10-06. Scope: verify + close the known open gaps in
`docs/controls/CONTROL_MODES_P5_ANIM.md` §2b / `CONTROL_MODES_TRACEABILITY.md`
A3/A10, `docs/netcode/SKILL_DATA_EXTRACTION.md` §4 (MotionTag), and
`docs/movement/JX3_DOUBLE_JUMP_RESEARCH.md` (in-place clips).

Client under test: `C:\SeasunGame\Game\JX3\bin\zhcn_hd` (bin64 DLLs + PakV4),
MovieEditor `C:\SeasunGame\MovieEditor`. All client installs read-only; every
extraction used the official `bin64\PakV4SfxExtract.exe` into scratch
(`C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\`). No repo file was
modified.

---

## Verified facts

| # | Fact | Evidence | Conf |
|---|---|---|---|
| V1 | The locomotion selection function `KRLRushState::UpdateMoveAnimation` (`JX3RepresentX64.dll` 0x1804C0700) fetches its per-state parameter row through `jmp` chain 0x18001252B → 0x18085CE60 → 0x180005204 → 0x180812D70 → 0x18000A907 → 0x1806252A0 | `proof/controls/p5/sub_1801252B.txt`, `sub_18085CE60.txt`; re-dumped scratch `lookup_thunks.txt`, `maplookup.txt` | HIGH |
| V2 | The param table is a contiguous array of **0x170-byte rows** at `[container+0x20308]`, count `[container+0x20310]`, container = `[0x180EDDFE0]+0x1A0`; binary search key = 3 u32 `{entry+0, entry+4, entry+8}` | 0x1806252A0 disasm (scratch `maplookup.txt`); enumerate helper 0x180630A90 reads same base/count with stride 0x170 | HIGH |
| V3 | That table object is registered as **`PlayerRush`**, format string `iiifiifippipppifippfippppppppifffffiifffpffpiifippiiiiiiiiiiiiiiiiiiiiiii` (73 fields), load call 0x18082E3C8, table object at manager+0x202C0 (`JX3RepresentX64.dll`) | loader sweep `loadbintab_summary.tsv`; region dump `tableregion.txt` | HIGH |
| V4 | `PlayerRush` resolves through `Represent\filepath.ini` (`KFilePath::LoadFilePath` 0x1803F0920, path string 0x1803F094C; `KRLTable::Load` 0x1803EAB50) to **`Represent/player/player_rush.txt`** | `filepath.ini` line `PlayerRush=Represent/player/player_rush.txt` (both mapviewer copy and client-pak extraction) | HIGH |
| V5 | The file is a GBK BinText TSV, 213,439 bytes, sha256 `8ef862af3318a86e24cef3da03108baa39099f0b84a998ee84eaaccaecc5b408`; extracted from the client's PakV4 with the official tool, and byte-identical to the mapviewer extraction copy | scratch `pakv4/out/represent/player/player_rush.txt`; mapviewer `...\jx3-web-map-viewer\tmp\nieyun-rush-out\represent\player\player_rush.txt` | HIGH |
| V6 | `PlayerRushParam` (0x18082E40F) is a **different** table → `Represent/player/player_rush_jump_count_param.txt` (jump-count phases), not the locomotion struct | `filepath.ini`; `loadbintab_summary.tsv` | HIGH |
| V7 | Row field mapping is exact (73 columns == runtime layout, no padding, p=8 bytes, total 0x170): +0x30 初始武器是否在背上, +0x34 移动(floor), +0x3C 移动(接近墙面), +0x44 移动(water), +0x4C 高速跑武器是否在背上, +0x50 速度修正值(f32), +0x54 姿态, +0x58 高速跑(地面), +0x60 高速跑(水面), +0x68 二阶速度修正值(f32), +0x6C 二阶姿态, +0x70 二阶高速跑(地面), +0x78 二阶高速跑(水面), +0x80 站立待机(floor), +0x88 站立待机(water) | layout computed from binary format string + file header, matches every `UpdateMoveAnimation` read in `proof/controls/p5/UpdateMoveAnimation.txt` | HIGH |
| V8 | Tier rule (0x1804C07CA..0x1804C081D ground pass): if `[[param+0x68]>0 && charSpeed >= [param+0x68]]` → clip `[param+0x70]`, pose→`[this+0x34]=[param+0x6C]`; else if `[[param+0x50]>0 && charSpeed >= [param+0x50]]` → clip `[param+0x58]`, pose `[param+0x54]`; else clip `[param+0x34]`. `charSpeed` = f32 `[this+0xD0]` (smoothed per frame, e.g. 0x1804BBF9F `if fabs(target-[this+0xD0]) > 1.19e-7 then [this+0xD0]=target`) | `UpdateMoveAnimation.txt`; scratch `rush_d0.txt` | HIGH (rule) / MED (unit) |
| V9 | Thresholds in the shipped F1/F2/M2 rows are only 35/40 (tier 1) and 50 (tier 2); 250/263 rows leave them empty (= disabled; the code requires > 0) | scratch stats over `player_rush_utf8.txt` | HIGH |
| V10 | `[this+0x114]==1` overrides the clip with 站立待机 (floor +0x80 / water +0x88); `[this+0x54]!=0` picks 移动(接近墙面) +0x3C; two animation slots at `+0x1B8`/`+0x1C0` are played each frame (`call 0x180020383(this, slotIndex, slotFlag)`, loop i<2) | `UpdateMoveAnimation.txt` + tail dump `uma_tail.txt` | HIGH |
| V11 | `pnForward`/`pnStrafeRight`/`pnRotationRight` are the **out-parameters of `GetMoveInfo`** (wrapper 0x180AD35A0; assert names at 0x180CC1400/.1410/.1420; engine impl 0x1805DFE90, thunk 0x18001AC8F): `pnForward=[ctrl+0x50]`, `pnStrafeRight=[ctrl+0x4C]`, `pnRotationRight=[ctrl+0x3C]` | `blend_xrefs.txt`; `setters.txt` | HIGH |
| V12 | Those fields are the controller's direct intent channels, written by the command enqueuers: Move 0x1805E0010 (`[ctrl+0x4C]=arg1, [ctrl+0x50]=arg2, [ctrl+0x54]=arg3`, cmd 5), SetRotation 0x1805E0360 (`[ctrl+0x3C]=arg1`, cmd 1), Jump 0x1805DFFF0 (`[ctrl+0x5C]/[ctrl+0x60]`, cmd 6), stop 0x1805E0030 (cmd 7). 8-way input is built upstream (`ResponseWASDKey` → `MOVE_*`, 8 directions; `docs/controls/CONTROL_MODES_LUA_ANNEX.md` §A2) | `setters.txt`; `ctx_reads`/`ctrl_reads.txt` | HIGH |
| V13 | There is **no blend table at 0x180CD8800**: the bytes are a heterogeneous pointer/descriptor array (qwords → 0x180D2EF40, 0x18000E8B8, 0x18001FEAB, …), not float weights | scratch `blend_table.txt` | HIGH |
| V14 | MotionTag data class exists in two tiers: serializer `KG3D_AnimationTagX64.dll` (`KG3D_AnimationMotionTag_Group_Data::LoadFromFile` @ 0x180003000, factory 0x180003540, `KG3D_CreateMotionTagListFromData` @ 0x180008E50) and runtime `KG3DEngineX64.dll` (`KG3DMotionTagData::LoadFromFile` @ 0x180299AC0, vtable @0x1806B31D8; `KG3DAnimationMotionTag::Helper_Apply` @ 0x180287320; header `KG3DAnimationMotionTag.h`) | string scan + disasm (scratch `motiontag_factory.txt`, `kg3dmotiontag_xref.txt`, `helper_apply.txt`) | HIGH |
| V15 | MotionTag version-1 stream layout (both loaders agree): read `u32` at this+0x158/+0x15C (engine: +0x50/+0x54), then per keyframe a **0x188-byte record**: `+0x100` u32 time/key index, `+0x104` u32 tag count, `+0x108+4*i` u32 tag payload size; tag payloads follow sequentially, each beginning with u32 type id | `motion_tag.txt` 0x1800030C8..; `kg3dmotiontag_xref.txt` 0x180299BE4.. | HIGH |
| V16 | 12 sub-tag types, factory jump table 0x180003A2C, per-type struct sizes (table @0x180047C60): 0:8, 1:0x118, 2:0x19C, 3:0x14, 4:0x10, 5:0xC, 6:0x2C, 7:0x30, 8:0x34, 9:0x68, 10:0x4C, 11:0x58; each object starts with u32 type id and ctor defaults (e.g. type 0: `[0]=0, [4]=1.0f`; type 2: weight 1.0f@0x10C, 100.0f@0x114, 1@0x110; type 9: -1@0x18, 1.0f@0x54) | `motiontag_factory.txt`; size table read @0x180047C60 | HIGH |
| V17 | 太阴指 root-motion measurement reproduces the committed proof exactly: `f1s01wh点穴19.ani`, 182 bones, 46 frames @33 fps, net horizontal 10.3698, path 392.5124, **max horizontal deviation 156.72** vs authored `.tani` block `(15.0, -156.32)` = 157.0 | scratch `taiyin_motion.json`; identical to `proof/netcode/skill_motion/taiyin_motion.json` | HIGH |
| V18 | The `.tani` float runs reproduce: `0x0358 n=12 0.00 1.00 0.00 130.82 284.49 1.00 1.00 1.00 0.01 0.99` and `0x06e8 n=8 0.00 1.00 0.00 15.00 -156.32 1.00 1.00 1.00` | scratch `run_motion.txt`; matches `docs/netcode/SKILL_MOTION_METHOD.md` | HIGH |
| V19 | The engine applies an animation's origin root transform **only for whitelisted paths**: `Represent/player/player_animation_use_originroot_trans.txt` (loader registration `PlayerAnimationUseOriginRootTrans` fmt `ip` @0x18082FDB8; lookup 0x18081BF40 via thunk 0x180021B4D; use site 0x18035514C passes the bool into the animation play call 0x180022E3F) | `filepath.ini`; extraction (1921 lines, all `Hash=0`); `roottrans_consumers.txt`, `roottrans_use_site.txt` | HIGH (table + lookup) / MED (semantics of downstream flag) |
| V20 | F1 ground/jump/dash clips are **not** in that whitelist (only 9 `F1b02yd*游泳*` rows exist for F1) → their root-bone translation is not used as actor displacement; movement comes from the gameplay/move-state layer | extracted table vs 太阴指/小跳/二段跳 paths | MED-HIGH |

### PlayerRush row map (0x170 bytes, keyed by 角色类型/门派/武器类型)

```
+0x00 i 角色类型 [key]      +0x50 f 速度修正值 (tier-1 threshold)
+0x04 i 门派   [key]        +0x54 i 姿态 (pose 1)
+0x08 i 武器类型 [key]      +0x58 p 高速跑(地面)
+0x0C f 默认移动速度(=35)   +0x60 p 高速跑(水面)
+0x10 i 进入是否使用残影    +0x68 f 二阶速度修正值 (tier-2 threshold)
+0x14 i 残影方式            +0x6C i 二阶姿态
+0x18 f 残影时间            +0x70 p 二阶高速跑(地面)
+0x1C i 残影个数            +0x78 p 二阶高速跑(水面)
+0x20 p 残影动作(floor)     +0x80 p 站立待机(floor)
+0x28 p 残影动作(water)     +0x88 p 站立待机(water)
+0x30 i 初始武器是否在背上  +0x90.. 进入/离开轻功 floor/water (4 ptrs)
+0x34 p 移动(floor)         +0xB0.. 残影颜色/落水等 + 阶段1..23 参数
+0x3C p 移动(接近墙面)      (73 columns total, total size = 0x170)
+0x44 p 移动(water)
+0x4C i 高速跑武器是否在背上
```

---

## Locomotion table hunt result

**IDENTIFIED (HIGH).** The "84-byte locomotion param entry" open question in
`CONTROL_MODES_P5_ANIM.md` §2b is closed, and the premise was partly wrong:

1. **Wrong table attribution in P5.** The 0x54-byte (84 B) stride binary search
   at 0x180812E00 belongs to the **SFX** table, not locomotion: it keys
   `{entry+0, entry+4}` over records at `[container+0x1E2B8]`, and that address
   is `CommonCharacterSFX`'s record array (`rcx+0x1E270` KRLTable object, +0x48
   = records; load site 0x18082C3C0, format `iipfpfiifffipiiifi`, name string
   "CommonCharacterSFX"; rows are 0x54 bytes). Evidence:
   `proof/controls/p5/table_loader_refs.txt` + scratch `loadbintab_summary.tsv`.
2. **The real locomotion table** is `PlayerRush`: 0x170-byte rows at
   `[container+0x20308]`, keyed by 3 ints, registered at manager+0x202C0 with
   the 73-field format above (load site 0x18082E3C8).
3. **Its file is `Represent/player/player_rush.txt`**, resolved by
   `Represent\filepath.ini` (`PlayerRush=Represent/player/player_rush.txt`,
   also `PlayerRushParam=Represent/player/player_rush_jump_count_param.txt`).
   Extracted from the client PakV4 with the official `PakV4SfxExtract.exe`:
   213,439 bytes, GBK BinText TSV, sha256 `8ef862…b408`, byte-identical to the
   mapviewer cached copy. 263 data rows, 73 columns, key = (角色类型, 门派,
   武器类型); role types 0/1/2/5/6, schools 0–22 plus **999** (96 rows — the
   999 sentinel used by the mounted/vehicle override in 0x18085CE60 matches).
4. **Format**: KRL BinText table (header line = GBK column names, tab-separated
   rows); the loader `KRLTable::Load` (0x1803EAB50) asserts
   `pszFilepathKey/pszFilepathValue/pszFormat`, `.krl`-style path resolution via
   `KFilePath` (0x1803F0920 `Represent\filepath.ini` + `CreateRLFile`
   from SemanticX64.dll). Runtime records are built with strings as 8-byte
   pointers, ints/floats 4 bytes, **no padding** (total 0x170 exactly matches
   the binary-search stride).
5. **`PlayerRushParam`** is a separate table
   (`player_rush_jump_count_param.txt`, 373 KB in the extraction tree) — it is
   the jump-count phase param table, *not* the locomotion struct.

Negative coverage (documented so the hunt need not be repeated): a full-tree
scan of `C:\SeasunGame` for `filepath.ini` and `player_rush*` found them only
under `SeasunDownloaderV2.4` mapviewer extraction trees; there is **no loose
copy of `player_rush.txt` in the client install root** — it lives in PakV4 and
is reachable with `pss_assets.run_pakv4(["represent\\player\\player_rush.txt"])`
(HIGH). `filepath.ini` itself is also in the paks
(`represent/filepath.ini`, 23,011 bytes, extracted).

Confidence: HIGH for file+format+path+key; MED for the unit of the speed
compare (see open questions).

---

## Thresholds & blend semantics

**Selection (ground pass, 0x1804C07A9..0x1804C081D, and water pass
0x1804C0884..0x1804C08E9):**

```
p = PlayerRush row (key = roleType, school, weaponType)

# special/standing override
if [this+0x114] == 1            -> clip = p.standIdleFloor / p.standIdleWater
elif [this+0x54] != 0 (pass A)  -> clip = p.moveNearWall
elif p.threshold2 > 0 && speed >= p.threshold2
                                -> clip = p.hiRun2Ground/Water ; [this+0x34]=p.pose2 ; [this+0x30]=p.hiRunWeaponOnBack
elif p.threshold1 > 0 && speed >= p.threshold1
                                -> clip = p.hiRunGround/Water  ; [this+0x34]=p.pose1 ; [this+0x30]=p.hiRunWeaponOnBack
else                            -> clip = p.moveFloor/Water   ; [this+0x30]=p.initWeaponOnBack
```

- `speed` = f32 `[this+0xD0]`, a smoothed per-frame scalar (0x1804BBF9F tracks
  a target with a 1.19e-7 epsilon). Thresholds in the shipped F1/F2 rows are
  35/40 (tier 1) and 50 (tier 2); the default move speed column is 35.
  Because `默认移动速度=35` and the thresholds are 35/40/50, the natural reading
  is the same unit as that column (an engine move-speed scalar), but the exact
  unit is not yet proven (OPEN; next probe: the target written at 0x1804BBF9F).
- Empty/0 thresholds disable their tier (code requires `> 0`).
- The P5 doc's "two transition-clip sets" (+0x34/+0x58/+0x70 vs
  +0x44/+0x60/+0x78) are not start/stop transitions — they are the **ground
  pass vs water pass** clip columns (floor/water); the same per-tier structure
  is duplicated for the water variants, and each pass can call the transition
  helper `0x180003D50(this, [this+0x80], 2, [this+0xAC], clip)` (gated by
  `[this+0xBC]`), which is a *start-of-clip* transition hook.
- The P5 "animation id" writes are corrected: `[param+0x30]` =
  初始武器是否在背上, `[param+0x4C]` = 高速跑武器是否在背上, and `[this+0x34]`
  receives 姿态 (pose state) — not playback speed.
- Two slots `+0x1B8/+0x1C0` are both played per frame via
  `0x180020383(this, i, slotFlag)` (i=0,1) with the function's `edx` slot flag.
  Which pass feeds which slot (and whether the water pass overwrites the ground
  pass unconditionally) is not fully decoded → OPEN.

**Blend params (A10) — resolved as "not a blend table":**

- `pnForward`, `pnStrafeRight`, `pnRotationRight` are the three out-parameters
  of the public `GetMoveInfo` binding (wrapper 0x180AD35A0, engine 0x1805DFE90,
  thunk 0x18001AC8F). Assert names live at 0x180CC1400/0x180CC1410/
  0x180CC1420; reads are `[ctrl+0x50]`, `[ctrl+0x4C]`, `[ctrl+0x3C]`.
- They are set by the intent command enqueuers: `Move` 0x1805E0010 stores
  `[+0x4C]`, `[+0x50]`, plus `[+0x54]` (third channel, unnamed) and queues
  command 5; `SetRotation` 0x1805E0360 stores `[+0x3C]` (command 1). So they are
  three scalar input channels, one per axis, not 8 entries with weights.
- The 8-direction mapping is upstream, in the shipped UI Lua
  (`ResponseWASDKey` builds `MOVE_FORWARD / RIGHTFORWARD / RIGHT / …`,
  `docs/controls/CONTROL_MODES_LUA_ANNEX.md` §A2, `interface`-derived LOW), and
  the engine receives the folded scalar channels.
- The cited "table ~0xCD8800" is **not** a weight table: hexdump at
  0x180CD8800 shows qword pointers into `.data`/`.text` (0x180D2EF40,
  0x18000E8B8, 0x18001FEAB, …) with zeros — a descriptor/handler array; no
  floats. There are no code xrefs to it (RIP-relative scan).

---

## MotionTag format

Derived from the two independent loaders (serializer + runtime) and the factory:

**Classes / anchors**

| Module | Symbol | Address |
|---|---|---|
| KG3D_AnimationTagX64.dll | `KG3D_AnimationMotionTag_Group_Data::LoadFromFile` (data-side loader) | 0x180003000 |
| KG3D_AnimationTagX64.dll | tag factory `switch(type 0..11)` | 0x180003540 |
| KG3D_AnimationTagX64.dll | `KG3D_CreateMotionTagListFromData` | 0x180008E50 |
| KG3DEngineX64.dll | `KG3DMotionTagData::LoadFromFile` (runtime loader) | 0x180299AC0 |
| KG3DEngineX64.dll | `KG3DAnimationMotionTag::Helper_Apply` | 0x180287320 |
| KG3DEngineX64.dll | runtime class vtable slot | 0x1806B31D8 (.rdata) |

**Signature** (from assert string): `LoadFromFile(IKG3D_BufferReader* piBuffer,
DWORD dwVersion, DWORD dwNumKeyFrames)`.

**Version handling (data-side 0x180003000)**

- `dwVersion == 0`: loop `dwNumKeyFrames` × `buffer->ReadObject(0x970, &p)` and
  hand each block to `sub_180003B10`; `[this+0x10] = dwNumKeyFrames*0x970`
  (memory accounting). (The 0x970 legacy per-keyframe block is the old flat
  format; its interior is processed by 0x180003B10 — not decoded in detail.)
- `dwVersion == 1`: read u32 → `[this+0x158]`, u32 → `[this+0x15C]`, then the
  keyframe vector (begin `[this+0x40]`, count `[this+0x48]`, stride 0x40).
- `dwVersion == 2`: read u32 `count`, drain `count` × `ReadObject(0x130)`, then
  fall into the version-1 path.
- anything else: error log `KG3D_AnimationMotionTagData::LoadFromFile(...)
  dwVersion %u error`, return 0x80004005.

**Keyframe record (version ≥ 1)**

The stream contains `dwNumKeyFrames` records. Each record object is read with
size **0x188**; proven fields (both loaders):

```
+0x100 u32  key time / frame index          (appended to u32 vector [this+0x18])
+0x104 u32  tag count n
+0x108 u32  tagSize[0..n-1]                 (per-tag payload byte size)
(record also carries a KG hash string, converted by
 KG3D_ConvertToOriginalHashString and stored at the 0x40-byte entry's +0)
```

Then `n` tag payloads follow sequentially; each payload starts with `u32 type`
(0..11) and its length is `tagSize[i]`. The reader calls
`factory(type)` (0x180003540) and deserializes with
`min(size_table[type], tagSize[i])` (clamp) via 0x180044263. The per-keyframe
0x40-byte entry layout: `+0` hash string, `+8` vector of tag object pointers
(count `+0x10`, capacity `+0x14`).

**Sub-tag type table (12 types)** — factory jump table 0x180003A2C; the size
table used for clamping is at 0x180047C60 and equals the allocation sizes:

| type | size | ctor defaults / notable fields |
|---|---|---|
| 0 | 0x08 | `[0]=0, [4]=1.0f` |
| 1 | 0x118 | `[0]=1`, 0x110 zeroed block |
| 2 | 0x19C | `[0]=2, [0x10C]=1.0f, [0x110]=1, [0x114]=100.0f`, 6×zero xmmword arrays |
| 3 | 0x14 | `[0]=3`, `[4..0xB]=1.0f` |
| 4 | 0x10 | `[0]=4` |
| 5 | 0x0C | `[0]=5` |
| 6 | 0x2C | `[0]=6` |
| 7 | 0x30 | `[0]=7, [0xC]=-1` |
| 8 | 0x34 | `[0]=8, [0x10]=1.0f` |
| 9 | 0x68 | `[0]=9, [0x18]=-1, [0x54]=1.0f` |
| 10 | 0x4C | `[0]=10` |
| 11 | 0x58 | `[0]=11` |

**Runtime side** (0x180299AC0, KG3DEngineX64): versions 0/1/2; for v1 it reads
u32 `[this+0x50]`, u32 `[this+0x54]`, then per keyframe `Read(0x188)`; it indexes
`record+0x100` (time; inserted as `time<<16|0xFFFE`), `record+0x104` (count),
`record+0x108+4i` (sizes), allocates the runtime tag list and streams the tag
payloads — i.e. it consumes exactly the layout above. `KG3DAnimationMotionTag::
Helper_Apply` (0x180287320) is the per-tag apply path into the scene.

**Container PROVEN (2026-10-06, HIGH).** The on-disk container is the **`.tani` file
itself** (`GATA` magic), read by `KG3DAnimationTagDataContainer::_Load`
(`KG3DEngineX64.dll` 0x180291490; strings `KG3DAnimationTagDataContainer::{Load,_Load,
LoadFromFileMultiThread,ExportTani}`, `%s%s%s.tani`):

- header (0x130 bytes): `GATA` magic (u32 0x41544147), version u32, base-`.ani` path
  (GBK, 0x103 bytes), block count u32 @0x10C;
- then per block a 12-byte header `{u32 type, u32 version, u32 keyCount}`; a non-zero
  keyCount creates the class object via factory 0x180290C00 and calls its
  `LoadFromFile` (vtable slot +0x10), which consumes the payload from the same reader;
  keyCount == 0 = empty block (type 2 v1/2 consumes 8 bytes);
- factory/RTTI mapping (vtable COLs verified): **type 0 = `KG3DSFXTagData`**
  (vtable 0x1806B3718), **type 1 = `KG3DSoundTagData`** (0x1806B3A58),
  **type 2 = `KG3DMotionTagData`** (0x1806B31C8);
- Motion payload (version 1): u32 + u32, then per keyframe a 0x188-byte record
  (`hash` string @+0x00, time u32 @+0x100, tag count u32 @+0x104, sizes u32[]
  @+0x108) followed by the tag payloads sequentially (each begins with u32 type id).

**Verified on real files (byte-exact).** `F1s01wh点穴19_太阴指_跳.tani` (8328 B):
header `GATA` v1, blocks=3; the type-2 block sits at 0x1EE4 {v1, keyCount=1} and its
payload parses to **exactly EOF 0x2088** — key time=6, hash `User Define Tag`,
1 tag (type 0, 8 B, payload `…80 3f` = 1.0f). Full scan: 23 `.tani` samples, 15
carry a parseable type-2 motion block (all single-key `User Define Tag` tags), 8
none (v0 / empty motion blocks) — scan output summarized in the tool run.

**Correction to the earlier float-run interpretation (MED, re-open).** The
"authored motion vector" float runs (`(0,1,0) | (15.0, −156.32) | (1,1,1)`, e.g.
太阴指 @0x06E8) do **not** sit in the type-2 motion block (which holds only the
8-byte `User Define Tag`); they lie inside the earlier **type-0 SFX block payload**
region (the same block holds the PSS path string and the `_rh` socket bind). Their
displacement correlation with the `.ani` root arc may therefore be an SFX binder
offset rather than MotionTag data — next probe: decode `KG3DSFXTagData::LoadFromFile`
(vtable slot 2 = 0x18029E0B0) to attribute the floats. The tool
`tools/character/motion_tag.py` (selftest 11/11) parses the container and dumps the
motion stream; use `--tsv` for the flat record table.

---

## Root motion usage

**Reproduction (target 5).** The `tools/netcode/measure_skill_motion.py`
pipeline was re-run on **太阴指** with the official PakV4 extractor into scratch
(no repo writes): tani `F1s01wh点穴19_太阴指_跳.tani` → base ani
`f1s01wh点穴19.ani` (182 bones, 46 frames @ 33 fps). Results are byte-for-byte
identical to the committed proof JSON:

- net horizontal root displacement 10.3698 u, path length 392.5124 u,
  **max horizontal deviation 156.72 u at frame 15**;
- authored `.tani` motion block `(15.0, -156.32)` → magnitude 157.0 u (0.2 %
  agreement), plus a second `(130.82, 284.49)` block.

So the authored 2-D vector in the `.tani` and the `.ani` root-bone arc encode
the same displacement (HIGH).

**Jump clips claim re-checked (correction).** Extracting the three F1 jump
clips from PakV4 and reading bip01 (bone 0) per frame:

| clip | frames | x range | y range | z range | net horizontal |
|---|---|---|---|---|---|
| `f1b02yd小跳b.ani` | 21 | **0.000** | **0.000** | **0.000** | **0.000** |
| `f1b02yd小跳a.ani` | 23 | 6.361 | 3.331 | 72.101 | 35.010 |
| `f1b02yd二段跳a.ani` | 25 | 9.993 | 1.205 | 85.940 | 57.157 |

Only `小跳b` is fully in-place. `小跳a` and the double-jump flip `二段跳a`
carry substantial horizontal root translation (the vertical arc is still the
physics result, y ranges ≈ 1–3 u). The double-jump doc's generalization
"the jump clips are in-place (no root motion)" is therefore refuted; the claim
holds only for `小跳b` (Y = 0 on every frame).

**Which source the engine uses for gameplay displacement.** Engine evidence:

- The engine's root-motion use is **whitelist-driven per animation path**:
  `Represent/player/player_animation_use_originroot_trans.txt`
  (path-keyed KRL table; lookup 0x18081BF40 hashes the path with
  `cstr_path`/0x180856FC0 and `_stricmp`-compares `record+4`; the call site
  0x18035514C passes the found/non-found bool into the animation play call
  0x180022E3F). The table has 1920 rows, all `Hash=0` (the hash column is
  populated at load time), and **F1 only has 9 rows, all swimming
  (`F1b02yd…游泳…`)** — no F1 ground jog/run/jump/dash clip is listed.
- Therefore for the F1 locomotion/skill/jump clips the engine does **not** use
  the clip's origin root transform as actor displacement; movement comes from
  the gameplay layer (move states / skill-move / server-synced positions), and
  the root-bone curve is authored data whose horizontal extent matches the
  scripted displacement (太阴指 156.7 ≈ 157.0). This is consistent with the
  netcode research (client-side prediction + server authority,
  `docs/movement/JX3_CHARACTER_MOVEMENT_RESEARCH.md` §6) — the `.tani` motion
  block is best read as the authored skill-move intent that the client/server
  move layer consumes, while the `.ani` root arc is the animation counterpart.
- Confidence: HIGH for the table+lookup+call site, MED-HIGH for the
  interpretation "flag=false ⇒ root translation not applied to actor";
  the exact downstream plumbing inside 0x180022E3F is one layer undecoded.

- The MotionTag runtime (`KG3DMotionTagData`, `KG3DAnimationMotionTag::Helper_
  Apply`) is the animation-event system (SFX/IK/force-feedback/weapon-motion
  tags with keyframe hashes) — separate from the root-bone translation path.

---

## Host integration (W5.5, 2026-10-06)

The client now applies the authored `.tani` motion vector to host displacement on
skill cast: `client/SkillMotion.cs` (GATA check + signature scan) + the
`RebornClient.cs` skill-cast/tick hook; env `RC_SKILL_TANI=<os path>`,
`RC_SKILL_DASH_MS=450` (dash duration), `RC_SKILL_AT=ms` (deterministic cast).

- **Selector (provisional).** Last non-zero signature run `(0,1,0 | dx,dz | w,w,w)`
  in file order; all candidates are logged. The SFX sub-tag semantics are still
  undecoded (W5.2), so the selection is a stopgap — re-open with the SFX loader
  decode (`KG3DSFXTagData::LoadFromFile`, vt slot 2 = 0x18029E0B0), which also
  attributes the floats' entry frame.
- **Direction.** The authored **magnitude** is applied along the character facing;
  the vector's entry-frame axes are not mapped yet (same W5.2 re-open). The
  magnitude is the verified quantity (see below).
- **Motion.** Position-tracked dash (target = start + unit×progress) so the
  engine's integer-per-tick position round cannot eat the fractional tail; runs
  through the same collision path as input movement (substeps < capsule radius),
  so walls block it (observed: demo-wander into a wall → dash moved 0.4 u).

**Driven verification (log `reborn_20261006_211409.log`).** Run:
`RC_STARTUP=nodb RC_SKILL_AT=3000 RC_SKILL_TANI=…\w55_taiyin_wu.tani`
(standing at spawn, open ground, no demo movement):

```
skillmotion cast: authored=(15.000,-156.315) mag=157.033 dur=450ms
  dir=(0.000,1.000) start=(18991,33853) info=sel@0x6e8 (15.000,-156.315)
  mag=157.033 candidates: [0x358:130.82,284.49|313.13] [0x488:0.00,0.00|0.00]
  [0x5b8:0.00,0.00|0.00] [0x6e8:15.00,-156.32|157.03]
skillmotion done: end=(18991,34010) moved=157.0 maxdev=157.0 authored=157.033
```

moved **157.0 u** vs authored 157.033 (0.02 %) and vs the measured `.ani` root
max deviation 156.72 u (0.18 %) — the authored vector is applied to host
displacement (HIGH for the applied magnitude; MED for direction/selector, W5.2).

---

## Corrections to existing docs

1. **`docs/controls/CONTROL_MODES_P5_ANIM.md` §2.1** — "the 84-byte entry is
   exactly the param struct `UpdateMoveAnimation` reads": wrong. The 0x54-stride
   table at `[container+0x1E2B8]` is `CommonCharacterSFX`; the locomotion param
   table is `PlayerRush`, stride **0x170**, at `[container+0x20308]` (lookup
   0x1806252A0, not 0x180812E00).
2. **`CONTROL_MODES_P5_ANIM.md` §2b** — "no loose locomotion table on disk;
   exact filename still OPEN": **closed**. File = `Represent/player/
   player_rush.txt` via `Represent/filepath.ini` key `PlayerRush`; format =
   GBK BinText TSV; row = 0x170 B packed (`i/f`=4 B, `p`=8 B, no padding);
   key = (角色类型, 门派, 武器类型) with school-999 mount rows.
3. **`CONTROL_MODES_P5_ANIM.md` §1** — "[animation id written to +0x30]" /
   "moving tiers share one animation id `[param+0x4C]`" / "playback speed to
   `[this+0x34]`": the fields are 初始武器是否在背上 (+0x30), 高速跑武器是否在背上
   (+0x4C), and 姿态 (+0x54/+0x6C) — boolean/pose semantics, not IDs/speeds.
4. **`CONTROL_MODES_TRACEABILITY.md` A3/A10** — A3 (exact thresholds) now has
   the file + rule (tier2 ≥ 二阶速度修正值, tier1 ≥ 速度修正值, values 35/40/50,
   empty = disabled); A10 (`pnForward/pnStrafeRight/pnRotationRight`, "table
   ~0xCD8800") is mis-scoped: they are `GetMoveInfo` out-parameters reading
   controller fields +0x50/+0x4C/+0x3C, written by the Move/SetRotation
   enqueuers; 0x180CD8800 holds no blend weights.
5. **`docs/movement/JX3_DOUBLE_JUMP_RESEARCH.md` line 34** — "the jump clips
   are in-place (no root motion)": only `f1b02yd小跳b.ani` is (all axes 0 over
   21 frames); `f1b02yd小跳a.ani` and `f1b02yd二段跳a.ani` carry root
   translation (net 35.0 / 57.2 u, z-range 72.1 / 85.9 u). The vertical arc is
   physics; horizontal root motion is present.
6. **`docs/netcode/SKILL_DATA_EXTRACTION.md` status table** — "MotionTag
   structure IN PROGRESS" can move to **decoded (structure)**: version
   dispatch, 0x188 keyframe record fields, 12 tag types + sizes, both loaders;
   remaining gap = container file + type semantics.
7. **`docs/netcode/SKILL_MOTION_METHOD.md` §5** — add that the runtime class is
   `KG3DMotionTagData` in `KG3DEngineX64.dll` (`LoadFromFile` 0x180299AC0,
   vtable 0x1806B31D8) and the apply path is `KG3DAnimationMotionTag::
   Helper_Apply` 0x180287320; the tag payloads are keyed by (time, type) and
   the sub-tag sizes are fixed (table above).

---

## Open questions + next probes

1. **`[this+0xD0]` speed unit / writer.** The value is smoothed at
   0x1804BBF9F/0x1804BC2AF toward an xmm9 target. Probe: disassemble backwards
   from 0x1804BBF40 to find the target computation (likely a move-speed field or
   normalised run factor); compare with `number.krl` walk/run (`docs/netcode/
   README.md` constants). Expect to settle whether thresholds 35/40/50 are
   u/frame or a ratio.
2. **Slot/pass mapping.** Decode `0x180020383(this, slotIndex, slotFlag)` and
   the slot objects at `+0x1B8/+0x1C0`; determine why the second (water) pass
   always runs and whether `[this+0x54]`/`[this+0xBC]` gate it. Probe: static
   read of 0x180020383 + a `camera_smoke`-style offline check of the row
   selection for a synthetic (role, school, weapon, speed) vector.
3. ~~**MotionTag container file.**~~ **DONE** — container PROVEN (`.tani` GATA,
   `KG3DAnimationTagDataContainer::_Load` 0x180291490; see the section above).
   Remaining sub-item: the SFX-block float-run attribution
   (`KG3DSFXTagData::LoadFromFile`, vt slot 2 = 0x18029E0B0) — decides the
   selector/direction stopgap in W5.5.
4. **Sub-tag type → semantic names.** Decode the switch in
   `KG3DAnimationMotionTag::Helper_Apply` (0x180287320) and the event-manager
   handlers; map 0..11 to `ForceField/IK/SFX/KeepFollowPositionSound/
   WeaponMotion/...`. (Delete jump table located @0x180003AE0, 12 entries;
   naming via RTTI still unresolved.)
5. **Version-0 0x970 block interior** (0x180003B10): the only remaining
   un-decoded MotionTag path; likely the pre-BinText editor format.
6. **`PlayerRush` mounted rows**: rows with 门派=999 — confirm they are the
   mounted-override set selected by the 0x3E7 sentinel at runtime (probe:
   `RC_PROBE_CONTROL` telemetry of the lookup key while riding, or static
   trace of the mount flag writer `[this+0x74]`).

---

## Host wiring update (2026-10-06, agent/3x-rig)

- **Loader:** the client reads the extracted table via `RC_LOCO_TABLE` (GBK TSV,
  header-matched columns), selects the row by (role, school, weapon)
  (`RC_LOCO_ROLE/SCHOOL/WEAPON`, default 6/0/0), and uses 移动（floor） as the
  ground-move clip. Verified in-engine: `clip=F1bqg加速跑02b_陆.tani` while running
  (`reborn_20261006_194543.log`), no AV; fallback (no table) keeps the previous
  `f1b02yd奔跑.ani` behaviour (`reborn_20261006_194655.log`).
- **Tier rule implemented but gated OFF** (`RC_LOCO_TIERS=1` enables): the F1 tier
  clip `F1bqg丐帮疾轻功烟尘.tani` AVs the host engine right after `PlayAnimation`
  rc=0 (`reborn_20261006_194627.log` tail) - each tier clip must be individually
  verified in-engine before it is enabled (the base move clip is verified).
- **PROVISIONAL (registered):** the comparator scalar = 默认移动速度 for normal
  movement, x2 under the host's 10x sprint (`RC_LOCO_SCALAR` test override); the
  engine's `[this+0xD0]` writer/unit is still untraced (open question 1). Re-open
  when the writer is decoded.

## Reproduce

Scratch scripts (all under `C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\`):

```powershell
$py = "C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"
# 1) locomotion table from the client PakV4 (official extractor)
& $py -c "import sys; sys.path.insert(0, r'C:\Users\Zhibin Ren\Desktop\reborn'); import pss_assets; from pathlib import Path; print(list(pss_assets.run_pakv4([r'represent\player\player_rush.txt', r'represent\filepath.ini', r'represent\player\player_animation_use_originroot_trans.txt'], work=Path(r'C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\char3x\pakv4')).items()))"
# 2) row layout / threshold stats
& $py sum_loadbintab.py          # loader sweep -> loadbintab_summary.tsv
& $py run_motion.py              # 太阴指 tani+ani measurement + float runs
& $py run_jump_check.py          # 小跳a/b, 二段跳a root ranges
# 3) disasm evidence
& $py "C:\Users\Zhibin Ren\Desktop\reborn\tools\netcode\dump_va.py" "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3RepresentX64.dll" 0x180812D70 0x180812F40 0x1806252A0 0x180625420
& $py "C:\Users\Zhibin Ren\Desktop\reborn\tools\netcode\dump_va.py" "C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineX64.dll" 0x180299AC0 0x180299D00
```

Provenance: all client files read-only; `player_rush.txt` extracted from the
game client's PakV4 by `bin64\PakV4SfxExtract.exe` (sha256
`8ef862af3318a86e24cef3da03108baa39099f0b84a998ee84eaaccaecc5b408`); the
mapviewer cached copy is byte-identical but was not relied on as primary
evidence.

Last verified: 2026-10-06.
