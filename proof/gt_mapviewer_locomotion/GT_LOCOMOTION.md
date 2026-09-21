# Map-viewer GT — 花萝 走路 + 跳跃

Staged 2026-09-20. Charge-skill focus dropped per Andy RESET.

## Character

| Field | Value |
|-------|-------|
| Preset | 花萝 |
| Body | F1 |
| FBX actor | `samples/player/actors/f1_hualuo/` |

## 走路 / 行走

| Field | Value |
|-------|-------|
| UI label | 行走 (catalog); Andy: 走路 |
| Playable | `samples/player/moves/from_mapviewer/f1/动作/f1b02yd行走.ani` |
| Format | MIN2 |
| Size / sha256 | 83743 / `cd10337ace1dec0f40715835a46c9fe95933ccf548eb64b5e37fa53f68deecad` |
| Stick | 182 × 43 @ 28.0 fps |
| Alt | `f1b04ty行走.ani` |

## 跳跃 (map-viewer 小跳)

| Phase | File | Size | Bones×Frames | sha256 |
|-------|------|------|--------------|--------|
| start (a) | `f1b02yd小跳a.ani` | 53873 | 182×23 | `08556529ac55157e25579a1cfc8e65e150f6e70620b5ff08497cd96bb0f71f95` |
| mid (b) **primary** | `f1b02yd小跳b.ani` | 48172 | 182×21 | `8e4fa260b3d37c1688a15d638acafcb7772c03fb25991726119909a1ad54a782` |
| land (c) | `f1b02yd小跳c.ani` | 58552 | 182×25 | `28900c27948665a224330ba7fa747131ddd48686ef226873275df7ff5a7d8b5a` |

All under `samples/player/moves/from_mapviewer/f1/动作/`. Also staged: `f1b02yd小跳a.tani` / `小跳c.tani` / `二段跳a.tani`.

Primary one-clip smoke for 跳跃: **小跳b**.

## Natasha list order

Surface **行走** then **跳跃** first. Drop 风来吴山·蓄力 from first_bar for Andy.

## Thor

PASS only if 花萝 looks like a person + 走路/跳跃 humanoid. Stick PNGs = FAIL.

## Pull

`PakV4SfxExtract` → `C:\Users\Zhibin Ren\jx3-staging\f1-jump-pull\`
