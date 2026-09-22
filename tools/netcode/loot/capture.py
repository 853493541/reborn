#!/usr/bin/env python3
"""Loot-capture decoder for JX3 (绝境战场 / 沙漠风暴) using the layouts recovered in
docs/netcode/JX3_LOOT_PROTOCOL_LAYOUTS.md.

Input: JSONL, one record per packet the client handler sees:

    {"dir":"S2C","opcode":4660,"buf":"<hex bytes>"}
    {"dir":"C2S","opcode":77,  "buf":"<hex bytes>"}

`buf` is the raw packet buffer as the handler reads it (offset 0 = start of the
buffer). Opcodes are not required to be known: use `detect` to probe every record
and classify by payload shape, or provide a map:

    {"neww": 1234, "state": 1235, "loot": 1236}

Outputs (into --out dir):
  spawns.csv        container spawns (id, template, position, region, flags)
  loot.csv          rolled loot entries per container (the drop-table results)
  takes.csv         take/money requests (0x4D/0x51)
  heatmap.txt       ASCII heatmap per spawn (plain grid, coordinates rounded)
  summary.txt       counts + per-template loot histogram

Self-test:  python loot_capture.py selftest
"""
from __future__ import annotations

import argparse
import json
import struct
import sys
from collections import Counter, defaultdict
from pathlib import Path


def u8(b: bytes, o: int) -> int:
    return b[o]


def u16(b: bytes, o: int) -> int:
    return struct.unpack_from("<H", b, o)[0]


def u32(b: bytes, o: int) -> int:
    return struct.unpack_from("<I", b, o)[0]


def s8(b: bytes, o: int) -> int:
    return struct.unpack_from("<b", b, o)[0]


def u64(b: bytes, o: int) -> int:
    return struct.unpack_from("<Q", b, o)[0]


# ---------------------------------------------------------------- decoders


def decode_new_doodad(p: bytes) -> dict:
    """S->C OnSyncNewDoodad (handler 0x18019D510)."""
    packed = u64(p, 0x39)
    x = packed & 0x3FFFF
    z = (packed >> 18) & 0x3FFFF
    y = (packed >> 36) & 0x3FFFFF
    rec = {
        "template": u32(p, 0x07),
        "id": u32(p, 0x0B),
        "flag_f": u8(p, 0x0F),
        "flag_10": u8(p, 0x10),
        "flag_11": u8(p, 0x11),
        "kind": u8(p, 0x12),
        "state": u8(p, 0x13),
        "npc_template": u32(p, 0x14),
        "npc2": u32(p, 0x18),
        "v1c": u32(p, 0x1C),
        "flag_28": u8(p, 0x28),
        "v29": u32(p, 0x29),
        "count": u32(p, 0x2D),
        "v31": u32(p, 0x31),
        "link_id": u32(p, 0x35),
        "x": x,
        "z": z,
        "y": y,
        "region_x": (x >> 11) & 0x7F,
        "region_z": (z >> 11) & 0x7F,
        "bit58": (packed >> 58) & 1,
        "bit59": (packed >> 59) & 1,
        "bit60": (packed >> 60) & 1,
        "flag_43": u8(p, 0x43),
    }
    return rec


def decode_doodad_state(p: bytes) -> dict:
    """S->C OnSyncDoodadState (handler 0x1801975F0)."""
    return {
        "id": u32(p, 0x07),
        "state": s8(p, 0x0B),
        "flag_c": u8(p, 0x0C),
        "flag_d": u8(p, 0x0D),
    }


def decode_loot_list(p: bytes) -> dict:
    """S->C OnSyncLootList (handler 0x18019BA60).

    Records: u8 type; u32 index; u8 count; u8 flag; u8 extra_len; extra[extra_len]
    """
    size = u16(p, 0x07)
    rec = {
        "size": size,
        "container_id": u32(p, 0x09),
        "list_id": u32(p, 0x0D),
        "new_window": u8(p, 0x11),
        "v12": u32(p, 0x12),
        "looter_count": u8(p, 0x16),
        "v17": u32(p, 0x17),
        "v1b": u32(p, 0x1B),
        "looters": [],
        "items": [],
        "trailing": 0,
    }
    n = rec["looter_count"]
    off = 0x1F
    if n > 64 or off + 4 * n > len(p):
        n = 0
        rec["looter_count"] = 0
    for i in range(n):
        rec["looters"].append(u32(p, off))
        off += 4
    end = min(size, len(p)) if size else len(p)
    while off + 8 <= end:
        itype = p[off]
        index = u32(p, off + 1)
        count = p[off + 5]
        flag = p[off + 6]
        extra_len = p[off + 7]
        extra = p[off + 8: off + 8 + extra_len]
        rec["items"].append({
            "type": itype,
            "index": index,
            "count": count,
            "flag": flag,
            "extra": extra.decode("gb18030", errors="replace"),
        })
        off += 8 + extra_len
    rec["trailing"] = max(0, end - off)
    return rec


