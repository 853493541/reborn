"""Type-2 (launcher) block inventory: names, mesh paths, textures, modules."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from _pss_probe import parse_header

PATH_RE = re.compile(rb"(?:data|Data)[\\/][\x20-\x7e\x81-\xfe]{4,200}?\.(?:Mesh|mesh|ani|tga|dds|jsondef|jsoninspack)", re.IGNORECASE)


def strings_in(blk: bytes) -> list[tuple[int, str]]:
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
            if len(s) >= 3:
                out.append((i, s))
            i = end
        else:
            i += 1
    return out


def path_hits(blk: bytes) -> list[tuple[int, str]]:
    out = []
    for m in PATH_RE.finditer(blk):
        try:
            s = m.group(0).decode("gb18030")
        except UnicodeDecodeError:
            try:
                s = m.group(0).decode("latin1")
            except UnicodeDecodeError:
                continue
        out.append((m.start(), s))
    return out


def main() -> None:
    report: dict[str, list[dict]] = {}
    for path in (Path(p) for p in sys.argv[1:]):
        data = path.read_bytes()
        hdr = parse_header(data)
        rows = []
        for i, e in enumerate(hdr["toc"]):
            if e["type"] != 2:
                continue
            blk = data[e["offset"] : e["offset"] + e["size"]]
            streets = strings_in(blk)
            name = streets[0][1] if streets else ""
            paths = path_hits(blk)
            cjk = [
                s
                for o, s in streets
                if any("\u4e00" <= ch <= "\u9fff" for ch in s) and len(s) <= 24
            ]
            rows.append(
                {
                    "index": i,
                    "size": e["size"],
                    "name": name,
                    "paths": [{"off": o, "value": s} for o, s in paths],
                    "cjk": cjk[:60],
                }
            )
        report[path.name] = rows
    out = Path(sys.argv[0]).with_suffix(".json")
    out.write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"wrote {out} ({out.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
