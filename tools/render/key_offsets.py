#!/usr/bin/env python3
"""Extract config-key -> engine-struct offset candidates from the annotated
disassembly captures (proof/render/disasm/adapter_nEngineGraphicsLevel.txt and
jx3ui_nEngineGraphicsLevel.txt).

The captures come from tools/netcode/xref_string.py: instruction lines carry the
referenced string as a `; 'NAME'` comment. In the adapter load function the key is
loaded with `lea r8, [rip+..] ; 'KEY'` and its destination is either
  - passed as `lea r9, [rsi + 0xNN]` shortly BEFORE the key load (typed getter
    writing straight into the struct), or
  - stored as `mov dword ptr [rsi + 0xNN], eax` shortly AFTER the getter call.

Output: proof/render/key_offsets.tsv (key, offset, method, address), plus a
summary and a self-check against known pairs (nShadowType +0x5c,
nEngineGraphicsLevel +0x260, fFoliageCullDist +0xa40...).

Usage:
  .venv\\Scripts\\python.exe tools\\render\\key_offsets.py [capture ...] [--out path]
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CAPTURES = [
    ROOT / "proof/render/disasm/adapter_nEngineGraphicsLevel.txt",
    ROOT / "proof/render/disasm/jx3ui_nEngineGraphicsLevel.txt",
]

KEY_RE = re.compile(r";\s*'([A-Za-z][A-Za-z0-9_]{2,})'")
LEA_R9_RE = re.compile(r"lea\s+r9,\s*\[rsi \+ (0x[0-9a-fA-F]+)\]")
MOV_EAX_RE = re.compile(r"mov\s+dword ptr \[rsi \+ (0x[0-9a-fA-F]+)\], eax")
ADDR_RE = re.compile(r"^\s*(0x[0-9a-fA-F]+):")

KNOWN = {
    "nShadowType": "0x5c",
    "nEngineGraphicsLevel": "0x260",
    "fSpeedTreeCullDist": "0xa40",
}


def parse(path: Path):
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    rows = []
    for i, line in enumerate(lines):
        m = KEY_RE.search(line)
        if not m:
            continue
        key = m.group(1)
        if not re.match(r"^[bfnu][A-Z_]", key) and not key.startswith("Enable"):
            continue
        addr = ADDR_RE.match(line)
        offset = None
        method = ""
        # r9 destination within the previous 8 instructions
        for j in range(max(0, i - 8), i + 1):
            m9 = LEA_R9_RE.search(lines[j])
            if m9:
                offset = m9.group(1)
                method = "r9-dest"
        # eax store within the next 10 instructions
        if offset is None:
            for j in range(i + 1, min(len(lines), i + 11)):
                me = MOV_EAX_RE.search(lines[j])
                if me:
                    offset = me.group(1)
                    method = "eax-store"
                    break
        if offset:
            rows.append((key, offset, method, addr.group(1) if addr else ""))
    return rows


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("captures", nargs="*", type=Path)
    ap.add_argument("--out", type=Path, default=ROOT / "proof/render/key_offsets.tsv")
    args = ap.parse_args()
    caps = args.captures or DEFAULT_CAPTURES
    seen = {}
    for c in caps:
        if not c.is_file():
            print("MISSING " + str(c), file=sys.stderr)
            continue
        for key, off, method, addr in parse(c):
            seen.setdefault(key, (off, method, addr, c.name))
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("w", encoding="utf-8") as f:
        f.write("key\toffset\tmethod\taddress\tsource\n")
        for key in sorted(seen):
            off, method, addr, src = seen[key]
            f.write("%s\t%s\t%s\t%s\t%s\n" % (key, off, method, addr, src))
    print("keys with offsets: %d -> %s" % (len(seen), args.out))
    bad = 0
    for key, want in KNOWN.items():
        got = seen.get(key)
        ok = got is not None and got[0] == want
        print("%s %s expected %s got %s" % ("OK  " if ok else "FAIL", key, want, got[0] if got else None))
        bad += 0 if ok else 1
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
