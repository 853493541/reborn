# PSS/PAR format — native findings (no map-viewer)

Source of truth: original `.pss` bytes + the XML written into them by
MovieEditor itself. Map-viewer is banned (`SFX_GROUND_RULES.md`).

## Container

```
PAR\0
u16  version (1)
u16  (1)
u32  block_region_end        # TOC end + sum(block sizes)
u32  emitter_count
emitter_count × { u32 type; u32 offset; u32 size }    # 12-byte TOC, blocks contiguous
<blocks>
PACK
u32  pack_size
<zip archive>                 # single entry named PSEF
```

## The PSEF entry is the spec

`PSEF` unzips to a UTF-8 `DataStorage` XML — MovieEditor's authored scene.
It contains:

- `<DataStorage>` global attrs (`LodDistance0..4`, `MaxParticls`, `AABBox*`,
  `RenderOrder`, `EnablePostRender`, `IsAutoLod`, ...).
- one `<Emitter>` per effect layer with real attrs:
  `Name`, `ID`, `DurationTime`, `DelayTime`, `Interval`, `RepeatTimes`,
  `ParticleForever`, `MotionType`, `FaceType`, `BlendType`,
  `ParticleCreateMode`, `ParticleEvent`, `UVMotionType`, `ParticleScaleType`,
  `Significance`, `PlatformFlags`, `IsShowEmitter`, `LodPercent0..8`.
- `<Module ModuleType="...">` children with typed `EMData` trees:
  `Emitter Shape`, `Emitter Spawn`, `Particle LifeTime`, `Particle Size`,
  `Particle Size LifeTime`, `Particle Size Scale LifeTime`, `Particle Material`,
  `Particle Type`, `Particle Color LifeTime`, `Emitter Rotation`,
  `Emitter Translation`, `Particle Velocity`, `Particle Rotation`,
  `Particle Rotation LifeTime`, `Particle UV Ani`, `Particle Sub UV`,
  `Particle Split UV`, `Particle Force Filed`, `Particle Forve WhirlWind`,
  `Camera`, ...
- resource strings as `PEVariant Type="String"`: material `.def`/`.jsondef`,
  texture `.tga`/`.dds`, mesh `.Mesh` (inside `Particle Type` when
  `ActiveType="Particle Type Mesh"`).

## Binary blocks

- type 0: global floats; observed `2000, 5000, 8000, 9000, 8000` — these equal
  `DataStorage` `LodDistance0..4`, so they are LOD distances, not event timing.
- type 1: serialized sprite layer (material path at +0x0C, texture slot
  labels/paths at ~+0x13C/+0x240, +0x344/+0x448, +0x54C/+0x650, module names,
  120-byte fixed tail).
- type 2: launcher block; leading GBK name `<Emitter>_模型粒子`/`_普通粒子`
  (or a custom emitter name), numeric tables, optional `.Mesh` string.
- Binary block count = 2 × XML emitter count (type1 + type2 per emitter), so the
  XML is the authoritative emitter list.

## Caution

`LodDistance0..4` must **not** be used as event start/play timing. Event timing
belongs to the GATA `.tani` event entry; parse that natively (`tani.py`).
