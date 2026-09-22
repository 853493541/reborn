"""Decode JX3 .foliage v1 (flags=4) instance files.

Format reverse-engineered from SceneManagerx64.dll:
  SceneFileLoader_Foliage_Binary::LoadInstances   @0x180020700
  SceneFileLoader_Foliage_Binary::ReadInstanceData@0x1800213a0
  readers: ReadUint@0x1800219a0 ReadUint1@0x180021a40 Readfloat@0x180021ae0

Layout (little endian):
  header 40 bytes:
    +0x00 u32 magic 'FOLI'
    +0x04 u32 version (1)
    +0x08 u32 fileLength
    +0x0C u32 unitCount
    +0x10 u32 totalInstanceCount
    +0x14 u32 flags
  unitCount x 72-byte unit heads (flags bit0 adds a 92-byte editor head first):
    +0x40 f32 baseZ          (cell origin z)
    +0x44 u32 nodeCount      (number of cell entries)
  per unit (in order):
    nodeCount x cell entries: u16 cell (lo=cellX, hi=cellY), then
        u8 count             (or u32 count when cell present in unit's dense map)
    then for each cell: count x instance records (variable size)
  instance record:
    u32 flags
    x,y,z      : Readfloat(mode 0,2,4, divisor 100) + (cellX*400, cellY*400, baseZ)
    rot        : u8/100-1 ; +u8/100-1 ; bit26 ? u8/100-1 : +-sqrt(1-a^2-b^2)
    scale      : bit7 ? 3x Readfloat(mode 8,0xa,0xc,/100) : 1x(mode 8) all axes
    bit14 ? (bit25 ? 4 bytes /100-1 : 3 bytes angles->quat) : 2 bytes /255*10
    u34        : ReadUint1(mode 0xf) | ReadUint1(mode 0x11)<<15
    u16?       : ReadUint1(mode 0x13)
    f?         : Readfloat(mode 0x15, /10)
    bit23 ? skip 1 ; bit24 -> valid=0 ; bit27 ? skip 1 ; bit28 ? skip 0x40
    ReadUint(mode 0x1d)
    bit31 ? skip 4
"""

import struct
import sys
from pathlib import Path

FOLIAGE_ID = 0x494C4F46
EPS = 1.192092896e-07
CELL = 400.0


class Reader:
    def __init__(self, data):
        self.d = data

    def u8(self, o):
        return self.d[o], o + 1

    def u16(self, o):
        return struct.unpack_from('<H', self.d, o)[0], o + 2

    def u32(self, o):
        return struct.unpack_from('<I', self.d, o)[0], o + 4

    def f32(self, o):
        return struct.unpack_from('<f', self.d, o)[0], o + 4

    def read_uint(self, o, flags, mode, zero_invalid=False):
        sel = (flags >> mode) & 3
        if zero_invalid and sel == 0:
            raise ValueError('ReadUint1 selector 0 (invalid) mode=%#x' % mode)
        if sel == 0:
            v, o = self.u8(o)
            return v, o
        if sel == 1:
            v, o = self.u16(o)
            return v, o
        v, o = self.u32(o)
        return v, o

    def read_uint1(self, o, flags, mode):
        """ReadUint1: selector 0 consumes nothing and yields 0."""
        sel = (flags >> mode) & 3
        if sel == 0:
            return 0, o
        if sel == 1:
            v, o = self.u8(o)
            return v, o
        if sel == 2:
            v, o = self.u16(o)
            return v, o
        v, o = self.u32(o)
        return v, o

    def read_float(self, o, flags, mode, divisor):
        sel = (flags >> mode) & 3
        if sel == 0:
            v, o = self.u8(o)
            return v / divisor, o
        if sel == 1:
            v, o = self.u16(o)
            return v / divisor, o
        v, o = self.f32(o)
        return v, o


