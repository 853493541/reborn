# -*- coding: utf-8 -*-
"""Face data tooling for JX3 Reborn, system point 3.4 (character face pipeline).

Decodes the client's new-face save (CNDK container + Lua payload), validates it
against the shipped FaceLift clamp tables, and drives the shipped
FaceLiftDataConverterX64.exe (KMETAFACE) to produce the MetaFace JSON that the
engine consumes. All game installs are read-only; extraction goes to ignored dirs.

Formats (decoded 2026-10-06, see docs/character/3_4_FACIAL.md):
  CNDK   = magic "CNDK" | u32 crc32(payload) | u32 payloadSize | u32 payloadSize | payload
  payload= GB18030 Lua source: return {tBone={..187 int8..,[0]=..}, tDecal={..29..},
           tDecoration={..2..}, nRoleType, nVersion, nMajorVersion, bNewFace}

Usage:
  .venv\\Scripts\\python.exe tools\\character\\face_data.py selftest
  .venv\\Scripts\\python.exe tools\\character\\face_data.py parse <save.ini> [--json out.json]
  .venv\\Scripts\\python.exe tools\\character\\face_data.py validate <save.ini> <v2_tab_dir>
  .venv\\Scripts\\python.exe tools\\character\\face_data.py convert <save.ini> <out.json> [--converter path]
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SAVE_DIR = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\newfacedata")
DEFAULT_TAB_ROOT = ROOT / "proof" / "character" / "face_tab"
DEFAULT_CONVERTER = ROOT / "proof" / "character" / "fltool" / "FaceLiftDataConverterX64.exe"
ENGINE_DLL_DIR = Path(r"C:\SeasunGame\MovieEditor\bin64")
ROLE_NAMES = {1: "StandardMale", 2: "StandardFemale", 3: "StrongMale",
              4: "SexyFemale", 5: "LittleBoy", 6: "LittleGirl"}


def _u32(b: bytes) -> int:
    return struct.unpack("<I", b)[0]


def parse_cndk(path: Path) -> dict:
    """Parse a CNDK container (game save format). Returns dict with text/payload."""
    data = path.read_bytes()
    if data[:4] != b"CNDK":
        raise ValueError("not a CNDK container: %s" % path)
    crc = _u32(data[4:8])
    size_a = _u32(data[8:12])
    size_b = _u32(data[12:16])
    payload = data[16:]
    if not (len(payload) == size_a == size_b):
        raise ValueError("CNDK size mismatch: len=%d size=%d/%d" % (len(payload), size_a, size_b))
    got = zlib.crc32(payload) & 0xFFFFFFFF
    if got != crc:
        raise ValueError("CNDK crc mismatch: got 0x%08X want 0x%08X" % (got, crc))
    return {"path": str(path), "size": len(payload), "crc": crc,
            "text": payload.decode("gb18030")}


def _block(text: str, key: str) -> str:
    marker = key + "={"
    i = text.find(marker)
    if i < 0:
        raise ValueError("block not found: " + key)
    j = i + len(key) + 1
    depth = 0
    for k in range(j, len(text)):
        c = text[k]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return text[j + 1:k]
    raise ValueError("unterminated block: " + key)


def _top_items(block: str) -> list[str]:
    items, depth, cur = [], 0, []
    for ch in block:
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
        if ch == "," and depth == 0:
            items.append("".join(cur));
            cur = []
        else:
            cur.append(ch)
    tail = "".join(cur).strip()
    if tail:
        items.append(tail)
    return [it.strip() for it in items if it.strip()]


def parse_tbone(block: str) -> list[int]:
    """tBone = 187 int8, index i = KFACE_LIFT_BONE_TYPE_V2; [0] written last."""
    plain: list[int] = []
    keyed: dict[int, int] = {}
    for item in _top_items(block):
        m = re.match(r"^\[(\d+)\]\s*=\s*(-?\d+)$", item)
        if m:
            keyed[int(m.group(1))] = int(m.group(2))
        elif re.match(r"^-?\d+$", item):
            plain.append(int(item))
        else:
            raise ValueError("unexpected tBone item: " + item[:40])
    arr: list[int | None] = [None] * 187
    for i, v in enumerate(plain, start=1):  # Lua arrays are 1-based
        if i >= 187:
            raise ValueError("tBone plain values overflow: %d" % len(plain))
        arr[i] = v
    for i, v in keyed.items():
        if i >= 187:
            raise ValueError("tBone keyed index out of range: %d" % i)
        arr[i] = v
    missing = [i for i, v in enumerate(arr) if v is None]
    if missing:
        raise ValueError("tBone missing indices: %s" % missing[:8])
    for v in arr:
        if not (-128 <= v <= 127):
            raise ValueError("tBone value out of int8 range: %d" % v)
    return [int(v) for v in arr]


def parse_tdecal(block: str) -> list[dict]:
    out = []
    for item in _top_items(block):
        m = re.match(r"^\[(\d+)\]\s*=\s*(\{.*\})$", item)
        body = m.group(2) if m else item
        if not body.startswith("{"):
            raise ValueError("unexpected tDecal item: " + item[:40])
        rec = {}
        for mm in re.finditer(r"(\w+)\s*=\s*(-?\d+)(?![\d])", body):
            rec[mm.group(1)] = int(mm.group(2))
        out.append(rec)
    return out


def parse_newface(save: Path) -> dict:
    cnd = parse_cndk(save)
    text = cnd["text"]
    role = re.search(r"nRoleType\s*=\s*(\d+)", text)
    ver = re.search(r"nVersion\s*=\s*(\d+)", text)
    major = re.search(r"nMajorVersion\s*=\s*(\d+)", text)
    tbone = parse_tbone(_block(text, "tBone"))
    tdecal = parse_tdecal(_block(text, "tDecal"))
    tdec = parse_tdecal(_block(text, "tDecoration"))
    return {"role_type": int(role.group(1)) if role else 0,
            "role_name": ROLE_NAMES.get(int(role.group(1)) if role else 0, "?"),
            "version": int(ver.group(1)) if ver else 0,
            "major_version": int(major.group(1)) if major else 0,
            "tBone": tbone, "tDecal": tdecal, "tDecoration": tdec,
            "crc": cnd["crc"], "size": cnd["size"]}


def load_clamp_tab(path: Path) -> list[tuple[int, int]]:
    """KRL BinText TSV: header Type ValueMin ValueMax; rows are index 0..N-1."""
    rows = []
    for line in path.read_text(encoding="gb18030").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        cells = [c for c in re.split(r"[\t ]+", line) if c]
        if len(cells) >= 3 and re.match(r"^-?\d+$", cells[0]):
            rows.append((int(cells[1]), int(cells[2])))
    return rows


def validate(save: Path, tab_dir: Path) -> dict:
    face = parse_newface(save)
    tab = tab_dir / (face["role_name"] + ".tab")
    if not tab.exists():
        raise FileNotFoundError("clamp table not extracted: " + str(tab))
    clamp = load_clamp_tab(tab)
    if len(clamp) != 187:
        raise ValueError("clamp rows != 187: %d (%s)" % (len(clamp), tab))
    bad = [(i, v, clamp[i]) for i, v in enumerate(face["tBone"])
           if not (clamp[i][0] <= v <= clamp[i][1])]
    face["violations"] = bad
    return face


def convert(save: Path, out_path: Path, converter: Path | None = None) -> Path:
    exe = converter or DEFAULT_CONVERTER
    if not exe.exists():
        raise FileNotFoundError("converter not copied: " + str(exe))
    out_path = out_path.resolve()
    env = dict(os.environ)
    if ENGINE_DLL_DIR.exists():
        # the converter links the engine runtime DLLs (Engine_Lua5X64 etc.); load
        # them read-only from the install via PATH (never copied into the repo)
        env["PATH"] = str(ENGINE_DLL_DIR) + os.pathsep + env.get("PATH", "")
    with tempfile.TemporaryDirectory(prefix="face_conv_") as td:
        tmp_in = Path(td) / save.name
        shutil.copyfile(save, tmp_in)
        tmp_out = Path(td) / "out.ini"
        proc = subprocess.run([str(exe), str(tmp_in), str(tmp_out), "KMETAFACE"],
                              cwd=str(exe.parent), capture_output=True, env=env)
        if proc.returncode != 0 or not tmp_out.exists():
            raise RuntimeError("converter failed rc=%d out=%r" % (proc.returncode, proc.stdout[-200:]))
        data = json.loads(tmp_out.read_text(encoding="utf-8"))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    return out_path


def _selftest() -> int:
    failures = 0
    # 1) synthetic CNDK round-trip (no game data)
    bone = ["%d" % ((i % 21) - 10) for i in range(1, 187)] + ["[0]=-15"]
    payload = ("return {tDecoration={{nShowID=0,nColorID=0},[0]={nShowID=1,nColorID=2}},"
               "bNewFace=true,tBone={%s},"
               "tDecal={%s},nMajorVersion=1,nRoleType=6,nVersion=1}" % (
                   ",".join(bone),
                   ",".join("{fValue1=%d,nColorID=0,bUse=1,fValue3=0,nShowID=%d,fValue2=%d}" % (i, i, i)
                            for i in range(29))))
    raw = payload.encode("gb18030")
    blob = b"CNDK" + struct.pack("<III", zlib.crc32(raw) & 0xFFFFFFFF, len(raw), len(raw)) + raw
    with tempfile.TemporaryDirectory(prefix="face_st_") as td:
        p = Path(td) / "synthetic.ini"
        p.write_bytes(blob)
        face = parse_newface(p)
        ok = (len(face["tBone"]) == 187 and face["tBone"][0] == -15
              and len(face["tDecal"]) == 29 and len(face["tDecoration"]) == 2
              and face["role_type"] == 6)
        print("[1] synthetic CNDK round-trip: %s (bones=%d decals=%d deco=%d role=%d)" % (
            "PASS" if ok else "FAIL", len(face["tBone"]), len(face["tDecal"]),
            len(face["tDecoration"]), face["role_type"]))
        failures += 0 if ok else 1

    # 2) real saves (install present -> strict; else skipped)
    saves = sorted(DEFAULT_SAVE_DIR.glob("New_Face_*_Create.ini")) if DEFAULT_SAVE_DIR.exists() else []
    if not saves:
        print("[2] real saves: SKIP (no %s)" % DEFAULT_SAVE_DIR)
    for s in saves:
        try:
            face = parse_newface(s)
            v2 = DEFAULT_TAB_ROOT / "v2" / (face["role_name"] + ".tab")
            line = "[2] %s: bones=%d decals=%d role=%s(%d) crc=0x%08X" % (
                s.name, len(face["tBone"]), len(face["tDecal"]),
                face["role_name"], face["role_type"], face["crc"])
            if v2.exists():
                clamp = load_clamp_tab(v2)
                bad = [i for i, v in enumerate(face["tBone"])
                       if not (clamp[i][0] <= v <= clamp[i][1])]
                line += " v2-clamp-violations=%d" % len(bad)
                failures += 1 if bad else 0
            print(line)
        except Exception as exc:  # noqa: BLE001
            print("[2] %s: FAIL (%s)" % (s.name, exc))
            failures += 1

    # 3) V1 table sanity (49 rows for LittleGirl)
    v1 = DEFAULT_TAB_ROOT / "v1" / "LittleGirl.tab"
    if v1.exists():
        rows = load_clamp_tab(v1)
        ok = len(rows) == 49
        print("[3] V1 clamp table rows=%d: %s" % (len(rows), "PASS" if ok else "FAIL"))
        failures += 0 if ok else 1
    else:
        print("[3] V1 clamp table: SKIP (extract to %s)" % v1)

    # 4) converter round-trip on a real save (if copied)
    if DEFAULT_CONVERTER.exists() and saves:
        try:
            with tempfile.TemporaryDirectory(prefix="face_out_") as td:
                out = convert(saves[0], Path(td) / "metaface.json")
                data = json.loads(out.read_text(encoding="utf-8"))
                keys = ("BodyType", "Bone", "Decal", "FacePart")
                ok = all(k in data for k in keys)
                print("[4] converter KMETAFACE: %s (keys=%s bone=%d decal=%d)" % (
                    "PASS" if ok else "FAIL", sorted(data.keys()),
                    len(data.get("Bone", {})), len(data.get("Decal", {}))))
                failures += 0 if ok else 1
        except Exception as exc:  # noqa: BLE001
            print("[4] converter: FAIL (%s)" % exc)
            failures += 1
    else:
        print("[4] converter: SKIP (copy FaceLiftDataConverterX64.exe to %s)" % DEFAULT_CONVERTER)

    print("SELFTEST %s (%d failures)" % ("PASS" if failures == 0 else "FAIL", failures))
    return 0 if failures == 0 else 1


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("selftest")
    p_parse = sub.add_parser("parse")
    p_parse.add_argument("save", type=Path)
    p_parse.add_argument("--json", type=Path, default=None)
    p_val = sub.add_parser("validate")
    p_val.add_argument("save", type=Path)
    p_val.add_argument("tab_dir", type=Path, nargs="?", default=DEFAULT_TAB_ROOT / "v2")
    p_conv = sub.add_parser("convert")
    p_conv.add_argument("save", type=Path)
    p_conv.add_argument("out", type=Path)
    p_conv.add_argument("--converter", type=Path, default=None)
    args = ap.parse_args(argv)

    if args.cmd == "selftest":
        return _selftest()
    if args.cmd == "parse":
        face = parse_newface(args.save)
        if args.json:
            args.json.write_text(json.dumps(face, ensure_ascii=False, indent=1), encoding="utf-8")
        print("%s role=%s(%d) v%d bones=%d decals=%d deco=%d crc=0x%08X" % (
            args.save.name, face["role_name"], face["role_type"], face["version"],
            len(face["tBone"]), len(face["tDecal"]), len(face["tDecoration"]), face["crc"]))
        return 0
    if args.cmd == "validate":
        face = validate(args.save, args.tab_dir)
        print("violations=%d bones=%d" % (len(face["violations"]), len(face["tBone"])))
        return 0 if not face["violations"] else 1
    if args.cmd == "convert":
        out = convert(args.save, args.out, args.converter)
        data = json.loads(out.read_text(encoding="utf-8"))
        print("wrote %s (keys=%s)" % (out, sorted(data.keys())))
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main())
