"""Copy the client's shipped UI fonts into the ui-process-app assets tree.

KGUI text is rendered with the loose `<client>\\ui\\Font\\*.ttf` files referenced
by `fontlist.ini` / `fontpathlist.ini` (see ui-process-app/Engine/Fonts.cs and
`ResolveFontFamily` in Engine/UiLayout.cs). The fonts are game assets, so they
are not committed: this script copies them into the git-ignored
`ui-process-app/assets/ui/Font/` where `AssetResolver` finds `ui/Font/<file>`.
Without them the renderer falls back to Microsoft YaHei UI.

It also verifies coverage: every font file named by the shipped UI tables
(`proof/ui/evidence/scheme/fontlist.ini` + `fontpathlist.ini`, tracked) must be
present after the copy; a missing referenced font is an error.

Usage:
  python tools/prepare_ui_fonts.py
  python tools/prepare_ui_fonts.py --client-root <zhcn_hd> --out <dir>
"""

import argparse
import pathlib
import shutil

REPO = pathlib.Path(__file__).resolve().parents[1]
CLIENT_ROOT = pathlib.Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd")
SCHEME = REPO / "proof" / "ui" / "evidence" / "scheme"
FONT_EXTENSIONS = (".ttf", ".otf", ".ttc")
# The renderer also needs the scheme tables (font schemes/colors) next to the app:
# SchemeRoot = ui-process-app/assets/ui/Scheme/Case (see Engine/Paths.cs).
SCHEME_FILES = ("font.ini", "fontlist.ini", "fontpathlist.ini", "color.txt")


def referenced_fonts() -> set:
    """Font file names referenced by the shipped font tables (fontlist/fontpathlist)."""
    names = set()
    for table in ("fontlist.ini", "fontpathlist.ini"):
        path = SCHEME / table
        if not path.is_file():
            print("WARN table missing:", path)
            continue
        text = path.read_bytes().decode("gb18030", errors="replace")
        for line in text.splitlines():
            if line.strip().lower().startswith("file="):
                value = line.split("=", 1)[1].strip().replace("\\", "/")
                names.add(value.rsplit("/", 1)[-1])
    return names


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--client-root", type=pathlib.Path, default=CLIENT_ROOT,
                    help="client install root (contains ui\\Font)")
    ap.add_argument("--out", type=pathlib.Path,
                    default=REPO / "ui-process-app" / "assets" / "ui" / "Font",
                    help="destination Font directory")
    args = ap.parse_args()

    src = args.client_root / "ui" / "Font"
    if not src.is_dir():
        print("MISS source:", src)
        return 1

    args.out.mkdir(parents=True, exist_ok=True)
    copied = []
    for font in sorted(src.iterdir()):
        if not font.is_file() or font.suffix.lower() not in FONT_EXTENSIONS:
            continue
        dest = args.out / font.name
        shutil.copyfile(font, dest)
        copied.append((font.name, font.stat().st_size))
        print("copy %-24s %10d bytes -> %s" % (font.name, font.stat().st_size, dest))

    # scheme tables -> assets/ui/Scheme/Case (the app's SchemeRoot)
    scheme_out = args.out.parent / "Scheme" / "Case"
    scheme_out.mkdir(parents=True, exist_ok=True)
    for name in SCHEME_FILES:
        src = SCHEME / name
        if src.is_file():
            shutil.copyfile(src, scheme_out / name)
            print("copy %-24s %10d bytes -> %s" % (name, src.stat().st_size, scheme_out / name))
        else:
            print("WARN scheme table missing:", src)

    present = {p.name.lower() for p in args.out.iterdir() if p.is_file()}
    referenced = sorted(referenced_fonts())
    missing = [name for name in referenced if name.lower() not in present]

    print("\nreferenced by shipped UI tables (%d):" % len(referenced))
    for name in referenced:
        state = "OK" if name.lower() in present else "MISSING"
        print("  %-28s %s" % (name, state))
    print("shipped font files copied: %d" % len(copied))
    if missing:
        print("ERROR missing referenced fonts:", ", ".join(missing))
        return 1
    print("all referenced fonts present in", args.out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