def read_instance(r, o, cell_origin):
    flags, o = r.u32(o)
    pos = []
    for mode in (0, 2, 4):
        v, o = r.read_float(o, flags, mode, 100.0)
        pos.append(v)

    rot = []
    b, o = r.u8(o)
    rot.append(b / 100.0 - 1.0)
    b, o = r.u8(o)
    rot.append(b / 100.0 - 1.0)
    if flags & (1 << 26):
        b, o = r.u8(o)
        rot.append(b / 100.0 - 1.0)
    else:
        c = 1.0 - rot[0] * rot[0] - rot[1] * rot[1]
        c = c ** 0.5 if c > 0.0 else 0.0
        if flags & (1 << 6):
            c = -c
        rot.append(c)

    if flags & (1 << 7):
        scale = []
        for mode in (8, 0xA, 0xC):
            v, o = r.read_float(o, flags, mode, 100.0)
            scale.append(1.0 if abs(v) < EPS else v)
    else:
        v, o = r.read_float(o, flags, 8, 100.0)
        v = 1.0 if abs(v) < EPS else v
        scale = [v, v, v]

    rot2 = [0.0, 0.0]
    if flags & (1 << 14):
        if flags & (1 << 25):
            for _ in range(4):
                _, o = r.u8(o)
        else:
            for _ in range(3):
                _, o = r.u8(o)
    else:
        for k in range(2):
            b, o = r.u8(o)
            rot2[k] = b / 255.0 * 6.283185307179586

    u34, o = r.read_uint(o, flags, 0xF)
    v11, o = r.read_uint(o, flags, 0x11)
    u34 |= (v11 << 15) & 0xFFFFFFFF
    v13, o = r.read_uint(o, flags, 0x13)
    v15, o = r.read_float(o, flags, 0x15, 10.0)
    # note: 0x1d reader is ReadUint1: selector 0 consumes nothing

    if flags & (1 << 23):
        _, o = r.u8(o)
    valid = not (flags & (1 << 24))
    if flags & (1 << 27):
        _, o = r.u8(o)
    if flags & (1 << 28):
        o += 0x40
    v29, o = r.read_uint1(o, flags, 0x1D)
    if flags & (1 << 31):
        o += 4

    pos[0] += cell_origin[0]
    pos[1] += cell_origin[1]
    pos[2] += cell_origin[2]
    return {'flags': flags, 'pos': pos, 'rot': rot, 'rot2': rot2,
            'scale': scale, 'valid': valid, 'u34': u34, 'v1': u34 & 0x7FFF,
            'v2': v11, 'v3': v13, 'v15': v15, 'v29': v29,
            'tex': (u34 >> 15) & 0x7FFF}, o


def decode(data, verbose=False):
    if len(data) < 40:
        raise ValueError('too small')
    magic, version, file_len, unit_cnt, total_cnt, flags = struct.unpack_from('<6I', data, 0)
    if magic != FOLIAGE_ID:
        raise ValueError('bad magic %#x' % magic)
    if version != 1:
        raise ValueError('bad version %d' % version)
    if file_len != len(data):
        raise ValueError('file length mismatch %d != %d' % (file_len, len(data)))
    if flags & 1:
        raise ValueError('editor head (flags bit0) not supported')
    if flags & 2:
        raise ValueError('dense map (flags bit1) not supported')

    r = Reader(data)
    o = 40
    units = []
    for _ in range(unit_cnt):
        head = data[o:o + 72]
        base_z = struct.unpack_from('<f', head, 0x40)[0]
        node_cnt = struct.unpack_from('<I', head, 0x44)[0]
        units.append({'base_z': base_z, 'node_cnt': node_cnt, 'cells': [], 'inst': []})
        o += 72

    for u in units:
        if flags & 4:
            u['mask'], o = r.u32(o)
        if flags & 8:
            u['level'], o = r.u32(o)
        if flags & 0x10:
            u['extra'], o = r.u32(o)
        for _ in range(u['node_cnt']):
            cell, o = r.u16(o)
            cnt, o = r.u8(o)
            u['cells'].append((cell & 0xFF, cell >> 8, cnt))
        for (cx, cy, cnt) in u['cells']:
            # engine foliage coords are (x, y=height, z); cell bias is
            # (cellX*400, baseZ, cellY*400) per ProcessInstances/FoliageRegion
            origin = (cx * CELL, u['base_z'], cy * CELL)
            for _ in range(cnt):
                inst, o = read_instance(r, o, origin)
                u['inst'].append(inst)

    if o != len(data):
        raise ValueError('trailing bytes: parsed %d of %d' % (o, len(data)))
    got = sum(len(u['inst']) for u in units)
    if got != total_cnt:
        raise ValueError('instance count %d != header %d' % (got, total_cnt))
    return units


def main():
    for path in sys.argv[1:]:
        p = Path(path)
        try:
            units = decode(p.read_bytes())
        except Exception as e:
            print('%-20s FAIL %s' % (p.name, e))
            continue
        total = sum(len(u['inst']) for u in units)
        zs = [i['pos'][2] for u in units for i in u['inst']]
        xs = [i['pos'][0] for u in units for i in u['inst']]
        ys = [i['pos'][1] for u in units for i in u['inst']]
        print('%-20s OK units=%d cells=%d inst=%d x[%.1f,%.1f] y[%.1f,%.1f] z[%.1f,%.1f]' % (
            p.name, len(units), sum(len(u['cells']) for u in units), total,
            min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))


if __name__ == '__main__':
    main()
