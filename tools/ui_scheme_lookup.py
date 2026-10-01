"""Resolve the KGUI font/color codes used by JX3 layout INIs.

A layout never names a font: it writes `FontScheme=<id>` (e.g. 43) and optionally
`FontColor=<name>`. This tool resolves those codes through the shipped tables
(tracked copies under proof/ui/evidence/scheme/):

  \\UI\\Scheme\\Elem\\font.ini        (SchemeElemFont)  421 schemes: FontID, Color,
                                      Size, BorderColor/Size, ProjectionColor/Size
  \\UI\\Scheme\\Elem\\fontlist.ini    (FontList)        36 font slots: File, base Size,
                                      Vertical, Dpi, Chat, ...
  \\UI\\Scheme\\Elem\\fontpathlist.ini(FontPathList)    family name -> file
  \\UI\\Scheme\\Elem\\color.txt      (SchemeElemColor) 106 named r/g/b colors

Usage:
  python tools/ui_scheme_lookup.py 18 212 43          # resolve scheme ids
  python tools/ui_scheme_lookup.py --color yellow2    # resolve color name(s)
  python tools/ui_scheme_lookup.py --list             # all 421 schemes, one line each
  python tools/ui_scheme_lookup.py 18 --scan <ini-dir># usage count in a layout tree
  python tools/ui_scheme_lookup.py --census --scan <ini-dir>   # full usage census

Exit code is nonzero when any requested code is not defined in the tables.
"""

import argparse
import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = pathlib.Path(__file__).resolve().parents[1]
SCHEME = REPO / "proof" / "ui" / "evidence" / "scheme"


def read_gbk(path: pathlib.Path) -> str:
    return path.read_bytes().decode("gb18030", errors="replace").replace("\r", "")


def parse_sections(text: str):
    for m in re.finditer(r"\[(\d+)\]\s*\n((?:[^\[]*))", text):
        fields = dict(re.findall(r"^([A-Za-z]+)=(.*)$", m.group(2), re.M))
        yield int(m.group(1)), fields


def load_tables():
    schemes = dict(parse_sections(read_gbk(SCHEME / "font.ini")))
    fontlist = dict(parse_sections(read_gbk(SCHEME / "fontlist.ini")))
    colors = {}
    for row in read_gbk(SCHEME / "color.txt").splitlines()[1:]:
        parts = row.strip().split("\t")
        if len(parts) >= 4:
            try:
                colors[parts[0]] = tuple(int(x) for x in parts[1:4])
            except ValueError:
                continue
    return schemes, fontlist, colors


def color_of(colors: dict, name: str):
    if not name:
        return None
    for key, value in colors.items():
        if key.lower() == name.lower():
            return key, value
    return None


