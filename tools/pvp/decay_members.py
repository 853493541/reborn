#!/usr/bin/env python3
"""Data-driven DecayType membership from Buff.tab + DecayType.tab.

Writes: decay_types_verified.tsv  (DecayType | Name | DecayFrame | ImmunityFrame |
seconds | BuffRowCount | VerifiedExamples | Note) and a membership check list for
the previously hand-picked example IDs.

Usage: python tools/pvp/decay_members.py <Buff.tab> <DecayType.tab> <out.tsv>
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BUFF, DECAY, OUT = (Path(a) for a in sys.argv[1:4])


def load(p: Path):
    text = p.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


bh, brows = load(BUFF)
bc = {h: i for i, h in enumerate(bh)}
dh, drows = load(DECAY)

HAND_PICKED = {
    "0": ["424", "453", "14928", "2755", "5876", "533", "464"],
    "1": ["558", "706", "2289", "3466", "679"],
    "2": ["554", "675", "685", "686", "749", "13052"],
    "3": ["556", "852", "2832", "2833"],
    "4": ["682"],
    "5": ["445", "534", "557", "726", "50379", "4053"],
    "6": ["2522", "2523"],
    "7": ["3224", "4871", "9756"],
    "8": ["749", "1931", "1936", "5694", "51858", "2799"],
    "9": ["27038", "20346"],
}


def c(r, name):
    i = bc[name]
    return r[i] if i < len(r) else ""


by_id: dict[str, list[list[str]]] = {}
for r in brows:
    by_id.setdefault(r[0], []).append(r)

lines = [
    "# DecayType membership VERIFIED from Buff.tab (data-driven; replaces hand-picked lists",
    "# in proof/pvp/decay_and_controls.tsv / decay_types.tsv, which contain IDs that do not",
    "# actually carry the listed DecayType).",
    "# Columns: DecayType | Name | DecayFrame | ImmunityFrame | Seconds | BuffRows | VerifiedExamples(<=12)",
]
for dr in drows:
    dt, dec, imm, name = dr[0], dr[1], dr[2], dr[3]
    mem = [r for r in brows if c(r, "DecayType") == dt]
    ex, seen = [], set()
    for r in mem:
        nm = f"{r[0]}:{c(r,'Name')}"
        if nm in seen:
            continue
        seen.add(nm)
        ex.append(nm)
        if len(ex) >= 12:
            break
    sec = f"{int(dec)/16:.1f}" if dec.lstrip("-").isdigit() and dec not in ("", "-") else "-"
    lines.append("\t".join([dt, name, dec, imm, sec, str(len(mem)), " ; ".join(ex)]))

lines.append("")
lines.append("# Hand-picked example audit (from make_decay_tsv.py EXAMPLES): ID -> actual DecayTypes")
for dt, ids in HAND_PICKED.items():
    for bid in ids:
        acts = sorted({c(r, "DecayType") or "(empty)" for r in by_id.get(bid, [])})
        mark = "OK" if dt in acts else "MISMATCH"
        nm = by_id.get(bid, [["?", "?"]])[0][1]
        lines.append(f"{dt}\t{bid}:{nm}\tactual={'/'.join(acts)}\t{mark}")

OUT.write_text("\n".join(lines), encoding="utf-8")
print(f"written {OUT} lines={len(lines)}")
