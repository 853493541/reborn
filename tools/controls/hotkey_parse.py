#!/usr/bin/env python3
"""Decode the JX3 hotkey tables into human-readable annexes.

Inputs (extracted from the client pak):
  proof/movement/extracted/ui_hotkey_default.txt     (name context key1 key2)
  proof/movement/extracted/ui_hotkey_bindings.ini    ([COMMAND] desc/down/up)

Outputs (written to proof/controls/):
  hotkey_default_decoded.tsv        all default rows with decoded keys
  hotkey_command_registry.tsv       all commands with handlers and description
  hotkey_registry_by_context.md     registry grouped by context
  hotkey_default.md                 full default-binding table (markdown)

Key encoding: low 16 bits = VK, high 16 bits = modifier word
(Ctrl=0x1, Shift=0x2, Alt=0x4). Mouse: 1 LMB, 2 RMB, 256 wheel up, 257 wheel down.
"""
from __future__ import annotations

import sys
from pathlib import Path

VK = {
    1: "LMB", 2: "RMB", 256: "WheelUp", 257: "WheelDown",
    3: "Cancel", 8: "Backspace", 9: "Tab", 12: "Clear", 13: "Enter",
    16: "Shift", 17: "Ctrl", 18: "Alt", 19: "Pause", 20: "CapsLock", 27: "Esc",
    32: "Space", 33: "PageUp", 34: "PageDown", 35: "End", 36: "Home",
    37: "Left", 38: "Up", 39: "Right", 40: "Down", 44: "PrintScreen",
    45: "Insert", 46: "Delete",
    48: "0", 49: "1", 50: "2", 51: "3", 52: "4", 53: "5", 54: "6", 55: "7",
    56: "8", 57: "9",
    65: "A", 66: "B", 67: "C", 68: "D", 69: "E", 70: "F", 71: "G", 72: "H",
    73: "I", 74: "J", 75: "K", 76: "L", 77: "M", 78: "N", 79: "O", 80: "P",
    81: "Q", 82: "R", 83: "S", 84: "T", 85: "U", 86: "V", 87: "W", 88: "X",
    89: "Y", 90: "Z",
    91: "LWin", 92: "RWin", 93: "Menu",
    96: "Num0", 97: "Num1", 98: "Num2", 99: "Num3", 100: "Num4", 101: "Num5",
    102: "Num6", 103: "Num7", 104: "Num8", 105: "Num9",
    106: "Num*", 107: "Num+", 109: "Num-", 110: "Num.", 111: "Num/",
    112: "F1", 113: "F2", 114: "F3", 115: "F4", 116: "F5", 117: "F6",
    118: "F7", 119: "F8", 120: "F9", 121: "F10", 122: "F11", 123: "F12",
    124: "F13", 125: "F14", 126: "F15", 127: "F16", 128: "F17", 129: "F18",
    130: "F19", 131: "F20", 132: "F21", 133: "F22", 134: "F23", 135: "F24",
    144: "NumLock", 145: "ScrollLock",
    186: ";", 187: "=", 188: ",", 189: "-", 190: ".", 191: "/", 192: "`",
    219: "[", 220: "\\", 221: "]", 222: "'", 226: "OEM102",
}


def decode_key(v: str) -> str:
    v = (v or "").strip()
    if not v:
        return ""
    try:
        n = int(v)
    except ValueError:
        return v
    low = n & 0xFFFF
    mods = (n >> 16) & 0xFFFF
    parts = []
    if mods & 0x4:
        parts.append("Alt")
    if mods & 0x2:
        parts.append("Shift")
    if mods & 0x1:
        parts.append("Ctrl")
    name = VK.get(low, "VK_0x%X" % low)
    return "+".join(parts + [name])


def read_gb(path: Path) -> str:
    return path.read_bytes().decode("gb18030", errors="replace")


def parse_default(path: Path):
    rows = []
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    for line in lines[1:]:
        if not line.strip():
            continue
        f = line.split("\t")
        if len(f) < 4:
            f = f + [""] * (4 - len(f))
        rows.append({"name": f[0].strip(), "context": f[1].strip(),
                     "key1": f[2].strip(), "key2": f[3].strip()})
    return rows


def parse_bindings(path: Path):
    cmds = {}
    cur = None
    for raw in read_gb(path).splitlines():
        line = raw.strip()
        if not line:
            continue
        if line.startswith("[") and line.endswith("]"):
            cur = line[1:-1]
            cmds[cur] = {"desc": "", "contextgroup": "", "down": "", "up": "",
                         "runOnUp": ""}
        elif cur and "=" in line:
            k, v = line.split("=", 1)
            k = k.strip()
            if k in cmds[cur]:
                cmds[cur][k] = v.strip()
    return cmds


