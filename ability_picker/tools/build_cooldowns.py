#!/usr/bin/env python3
"""Extract the client's cooldown table (settings/CoolDownList.tab).

Row id -> cooldown Duration (seconds), MaxCount (charges), MaxDuration. The GCD is
row 16 (1.5 s); skills reference a row via `SetPublicCoolDown` / `SetNormalCoolDown`.

Emits ability_picker/data/cooldowns_f1.tsv: id durationSec minDuration maxCount maxDuration.

Reproduce:
  .venv\\Scripts\\python.exe ability_picker\\tools\\build_cooldowns.py
"""
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "data")
PKV4 = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PakV4SfxExtract.exe"
VFS = "settings/CoolDownList.tab"


def extract():
    tmp = tempfile.mkdtemp(prefix="cd_")
    pl = os.path.join(tmp, "p.txt")
    open(pl, "wb").write((VFS + "\r\n").encode("gb18030"))
    subprocess.run([PKV4, pl, tmp], cwd=os.path.dirname(PKV4),
                   capture_output=True, timeout=180)
    p = os.path.join(tmp, "settings", "CoolDownList.tab")
    return p if os.path.exists(p) else None


def main():
    p = extract()
    if not p:
        print("CoolDownList.tab extract failed", file=sys.stderr)
        return 2
    lines = open(p, "rb").read().decode("gb18030", "replace").splitlines()
    hdr = lines[0].split("\t")
    ci = {h: i for i, h in enumerate(hdr)}
    out = []
    for line in lines[1:]:
        c = line.split("\t")
        if not c or not c[0].isdigit():
            continue
        def g(name):
            i = ci.get(name, -1)
            return c[i] if 0 <= i < len(c) else ""
        out.append((c[0], g("Duration"), g("MinDuration"), g("MaxCount"), g("MaxDuration")))
    tsv = os.path.join(DATA, "cooldowns_f1.tsv")
    with open(tsv, "w", encoding="utf-8", newline="\n") as f:
        f.write("id\tdurationSec\tminDuration\tmaxCount\tmaxDuration\n")
        for r in out:
            f.write("\t".join(r) + "\n")
    print("cooldowns: %d rows -> %s" % (len(out), tsv))
    for want in ("16", "1"):
        for r in out:
            if r[0] == want:
                print("  row %s duration=%ss maxCount=%s" % (r[0], r[1], r[3]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
