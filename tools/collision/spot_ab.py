# -*- coding: utf-8 -*-
# A/B check: capsule (r=17, hh=41) vertical scan at a field spot against our
# world-baked structure bin, vs the engine PxMeshQuery result (proof doc
# phys_engine_vtables.txt 2026-10-02b).
import struct, math

BIN = r'C:\SeasunGame\MovieEditor\bin64\collision_data\龙门寻宝_夜晚_structure_collision.bin'
SPOTS = [(23334.0, 24224.0, 'spawn'), (18915.0, 36850.0, 'wall'), (20450.0, 31000.0, 'pile')]
R, HH = 17.0, 41.0


def point_tri_dist2(px, py, pz, a, b, c):
    ax, ay, az = a; bx, by, bz = b; cx, cy, cz = c
    abx, aby, abz = bx - ax, by - ay, bz - az
    acx, acy, acz = cx - ax, cy - ay, cz - az
    apx, apy, apz = px - ax, py - ay, pz - az
    d1 = abx * apx + aby * apy + abz * apz
    d2 = acx * apx + acy * apy + acz * apz
    if d1 <= 0 and d2 <= 0:
        return apx * apx + apy * apy + apz * apz
    bpx, bpy, bpz = px - bx, py - by, pz - bz
    d3 = abx * bpx + aby * bpy + abz * bpz
    d4 = acx * bpx + acy * bpy + acz * bpz
    if d3 >= 0 and d4 <= d3:
        return bpx * bpx + bpy * bpy + bpz * bpz
    vc = d1 * d4 - d3 * d2
    if vc <= 0 and d1 >= 0 and d3 <= 0:
        t = d1 / (d1 - d3)
        qx, qy, qz = ax + t * abx, ay + t * aby, az + t * abz
        return (px - qx) ** 2 + (py - qy) ** 2 + (pz - qz) ** 2
    cpx, cpy, cpz = px - cx, py - cy, pz - cz
    d5 = abx * cpx + aby * cpy + abz * cpz
    d6 = acx * cpx + acy * cpy + acz * cpz
    if d6 >= 0 and d5 <= d6:
        return cpx * cpx + cpy * cpy + cpz * cpz
    vb = d5 * d2 - d1 * d6
    if vb <= 0 and d2 >= 0 and d6 <= 0:
        t = d2 / (d2 - d6)
        qx, qy, qz = ax + t * acx, ay + t * acy, az + t * acz
        return (px - qx) ** 2 + (py - qy) ** 2 + (pz - qz) ** 2
    va = d3 * d6 - d5 * d4
    if va <= 0 and (d4 - d3) >= 0 and (d5 - d6) >= 0:
        t = (d4 - d3) / ((d4 - d3) + (d5 - d6))
        qx, qy, qz = bx + t * (cx - bx), by + t * (cy - by), bz + t * (cz - bz)
        return (px - qx) ** 2 + (py - qy) ** 2 + (pz - qz) ** 2
    den = 1.0 / (va + vb + vc)
    v = vb * den; w = vc * den
    qx = ax + abx * v + acx * w
    qy = ay + aby * v + acy * w
    qz = az + abz * v + acz * w
    return (px - qx) ** 2 + (py - qy) ** 2 + (pz - qz) ** 2


spot_tris = {sn: [] for (_, _, sn) in SPOTS}
f = open(BIN, 'rb')
f.read(8)
mc = struct.unpack('<i', f.read(4))[0]
f.read(4)
for i in range(mc):
    vc, tc = struct.unpack('<ii', f.read(8))
    vb = f.read(vc * 12)
    tb = f.read(tc * 12)
    vs = struct.unpack('<%df' % (vc * 3), vb)
    idx = struct.unpack('<%dI' % (tc * 3), tb)
    for k in range(tc):
        a = (vs[idx[k * 3] * 3], vs[idx[k * 3] * 3 + 1], vs[idx[k * 3] * 3 + 2])
        b = (vs[idx[k * 3 + 1] * 3], vs[idx[k * 3 + 1] * 3 + 1], vs[idx[k * 3 + 1] * 3 + 2])
        c = (vs[idx[k * 3 + 2] * 3], vs[idx[k * 3 + 2] * 3 + 1], vs[idx[k * 3 + 2] * 3 + 2])
        for (sx, sz, sn) in SPOTS:
            if min(abs(a[0] - sx), abs(b[0] - sx), abs(c[0] - sx)) < 3000 and \
               min(abs(a[2] - sz), abs(b[2] - sz), abs(c[2] - sz)) < 3000:
                spot_tris[sn].append((a, b, c))
f.close()
for (sx, sz, sn) in SPOTS:
    tris = spot_tris[sn]
    print('%s (%g,%g): nearby triangles=%d' % (sn, sx, sz, len(tris)))
    prev = -1
    for y in range(0, 1501, 25):
        cnt = 0
        for (a, b, c) in tris:
            hit = False
            for s in range(18):
                sy = y - HH + (2.0 * HH) * s / 17.0
                if point_tri_dist2(sx, sy, sz, a, b, c) <= R * R:
                    hit = True
                    break
            if hit:
                cnt += 1
        if cnt != prev:
            print('    our-solver scan: y%d -> %d' % (y, cnt))
            prev = cnt
