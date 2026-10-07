#!/usr/bin/env python3
"""Parse the JX3 animation-tag container (.tani, GATA magic) and dump MotionTag streams.

Container (proven 2026-10-06, KG3DEngineX64.dll):
  KG3DAnimationTagDataContainer::_Load @0x180291490 reads a 0x130-byte header:
    +0x00 magic "GATA" (u32 0x41544147)   +0x04 version
    +0x08 base .ani path (GBK, 0x103 bytes)
    +0x10C block count
  then per block a 12-byte header {u32 type, u32 version, u32 flag}:
    flag != 0  -> class object created by type, LoadFromFile reads the payload:
      type 0 = KG3DSFXTagData (vtable 0x1806B3718)
      type 1 = KG3DSoundTagData (vtable 0x1806B3A58)
      type 2 = KG3DMotionTagData (vtable 0x1806B31C8)   <- this tool
    flag == 0  -> type 0/1: no payload; type 2: 8 extra bytes.
  KG3DMotionTagData::LoadFromFile @0x180299AC0 version 1:
    u32, u32 (two header values), then per keyframe a 0x188-byte record:
      +0x00: hash string (ASCII, e.g. "User Define Tag")
      +0x100 u32 time/frame
      +0x104 u32 tag count n
      +0x108 u32[n] per-tag payload byte sizes
    then n tag payloads sequentially; each begins with u32 type (0..11).
    Fixed tag struct sizes (clamp table): 8,0x118,0x19C,0x14,0x10,0xC,0x2C,
    0x30,0x34,0x68,0x4C,0x58.

Usage:
  python tools/character/motion_tag.py selftest
  python tools/character/motion_tag.py FILE.tani [--tsv OUT] [--json OUT]
"""
from __future__ import annotations

import argparse
import json
import struct
import sys

GATA = 0x41544147
TAG_SIZES = [0x8, 0x118, 0x19C, 0x14, 0x10, 0xC, 0x2C, 0x30, 0x34, 0x68, 0x4C, 0x58]
BLOCK_NAMES = {0: "SFX", 1: "Sound", 2: "Motion"}


def _u32(d: bytes, o: int) -> int:
    return struct.unpack_from("<I", d, o)[0]


def parse_header(d: bytes) -> dict:
    if len(d) < 0x130:
        raise ValueError("file shorter than 0x130-byte header")
    magic = _u32(d, 0)
    if magic != GATA:
        raise ValueError("bad magic %#x (expected GATA)" % magic)
    path_end = d.find(b"\0", 8, 8 + 0x103)
    if path_end < 0:
        path_end = 8 + 0x103
    return {
        "magic": "GATA",
        "version": _u32(d, 4),
        "base_ani": d[8:path_end].decode("gb18030", "replace"),
        "block_count": _u32(d, 0x10C),
    }


def _parse_motion_chain(d: bytes, off: int, key_count: int):
    """Parse key_count motion records starting at off. Returns (records, end)."""
    if off + 8 > len(d):
        raise ValueError("truncated motion payload header")
    head0, head1 = _u32(d, off), _u32(d, off + 4)
    off += 8
    records = []
    for _ in range(key_count):
        if off + 0x188 > len(d):
            raise ValueError("truncated 0x188 record")
        rec_end = off + 0x188
        h = d[off:off + 0x100]
        h = h.split(b"\0")[0]
        time = _u32(d, off + 0x100)
        n = _u32(d, off + 0x104)
        if n > 16:
            raise ValueError("tag count %d too large" % n)
        sizes = [_u32(d, off + 0x108 + 4 * i) for i in range(n)]
        tags = []
        p = rec_end
        for s in sizes:
            if s <= 0 or p + s > len(d):
                raise ValueError("bad tag payload size %d" % s)
            ty = _u32(d, p)
            tags.append({"type": ty, "size": s, "data": d[p:p + s].hex()})
            p += s
        records.append({
            "offset": off,
            "hash": h.decode("ascii", "replace"),
            "time": time,
            "tags": tags,
        })
        off = p
    return {"head0": head0, "head1": head1, "records": records}, off


def find_motion_blocks(d: bytes):
    """Locate blocks whose motion payload parses fully; pick the longest chain."""
    best = None
    for o in range(0x130, len(d) - 12):
        t, v, flag = _u32(d, o), _u32(d, o + 4), _u32(d, o + 8)
        if t != 2 or v > 3:
            continue
        if flag != 0:
            nkeys = flag
        else:
            # empty motion block: 2 header u32 + 8 bytes
            nkeys = 0
        if nkeys == 0:
            continue
        if nkeys > 4096:
            continue
        try:
            chain, end = _parse_motion_chain(d, o + 12, nkeys)
        except ValueError:
            continue
        cand = {
            "offset": o,
            "version": v,
            "key_count": nkeys,
            "chain": chain,
            "end": end,
        }
        # score: full-parse chains that reach EOF or a plausible next block
        if best is None or (end > best["end"]):
            best = cand
    return best


def parse(path: str) -> dict:
    with open(path, "rb") as f:
        d = f.read()
    out = {"file": path, "size": len(d)}
    out["header"] = parse_header(d)
    # best-effort block list (SFX/Sound payload sizes are not modelled here)
    blocks = []
    off = 0x130
    while off + 12 <= len(d) and len(blocks) < 64:
        t, v, flag = _u32(d, off), _u32(d, off + 4), _u32(d, off + 8)
        if t not in BLOCK_NAMES:
            break
        e = {"offset": off, "type": t, "type_name": BLOCK_NAMES[t],
             "version": v, "flag": flag}
        if t == 2 and flag:
            try:
                chain, end = _parse_motion_chain(d, off + 12, flag)
                e["keys"] = len(chain["records"])
                e["end"] = end
                off = end
            except ValueError:
                e["parse_error"] = True
                blocks.append(e)
                break
        else:
            e["payload_unknown"] = True
            blocks.append(e)
            break
        blocks.append(e)
    out["blocks_walked"] = blocks
    mb = find_motion_blocks(d)
    out["motion"] = mb
    return out


