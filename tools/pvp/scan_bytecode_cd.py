#!/usr/bin/env python3
"""Extract SetPublicCoolDown/SetNormalCoolDown/SetCheckCoolDown calls from bytecode scripts."""
from __future__ import annotations

import importlib.util
import os
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
    r"\jx3-web-map-viewer\cache-extraction\pakv4-probe"
    r"\ability-matcher\extracted\scripts\skill"
)
spec = importlib.util.spec_from_file_location(
    "d", str(Path(__file__).with_name("lua51_disasm.py"))
)
d = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    pub = Counter()
    pub_ex: dict[str, list] = defaultdict(list)
    norm = Counter()
    chk = Counter()
    n = 0
    for dirpath, _dirs, files in os.walk(ROOT):
        for fn in files:
            if not fn.lower().endswith(".lua"):
                continue
            p = Path(dirpath) / fn
            b = p.read_bytes()
            if b[:4] != b"\x1bLua":
                continue
            r = d.Reader(b)
            r.read(12)
            out: list = []
            try:
                d.parse_function(r, 0, out, 200)
            except Exception:
                continue
            rel = str(p.relative_to(ROOT))
            calls: dict[str, list] = {k: [] for k in
                                     ["SetPublicCoolDown", "SetNormalCoolDown", "SetCheckCoolDown"]}
            dump: list = []
            try:
                for f, path in d.walk(out[0]):
                    d.analyze(f, path, [], calls, dump)
            except Exception:
                pass
            for k, rows in calls.items():
                for _f, _pc, args, _callee in rows:
                    key = tuple(a for a in args if a is not None)
                    if k == "SetPublicCoolDown":
                        pub[key] += 1
                        if len(pub_ex[key]) < 5:
                            pub_ex[key].append(rel)
                    elif k == "SetNormalCoolDown":
                        norm[key] += 1
                    else:
                        chk[key] += 1
            n += 1
    print(f"bytecode scripts parsed: {n}")
    print("\n== SetPublicCoolDown value counts")
    for k, v in pub.most_common(30):
        print(f"  {v:5d}  {k}  e.g. {pub_ex[k]}")
    print("\n== SetNormalCoolDown (index,cdID) top")
    for k, v in norm.most_common(20):
        print(f"  {v:5d}  {k}")
    print("\n== SetCheckCoolDown (index,cdID) top")
    for k, v in chk.most_common(20):
        print(f"  {v:5d}  {k}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
