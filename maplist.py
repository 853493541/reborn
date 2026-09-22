"""Parse MovieEditor ResourcePack/MapList.tab (GBK TSV: ID, Name, ResourcePath, UnderEditorTool)."""

import sys
from pathlib import Path

DEFAULT_MAPLIST = Path(r"C:\SeasunGame\MovieEditor\ResourcePack\MapList.tab")


def parse(path: Path = DEFAULT_MAPLIST) -> list[dict]:
    rows = []
    for line in path.read_bytes().decode("gbk", errors="replace").splitlines():
        parts = line.split("\t")
        if len(parts) >= 4 and parts[0].strip().isdigit():
            rows.append(
                {
                    "id": int(parts[0]),
                    "name": parts[1],
                    "path": parts[2],
                    "under_editor_tool": parts[3],
                }
            )
    return rows


def find(rows: list[dict], name: str) -> list[dict]:
    return [r for r in rows if r["name"] == name]


if __name__ == "__main__":
    rows = parse()
    print(f"{len(rows)} maps")
    for r in find(rows, "龙门寻宝"):
        print(r)
