#!/usr/bin/env python3
"""绝境战场 mode capture decoder (S2C packets with recovered opcode IDs).

Opcodes come from the KPlayerClient registration table parsed by
tools/netcode/parse_protocol_registration.py (see
proof/netcode/protocol_table_s2c_annotated.tsv and
docs/netcode/JX3_MODE_MATCH_LIFECYCLE.md §6).

Input: JSONL, one record per packet the client handler sees:

    {"dir":"S2C","opcode":282,"buf":"<hex bytes>"}

"opcode" is the decimal protocol id (buffer offset 0 = frame start, so the id
field at +0x00 and payload at +0x07..). Records with unknown opcodes are
skipped unless --raw is given (then logged to raw_unknown.csv).

Outputs (into --out dir):
  switch_maps.csv   map id + placement x/y/z + scene (opcode 0x08)
  sides.csv         character id -> battlefield side (0xAA)
  objective.csv     objective array increments: index,valueA,valueB (0x11A)
  pq_clock.csv      phase clock: stateA,stateB,startOffset,duration (0x11B)
  statistics.csv    per-player statistics record (0x119)
  stat_flags.csv    battle stat flag packets (0x330)
  bf_roles.csv      BR role data (0x245, raw + known header fields)
  raw_known.csv     var-length mode handlers logged as hex
                    (0x25B rank, 0x25F/0x260/0x262 competitor, 0x323/0x324 rooms)
  summary.txt       counts

Self-test:  python tools/netcode/mode/capture.py selftest
"""
from __future__ import annotations

import argparse
import json
import struct
import sys
from collections import Counter
from pathlib import Path

# ---------------------------------------------------------------- opcodes

OPCODES = {
    "switch_map": 0x08,        # OnSwitchMap (31 B)
    "side": 0xAA,              # OnSetBattleFieldSide (15 B)
    "statistics": 0x119,       # OnSyncBattlefieldStatistics (264 B)
    "objective": 0x11A,        # OnSyncBFObjectiveIncrement (16 B)
    "pq_clock": 0x11B,         # OnSyncBFPQInfo (23 B)
    "bf_role": 0x245,          # OnSyncBFRoleData (112 B)
    "stat_flag": 0x330,        # OnSyncBattleStatFlag (15 B)
}
RAW_KNOWN = {
    0x25B: "OnGetBFRankRespond",
    0x25F: "OnSyncBaseInfoFromBattlefieldCompetitorList",
    0x260: "OnSyncVariableInfoFromBattlefieldCompetitorList",
    0x261: "OnSyncBattlefieldCompetitorCDState",
    0x262: "OnSyncBattlefieldCompetitorBuffList",
    0x323: "OnCreateBattlefieldRoomRespond",
    0x324: "OnForceStartBattleFieldChaosFightRespond",
}


def u8(b: bytes, o: int) -> int:
    return b[o]


def u16(b: bytes, o: int) -> int:
    return struct.unpack_from("<H", b, o)[0]


def u32(b: bytes, o: int) -> int:
    return struct.unpack_from("<I", b, o)[0]


def s32(b: bytes, o: int) -> int:
    return struct.unpack_from("<i", b, o)[0]


def u64(b: bytes, o: int) -> int:
    return struct.unpack_from("<Q", b, o)[0]


def text(b: bytes, o: int, n: int) -> str:
    return b[o:o + n].split(b"\x00", 1)[0].decode("gb18030", errors="replace")


# ---------------------------------------------------------------- decoders


def decode_switch_map(p: bytes) -> dict:
    """S->C 0x08 OnSwitchMap (31 B).

    field7 @+0x07 = map id; +0x0B unknown global; +0x0F/+0x13/+0x17 are stored
    to character +0x10/+0x14/+0x18 = world x/y/z (cm); +0x1B -> player+0xAB0.
    """
    return {
        "map_id": u32(p, 0x07),
        "a": u32(p, 0x0B),
        "x": u32(p, 0x0F),
        "y": u32(p, 0x13),
        "z": u32(p, 0x17),
        "scene": u32(p, 0x1B),
    }


