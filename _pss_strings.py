"""Full string/path inventory for all PSS blocks (no external references)."""
from __future__ import annotations

import re
import sys
from pathlib import Path

from _pss_probe import parse_header

TOKEN_RE = re.compile(rb"[\x20-\x7e\x81-\xfe]{4,}?")


def tokens(blk: bytes) -> list[tuple[int, str]]:
    out: list[tuple[int, str]] = []
    i = 0
    n = len(blk)
    while i < n:
        b = blk[i]
        if 0x20 <= b < 0x7F or (0x81 <= b <= 0xFE and i + 1 < n and blk[i + 1] >= 0x40):
            end = i
            while end < n:
                c = blk[end]
                if 0x20 <= c < 0x7F:
                    end += 1
                elif 0x81 <= c <= 0xFE and end + 1 < n and blk[end + 1] >= 0x40:
                    end += 2
                else:
                    break
            raw = blk[i:end]
            try:
                s = raw.decode("gb18030")
            except UnicodeDecodeError:
                i = end
                continue
            if len(s) >= 4 and not s.isdigit():
                out.append((i, s))
            i = end
        else:
            i += 1
    return out


def main() -> None:
    for path in (Path(p) for p in sys.argv[1:]):
        data = path.read_bytes()
        hdr = parse_header(data)
        print("=" * 100)
        print(f"FILE {path.name} emitters={hdr['count']}")
        for i, e in enumerate(hdr["toc"]):
            blk = data[e["offset"] : e["offset"] + e["size"]]
            toks = tokens(blk)
            # keep authored-looking: paths, module names (CJK), ascii identifiers
            shown = [
                (o, s)
                for o, s in toks
                if ("data" in s.lower() or s.endswith((".Mesh", ".mesh", ".ani", ".tga", ".dds", ".jsondef", ".jsoninspack", ".Sfx", ".sfx"))
                    or any("\u4e00" <= ch <= "\u9fff" for ch in s)
                    or (s.isascii() and re.fullmatch(r"[A-Za-z_][A-Za-z0-9_.:]{3,}", s)))
            ]
            print(f"--- [{i:3d}] type={e['type']} size={e['size']} ---")
            for o, s in shown[:40]:
                print(f"   +0x{o:04x} {s!r}")


if __name__ == "__main__":
    main()
