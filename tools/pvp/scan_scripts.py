#!/usr/bin/env python3
"""Scan JX3 skill scripts (plaintext Lua + Lua5.1 bytecode) for keyword groups.

Usage: python tools/pvp/scan_scripts.py <scripts_root> <out.txt>
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "netcode"))
import lua51_constants  # noqa: E402


def _string_utf8(self):
    n = self.size()
    if n == 0:
        return None
    raw = self.read(n)[:-1]
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("gb18030", errors="replace")


lua51_constants.Reader.string = _string_utf8

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(sys.argv[1])
OUT = Path(sys.argv[2])

GROUPS: dict[str, list[str]] = {
    "decay_dr": ["递减", "Decay", "decay", "免疫", "Immunity", "免控", "解控", "ControlImmune", "ControlResist", "抵抗"],
    "dispel": ["驱散", "偷取", "转移", "吞噬", "Dispel", "Steal", "Transfer", "Devor", "Purge", "移除"],
    "control": ["眩晕", "击倒", "定身", "锁足", "沉默", "封内", "缴械", "恐惧", "嘲讽", "致盲", "减速", "冰环", "昏睡", "眠蛊", "迷神钉", "麻痹", "击退", "拖拽", "封轻功", "ACT控制"],
    "immunity_buffs": ["生太极", "星楼月影", "天地低昂", "疾如风", "无相诀", "罗汉金身", "折叶笼花", "御风而行", "啸如虎", "风袖低昂", "守如山", "凭虚御风", "惊鸿游龙", "乱洒青荷", "磐石", "笑醉狂", "免控"],
    "pvp_modes": ["名剑", "竞技", "战场", "绝境", "沙暴", "减疗", "化劲", "御劲", "破防", "追命箭", "JJC", "jjc"],
}

files = sorted(ROOT.rglob("*.lua"))
lines: list[str] = [f"# root={ROOT} lua_files={len(files)}"]
n_bc = n_pt = 0

for group, terms in GROUPS.items():
    lines.append(f"\n{'='*80}\n# GROUP {group}\n{'='*80}")
    for f in files:
        try:
            raw = f.read_bytes()
        except OSError:
            continue
        rel = str(f.relative_to(ROOT))
        if raw[:4] == b"\x1bLua":
            n_bc += 1
            res = lua51_constants.dump(f, max_depth=32)
            if res is None:
                continue
            for fn in res["functions"]:
                blob = " | ".join(s for s in fn["strings"] if s)
                src = fn.get("source") or ""
                for t in terms:
                    if t in blob or t in src:
                        lines.append(f"{rel}\t[BC d{fn['depth']}]\t[{t}]\t{(src + ' :: ' + blob)[:400]}")
                        break
        else:
            n_pt += 1
            try:
                text = raw.decode("utf-8")
            except UnicodeDecodeError:
                text = raw.decode("gb18030", errors="replace")
            for i, line in enumerate(text.splitlines(), 1):
                for t in terms:
                    if t in line:
                        s = line.strip()
                        if len(s) > 300:
                            s = s[:300] + "..."
                        lines.append(f"{rel}:{i}\t[{t}]\t{s}")
                        break

lines.append(f"\n# plaintext={n_pt} bytecode={n_bc}")
OUT.write_text("\n".join(lines), encoding="utf-8")
print(f"written {OUT} lines={len(lines)} plaintext={n_pt} bytecode={n_bc}")
