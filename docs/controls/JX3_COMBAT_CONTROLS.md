# JX3 combat controls

**Evidence:** `docs/pvp/JX3_PVP_BATTLE_RESEARCH.md`,
`docs/pvp/REBORN_PVP_BATTLE_SPEC.md`,
`proof/movement/extracted/ui_hotkey_default.txt`,
`proof/movement/extracted/ui_hotkey_bindings.ini`.

---

## 1. Targeting (client-side only)

There is **no target-select opcode** — the server never receives "I selected X";
the client tracks the target and sends it with cast intents.

| Input | Command | Action |
|---|---|---|
| click / mouseover | (mouse) | select actor under cursor |
| Tab | `SEARCH_ENEMY` | next target (enemy filter) |
| Ctrl+Tab | `SELECT_PREV_TARGET` | previous target |
| F1 | `SELECT_SELF` | self |
| F2–F5 | `SELECT_TEAMMATE1..4` | party members |
| — | `SELECT_TARGETTARGET` | target of target |
| — | `SELECT_ONLYPLAYER` | toggle only-player filter |
| — | `ATTACKTARGET` | attack current target (unbound by default) |

## 2. Action bars

| Bar | Keys | Slots |
|---|---|---|
| Action bar 1 | `1..0`, `-`, `=` | 16 |
| Action bar 2 | `Alt+1..Alt+0`, `Alt+-`, `Alt+=` | 16 |
| Action bars 3–11 | unbound by default | 16/10/… each |
| Pages | `Shift+1..5`, `PgUp`/`PgDn` | 5 |
| Dynamic bars | contexts `ExtentDynamicAction`, `DynamicBattleRoyale`, `RougeDynamicAction`, `NpcAssistedDynamicAction`, `MobileSkillActionBar` | up to 32 |
| Stance/pose | `ACTIONBAR_BADAO_*`, `ACTIONBAR_QIXIU_*`, `ACTIONBAR_CANGJIAN_*`, `ACTIONBAR_CHANGGE_*` | school-specific |
| Lock | `TOGGLEACTIONBARLOCK` | — |

Skill assignment is drag-and-drop from the skill panel; BR looting adds skills
to the dynamic bar (`szDropSkillRemoteCall`).

## 3. Cast input

- **Keydown cast**: action-bar button or `CastSkillByKeyDown(...)`.
- **Directional cast**: `SKILL_CAST_FORWARD/BACK/LEFT/RIGHT` = `Alt+W/S/A/D`
  (plus `SKILL_YYX_*` variants) — forces the skill direction.
- **Ground-target skills**: aim position sent in the cast intent
  (`OP_TARGET_INTENT` / aim[3] in `OP_CAST_INTENT`).
- **Auto-face**: `LuaTurnToCharacter` snaps facing to the target;
  server `OnSetTurnRange` sets the allowed turn range; skill turning speeds in
  `number.krl` (`SkillTurningToTargetSpeed 0.01`, `SkillTurningTime 1500`).
- **Cast/channel**: `nPrepareFrames` (cast), `nChannelFrame`/`nChannelInterval`
  (channel ticks), `bInstantChannel`; the GCD is a cooldown row
  (`SetPublicCoolDown(16)` = 1.5 s default; per-school variants).
- **Movement/casting interaction**: per-skill move-state masks
  (`SelfMoveStateMask`/`TargetMoveStateMask`); moving can cancel casts unless
  the skill allows it.

## 4. Authority model

| Concern | Owner |
|---|---|
| cast legality (range/LOS/state/silence/immunity) | server |
| cooldowns, charges, overdraft, GCD | server (client renders, corrects on sync) |
| resources (mana/内力/剑气/…), damage/heal | server |
| buffs/CC/DR stacks and timers | server |
| animation, cast bar, cooldown sweep, SFX | client prediction |

C2S intents: `DoCastProfessionSkill 0x49`, `DoCharacterSkill 0x1B`,
`DoStartHoardSkill 0x103`, `DoCastHoardSkill 0x104`;
S2C: `OnSkillPrepare/Cast/Channel/EffectResult/Interrupt` — only the effect
message changes HP. Client validation of range/angle/LOS does not exist.

## 5. Cast validation checklist (server, ordered)

0 alive + not in blocking state → 1 mode bans (`MapBanMask`) → 2 cooldown/GCD →
3 resources → 4 silence/control unless ignored → 5 target type/relation/stealth →
6 range/angle/LOS/path → 7 immunity/shield flags. Rejection returns a reason and
consumes nothing.

## 6. Our client today

One hardcoded key (`1`) plays a tani; no targeting, no action bar, no cast
model, no cooldowns/GCD, no server intents. Target design:
`controls/REBORN_CONTROLS_SPEC.md` §Combat.

## 7. Open items

1. MoveStateMask semantics (whitelist vs suppression).
2. DR ladder/reset rules (client ships windows only: 6/10/20 s).
3. Rating→% equations (`KPlayer::GetAttributeValue` not decompiled).
4. Mouseover-cast support (does the real client cast on mouseover?).