def decode_apply_loot(p: bytes) -> dict:
    """C->S DoApplyLootList, protocol 0x4D, 15-byte frame (u32 @+0xB)."""
    return {"container_id": u32(p, 0x0B)}


def decode_loot_money(p: bytes) -> dict:
    """C->S DoLootMoney, protocol 0x51, 15-byte frame (u32 @+0xB)."""
    return {"loot_index": u32(p, 0x0B)}


# ---------------------------------------------------------------- detection

TEMPLATE_RANGE = (1, 20000)
POS_MASK = 0x3FFFF


def looks_like_new_doodad(p: bytes) -> bool:
    if len(p) < 0x44:
        return False
    t = u32(p, 0x07)
    gid = u32(p, 0x0B)
    if not (TEMPLATE_RANGE[0] <= t <= TEMPLATE_RANGE[1]):
        return False
    if gid == 0 or gid > 0xFFFFFFF:
        return False
    packed = u64(p, 0x39)
    x = packed & POS_MASK
    z = (packed >> 18) & POS_MASK
    return x != 0 or z != 0


def looks_like_loot_list(p: bytes) -> bool:
    if len(p) < 0x1F:
        return False
    size = u16(p, 0x07)
    if not (0x1F <= size <= len(p)):
        return False
    n = u8(p, 0x16)
    if n > 64:
        return False
    off = 0x1F + 4 * n
    if off > size:
        return False
    try:
        probes = 0
        while off + 8 <= size and probes < 200:
            extra = p[off + 7]
            if off + 8 + extra > size:
                return False
            off += 8 + extra
            probes += 1
        return off == size
    except IndexError:
        return False


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
    dec.add_argument("--opcode-map", type=Path,
                     help="JSON {neww:.., state:.., loot:..} opcode hints")
    dec.add_argument("--detect", action="store_true",
                     help="ignore dir/opcodes and classify by payload shape")

    sub.add_parser("selftest")
    args = ap.parse_args(argv)

    if args.cmd == "selftest":
        return selftest()
    return run_decode(args)


