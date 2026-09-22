"""Export solid foliage instances + their meshes for the map host collision.

Inputs:
  foliage dump dir   (default C:/jx3tmp/foliage_dump)   *.foliage region files
  extracted meshes   (default C:/jx3tmp/fol_meshes/out) maps_source tree
  pattern table      (default C:/jx3tmp/house_dump/龙门寻宝_foliageinfo_editor.json)
Output:
  engine_host_spike/collision_data/foliage_collision.bin
  and a copy in the host's working dir (bin64/collision_data) when --copy-to given.

Binary format (little endian):
  magic 'FCOL', version u32 = 1, meshCount u32, instanceCount u32
  mesh:   patternID u32, sceneScale f32, vertCount u32, triCount u32,
          verts f32[3*V], tris u32[3*T]
  inst:   patternID u32, x f32, y f32, z f32, yaw f32, scale f32
"""

import argparse
import glob
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import decode_foliage as D  # noqa: E402
import mesh as jx3mesh  # noqa: E402

REGION = 51200.0
WORLD_ORIGIN = -102400.0
SOLID_PATTERNS = (4, 5, 6, 7)
MAGIC = 0x4C4F4346  # 'FCOL'

MESH_BY_PATTERN = {
    4: r'foliage\st_deadwood001_005_hd.mesh',
    5: r'foliage\wj_cactus001_002_hd.mesh',
    6: r'石头\wj_石头006_007_hd.mesh',
    7: r'石头\wj_石头006_005_hd.mesh',
}


def load_pattern_scales(pattern_json):
    scales = {}
    raw = Path(pattern_json).read_bytes()
    for enc in ('gb18030', 'utf-8-sig', 'utf-8'):
        try:
            arr = json.loads(raw.decode(enc))
            break
        except (UnicodeDecodeError, ValueError):
            arr = None
    if arr is None:
        raise SystemExit('cannot decode pattern table %s' % pattern_json)
    for p in arr:
        scales[int(p['FoliagePatternID'])] = float(p.get('FoliagePatternSceneScale', 1.0))
    return scales


def load_instances(dump_dir):
    out = []
    for f in sorted(glob.glob(str(Path(dump_dir) / '*.foliage'))):
        name = Path(f).stem
        ix, iz = (int(v) for v in name.split('_'))
        ox = WORLD_ORIGIN + ix * REGION
        oz = WORLD_ORIGIN + iz * REGION
        for u in D.decode(Path(f).read_bytes()):
            for i in u['inst']:
                if i['v1'] not in SOLID_PATTERNS:
                    continue
                x, y, z = i['pos']
                out.append({
                    'pattern': i['v1'],
                    'x': ox + x,
                    'y': y,
                    'z': oz + z,
                    'yaw': i['rot2'][0],
                    'scale': i['scale'][0],
                })
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--dump', default=r'C:\jx3tmp\foliage_dump')
    ap.add_argument('--mesh-root', default=r'C:\jx3tmp\fol_meshes\out\data\source\maps_source')
    ap.add_argument('--patterns', default=r'C:\jx3tmp\house_dump\龙门寻宝_foliageinfo_editor.json')
    ap.add_argument('--out', default=str(Path(__file__).resolve().parent.parent /
                                         'engine_host_spike' / 'collision_data' / 'foliage_collision.bin'))
    ap.add_argument('--copy-to', default=r'C:\SeasunGame\MovieEditor\bin64\collision_data')
    args = ap.parse_args()

    scales = load_pattern_scales(args.patterns)
    instances = load_instances(args.dump)
    print('instances: %d' % len(instances))
    per = {}
    for i in instances:
        per[i['pattern']] = per.get(i['pattern'], 0) + 1
    print('per pattern:', per)

    meshes = {}
    for pat, rel in MESH_BY_PATTERN.items():
        m = jx3mesh.load_mesh(Path(args.mesh_root) / rel)
        meshes[pat] = m
        print('mesh pattern %d: %s V=%d F=%d' % (pat, rel, m.vertex_count, m.face_count))

    out = bytearray()
    out += struct.pack('<IIII', MAGIC, 1, len(meshes), len(instances))
    for pat in sorted(meshes):
        m = meshes[pat]
        verts = m.positions.astype('<f4')
        tris = m.faces.astype('<u4')
        out += struct.pack('<IfII', pat, scales.get(pat, 1.0), len(verts), len(tris))
        out += verts.tobytes()
        out += tris.tobytes()
    for i in instances:
        out += struct.pack('<Ifffff', i['pattern'], i['x'], i['y'], i['z'], i['yaw'],
                           i['scale'] * scales.get(i['pattern'], 1.0))

    dest = Path(args.out)
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(out)
    print('wrote %s (%d bytes)' % (dest, len(out)))
    if args.copy_to:
        dest2 = Path(args.copy_to) / dest.name
        dest2.parent.mkdir(parents=True, exist_ok=True)
        dest2.write_bytes(out)
        print('copied to %s' % dest2)


if __name__ == '__main__':
    main()
