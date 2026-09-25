#!/usr/bin/env python3
"""Verify the decoded hotkey default map and movement constants.

Parses proof/movement/extracted/ui_hotkey_default.txt and decodes
key = VK | (mods << 16) with mods bit0=Ctrl, bit1=Shift, bit2=Alt
(as decoded from UI::KHotkeyMgr::LoadAsOldAdd in KGUIX64.dll).
"""
from __future__ import annotations

import string
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_TXT = ROOT / "proof" / "movement" / "extracted" / "ui_hotkey_default.txt"

MODS = (("Ctrl", 1), ("Shift", 2), ("Alt", 4))
NAMED = {
    1: "LMB",
    2: "RMB",
    32: "Space",
    37: "Left",
    38: "Up",
    39: "Right",
    40: "Down",
    111: "Numpad/",
    122: "F11",
    144: "NumLock",
    187: "=",
    189: "-",
    256: "WheelUp",
    257: "WheelDown",
}

WANT = {
    "MOVEFORWARD", "MOVEBACKWARD", "TURNLEFT", "TURNRIGHT", "STRAFELEFT",
    "STRAFERIGHT", "JUMP", "TOGGLERUN", "TOGGLEAUTORUN", "CAMERARESET",
    "SKILL_CAST_FORWARD", "SKILL_CAST_BACK", "SKILL_CAST_LEFT",
    "SKILL_CAST_RIGHT", "FOLLOWTARGET", "TOGGLE_UI", "RIDEHORSE",
}


def key_name(value: str) -> str:
    if not value.strip():
        return "-"
    v = int(value)
    mods = [name for name, bit in MODS if (v >> 16) & bit]
    k = v & 0xFFFF
    if k in NAMED:
        base = NAMED[k]
    elif k in range(48, 58):
        base = chr(k)
    elif k in range(65, 91):
        base = chr(k)
    elif k in range(96, 106):
        base = "Numpad" + str(k - 96)
    elif 32 <= k < 127 and chr(k) in string.printable:
        base = chr(k)
    else:
        base = f"VK_{k}"
    return "+".join(mods + [base]) if mods else base


def main() -> int:
    text = DEFAULT_TXT.read_text(encoding="gbk", errors="replace")
    for line in text.splitlines():
        cols = line.split("\t")
        if len(cols) < 4 or cols[0] not in WANT:
            continue
        print(f"{cols[0]:<20} key1={key_name(cols[2]):<14} key2={key_name(cols[3]):<14}")

    mask = 0x1000011E
    print("state mask 0x1000011E bits:", [i for i in range(32) if (mask >> i) & 1])
    print("turn threshold 0x50/0x100 =", 0x50 / 0x100 * 360, "deg")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