def run_decode(args) -> int:
    opmap = json.loads(args.opcode_map.read_text(encoding="utf-8")) if args.opcode_map else {}
    inv = {v: k for k, v in opmap.items()}

    spawns, states, loots, takes = [], [], [], []
    for rec in load_records(args.capture):
        buf = bytes.fromhex(rec["buf"])
        kind = inv.get(rec.get("opcode"))
        if args.detect or not kind:
            if looks_like_new_doodad(buf):
                kind = "neww"
            elif looks_like_loot_list(buf):
                kind = "loot"
            else:
                kind = None
        try:
            if kind == "neww":
                d = decode_new_doodad(buf)
                d["opcode"] = rec.get("opcode")
                spawns.append(d)
            elif kind == "state":
                d = decode_doodad_state(buf)
                d["opcode"] = rec.get("opcode")
                states.append(d)
            elif kind == "loot":
                d = decode_loot_list(buf)
                d["opcode"] = rec.get("opcode")
                loots.append(d)
            else:
                op = rec.get("opcode")
                if op == 0x4D:
                    takes.append({"kind": "item", **decode_apply_loot(buf)})
                elif op == 0x51:
                    takes.append({"kind": "money", **decode_loot_money(buf)})
        except (IndexError, struct.error) as exc:
            print(f"WARN bad record opcode={rec.get('opcode')}: {exc}", file=sys.stderr)

    out = args.out
    out.mkdir(parents=True, exist_ok=True)

    with (out / "spawns.csv").open("w", encoding="utf-8") as fh:
        cols = ["id", "template", "x", "z", "y", "region_x", "region_z", "kind",
                "state", "link_id", "npc_template", "count", "bit58", "bit59", "bit60"]
        fh.write(",".join(cols) + "\n")
        for s in spawns:
            fh.write(",".join(str(s.get(c, "")) for c in cols) + "\n")

    with (out / "loot.csv").open("w", encoding="utf-8") as fh:
        fh.write("container_id,type,index,count,flag,extra\n")
        for l in loots:
            for it in l["items"]:
                fh.write(f"{l['container_id']},{it['type']},{it['index']},"
                         f"{it['count']},{it['flag']},{it['extra']}\n")

    with (out / "takes.csv").open("w", encoding="utf-8") as fh:
        fh.write("kind,id_or_index\n")
        for t in takes:
            fh.write(f"{t['kind']},{t.get('container_id', t.get('loot_index', ''))}\n")

    if spawns:
        xs = [s["x"] for s in spawns]
        zs = [s["z"] for s in spawns]
        step = max(1, (max(xs) - min(xs)) // 60 or 1)
        width = (max(xs) - min(xs)) // step + 1
        height = (max(zs) - min(zs)) // step + 1
        grid = [["." for _ in range(width)] for _ in range(height)]
        for s in spawns:
            gx = (s["x"] - min(xs)) // step
            gz = (s["z"] - min(zs)) // step
            if 0 <= gz < height and 0 <= gx < width:
                grid[gz][gx] = "#" if grid[gz][gx] == "." else "*"
        with (out / "heatmap.txt").open("w", encoding="utf-8") as fh:
            fh.write(f"spawn heatmap x {min(xs)}..{max(xs)} z {min(zs)}..{max(zs)} "
                     f"step={step}\n")
            for row in grid:
                fh.write("".join(row) + "\n")

    hist = defaultdict(Counter)
    for l in loots:
        for it in l["items"]:
            hist[l["container_id"]][(it["type"], it["index"], it["count"])] += 1

    with (out / "summary.txt").open("w", encoding="utf-8") as fh:
        fh.write(f"spawns={len(spawns)} states={len(states)} "
                 f"loot_windows={len(loots)} takes={len(takes)}\n")
        tmpl = Counter(s["template"] for s in spawns)
        fh.write(f"distinct templates={len(tmpl)}\n")
        for t, c in tmpl.most_common(30):
            fh.write(f"  template {t}: {c}\n")
        fh.write("loot by container:\n")
        for cid, counter in list(hist.items())[:50]:
            fh.write(f"  container {cid}: {dict(counter)}\n")

    print(f"spawns={len(spawns)} loots={len(loots)} takes={len(takes)} -> {out}")
    return 0


# ---------------------------------------------------------------- self-test


def build_new_doodad(template=6816, gid=0x123456, x=147463, z=49911, y=5231,
                     kind=0x0E, link=0xFFFFFFFF, flag=1) -> bytes:
    buf = bytearray(0x60)
    struct.pack_into("<I", buf, 0x07, template)
    struct.pack_into("<I", buf, 0x0B, gid)
    buf[0x12] = kind
    struct.pack_into("<I", buf, 0x35, link)
    packed = (x & 0x3FFFF) | ((z & 0x3FFFF) << 18) | ((y & 0x3FFFFF) << 36) | (flag << 58)
    struct.pack_into("<Q", buf, 0x39, packed)
    return bytes(buf)


def build_loot_list(container=0x123456, items=((1, 100446, 1, 0, ""),
                                              (7, 42592, 2, 1, "abc"))) -> bytes:
    body = bytearray()
    for itype, index, count, flag, extra in items:
        ex = extra.encode("gb18030")
        body += bytes([itype]) + struct.pack("<I", index) + bytes([count, flag,
                                                                   len(ex)]) + ex
    looters = struct.pack("<I", 0xABCD)
    size = 0x1F + len(looters) + len(body)
    buf = bytearray(size)
    struct.pack_into("<H", buf, 0x07, size)
    struct.pack_into("<I", buf, 0x09, container)
    buf[0x11] = 0
    buf[0x16] = 1
    buf[0x1F:0x23] = looters
    buf[0x23:] = body
    return bytes(buf)


def selftest() -> int:
    ok = True

    def check(name, cond, detail=""):
        nonlocal ok
        print(("PASS" if cond else "FAIL") + f": {name}" + (f" - {detail}" if detail else ""))
        ok = ok and cond

    d = build_new_doodad()
    check("shape detect new doodad", looks_like_new_doodad(d))
    r = decode_new_doodad(d)
    check("new doodad fields", r["template"] == 6816 and r["id"] == 0x123456
          and r["x"] == 147463 and r["z"] == 49911 and r["y"] == 5231
          and r["region_x"] == (147463 >> 11) & 0x7F and r["bit58"] == 1,
          str(r))

    st = bytearray(0x10)
    struct.pack_into("<I", st, 7, 0x123456)
    struct.pack_into("<b", st, 0x0B, -3)
    st[0x0C] = 1
    check("doodad state", decode_doodad_state(bytes(st)) ==
          {"id": 0x123456, "state": -3, "flag_c": 1, "flag_d": 0})

    l = build_loot_list()
    check("shape detect loot list", looks_like_loot_list(l))
    rl = decode_loot_list(l)
    check("loot entries", rl["container_id"] == 0x123456 and rl["looter_count"] == 1
          and rl["looters"] == [0xABCD] and len(rl["items"]) == 2
          and rl["items"][0] == {"type": 1, "index": 100446, "count": 1,
                                 "flag": 0, "extra": ""}
          and rl["items"][1]["extra"] == "abc" and rl["trailing"] == 0, str(rl))

    a = bytearray(15)
    struct.pack_into("<I", a, 0x0B, 0x123456)
    check("apply loot", decode_apply_loot(bytes(a))["container_id"] == 0x123456)
    m = bytearray(15)
    struct.pack_into("<I", m, 0x0B, 7)
    check("loot money", decode_loot_money(bytes(m))["loot_index"] == 7)

    check("loot list rejected for doodad", not looks_like_loot_list(d))
    check("doodad rejected for loot", not looks_like_new_doodad(l))
    print("SELFTEST", "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
