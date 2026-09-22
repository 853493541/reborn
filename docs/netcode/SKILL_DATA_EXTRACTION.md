# How we extracted real skill data from the client (method note)

**Branch:** `research/jx3-netcode`
**First used:** 2026-09-21, for "how far does a dash go?"
**Status:** method proven; units not yet calibrated to meters.

This note records the *exact* path that produced the skill-motion findings so it
can be repeated for any ability without re-discovery.

---

## 1. Pick the skill and find its `.tani`

JX3 attaches gameplay tags (SFX, sound, motion) to a **GATA `.tani`**, which
points at a base **MIN2 `.ani`** animation.

MovieEditor ships a GBK table mapping every animation to its logical VFS path:

`C:\SeasunGame\MovieEditor\ResourcePack\Tani.rt` — columns:
`ID \t DisplayName \t data\source\...\x.tani \t Root\官方资源\...`

```python
rows = [line.split("\t") for line in Tani_rt_text.splitlines()]
# match display or logical name: e.g. "太阴指", "玉泉鱼跃", "风来吴山"
```

Tool: `tools/netcode/measure_skill_motion.py` (`find_tani_rows`).

## 2. Extract from PakV4 (no manual pak parsing)

`pss_assets.run_pakv4(entries)` wraps the official
`C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PakV4SfxExtract.exe`
(GBK pathlist + cwd = `bin64`) and returns `logical path -> bytes`.

```python
import pss_assets
found = pss_assets.run_pakv4([tani_logical], work=scratch_dir)
```

Note: `FolderTree.xml` in `MovieEditor\ResourcePack` is editor-only — no
`data\source` paths. The pak `.idx` files are binary, so **Tani.rt/Ani.rt are
the practical file-name index** for extraction.

## 3. Read the `.tani` header

`GATA` magic (4) + version u32 + null-terminated GBK base-ani path:

```
GATA | u32 version=1 | data\source\player\f1\动作\f1s01wh花穴19.ani\0 | <tags...>
```

Tool: `tani.parse_tani` (also extracts pss/sound/other path strings).

## 4. Decode the base `.ani` and measure root motion

Extract the `.ani` the same way, then `min2.load_min2_stick(path)` gives a
182-bone `Min2SkelClip`. Root bone is `bip01` (index 0):

```python
clip = load_min2_stick(ani_path)
for f in range(clip.frame_count):
    x, y, z = clip.positions_at(f)[0]
    # horizontal distance from frame 0 = dash curve
```

`fps` is 33 for these clips. Output JSON: `proof/netcode/skill_motion/*.json`.

## 5. Read the authored motion blocks in the `.tani`

Scan the binary for runs of plausible floats. Dash tani files contain blocks
shaped like:

```
0.00 1.00 0.00   <dx> <dz>   1.00 1.00 1.00 ...
```

Tool: `tools/netcode/dump_motion_floats.py` (runs of >= 8 finite floats,
|v| < 2000). The `(dx, dz)` pair is the displacement; its magnitude matches the
animation root arc, so both encode the same authored motion.

## 6. Validate with a non-dash control

Run the same pipeline on a skill with no displacement (风来吴山 = spin):

- root net displacement `0.0`
- float-run scan: **0** motion blocks

So motion blocks + root arc are a reliable dash signal, not parser noise.

## 7. Results (F1 branch)

| Skill | `.tani` motion blocks (engine units) | root arc peak | frames @ fps |
|---|---|---|---|
| 太阴指 (万花) | `(130.8, 284.5)`, `(15.0, -156.3)` | 156.7 (matches 157.0 magnitude) | 46 @ 33 |
| 玉泉鱼跃 15a (藏剑) | `(76.9, -61.8)` | 5.6 net | 20 @ 33 |
| 风来吴山 (control) | none | 0.0 | 51 @ 33 |

Raw evidence: `proof/netcode/skill_motion/{taiyin,yuquan,flws_control}_motion.json`.

## 8. Reproduce

```powershell
$py = "C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"  # pefile/capstone venv
cd C:\Users\Zhibin Ren\Desktop\reborn-netcode
python tools\netcode\measure_skill_motion.py --name 太阴指 --branch f1 --json proof\netcode\skill_motion\taiyin_motion.json
python tools\netcode\dump_motion_floats.py proof\netcode\skill_motion
```

Requires the JX3 client + MovieEditor installs on this machine (GBK tables and
PakV4). Extracted `.tani`/`.ani` are git-ignored (proprietary); only JSON
measurements and tools are committed.

## 9. Confidence and open questions

| Item | Confidence | Note |
|---|---|---|
| tani → ani → bone pipeline | HIGH | reproduced on 3 skills |
| motion float blocks are displacement | MED-HIGH | correlates with root arc; absent for non-dash; full layout decode pending |
| engine units → meters | OPEN | needs one calibration reference (known range in 尺, or in-game measurement) |
| MotionTag structure (keyframes, sub-tag types) | IN PROGRESS | loader disassembled: `proof/netcode/disasm/motion_tag.txt`, `KG3D_AnimationMotionTag_Group_Data::LoadFromFile` @ `0x180003000` |

Effects (particles/SFX) use the same tani: `tani.py` yields `.pss` paths that
`pss.py` + `pss_assets.py` already parse (emitters, materials, meshes, textures).
