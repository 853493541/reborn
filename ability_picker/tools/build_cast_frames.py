#!/usr/bin/env python3
"""Extract per-skill cast frames (prepare / channel) from the shipped skill scripts.

Client truth (docs/pvp/REBORN_PVP_BATTLE_SPEC.md §3.7, JX3_PVP_BATTLE_RESEARCH.md):
  - `skill.nPrepareFrames`          cast/prepare time, in frames (GAME_FPS 16)
  - `skill.nChannelFrame`           channel duration, in frames
  - `skill.nChannelInterval`        channel tick interval (ms)
  - `skill.nMinPrepareFrames`       haste floor
  - skills.tab `IsChannelSkill`      the table's channel flag

The skill scripts are Lua (text or 5.1 bytecode) under the map-viewer cache's
`scripts/skill/<ScriptFile>`.  Text scripts are parsed directly; bytecode is
parsed via tools/netcode/lua51_dump (SETTABLE const `nPrepareFrames`, LOADK value).

Emits ability_picker/data/cast_frames_f1.tsv:
  skillId  isChannel  prepareFrames  channelFrame  channelInterval
(-1 = unknown / not found).  Joins into roster_f1.tsv via build_roster.py.

Reproduce:
  .venv\\Scripts\\python.exe ability_picker\\tools\\build_cast_frames.py
"""
from __future__ import annotations

import glob
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "data")
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(REPO, "tools", "netcode"))
CACHE = (r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
         r"\jx3-web-map-viewer\cache-extraction\pakv4-probe")

try:
    import lua51_dump as _L
except Exception:
    _L = None


def find_skills_tab():
    cands = glob.glob(os.path.join(CACHE, "**", "skills.tab"), recursive=True)
    # prefer the ones under ad-desc / logic-skill (full skill table)
    for pref in ("ad-desc-probe-out", "logic-skill"):
        for c in cands:
            if pref in c:
                return c
    return cands[0] if cands else None


def script_index():
    """basename(lower) -> path, from every scripts/skill root under the cache."""
    idx = {}
    roots = [p for p in glob.glob(os.path.join(CACHE, "**", "scripts"), recursive=True)
             if os.path.isdir(os.path.join(p, "skill"))]
    for root in roots:
        sk = os.path.join(root, "skill")
        for dirpath, _dn, fn in os.walk(sk):
            for f in fn:
                if f.lower().endswith(".lua"):
                    idx.setdefault(f.lower(), os.path.join(dirpath, f))
    return idx


def strip_comment(line):
    i = line.find("--")
    return line[:i] if i >= 0 else line


def num_assign(text, name):
    """`name = <int>` -> int, or -1."""
    m = re.search(r"(?<![A-Za-z])" + name + r"\s*=\s*(\d+)", text)
    return int(m.group(1)) if m else -1


def channel_interval(text):
    """nChannelInterval expression -> the product of its numeric literals, or -1
    when the expression depends on a runtime symbol (e.g. HDJueJingSkillCoe)."""
    m = re.search(r"(?<![A-Za-z])nChannelInterval\s*=\s*([^;\r\n]+)", text)
    if not m:
        return -1
    expr = m.group(1).strip()
    toks = re.findall(r"[A-Za-z_]\w*|\d+\.?\d*|\*|/|\.\.", expr)
    prod, sym = 1.0, False
    for t in toks:
        if re.match(r"^\d", t):
            prod *= float(t)
        elif t in ("*", "/"):
            continue
        else:
            sym = True
            break
    return -1 if sym else int(prod)


def parse_text_bc(data):
    """-> (isBC, text) : text scripts get comments stripped per line."""
    if data[:4] == b"\x1bLua":
        return True, ""
    txt = data.decode("gb18030", "replace")
    txt = "\n".join(strip_comment(l) for l in txt.splitlines())
    return False, txt


def frames(data):
    """-> (prepareFrames, channelFrame, channelInterval) ; -1 = absent."""
    if not data:
        return (-1, -1, -1)
    isbc, txt = parse_text_bc(data)
    if not isbc:
        return (num_assign(txt, "nPrepareFrames"),
                num_assign(txt, "nChannelFrame"),
                channel_interval(txt))
    # bytecode: read SETTABLE assignments via lua51_dump
    if _L is None:
        return (-1, -1, -1)
    out = {"nPrepareFrames": -1, "nChannelFrame": -1}
    try:
        r = _L.Reader(data, data[6] == 1, data[8], data[7], data[10], bool(data[11]))
        r.o = 12
        stack = [_L.read_proto(r)]
        while stack:
            p = stack.pop()
            stack.extend(p["protos"])
            code, consts = p["code"], p["consts"]
            for pc, ins in enumerate(code):
                if _L.OPCODES[ins & 0x3F] != "SETTABLE":
                    continue
                C = (ins >> 14) & 0x1FF
                if not (C & 0x100) or consts[C & 0xFF] not in out:
                    continue
                name = consts[C & 0xFF]
                for k in range(pc + 1, min(pc + 5, len(code))):
                    if _L.OPCODES[code[k] & 0x3F] == "LOADK":
                        bx = (code[k] >> 14) & 0x3FFFF
                        v = consts[bx] if 0 <= bx < len(consts) else None
                        if isinstance(v, (int, float)):
                            out[name] = int(v)
                        break
    except Exception:
        pass
    return (out["nPrepareFrames"], out["nChannelFrame"], -1)


def main():
    tab = find_skills_tab()
    if not tab:
        print("skills.tab not found under cache", file=sys.stderr)
        return 2
    idx = script_index()
    lines = open(tab, "rb").read().decode("gb18030", "replace").splitlines()
    hdr = lines[0].split("\t")
    ci_sid, ci_script, ci_chan = 1, 57, 20
    out = []
    got_script = got_prep = got_chan = 0
    for line in lines[1:]:
        c = line.split("\t")
        if len(c) <= ci_script:
            continue
        sid = c[ci_sid].strip()
        if not sid.isdigit():
            continue
        ischan = c[ci_chan].strip() == "1" if len(c) > ci_chan else False
        rel = c[ci_script].replace("\\", "/").split("/")[-1].lower()
        path = idx.get(rel)
        prep = chanf = chanint = -1
        if path:
            got_script += 1
            prep, chanf, chanint = frames(open(path, "rb").read())
            if prep >= 0:
                got_prep += 1
            if chanf >= 0 or chanint >= 0:
                got_chan += 1
        out.append((sid, ischan, prep, chanf, chanint))

    tsv = os.path.join(DATA, "cast_frames_f1.tsv")
    with open(tsv, "w", encoding="utf-8", newline="\n") as f:
        f.write("skillId\tisChannel\tprepareFrames\tchannelFrame\tchannelInterval\n")
        for row in out:
            f.write("%s\t%d\t%d\t%d\t%d\n" % (row[0], 1 if row[1] else 0, row[2], row[3], row[4]))
    print("cast_frames: %d skills (%d script files, %d nPrepareFrames, %d channel)"
          % (len(out), got_script, got_prep, got_chan))
    for want in ("65076", "65029"):
        for row in out:
            if row[0] == want:
                print("  %s isChannel=%d prepareFrames=%d channelFrame=%d channelInterval=%d"
                      % row)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
