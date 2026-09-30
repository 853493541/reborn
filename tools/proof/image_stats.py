"""Numeric fingerprint for images - inspect screenshots without attaching them.

The model API caps images per request (30); a long session that Reads image files
fails with "Too many images in request" and every later request in that session
keeps failing. Use this tool instead: dimensions, hash, and per-region mean RGB.

Usage:
    .venv\\Scripts\\python.exe tools\\proof\\image_stats.py <image> [<image>...]
        [--grid 4x4]

Examples:
    .venv\\Scripts\\python.exe tools\\proof\\image_stats.py proof/ui/evidence/mode_select_battlefield.png
    .venv\\Scripts\\python.exe tools\\proof\\image_stats.py C:\\SeasunGame\\MovieEditor\\bin64\\reborn_out\\rc_00_3000ms.png --grid 8x8
"""

import argparse
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


def main():
    ap = argparse.ArgumentParser(description="Image numeric fingerprint (no attachments).")
    ap.add_argument("images", nargs="+")
    ap.add_argument("--grid", default="4x4", help="region grid, e.g. 4x4 (default) or 8x8")
    args = ap.parse_args()
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
