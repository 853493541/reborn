#!/usr/bin/env python3
"""Parse CoolDownList.tab (GBK TSV) and report column stats.

Read-only. Input: C:\\...\\reborn-netcode\\proof\\netcode\\mode_juejing\\pak_out2\\CoolDownList.tab
Output: proof/pvp/netcode/cool_down_list_analysis.txt
"""
from __future__ import annotations

import sys
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SRC = Path(r"C:\Users\Zhibin Ren\Desktop\reborn-netcode\proof\netcode\mode_juejing\pak_out2\CoolDownList.tab")
OUT = Path(r"C:\Users\Zhibin Ren\Desktop\reborn-pvp\proof\pvp\netcode\cool_down_list_analysis.txt")

HEADER = ["ID", "Duration", "MinDuration", "note", "Usage", "MaxCount",
          "MaxDuration", "CanBackup", "MaxOverDraftCount", "CanAccelerate", "NeedSyncOB"]


def main() -> int:
    raw = SRC.read_bytes()
    text = raw.decode("gbk", errors="replace")
    lines = [ln for ln in text.splitlines() if ln.strip()]
    rows = [ln.split("\t") for ln in lines[1:]]
    cols = HEADER
    n = len(rows)

    out = [f"# CoolDownList.tab — {n} data rows, {len(cols)} columns",
           f"# source: {SRC}",
           "# header: " + " | ".join(cols), ""]

    def num(i, r):
        try:
            return int(r[i])
        except Exception:
            try:
                return float(r[i])
            except Exception:
                return None

    for i, name in enumerate(cols):
        if i in (0, 3):
            continue
        vals = [num(i, r) for r in rows]
        vals = [v for v in vals if v is not None]
        c = Counter(vals)
        if name in ("Duration", "MinDuration", "MaxDuration"):
            out.append(f"{name}: min={min(vals)} max={max(vals)} distinct={len(c)} top={c.most_common(8)}")
        else:
            out.append(f"{name}: distinct={len(c)} counts={dict(sorted(c.items()))}")

    nonzero = lambda k: sum(1 for r in rows if num(k, r))
    out.append("")
    out.append(f"CanBackup != 0        : {nonzero(7)} / {n}")
    out.append(f"MaxOverDraftCount != 0: {nonzero(8)} / {n}")
    out.append(f"CanAccelerate != 0    : {nonzero(9)} / {n}")
    out.append(f"NeedSyncOB != 0       : {nonzero(10)} / {n}")

    usage_names = {0: "none?", 1: "?", 2: "?", 3: "?", 4: "?", 5: "?", 6: "?", 7: "?", 8: "?", 9: "?",
                   10: "?", 11: "?", 12: "?", 13: "?", 14: "?"}
    uc = Counter(num(4, r) for r in rows)
    out.append("")
    out.append(f"Usage histogram: {dict(sorted(uc.items(), key=lambda kv: (kv[0] is None, kv[0])))}")

    out.append("")
    out.append("## rows with MaxOverDraftCount > 0 (charge/overdraft system), first 30")
    shown = 0
    for r in rows:
        if num(8, r) and shown < 30:
            out.append("\t".join(r[:11]))
            shown += 1
    out.append("")
    out.append("## rows with CanAccelerate != 0, first 20")
    shown = 0
    for r in rows:
        if num(9, r) and shown < 20:
            out.append("\t".join(r[:11]))
            shown += 1
    out.append("")
    out.append("## rows with NeedSyncOB != 0, first 30")
    shown = 0
    for r in rows:
        if num(10, r) and shown < 30:
            out.append("\t".join(r[:11]))
            shown += 1
    out.append("")
    out.append("## rows with CanBackup != 0, first 20")
    shown = 0
    for r in rows:
        if num(7, r) and shown < 20:
            out.append("\t".join(r[:11]))
            shown += 1

    OUT.write_text("\n".join(out) + "\n", encoding="utf-8")
    print("\n".join(out[:24]))
    print(f"\nwrote {OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
