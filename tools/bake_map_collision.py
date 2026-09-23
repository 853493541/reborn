"""Bake full collision data for any map from the game's own local files.

Pipeline (all data comes from the pak; nothing is authored by hand):
  1. read entities/<map>_sceneinfo.json for the region table size
  2. extract foliage/foliageinfo/%03u_%03u.foliage  -> decode -> foliage bins
  3. extract the foliage pattern table + pattern meshes
  4. extract entities/sceneinfo_full/%03u_%03u.json -> structure bins
     (world objects with their real meshes and 4x4 matrices)
  5. write <map>_foliage_collision.bin / <map>_structure_collision.bin

Usage:
  python tools/bake_map_collision.py --map 龙门寻宝
  python tools/bake_map_collision.py --map 龙门寻宝 --copy-to C:/SeasunGame/MovieEditor/bin64/collision_data
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from pss_assets import run_pakv4  # noqa: E402

TOOLS = Path(__file__).resolve().parent
REPO = TOOLS.parent

FOLIAGE_MESHES = [
    r'data\source\maps_source\foliage\st_deadwood001_005_hd.mesh',
    r'data\source\maps_source\foliage\wj_cactus001_002_hd.mesh',
    r'data\source\maps_source\石头\wj_石头006_007_hd.mesh',
    r'data\source\maps_source\石头\wj_石头006_005_hd.mesh',
]


def dump(found, dest):
    dest.mkdir(parents=True, exist_ok=True)
    n = 0
    for logical, data in found.items():
        name = logical.split('/')[-1]
        (dest / name).write_bytes(data)
        n += 1
    return n


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--map', required=True, help='map name, e.g. 龙门寻宝')
    ap.add_argument('--work', default=r'C:\jx3tmp\map_bake')
    ap.add_argument('--out-dir', default=None,
                    help='where to write the bins (default: repo collision_data)')
    ap.add_argument('--copy-to', default=None,
                    help='also copy the bins here (e.g. MovieEditor bin64 collision_data)')
    ap.add_argument('--max-regions', type=int, default=16)
    args = ap.parse_args()

    m = args.map
    work = Path(args.work) / m
    work.mkdir(parents=True, exist_ok=True)
    out_dir = Path(args.out_dir) if args.out_dir else REPO / 'engine_host_spike' / 'collision_data'
    out_dir.mkdir(parents=True, exist_ok=True)

    # 1. sceneinfo -> region table size
    rx = ry = 8
    found = run_pakv4([f'data/source/maps/{m}/entities/{m}_sceneinfo.json'], work=work / '_sceneinfo')
    if found:
        try:
            info = json.loads(list(found.values())[0].decode('gb18030', errors='replace'))
            rx = int(info.get('RegionTableSize.x', rx))
            ry = int(info.get('RegionTableSize.y', ry))
        except Exception as e:
            print('sceneinfo parse failed (%s), assuming 8x8' % e)
    print('[%s] region table %dx%d' % (m, rx, ry))

    # 2. foliage region files
    cands = [f'data/source/maps/{m}/foliage/foliageinfo/%03d_%03d.foliage' % (x, y)
             for x in range(rx) for y in range(ry)]
    found = run_pakv4(cands, work=work / '_foliage')
    fol_dir = work / 'foliage_dump'
    n_fol = dump(found, fol_dir)
    print('[%s] foliage region files: %d' % (m, n_fol))

    # 3. pattern table + pattern meshes
    found = run_pakv4([f'data/source/maps/{m}/foliage/{m}_foliageinfo_editor.json'],
                      work=work / '_patterns')
    if not found:
        print('[%s] no foliage pattern table - skipping foliage export' % m)
        pat_json = None
    else:
        pat_json = work / 'patterns.json'
        pat_json.write_bytes(list(found.values())[0])

    mesh_root = work / 'meshes'
    found = run_pakv4(FOLIAGE_MESHES, work=work / '_folmeshes')
    for logical, data in found.items():
        rel = logical.replace('data/source/maps_source/', '')
        dest = mesh_root / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(data)
    print('[%s] foliage meshes: %d' % (m, len(found)))

    if n_fol and pat_json is not None:
        out_f = out_dir / f'{m}_foliage_collision.bin'
        cmd = [sys.executable, str(TOOLS / 'export_foliage_collision.py'),
               '--dump', str(fol_dir), '--patterns', str(pat_json),
               '--mesh-root', str(mesh_root), '--out', str(out_f)]
        if args.copy_to:
            cmd += ['--copy-to', args.copy_to]
        subprocess.run(cmd, check=False)

    # 4. world objects
    cands = [f'data/source/maps/{m}/entities/sceneinfo_full/%03d_%03d.json' % (x, y)
             for x in range(rx) for y in range(ry)]
    found = run_pakv4(cands, work=work / '_entities')
    ent_dir = work / 'sceneinfo_full'
    n_ent = dump(found, ent_dir)
    print('[%s] world-object region files: %d' % (m, n_ent))

    if n_ent:
        out_s = out_dir / f'{m}_structure_collision.bin'
        cmd = [sys.executable, str(TOOLS / 'export_structure_collision.py'),
               '--regions', str(ent_dir), '--out', str(out_s),
               '--work', str(work / 'struct_meshes')]
        if args.copy_to:
            cmd += ['--copy-to', args.copy_to]
        subprocess.run(cmd, check=False)

    print('[%s] done -> %s' % (m, out_dir))


if __name__ == '__main__':
    main()
