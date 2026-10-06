#!/usr/bin/env python3
"""Build a mini sandbox map: a cropped copy of a real map, loaded loose.

The client accepts an absolute map path in RC_MAP and resolves the map's
sibling files (landscape/, entities/, foliage/, env_probe/) through the same
path, so the sandbox is just a small loose map directory — no sandbox asset
root, no repacking, no junctions. All other assets (props, textures, actor
clips) keep loading from the normal client pak.

Read-only w.r.t. the game installs: every map file is pulled through the
official PakV4SfxExtract.exe into a work dir, then assembled under --out.

Crop model: region world position = WorldOrigin + index * RegionSize * UnitSize
(龙门寻宝: -102400 + i*51200, 8x8). Keeping regions (cx..cx+cw, cy..cy+ch),
renaming them to 0-based indices and moving WorldOrigin to
(-102400 + cx*51200, -102400 + cy*51200) preserves every world coordinate, so
spawn, object matrices, foliage and the baked collision bins stay valid.

Runtime heightfield: the renderer reads `landscape/heightmap/<name>_i_j.r32`
and the physics terrain loader reads `landscape/heightmap_bc/<name>_i_j.bch`;
both are required (dropping .bch breaks terrain sampling; dropping .r32 breaks
the terrain render). `landscape/blendmap_bc/*.r8` is an editor bake cache the
runtime does not read and is dropped by default (`--keep-bake-caches` keeps it).

Usage:
  python tools/sandbox/build_sandbox.py --map 龙门寻宝 --crop 2,2,2,2

Run the sandbox (feature exe name per AGENTS.md parallel-work rule):
  set RC_MAP=C:\\jx3tmp\\reborn_sandbox\\map\\龙门寻宝_mini\\龙门寻宝_mini.jsonmap
  set RC_CLIENT_EXE=reborn_client_mini.exe
  client\\build_client.cmd
  C:\\SeasunGame\\MovieEditor\\bin64\\reborn_client_mini.exe
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

CLIENT_ROOT = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd")
WORK_ROOT = Path(r"C:\jx3tmp\sandbox_work")
DEFAULT_OUT = Path(r"C:\jx3tmp\reborn_sandbox\map")

ENV_PROBE_FILES = [
    "skybox_bg.dds",
    "skybox_d.dds",
    "skybox_dshfactor.dds",
    "skybox_hd.dds",
    "skybox_s.dds",
]

REGION_FILES = [
    ("landscape\\heightmap\\{m}_{i:03d}_{j:03d}.r32", "landscape\\heightmap\\{m}_{i:03d}_{j:03d}.r32"),
    ("landscape\\heightmap_bc\\{m}_{i:03d}_{j:03d}.bch", "landscape\\heightmap_bc\\{m}_{i:03d}_{j:03d}.bch"),
    ("landscape\\regioninfo\\{m}_{i:03d}_{j:03d}.json", "landscape\\regioninfo\\{m}_{i:03d}_{j:03d}.json"),
    ("entities\\sceneinfo_full\\{i:03d}_{j:03d}.json", "entities\\sceneinfo_full\\{i:03d}_{j:03d}.json"),
    ("entities\\sceneinfo\\{i:03d}_{j:03d}.json", "entities\\sceneinfo\\{i:03d}_{j:03d}.json"),
    ("foliage\\foliageinfo\\{i:03d}_{j:03d}.foliage", "foliage\\foliageinfo\\{i:03d}_{j:03d}.foliage"),
]
for _k in range(8):
    REGION_FILES.append((
        "landscape\\blendmap\\{m}_{i:03d}_{j:03d}_%03d.png" % _k,
        "landscape\\blendmap\\{m}_{i:03d}_{j:03d}_%03d.png" % _k,
    ))
    REGION_FILES.append((
        "landscape\\blendmap_bc\\{m}_{i:03d}_{j:03d}_%03d.r8" % _k,
        "landscape\\blendmap_bc\\{m}_{i:03d}_{j:03d}_%03d.r8" % _k,
    ))
    REGION_FILES.append((
        "landscape\\procedural\\{m}_{i:03d}_{j:03d}_b%d.dds" % _k,
        "landscape\\procedural\\{m}_{i:03d}_{j:03d}_b%d.dds" % _k,
    ))


def _write_pathlist(path: Path, entries: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = "\r\n".join(entries) + "\r\n"
    path.write_bytes(text.encode("gb18030", errors="replace"))


def run_pakv4(entries: list[str], work: Path, extractor: Path) -> dict[str, bytes]:
    """Run the official extractor for ``entries``; return logical path -> bytes.

    Ported from the frozen root pss_assets.run_pakv4 (new code must not import
    the legacy root modules; tools keep their own copy).
    """
    if not extractor.is_file():
        raise FileNotFoundError("PakV4SfxExtract not found: %s" % extractor)
    if not entries:
        return {}
    list_path = work / "pathlist.txt"
    out_dir = work / "out"
    _write_pathlist(list_path, entries)
    if out_dir.exists():
        shutil.rmtree(out_dir, ignore_errors=True)
    out_dir.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [str(extractor), str(list_path), str(out_dir)],
        cwd=str(extractor.parent),
        capture_output=True,
        text=True,
        timeout=600,
    )
    found: dict[str, bytes] = {}
    for file in out_dir.rglob("*"):
        if file.is_file() and not file.name.startswith("_"):
            rel = file.relative_to(out_dir).as_posix()
            found[rel] = file.read_bytes()
    return found


def norm(path: str) -> str:
    return path.replace("\\", "/").lower()


def build_plan(m: str, nm: str, cx: int, cy: int, cw: int, ch: int) -> list[tuple[str, str]]:
    plan: list[tuple[str, str]] = []

    def add(logical: str, dest: str) -> None:
        plan.append((logical, dest))

    base = "data\\source\\maps\\" + m
    add(base + "\\" + m + ".jsonmap", nm + ".jsonmap")
    add(base + "\\" + m + "_Setting.ini", nm + "_Setting.ini")
    add(base + "\\" + m + ".SRScene", nm + ".SRScene")
    add(base + "\\" + m + ".rcidx", nm + ".rcidx")
    add(base + "\\environment.json", "environment.json")
    add(base + "\\playerEnvironment.json", "playerEnvironment.json")
    add(base + "\\systemCamera.json", "systemCamera.json")
    for q in ("bd", "low", "mb"):
        add(base + "\\" + q + "\\environment.json", q + "\\environment.json")
        add(base + "\\" + q + "\\playerEnvironment.json", q + "\\playerEnvironment.json")
        add(base + "\\" + q + "\\" + m + ".rcidx", q + "\\" + nm + ".rcidx")

    add(base + "\\landscape\\" + m + "_landscapeinfo.json", "landscape\\" + nm + "_landscapeinfo.json")
    add(base + "\\landscape\\" + m + "_materials.json", "landscape\\" + nm + "_materials.json")
    add(base + "\\landscape\\utillayers.json", "landscape\\utillayers.json")

    add(base + "\\entities\\" + m + "_sceneinfo.json", "entities\\" + nm + "_sceneinfo.json")
    add(base + "\\entities\\" + m + "_layerinfo.json", "entities\\" + nm + "_layerinfo.json")
    add(base + "\\foliage\\" + m + "_foliageinfo_editor.json", "foliage\\" + nm + "_foliageinfo_editor.json")
    add(base + "\\foliage\\" + m + "_layerinfo.json", "foliage\\" + nm + "_layerinfo.json")
    add(base + "\\foliage\\" + m + "_FoliageWorldObjectInfo.json", "foliage\\" + nm + "_FoliageWorldObjectInfo.json")

    for i in range(cx, cx + cw):
        for j in range(cy, cy + ch):
            fmt = {"m": m, "i": i, "j": j}
            dest_fmt = {"m": nm, "i": i - cx, "j": j - cy}
            for src_tpl, dst_tpl in REGION_FILES:
                add(base + "\\" + src_tpl.format(**fmt), dst_tpl.format(**dest_fmt))

    for f in ENV_PROBE_FILES:
        add(base + "\\env_probe\\" + f, "env_probe\\" + f)

    add(base + "\\water\\" + m + "_waterinfo.json", "water\\" + nm + "_waterinfo.json")
    add(base + "\\water\\wave.json", "water\\wave.json")
    add(base + "\\water\\regiondata\\RegionInfo.json", "water\\regiondata\\RegionInfo.json")
    return plan


def patch_map_json(path: Path, origin_x: float, origin_y: float, tw: int, th: int) -> None:
    obj = json.loads(path.read_text(encoding="utf-8"))
    obj["WorldOrigin.x"] = origin_x
    obj["WorldOrigin.y"] = origin_y
    obj["RegionTableSize.x"] = tw
    obj["RegionTableSize.y"] = th
    path.write_text(json.dumps(obj, indent="\t", ensure_ascii=False), encoding="utf-8")


def sha1(data: bytes) -> str:
    return hashlib.sha1(data).hexdigest()


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--map", default="龙门寻宝", help="source map name in the client pak")
    ap.add_argument("--name", default=None, help="sandbox map name (default: <map>_mini)")
    ap.add_argument("--crop", default="2,2,2,2", help="cx,cy,cw,ch in regions (default 2,2,2,2)")
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT, help="output dir (map bundle root)")
    ap.add_argument("--client-root", type=Path, default=CLIENT_ROOT)
    ap.add_argument("--work", type=Path, default=None)
    ap.add_argument("--keep-bake-caches", action="store_true",
                    help="keep landscape/blendmap_bc/*.r8 (editor bake cache; not read at runtime)")
    args = ap.parse_args(argv)

    cx, cy, cw, ch = (int(v) for v in args.crop.split(","))
    m = args.map
    nm = args.name or (m + "_mini")
    out: Path = args.out.resolve()
    map_dir = out / nm
    work = (args.work or (WORK_ROOT / nm)).resolve()
    client_root: Path = args.client_root.resolve()
    extractor = client_root / "bin64" / "PakV4SfxExtract.exe"
    pak_dir = client_root.parents[1] / "PakV4"
    if map_dir.exists():
        shutil.rmtree(map_dir)
    map_dir.mkdir(parents=True, exist_ok=True)

    plan = build_plan(m, nm, cx, cy, cw, ch)
    if not args.keep_bake_caches:
        # blendmap_bc/*.r8 is an editor bake cache; the runtime reads
        # blendmap/*.png. Verified: identical render and terrain without it.
        plan = [p for p in plan if "\\landscape\\blendmap_bc\\" not in p[0]]
    print("[%s] extracting %d candidate paths (crop %d,%d %dx%d%s)"
          % (nm, len(plan), cx, cy, cw, ch, ", keep-bake-caches" if args.keep_bake_caches else ""))
    found = run_pakv4([p[0] for p in plan], work, extractor)
    print("[%s] extractor returned %d files" % (nm, len(found)))
    by_norm = {norm(k): v for k, v in found.items()}

    files_meta = []
    misses = []
    for logical, dest in plan:
        data = by_norm.get(norm(logical))
        if data is None:
            misses.append(logical)
            continue
        target = map_dir / dest
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        files_meta.append({"path": dest.replace("\\", "/"), "bytes": len(data), "sha1": sha1(data)})

    origin_x = -102400.0 + cx * 51200.0
    origin_y = -102400.0 + cy * 51200.0
    sceneinfo = map_dir / "entities" / (nm + "_sceneinfo.json")
    landinfo = map_dir / "landscape" / (nm + "_landscapeinfo.json")
    if sceneinfo.is_file():
        patch_map_json(sceneinfo, origin_x, origin_y, cw, ch)
    if landinfo.is_file():
        patch_map_json(landinfo, origin_x, origin_y, cw, ch)
    print("[%s] map files: %d hit / %d miss; origin=(%.0f,%.0f) table=%dx%d"
          % (nm, len(files_meta), len(misses), origin_x, origin_y, cw, ch))

    manifest = {
        "map": m,
        "sandbox_map": nm,
        "crop": {"cx": cx, "cy": cy, "cw": cw, "ch": ch},
        "world_origin": [origin_x, origin_y],
        "client_root": str(client_root),
        "pak_dir": str(pak_dir),
        "extractor": str(extractor),
        "files": files_meta,
        "misses": misses,
    }
    (out / (nm + "_manifest.json")).write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")

    total = sum(f["bytes"] for f in files_meta)
    jsonmap = map_dir / (nm + ".jsonmap")
    print("[%s] done: %d files, %.1f MB -> %s" % (nm, len(files_meta), total / 1e6, map_dir))
    print("run:  set RC_MAP=%s" % jsonmap)
    if misses:
        print("[%s] misses (first 20):" % nm)
        for p in misses[:20]:
            print("   " + p)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
