#!/usr/bin/env python3
"""mani_probe.py - decode JX3 `.mani` camera-track files (ACON container).

Format (verified from KG3DMovieX64.dll serialization, see
docs/camera/MANI_FORMAT.md and proof/camera_tracks/disasm/*):

  File = sequence of 40-byte sections:
      {u32 magic 'ACON' (0x4E4F4341), u32 classId, 32 zero bytes}
      + class payload.

  Cameradata variant (`represent/camera/cameradata/<id>_<n>.mani`,
  referenced by SceneCameraAni.tab):
      class 25 payload: 8 zero bytes
          + meta {u32 1, f32 durationFrames, u32 0, u32 1}
      class 10 payload: 8 zero bytes
          + meta {u32 1, f32 durationFrames, u32 1, u32 0}
          + track A header {u32 nA, f32 x0, f32 z0, f32 y0}
              (implicit frame-0 key at x0, z0, y0; x0=0 in all shipped samples)
          + (nA-1) x A-key {f32 x, u32 frame, f32 z, f32 y}
          + track B header {f32 x0, u32 0, u32 nB, u32 0}
          + nB x B-key {f32 x, f32 a, f32 b, u32 frame}
      A/B keys are sparse (frames increasing, gaps allowed, last <= nA-1).
      A = primary camera position track; B = secondary track (layout HIGH,
      semantics MED - treated as look-at target by the host player).

  Rush variant (`data/movie/camera/16.mani` etc.): class 10 only, payload
  starts with meta {u32 1, f32 dur, u32 0, u32 1} (third/fourth swapped) and
  a different key grammar - NOT decoded here (deferred, see the plan).

Usage:
  python tools/camera/mani_probe.py <file.mani> [--keys] [--limit N]
  python tools/camera/mani_probe.py --verify <dir>      # parse every *.mani
  python tools/camera/mani_probe.py --selftest          # offline round-trip

Stdlib only. Read-only: never writes into the game installs.
"""
from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

ACON = 0x4E4F4341
HEADER = 40
CLASS_CAMERA_ANI_SET = 25
CLASS_CAMERA_TRACK = 10


class ManiError(Exception):
    pass


def _u32(b, o):
    return struct.unpack_from("<I", b, o)[0]


def _f32(b, o):
    return struct.unpack_from("<f", b, o)[0]


class Key(object):
    __slots__ = ("frame", "x", "y", "z")

    def __init__(self, frame, x, y, z):
        self.frame = frame
        self.x = x
        self.y = y
        self.z = z

    def __repr__(self):
        return "Key(f=%d x=%.2f y=%.2f z=%.2f)" % (self.frame, self.x, self.y, self.z)


class Track(object):
    """One camera track: sparse keys {x, z, y} indexed by frame.

    `wrap_index` (secondary track only): index of the final loop-closure key,
    which sits at frame 1 after the last key (verified on all 10 samples).
    """

    def __init__(self, name, keys, x0, z0, y0, wrap_index=None):
        self.name = name
        self.keys = keys
        self.x0 = x0
        self.z0 = z0
        self.y0 = y0
        self.wrap_index = wrap_index

    @property
    def last_frame(self):
        keys = self.keys[:self.wrap_index] if self.wrap_index is not None else self.keys
        return keys[-1].frame if keys else 0

    def sample(self, frame):
        """Linear sample of (x, y, z) at a frame; clamps outside the key range.

        The frame-0 key is the header default (x=0, z=z0, y=y0). The loop-closure
        key (if any) is not used for interpolation.
        """
        keys = self.keys[:self.wrap_index] if self.wrap_index is not None else self.keys
        pts = [(0, self.x0, self.z0, self.y0)] + [(k.frame, k.x, k.z, k.y) for k in keys]
        if frame <= pts[0][0]:
            return (pts[0][1], pts[0][3], pts[0][2])  # x, y, z
        for i in range(1, len(pts)):
            f0, x0, z0, y0 = pts[i - 1]
            f1, x1, z1, y1 = pts[i]
            if frame <= f1:
                t = 0.0 if f1 == f0 else (frame - f0) / float(f1 - f0)
                x = x0 + (x1 - x0) * t
                y = y0 + (y1 - y0) * t
                z = z0 + (z1 - z0) * t
                return (x, y, z)
        f0, x0, z0, y0 = pts[-1]
        return (x0, y0, z0)


class ManiFile(object):
    def __init__(self, path=None):
        self.path = path
        self.duration = 0.0
        self.set_meta = None      # class-25 meta tuple
        self.track = None         # Track A
        self.secondary = None     # Track B
        self.variant = "unknown"

    def __repr__(self):
        return "ManiFile(%s variant=%s dur=%.0f A=%d keys B=%s)" % (
            self.path, self.variant, self.duration,
            len(self.track.keys) if self.track else -1,
            len(self.secondary.keys) if self.secondary else -1)


