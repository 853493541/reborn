#!/usr/bin/env python3
"""Decode JX3 MoveStateMask bitmasks against the MOVE_STATE enum run found in
JX3RepresentX64_all_strings.txt (0x00CBCB00..0x00CBCD9F).

Usage: python tools/pvp/decode_movestate.py [value ...]
"""
from __future__ import annotations

import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Order as found in the binary string run (consecutive addresses).
NAMES = [
    "ON_STAND", "ON_WALK", "ON_RUN", "ON_JUMP", "ON_SWIM_JUMP", "ON_SWIM",
    "ON_FLOAT", "ON_SIT", "ON_KNOCKED_DOWN", "ON_KNOCKED_BACK",
    "ON_KNOCKED_OFF", "ON_SPRINT_BREAK", "ON_SPRINT_DASH", "ON_SPRINT_KICK",
    "ON_SPRINT_FLASH", "ON_SKILL_MOVE_SRC", "ON_SKILL_MOVE_DST",
    "ON_SKILL_MOVE_TAIL", "ON_SKILL_MOVE_DEATH", "ON_HALT", "ON_FREEZE",
    "ON_ENTRAP", "ON_AUTO_FLY", "ON_DEATH", "ON_DASH", "ON_PULL",
    "ON_REPULSED", "ON_RISE", "ON_SKID", "ON_START_AUTO_FLY", "ON_FLY",
    "ON_FLY_FLOAT", "ON_FLY_JUMP", "ON_DASH_TO_POSITION", "ON_BIRD_FLY",
    "ON_BIRD_FLOAT", "ON_BIRD_JUMP",
]
# 1-based hypothesis: enum value = index+1 (ON_STAND=1)
IDX1 = {i + 1: n for i, n in enumerate(NAMES)}
# 0-based hypothesis: enum value = index (ON_STAND=0)
IDX0 = {i: n for i, n in enumerate(NAMES)}


def bits(v: int) -> list[int]:
    return [i for i in range(64) if v & (1 << i)]


def fmt(v: int, base: int) -> str:
    table = IDX1 if base == 1 else IDX0
    return ", ".join(f"{i}:{table.get(i, '?')}" for i in bits(v))


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args:
        for v in [3758030846, 4294967294, 4294967295, 256, 1405100032,
                  1405108224, 16384, 1342181376, 1610547150, 3619840030,
                  1073897664, 3552575536, 1405095936, 1405485056, 3757998078,
                  12, 18, 30, 259, 258, 286, 1610547166, 1405120514]:
            print(f"{v} 0x{v:08X}")
            print(f"   1-based: {fmt(v, 1)}")
            print(f"   0-based: {fmt(v, 0)}")
        raise SystemExit(0)
    for a in args:
        v = int(a, 0)
        print(f"{v} 0x{v:08X}")
        print(f"   1-based: {fmt(v, 1)}")
        print(f"   0-based: {fmt(v, 0)}")
