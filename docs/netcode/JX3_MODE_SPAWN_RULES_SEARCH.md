# JX3 BR spawn rules — hunt log and findings

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Question:** where does the game keep the loot spawn rules (anchors/refresh sets), and can we
recover their values locally?

---

## 0. How to read this (status, plain language)

| Thing | Do we have it? |
|---|---|
| **Rule shape** — anchor fields (`AnchorID/X/Y/Z/Dir/RollDir`), refresh fields (`IsRandom/ReviveID/Min/Max`), per-map counters, file names | YES (recovered from binaries) |
| **Runtime wire format** — how a spawned container + its rolled loot are delivered to the client | YES, fully decoded (`JX3_LOOT_PROTOCOL_LAYOUTS.md`), decoder built and tested (`tools/netcode/loot/`) |
| **Byte layout of the server's static map files** (`AnchorPointList.tab`, `DoodadReviveList.tab`, `.land`/`.pland`) | NO — only the field names/schema are known; the loader has not been reversed because **no sample file exists locally** |
| **Actual values** — anchor coordinates, refresh counts/radii, weights, drop-table rows/rates | NO — not in any local store |

So, in one sentence: **we know the shape of the rules and can parse them when they arrive at
runtime, but the values themselves must come from either the server's map bundle (files) or a
live capture (messages).** If a server file is obtained, a parser can be written for it
(disassemble the loader + sample file); if a capture is obtained, the existing decoder already
extracts positions and rolled loot.

---

## 1. What the rules look like (recovered from binaries, HIGH)

The editor/logic module carries the authoring structures:

| Symbol / string | Meaning |
|---|---|
| `KGLogicEditorDocLogical::LoadCustomObjectFromIniFile` | per-map custom objects loaded from an ini/tab |
| `KLogicExporter::ExchangeLogicalData` | writes the logical map bundle |
| `KLogicExporter::ExchangeDoodadReliveSetting` | doodad revive/refresh settings; fields include `IsRandom`, `ReviveID`, `Max`, `Min` |
| `KLogicExporter::ExchangeAnchorPointSetting` | anchor fields: `AnchorID`, `X`, `Y`, `Z`, `Dir`, `RollDir`, `Pitch` |
| `NumDoodadRefreshSet` / `NumDoodadRefreshSetMaxID`, `NumNpcRefreshSet...`, `NumCustomObject`, `NumDoodad`, `NumDoodadGroupMaxID` | per-map logic header counters |

File formats the exporter writes (`JX3LogicEditOperationX64.dll` @ `0x8F5E5C`–`0x8F6008`):

```
%s%s\map\%s\
%s%s\map\%s\DoodadRelive\DoodadReviveList.tab
%s%s\map\%s\AnchorPoint\AnchorPointList.tab
%s%s\map\%s\NpcAIParameter\NpcAIParameterList.tab
%s%s\map\%s\NpcRelive\MapReviveList.tab
%s%s\map\%s\CustomObject\
%sCustomObject.tab
%s\maps\%s\%s.land
%s\maps\%s\%s.pland
```

Area/template side (client tables, already extracted): per-map doodad sets in
`DoodadTemplate.tab` (`沙漠风暴` = 255 rows, `沙漠风暴_寻宝模式` = 168), tier drop tables with
per-map suffixes (`_sea` 18 refs, `_bl` 15, `_lw` 7), and MapList fields
(`MaxLootRange=5`, `ReviveInSitu=0`, `RevieCycle=160`, refresh fields all 0).

## 2. Where the values are NOT (all probed, 0 hits)

| Store | Probe | Result |
|---|---|---|
| client PakV4 | 25 doodad-script names, 60 logical-file variants (`.land`, `.pland`, `CustomObject.tab`, `AnchorPointList.tab`, `DoodadReviveList.tab`, `MapReviveList.tab`) via `extract_pak_paths.py` | 0 |
| launcher cache (`seasun/zscache/dat`, 15 GB) | path-hash PakV5 reader (`jx3-cache-reader.js`) for the same paths | only `.jsonmap` descriptors cached (2/32) |
| second launcher cache (`zsCache-tmp-out/dat`) | same reader, 32 paths | 0 |
| editor resource tree (`FolderTree.xml`, 95 MB) | token search `AnchorPoint`, `DoodadRevive`, `CustomObject`, `pland`, `RefreshSet` | 0 |
| local filesystem | `*AnchorPoint*`, `*CustomObject*`, `*DoodadRevive*`, `*.pland`, `*.land` over `C:\SeasunGame`, Desktop, Documents, AppData | only scene-editor JSON templates (not data) |
| community web | 4 searches (mode spawn points, refresh rules) | official 2018 article only (100-player squads, glider entry, organizer weapon, loot-all, kill drops); no coordinates/rates |

Client PakV4 file-name enumeration was attempted (`FileListSpike.cs`, engine-host +
`KG_PAKFS_CollectAllFileNames`), but the exported sub-pak manager registry is empty in the
editor host (`KG_PakNumManager::GetPakManager` slots 0..9 unused), so no full listing came
from that route. The logical files are written by the **editor exporter**; nothing shows
the runtime client reading them (the client only receives spawns via protocol).

## 3. Conclusion

- The **shape** of JX3's spawn system is known: fixed anchors with `AnchorID/X/Y/Z/Dir/RollDir`,
  per-anchor random flags, doodad refresh sets, per-map revive lists — authored in the map
  logical bundle (`AnchorPointList.tab`, `DoodadReviveList.tab`, `CustomObject.tab`,
  `<map>.land/.pland`).
- The **values** (anchor coordinates, refresh counts/radii, weights, drop-table rows) are
  not present in any local store we can reach: not the client pak, not either launcher
  cache, not the editor tree, not the filesystem.
- Therefore exact spawn rules cannot be recovered from this machine by static analysis
  alone. Remaining routes: server-side map bundle (`maps\<map>\...` from the server
  package), a runtime capture (client decodes them out of `OnSyncNewDoodad`), or
  community documentation (none found at coordinate level).

## 4. Evidence

`proof/netcode/mode_juejing/map_loader_paths.txt`,
`foldertree_search.txt`, `doodad_mapnames.txt`, `juejing_doodad_sets.txt`,
`cache_map_probe.txt`, `pack_extract_log9.txt`, `proof/netcode/disasm/pakfs_*.txt`,
`engine_host_spike/FileListSpike.cs` (+ `MovieEditor\bin64\filelist_out\filelist_host.log`).