def parse_bytes(data, path=None):
    m = ManiFile(path)
    if len(data) < HEADER or _u32(data, 0) != ACON:
        raise ManiError("not an ACON container")
    cls0 = _u32(data, 4)
    if any(data[8 + i] for i in range(32)):
        raise ManiError("section-0 reserved bytes not zero")

    if cls0 == CLASS_CAMERA_ANI_SET:
        # 8 zero bytes + 16-byte meta, next section at 64.
        if len(data) < 64 or any(data[40 + i] for i in range(8)):
            raise ManiError("class-25 payload prefix not zero")
        meta = (_u32(data, 48), _f32(data, 52), _u32(data, 56), _u32(data, 60))
        m.set_meta = meta
        if _u32(data, 64) != ACON:
            raise ManiError("missing class-10 section at 64")
        cls1 = _u32(data, 68)
        if cls1 != CLASS_CAMERA_TRACK:
            raise ManiError("expected class 10, got %d" % cls1)
        if any(data[72 + i] for i in range(32)):
            raise ManiError("class-10 reserved bytes not zero")
        off = 104
        m.variant = "cameradata"
    elif cls0 == CLASS_CAMERA_TRACK:
        if any(data[8 + i] for i in range(32)):
            raise ManiError("class-10 reserved bytes not zero")
        off = 40
        m.variant = "class10-only"
    else:
        raise ManiError("unsupported class %d" % cls0)

    if any(data[off + i] for i in range(8)):
        raise ManiError("payload prefix not zero")
    off += 8

    def take(n):
        nonlocal off
        if off + n > len(data):
            raise ManiError("truncated payload")
        o = off
        off += n
        return o

    e0 = take(16)
    kind, dur, third, fourth = _u32(data, e0), _f32(data, e0 + 4), _u32(data, e0 + 8), _u32(data, e0 + 12)
    m.duration = dur
    if kind != 1:
        raise ManiError("meta kind %d" % kind)
    if cls0 == CLASS_CAMERA_ANI_SET:
        if (third, fourth) != (1, 0):
            raise ManiError("cameradata meta marker mismatch: %d,%d" % (third, fourth))
    else:
        if (third, fourth) != (0, 1):
            raise ManiError("rush-variant grammar not decoded (meta %d,%d)" % (third, fourth))

    e1 = take(16)
    n_a = _u32(data, e1)
    x0 = _f32(data, e1 + 4)
    z0 = _f32(data, e1 + 8)
    y0 = _f32(data, e1 + 12)
    if n_a < 1:
        raise ManiError("nA=%d" % n_a)
    keys_a = []
    prev = -1
    for _ in range(n_a - 1):
        o = take(16)
        x, frame, z, y = _f32(data, o), _u32(data, o + 4), _f32(data, o + 8), _f32(data, o + 12)
        if frame <= prev:
            raise ManiError("A frames not increasing at %d" % frame)
        prev = frame
        keys_a.append(Key(frame, x, y, z))
    m.track = Track("A", keys_a, x0, z0, y0)

    et = take(16)
    n_b = _u32(data, et + 8)
    if n_b > 0:
        keys_b = []
        prev = -1
        wrap_index = None
        for i in range(n_b):
            o = take(16)
            x, a, bb, frame = _f32(data, o), _f32(data, o + 4), _f32(data, o + 8), _u32(data, o + 12)
            if frame <= prev:
                # The shipped files close the loop with a final key at frame 1
                # (verified on all 10 cameradata samples: 13_0, 1_0, 21_2, 23_0,
                # 30_1, 105_0..3, 108_0).
                if i == n_b - 1 and frame == 1:
                    wrap_index = i
                else:
                    raise ManiError("B frames not increasing at %d" % frame)
            prev = frame
            keys_b.append(Key(frame, x, a, bb))
        m.secondary = Track("B", keys_b, 0.0, 0.0, wrap_index)

    if off != len(data):
        raise ManiError("payload not fully consumed: %d trailing bytes" % (len(data) - off))
    return m


def parse_file(path):
    return parse_bytes(Path(path).read_bytes(), str(path))


# ---------------------------------------------------------------- synthetic writer

def _p32(v):
    return struct.pack("<I", v)


def _pf(v):
    return struct.pack("<f", v)


def build_cameradata(duration, x0, z0, y0, keys_a, keys_b):
    """Inverse writer for the cameradata variant (used by --selftest)."""
    out = bytearray()
    out += _p32(ACON) + _p32(CLASS_CAMERA_ANI_SET) + b"\0" * 32
    out += b"\0" * 8 + _p32(1) + _pf(duration) + _p32(0) + _p32(1)
    out += _p32(ACON) + _p32(CLASS_CAMERA_TRACK) + b"\0" * 32
    out += b"\0" * 8
    out += _p32(1) + _pf(duration) + _p32(1) + _p32(0)
    out += _p32(len(keys_a) + 1) + _pf(x0) + _pf(z0) + _pf(y0)
    for (x, frame, z, y) in keys_a:
        out += _pf(x) + _p32(frame) + _pf(z) + _pf(y)
    out += _pf(0.0) + _p32(0) + _p32(len(keys_b)) + _p32(0)
    for (x, a, b, frame) in keys_b:
        out += _pf(x) + _pf(a) + _pf(b) + _p32(frame)
    return bytes(out)


