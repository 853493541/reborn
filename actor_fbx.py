"""Map-viewer F1 FBX actor preset (花萝) — shared by Dev3 viewport host + Dev4 draw.

Mirrors actor-animation-player.js ensurePlayerAnchorRig preference:
  ACTOR_PRESET_BY_BODY_TYPE['f1'] = '花萝'
  samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx + tex/
"""
from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parent
PRESET_DIR = ROOT / "samples" / "actor_presets" / "f1_hualuo"
FBX_PATH = PRESET_DIR / "hualuo_no_anim.fbx"
TEX_DIR = PRESET_DIR / "tex"
VIEWPORT_DIR = ROOT / "viewport_fbx"

BODY_PRESET = {
    "F1": PRESET_DIR,
    "f1": PRESET_DIR,
}


def preset_for_body(body: str) -> Path | None:
    d = BODY_PRESET.get((body or "").strip()) or BODY_PRESET.get((body or "").strip().upper())
    if d is None:
        return None
    fbx = d / "hualuo_no_anim.fbx"
    return d if fbx.is_file() else None


def fbx_ready() -> bool:
    return FBX_PATH.is_file() and TEX_DIR.is_dir()
