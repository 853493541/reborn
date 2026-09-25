#!/usr/bin/env python3
"""Summarize proto-id / size / payload-store offsets for Do* builders.

Parses the disasm txt files produced by dump_fn_disasm.py: for each call to
SendPacket (0x1801acda0) or RealSend (0x1801ac7d0), walks the previous ~40
instructions for the protocol id (`mov eax, IMM`), the size (`mov r8d, IMM`),
and the header store (`mov word ptr [..], ax`).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(r"C:\Users\Zhibin Ren\Desktop\reborn-pvp\proof\pvp\netcode")
CALL_RE = re.compile(r"^(0x[0-9a-f]+): call\t(0x1801acda0|0x1801ac7d0)$")
MOV_RE = re.compile(r"^(0x[0-9a-f]+): mov\t(eax|r8d|r9d), (0x[0-9a-f]+)$")
STORE_RE = re.compile(r"^(0x[0-9a-f]+): mov\t(word ptr \[[^\]]+\]), ax$")


def parse_file(path: Path) -> list[str]:
    out = []
    lines = path.read_text(encoding="utf-8").splitlines()
    header = lines[0].split(":", 1)[-1].strip() if lines else path.name
    for i, line in enumerate(lines):
        cm = CALL_RE.match(line)
        if not cm:
            continue
        pid = size = store = None
        for j in range(i - 1, max(0, i - 45), -1):
            m = MOV_RE.match(lines[j])
            if m:
                if m.group(2) == "eax" and pid is None:
                    pid = m.group(3)
                elif m.group(2) == "r8d" and size is None:
                    size = m.group(3)
            if store is None:
                s = STORE_RE.match(lines[j])
                if s:
                    store = s.group(1)
            if pid is not None and size is not None and store is not None:
                break
        out.append(f"{header}: call@{cm.group(1)} proto={pid} size={size} hdr_store@{store}")
    return out


def main() -> int:
    for d in ["disasm", "disasm_deep2"]:
        base = ROOT / d
        if not base.is_dir():
            continue
        print(f"== {d} ==")
        for f in sorted(base.glob("*.txt")):
            for r in parse_file(f):
                print(r)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