# Movement command set dispatched by the client input core (C1/C2). The
# expected decoded keys are the defaults the real client ships.
MOVEMENT_MAP = {
    "MOVEFORWARD": ["W", "Up"],
    "MOVEBACKWARD": ["S", "Down"],
    "TURNLEFT": ["Left"],
    "TURNRIGHT": ["Right"],
    "STRAFELEFT": ["A"],
    "STRAFERIGHT": ["D"],
    "JUMP": ["Space"],
    "TOGGLERUN": ["Num/"],
    "TOGGLEAUTORUN": ["G", "NumLock"],
}


def movement_check(rows) -> int:
    """Numeric check of the decoded movement key map (offline gate for C1/C2)."""
    by_name = {r["name"]: r for r in rows}
    ok = True
    for name, want in MOVEMENT_MAP.items():
        r = by_name.get(name)
        if r is None:
            print("MISS %s (not in default.txt)" % name)
            ok = False
            continue
        got = [k for k in (decode_key(r["key1"]), decode_key(r["key2"])) if k]
        if got != want:
            print("FAIL %s got=%s want=%s" % (name, got, want))
            ok = False
        else:
            print("PASS %s %s" % (name, got))
    print("movement map: %s (%d commands)" % ("PASS" if ok else "FAIL", len(MOVEMENT_MAP)))
    return 0 if ok else 1


def main(argv) -> int:
    root = Path(__file__).resolve().parent.parent.parent
    dpath = root / "proof" / "movement" / "extracted" / "ui_hotkey_default.txt"
    bpath = root / "proof" / "movement" / "extracted" / "ui_hotkey_bindings.ini"
    out = root / "proof" / "controls"

    if "--movement-check" in argv:
        return movement_check(parse_default(dpath))

    out.mkdir(parents=True, exist_ok=True)

    default = parse_default(dpath)
    bindings = parse_bindings(bpath)

    # registry TSV
    with (out / "hotkey_command_registry.tsv").open("w", encoding="utf-8") as fh:
        fh.write("command\tcontextgroup\tdown\tup\trunOnUp\tdesc\n")
        for name in sorted(bindings):
            c = bindings[name]
            fh.write("\t".join([name, c["contextgroup"], c["down"], c["up"],
                                c["runOnUp"], c["desc"].replace("\t", " ")]) + "\n")

    # decoded default TSV
    with (out / "hotkey_default_decoded.tsv").open("w", encoding="utf-8") as fh:
        fh.write("command\tcontext\tkey1\tkey1_decoded\tkey2\tkey2_decoded\n")
        for r in default:
            fh.write("\t".join([r["name"], r["context"], r["key1"],
                                decode_key(r["key1"]), r["key2"],
                                decode_key(r["key2"])]) + "\n")

    # markdown annex: default bindings
    with (out / "hotkey_default.md").open("w", encoding="utf-8") as fh:
        fh.write("# Default bindings — decoded (all %d rows)\n\n" % len(default))
        fh.write("Source: `ui/hotkey/default.txt`. Key encoding: VK + modifiers.\n\n")
        fh.write("| # | command | context | key 1 | key 2 |\n|---|---|---|---|---|\n")
        for i, r in enumerate(default, 1):
            fh.write("| %d | `%s` | %s | %s | %s |\n" % (
                i, r["name"], r["context"] or "—",
                decode_key(r["key1"]) or "—", decode_key(r["key2"]) or "—"))

    # markdown annex: registry by context
    ctxgroups = {}
    for r in default:
        for cmd in (r["name"],):
            ctxgroups.setdefault(r["context"], []).append(cmd)
    bound = {r["name"] for r in default}
    with (out / "hotkey_registry_by_context.md").open("w", encoding="utf-8") as fh:
        fh.write("# Command registry — %d commands\n\n" % len(bindings))
        fh.write("| command | context | default key | down | up | description |\n")
        fh.write("|---|---|---|---|---|---|\n")
        default_map = {r["name"]: r for r in default}
        for name in sorted(bindings):
            c = bindings[name]
            r = default_map.get(name)
            keys = ""
            if r:
                k1 = decode_key(r["key1"])
                k2 = decode_key(r["key2"])
                keys = ", ".join([k for k in (k1, k2) if k])
            fh.write("| `%s` | %s | %s | `%s` | `%s` | %s |\n" % (
                name, (r["context"] if r else "") or "—", keys or "—",
                c["down"] or "—", c["up"] or "—",
                c["desc"].replace("|", "/")))

    print("default rows:", len(default))
    print("commands:", len(bindings))
    print("contexts:", sorted({r["context"] for r in default}))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
