#!/usr/bin/env python3
"""Extract logical paths from client PakV4 by trying candidates one-by-one.

Avoids shell-quoting issues: paths come from a UTF-8 file (one per line).
Uses pss_assets.run_pakv4 (official PakV4SfxExtract) per path so a missing
candidate cannot abort the whole batch.

Usage:
  python tools/netcode/extract_pak_paths.py --list candidates.txt --out-dir proof/netcode/out
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

import pss_assets  # noqa: E402


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--list", type=Path, required=True)
    ap.add_argument("--out-dir", type=Path, required=True)
    ap.add_argument("--work", type=Path, default=None)
    args = ap.parse_args(argv)

    candidates = [
        line.strip() for line in args.list.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.strip().startswith("#")
    ]
    out_dir = args.out_dir.resolve()
    work = (args.work or (out_dir / "_work")).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    hits = 0
    for cand in candidates:
        try:
            found = pss_assets.run_pakv4([cand], work=work)
        except Exception as exc:  # noqa: BLE001
            print(f"ERR  {cand}: {exc}")
            continue
        if not found:
            print(f"MISS {cand}")
            continue
        for key, data in found.items():
            dest = out_dir / Path(key).name
            dest.write_bytes(data)
            print(f"HIT  {cand} -> {dest} ({len(data)} bytes)")
            hits += 1
    print(f"{hits} files extracted from {len(candidates)} candidates")
    return 0 if hits else 1


if __name__ == "__main__":
    raise SystemExit(main())
