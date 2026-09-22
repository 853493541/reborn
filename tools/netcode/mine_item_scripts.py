#!/usr/bin/env python3
"""Summarize BR mode item scripts (道具_*) from the map-viewer's extracted cache.

Reads each Lua source (GBK), pulls the header comment fields (skill name/effect)
and the first numeric assignments, and writes a readable catalog.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

CACHE = Path(
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer"
    r"\cache-extraction\pakv4-probe\ability-matcher\extracted\scripts\skill\沙漠风暴"
)
OUT = Path(__file__).resolve().parents[2] / "proof/netcode/mode_juejing/mode_item_catalog.txt"

FIELDS = ["技能名称", "技能效果", "武功套路", "技能类型", "脚本说明"]


def summarize(path: Path) -> list[str]:
    text = path.read_bytes().decode("gb18030", errors="replace")
    lines = [f"== {path.name}"]
    for field in FIELDS:
        m = re.search(rf"{field}\s*[:：]\s*(.+)", text)
        if m:
            lines.append(f"   {field}: {m.group(1).strip()}")
    for pat in (
        r"SetSkill\((\d+)\)",
        r"AddBuff\((\d+)",
        r"RemoveBuff\((\d+)",
        r"Damage\([^)]*\)",
        r"cd\s*=\s*(\d+)",
        r"CD\s*=\s*(\d+)",
    ):
        hits = re.findall(pat, text)
        if hits:
            lines.append(f"   {pat}: {sorted(set(hits))[:8]}")
    nums = re.findall(r"(?:伤害|治疗|护盾|持续|冷却|距离|范围)[^\d\n]{0,6}(\d+(?:\.\d+)?)", text)
    if nums:
        lines.append(f"   keyword numbers: {sorted(set(nums))[:10]}")
    return lines


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not CACHE.is_dir():
        print(f"cache dir missing: {CACHE}")
        return 2
    out: list[str] = []
    files = sorted(p for p in CACHE.iterdir() if p.suffix == ".lua")
    for p in files:
        out += summarize(p)
        out.append("")
    OUT.write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"{len(files)} scripts -> {OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
