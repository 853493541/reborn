#!/usr/bin/env python3
"""Build the v6 ability roster the client's panel + cast chain read.

Roster source = `ability_picker/data/skill_data.json` (the shipped-table ability
list: name, ids, matched tani, castMode, channel, icon).  Joined by skill id to
`ability_picker/data/cast_chain_f1.tsv` (v6 cast chain: anim / effect / bone /
dash).  Output: `ability_picker/data/roster_f1.tsv` (client-readable, tab-sep)
+ `roster_f1.json`.

Columns (roster_f1.tsv):
  id  name  tani  effectSfx  effectBone  dash  castMode  channel  iconPng

Atlas uses `skill_data.json`'s `matched` tani when the cast chain has no anim for
that id, and the cast chain's anim when it does.

Reproduce:
  .venv\\Scripts\\python.exe ability_picker\\tools\\build_roster.py
  Copy-Item ability_picker\\data\\roster_f1.tsv C:\\SeasunGame\\MovieEditor\\bin64\\ability_picker\\
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "data")


def load_cast_chain():
    """id -> {anim, effect, bone, dash} from cast_chain_f1.tsv (skip bad rows)."""
    out = {}
    p = os.path.join(DATA, "cast_chain_f1.json")
    if not os.path.exists(p):
        return out
    d = json.load(open(p, encoding="utf-8"))
    for a in d.get("abilities", []):
        sid = str(a.get("skillId", "")).strip()
        if not sid:
            continue
        out[sid] = {
            "anim": a.get("animTani", ""),
            "prepare": a.get("prepareAnim", ""),
            "effect": a.get("effectSfx", ""),
            "bone": a.get("effectBone", ""),
            "dash": str(a.get("dash", "0")),
        }
    return out


def load_frames():
    """id -> {isChannel, prepareFrames, channelFrame, channelInterval} from
    cast_frames_f1.tsv (build_cast_frames.py)."""
    out = {}
    p = os.path.join(DATA, "cast_frames_f1.tsv")
    if not os.path.exists(p):
        return out
    with open(p, "r", encoding="utf-8") as f:
        for i, line in enumerate(f):
            if i == 0:
                continue
            c = line.rstrip("\r\n").split("\t")
            if len(c) >= 5 and c[0]:
                out[c[0]] = {"isChannel": c[1], "prepare": c[2], "chanFrame": c[3], "chanInterval": c[4]}
    return out


def main():
    skills = json.load(open(os.path.join(DATA, "skill_data.json"), encoding="utf-8"))["abilities"]
    chain = load_cast_chain()
    frames = load_frames()

    rows = []
    for name, v in skills.items():
        ids = v.get("ids") or []
        if not ids:
            continue
        sid = str(ids[0])
        c = chain.get(sid)
        tani = (c["anim"] if c and c["anim"] else v.get("matched", "")) or v.get("matched", "")
        prepareAnim = c["prepare"] if c else ""
        effect = c["effect"] if c else ""
        bone = c["bone"] if c else ""
        dash = c["dash"] if c else "0"
        f = frames.get(sid, {})
        icon = v.get("iconPng") or (sid + ".png")
        rows.append({
            "id": sid, "name": name, "tani": tani, "effect": effect, "bone": bone,
            "dash": dash, "castMode": v.get("castMode", ""),
            "channel": 1 if v.get("channel") else 0, "icon": icon,
            "prepare": f.get("prepare", "-1"), "chanFrame": f.get("chanFrame", "-1"),
            "chanInterval": f.get("chanInterval", "-1"),
            "funcType": v.get("functionType", ""),
            "prepareAnim": prepareAnim,
        })
    rows.sort(key=lambda r: (int(r["id"]) if r["id"].isdigit() else 0, r["id"]))

    cols = ["id", "name", "tani", "effect", "bone", "dash", "castMode", "channel",
            "icon", "prepare", "chanFrame", "chanInterval", "funcType", "prepareAnim"]
    tsv = os.path.join(DATA, "roster_f1.tsv")
    with open(tsv, "w", encoding="utf-8", newline="\n") as f:
        f.write("\t".join(cols) + "\n")
        for r in rows:
            f.write("\t".join(str(r[c]) for c in cols) + "\n")
    with open(os.path.join(DATA, "roster_f1.json"), "w", encoding="utf-8") as f:
        json.dump({"columns": cols, "abilities": rows}, f, ensure_ascii=False, indent=1)

    have_chain = sum(1 for r in rows if r["id"] in chain)
    channels = sum(1 for r in rows if r["channel"])
    print("roster: %d abilities -> %s" % (len(rows), tsv))
    print("  joined to cast chain: %d ; channel=true: %d" % (have_chain, channels))
    for want in ("65029", "65076"):
        for r in rows:
            if r["id"] == want:
                print("  %s %s castMode=%s channel=%d dash=%s tani=%s"
                      % (r["id"], r["name"], r["castMode"], r["channel"], r["dash"],
                         os.path.basename(r["tani"].replace("\\", "/"))))


if __name__ == "__main__":
    main()