def selftest():
    checks = 0
    keys_a = [(10.0, 1, 20.0, 30.0), (11.0, 5, 21.0, 31.0), (12.0, 9, 22.0, 32.0)]
    keys_b = [(1.0, 2.0, 3.0, 1), (4.0, 5.0, 6.0, 9)]
    data = build_cameradata(10.0, 0.0, 20.0, 30.0, keys_a, keys_b)
    m = parse_bytes(data, "<synth>")
    assert m.variant == "cameradata"; checks += 1
    assert m.duration == 10.0; checks += 1
    assert m.set_meta == (1, 10.0, 0, 1); checks += 1
    assert len(m.track.keys) == 3 and m.track.keys[-1].frame == 9; checks += 1
    assert m.track.z0 == 20.0 and m.track.y0 == 30.0; checks += 1
    assert len(m.secondary.keys) == 2; checks += 1
    x, y, z = m.track.sample(3)
    assert abs(x - 10.5) < 1e-6 and abs(z - 20.5) < 1e-6 and abs(y - 30.5) < 1e-6; checks += 1
    x, y, z = m.track.sample(-5)
    assert (x, y, z) == (0.0, 30.0, 20.0); checks += 1
    x, y, z = m.track.sample(99)
    assert (x, y, z) == (12.0, 32.0, 22.0); checks += 1
    for bad in (b"", data[:20], b"NOPE" + data[4:], data[:-4],
                data[:64] + b"NOPE" + data[68:]):
        try:
            parse_bytes(bad)
        except ManiError:
            checks += 1
        else:
            raise AssertionError("bad input accepted: %r" % bad[:16])
    print("mani_probe selftest: %d/%d PASS" % (checks, checks))
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description="JX3 .mani (ACON) camera track probe")
    ap.add_argument("file", nargs="*", help="path(s) to .mani file(s)")
    ap.add_argument("--verify", metavar="DIR", help="parse every *.mani under DIR")
    ap.add_argument("--selftest", action="store_true", help="offline round-trip checks")
    ap.add_argument("--keys", action="store_true", help="print track keys")
    ap.add_argument("--tsv", metavar="OUT", help="append keys as TSV rows (proof table)")
    ap.add_argument("--limit", type=int, default=12, help="keys per track to print")
    args = ap.parse_args(argv)

    if args.selftest:
        return selftest()
    if args.verify:
        root = Path(args.verify)
        files = sorted(root.rglob("*.mani"))
        if not files:
            print("no .mani under %s" % root)
            return 1
        ok = 0
        for f in files:
            try:
                m = parse_file(f)
            except ManiError as e:
                print("FAIL %-28s %s" % (f.name, e))
                continue
            ok += 1
            print("OK   %-28s variant=%-13s dur=%-7.0f A=%-5d B=%-5d last=%d" % (
                f.name, m.variant, m.duration,
                len(m.track.keys), len(m.secondary.keys) if m.secondary else 0,
                m.track.last_frame))
        print("%d/%d parsed" % (ok, len(files)))
        return 0 if ok == len(files) else 1
    if not args.file:
        ap.print_help()
        return 1
    if args.tsv:
        out = Path(args.tsv)
        new = not out.exists()
        with out.open("a", encoding="utf-8", newline="\n") as fh:
            if new:
                fh.write("file\ttrack\tframe\tx\ty\tz\twrap\n")
            for path in args.file:
                m = parse_file(path)
                name = Path(path).name
                for tr in (m.track, m.secondary):
                    if tr is None:
                        continue
                    for i, k in enumerate(tr.keys):
                        wrap = 1 if (tr.wrap_index is not None and i == tr.wrap_index) else 0
                        fh.write("%s\t%s\t%d\t%.3f\t%.3f\t%.3f\t%d\n" % (
                            name, tr.name, k.frame, k.x, k.y, k.z, wrap))
        print("tsv appended: %s" % out)
    for path in args.file:
        m = parse_file(path)
        print(m)
        for tr in (m.track, m.secondary):
            if tr is None:
                continue
            print("track %s: n=%d wrap=%s" % (tr.name, len(tr.keys), tr.wrap_index))
            for k in tr.keys[:args.limit]:
                print("  %s" % k)
            if len(tr.keys) > args.limit:
                print("  ... (%d more)" % (len(tr.keys) - args.limit))
    return 0


if __name__ == "__main__":
    sys.exit(main())
