# UI manifest — how every UI file is enumerated

**Confidence:** VERIFIED against the extracted client files and PakV4.

## Source

`proof/netcode/ui_lua_probe/out/ui/module_info.xml` (already in the repo) is the
client's UI module manifest. It is referenced by the path table as
`ModuleInfo = \ui\module_info.xml` (`proof/movement/extracted/ui_filepath.txt`).

Parsed census (see `ui_manifest.tsv` in this directory):

| class | entries |
|---|---|
| `<file>` total | 1,783 |
| `ui/**` files | 1,679 |
| — `ui/Config/**` | 1,402 |
| — `ui/Script/**` | 231 |
| — `ui/String/**` | 22 |
| — other `ui/**` | 24 |
| non-UI (`scripts/**`, etc.) | 104 |

Every `<module>` carries `name`, `load`, `layer` and usually `type`:

- `type="frame"` — a window module: its Lua (usually
  `ui/Config/Default/<Name>.lua`) registers open/close functions and events.
- `type="lib"` — a shared library.
- no type — startup/script modules (`base`, `gamelogic`, `DefaultScriptN`, ...).
- `layer` 1 vs 2 — load layer order.

## Local presence

At the time of writing, 167 of the 1,679 `ui/**` entries exist locally
(134 `Config` files from `proof/minimap/ui/` plus 33 extracted for this
research under `proof/ui/evidence/`). The remaining ~1,500 are in PakV4 only
and are extractable by exact path (see `resource-sources.md`).

`ui_manifest.tsv` columns: `module`, `type`, `layer`, `load`, `file`,
`present_local`.

## Window → files rule

1. Find the window name as a module in `module_info.xml`
   (e.g. `Minimap` → module `DefaultScript17`;
   `NewBattleFieldQueue` → `DefaultScript47`).
2. The module's Lua driver is normally `ui/Config/Default/<Name>.lua`
   (window drivers) or `ui/Script/<name>.lua` (shared).
3. The layout file is the sibling `ui/Config/Default/<Name>.ini`; a window's
   root section may explicitly name its script with `ScriptFile=...`.
4. Some windows live in subfolders (`ui/Config/Default/BattleField/`,
   `.../CoinShop/`, `.../ChatPanel/`, ...). The extracted local subset does not
   contain those subfolders yet, but the manifest paths are exact.
5. MessageBox-style templates use
   `ui/Config/Default/MessageBox/%s.ini` (from `MessageBox.lua`).

## Not in the manifest

- INI layouts are referenced from Lua (`ScriptFile` / loader calls), not
  listed by `module_info.xml`, so the manifest is a superset source:
  `ui/Config/**` entries include both `.lua` and `.ini`.
- `ui/framellist.ini` / `ui/framesetting.txt` are declared in the path table
  but are absent from this build (see `system-tables.md`).

## Evidence files

- `ui_manifest.tsv` — full parsed manifest with presence flags.
- `module_info.xml` — the manifest itself (tracked at
  `proof/netcode/ui_lua_probe/out/ui/module_info.xml`).
- `../re/jx3ui_uiconfig.txt` — disassembly showing `JX3UIX64.dll` opens
  `ui/scheme/elem/uiconfig.ini`.
