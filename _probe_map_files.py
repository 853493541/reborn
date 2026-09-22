"""Extract 龙门寻宝 map descriptor files from the client PakV4 (official tool)."""

from pathlib import Path

from pss_assets import PAKV4_EXE, run_pakv4

MAP = "data\\source\\maps\\龙门寻宝\\龙门寻宝"
ENTRIES = [
    MAP + ".jsonmap",
    MAP + ".SRScene",
    MAP + ".rcidx",
    MAP + "_Setting.ini",
    "data\\source\\maps\\龙门寻宝\\systemCamera.json",
    "data\\source\\maps\\龙门寻宝\\environment.json",
]

out = Path("proof/map_spike/龙门寻宝_extracted")
out.mkdir(parents=True, exist_ok=True)

found = run_pakv4(ENTRIES, work=Path("C:/jx3tmp/map_spike_extract"))
print(f"found {len(found)} files")
for rel, data in found.items():
    dest = out / rel.replace("/", "\\")
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(data)
    print(f"  {rel} ({len(data)} bytes) -> {dest}")
for e in ENTRIES:
    if not any(rel.lower() == e.lower().replace("\\", "/") for rel in found):
        print(f"  MISSING {e}")
