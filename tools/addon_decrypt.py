"""Decrypt JX3 addon Lua files (static, no game process).

The client's addon loader (KGUIX64.dll) uses a custom block cipher:
  0x180231230  key setup (rcx = ctx, rdx = 16-byte key)
  0x180231ca0  block op  (rcx = 16-byte data, rdx = ctx), in place

Wrapper at 0x1801ae1c0: for the whole file (including the first 4 bytes, which
are the encrypted Lua header), for each 16-byte block C_i:
    P_i = op(C_i) XOR C_{i-1},   C_{-1} = IV
Hardcoded key/IV (0x1801ae208..0x1801ae246):
    key = b"lKT#tOXyaC8lNP!Z"
    IV  = b"SWocY*IW5Hw!sBxL"

Usage:
  .venv\\Scripts\\python.exe tools/addon_decrypt.py <file.lua> [more.lua ...]
Writes <file>.dec.lua next to each input.
"""
import ctypes
import os
import sys
from pathlib import Path

BIN = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64")
KEY = b"lKT#tOXyaC8lNP!Z"
IV = b"SWocY*IW5Hw!sBxL"


def load_cipher():
    os.add_dll_directory(str(BIN))
    ctypes.WinDLL(str(BIN / "KGUIX64.dll"))
    k32 = ctypes.WinDLL("kernel32")
    k32.GetModuleHandleW.restype = ctypes.c_void_p
    k32.GetModuleHandleW.argtypes = [ctypes.c_wchar_p]
    base = k32.GetModuleHandleW("KGUIX64.dll")
    setup = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_void_p)(base + 0x231230)
    block = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.c_void_p)(base + 0x231ca0)
    return setup, block


SETUP, BLOCK = load_cipher()


def decrypt(data: bytes) -> bytes:
    ctx = ctypes.create_string_buffer(0x200)
    key = ctypes.create_string_buffer(KEY, 16)
    SETUP(ctypes.cast(ctx, ctypes.c_void_p), ctypes.cast(key, ctypes.c_void_p))
    prev = IV
    out = bytearray()
    n = len(data) // 16
    for i in range(n):
        c = data[i * 16 : (i + 1) * 16]
        buf = ctypes.create_string_buffer(c, 16)
        BLOCK(ctypes.cast(buf, ctypes.c_void_p), ctypes.cast(ctx, ctypes.c_void_p))
        out += bytes(a ^ b for a, b in zip(buf.raw[:16], prev))
        prev = c
    out += data[n * 16 :]
    return bytes(out)


def main(argv):
    if not argv:
        print(__doc__)
        return 1
    for arg in argv:
        p = Path(arg)
        data = p.read_bytes()
        out = decrypt(data)
        dst = p.with_suffix(".dec.lua")
        dst.write_bytes(out)
        ok = out[:4] == b"\x1bLua"
        print(f"{p.name}: {len(data)} -> {len(out)}  lua={ok}  {dst}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
