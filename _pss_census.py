"""Census of PSS blocks: leading bytes/strings and section markers per type."""
from __future__ import annotations

import sys
from pathlib import Path

from _pss_probe import gbk_strings, parse_header


def lead_string(blk: bytes) -> str:
    ss = gbk_strings(blk[:96])
    return ss[0] if ss else ""


def main() -> None:
    files = [Path(p) for p in sys.argv[1:]]
    for path in files:
        data = path.read_bytes()
        hdr = parse_header(data)
        print("=" * 100)
        print(f"{path.name}: emitters={hdr['count']}")
        by_type: dict[int, int] = {}
        for i, e in enumerate(hdr["toc"]):
            by_type[e["type"]] = by_type.get(e["type"], 0) + 1
            blk = data[e["offset"] : e["offset"] + e["size"]]
            lead = lead_string(blk)
            head = " ".join(f"{b:02x}" for b in blk[:16])
            print(
                f"  [{i:3d}] t={e['type']} size={e['size']:6d} head=[{head}] lead={lead!r}"
            )
        print(f"  type counts: {by_type}")


if __name__ == "__main__":
    main()
