# Control modes P5 — animation resource + selection decode (static)

**Source:** `JX3RepresentX64.dll` (game client 1.5.0.9975). Dumps:
`proof/controls/p5/UpdateMoveAnimation.txt`, `UpdateDirection.txt`,
`UpdateFaceFootDirection.txt`.

## 1. `KRLRushState::UpdateMoveAnimation` (`0x1804C0700`)

Signature: `this = rcx` (rush state), `edx` = slot flag (used for the two
animation slots at the end).

```
if [this+0x7C] != 0                -> skip selection (goto slots)
if [this+0x80] == 0                -> return 0
if [this+0xA0] && sub_18011C1B()   -> skip selection
param = sub_1801252B([this+0xAC], [this+0xB0], [this+0xB4],
                     [this+0x74] != 0)          ; state -> animation param
if !param -> assert

speed = [this+0xD0]
; two selection passes over `param`, each: default / low tier / high tier
;   tier thresholds: low = f32 [param+0x50], high = f32 [param+0x68]
;   tiers write playback speed to [this+0x34] and the transition clip:
;     pass A: default clip [param+0x34], low [param+0x58], high [param+0x70]
;     pass B: default clip [param+0x44], low [param+0x60], high [param+0x78]
;   animation id written to [this+0x30]:
;     default: [param+0x30]; low/high (moving): [param+0x4C]
;   pass A id/speed pair: low [param+0x54], high [param+0x6C]
;   (pass B same ids; only the transition clips differ)
; pass B default branch also uses [param+0x88] when [this+0x114] == 1

; transitions (blend/start clip), only when flag != 0 and clip non-empty:
sub_180003D50(this, [this+0x80], 2, [this+0xAC], clip)
    success -> [this+0x1C] = 1

; finally drive both animation slots:
for i, slot in { [this+0x1B8], [this+0x1C0] }: play(slot, i, slotFlag)
```

Decoded facts:

- **Clip selection is data-driven per state**: three tiers (idle / low-speed /
  high-speed) chosen by comparing the character speed `[this+0xD0]` against two
  thresholds stored in the animation param struct (`+0x50`, `+0x68`). The host's
  hard-coded 45°/135°-style thresholds must come from this struct.
- Moving tiers share one animation **id** (`[param+0x4C]`) and differ in
  playback **speed** (`[param+0x54]` vs `[param+0x6C]`) and transition clip.
- Two transition-clip sets (A `+0x34/+0x58/+0x70`, B `+0x44/+0x60/+0x78`) and a
  combat/stance selector `[this+0x114] == 1` (set B default `+0x88`).
- Two animation slots (`+0x1B8`, `+0x1C0`) are driven per frame.
- Gate fields: `[this+0x7C]`, `[this+0x80]`, `[this+0xA0]`, `[this+0x74]`
  (feeds the param lookup), `[this+0xBC]` (transition enable), `[this+0x1C]`.

## 2. Param lookup chain — `sub_1801252B` → `0x18085CE60` (PART)

`sub_1801252B` is a thunk to `0x18085CE60`:

```
sub_18085CE60(ecx = [this+0xAC], edx = [this+0xB0],
              r8d = [this+0xB4], r9b = ([this+0x74] != 0)):
  base = [0x180EDDFE0]            ; same singleton as the camera rows
  if r9b && [base+0x26466] != 0:
      index = 0x3E7                ; 999 sentinel (mounted/vehicle override)
  else:
      index = edx                  ; state index
  return sub_180005204(base + 0x1A0, mode = ecx, index, ?)
```

The sibling routine at `0x18085CEB0` maps a raw index into a **10-entry
{u32 id, u32 limit} table** (`[table+4]` count, entries at `+0`/`+4`,
`index % count` when a flag is set, first 10 scanned linearly), i.e. the
state→kind mapping table behind the param struct.

### 2.1 Param fetch + table layout (HIGH)

`sub_180005204` → `0x180812D70` fetches one entry by key `{mode, index, ?}`:

