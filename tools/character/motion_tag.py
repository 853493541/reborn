#!/usr/bin/env python3
"""Parse the JX3 animation-tag container (.tani, GATA magic) and dump MotionTag streams.

Settled container model (SPEC_MOTION.md §3, corrected 2026-10-07; supersedes the
raw 0x188 signature scan and the earlier type-1=MotionTag claim):

  .tani = ANI_TAG_FILE, loaded by KG3D_AnimationTani_Data::LoadFromFile
  (KG3D_AnimationTagX64.dll 0x18001D260; magic GATA @0x18001D38F, header
  ReadObject(0x130) @0x18001D33D, factory _NewTagData 0x18001DB60).

  Header (0x130 bytes):
    +0x00 char[4] "GATA"    +0x04 u32 version (0/1; >=2 rejected)
    +0x08 NUL base .ani path (GBK)    +0x10C u32 group count
  Then per group a 12-byte header {i32 type, i32 version, i32 count} + payload.

  Group classes (tani factory jump table @RVA 0x1DEA4, cases 0..5):
    0 = SFXTag_Group_Data    (alloc 0x298, vtable 0x180048CC8, loader 0x180004AB0)
    1 = sound group          (alloc 0x508, vtable 0x1800490B0; payload carries
                              the "FMOD" magic + Wwise event names)
    2 = MotionTag_Group_Data (alloc 0x160, vtable 0x180048AC0, LoadFromFile =
                              vtable slot 3 = 0x180003000)
    3 = 0x130 / 4 = 0x58D8 / 5 = 0x1B0  (classes not decoded)

  Payload sizes (from the loaders):
    type 0 SFX: u32 n1 + n1*0x130 + count*(0x164 + 8*4 + 0x64 + (v==3 ? 4 : 0))
    type 2 MotionTag: v0 = count*0x970; v1 = u32,u32 + count*(0x188 + tag
      payloads); v2 = u32 n2 + n2*0x130 then v1.
      0x188 record: +0x00 hash string; +0x100 u32 time; +0x104 u32 tag count n;
      +0x108 u32[n] sizes; then n payloads, each beginning u32 type (0..11).
      Fixed tag struct sizes (clamp table @0x180047C60): 8,0x118,0x19C,0x14,0x10,
      0xC,0x2C,0x30,0x34,0x68,0x4C,0x58.
    Other types: opaque - the walker scans forward for the next plausible group
    header such that the remaining groups walk exactly to EOF (structural).

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
GROUP_NAMES = {0: "SFX", 1: "Sound(opaque)", 2: "MotionTag", 3: "type3",
               4: "type4", 5: "type5"}


def _u32(d: bytes, o: int) -> int:
    return struct.unpack_from("<I", d, o)[0]


def parse_header(d: bytes) -> dict:
    if len(d) < 0x130:
        raise ValueError("file shorter than 0x130-byte header")
    if _u32(d, 0) != GATA:
        raise ValueError("bad magic %#x (expected GATA)" % _u32(d, 0))
    path_end = d.find(b"\0", 8, 8 + 0x103)
    if path_end < 0:
        path_end = 8 + 0x103
    return {
        "magic": "GATA",
        "version": _u32(d, 4),
        "base_ani": d[8:path_end].decode("gb18030", "replace"),
        "group_count": _u32(d, 0x10C),
    }


def _header_ok(d: bytes, off: int) -> bool:
    if off < 0 or off + 12 > len(d):
        return False
    t, v, c = _u32(d, off), _u32(d, off + 4), _u32(d, off + 8)
    return 0 <= t <= 5 and 0 <= v <= 3 and 0 <= c <= 4096


def sfx_payload_size(d: bytes, off: int, v: int, c: int):
    p = off + 12
    if p + 4 > len(d):
        return None
    n1 = _u32(d, p)
    if n1 > 4096:
        return None
    return 4 + n1 * 0x130 + c * (0x1E8 + (4 if v == 3 else 0))


def motion_chain(d: bytes, off: int, v: int, c: int):
    """Parse the MotionTag payload at off (after the 12-byte group header).

    Returns (payload_size, chain) or (None, None) when the payload does not
    parse cleanly.
    """
    p = off + 12
    if v == 0:
        return c * 0x970, {"version": 0, "records": []}
    if v not in (1, 2):
        return None, None
    q = p
    n2 = 0
    if v == 2:
        if q + 4 > len(d):
            return None, None
        n2 = _u32(d, q)
        if n2 > 4096:
            return None, None
        q += 4 + n2 * 0x130
    if q + 8 > len(d):
        return None, None
    head0, head1 = _u32(d, q), _u32(d, q + 4)
    q += 8
    records = []
    for _ in range(c):
        if q + 0x188 > len(d):
            return None, None
        h = d[q:q + 0x100].split(b"\0")[0]
        time = _u32(d, q + 0x100)
        n = _u32(d, q + 0x104)
        if n > 64:
            return None, None
        sizes = [_u32(d, q + 0x108 + 4 * i) for i in range(n)]
        p2 = q + 0x188
        tags = []
        for s in sizes:
            if s <= 0 or p2 + s > len(d):
                return None, None
            tags.append({"type": _u32(d, p2), "size": s, "data": d[p2:p2 + s].hex()})
            p2 += s
        records.append({"offset": q, "hash": h.decode("ascii", "replace"),
                        "time": time, "tags": tags})
        q = p2
    return q - p, {"version": v, "n2": n2, "head0": head0, "head1": head1,
                   "records": records}


def known_payload_size(d: bytes, off: int, t: int, v: int, c: int):
    if t == 0:
        return sfx_payload_size(d, off, v, c)
    if t == 2:
        size, _ = motion_chain(d, off, v, c)
        return size
    return None


def _walk_ok(d: bytes, off: int, groups_left: int) -> bool:
    if groups_left == 0:
        return off == len(d)
    if not _header_ok(d, off):
        return False
    t, v, c = _u32(d, off), _u32(d, off + 4), _u32(d, off + 8)
    size = known_payload_size(d, off, t, v, c)
    if size is not None:
        return _walk_ok(d, off + 12 + size, groups_left - 1)
    for cand in range(off + 16, len(d) - 11, 4):
        if _walk_ok(d, cand, groups_left - 1):
            return True
    return False


def _scan_next(d: bytes, start: int, groups_left: int) -> int:
    for cand in range(start, len(d) - 11, 4):
        if _walk_ok(d, cand, groups_left):
            return cand
    return -1


def walk_groups(d: bytes, limit: int = 16):
    """Walk the group table. Returns (groups, motion_group, ok_to_eof)."""
    hdr = parse_header(d)
    groups = []
    motion = None
    off = 0x130
    for gi in range(min(hdr["group_count"], limit)):
        if not _header_ok(d, off):
            groups.append({"index": gi, "offset": off, "error": "bad header"})
            return groups, motion, False
        t, v, c = _u32(d, off), _u32(d, off + 4), _u32(d, off + 8)
        g = {"index": gi, "offset": off, "type": t, "type_name": GROUP_NAMES.get(t, "?"),
             "version": v, "count": c}
        size = known_payload_size(d, off, t, v, c)
        if size is None:
            cand = _scan_next(d, off + 16, hdr["group_count"] - gi - 1)
            if cand < 0:
                g["payload"] = "opaque"
                groups.append(g)
                return groups, motion, False
            g["payload"] = "opaque"
            g["next_scanned"] = cand
            groups.append(g)
            off = cand
            continue
        g["payload_size"] = size
        g["end"] = off + 12 + size
        if t == 2:
            _, chain = motion_chain(d, off, v, c)
            motion = {"offset": off, "version": v, "key_count": c, "chain": chain,
                      "end": g["end"]}
        groups.append(g)
        off = g["end"]
    return groups, motion, off == len(d)


def parse(path: str) -> dict:
    with open(path, "rb") as f:
        d = f.read()
    groups, motion, ok = walk_groups(d)
    return {"file": path, "size": len(d), "header": parse_header(d),
            "groups": groups, "motion": motion, "walk_to_eof": ok}


def dump_tsv(res: dict, fh) -> None:
    fh.write("key\toffset\ttime\thash\tntags\ttag_index\ttag_type\ttag_size\ttag_data\n")
    mb = res.get("motion")
    if not mb:
        return
    for ki, rec in enumerate(mb["chain"]["records"]):
        if not rec["tags"]:
            fh.write("%d\t%#x\t%d\t%s\t0\t\t\t\t\n"
                     % (ki, rec["offset"], rec["time"], rec["hash"]))
        for ti, tag in enumerate(rec["tags"]):
            fh.write("%d\t%#x\t%d\t%s\t%d\t%d\t%d\t%d\t%s\n"
                     % (ki, rec["offset"], rec["time"], rec["hash"],
                        len(rec["tags"]), ti, tag["type"], tag["size"], tag["data"]))


def _motion_group(v: int, keys):
    """keys = [(time, hash, [(tag_type, payload)])] -> type-2 group bytes."""
    body = struct.pack("<III", 2, v, len(keys))
    payload = struct.pack("<II", 0, 0)
    for time, h, tags in keys:
        rec = h.encode("ascii") + b"\0"
        rec = rec.ljust(0x100, b"\0")
        rec += struct.pack("<II", time, len(tags))
        rec += b"".join(struct.pack("<I", len(p)) for _, p in tags)
        rec = rec.ljust(0x188, b"\0")
        rec += b"".join(p for _, p in tags)
        payload += rec
    return body + payload


def _build_synthetic() -> bytes:
    """GATA container: SFX group + opaque type-1 group + MotionTag group (v1)."""
    hdr = struct.pack("<I", GATA) + struct.pack("<I", 1)
    hdr += b"data\\source\\player\\f1\\test.ani\0"
    hdr = hdr.ljust(0x10C, b"\0")
    hdr += struct.pack("<I", 3)
    hdr = hdr.ljust(0x130, b"\0")
    # group 0: SFX v1 count 2 -> u32 n1=1 + 1*0x130 + 2*(0x164+8*4+0x64)
    sfx = struct.pack("<III", 0, 1, 2)
    sfx += struct.pack("<I", 1) + b"\x11" * 0x130 + b"\x22" * (2 * 0x1E8)
    # group 1: opaque sound (type 1) - 0x40 bytes of fill
    snd = struct.pack("<III", 1, 2, 1) + b"\xAA" * 0x40
    # group 2: MotionTag v1, 2 keys, one type-0 tag (float 1.0) each
    mot = _motion_group(1, [(3, "TestTag", [(0, struct.pack("<If", 0, 1.0))]),
                            (9, "OtherTag", [(0, struct.pack("<If", 0, 1.0))])])
    return hdr + sfx + snd + mot


def selftest() -> int:
    checks = []
    d = _build_synthetic()
    h = parse_header(d)
    checks.append(("magic/version", h["magic"] == "GATA" and h["version"] == 1))
    checks.append(("base_ani", h["base_ani"] == "data\\source\\player\\f1\\test.ani"))
    checks.append(("group_count", h["group_count"] == 3))
    groups, motion, ok = walk_groups(d)
    checks.append(("groups walked", len(groups) == 3))
    checks.append(("group types", [g.get("type") for g in groups] == [0, 1, 2]))
    checks.append(("sfx exact", groups[0].get("payload_size") == 4 + 0x130 + 2 * 0x1E8))
    checks.append(("opaque scanned", groups[1].get("payload") == "opaque"
                   and groups[1].get("next_scanned") == groups[2]["offset"]))
    checks.append(("motion found", motion is not None and motion["key_count"] == 2))
    checks.append(("motion version", motion and motion["chain"]["version"] == 1))
    checks.append(("key times", motion and [r["time"] for r in motion["chain"]["records"]] == [3, 9]))
    checks.append(("hashes", motion and [r["hash"] for r in motion["chain"]["records"]] == ["TestTag", "OtherTag"]))
    checks.append(("tags", motion and all(len(r["tags"]) == 1 and r["tags"][0]["type"] == 0
                                          and r["tags"][0]["size"] == 8
                                          for r in motion["chain"]["records"])))
    checks.append(("walk to EOF", ok))
    try:
        parse_header(b"\0" * 0x130)
        checks.append(("bad magic rejected", False))
    except ValueError:
        checks.append(("bad magic rejected", True))
    bad = d[:-4]  # truncate the last motion tag payload
    _, m2, ok2 = walk_groups(bad)
    checks.append(("truncated motion rejected", m2 is None or not ok2))
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
    for g in res["groups"]:
        print("group[%d] off=%#x type=%d %s ver=%d count=%d %s"
              % (g["index"], g["offset"], g.get("type", -1), g.get("type_name", "?"),
                 g.get("version", -1), g.get("count", -1),
                 ("payload=%#x" % g["payload_size"]) if "payload_size" in g
                 else g.get("payload", "error")))
    mb = res["motion"]
    if mb:
        print("motion @%#x version=%d keys=%d end=%#x"
              % (mb["offset"], mb["version"], mb["key_count"], mb["end"]))
        for i, r in enumerate(mb["chain"]["records"]):
            print("  key %d time=%d hash=%r tags=%s"
                  % (i, r["time"], r["hash"],
                     [(t["type"], t["size"]) for t in r["tags"]]))
    else:
        print("no MotionTag group found (or walk incomplete)")
    print("walk_to_eof=%s" % res["walk_to_eof"])
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(res, f, indent=1, ensure_ascii=False)
    if args.tsv:
        with open(args.tsv, "w", encoding="utf-8", newline="") as f:
            dump_tsv(res, f)
    return 0


if __name__ == "__main__":
    sys.exit(main())
