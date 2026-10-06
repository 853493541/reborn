#!/usr/bin/env python3
"""Build ability_picker/data/skill_data.json from the client's own skill tables.

Sources (read-only, extracted from the game client paks):
  ui/Scheme/Case/Skill.txt      skill tooltips: Name/Desc/ShortDesc/SimpleDesc + IconID
  ui/Scheme/Case/Icon.txt       IconID -> ui/Image/Icon/<FileName>.UITex + Frame
  settings/skill/skills.tab     master table (kind/school/cast mode/script)
  settings/skill/SkillRealization.tab  school per skill

Icons: the UITex names its texture (e.g. skill_tiance04.Tga); the tool extracts the
TGA through the official PakV4SfxExtract.exe and converts the frame rect to PNG in
the runtime dir (bin64\\ability_picker\\icons\\<id>.png).

Usage:
  python ability_picker/tools/build_skill_data.py [--ids 65119,65101,...]
      [--out ability_picker/data/skill_data.json]
      [--icons-out C:\\SeasunGame\\MovieEditor\\bin64\\ability_picker\\icons]
      [--scratch <workdir>]
"""
from __future__ import annotations

import argparse
import io
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
PICKER = os.path.dirname(HERE)                      # ability_picker/
DEFAULT_OUT = os.path.join(PICKER, "data", "skill_data.json")
DEFAULT_ICONS = r"C:\SeasunGame\MovieEditor\bin64\ability_picker\icons"
DEFAULT_CANDIDATES = os.path.join(PICKER, "data", "ability_candidates.json")

CLIENT_BIN64 = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64"
EXTRACTOR = os.path.join(CLIENT_BIN64, "PakV4SfxExtract.exe")
PROBE = (
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer"
    r"\cache-extraction\pakv4-probe"
)

UI_FILES = ["ui/Scheme/Case/Skill.txt", "ui/Scheme/Case/Icon.txt"]


def extract(paths: list[str], outdir: str) -> dict:
    """Extract VFS paths through the official extractor; returns name -> file path."""
    os.makedirs(outdir, exist_ok=True)
    pl = os.path.join(outdir, "pathlist.txt")
    with io.open(pl, "w", encoding="gb18030") as fh:
        fh.write("\r\n".join(paths))
    subprocess.run([EXTRACTOR, pl, outdir], cwd=CLIENT_BIN64, capture_output=True)
    found = {}
    for root, _dirs, files in os.walk(outdir):
        for f in files:
            if f.lower() == "pathlist.txt":
                continue
            found[f.lower()] = os.path.join(root, f)
    return found


def read_table(path: str) -> list[list[str]]:
    return io.open(path, encoding="gb18030", errors="replace").read().splitlines()


def ui_table(scratch: str, vfs: str) -> str:
    """Return a local copy of a ui/Scheme/Case file (probe copy or fresh extract)."""
    local = os.path.join(scratch, vfs.replace("/", os.sep))
    if os.path.exists(local):
        return local
    got = extract([vfs], scratch)
    name = os.path.basename(vfs).lower()
    if name not in got:
        raise SystemExit("extract failed: " + vfs)
    return got[name]


def parse_uitex(path: str):
    """UITex: 'UI' + u32 ver + u32 w + u32 h + u32 count + texname[64] + 20-byte frames."""
    b = open(path, "rb").read()
    if b[:2] != b"UI" or len(b) < 92:
        return None
    w, h, count = struct.unpack_from("<III", b, 4)[0], struct.unpack_from("<III", b, 8)[0], struct.unpack_from("<III", b, 12)[0]
    tex = b[24:88].split(b"\x00")[0].decode("ascii", "replace")
    frames = []
    for i in range(count):
        o = 92 + i * 20
        if o + 20 > len(b):
            break
        x, y, fw, fh, flag = struct.unpack_from("<iiiii", b, o)
        frames.append((x, y, fw, fh, flag))
    return {"tex": tex, "w": w, "h": h, "frames": frames}