```
sub_180812D70(table, mode, index, ?):
  zero key buffer (0x164 bytes)
  key = { mode, index, 0 }
  row = lookup(table, key)                  ; 0x18000A907
  if !row: key.index = 0; row = lookup(...) ; fallback (mode, 0, ?)
  if !row: key.? = 0; row = lookup(...)     ; fallback (mode, 0, 0)
  return row
```

`0x180812E00` is the ordered-map find: **binary search** over a contiguous
array of **0x54-byte entries** at `[container+0x1E2B8]` (count from
`[container+0x1E2C0]`, magic-division by 0x54), key compare `entry+0 == mode`,
`entry+4 == index`; returns the entry or 0. The 84-byte entry is exactly the

> **Correction (2026-10-06):** the 0x54-stride entry belongs to `CommonCharacterSFX`; the locomotion table is `PlayerRush` -> `Represent/player/player_rush.txt` (0x170 B rows, 73 fields, lookup 0x1806252A0). See `docs/character/3_2_3_3_LOCOMOTION_MOTION.md`.
param struct `UpdateMoveAnimation` reads (`+0x30…+0x88`).

**Data provenance:** the table is loaded with `KTableList::LoadBinTextTab`
(assert string `0x180CD02A0`) — a shipped BinText table (exact filename still
OPEN; not an inline string in Represent, likely supplied by the exe/loader
layer). Enumerating the entries is therefore a **runtime/data capture** (P4
probe or the table extractor), not further static disasm.

`[singleton+0x26466]` (the 999-sentinel mounted override) is set from a config
option lookup (`0x1803185E0`: option query at `0x180318607` → byte).

## 2b. Locomotion table data source — status (2026-10-02)

- The runtime host cannot enumerate the table (P4 probe: the MovieEditor host
  never instantiates the game-world singleton — `CONTROL_MODES_P4_PROBE.md` §2b).
- No loose locomotion table exists on disk: a depth-3 scan of the client
  install found only MovieEditor `PropertyTemplate\Action*.tab` (editor UI
  templates) and the extracted catalogs under `samples/player/catalog/`
  (`player_animation_f*.txt` = per-kind animation rows: AnimationID, KindID,
  SheathType, AnimationRatio, AnimationSpeed, IsLoop, AnimationFile,
  ShadowFile, 是否禁止自动转头, IsLookAtCamera, PoseState, 锁定朝向;
  `player_serial_animation_table.txt` = phased A/B/C animation sets, unrelated).
- The 84-byte locomotion param entry (two speed thresholds, three clip pairs)

> **Correction (2026-10-06):** closed - file identified (`player_rush.txt`, GBK BinText TSV, 263 rows); tier rule/thresholds 35/40/50 decoded; the P5 animation-id/playback-speed reads are actually 武器是否在背上/姿态 fields. See `docs/character/3_2_3_3_LOCOMOTION_MOTION.md`.
  is a **separate BinText table**; its rows are loaded by
  `KTableList::LoadBinTextTab`. Next probes, in order:
  1. In the **game client** binary, xref the `KTableList::LoadBinTextTab`
     assert string to the loader and inspect caller-supplied table names
     (the name may be a plain string at the call site).
  2. Enumerate the PakV4 index (the guessed `Data\filepath.ini` variants are
     not present) via the official PakV4 tooling; grep for `.tab` names.
  3. If the table ships in an hpkg pack, use `tools/netcode/extract_hpkg_member.py`
     once the pack/member name is known from (1).

## 3. `UpdateDirection` (`0x180533D40`) and `UpdateFaceFootDirection`

Dumps committed (`proof/controls/p5/`); decode next: the turn interpolation
(`tabCGAni` `KeepTurningFrame`/`TurningEpsilon`) and the immediate states
`{1,28,32}` path.

## Reproduce

```
.venv\Scripts\python.exe <inline disasm>      # capstone windows -> proof/controls/p5
```

Last verified: 2026-10-02.
