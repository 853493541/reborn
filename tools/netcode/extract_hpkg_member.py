#!/usr/bin/env python3
"""Extract members from a JX3 CDN ``.hpkg`` package (LZHAM index + members).

Header: magic 0x9585, version 102; count @0x10, indexSize @0x20,
packedIndexSize @0x28, payloadSize @0x30. The packed index starts at 64 with a
4-byte size prefix; member payloads start at ``64 + packedIndexSize``.
Record (``indexSize / count`` bytes): path cstr @+4 (<= 256), originalSize
@+280, storedSize @+284, payloadOffset @+288, flags @+292.

Members are either LZHAM blocks (try skip 0/4/.../48) or raw with a
``storedSize - originalSize`` byte header (0..64). See
``docs/MAP_MINIMAP_RESEARCH.md`` §6 for the pitfall this handles.

Usage:
  python tools/netcode/extract_hpkg_member.py <file.hpkg> --list
  python tools/netcode/extract_hpkg_member.py <file.hpkg> --match loading --out-dir out
"""
from __future__ import annotations

import argparse
import ctypes
import json
import struct
import sys
from pathlib import Path

LZHAM_DLL = Path(
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\seasun"
    r"\editortool\qseasuneditor\seasunapp\httppacking\lzham_x64.dll"
)
HEADER_MAGIC = 0x9585
HEADER_VERSION = 102
PACKED_START = 64
RECORD_PATH_OFF = 4
RECORD_PATH_MAX = 260
RECORD_ORIGINAL_OFF = 280
RECORD_STORED_OFF = 284
RECORD_PAYLOAD_OFF = 288
RECORD_FLAGS_OFF = 292


def _load_lzham() -> ctypes.CDLL:
    lib = ctypes.CDLL(str(LZHAM_DLL))
    fn = lib.lzham_z_uncompress
    fn.restype = ctypes.c_int
    fn.argtypes = [
        ctypes.c_void_p,
        ctypes.POINTER(ctypes.c_uint32),
        ctypes.c_void_p,
        ctypes.c_uint32,
    ]
    return lib


def _lzham_uncompress(lib: ctypes.CDLL, src: bytes, out_size: int) -> bytes | None:
    if not src or out_size <= 0:
        return None
    dest = ctypes.create_string_buffer(out_size)
    dest_len = ctypes.c_uint32(out_size)
    src_buf = ctypes.create_string_buffer(src, len(src))
    status = lib.lzham_z_uncompress(dest, ctypes.byref(dest_len), src_buf, len(src))
    if status != 0 or dest_len.value != out_size:
        return None
    return dest.raw[:out_size]


def _read_cstr(data: bytes, start: int, end: int) -> str:
    stop = start
    while stop < end and data[stop] != 0:
        stop += 1
    raw = data[start:stop]
    for encoding in ("utf-8", "gb18030"):
        try:
            text = raw.decode(encoding)
        except UnicodeDecodeError:
            continue
        if "\ufffd" not in text:
            return text
    return raw.decode("gb18030", errors="replace")