def decode_side(p: bytes) -> dict:
    """S->C 0xAA OnSetBattleFieldSide (15 B): u32 char id @+0x07, u32 side @+0x0B."""
    return {"char_id": u32(p, 0x07), "side": u32(p, 0x0B)}


def decode_objective(p: bytes) -> dict:
    """S->C 0x11A OnSyncBFObjectiveIncrement (16 B).

    u8 index (<8) @+0x07, u32 A @+0x08, u32 B @+0x0C; writes the two 8-slot
    caches +0x1b498 (A) and +0x1b4b8 (B) and pokes the UI.
    """
    return {"index": u8(p, 0x07), "value_a": u32(p, 0x08), "value_b": u32(p, 0x0C)}


def decode_pq_clock(p: bytes) -> dict:
    """S->C 0x11B OnSyncBFPQInfo (23 B).

    u32 stateA @+0x07, u32 stateB @+0x0B, s32 start_offset @+0x0F,
    s32 duration @+0x13. Client stores:
      +0x1b488 = (now + server_offset) - start_offset
      +0x1b490 = base + duration   (duration == -1 -> 0 = no timer)
    Both are absolute client times the UI counts down (GetBattleFieldPQInfo).
    """
    return {
        "state_a": u32(p, 0x07),
        "state_b": u32(p, 0x0B),
        "start_offset": s32(p, 0x0F),
        "duration": s32(p, 0x13),
    }


STAT_COUNT = 25


def decode_statistics(p: bytes) -> dict:
    """S->C 0x119 OnSyncBattlefieldStatistics (264 B fixed).

    +0x0B u32 id; +0x0F u32 force; +0x13 u32 side; +0x17 char[0x20] name;
    +0x37 25 x u64 stats; +0xFF u64 global id; +0x107 u8 client version.
    """
    rec = {
        "id": u32(p, 0x0B),
        "force": u32(p, 0x0F),
        "side": u32(p, 0x13),
        "name": text(p, 0x17, 0x20),
        "global_id": u64(p, 0xFF),
        "client_version": u8(p, 0x107),
        "stats": [u64(p, 0x37 + 8 * i) for i in range(STAT_COUNT)],
    }
    return rec


def decode_stat_flag(p: bytes) -> dict:
    """S->C 0x330 OnSyncBattleStatFlag (15 B): u32 @+0x07, u32 @+0x0B."""
    return {"a": u32(p, 0x07), "b": u32(p, 0x0B)}


def decode_bf_role(p: bytes) -> dict:
    """S->C 0x245 OnSyncBFRoleData (112 B).

    +0x0F u32 player id (entity lookup); +0x17 array base, +0x6F u8 count.
    Element layout is not yet decoded, raw hex kept for analysis.
    """
    return {
        "player_id": u32(p, 0x0F),
        "v13": u32(p, 0x13),
        "count": u8(p, 0x6F),
        "raw": p[0x17:0x6F].hex(),
    }


# ---------------------------------------------------------------- run


def load_records(path: Path):
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        yield json.loads(line)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    sub = ap.add_subparsers(dest="cmd", required=True)

    dec = sub.add_parser("decode")
    dec.add_argument("capture", type=Path)
    dec.add_argument("--out", type=Path, required=True)
    dec.add_argument("--raw", action="store_true",
                     help="log unknown opcodes to raw_unknown.csv")

    sub.add_parser("selftest")
    args = ap.parse_args(argv)
    if args.cmd == "selftest":
        return selftest()
    return run_decode(args)


