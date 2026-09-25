"""Convert the GBK game text assets needed by map-ui-app into UTF-8 copies.

The WPF app reads these prepared copies so it does not need a GB18030 code page
provider. Image assets (TGA/DDS/PNG) are read from the original proof tree.
"""

import pathlib

REPO = pathlib.Path(__file__).resolve().parents[1]
OUT = REPO / "map-ui-app" / "assets" / "text"

MAP_FOLDERS = [
    "龙门寻宝minimap_mb",
    "龙门寻宝_夜晚minimap_mb",
    "海岛绝境minimap_mb",
    "白龙绝境minimap_mb",
    "天原绝境minimap_mb",
    "洱海绝境minimap_mb",
    "林海绝境minimap_mb",
]

FILES = [
    "proof/minimap/ui/Config/Default/MiniMap.ini",
    "proof/minimap/ui/Config/Default/MiddleMap.ini",
    "proof/minimap/ui/Config/Default/BattleField/BattleFieldMap.ini",
    "proof/minimap/ui/Scheme/Case/string.txt",
    "proof/minimap/ui/Scheme/Case/String_Comman.txt",
    "proof/minimap/ui/Scheme/Case/string_ArenaCorpsPanel.txt",
]

for folder in MAP_FOLDERS:
    base = f"proof/minimap/extracted/data/source/maps/{folder}"
    FILES.append(f"{base}/config.ini")
    FILES.append(f"{base}/area.tab")


def main() -> None:
    for rel in FILES:
        src = REPO / rel
        if not src.is_file():
            print("missing", rel)
            continue
        data = src.read_bytes()
        try:
            text = data.decode("utf-8")
        except UnicodeDecodeError:
            text = data.decode("gb18030")
        dst = OUT / rel[len("proof/minimap/"):]
        dst.parent.mkdir(parents=True, exist_ok=True)
        dst.write_text(text, encoding="utf-8")
        print("ok", rel)


if __name__ == "__main__":
    main()