class Hpkg:
    def __init__(self, path: Path) -> None:
        self.path = path
        self.data = path.read_bytes()
        magic, version = struct.unpack_from("<II", self.data, 0)
        if magic != HEADER_MAGIC or version != HEADER_VERSION:
            raise ValueError(
                f"unexpected HPKG header: magic=0x{magic:x} version={version}"
            )
        self.count = struct.unpack_from("<I", self.data, 0x10)[0]
        self.index_size = struct.unpack_from("<I", self.data, 0x20)[0]
        self.packed_index_size = struct.unpack_from("<I", self.data, 0x28)[0]
        self.payload_size = struct.unpack_from("<I", self.data, 0x30)[0]
        if not self.count or self.index_size % self.count:
            raise ValueError(
                f"invalid index sizing: count={self.count} indexSize={self.index_size}"
            )
        self.payload_start = PACKED_START + self.packed_index_size
        self.record_size = self.index_size // self.count
        self.lib = _load_lzham()
        packed = self.data[PACKED_START + 4 : self.payload_start]
        index = _lzham_uncompress(self.lib, packed, self.index_size)
        if index is None:
            raise ValueError("HPKG index LZHAM decode failed")
        self.index = index
        self.records = self._parse_records()

    def _parse_records(self) -> list[dict]:
        records = []
        for i in range(self.count):
            base = i * self.record_size
            records.append(
                {
                    "index": struct.unpack_from("<I", self.index, base)[0],
                    "path": _read_cstr(
                        self.index,
                        base + RECORD_PATH_OFF,
                        base + RECORD_PATH_MAX,
                    ),
                    "originalSize": struct.unpack_from(
                        "<I", self.index, base + RECORD_ORIGINAL_OFF
                    )[0],
                    "storedSize": struct.unpack_from(
                        "<I", self.index, base + RECORD_STORED_OFF
                    )[0],
                    "payloadOffset": struct.unpack_from(
                        "<I", self.index, base + RECORD_PAYLOAD_OFF
                    )[0],
                    "flags": struct.unpack_from(
                        "<I", self.index, base + RECORD_FLAGS_OFF
                    )[0],
                }
            )
        return records

    def extract(self, record: dict) -> tuple[bytes, str]:
        start = self.payload_start + record["payloadOffset"]
        stored = self.data[start : start + record["storedSize"]]
        if len(stored) != record["storedSize"]:
            raise ValueError(f"record payload beyond file: {record['path']}")
        original = record["originalSize"]
        for skip in range(0, min(64, len(stored)) + 1, 4):
            decoded = _lzham_uncompress(self.lib, stored[skip:], original)
            if decoded is not None:
                return decoded, f"lzham+{skip}"
        header = record["storedSize"] - original
        if 0 <= header <= 64:
            raw = stored[header : header + original]
            if len(raw) == original:
                return raw, f"raw+{header}"
        raise ValueError(f"member decode failed: {record['path']}")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("hpkg", type=Path)
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--match", default="", help="case-insensitive path substring")
    ap.add_argument("--exact", default="", help="exact path match")
    ap.add_argument("--out-dir", type=Path, default=None)
    ap.add_argument("--report", type=Path, default=None)
    args = ap.parse_args(argv)

    pkg = Hpkg(args.hpkg)
    needle = args.match.lower()
    matches = [
        r
        for r in pkg.records
        if (not args.exact and (not needle or needle in r["path"].lower()))
        or (args.exact and r["path"].lower() == args.exact.lower())
    ]
    if args.list or not args.out_dir:
        print(
            json.dumps(
                {
                    "hpkg": str(pkg.path),
                    "count": pkg.count,
                    "indexSize": pkg.index_size,
                    "packedIndexSize": pkg.packed_index_size,
                    "payloadStart": pkg.payload_start,
                    "recordSize": pkg.record_size,
                    "matches": matches,
                },
                ensure_ascii=False,
                indent=2,
            )
        )
        return 0

    args.out_dir.mkdir(parents=True, exist_ok=True)
    report = []
    for record in matches:
        try:
            data, method = pkg.extract(record)
        except ValueError as exc:
            print(f"FAIL {record['path']}: {exc}")
            continue
        name = Path(record["path"].replace("\\", "/")).name
        dest = args.out_dir / name
        dest.write_bytes(data)
        head = data[:16].hex()
        report.append(
            {
                "path": record["path"],
                "file": name,
                "size": len(data),
                "method": method,
                "head": head,
            }
        )
        print(f"HIT  {record['path']} -> {dest} ({len(data)} bytes, {method})")
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(
            json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
        )
    print(f"{len(report)} members extracted from {len(matches)} matches")
    return 0 if report else 1


if __name__ == "__main__":
    raise SystemExit(main())
