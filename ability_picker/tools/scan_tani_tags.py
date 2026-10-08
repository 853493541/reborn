"""Scan extracted .tani files for their embedded effect records.

The GATA .tani carries the base animation path plus its authored effect
records: `.pss` effect paths and `New SFX Tag_*` entries. The ability dataset
stages some of these PSS files as dummy steps; when the tani already carries
the same PSS, playing the tani makes the engine spawn it a second time (the
tag instance) next to the staged dummy -> the effect visibly restarts.

This tool records, per dataset ability, the matched tani's base `.ani` path and
its embedded `.pss` paths into `ability_picker/data/tani_pss_tags.json`; the
builder (`build_candidates.py`) uses it to fall back to the base `.ani` for
abilities whose staged PSS is already inside the tani.

Input : a directory of extracted .tani files (runtime cache, e.g.
        `MovieEditor\\bin64\\ability_picker\\sound\\_trace_all`), indexed by
        basename, and the tracked dataset `ability_picker/data/ability_candidates.json`.
Output: `ability_picker/data/tani_pss_tags.json` (tracked manifest).

Usage:
    python ability_picker/tools/scan_tani_tags.py <tani_dir> [dataset.json] [out.json]
"""
import io
import json
import os
import re
import sys


def scan_tani(path):
    data = open(path, "rb").read()
    if data[:4] != b"GATA":
        return None
    end = 8
    while end < len(data) and data[end] != 0:
        end += 1
    base_ani = data[8:end].decode("gb18030", errors="replace")
    txt = data.decode("gb18030", errors="replace")
    pss = []
    for m in re.finditer(r"[^\x00-\x1f]{3,160}?\.pss", txt, re.IGNORECASE):
        p = m.group(0).replace("/", "\\")
        low = p.lower()
        if "\\pss\\" not in low and "/pss/" not in low:
            continue
        if p not in pss:
            pss.append(p)
    sfx = []
    for m in re.finditer(r"[^\x00-\x1f]{3,160}?\.sfx", txt, re.IGNORECASE):
        p = m.group(0).replace("/", "\\")
        if p not in sfx:
            sfx.append(p)
    return {"baseAni": base_ani, "pss": pss, "sfx": sfx}


def main():
    tani_dir = sys.argv[1] if len(sys.argv) > 1 else \
        r"C:\SeasunGame\MovieEditor\bin64\ability_picker\sound\_trace_all"
    here = os.path.dirname(os.path.abspath(__file__))
    dataset = sys.argv[2] if len(sys.argv) > 2 else \
        os.path.join(here, "..", "data", "ability_candidates.json")
    out_path = sys.argv[3] if len(sys.argv) > 3 else \
        os.path.join(here, "..", "data", "tani_pss_tags.json")

    index = {}
    for root, dirs, files in os.walk(tani_dir):
        for f in files:
            if f.lower().endswith(".tani"):
                index.setdefault(f.lower(), os.path.join(root, f))

    d = json.load(io.open(dataset, encoding="utf-8"))
    out = {}
    missing = []
    for a in d.get("abilities", []):
        name = a.get("name") or ""
        matched = a.get("matched") or ""
        if not name or not matched:
            continue
        base = os.path.basename(matched.replace("/", "\\")).lower()
        p = index.get(base)
        if p is None:
            missing.append(name)
            continue
        info = scan_tani(p)
        if info is None:
            missing.append(name)
            continue
        info["tani"] = matched
        out[name] = info

    json.dump(out, io.open(out_path, "w", encoding="utf-8"),
              ensure_ascii=False, indent=1, sort_keys=True)
    print("wrote %s: %d abilities (%d tanis missing)" %
          (out_path, len(out), len(missing)))
    for m in missing:
        print("  missing:", m)


if __name__ == "__main__":
    main()
