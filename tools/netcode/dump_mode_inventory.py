#!/usr/bin/env python3
"""Dump the 沙漠风暴/绝境 loot template inventory from DoodadTemplate.tab."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "proof/netcode/mode_juejing/pak_out6/DoodadTemplate.tab"
OUT = ROOT / "proof/netcode/mode_juejing/mode_doodad_inventory.txt"


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    text = SRC.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    hdr = rows[0]
    idx = {h: i for i, h in enumerate(hdr)}

    def cell(row: list[str], col: str) -> str:
        i = idx.get(col)
        return row[i] if i is not None and i < len(row) else ""

    lines: list[str] = []
    for r in rows[1:]:
        if "沙漠风暴" not in cell(r, "MapName"):
            continue
        drops = "; ".join(
            f"{cell(r, 'Drop' + str(i))}x{cell(r, 'Count' + str(i))}"
            for i in range(1, 11) if cell(r, f"Drop{i}")
        )
        lines.append(
            f"[{cell(r, 'MapName')}] ID={cell(r, 'ID')} {cell(r, 'Name')}"
            f" | Kind={cell(r, 'Kind')} Class={cell(r, 'ClassID')}"
            f" Prepare={cell(r, 'OpenPrepareFrame')} Over={cell(r, 'OverLootFrame')}"
            f" Remove={cell(r, 'RemoveDelay')} Revive={cell(r, 'ReviveDelay')}"
            f" Bar={cell(r, 'BarText')}"
            f" | drops: {drops or '-'}"
            f" | script: {cell(r, 'Script') or '-'}"
        )
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(lines)} rows -> {OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