def describe(sid, fields, fontlist, colors):
    font_id = int(fields.get("FontID", 0))
    base = fontlist.get(font_id, {})
    size_override = int(fields.get("Size", 0))
    size = size_override if size_override > 0 else int(base.get("Size", 0))
    origin = "scheme" if size_override > 0 else "fontlist base"
    fill = color_of(colors, fields.get("Color", ""))
    fill_s = "#%02X%02X%02X" % fill[1] if fill else "?"
    border = color_of(colors, fields.get("BorderColor", ""))
    border_s = "#%02X%02X%02X" % border[1] if border else "?"
    proj = color_of(colors, fields.get("ProjectionColor", ""))
    proj_s = "#%02X%02X%02X" % proj[1] if proj else "?"
    file = (base.get("File") or "?").replace("\\", "/").rsplit("/", 1)[-1]
    print("#%-3d %s" % (sid, fields.get("Name", "?")))
    print("     FontID=%d -> %s (base size %s, vertical=%s, dpi=%s%s)" % (
        font_id, file, base.get("Size", "?"), base.get("Vertical", "?"), base.get("Dpi", "?"),
        ", chat-only" if base.get("Chat") == "1" else ""))
    print("     Size=%d (%s)  Color=%s %s" % (size, origin, fields.get("Color"), fill_s))
    print("     Border: size=%s color=%s %s | Projection: size=%s color=%s %s" % (
        fields.get("BorderSize", 0), fields.get("BorderColor"), border_s,
        fields.get("ProjectionSize", 0), fields.get("ProjectionColor"), proj_s))


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("codes", nargs="*", type=int, help="FontScheme id(s)")
    ap.add_argument("--color", action="append", default=[], help="resolve a color name")
    ap.add_argument("--list", action="store_true", help="list all schemes")
    ap.add_argument("--census", action="store_true", help="usage census over --scan")
    ap.add_argument("--scan", type=pathlib.Path, help="INI tree for usage counts")
    args = ap.parse_args()

    schemes, fontlist, colors = load_tables()
    if not schemes:
        print("font.ini not found under", SCHEME)
        return 1

    if args.census:
        if not args.scan:
            print("--census needs --scan <ini-dir>")
            return 1
        import collections
        scheme_use = collections.Counter()
        color_use = collections.Counter()
        files = 0
        for ini in args.scan.rglob("*.ini"):
            files += 1
            text = read_gbk(ini)
            for value in re.findall(r"^FontScheme=(\d+)", text, re.M):
                scheme_use[int(value)] += 1
            for value in re.findall(r"^FontColor=([A-Za-z0-9_]+)", text, re.M):
                color_use[value] += 1
        print("schemes=%d fontlistSlots=%d colors=%d" % (len(schemes), len(fontlist), len(colors)))
        print("INIs scanned=%d | FontScheme refs=%d | distinct schemes=%d" % (
            files, sum(scheme_use.values()), len(scheme_use)))
        print("top schemes:")
        for sid, n in scheme_use.most_common(15):
            print("  #%-3d x%-6d %s" % (sid, n, schemes.get(sid, {}).get("Name", "?")))
        unknown = sorted(sid for sid in scheme_use if sid not in schemes)
        print("FontScheme ids not in font.ini:", unknown or "none")
        unresolved = sorted(name for name in color_use if color_of(colors, name) is None)
        print("FontColor names used=%d | not in color.txt: %s" % (
            len(color_use), unresolved or "none"))
        return 1 if unknown or unresolved else 0

    if args.list:
        for sid in sorted(schemes):
            f = schemes[sid]
            print("#%-3d %-28s FontID=%-2s Size=%-2s %s" % (
                sid, f.get("Name", "?")[:28], f.get("FontID", "?"), f.get("Size", "?"), f.get("Color", "?")))
        return 0

    failed = 0
    for sid in args.codes:
        if sid in schemes:
            describe(sid, schemes[sid], fontlist, colors)
        else:
            print("#%d not defined in font.ini" % sid)
            failed += 1

    for name in args.color:
        hit = color_of(colors, name)
        if hit:
            print("%s = #%02X%02X%02X" % (hit[0], *hit[1]))
        else:
            print("%s not defined in color.txt" % name)
            failed += 1

    if args.scan:
        scheme_hits = {sid: 0 for sid in args.codes}
        color_hits = {name.lower(): 0 for name in args.color}
        files = 0
        for ini in args.scan.rglob("*.ini"):
            files += 1
            text = read_gbk(ini)
            for value in re.findall(r"^FontScheme=(\d+)", text, re.M):
                if int(value) in scheme_hits:
                    scheme_hits[int(value)] += 1
            for value in re.findall(r"^FontColor=([A-Za-z0-9_]+)", text, re.M):
                if value.lower() in color_hits:
                    color_hits[value.lower()] += 1
        print("scanned %d INIs under %s" % (files, args.scan))
        for sid, n in scheme_hits.items():
            print("  FontScheme=%d refs: %d" % (sid, n))
        for name, n in color_hits.items():
            print("  FontColor=%s refs: %d" % (name, n))
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
