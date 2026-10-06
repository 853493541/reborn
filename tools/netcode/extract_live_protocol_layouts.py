"""Batch-extract the live game S2C protocol layouts (JX3ClientX64.exe, 54 ids).

For each registered handler: disassemble it and collect every [rdx+N] packet field
access and the callee count -> proof/netcode/game_protocol_layouts_live.tsv.

This is the mass static extraction for the game server spec (no live runs).
"""
import re
import struct

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs

EXE = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe"
OUT = r"C:\Users\Zhibin Ren\Desktop\reborn-iso-v2\proof\netcode\game_protocol_layouts_live.tsv"

pe = pefile.PE(EXE, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.startswith(b".text")][0]
tdata = pe.__data__[text.PointerToRawData:text.PointerToRawData + text.SizeOfRawData]
tva = base + text.VirtualAddress
md = Cs(CS_ARCH_X86, CS_MODE_64)


def func_end(va):
    o = va - tva
    j = o
    while j < o + 0x3000 and tdata[j:j + 8] != b"\xcc" * 8:
        j += 1
    return tva + j


# parse the registrations (both sites, whole .text)
regs = {}
k = 0
n = len(tdata)
while k < n - 24:
    if tdata[k:k + 2] == b"\xc7\x87":
        sslot = struct.unpack_from("<i", tdata, k + 2)[0]
        if 0x17F00 <= sslot <= 0x18000:
            size = struct.unpack_from("<i", tdata, k + 6)[0]
            if tdata[k - 7:k - 4] == bytes.fromhex("488987") and tdata[k - 14:k - 11] == bytes.fromhex("488d05"):
                slot = struct.unpack_from("<i", tdata, k - 4)[0]
                disp = struct.unpack_from("<i", tdata, k - 11)[0]
                handler = tva + (k - 14) + 7 + disp
                if tva <= handler < tva + len(tdata) and 0x16460 <= slot <= 0x17000:
                    pid = (slot - 0x16468) // 8 + 1
                    regs[pid] = (handler, size)
                k += 10
                continue
    k += 1

rows = []
for pid in sorted(regs):
    h, size = regs[pid]
    fe = func_end(h)
    o = h - tva
    data = tdata[o:fe - tva]
    offs = []
    calls = 0
    for insn in md.disasm(data, h):
        if insn.mnemonic == "call":
            calls += 1
        m = re.search(r"\[rdx \+ (0x[0-9a-fA-F]+|\d+)\]", insn.op_str)
        if m:
            v = m.group(1)
            off = int(v, 16) if v.startswith("0x") else int(v)
            if off not in offs:
                offs.append(off)
    rows.append((pid, h, size, len(data), offs, calls))

with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write("# live game S2C protocol (JX3ClientX64.exe registration, %d ids)\n" % len(rows))
    f.write("# id\tsize\thandler_va\tcode_bytes\tpacket_offsets\tn_calls\n")
    for pid, h, size, cb, offs, calls in rows:
        f.write("%d\t%s\t0x%X\t%d\t%s\t%d\n" % (pid, size, h, cb,
              ",".join("0x%X" % x for x in offs), calls))
print("extracted", len(rows), "handlers ->", OUT)
for pid, h, size, cb, offs, calls in rows[:14]:
    print("id=%-3d size=%-5s fields=%s" % (pid, size, ",".join("0x%X" % x for x in offs[:8])))
