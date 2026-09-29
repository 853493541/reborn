# JX3 step forgiveness — what the real game actually does with low obstacles

Date: 2026-09-29
Branch: `agent/collision-improvement`
Question: does the real client let the character walk over objects below some height
(the live-game feel: low walls/steps do not block)? Find the real mechanism, no
invented thresholds.

## 1. Answer in one line

The shipped engine ships a character controller whose **stepOffset default is 0.5 m
(50 game units) and slopeLimit 45°** (recovered from the shipped DLL); PhysX
character-controller semantics climb anything up to that step height and block above
it. **No gameplay-specific "1 m" rule exists in the client files we can inspect**, and
online movement is server-authoritative, so the live game's full forgiveness cannot be
attributed to a client constant we can recover statically.

## 2. Recovered engine values (HIGH confidence)

`PxControllerDesc` constructor in `C:\SeasunGame\MovieEditor\bin64\PhysicsEngineX64.dll`
(RVA `0x18000e910`, dump `proof/collision/disasm/pxcontrollerdesc_ctor.txt`):

| Offset | Field | Written value | Meaning |
|---|---|---|---|
| `+0x20` | upDirection | `(0,1,0)` | — |
| `+0x2c` | `slopeLimit` | `0x3f34fdf4` = **0.70710677** | cos 45°: slopes steeper than 45° are not climbable |
| `+0x38` | `contactOffset` | `0x3dcccccd` = **0.1** | 0.1 m skin |
| `+0x3c` | `stepOffset` | `0x3f000000` = **0.5** | steps up to 0.5 m are climbed automatically |
| `+0x40` | `density` | `0x41200000` = **10** | PhysX default |
| `+0x44` | `scaleCoeff` | `0x3f4ccccd` = **0.8** | PhysX default |
| `+0x48` | `volumeGrowth` | `0x3fc00000` = **1.5** | PhysX default |

These are the engine's own defaults (the base `PxControllerDesc` ctor, called by the
capsule ctor at `0x18000e9e0`). The PhysX scene is **metric**: its gravity is
`(0, −9.81, 0)` (`JX3_COLLISION_SYSTEM.md` §3.9, `JX3_GRAVITY_RESEARCH.md` §3.9), so
`stepOffset 0.5` = 0.5 m = **50 u** (1 u = 1 cm), not 0.5 u.

PhysX 3.3/3.4 character-controller behavior (the shipped
`PhysX3CharacterKinematic_x64.dll`, exports `PxControllerManager::createController`):
during a horizontal move, if the sweep is blocked, the controller tries to move up by
`stepOffset`, forward, and down again; it climbs when a standable surface exists within
the budget, otherwise the move is blocked. `maxJumpHeight` and `invisibleWallHeight`
are 0 in the desc.

## 3. What is NOT recovered (be explicit)

- **Whether the online character uses this controller.** `JX3_COLLISION_SYSTEM.md`
  §11.1: the gameplay body is a kinematic capsule + `KCharacter` foot/trajectory solver
  tracked by SIMWorld; the PhysX controller manager path "may serve the editor/cinematic
  avatar rather than the online character". G-1/G-13 remain.
- **No gameplay step constant** exists in the files we can read: `config.ini`,
  `config/EngineStaticConfig.ini` ([KG3DENGINE]: `bAddPlayerPhysicsActor=0`,
  `bEnableSceneCollision=1`, `bUseLODMeshCollision=1`), `number.krl.txt`, or any client
  string dump (`StepHeight`/`ClimbHeight`/`MaxStep` all absent). The unit-template key
  `fPathingHeight` (KG3DEngineX64 `0x6927B0`, next to `bAutoPathing`/`nPathingType`) has
  no recovered consumer or value (G-21) — it is the only height-like candidate left.
- **Server authority.** `JX3_COLLISION_SYSTEM.md` §24.4: movement legality is
  server-side; navmesh (`NAVX64`/PathEngine) decides walkability on the server and the
  nav data lives outside the client install (G-25). The live game's "I can walk over
  that" may largely be server/nav behavior we cannot extract from the client.
- `bAddPlayerPhysicsActor=0`: the client does **not** put a rigid player body into its
  PhysX scene, so client-side walk blocking is not rigid-body collision either.

## 4. What our host does today

- Terrain: fixed 40 u look-ahead, 70 u rise budget (~60°), `RebornClient.cs` movement
  loop (C-3); terrain itself only stores heights, so baked rocks/cliffs are climbable
  below that rise.
- Objects (`FoliageCollision.Resolve`): step-up onto a structure only when the
  *instance's* `maxY <= py + 70` and a support surface exists under the capsule; a low
  step that is part of a large mesh is therefore treated as part of that mesh's full
  height (structural weakness, independent of the number).
- Client ahead-probes (`SupportHeight` at radius+25/50/75) already step onto low
  up-facing surfaces inside the `[py-20, py+70]` window regardless of instance size.

## 5. Options (pick one; none is invented)

1. **Adopt the recovered engine default**: contact-local step-up with a 50 u
   (0.5 m) budget, slope 45°. Evidence: §2. Risk: it is *stricter* than our current
   70 u, so it will not reproduce an observed ~1 m forgiveness; gameplay applicability
   unproven.
2. **Keep the calibrated 70 u** and fix only the structural part (step onto the
   contact surface / support within the budget instead of instance `maxY`). Unit-free,
   engine-semantics-consistent, no threshold invention.
3. **Wait for a measurement**: if a specific object in the live game is walked over and
   its height measured (e.g., HUD position before/after, or object bounds from the
   sceneinfo), set the host budget to that observed value and log it as the reference.

Recommendation: 2 now, 3 for the number (a live observation is the only source that can
settle the gameplay step value until the server/nav layer is reproducible).

## 6. Reproduce

```powershell
.venv\Scripts\python.exe tools\collision\disasm_range.py `
  "C:\SeasunGame\MovieEditor\bin64\PhysicsEngineX64.dll" e910 120 `
  proof\collision\disasm\pxcontrollerdesc_ctor.txt
```

`PxCapsuleControllerDesc::isValid` (RVA `0x18000e980`) checks `radius>0`, `height>0`,
`stepOffset <= 2*radius + height` — the validation the engine runs on these values.

## 7. Evidence index

- `proof/collision/disasm/pxcontrollerdesc_ctor.txt` — ctor defaults (this doc §2)
- `proof/collision/disasm/pxcontrollerdesc_defaults.txt` — `isValid` checks
- `docs/movement/JX3_COLLISION_SYSTEM.md` §3.9, §11.1, §24.4, G-1/G-13/G-25
- `C:\SeasunGame\Game\JX3\bin\zhcn_hd\config\EngineStaticConfig.ini` `[KG3DENGINE]`

## 8. Applied to the host (2026-09-29)

Per the decision to use the real engine values, `client/RebornClient.cs` now uses the
recovered PhysX CCT default:

- object step budget **50 u (0.5 m)** (`RC_STEP_HEIGHT` overrides), used by the
  structure `SupportHeight` windows, the ground snap and `FoliageCollision.Resolve`;
- `FoliageCollision.Resolve` / `MoveResolved` take the budget as a parameter
  (default 70 keeps old self-test semantics);
- terrain slope rule is **unchanged** (calibrated 40 u look-ahead / 70 u rise, ~60°):
  terrain in JX3 follows the `ProcessDropSpeed` slope model, not the CCT, and the
  calibrated value is the documented host reference.

Self-test: `step_up_50u_budget` PASS, `step_blocks_over_budget` PASS (11/11 total).