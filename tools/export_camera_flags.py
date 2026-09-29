"""Export per-mesh `bObscatleCamera` flags for the camera obstruction bake.

Game mechanism (verified 2026-09-28):
  KG3DMeshFileDataLoader::_LoadMeshProperty (KG3DEngineX64.dll) opens the
  mesh's sibling property ini (`<mesh path with .ini extension>` via the
  bin/bsp/ini swap) and reads `[Display] bObscatleCamera`. The KG3DMesh
  constructor defaults the whole [Display] block to 1, so a missing ini means
  the mesh BLOCKS the camera.

Inputs:
  region dirs with sceneinfo JSONs (worldObjects[].comRender.actorModel),
  plus extra mesh paths (foliage patterns).
Output:
  JSON { "<lowercase mesh path>": 0|1 } plus stats.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

import pss_assets  # noqa: E402


def norm(p: str) -> str:
    return p.replace("/", "\\").strip()


def collect_meshes(regions: list[Path]) -> list[str]:
    meshes = set()
    for d in regions:
        for f in sorted(Path(d).glob("*.json")):
            raw = f.read_bytes()
            if len(raw) < 100:
                continue
            try:
                j = json.loads(raw.decode("gb18030"))
            except Exception:
                continue
            for g, o in (j.get("worldObjects") or {}).items():
                r = o.get("comRender") or {}
                m = (r.get("actorModel") or "").strip()
                if not m:
                    continue
                m = norm(m)
                meshes.add(m)
                low = m.lower()
                if low.endswith(".srt"):
                    meshes.add(m[: -len(".srt")] + ".mesh")
                    meshes.add(m[: -len(".srt")] + ".CollisionMesh")
                elif low.endswith(".mesh"):
                    meshes.add(m)
    return sorted(meshes)


def ini_candidates(mesh: str) -> list[str]:
    low = mesh.lower()
    for ext in (".collisionmesh", ".mesh", ".srt"):
        if low.endswith(ext):
            base = mesh[: -len(ext)]
            return [base + ".ini", mesh + ".ini"]
    return [mesh + ".ini"]


def parse_flag(raw: bytes) -> int | None:
    for enc in ("gb18030", "latin1"):
        try:
            txt = raw.decode(enc)
            break
        except Exception:
            continue
    else:
        return None
    for line in txt.splitlines():
        s = line.strip().lower()
        if s.startswith("bobscatlecamera") and "=" in s:
            v = s.split("=", 1)[1].strip()
            if v in ("0", "1"):
                return int(v)
    return None


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--regions", nargs="+",
                    default=[r"C:\jx3tmp\ent_all\out\data\source\maps\龙门寻宝\entities\sceneinfo_full"])
    ap.add_argument("--extra", nargs="*", default=[
        r"data\source\maps_source\石头\wj_石头006_007_hd.mesh",
        r"data\source\maps_source\石头\wj_石头006_005_hd.mesh",
        r"data\source\maps_source\foliage\st_deadwood001_005_hd.mesh",
        r"data\source\maps_source\foliage\wj_cactus001_002_hd.mesh",
    ])
    ap.add_argument("--out", default=str(ROOT / "engine_host_spike" / "collision_data" / "camera_mesh_flags.json"))
    ap.add_argument("--copy-to", default=r"C:\SeasunGame\MovieEditor\bin64\collision_data")
    ap.add_argument("--work", default=r"C:\jx3tmp\cam_flag_work")
    ap.add_argument("--chunk", type=int, default=150)
    args = ap.parse_args()

    meshes = list(dict.fromkeys(norm(m) for m in (collect_meshes([Path(r) for r in args.regions]) + args.extra)))
    print(f"distinct mesh paths: {len(meshes)}")

    # build candidate list (mesh, ini candidate) — extract the first that exists
    cands = []
    for m in meshes:
        for c in ini_candidates(m):
            cands.append((m, c))
    print(f"ini candidates: {len(cands)}")

    found_flags: dict[str, int] = {}
    missing = 0
    work = Path(args.work)
    work.mkdir(parents=True, exist_ok=True)
    by_mesh_cands = {}
    for m, c in cands:
        by_mesh_cands.setdefault(m, []).append(c)

    paths = [c for _, c in cands]
    for i in range(0, len(paths), args.chunk):
        batch = paths[i:i + args.chunk]
        try:
            res = pss_assets.run_pakv4(batch, work=work)
        except Exception as exc:  # noqa: BLE001
            print("ERR batch", i, exc)
            continue
        # map returned rel path (basename match) to flag
        by_name = {}
        for key, data in res.items():
            by_name[Path(key).name.lower()] = data
        for m in meshes:
            if m.lower() in found_flags:
                continue
            for c in by_mesh_cands.get(m, []):
                data = by_name.get(Path(c).name.lower())
                if data is None:
                    continue
                flag = parse_flag(data)
                if flag is not None:
                    found_flags[m.lower()] = flag
                    break
        print(f"batch {i//args.chunk + 1}/{(len(paths)+args.chunk-1)//args.chunk}: "
              f"flags so far {len(found_flags)}/{len(meshes)}")

    table = {}
    zeros = 0
    for m in meshes:
        f = found_flags.get(m.lower(), 1)   # ctor default = 1 (blocks)
        table[m.lower()] = f
        if f == 0:
            zeros += 1
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(table, indent=1, ensure_ascii=False), encoding="utf-8")
    print(f"wrote {out}: {len(table)} meshes, {zeros} with bObscatleCamera=0, "
          f"{len(found_flags)} from inis, rest default 1")
    if args.copy_to:
        dest = Path(args.copy_to) / out.name
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text(out.read_text(encoding="utf-8"), encoding="utf-8")
        print("copied to", dest)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
