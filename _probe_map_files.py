"""Extract MovieEditor map descriptor files from the client PakV4 (official tool).

Usage: python _probe_map_files.py [map_name ...]
Maps looked up in ResourcePack/MapList.tab (GBK). Extracts the descriptor
bundle for each map into proof/map_spike/<name>_extracted/ and prints the
system camera positions (world coords) for camera tours.
"""

import json
import sys
from pathlib import Path

import maplist
from pss_assets import run_pakv4

OUT_ROOT = Path(__file__).resolve().parent / "proof" / "map_spike"

DESCRIPTORS = [
    "{dir}/{stem}.jsonmap",
    "{dir}/{stem}.SRScene",
    "{dir}/{stem}.rcidx",
    "{dir}/{stem}_Setting.ini",
    "{dir}/systemCamera.json",
    "{dir}/environment.json",
]


def entries_for(row: dict) -> list[str]:
    p = row["path"].replace("/", "\\")
    folder, file = p.rsplit("\\", 1)
    stem = file[: -len(".jsonmap")] if file.lower().endswith(".jsonmap") else file
    return [e.format(dir=folder, stem=stem) for e in DESCRIPTORS]


def camera_positions(data: bytes) -> list[tuple[float, float, float]]:
    """World translations of systemCamera objects (4x4 matrices, last row)."""
    out = []
    try:
        doc = json.loads(data.decode("utf-8-sig"))
        for obj in doc.get("worldObjects", {}).values():
            m = obj.get("comBasic", {}).get("actorLocalMatrix") or []
            if len(m) >= 16:
                out.append((m[12], m[13], m[14]))
    except (ValueError, AttributeError):
        pass
    return out


def main() -> int:
    names = sys.argv[1:] or [
        "龙门寻宝_夜晚",
        "海岛绝境",
        "白龙绝境",
        "天原绝境",
    ]
    rows = maplist.parse()
    by_name: dict[str, list[dict]] = {}
    for r in rows:
        by_name.setdefault(r["name"], []).append(r)

    total = 0
    for name in names:
        hits = by_name.get(name)
        if not hits:
            print(f"NOT FOUND: {name}")
            continue
        for row in hits:
            entries = entries_for(row)
            print(f"{name} (id {row['id']}): {row['path']}")
            found = run_pakv4(entries, work=Path("C:/jx3tmp/map_spike_extract"))
            outdir = OUT_ROOT / f"{name}_id{row['id']}_extracted"
            outdir.mkdir(parents=True, exist_ok=True)
            for rel, data in found.items():
                dest = outdir / rel.replace("/", "\\")
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_bytes(data)
                print(f"  + {rel} ({len(data)} B)")
            for e in entries:
                if not any(rel.lower() == e.lower().replace("\\", "/") for rel in found):
                    print(f"  MISSING {e}")
            # camera world positions for tours
            for key, data in found.items():
                if key.lower().endswith("systemcamera.json"):
                    cams = camera_positions(data)
                    for i, c in enumerate(cams):
                        print(f"  camera[{i}] worldpos = ({c[0]:.0f}, {c[1]:.0f}, {c[2]:.0f})")
            total += len(found)
    print(f"total extracted: {total}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