def run_decode(args) -> int:
    rows = {k: [] for k in ("switch_map", "side", "objective", "pq_clock",
                            "statistics", "stat_flag", "bf_role")}
    raw_known, raw_unknown = [], []
    counts = Counter()

    for rec in load_records(args.capture):
        op = rec.get("opcode")
        buf = bytes.fromhex(rec["buf"])
        try:
            if op == OPCODES["switch_map"]:
                rows["switch_map"].append(decode_switch_map(buf))
            elif op == OPCODES["side"]:
                rows["side"].append(decode_side(buf))
            elif op == OPCODES["objective"]:
                rows["objective"].append(decode_objective(buf))
            elif op == OPCODES["pq_clock"]:
                rows["pq_clock"].append(decode_pq_clock(buf))
            elif op == OPCODES["statistics"]:
                rows["statistics"].append(decode_statistics(buf))
            elif op == OPCODES["stat_flag"]:
                rows["stat_flag"].append(decode_stat_flag(buf))
            elif op == OPCODES["bf_role"]:
                rows["bf_role"].append(decode_bf_role(buf))
            elif op in RAW_KNOWN:
                raw_known.append({"opcode": op, "name": RAW_KNOWN[op],
                                  "size": len(buf), "hex": buf.hex()})
            elif args.raw:
                raw_unknown.append({"opcode": op, "size": len(buf),
                                    "hex": buf.hex()})
            counts[op] += 1
        except (IndexError, struct.error) as exc:
            print(f"WARN bad record opcode={op}: {exc}", file=sys.stderr)

    out = args.out
    out.mkdir(parents=True, exist_ok=True)

    def write(name, header, items, render):
        with (out / name).open("w", encoding="utf-8") as fh:
            fh.write(",".join(header) + "\n")
            for it in items:
                fh.write(render(it) + "\n")

    write("switch_maps.csv", ["map_id", "a", "x", "y", "z", "scene"],
          rows["switch_map"],
          lambda r: f"{r['map_id']},{r['a']},{r['x']},{r['y']},{r['z']},{r['scene']}")
    write("sides.csv", ["char_id", "side"], rows["side"],
          lambda r: f"{r['char_id']},{r['side']}")
    write("objective.csv", ["index", "value_a", "value_b"], rows["objective"],
          lambda r: f"{r['index']},{r['value_a']},{r['value_b']}")
    write("pq_clock.csv", ["state_a", "state_b", "start_offset_s", "duration_s"],
          rows["pq_clock"],
          lambda r: f"{r['state_a']},{r['state_b']},{r['start_offset']},{r['duration']}")
    write("statistics.csv",
          ["id", "force", "side", "name", "global_id", "client_version"]
          + [f"stat{i}" for i in range(STAT_COUNT)],
          rows["statistics"],
          lambda r: ",".join(str(x) for x in
                             [r["id"], r["force"], r["side"], r["name"],
                              r["global_id"], r["client_version"], *r["stats"]]))
    write("stat_flags.csv", ["a", "b"], rows["stat_flag"],
          lambda r: f"{r['a']},{r['b']}")
    write("bf_roles.csv", ["player_id", "v13", "count", "raw"], rows["bf_role"],
          lambda r: f"{r['player_id']},{r['v13']},{r['count']},{r['raw']}")
    write("raw_known.csv", ["opcode", "name", "size", "hex"], raw_known,
          lambda r: f"{r['opcode']},{r['name']},{r['size']},{r['hex']}")
    if args.raw:
        write("raw_unknown.csv", ["opcode", "size", "hex"], raw_unknown,
              lambda r: f"{r['opcode']},{r['size']},{r['hex']}")

    with (out / "summary.txt").open("w", encoding="utf-8") as fh:
        for k in ("switch_map", "side", "objective", "pq_clock",
                  "statistics", "stat_flag", "bf_role"):
            fh.write(f"{k}={len(rows[k])}\n")
        fh.write(f"raw_known={len(raw_known)} raw_unknown={len(raw_unknown)}\n")
        for op, c in counts.most_common():
            fh.write(f"  opcode {op:#x}: {c}\n")

    print(f"switch={len(rows['switch_map'])} side={len(rows['side'])} "
          f"objective={len(rows['objective'])} pq={len(rows['pq_clock'])} "
          f"stats={len(rows['statistics'])} -> {out}")
    return 0


# ---------------------------------------------------------------- self-test


def build_switch_map(map_id=512, a=7, x=147463, y=5231, z=49911, scene=788) -> bytes:
    buf = bytearray(31)
    struct.pack_into("<H", buf, 0, OPCODES["switch_map"])
    struct.pack_into("<I", buf, 7, map_id)
    struct.pack_into("<I", buf, 0x0B, a)
    struct.pack_into("<I", buf, 0x0F, x)
    struct.pack_into("<I", buf, 0x13, y)
    struct.pack_into("<I", buf, 0x17, z)
    struct.pack_into("<I", buf, 0x1B, scene)
    return bytes(buf)


