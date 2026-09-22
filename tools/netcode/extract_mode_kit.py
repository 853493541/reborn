#!/usr/bin/env python3
"""Extract 绝境战场 mode kit/mechanics rows from client tables.

Reads the extracted settings tables and reports mode-specific skills, cooldowns,
buffs and NPCs, grouped by mechanic keyword.
"""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "proof" / "netcode" / "mode_juejing" / "mode_kit_report.txt"

TABLES = {
    "skills": ROOT / "proof/netcode/mode_juejing/pak_out/skills.tab",
    "buff": ROOT / "proof/netcode/mode_juejing/pak_out4/Buff.tab",
    "npc": ROOT / "proof/netcode/mode_juejing/pak_out/NpcTemplateList.tab",
    "doodad": ROOT / "proof/netcode/mode_juejing/pak_out2/DoodadTemplate.tab",
    "cooldown": ROOT / "proof/netcode/mode_juejing/pak_out4/CoolDownList.tab",
}

KEYWORDS = [
    "绝境", "沙漠风暴", "吃鸡", "楚河汉界", "雷霆", "无相", "连环", "飞爪",
    "滑翔", "火把", "燧石", "战象", "天玄", "绝脉", "武器初始", "夺马",
]


def load(path: Path):
    text = path.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    lines: list[str] = []
    for table, path in TABLES.items():
        if not path.exists():
            lines.append(f"## {table}: MISSING {path}")
            continue
        hdr, rows = load(path)
        idx = {h: i for i, h in enumerate(hdr)}

        def cell(row, col):
            i = idx.get(col)
            return row[i] if i is not None and i < len(row) else ""

        name_col = {"skills": "SkillName", "buff": "Name", "npc": "Name",
                    "doodad": "Name", "cooldown": "note"}.get(table)
        lines.append(f"## {table} ({len(rows)} rows)")
        for kw in KEYWORDS:
            hits = [r for r in rows if kw in cell(r, name_col)]
            if not hits:
                continue
            lines.append(f"-- {kw}: {len(hits)}")
            for r in hits[:40]:
                if table == "skills":
                    cols = ["SkillName", "SkillID", "KindType", "FunctionType", "CastMode", "ScriptFile", "MaxLevel", "MapBanMask"]
                elif table == "buff":
                    cols = ["Name", "ID", "Useage", "FunctionType", "Count", "Interval", "ScriptFile", "MoveStateMask", "MapBanMask", "MapInvalidMask"]
                elif table == "cooldown":
                    cols = ["ID", "Duration", "MinDuration", "Usage", "MaxCount", "CanAccelerate", "NeedSyncOB"]
                else:
                    cols = ["Name", "ID", "TemplateID", "ScriptFile"]
                pairs = [f"{c}={cell(r, c)}" for c in cols if cell(r, c) not in ("", "0")]
                lines.append("   " + " ; ".join(pairs)[:340])
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {OUT} ({len(lines)} lines)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