def load_candidates(path: str) -> list[dict]:
    d = json.load(io.open(path, encoding="utf-8"))
    return d.get("abilities") or []


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ids", default="", help="comma list of skill ids (default: all dataset ids)")
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--icons-out", default=DEFAULT_ICONS)
    ap.add_argument("--candidates", default=DEFAULT_CANDIDATES)
    ap.add_argument("--scratch", default=os.path.join(tempfile.gettempdir(), "reborn_skill_data"))
    args = ap.parse_args()

    os.makedirs(args.scratch, exist_ok=True)

    # 1. gather ids from the dataset (resolved rows only)
    entries = []
    for a in load_candidates(args.candidates):
        ids = a.get("ids") or ([a["id"]] if a.get("id") else [])
        if not ids or not a.get("matched"):
            continue
        entries.append(a)
    if args.ids:
        want = set(x.strip() for x in args.ids.split(",") if x.strip())
        entries = [a for a in entries if want & set(a.get("ids") or [])]

    # 2. UI tables
    skill_txt = ui_table(args.scratch, UI_FILES[0])
    icon_txt = ui_table(args.scratch, UI_FILES[1])

    # 3. index Skill.txt by (id, level) -> row; Icon.txt by id -> row
    skill_rows = {}
    for ln in read_table(skill_txt)[1:]:
        c = ln.split("\t")
        if len(c) >= 17 and c[0]:
            try:
                skill_rows[(c[0], int(c[1] or 0))] = c
            except ValueError:
                pass
    icon_rows = {}
    for ln in read_table(icon_txt)[1:]:
        c = ln.split("\t")
        if len(c) >= 3 and c[0]:
            icon_rows[c[0]] = c

    # 4. master tables
    skills_tab = {}
    tab_path = os.path.join(PROBE, "ad-desc-probe-out", "settings", "skill", "skills.tab")
    if os.path.exists(tab_path):
        for ln in read_table(tab_path)[1:]:
            c = ln.split("\t")
            if len(c) > 99 and c[1]:
                skills_tab.setdefault(c[1], c)
    real_tab = {}
    real_path = os.path.join(PROBE, "ad-desc-probe-out", "settings", "skill", "SkillRealization.tab")
    if os.path.exists(real_path):
        for ln in read_table(real_path)[1:]:
            c = ln.split("\t")
            if len(c) >= 7 and c[0]:
                real_tab.setdefault(c[0], c)

    def tab(row, i):
        return row[i] if row and i < len(row) else ""

    # 5. resolve per ability: prefer the skill id row with the highest level (or level 1)
    out = {}
    icons_needed = {}
    for a in entries:
        name = a["name"]
        best = None
        for sid in (a.get("ids") or []):
            rows = [r for (i, _lvl), r in skill_rows.items() if i == sid]
            if rows:
                rows.sort(key=lambda r: int(r[1] or 0))
                best = rows[0] if len(rows) == 1 else rows[1] if rows[0][1] == "0" else rows[0]
                break
        rec = {
            "name": name,
            "ids": a.get("ids") or [],
            "matched": a.get("matched") or "",
            "school": "",
            "kind": "",
            "functionType": "",
            "castMode": "",
            "passive": False,
            "channel": False,
            "maxLevel": 0,
            "iconId": "",
            "iconVfs": "",
            "iconPng": "",
            "desc": "",
            "shortDesc": "",
            "simpleDesc": "",
            "script": "",
        }
        if best is not None:
            rec["iconId"] = best[2]
            rec["desc"] = best[12].replace("\\n", "\n")
            rec["shortDesc"] = best[13]
            rec["simpleDesc"] = best[26] if len(best) > 26 else ""
        sid0 = (a.get("ids") or [""])[0]
        row = skills_tab.get(sid0)
        if row:
            rec["school"] = tab(row, 9)
            rec["kind"] = tab(row, 5)
            rec["functionType"] = tab(row, 6)
            rec["castMode"] = tab(row, 10)
            rec["passive"] = tab(row, 19) == "1"
            rec["channel"] = tab(row, 20) == "1"
            rec["maxLevel"] = int(tab(row, 4) or 0)
            rec["script"] = tab(row, 57)
        real = real_tab.get(sid0)
        if real and not rec["school"]:
            rec["school"] = tab(real, 6)
        iid = rec["iconId"]
        if iid and iid in icon_rows:
            fn = icon_rows[iid][1].replace("/", "\\")
            frame = int(icon_rows[iid][2] or 0)
            rec["iconVfs"] = "ui/Image/Icon/" + fn
            rec["iconFrame"] = frame
            icons_needed.setdefault(iid, rec["iconVfs"])
        out[name] = rec

    # 6. extract icons (UITex + texture) and convert to PNG
    if icons_needed:
        os.makedirs(args.icons_out, exist_ok=True)
        ui_paths = list(icons_needed.values())
        got = extract(ui_paths, os.path.join(args.scratch, "icons"))
        tex_paths = []
        parsed = {}
        for iid, uip in icons_needed.items():
            base = os.path.basename(uip.replace("/", "\\")).lower()
            local = got.get(base)
            if not local:
                continue
            info = parse_uitex(local)
            if not info:
                continue
            parsed[iid] = info
            tex_dir = os.path.dirname(uip)
            # the shipped texture may swap extension vs the UITex name (.tga/.dds)
            tex_paths.append(tex_dir + "/" + info["tex"])
            stem, ext = os.path.splitext(info["tex"])
            alt = stem + (".dds" if ext.lower() != ".dds" else ".tga")
            tex_paths.append(tex_dir + "/" + alt)
        if tex_paths:
            got_tex = extract(tex_paths, os.path.join(args.scratch, "tex"))
        else:
            got_tex = {}
        for iid, info in parsed.items():
            tname = os.path.basename(info["tex"]).lower()
            stem, ext = os.path.splitext(tname)
            local = got_tex.get(tname) or got_tex.get(stem + (".dds" if ext != ".dds" else ".tga"))
            if not local:
                continue
            rec = next((r for r in out.values() if r["iconId"] == iid), None)
            fidx = int(rec.get("iconFrame", 0)) if rec else 0
            if fidx >= len(info["frames"]):
                fidx = 0
            x, y, fw, fh, _flag = info["frames"][fidx] if info["frames"] else (0, 0, info["w"], info["h"])
            try:
                from PIL import Image
            except Exception:
                print("Pillow missing; keeping TGA only")
                break
            im = Image.open(local).convert("RGBA")
            im = im.crop((x, y, x + fw, y + fh))
            for r in out.values():
                if r["iconId"] == iid:
                    png = os.path.join(args.icons_out, str(r["ids"][0]) + ".png")
                    im.save(png)
                    # relative name only (no absolute paths in the committed JSON)
                    r["iconPng"] = str(r["ids"][0]) + ".png"

    # 7. write
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    doc = {
        "source": "ui/Scheme/Case/Skill.txt + Icon.txt + settings/skill/skills.tab",
        "count": len(out),
        "abilities": out,
    }
    with io.open(args.out, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, ensure_ascii=False, indent=1)
    with_icon = sum(1 for r in out.values() if r["iconPng"])
    print("wrote %s: %d abilities, %d icons" % (args.out, len(out), with_icon))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
