"""Scan MovieEditor binaries for particle/module spec strings (ASCII+UTF16)."""
from __future__ import annotations

import re
import sys
from pathlib import Path

PATTERNS = [
    "Particle Type Mesh",
    "Particle Type",
    "Emitter Shape",
    "Particle Material",
    "ModuleType",
    "LodDistance",
    "MaxParticls",
    "ActiveType",
]


def scan(path: Path, patterns: list[str]) -> dict[str, list[str]]:
    data = path.read_bytes()
    hits: dict[str, list[str]] = {}
    for pat in patterns:
        found: list[str] = []
        for enc in ("latin1", "utf-16-le"):
            needle = pat.encode(enc)
            start = 0
            while True:
                i = data.find(needle, start)
                if i < 0:
                    break
                ctx = data[max(0, i - 40) : i + len(needle) + 60]
                text = "".join(chr(b) if 32 <= b < 127 else "." for b in ctx)
                found.append(text)
                start = i + 1
                if len(found) >= 4:
                    break
        if found:
            hits[pat] = found
    return hits


def main() -> None:
    targets = [Path(p) for p in sys.argv[1:]]
    for t in targets:
        if not t.is_file():
            continue
        print("=" * 100)
        print(f"FILE {t} ({t.stat().st_size} bytes)")
        for pat, found in scan(t, PATTERNS).items():
            print(f"  [{pat}]")
            for f in found:
                print(f"    {f}")


if __name__ == "__main__":
    main()
