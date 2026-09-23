"""Export the map's real world objects (walls, buildings, rocks, props) for collision.

Source: entities/sceneinfo_full/%03u_%03u.json (JXSubSceneInfo.worldObjects),
the same files the engine's SceneFileLoader_Jsonmap::OnSyncLoad reads
(region file template "%s%s\\entities\\%s_full\\%03u_%03u.json" with %s =
"sceneinfo").

Each object has:
  comRender.actorModel        mesh path (data\\source\\maps_source\\...)
  comBasic.actorLocalMatrix   4x4 row-major, translation in row 3
  comBasic.actorBoundBoxMin/Max  world-space bounds

Output: structure_collision.bin
  magic 'FCOL', version u32 = 2, meshCount u32, instanceCount u32
  mesh:     vertCount u32, triCount u32, verts f32[3V], tris u32[3T]
  instance: meshIndex u32, m[16] f32 (local->world), pad u32,
            bboxMin f32[3], bboxMax f32[3]
"""

import argparse
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import mesh as jx3mesh  # noqa: E402
from pss_assets import run_pakv4  # noqa: E402

MAGIC = 0x4C4F4346  # 'FCOL'


def load_objects(region_dir):
    objs = []
    for f in sorted(Path(region_dir).glob('*.json')):
        raw = f.read_bytes()
        if len(raw) < 100:
            continue
        j = json.loads(raw.decode('gb18030'))
        for g, o in (j.get('worldObjects') or {}).items():
            r = o.get('comRender') or {}
            b = o.get('comBasic') or {}
            model = (r.get('actorModel') or '').strip()
            m = b.get('actorLocalMatrix')
            if not model or not m or len(m) != 16:
                continue
            ext = Path(model).suffix.lower()
            if ext == '.mesh':
                coll = model
            elif ext == '.srt':
                # SpeedTree: the physics engine builds "<base>.CollisionMesh"
                # from the .srt name (PhysicsEngine::_GetCollisionGeometryFilesFromFile)
                coll = model[:-len('.srt')] + '.CollisionMesh'
            else:
                continue
            objs.append({
                'uuid': g,
                'model': coll,
                'srt': ext == '.srt',
                'm': [float(v) for v in m],
                'bmin': b.get('actorBoundBoxMin'),
                'bmax': b.get('actorBoundBoxMax'),
            })
    return objs


def make_cylinder(radius, y0, y1, segs=14):
    import math
    verts = []
    tris = []
    for i in range(segs):
        a = 2.0 * math.pi * i / segs
        x, z = radius * math.cos(a), radius * math.sin(a)
        verts.append((x, y0, z))
        verts.append((x, y1, z))
    for i in range(segs):
        j = (i + 1) % segs
        b0, t0, b1, t1 = i * 2, i * 2 + 1, j * 2, j * 2 + 1
        tris.append((b0, b1, t1))
        tris.append((b0, t1, t0))
    # caps
    cb = len(verts); verts.append((0.0, y0, 0.0))
    ct = len(verts); verts.append((0.0, y1, 0.0))
    for i in range(segs):
        j = (i + 1) % segs
        tris.append((cb, j * 2, i * 2))
        tris.append((ct, i * 2 + 1, j * 2 + 1))
    return verts, tris


