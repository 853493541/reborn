#!/usr/bin/env python3
"""Extract UI path tables from the client PakV4 and print hotkey path entries.

Read-only research helper: uses the official PakV4SfxExtract.exe through
pss_assets.run_pakv4 to fetch logical UI files, then prints the entries whose
key or value mentions hotkey/keybind.
"""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from pss_assets import run_pakv4  # noqa: E402

GBK = "gb18030"

CANDIDATES = [
    r"ui\filepath.txt",
    r"ui\hotkey\default.txt",
    r"ui\hotkey\bindings.ini",
]

OUT_DIR = ROOT / "proof" / "movement" / "extracted"


def main() -> int:
    found = run_pakv4(CANDIDATES)
    if not found:
        print("no files extracted")
        return 1
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, data in sorted(found.items()):
        dest = OUT_DIR / name.replace("/", "_")
        dest.write_bytes(data)
        print(f"saved {dest} ({len(data)} bytes)")
        text = data.decode(GBK, errors="replace")
        for line in text.splitlines():
            low = line.lower()
            if "hotkey" in low or "keybind" in low or "快捷键" in line:
                print("   ", line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
