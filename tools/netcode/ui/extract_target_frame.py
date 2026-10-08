#!/usr/bin/env python3
"""Extract the client's real target-frame UI (layout + UITex atlases + textures).

Read-only against the game client install (official PakV4SfxExtract); outputs
land in the git-ignored assets/ui/targetframe/ tree. The client's target HUD
renders from these files - no hand-drawn UI.

Sources (all from the game client paks):
  ui/Config/Default/TargetTarget.ini        selected-target window (Target.lua)
  ui/Config/Default/TargetCommon.ini        per-school target elements
  ui/Config/Default/TargetBuff.ini          target buff rows
  ui/Config/Default/TargetDeBuff.ini        target debuff rows
  ui/Config/Default/TargetSkill.ini         target skill row
  ui/Config/Default/Player.ini              player frame reference layout
  ui/Image/.../ *.UITex + the .Tga/.dds texture each atlas names
  ui/Scheme/Elem/{font.ini,fontlist.ini,color.txt,fontpathlist.ini}

Fonts themselves are loaded read-only from the game client install
(ui/Font/*.ttf), not copied here.

Usage:
  python tools/netcode/ui/extract_target_frame.py
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT))

import pss_assets  # noqa: E402

LAYOUTS = [
    r"ui/Config/Default/TargetTarget.ini",
    r"ui/Config/Default/TargetCommon.ini",
    r"ui/Config/Default/TargetBuff.ini",
    r"ui/Config/Default/TargetDeBuff.ini",
    r"ui/Config/Default/TargetSkill.ini",
    r"ui/Config/Default/Player.ini",
]
SCHEME = [
    r"ui/Scheme/Elem/font.ini",
    r"ui/Scheme/Elem/fontlist.ini",
    r"ui/Scheme/Elem/color.txt",
    r"ui/Scheme/Elem/fontpathlist.ini",
]
ATLASES = [
    r"ui/Image/Common/ProgressBar.UITex",
    r"ui/Image/Minimap/Minimap.UITex",
    r"ui/Image/PlayerAvatar/AvatarBgTest.UITex",
    r"ui/Image/PlayerAvatar/CHGJ_1_DK_2.UITex",
    r"ui/Image/TargetPanel/Target.UITex",
    r"ui/Image/TargetPanel/Player.UITex",
    r"ui/Image/TargetPanel/FramePlayer.UITex",
    r"ui/Image/UICommon/Baizhan.UITex",
    r"ui/Image/UICommon/CommonPanel.UITex",
    r"ui/Image/UICommon/CommonPanel2.UITex",
    r"ui/Image/UItimate/UICommon/Player.UITex",
    r"ui/Image/UItimate/UICommon/TargetBg.UITex",
]
AVATAR = [
    r"ui/Image/PlayerAvatar/HYGY_001.dds",
]


def texture_name(data: bytes) -> str | None:
    if len(data) < 92 or data[0:2] != b"UI":
        return None
    return data[24:88].split(b"\x00", 1)[0].decode("ascii", "replace").strip()


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--out-dir", type=Path, default=ROOT / "assets" / "ui" / "targetframe")
    args = ap.parse_args(argv)
    out_dir = args.out_dir.resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    wanted = LAYOUTS + SCHEME + ATLASES + AVATAR
    found = pss_assets.run_pakv4(wanted, work=out_dir / "_work")
    saved: dict[str, bytes] = {}
    for key, data in found.items():
        saved[key.lstrip("/")] = data

    # Each atlas names its texture file next to it (e.g. Target.Tga).
    extra: list[str] = []
    for key, data in list(saved.items()):
        if key.lower().endswith(".uitex"):
            name = texture_name(data)
            if name:
                extra.append(key.rsplit("/", 1)[0] + "/" + name)
    if extra:
        more = pss_assets.run_pakv4(sorted(set(extra)), work=out_dir / "_work2")
        for key, data in more.items():
            saved.setdefault(key.lstrip("/"), data)

    for key, data in sorted(saved.items()):
        dest = out_dir / key
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(data)
        print("HIT  %-56s %8d bytes" % (key, len(data)))
    missing = [p for p in wanted if p not in saved]
    for p in missing:
        print("MISS %s" % p)
    print("WROTE %s (%d files)" % (out_dir, len(saved)))
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
