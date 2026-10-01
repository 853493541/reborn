"""Export per-mesh obstacle flags for the baked collision bins (plan P0).

Every shipped mesh has a sibling `<mesh>.ini` property file read by
`KG3DMeshFileDataLoader::_LoadMeshProperty` (KG3DEngineX64.dll); the obstacle
keys are:

  [Display]        bAutoProduceObstacle   (mesh-level; engine default 1)
  [DisplaySub-*]   bLogicObstacle         (per LOD submesh; engine default 1)
  [DisplaySub-*]   bCollisionOnly         (per LOD submesh; engine default 0)

Consumers located in the client install: KG3DEngineDX11EX64.dll and
KG3D_LoaderNoRenderX64.dll (strings + property getters).

Input:  a baked structure bin plus its `<bin>.meshes.txt` sidecar
        (mesh index -> source model path), as written by
        tools/export_structure_collision.py.
Output: `<bin>.oflags` sidecar, aligned to the mesh order:
        u32 magic 'OFLG' (0x474C464F), u32 count, one byte per mesh:
          bit0  bAutoProduceObstacle == 1
          bit1  any LOD0 submesh bLogicObstacle == 1
          bit2  all LOD0 submeshes bLogicObstacle == 1
          bit3  any LOD0 submesh bCollisionOnly == 1
          bit4  no ini found (engine defaults used)

Usage:
  python tools/export_obstacle_flags.py \
      --bin engine_host_spike/collision_data/龙门寻宝_structure_collision.bin
  python tools/export_obstacle_flags.py --bin <bin> \
      --copy-to C:/SeasunGame/MovieEditor/bin64/collision_data
"""
from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

import pss_assets  # noqa: E402


def parse_ini(text: str) -> dict:
    d = {}
    sec = ""
    for ln in text.splitlines():
        ln = ln.strip()
        if not ln or ln.startswith(";"):
            continue
        if ln.startswith("["):
            sec = ln.strip("[]")
            continue
        if "=" in ln:
            k, v = ln.split("=", 1)
            d[(sec, k.strip())] = v.strip()
    return d


def ini_candidates(model: str) -> list[str]:
    low = model.lower()
    for ext in (".collisionmesh", ".mesh", ".srt"):
        if low.endswith(ext):
            base = model[: -len(ext)]
            return [base + ".ini", model + ".ini"]
    return [model + ".ini"]


def collision_siblings(model: str) -> list[str]:
    low = model.lower()
    if low.endswith(".mesh"):
        base = model[: -len(".mesh")]
    elif low.endswith(".srt"):
        base = model[: -len(".srt")]
    else:
        base = model
    return [
        base + ".CollisionMesh",
        base + "_proxymesh.mesh",
        base + ".proxymesh",
        base + "_proxy.mesh",
        base + ".collisionmesh",
    ]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", required=True, help="path to the structure .bin")
    ap.add_argument("--copy-to", default=None, help="also write the sidecar here")
    args = ap.parse_args()

    bin_path = Path(args.bin)
    side = Path(str(bin_path) + ".meshes.txt")
    if not side.is_file():
        raise SystemExit("missing sidecar: %s" % side)
    models = {}
    for ln in side.read_text(encoding="utf-8", errors="replace").replace("\r", "").split("\n"):
        ln = ln.strip()
        if not ln:
            continue
        parts = ln.split(None, 1)
        if len(parts) != 2:
            continue
        models[int(parts[0])] = parts[1].strip()
    count = (max(models) + 1) if models else 0
    print("meshes: %d" % count)

    # fetch inis
    req = []
    for i in range(count):
        m = models.get(i)
        if m:
            req.extend(ini_candidates(m))
    req = sorted(set(req))
    got = pss_assets.run_pakv4(
        req, work=Path(r"C:\Users\Zhibin Ren\AppData\Local\Temp\opencode\oflags_work"))
    ini_map = {}
    for k, v in got.items():
        ini_map[k.replace("/", "\\").lower()] = v.decode("utf-8", "replace")

    flags = bytearray(count)
    auto0 = []
    for i in range(count):
        m = models.get(i)
        if not m:
            continue
        low = m.replace("/", "\\").lower()
        # the engine reads TWO files: `_LoadDotIni` (mesh-level [Display]) and
        # `_LoadDotMeshDotIni` (per-submesh [DisplaySub-*]). Both `<base>.ini`
        # (a partial/stub) and `<base>.mesh.ini` (full) ship for many meshes -
        # merging all candidates is what the engine does (field bug 2026-10-01:
        # first-candidate-wins lost every DisplaySub key -> logic bits always 0).
        d = {}
        for cand in ini_candidates(m):
            t = ini_map.get(cand.replace("/", "\\").lower())
            if t is not None:
                d.update(parse_ini(t))
        if not d:
            flags[i] = 0x01 | 0x02 | 0x04 | 0x10   # defaults: auto=1, logic=1
            continue
        auto = d.get(("Display", "bAutoProduceObstacle"), "1")
        subs = [k for k in d if k[0].startswith("DisplaySub-Lod0") or k[0].startswith("DisplaySub-Lod1")]
        logic = [d[k] for k in subs if k[1] == "bLogicObstacle"]
        co = [d[k] for k in subs if k[1] == "bCollisionOnly"]
        b = 0
        if auto != "0":
            b |= 0x01
        if logic and all(x == "1" for x in logic):
            b |= 0x02 | 0x04
        elif any(x == "1" for x in logic):
            b |= 0x02
        if any(x == "1" for x in co):
            b |= 0x08
        flags[i] = b
        if auto == "0":
            auto0.append(m)

    # meshes with bAutoProduceObstacle=0 but an AUTHORED collision sibling
    # (proxymesh / CollisionMesh) still receive physics in the engine
    # (file-selection chain falls back to it): mark bit5 so the runtime only
    # skips auto=0 meshes that have NO authored collision.
    if auto0:
        sib = pss_assets.run_pakv4(
            sorted({s for m in auto0 for s in collision_siblings(m)}),
            work=Path(r"C:\Users\Zhibin Ren\AppData\Local\Temp\opencode\oflags_sib"))
        sibl = {k.replace("/", "\\").lower() for k in sib}
        for i in range(count):
            m = models.get(i)
            if not m or (flags[i] & 0x01):
                continue
            if any(s.replace("/", "\\").lower() in sibl for s in collision_siblings(m)):
                flags[i] |= 0x20
    out = struct.pack("<II", 0x474C464F, count) + bytes(flags)
    Path(str(bin_path) + ".oflags").write_bytes(out)
    print("wrote %s.oflags (%d bytes)" % (bin_path.name, len(out)))
    if args.copy_to:
        dest = Path(args.copy_to) / (bin_path.name + ".oflags")
        dest.write_bytes(out)
        print("copied to %s" % dest)

    n_auto0 = sum(1 for b in flags if not (b & 0x01))
    n_noini = sum(1 for b in flags if b & 0x10)
    n_logic0 = sum(1 for b in flags if not (b & 0x02))
    print("stats: auto=0 %d, no-ini %d, no-logic %d" % (n_auto0, n_noini, n_logic0))


if __name__ == "__main__":
    main()
