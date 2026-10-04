# -*- coding: utf-8 -*-
# Phase-1 grid A/B diff: parses engine "PX_GRID x z yA:st yB:st..." lines and
# solver "GRID x z yA:st ..." lines, expands transitions to per-y state sets,
# and reports per-point mismatches (default 1 y-step tolerance off).
import sys, re

def parse(path, tag):
    pts = {}
    for ln in open(path, encoding='utf-8', errors='replace'):
        m = re.match(r'.*\b' + tag + r' (-?\d+) (-?\d+)((?: y\d+:[01])*)\s*$', ln)
        if not m:
            continue
        x, z = int(m.group(1)), int(m.group(2))
        states = {}
        for ym in re.finditer(r'y(\d+):([01])', m.group(3)):
            states[int(ym.group(1))] = int(ym.group(2))
        pts[(x, z)] = states
    return pts

def expand(states, step, ymax):
    out = {}
    cur = 0
    for y in range(0, ymax + 1, step):
        if y in states:
            cur = states[y]
        out[y] = cur
    return out

def main():
    eng = parse(sys.argv[1], 'PX_GRID')
    sol = parse(sys.argv[2], 'GRID')
    step = int(sys.argv[3]) if len(sys.argv) > 3 else 25
    ymax = int(sys.argv[4]) if len(sys.argv) > 4 else 1500
    common = sorted(set(eng) & set(sol))
    only_e = len(set(eng) - set(sol))
    only_s = len(set(sol) - set(eng))
    mism = []
    for key in common:
        e = expand(eng[key], step, ymax)
        s = expand(sol[key], step, ymax)
        bad = [y for y in e if e[y] != s.get(y, 0)]
        if bad:
            mism.append((key, bad, eng[key], sol[key]))
    print('points: engine=%d solver=%d common=%d (onlyE=%d onlyS=%d)' % (len(eng), len(sol), len(common), only_e, only_s))
    print('mismatched points: %d / %d (%.2f%%)' % (len(mism), len(common), 100.0 * len(mism) / max(1, len(common))))
    for (key, bad, et, st) in mism[:15]:
        print('  MISMATCH x=%d z=%d firstY=%s n=%d' % (key[0], key[1], bad[0], len(bad)))
        print('    engine:', {k: et[k] for k in sorted(et)})
        print('    solver:', {k: st[k] for k in sorted(st)})

main()
