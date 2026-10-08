#!/usr/bin/env python3
"""Extract the UI global string table (`ui/String/string.lua`) to TSV.

`ui/filepath.txt` names `UI\\Scheme\\Case\\String.txt` as the global valuable
table, but the Lua scripts read `g_tStrings`, which `ui/module_info.xml` binds
to the compiled lib `\\ui\\String\\string.lua` (and `g_tHotKey` to
`hotkeystring.lua`). Those bytecode chunks assign `ID = "text"` pairs; this
tool walks the Lua 5.1 instructions (`SETTABLE` with a constant key) and emits
the same `ID<TAB>Length<TAB>String` TSV as `ui/Scheme/Case/string.txt`, UTF-8.

Usage:
  .venv\\Scripts\\python.exe tools/ui/extract_lua_string_table.py \\
      <string.lua|hotkeystring.lua> -o ui-process-app/Data/text/ui/String/string.txt

The lib itself comes from PakV4 (read-only extraction):
  .venv\\Scripts\\python.exe tools/netcode/extract_pak_paths.py \\
      --list <paths.txt with ui/String/string.lua> --out-dir <dir>
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "netcode"))

import lua51_dump  # noqa: E402

LOADK = 1
SETTABLE = 9


def is_name(value: str) -> bool:
    return (
        len(value) > 2
        and value[0].isupper()
        and all(c.isupper() or c.isdigit() or c == "_" for c in value)
    )


def extract_pairs(proto: dict) -> list[tuple[str, str]]:
    consts = proto["consts"]
    regs: dict[int, int] = {}
    pairs: list[tuple[str, str]] = []
    for ins in proto["code"]:
        op = ins & 0x3F
        if op == LOADK:
            a = (ins >> 6) & 0xFF
            bx = ins >> 14
            regs[a] = bx
        elif op == SETTABLE:
            # SETTABLE A B C: R(A)[RK(B)] = RK(C) -> B is the key, C the value.
            key_rk = (ins >> 23) & 0x1FF
            value_rk = (ins >> 14) & 0x1FF

            def operand(rk: int):
                if rk & 0x100:
                    idx = rk & 0xFF
                    return consts[idx] if idx < len(consts) else None
                return consts[regs[rk]] if rk in regs and regs[rk] < len(consts) else None

            key, value = operand(key_rk), operand(value_rk)
            if isinstance(key, str) and isinstance(value, str) and is_name(key):
                pairs.append((key, value))
    for sub in proto["protos"]:
        pairs.extend(extract_pairs(sub))
    return pairs


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("lua", type=Path)
    ap.add_argument("-o", "--out", type=Path, required=True)
    args = ap.parse_args(argv)

    data = args.lua.read_bytes()
    if data[:4] != b"\x1bLua":
        print(f"not Lua bytecode: {data[:4]!r}")
        return 2
    version, fmt, little, int_size, size_t, instr_size, number_size = data[4:11]
    integral = data[11] if len(data) > 11 else 0
    reader = lua51_dump.Reader(data, little == 1, size_t, int_size, number_size, bool(integral))
    reader.o = 12
    proto = lua51_dump.read_proto(reader)

    pairs = extract_pairs(proto)
    seen: set[str] = set()
    rows: list[str] = []
    for key, value in pairs:
        if key in seen:
            continue
        seen.add(key)
        rows.append(f"{key}\t{len(value)}\t{value}".replace("\r", "").replace("\n", "\\n"))

    args.out.parent.mkdir(parents=True, exist_ok=True)
    with open(args.out, "w", encoding="utf-8", newline="\r\n") as fh:
        fh.write("ID\tLength\tString\n")
        fh.write("\n".join(rows) + "\n")
    print(f"{len(rows)} ids from {args.lua.name} -> {args.out}")
    return 0 if rows else 1


if __name__ == "__main__":
    raise SystemExit(main())