def dump_tsv(res: dict, fh) -> None:
    fh.write("block\tkey\toffset\ttime\thash\tntags\ttag_index\ttag_type\ttag_size\ttag_data\n")
    mb = res.get("motion")
    if not mb:
        return
    for ki, rec in enumerate(mb["chain"]["records"]):
        if not rec["tags"]:
            fh.write("motion\t%d\t%#x\t%d\t%s\t0\t\t\t\t\n"
                     % (ki, rec["offset"], rec["time"], rec["hash"]))
        for ti, tag in enumerate(rec["tags"]):
            fh.write("motion\t%d\t%#x\t%d\t%s\t%d\t%d\t%d\t%d\t%s\n"
                     % (ki, rec["offset"], rec["time"], rec["hash"],
                        len(rec["tags"]), ti, tag["type"], tag["size"], tag["data"]))


def _build_synthetic() -> bytes:
    """Synthetic GATA container with one motion block: 2 keys, 1 tag each."""
    path = b"data\\source\\player\\f1\\test.ani"
    hdr = struct.pack("<I", GATA) + struct.pack("<I", 1)
    hdr += path + b"\0"
    hdr = hdr.ljust(0x10C, b"\0")
    hdr += struct.pack("<I", 1)          # block count
    hdr = hdr.ljust(0x130, b"\0")
    block = struct.pack("<III", 2, 1, 2)  # motion, version 1, 2 keys
    payload = struct.pack("<II", 0, 0)
    rec1 = b"TestTag\0".ljust(0x100, b"\0") + struct.pack("<II", 3, 1) + struct.pack("<I", 8)
    rec1 = rec1.ljust(0x188, b"\0")
    rec1 += struct.pack("<II", 0, 0x3F800000)  # type 0, float 1.0
    rec2 = b"OtherTag\0".ljust(0x100, b"\0") + struct.pack("<II", 9, 1) + struct.pack("<I", 8)
    rec2 = rec2.ljust(0x188, b"\0")
    rec2 += struct.pack("<II", 0, 0x3F800000)
    return hdr + block + payload + rec1 + rec2


def selftest() -> int:
    checks = []
    d = _build_synthetic()
    h = parse_header(d)
    checks.append(("magic/version", h["magic"] == "GATA" and h["version"] == 1))
    checks.append(("base_ani", h["base_ani"] == "data\\source\\player\\f1\\test.ani"))
    checks.append(("block_count", h["block_count"] == 1))
    mb = find_motion_blocks(d)
    checks.append(("motion found", mb is not None and mb["offset"] == 0x130))
    checks.append(("key count", mb and mb["key_count"] == 2))
    checks.append(("key times", mb and [r["time"] for r in mb["chain"]["records"]] == [3, 9]))
    checks.append(("hashes", mb and [r["hash"] for r in mb["chain"]["records"]] == ["TestTag", "OtherTag"]))
    checks.append(("tags", mb and all(len(r["tags"]) == 1 and r["tags"][0]["type"] == 0
                                      and r["tags"][0]["size"] == 8 for r in mb["chain"]["records"])))
    checks.append(("full consume", mb and mb["end"] == len(d)))
    # negatives
    try:
        parse_header(b"\0" * 0x130)
        checks.append(("bad magic rejected", False))
    except ValueError:
        checks.append(("bad magic rejected", True))
    try:
        _parse_motion_chain(d, 0x130 + 12, 1)  # 1 key on 2-key block: ends before EOF, but parses
        checks.append(("partial chain parses", True))
    except ValueError:
        checks.append(("partial chain parses", False))
    ok = 0
    for name, res in checks:
        print("%s %s" % ("PASS" if res else "FAIL", name))
        ok += 1 if res else 0
    print("selftest: %d/%d" % (ok, len(checks)))
    return 0 if ok == len(checks) else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("file", nargs="?", help="path to .tani")
    ap.add_argument("command", nargs="?", help="'selftest'")
    ap.add_argument("--tsv")
    ap.add_argument("--json")
    args = ap.parse_args(argv)
    if args.file == "selftest" or args.command == "selftest":
        return selftest()
    if not args.file:
        ap.error("file required (or 'selftest')")
    res = parse(args.file)
    print("file=%s size=%d" % (res["file"], res["size"]))
    print("header=%s" % json.dumps(res["header"], ensure_ascii=False))
    for b in res["blocks_walked"]:
        print("block %s" % json.dumps(b, ensure_ascii=False))
    mb = res["motion"]
    if mb:
        print("motion block @%#x version=%d keys=%d end=%#x"
              % (mb["offset"], mb["version"], mb["key_count"], mb["end"]))
        for i, r in enumerate(mb["chain"]["records"]):
            print("  key %d time=%d hash=%r tags=%s"
                  % (i, r["time"], r["hash"],
                     [(t["type"], t["size"]) for t in r["tags"]]))
    else:
        print("no motion block found")
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(res, f, indent=1, ensure_ascii=False)
    if args.tsv:
        with open(args.tsv, "w", encoding="utf-8", newline="") as f:
            dump_tsv(res, f)
    return 0


if __name__ == "__main__":
    sys.exit(main())
