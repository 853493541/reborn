#!/usr/bin/env python3
"""Trace which tani a skill's sound belongs to.

Extracts candidate tanis (Tani.rt f1, filtered) from the client VFS with
PakV4SfxExtract and scans the raw GATA bytes for the skill's sound keywords
(wem source names / event paths). A tani that carries the keyword IS the clip
the game plays with that sound.

Usage:
  python trace_sounds.py --filters "F1s04tc|F1s04sl" --keywords xurulin,baocanshi
  python trace_sounds.py --groups groups.json
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess

BIN64 = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64"
TANI_RT = r"C:\SeasunGame\MovieEditor\ResourcePack\Tani.rt"
OUT = r"C:\SeasunGame\MovieEditor\bin64\ability_picker\sound\_trace"
BATCH = 400


def read_f1_tanis() -> list:
    rows = []
    for ln in open(TANI_RT, encoding="gb18030", errors="replace").read().splitlines():
        c = ln.split("\t")
        if len(c) >= 3 and c[1].lower().endswith(".tani") and "\\player\\f1\\" in c[2].lower():
            rows.append((c[1], c[2]))
    return rows


def gata_ani(data: bytes) -> str:
    if data[:4] != b"GATA":
        return ""
    j = data.find(b"\x00", 8)
    try:
        return data[8:j].decode("gb18030", errors="replace") if j > 0 else ""
    except Exception:
        return ""


def extract_batch(paths: list, tag: str) -> str:
    os.makedirs(OUT, exist_ok=True)
    plist = os.path.join(OUT, "pathlist_" + tag + ".txt")
    with open(plist, "w", encoding="gb18030") as fh:
        for p in paths:
            fh.write(p + "\n")
    outdir = os.path.join(OUT, tag)
    os.makedirs(outdir, exist_ok=True)
    exe = os.path.join(BIN64, "PakV4SfxExtract.exe")
    r = subprocess.run([exe, plist, outdir], cwd=BIN64, capture_output=True, timeout=1200)
    print(f"  batch {tag}: {len(paths)} paths rc={r.returncode}")
    return outdir


def scan(outdir: str, keywords: list) -> list:
    hits = []
    kws = [(k.lower(), k) for k in keywords]
    for root, _dirs, files in os.walk(outdir):
        for f in files:
            if not f.lower().endswith(".tani"):
                continue
            p = os.path.join(root, f)
            try:
                data = open(p, "rb").read()
            except Exception:
                continue
            low = data.lower()
            for kl, ko in kws:
                i = low.find(kl.encode("ascii", "ignore"))
                if i < 0 or not kl:
                    continue
                ctx = data[max(0, i - 80):i + 120].decode("gb18030", errors="replace")
                hits.append({
                    "keyword": ko,
                    "tani": f,
                    "ani": gata_ani(data),
                    "context": ctx.replace("\x00", "").strip(),
                })
                break
    return hits


def main() -> int:
    global OUT
    ap = argparse.ArgumentParser()
    ap.add_argument("--filters", default="", help="regex over tani filename")
    ap.add_argument("--keywords", default="", help="comma separated keywords")
    ap.add_argument("--groups", default="", help="json {ability: [keywords...]}")
    ap.add_argument("--extract-only", action="store_true", help="extract without scanning")
    ap.add_argument("--out", default=OUT)
    args = ap.parse_args()
    OUT = args.out

    groups = {}
    if args.groups:
        groups = json.load(open(args.groups, encoding="utf-8"))
        keywords = sorted({k for v in groups.values() for k in v})
        filters = args.filters
    else:
        filters = args.filters
        keywords = [k.strip() for k in args.keywords.split(",") if k.strip()]

    rx = re.compile(filters) if filters else None
    tanis = read_f1_tanis()
    sel = [(n, p) for n, p in tanis if (rx.search(n) if rx else True)]
    print(f"candidates: {len(sel)} of {len(tanis)} f1 tanis; keywords={len(keywords)}")

    if args.extract_only:
        for i in range(0, len(sel), BATCH):
            chunk = sel[i:i + BATCH]
            extract_batch([c[1] for c in chunk], f"{i:05d}")
        print("extract-only done")
        return 0

    all_hits = []
    for i in range(0, len(sel), BATCH):
        chunk = sel[i:i + BATCH]
        outdir = extract_batch([c[1] for c in chunk], f"{i:05d}")
        all_hits += scan(outdir, keywords)

    print(f"HITS: {len(all_hits)}")
    for h in all_hits:
        print(f"  [{h['keyword']}] {h['tani']}")
        print(f"      ani={h['ani']}")
        if len(all_hits) <= 40:
            print(f"      ctx={h['context'][:150]}")
    json.dump(all_hits, open(os.path.join(OUT, "hits.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
