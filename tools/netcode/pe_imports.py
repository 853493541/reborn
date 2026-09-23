#!/usr/bin/env python3
"""Print the full import (and delay-import) table of a PE binary.

Reusable read-only recon helper: net evidence must cite actual imports, not
string noise.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import pefile

DIRS = (
    pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"],
    pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT"],
)


def dump(path: Path, show_functions: bool) -> str:
    lines = [f"# {path.name} ({path.stat().st_size:,} bytes)"]
    pe = pefile.PE(str(path), fast_load=True)
    pe.parse_data_directories(directories=list(DIRS))

    for label, attr, dir_attr in (
        ("imports", "DIRECTORY_ENTRY_IMPORT", "DIRECTORY_ENTRY_IMPORT"),
        ("delay-imports", "DIRECTORY_ENTRY_DELAY_IMPORT", "DIRECTORY_ENTRY_DELAY_IMPORT"),
    ):
        entries = getattr(pe, attr, None) or []
        lines.append(f"## {label} ({len(entries)} DLLs)")
        for entry in entries:
            dll = entry.dll.decode("ascii", "replace")
            lines.append(f"### {dll} ({len(entry.imports)})")
            if show_functions:
                for imp in entry.imports:
                    name = imp.name.decode("ascii", "replace") if imp.name else f"ordinal{imp.ordinal}"
                    lines.append(f"  {name}")
        lines.append("")
    pe.close()
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("binary", type=Path)
    ap.add_argument("-o", "--out", type=Path)
    ap.add_argument("--functions", action="store_true")
    args = ap.parse_args(argv)

    if not args.binary.is_file():
        print(f"not a file: {args.binary}", file=sys.stderr)
        return 2

    text = dump(args.binary, args.functions)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8", errors="replace")
        print(f"-> {args.out}")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
