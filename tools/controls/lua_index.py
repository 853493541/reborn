#!/usr/bin/env python3
"""Batch proto index for packed JX3 UI Lua scripts (Lua 5.1, LuaQ, size_t=4).

Walks every .lua under a directory, lists each prototype with a best-effort
name (from the parent's CLOSURE + SETGLOBAL/SETTABLE pattern), line range,
params/upvalues, code size, string constants and globals used. This is the
P1 layer substrate of docs/controls/CONTROL_MODES_TRACEABILITY.md (map every
handler before decompiling bodies).

Usage:
  .venv\\Scripts\\python.exe tools/controls/lua_index.py <dir> --out proof/controls/lua_index_control.txt
  .venv\\Scripts\\python.exe tools/controls/lua_index.py <dir> --find ResponseWASDKey
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "netcode"))
from lua51_dump import Reader, read_proto, OPCODES  # noqa: E402

G = OPCODES.index("GETGLOBAL")
SG = OPCODES.index("SETGLOBAL")
ST = OPCODES.index("SETTABLE")
CL = OPCODES.index("CLOSURE")


def bxx(ins: int) -> int:
    return (ins >> 14) & 0x3FFFF


def abc(ins: int):
    return (ins >> 6) & 0xFF, (ins >> 23) & 0x1FF, (ins >> 14) & 0x1FF


def child_names(p: dict) -> dict:
    """Best-effort child proto index -> assigned name (global or field)."""
    names: dict = {}
    code = p["code"]
    consts = p["consts"]
    for pc, ins in enumerate(code):
        if (ins & 0x3F) != CL:
            continue
        child = bxx(ins)
        dest = (ins >> 6) & 0xFF
        for nxt in code[pc + 1: pc + 3]:
            op = nxt & 0x3F
            if op == SG and bxx(nxt) < len(consts) and isinstance(consts[bxx(nxt)], str):
                names[child] = consts[bxx(nxt)]
                break
            if op == ST:
                _, _b, c = abc(nxt)
                if (c & 0x100) and (c & 0xFF) < len(consts) and isinstance(consts[c & 0xFF], str):
                    names[child] = consts[c & 0xFF]
                    break
                _, b, _c = abc(nxt)
                if (b & 0x100) and (b & 0xFF) < len(consts) and isinstance(consts[b & 0xFF], str):
                    names[child] = consts[b & 0xFF]
                    break
            # skip over the A=dest bookkeeping ops without losing the pair
            if op not in (1, 2, 3):  # LOADK/LOADBOOL/LOADNIL between CLOSURE and SET
                pass
    return names


def walk(p: dict, path: str, out: list, find: str, hits: list) -> None:
    strings = [c for c in p["consts"] if isinstance(c, str)]
    numbers = [c for c in p["consts"] if isinstance(c, (int, float)) and not isinstance(c, bool)]
    globals_used = []
    for ins in p["code"]:
        op = ins & 0x3F
        if op in (G, SG):
            i = bxx(ins)
            if i < len(p["consts"]) and isinstance(p["consts"][i], str):
                globals_used.append(p["consts"][i])
    name = None
    if find:
        if find in (p.get("_name") or "") or any(find in s for s in strings):
            hits.append(f"{p.get('_file')} {path} {p.get('_name') or '?'} L{p['line_defined']}-{p['last_line']}")
    out.append(
        "\t".join(
            [
                p.get("_file", "?"),
                path,
                p.get("_name") or "?",
                f"L{p['line_defined']}-{p['last_line']}",
                f"params={p['numparams']}",
                f"ups={p['nups']}",
                f"code={len(p['code'])}",
                "strs=" + "|".join(s[:40] for s in strings[:12]),
                "nums=" + "|".join(str(round(n, 4)) for n in numbers[:12]),
                "globals=" + "|".join(sorted(set(globals_used))[:16]),
            ]
        )
    )
    subs = child_names(p)
    for i, sub in enumerate(p["protos"]):
        sub["_file"] = p.get("_file")
        sub["_name"] = subs.get(i)
        walk(sub, f"{path}/{i}", out, find, hits)


def index_file(path: Path, out: list, find: str, hits: list) -> None:
    data = path.read_bytes()
    if data[:4] != b"\x1bLua":
        return
    r = Reader(data, little=data[6] == 1, size_t=data[8], int_size=data[7], number_size=data[10])
    r.take(12)
    proto = read_proto(r)
    proto["_file"] = path.name
    proto["_name"] = path.stem
    walk(proto, "0", out, find, hits)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("dir", type=Path)
    ap.add_argument("--out", type=Path)
    ap.add_argument("--find", default="")
    ap.add_argument("--glob", default="*.lua")
    args = ap.parse_args()

    files = sorted(args.dir.rglob(args.glob))
    out: list = []
    hits: list = []
    counts = {"files": 0, "protos": 0}
    for f in files:
        before = len(out)
        index_file(f, out, args.find, hits)
        if len(out) > before:
            counts["files"] += 1
        counts["protos"] += len(out) - before

    header = f"# lua_index {args.dir} files={counts['files']} protos={counts['protos']}\n"
    table = "file\tproto\tname\tlines\tparams\tups\tcode\tstrings\tnums\tglobals\n"
    text = header + (("\n".join("HIT " + h for h in hits) + "\n\n") if args.find else "") + table + "\n".join(out) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
        print(f"{counts['protos']} protos from {counts['files']} files -> {args.out}")
        if args.find:
            print(f"{len(hits)} hits for {args.find!r}")
    else:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