def norm_pak_path(p):
    p = p.replace('\\', '/')
    if p[:5].lower() == 'data/':
        p = 'data/' + p[5:]
    return p


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--regions', default=r'C:\jx3tmp\ent_all\out\data\source\maps\龙门寻宝\entities\sceneinfo_full')
    ap.add_argument('--out', default=str(Path(__file__).resolve().parent.parent /
                                         'engine_host_spike' / 'collision_data' / 'structure_collision.bin'))
    ap.add_argument('--copy-to', default=r'C:\SeasunGame\MovieEditor\bin64\collision_data')
    ap.add_argument('--work', default=r'C:\jx3tmp\struct_meshes')
    args = ap.parse_args()

    objs = load_objects(args.regions)
    print('objects with .mesh: %d' % len(objs))
    models = {}
    for o in objs:
        models.setdefault(norm_pak_path(o['model']), 0)
        models[norm_pak_path(o['model'])] += 1
    print('distinct models: %d' % len(models))

    # extract all distinct meshes in batches
    work = Path(args.work)
    work.mkdir(parents=True, exist_ok=True)
    extracted = {}
    paths = sorted(models)
    B = 120
    for i in range(0, len(paths), B):
        batch = paths[i:i + B]
        found = run_pakv4(batch, work=work)
        for k, v in found.items():
            extracted[k.replace('/', '\\').lower()] = v
        print('  extracted batch %d: %d/%d (total %d)' % (i // B, len(found), len(batch), len(extracted)))

    meshes = {}
    skipped = []
    for path in paths:
        data = extracted.get(path.replace('/', '\\').lower())
        if data is None:
            skipped.append(path)
            continue
        tmp = work / '_one.mesh'
        tmp.write_bytes(data)
        try:
            m = jx3mesh.load_mesh(tmp)
            meshes[path] = m
        except Exception as e:
            skipped.append('%s (%s)' % (path, e))
    print('parsed meshes: %d, skipped: %d' % (len(meshes), len(skipped)))
    for s in skipped[:10]:
        print('   skip', s)

    # Use exactly the colliders the game ships. Trees whose CollisionMesh is a
    # degenerate fragment (no usable collider in the real client either) are
    # skipped rather than filled with invented geometry.
    import numpy as np
    extra = {}      # synthetic mesh key -> (verts, tris)
    placed = []     # (object, mesh key) pairs to write
    degenerate = 0
    for o in objs:
        p = norm_pak_path(o['model'])
        m = meshes.get(p)
        if m is None:
            continue
        # use exactly the shipped CollisionMesh: no synthetic colliders,
        # no invented sizes
        v = m.positions
        h = float(v[:, 1].max() - v[:, 1].min())
        xz = float(max(v[:, 0].max() - v[:, 0].min(),
                       v[:, 2].max() - v[:, 2].min()))
        if o.get('srt') and (h < 150.0 or xz < 30.0):
            degenerate += 1
            continue
        placed.append((o, p))
    print('trees with only a degenerate shipped collider (skipped): %d' % degenerate)

    all_meshes = {}
    for path in meshes:
        all_meshes[path] = (meshes[path].positions.astype('<f4'),
                            meshes[path].faces.astype('<u4'))
    for k, (cv, ct) in extra.items():
        all_meshes[k] = (np.asarray(cv, dtype='<f4'), np.asarray(ct, dtype='<u4'))

    mesh_index = {p: i for i, p in enumerate(sorted(all_meshes))}

    out = bytearray()
    out += struct.pack('<IIII', MAGIC, 2, len(mesh_index), len(placed))
    for path in sorted(all_meshes):
        verts, tris = all_meshes[path]
        out += struct.pack('<II', len(verts), len(tris))
        out += verts.tobytes()
        out += tris.tobytes()

    written = 0
    for o, key in placed:
        mi = mesh_index.get(key)
        if mi is None:
            continue
        bmin = o['bmin'] or [0, 0, 0]
        bmax = o['bmax'] or [0, 0, 0]
        out += struct.pack('<I16fI6f', mi, *(o['m']), 0,
                           float(bmin[0]), float(bmin[1]), float(bmin[2]),
                           float(bmax[0]), float(bmax[1]), float(bmax[2]))
        written += 1

    dest = Path(args.out)
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(out)
    print('wrote %s (%d bytes, %d meshes, %d instances)' % (dest, len(out), len(mesh_index), written))
    if args.copy_to:
        d2 = Path(args.copy_to) / dest.name
        d2.parent.mkdir(parents=True, exist_ok=True)
        d2.write_bytes(out)
        print('copied to %s' % d2)


if __name__ == '__main__':
    main()
