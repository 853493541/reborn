"""Numeric fingerprint for images - inspect screenshots without attaching them.

The model API caps images per request (30); a long session that Reads image files
fails with "Too many images in request" and every later request in that session
keeps failing. Use this tool instead: dimensions, hash, and per-region mean RGB.

Usage:
    .venv\\Scripts\\python.exe tools\\proof\\image_stats.py <image> [<image>...]
        [--grid 4x4]
    .venv\\Scripts\\python.exe tools\\proof\\image_stats.py <glob> --series
        [--crop 0.3,0.2,0.7,0.8] [--thresh 200]

Examples:
    .venv\\Scripts\\python.exe tools\\proof\\image_stats.py proof/ui/evidence/mode_select_battlefield.png
    .venv\\Scripts\\python.exe tools\\proof\\image_stats.py C:\\SeasunGame\\MovieEditor\\bin64\\reborn_out\\rc_00_3000ms.png --grid 8x8
    .venv\\Scripts\\python.exe tools\\proof\\image_stats.py "reborn_out\\rc_*.png" --series --crop 0.3,0.2,0.7,0.8

The --series mode is for a time-ordered capture sequence (e.g. an effect over
its authored span): it prints one line per frame with the full-frame mean and
the centre-crop mean + bright-pixel count (`hot`, max channel > --thresh), plus a
sparkline, so a re-burst shows up as periodic dips in `hot` without attaching images.
"""

import argparse
import glob
import hashlib
import sys
from pathlib import Path


def _hex(px):
    return "%02X%02X%02X" % (px[0], px[1], px[2])


def stats(path, gx, gy):
    from PIL import Image  # .venv has Pillow (matplotlib dependency)

    resample = getattr(Image, "Resampling", Image).BOX  # area average
    data = path.read_bytes()
    sha = hashlib.sha256(data).hexdigest()
    im = Image.open(path).convert("RGB")
    w, h = im.size
    print(str(path))
    print("  size=%dx%d mode=%s bytes=%d sha256=%s" % (w, h, im.mode, len(data), sha[:16]))
    total = [0, 0, 0]
    rows = []
    for cy in range(gy):
        row = []
        for cx in range(gx):
            box = (cx * w // gx, cy * h // gy, (cx + 1) * w // gx, (cy + 1) * h // gy)
            r, g, b = im.crop(box).resize((1, 1), resample).getpixel((0, 0))
            total[0] += r
            total[1] += g
            total[2] += b
            row.append("%02X%02X%02X" % (r, g, b))
        rows.append(row)
    cells = gx * gy
    print("  mean=#%s" % _hex((total[0] // cells, total[1] // cells, total[2] // cells)))
    for row in rows:
        print("  " + " ".join(row))


def series(patterns, crop, thresh):
    from PIL import Image  # .venv has Pillow (matplotlib dependency)

    paths = []
    for pat in patterns:
        paths.extend(sorted(Path(p) for p in glob.glob(pat)))
    paths = [p for p in paths if p.is_file()]
    if not paths:
        sys.exit("no files matched")

    x0f, y0f, x1f, y1f = crop
    hots = []
    for p in paths:
        im = Image.open(p).convert("RGB")
        w, h = im.size
        box = (int(x0f * w), int(y0f * h), int(x1f * w), int(y1f * h))
        full = im.resize((1, 1), getattr(Image, "Resampling", Image).BOX).getpixel((0, 0))
        c = im.crop(box)
        cw, ch = c.size
        cmean = c.resize((1, 1), getattr(Image, "Resampling", Image).BOX).getpixel((0, 0))
        hot = 0
        for px in c.getdata():
            if px[0] > thresh or px[1] > thresh or px[2] > thresh:
                hot += 1
        hots.append(hot)
        print("%s full=#%s crop=%dx%d mean=#%s hot=%d" % (
            p.name, _hex(full), cw, ch, _hex(cmean), hot))

    lo, hi = min(hots), max(hots)
    span = (hi - lo) or 1
    ramp = " .:-=+*#%@"
    line = "".join(ramp[min(9, (v - lo) * 9 // span)] for v in hots)
    print("hot range %d..%d  sparkline: %s" % (lo, hi, line))


def main():
    ap = argparse.ArgumentParser(description="Image numeric fingerprint (no attachments).")
    ap.add_argument("images", nargs="+")
    ap.add_argument("--grid", default="4x4", help="region grid, e.g. 4x4 (default) or 8x8")
    ap.add_argument("--series", action="store_true",
                    help="treat args as globs; print a per-frame brightness series")
    ap.add_argument("--crop", default="0.3,0.2,0.7,0.8",
                    help="series centre crop as fractions x0,y0,x1,y1")
    ap.add_argument("--thresh", type=int, default=200, help="series hot-pixel threshold")
    args = ap.parse_args()

    if args.series:
        crop = tuple(float(v) for v in args.crop.split(","))
        series(args.images, crop, args.thresh)
        return
    try:
        gx, gy = (int(v) for v in args.grid.lower().split("x"))
    except ValueError:
        sys.exit("bad --grid, use NxN (e.g. 4x4)")
    for p in args.images:
        path = Path(p)
        if not path.is_file():
            print("MISSING %s" % path)
            continue
        try:
            stats(path, gx, gy)
        except ImportError:
            sys.exit("PIL missing - run with .venv\\Scripts\\python.exe")
        except Exception as e:
            print("ERROR %s: %s" % (path, e))


if __name__ == "__main__":
    main()
