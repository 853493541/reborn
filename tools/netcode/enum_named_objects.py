#!/usr/bin/env python3
"""List named objects in the session / global object namespace.

Read-only observation used to find launcher-created named objects (mutexes, events,
sections) that a hosted game client may wait for. Prints matching names; default filter
covers jx3/seasun/game/launch/xgame/kg keywords.

Usage:
  python tools/netcode/enum_named_objects.py                 # filtered
  python tools/netcode/enum_named_objects.py --all           # everything
  python tools/netcode/enum_named_objects.py --filter ""     # no filter
"""
from __future__ import annotations

import argparse
import ctypes
import ctypes.wintypes as w

ntdll = ctypes.windll.ntdll
kernel32 = ctypes.windll.kernel32

DIRECTORY_QUERY = 0x0001
STATUS_NO_MORE_ENTRIES = 0x8000001A
OBJ_CASE_INSENSITIVE = 0x00000040


class UNICODE_STRING(ctypes.Structure):
    _fields_ = [("Length", ctypes.c_ushort),
                ("MaximumLength", ctypes.c_ushort),
                ("Buffer", ctypes.c_void_p)]


class OBJECT_ATTRIBUTES(ctypes.Structure):
    _fields_ = [("Length", ctypes.c_ulong),
                ("RootDirectory", ctypes.c_void_p),
                ("ObjectName", ctypes.POINTER(UNICODE_STRING)),
                ("Attributes", ctypes.c_ulong),
                ("SecurityDescriptor", ctypes.c_void_p),
                ("SecurityQualityOfService", ctypes.c_void_p)]


class OBJECT_DIRECTORY_INFORMATION(ctypes.Structure):
    _fields_ = [("Name", UNICODE_STRING),
                ("TypeName", UNICODE_STRING)]


def ustr(text):
    us = UNICODE_STRING()
    us.Buffer = ctypes.cast(ctypes.create_unicode_buffer(text), ctypes.c_void_p)
    us.Length = len(text) * 2
    us.MaximumLength = us.Length + 2
    return us, ctypes.cast(us.Buffer, ctypes.c_void_p)


def list_dir(path, limit=20000):
    buf_path = ctypes.create_unicode_buffer(path)
    us = UNICODE_STRING()
    us.Buffer = ctypes.cast(buf_path, ctypes.c_void_p)
    us.Length = len(path) * 2
    us.MaximumLength = us.Length + 2
    oa = OBJECT_ATTRIBUTES()
    oa.Length = ctypes.sizeof(OBJECT_ATTRIBUTES)
    oa.ObjectName = ctypes.pointer(us)
    oa.Attributes = OBJ_CASE_INSENSITIVE
    h = w.HANDLE()
    st = ntdll.NtOpenDirectoryObject(ctypes.byref(h), DIRECTORY_QUERY, ctypes.byref(oa))
    if st != 0:
        return [], st
    names = []
    ctx = ctypes.c_void_p(0)
    ret = ctypes.c_ulong(0)
    buflen = 0x10000
    buf = ctypes.create_string_buffer(buflen)
    while len(names) < limit:
        st = ntdll.NtQueryDirectoryObject(h, buf, buflen, True, False, ctypes.byref(ctx), ctypes.byref(ret))
        if st == STATUS_NO_MORE_ENTRIES:
            break
        if st != 0:
            break
        info = ctypes.cast(buf, ctypes.POINTER(OBJECT_DIRECTORY_INFORMATION)).contents
        if info.Name.Buffer:
            n = ctypes.wstring_at(info.Name.Buffer, info.Name.Length // 2)
            t = ctypes.wstring_at(info.TypeName.Buffer, info.TypeName.Length // 2) if info.TypeName.Buffer else ""
            names.append((n, t))
    kernel32.CloseHandle(h)
    return names, 0


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--all", action="store_true", help="print every object")
    ap.add_argument("--filter", default="jx3|seasun|game|launch|xgame|xlauncher|kg|dotnot",
                    help="case-insensitive regex filter (default: game-ish keywords)")
    args = ap.parse_args()

    import re
    pat = re.compile(args.filter, re.I) if args.filter and not args.all else None

    sid = w.DWORD()
    kernel32.ProcessIdToSessionId(kernel32.GetCurrentProcessId(), ctypes.byref(sid))
    paths = [r"\BaseNamedObjects", r"\Sessions\%d\BaseNamedObjects" % sid.value]

    total = 0
    for p in paths:
        names, st = list_dir(p)
        if st != 0:
            print("== %s: open failed status=0x%X" % (p, st & 0xFFFFFFFF))
            continue
        print("== %s: %d objects" % (p, len(names)))
        for n, t in names:
            total += 1
            if pat is None or pat.search(n):
                print("  [%s] %s" % (t, n))
    print("total objects: %d" % total)


if __name__ == "__main__":
    main()
