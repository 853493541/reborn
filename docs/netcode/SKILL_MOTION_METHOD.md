# How we extracted skill motion (dash) data from the client

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Question answered:** "a dash — how far does it dash?" — can we get real numbers from the local client?
**Answer:** yes, two independent client-side sources correlate: authored motion vectors in `.tani` and animation root motion in `.ani`.

---

## 1. The pipeline (discovered by following name → VFS → bytes)

```
skill name (e.g. 太阴指)
  │
  ├─ 1. index lookup   ResourcePack\Tani.rt            (GBK TSV: ID, display, logical path, tree path)
  │                     → data\source\player\f1\动作\F1s01wh...太阴指....tani
  │
  ├─ 2. extract        pss_assets.run_pakv4()           (official PakV4SfxExtract.exe)
  │                     pathlist must be GBK; cwd = bin64
  │
  ├─ 3. parse header   tani.py parse_tani()             GATA magic + version + cstring base .ani path
  │                     → data\source\player\f1\动作\f1s01wh花穴19.ani
  │
  ├─ 4. extract .ani   run_pakv4() again
  │
  ├─ 5. decode MIN2    min2.load_min2_stick()           182 bones; positions_at(frame) = world pos per bone
  │
  ├─ 6. root motion    positions_at(f)[0] = bip01        per-frame steps + distance-from-start curve
  │
  └─ 7. motion blocks  dump_motion_floats.py            runs of plausible floats in the .tani bytes
```

## 2. How the motion-vector blocks were found

`dump_motion_floats.py` scans the raw `.tani` for runs of >= 8 consecutive plausible
floats (finite, |v| < 2000, at least 4 non-trivial). In dash tanis it found a
recurring shape:

```
0x0358  n=12  0.00 1.00 0.00 | 130.82 284.49 | 1.00 1.00 1.00 0.01 0.99
0x06e8  n=8   0.00 1.00 0.00 |  15.00 -156.32 | 1.00 1.00 1.00
```

Reading: `(0, 1, 0)` up/axis marker, then a **2D vector** `(x, z)`, then scale/weight values.
For 玉泉鱼跃: `(76.88, -61.75)` followed by `0.70 0.70 0.70`.

## 3. The control experiment that proved it

The same scan on a **non-dash** skill (藏剑·风来吴山, a spin) returns:

```
F1s07cj重剑技能15_风来吴山HD.tani  runs=0
```

Zero motion blocks, and its root motion net displacement is 0.0. Dashes have
blocks; the spin does not. The interpretation is therefore data-driven, not assumed.

## 4. Correlation between the two sources

| Skill | motion block `(x, z)` | magnitude | root arc peak (measured) |
|---|---|---|---|
| 太阴指 | `(15.0, -156.32)` | 157.0 | **156.7** |
| 太阴指 | `(130.82, 284.49)` | 313.1 | (second motion in same tani) |
| 玉泉鱼跃 15a | `(76.88, -61.75)` | 98.6 | small in-place sway |
| 风来吴山 (control) | none | — | 0.0 |

The root-motion peak for 太阴指 matches the authored vector magnitude to within
0.2%, so both encode the same displacement.

## 5. Confirmed against the tag loader

`KG3D_AnimationTagX64.dll` contains:

```
KG3D_AnimationMotionTag_Group_Data::LoadFromFile
KG3D_AnimationMotionTagData::LoadFromFile(IKG3D_BufferReader* piBuffer, DWORD dwVersion, DWORD dwNumKeyFrames)
```

The disassembly (`proof/netcode/disasm/motion_tag.txt`) shows per-keyframe records
(0x40 bytes) with a hash string and a list of up to 12 typed sub-tag payloads loaded
by a type table — i.e. the motion vectors above are typed motion keyframes.

## 6. Caveats

- Numbers are **engine units**; converting to meters needs a calibration reference
  (character height or a known 尺 range). Not yet calibrated.
- Live in-game displacement/damage is server-authoritative; the client carries the
  authored/visual motion. Exact live values arrive via synced protocol messages.
- The motion-block field roles are inferred from correlation (MED-HIGH); a complete
  MotionTag payload parser would settle field names.

## 7. Reproduce

```powershell
# in worktree C:\Users\Zhibin Ren\Desktop\reborn-netcode
python tools\netcode\measure_skill_motion.py --name 太阴指 --branch f1 --json proof\netcode\skill_motion\taiyin_motion.json
python tools\netcode\dump_motion_floats.py proof\netcode\skill_motion
```

Evidence: `proof/netcode/skill_motion/*.json`, `proof/netcode/disasm/motion_tag.txt`,
`proof/netcode/KG3D_AnimationTag_strings.txt`.
Tools: `tools/netcode/measure_skill_motion.py`, `dump_motion_floats.py`, `find_skill_tables.py`.