def build_side(char_id=0x123456, side=3) -> bytes:
    buf = bytearray(15)
    struct.pack_into("<H", buf, 0, OPCODES["side"])
    struct.pack_into("<I", buf, 7, char_id)
    struct.pack_into("<I", buf, 0x0B, side)
    return bytes(buf)


def build_objective(index=2, a=12345, b=67890) -> bytes:
    buf = bytearray(16)
    struct.pack_into("<H", buf, 0, OPCODES["objective"])
    buf[7] = index
    struct.pack_into("<I", buf, 8, a)
    struct.pack_into("<I", buf, 0x0C, b)
    return bytes(buf)


def build_pq_clock(a=1, b=2, start=-30, duration=600) -> bytes:
    buf = bytearray(23)
    struct.pack_into("<H", buf, 0, OPCODES["pq_clock"])
    struct.pack_into("<I", buf, 7, a)
    struct.pack_into("<I", buf, 0x0B, b)
    struct.pack_into("<i", buf, 0x0F, start)
    struct.pack_into("<i", buf, 0x13, duration)
    return bytes(buf)


def build_bf_role(player_id=0x123456, v13=9, count=3) -> bytes:
    buf = bytearray(112)
    struct.pack_into("<H", buf, 0, OPCODES["bf_role"])
    struct.pack_into("<I", buf, 0x0F, player_id)
    struct.pack_into("<I", buf, 0x13, v13)
    buf[0x6F] = count
    buf[0x17:0x6F] = bytes(range(0x58))
    return bytes(buf)


def build_statistics(pid=0x1000, force=1, side=2, name="测试", gid=0xDEAD,
                     version=3) -> bytes:
    buf = bytearray(264)
    struct.pack_into("<H", buf, 0, OPCODES["statistics"])
    struct.pack_into("<I", buf, 0x0B, pid)
    struct.pack_into("<I", buf, 0x0F, force)
    struct.pack_into("<I", buf, 0x13, side)
    nb = name.encode("gb18030")
    buf[0x17:0x17 + len(nb)] = nb
    for i in range(STAT_COUNT):
        struct.pack_into("<Q", buf, 0x37 + 8 * i, 100 + i)
    struct.pack_into("<Q", buf, 0xFF, gid)
    buf[0x107] = version
    return bytes(buf)


def selftest() -> int:
    ok = True

    def check(name, cond, detail=""):
        nonlocal ok
        print(("PASS" if cond else "FAIL") + f": {name}" + (f" - {detail}" if detail else ""))
        ok = ok and cond

    r = decode_switch_map(build_switch_map())
    check("switch map", r == {"map_id": 512, "a": 7, "x": 147463, "y": 5231,
                              "z": 49911, "scene": 788}, str(r))
    check("side", decode_side(build_side()) == {"char_id": 0x123456, "side": 3})
    check("objective", decode_objective(build_objective()) ==
          {"index": 2, "value_a": 12345, "value_b": 67890})
    check("pq clock", decode_pq_clock(build_pq_clock()) ==
          {"state_a": 1, "state_b": 2, "start_offset": -30, "duration": 600})
    s = decode_statistics(build_statistics())
    check("statistics", s["id"] == 0x1000 and s["force"] == 1 and s["side"] == 2
          and s["name"] == "测试" and s["global_id"] == 0xDEAD
          and s["client_version"] == 3 and s["stats"][0] == 100
          and s["stats"][-1] == 124, str(s))
    check("stat flag", decode_stat_flag(build_pq_clock()) ==
          {"a": 1, "b": 2})
    role = decode_bf_role(build_bf_role())
    check("bf role", role["player_id"] == 0x123456 and role["v13"] == 9
          and role["count"] == 3 and len(role["raw"]) == 0x58 * 2, str(role))
    print("SELFTEST", "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
