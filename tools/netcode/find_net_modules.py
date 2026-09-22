#!/usr/bin/env python3
"""Find which JX3 client modules import network APIs (ws2_32 / winhttp / wininet).

Read-only PE recon for the netcode research. Prints module -> imported net
functions so the netcode docs can cite exact import evidence.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import pefile

NET_DLLS = {
    "ws2_32.dll": "winsock",
    "wsock32.dll": "winsock-legacy",
    "winhttp.dll": "winhttp",
    "wininet.dll": "wininet",
    "iphlpapi.dll": "iphlpapi",
    "dnsapi.dll": "dns",
    "crypt32.dll": "crypto",
    "secur32.dll": "secur32",
}


def imports_by_dll(path: Path) -> dict[str, list[str]]:
    result: dict[str, list[str]] = {}
    try:
        pe = pefile.PE(str(path), fast_load=True)
        pe.parse_data_directories(
            directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"]]
        )
    except Exception as exc:  # noqa: BLE001
        return {"<error>": [str(exc)]}
    for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []) or []:
        dll = entry.dll.decode("ascii", "replace").lower()
        result[dll] = [
            (imp.name.decode("ascii", "replace") if imp.name else f"ordinal{imp.ordinal}")
            for imp in entry.imports
        ]
    pe.close()
    return result


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("dir", type=Path)
    ap.add_argument("--out", type=Path, help="write report here (default stdout)")
    ap.add_argument("--all-functions", action="store_true",
                    help="print every imported function, not just net answers")
    args = ap.parse_args(argv)

    binaries = sorted(
        [p for p in args.dir.iterdir() if p.suffix.lower() in (".dll", ".exe")]
    )
    lines: list[str] = []
    for path in binaries:
        imports = imports_by_dll(path)
        net_hits = {d: fns for d, fns in imports.items() if d in NET_DLLS}
        if not net_hits:
            continue
        lines.append(f"## {path.name}")
        for dll, fns in sorted(net_hits.items()):
            lines.append(f"### {dll} ({NET_DLLS[dll]})")
            if args.all_functions:
                for fn in fns:
                    lines.append(f"  {fn}")
            lines.append(f"  ({len(fns)} functions)")
        lines.append("")

    if not lines:
        lines = ["(no module imports a known network DLL)"]
    text = "\n".join(lines) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8", errors="replace")
        print(f"{len(binaries)} binaries scanned -> {args.out}")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
