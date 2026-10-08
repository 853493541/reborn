# Full PakV4 UI Lua sweep — findings (2026-09-30)

**Provenance.** Candidate list built from the UI manifest
(`proof/netcode/ui_lua_probe/out/ui/module_info.xml`, tracked):
`proof/ui/evidence/battle_hud/pakv4_candidates_fullui_lua.txt` (1,619 paths:
`ui/Config/**/*.lua` + `ui/Script/**/*.lua`). Batch-extracted with the official
PakV4 extractor (via `pss_assets.run_pakv4`, grouped chunks) to the local ignored
`proof/ui/battle_hud/ui_sweep/` — **1,615 HIT / 4 MISS**. Decompiles under
`proof/ui/battle_hud/decompiled_sweep/` (local, unluac).

## Needle results (exact byte search over all 1,615 Lua)

| needle | files | conclusion |
|---|---|---|
| `CASTINGBAR` | **0** | the native `CASTINGBAR_START/END` events have **no Lua consumer anywhere** — native-only listener |
| `CreateProgressBar` / `REPRESENT_CALL` | 1 (self) | only `ui/Script/RepresentCommand.lua` — the producer is native (not in any readable binary; protected base DLL suspected) |
| `ProgressBar` (exact identifier; not `tProgressBar`) | 4 | `globalmgr.lua` (registers `ProgressBar.Anchor/Verion`, calls `ApplyDefaultAnchor`/`OnCustomUIMode`), `table.lua`/`table_defs.lua` (table paths), self — **no `Start`/`Finish` caller**; the generic bar is also native-driven |
| `FullScreenWarning` | 3 | `DynamicCarrierBar.lua`, `CoinShop/CoinShop_View.lua`, self |
| `EndOfBattle` | 3 | `ui/Script/module.lua` (opener), `table_defs.lua` (path), self |
| `ComboWinEffect` | 2 | `ui/Script/Arena_head.lua` (opener), self |

## FullScreenWarning opener (decoded)

`ui/Config/Default/DynamicCarrierBar.lua` registers `PLAYER_STATE_UPDATE` and
`SKILL_EFFECT_TEXT`. Its update computes
`ratio = KeepDecimalPoint(nCurrentLife / nMaxLife, 3)` and then (decompiled
lines 552-566):

```lua
if ratio <= 0.15 then
  if not FullScreenWarning.IsOpened() then
    FullScreenWarning.Open(1000)          -- newly opened, 1000 ms
  else
    FullScreenWarning.UpdateTime(1000)    -- extend while still low
  end
end
if ratio <= 0.2 then Image_Hp:SetFrame(42)
elseif ratio <= 0.5 then Image_Hp:SetFrame(41) end
```

A second variant uses 1500 ms and a third `Open(10000000)` with
`ShowWhenUIHide` (carrier/vehicle modes). `CoinShop_View.lua` also drives the
window for its own fatal-state warning (`Open`/`Close`/`GetFrame`).

⇒ `FullScreenWarning` = **low-health red-edge flash**, opened from the carrier
bar refresh path (not from any generic damage event).
