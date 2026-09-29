#!/usr/bin/env python3
"""Fetch + decode ability SFX into ability_picker/sound/<wem>.wav.

Pipeline per wem id:
  1. local caches (jx3ac StreamingAssets / map-viewer wwise extract / reborn assets)
  2. ONE batched PakV4SfxExtract.exe run with the logical paths
     data\\Wwiseaudio\\GeneratedSoundBanks\\Windows\\Base\\<id>.wem
  3. ww2ogg (Wwise Vorbis -> OGG)
  4. ffmpeg (imageio-ffmpeg) -> PCM16 WAV  [soundfile fallback]

Usage:
  python fetch_sounds.py --abilities 65161,64992,65671
  python fetch_sounds.py --wems 199467777,369499237
  python fetch_sounds.py --matched-only --per-ability 2
  python fetch_sounds.py --limit 5
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess

BIN64 = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64"
JX3AC = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\jx3ac\jx3ac_Data\StreamingAssets\Audio\GeneratedSoundBanks\Windows"
PAK_EXT = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer\cache-extraction\wwise-pak-extract\Windows\base"
WW2OGG = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer\tools\bin\ww2ogg\ww2ogg.exe"
PCB = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer\tools\bin\ww2ogg\packed_codebooks_aoTuV_603.bin"
REBORN_SND = r"C:\Users\Zhibin Ren\Desktop\reborn\assets\sound"
DATA = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data", "ability_candidates.json")
OUT = r"C:\SeasunGame\MovieEditor\bin64\ability_picker\sound"
LOGICAL = "data\\Wwiseaudio\\GeneratedSoundBanks\\Windows\\Base\\"
LOCAL_DIRS = [JX3AC, PAK_EXT, REBORN_SND]
MIN_WAV = 4096


def ffmpeg_exe() -> str:
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return ""


def load_items(args):
    j = json.load(open(DATA, encoding="utf-8"))
    abilities = j["abilities"]
    if args.wems:
        return [("", [w.strip() for w in args.wems.split(",") if w.strip()])]
    want_ids = {x.strip() for x in args.abilities.split(",") if x.strip()} if args.abilities else set()
    out = []
    for a in abilities:
        if not a.get("wems"):
            continue
        if want_ids and not (set(a.get("ids") or []) & want_ids):
            continue
        if args.matched_only and not a.get("matched"):
            continue
        if not want_ids and not args.matched_only and args.limit:
            if len(out) >= args.limit:
                break
        out.append((a["name"], a["wems"][: args.per_ability] if args.per_ability else a["wems"]))
    return out


def find_local(wem: str) -> str:
    for d in LOCAL_DIRS:
        p = os.path.join(d, wem + ".wem")
        if os.path.exists(p):
            return p
    return ""


def pak_extract_batch(wems, tmp) -> dict:
    """one extractor run for all missing wems -> {wem: wem_path}"""
    res = {}
    if not wems:
        return res
    plist = os.path.join(tmp, "pathlist.txt")
    with open(plist, "w", encoding="gb18030") as fh:
        for w in wems:
            fh.write(LOGICAL + w + ".wem\n")
    exe = os.path.join(BIN64, "PakV4SfxExtract.exe")
    outdir = os.path.join(tmp, "pak_out")
    os.makedirs(outdir, exist_ok=True)
    print(f"pak extract: {len(wems)} wems ...")
    try:
        r = subprocess.run([exe, plist, outdir], cwd=BIN64, capture_output=True, timeout=900)
        print("  rc", r.returncode)
    except Exception as exc:
        print("  pak extract failed:", exc)
        return res
    wanted = {w.lower() for w in wems}
    for root, _dirs, files in os.walk(outdir):
        for f in files:
            base = f[:-4] if f.lower().endswith(".wem") else ""
            if base.lower() in wanted:
                res[base.lower()] = os.path.join(root, f)
    return res


def wem_to_ogg(wem_path: str, wem: str, tmp: str) -> str:
    ogg = os.path.join(tmp, wem + ".ogg")
    if os.path.exists(ogg) and os.path.getsize(ogg) > 1024:
        return ogg
    try:
        subprocess.run([WW2OGG, wem_path, "--pcb", PCB, "-o", ogg], capture_output=True, timeout=180)
    except Exception:
        return ""
    return ogg if os.path.exists(ogg) else ""


def ogg_to_wav(ogg: str, wav: str) -> bool:
    ff = ffmpeg_exe()
    if ff:
        try:
            r = subprocess.run([ff, "-y", "-i", ogg, "-acodec", "pcm_s16le", wav],
                               capture_output=True, timeout=180)
            if r.returncode == 0 and os.path.exists(wav) and os.path.getsize(wav) > MIN_WAV:
                return True
        except Exception:
            pass
    try:
        import soundfile as sf
        data, rate = sf.read(ogg, dtype="int16")
        if len(data) == 0:
            return False
        sf.write(wav, data, rate, subtype="PCM_16")
        return os.path.exists(wav) and os.path.getsize(wav) > MIN_WAV
    except Exception:
        return False


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--abilities", default="")
    ap.add_argument("--wems", default="")
    ap.add_argument("--matched-only", action="store_true")
    ap.add_argument("--per-ability", type=int, default=2)
    ap.add_argument("--limit", type=int, default=5)
    ap.add_argument("--out", default=OUT)
    args = ap.parse_args()

    items = load_items(args)
    os.makedirs(args.out, exist_ok=True)
    tmp = os.path.join(args.out, "_tmp")
    os.makedirs(tmp, exist_ok=True)

    todo = []          # (name, wem)
    for name, wems in items:
        for w in wems:
            todo.append((name, w))

    ok = skip = 0
    missing = []       # wems needing pak extraction
    local = {}         # wem -> local path
    for name, w in todo:
        target = os.path.join(args.out, w + ".wav")
        if os.path.exists(target) and os.path.getsize(target) > MIN_WAV:
            skip += 1
            continue
        p = find_local(w)
        if p:
            local[w] = p
        else:
            missing.append(w)

    pak = pak_extract_batch(sorted(set(missing)), tmp)
    fail = 0
    for name, w in todo:
        target = os.path.join(args.out, w + ".wav")
        if os.path.exists(target) and os.path.getsize(target) > MIN_WAV:
            continue
        src = local.get(w) or pak.get(w.lower(), "")
        if not src:
            print(f"[miss] {name} {w}")
            fail += 1
            continue
        ogg = wem_to_ogg(src, w, tmp)
        if not ogg:
            print(f"[fail] {name} {w} (ww2ogg)")
            fail += 1
            continue
        if ogg_to_wav(ogg, target):
            ok += 1
            print(f"[ok] {name} {w} {os.path.getsize(target)} bytes")
        else:
            print(f"[fail] {name} {w} (decode)")
            fail += 1

    print(f"done: ok={ok} skip={skip} fail={fail} -> {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
