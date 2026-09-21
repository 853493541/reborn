# -*- coding: utf-8 -*-
"""Andy one-shot: MIN2 helper-skel → SkeletonUtils.retargetClip → Mixer capture.

Run on Andy:
  cd "C:\\Users\\Zhibin Ren\\jx3-ani-player"
  .venv\\Scripts\\python.exe APPLY_HELPER_RETARGET_ANDY.py

Constraints: KEEP Mixer; NO bone.decompose / apply_pose spray for FLWS.
"""
from __future__ import annotations

import json
import re
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

HELPER_JS = (ROOT / "web" / "_helper_retarget_fn.js").read_text(encoding="utf-8")
# Fallback if only delivered beside this script
if "retargetClipViaHelper" not in HELPER_JS:
    alt = Path(__file__).with_name("_helper_retarget_fn.js")
    if alt.is_file():
        HELPER_JS = alt.read_text(encoding="utf-8")


def _bump_html(html_path: Path) -> None:
    if not html_path.is_file():
        return
    t = html_path.read_text(encoding="utf-8")
    # fbx_viewport.js?v=N → bump
    def bump(m):
        n = int(m.group(1) or 0) + 1
        return f"fbx_viewport.js?v={n}"

    nt, c = re.subn(r"fbx_viewport\.js\?v=(\d+)", bump, t)
    if c == 0:
        nt, c = re.subn(
            r"fbx_viewport\.js(\"|')",
            r"fbx_viewport.js?v=1\1",
            t,
            count=1,
        )
    if c:
        html_path.write_text(nt, encoding="utf-8")
        print("bumped", html_path)
    else:
        print("WARN no ?v= bump mark in", html_path)


def patch_viewport(path: Path) -> None:
    t = path.read_text(encoding="utf-8")
    orig = t

    # 1) Import SkeletonUtils
    if "SkeletonUtils" not in t:
        if "from 'three/addons/loaders/FBXLoader.js'" in t:
            t = t.replace(
                "import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';",
                "import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';\n"
                "import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';",
                1,
            )
        else:
            raise SystemExit(f"FBXLoader import missing in {path}")

    # 2) Insert helper fns once (before animationClipFromJson or startMixerClip)
    if "function retargetClipViaHelper" not in t:
        mark = None
        for cand in (
            "function animationClipFromWorldUuid",
            "function animationClipFromWorldJson",
            "function animationClipFromJson",
            "async function startMixerClip",
        ):
            if cand in t:
                mark = cand
                break
        if not mark:
            raise SystemExit(f"no insert mark in {path}")
        # Ensure timesMax exists for animationClipFromTracksPayload
        if "function timesMax" not in t:
            HELPER_JS_FULL = (
                "function timesMax(tracks) {\n"
                "  let m = 0;\n"
                "  for (const tr of tracks) {\n"
                "    const arr = tr.times;\n"
                "    if (arr && arr.length) m = Math.max(m, arr[arr.length - 1]);\n"
                "  }\n"
                "  return m;\n"
                "}\n\n"
                + HELPER_JS
            )
        else:
            HELPER_JS_FULL = HELPER_JS
        t = t.replace(mark, HELPER_JS_FULL + "\n" + mark, 1)

    # 3) Hook space min2HelperRetarget at top of animationClipFromJson — prefer startMixerClip path instead
    # Rewrite startMixerClip to use retarget when space matches.
    if "min2HelperRetarget" not in t.split("async function startMixerClip", 1)[-1][:2500]:
        # Replace the clip-building portion of startMixerClip
        old = """async function startMixerClip(rootObj, clipUrl) {
  const payload = await loadClipJson(clipUrl);
  const clip = animationClipFromJson(payload, rootObj);"""
        new = """async function startMixerClip(rootObj, clipUrl) {
  const payload = await loadClipJson(clipUrl);
  let clip;
  let mixerRoot = rootObj;
  let helperMeta = null;
  if (payload && payload.space === 'min2HelperRetarget') {
    const out = retargetClipViaHelper(payload, rootObj);
    clip = out.clip;
    mixerRoot = out.mixerRoot;
    helperMeta = out.meta;
  } else {
    clip = (typeof animationClipFromJson === 'function')
      ? animationClipFromJson(payload, rootObj)
      : animationClipFromJson(payload);
  }"""
        # Also handle signature without rootObj
        old2 = """async function startMixerClip(rootObj, clipUrl) {
  const payload = await loadClipJson(clipUrl);
  const clip = animationClipFromJson(payload);"""
        if old in t:
            t = t.replace(old, new, 1)
        elif old2 in t:
            t = t.replace(old2, new, 1)
        else:
            # Try looser: after loadClipJson, before AnimationMixer
            m = re.search(
                r"(async function startMixerClip\([^)]*\) \{\s*"
                r"const payload = await loadClipJson\(clipUrl\);\s*)"
                r"(const clip = animationClipFromJson\([^;]+;)",
                t,
            )
            if not m:
                raise SystemExit(f"startMixerClip clip= line not found in {path}")
            t = t[: m.start(2)] + (
                "let clip;\n"
                "  let mixerRoot = rootObj;\n"
                "  let helperMeta = null;\n"
                "  if (payload && payload.space === 'min2HelperRetarget') {\n"
                "    const out = retargetClipViaHelper(payload, rootObj);\n"
                "    clip = out.clip;\n"
                "    mixerRoot = out.mixerRoot;\n"
                "    helperMeta = out.meta;\n"
                "  } else {\n"
                "    clip = animationClipFromJson(payload, rootObj);\n"
                "  }"
            ) + t[m.end(2) :]

        # Mixer must bind to mixerRoot (SkinnedMesh for retarget tracks)
        t2 = t
        # Only within startMixerClip: first AnimationMixer(rootObj) → mixerRoot
        i = t.find("async function startMixerClip")
        j = t.find("\nasync function ", i + 1)
        if j < 0:
            j = t.find("\nfunction ", i + 1)
        block = t[i:j] if j > i else t[i:]
        block2 = block.replace(
            "mixer = new THREE.AnimationMixer(rootObj);",
            "mixer = new THREE.AnimationMixer(mixerRoot || rootObj);",
            1,
        )
        block2 = block2.replace(
            "hud.textContent += ` · mixer ${payload.name} t=${mixer.time.toFixed(3)}s tracks=${clip.tracks.length}`;",
            "hud.textContent += ` · mixer ${payload.name} t=${mixer.time.toFixed(3)}s tracks=${clip.tracks.length}` + (helperMeta ? ` map=${helperMeta.matched}/${helperMeta.total}` : '');",
            1,
        )
        # Enrich __MIXER_READY__
        if "helperMeta" not in block2:
            block2 = block2.replace(
                "window.__MIXER_READY__ = {",
                "window.__MIXER_READY__ = {\n"
                "    helperMatched: helperMeta?.matched,\n"
                "    helperTotal: helperMeta?.total,\n"
                "    helperMapPct: helperMeta?.mapPct,\n"
                "    helperMesh: helperMeta?.meshName,\n"
                "    helperHip: helperMeta?.hip,\n",
                1,
            )
        # Ensure space reported
        if "space: payload.space" not in block2 and "space:" not in block2.split("__MIXER_READY__", 1)[-1][:400]:
            block2 = block2.replace(
                "quatOnly: !!payload.quat_only,",
                "quatOnly: !!payload.quat_only,\n"
                "    space: payload.space || 'local',\n"
                "    matched: helperMeta?.matched ?? clip.userData?.worldRetargetMatched,",
                1,
            )
        t = t[:i] + block2 + (t[j:] if j > i else "")

    if t == orig:
        print("no textual change?", path)
    path.write_text(t, encoding="utf-8")
    print(
        "patched",
        path,
        "hasHelper",
        "retargetClipViaHelper" in path.read_text(encoding="utf-8"),
        "hasSU",
        "SkeletonUtils" in path.read_text(encoding="utf-8"),
    )


def export_clips() -> dict:
    from export_min2_helper_clip import export_helper

    out = {}
    charge = ROOT / "samples/player/moves/f1s07cj重剑技能15蓄力_奇穴.ani"
    cast = ROOT / "samples/player/moves/fenglaiwushan/f1s07cj重剑技能15.ani"
    for ani, out_name, clip_name in (
        (charge, "clip_flws_charge.json", "风来吴山·蓄力"),
        (cast, "clip_flws_cast.json", "风来吴山·释放"),
    ):
        if not ani.is_file():
            print("MISSING_ANI", ani)
            continue
        meta = export_helper(ani, ROOT / "web" / "runtime" / out_name, name=clip_name)
        out[out_name] = meta
        print("EXPORTED", meta)
    return out


def write_diag(export_meta: dict) -> None:
    diag = ROOT / "proof" / "compare" / "flws_retarget_diag.md"
    diag.parent.mkdir(parents=True, exist_ok=True)
    # Dump FBX bone parent sample via existing dump if present
    charge = export_meta.get("clip_flws_charge.json") or {}
    lines = [
        "# FLWS retarget diagnosis (helper-skel + SkeletonUtils.retargetClip)",
        "",
        "## MIN2 space",
        "- `positions_at` / `quats_at`: **WORLD** (see min2.Min2SkelClip docstring).",
        "- `local_quats_at` / `_local_positions`: **parent-local** (used by this export).",
        "- worldUuid shred root cause hypothesis: applying one world pose (name-keyed) onto",
        "  **all duplicate-named bones** across multi-mesh (weapon/hair) with FBX parent graph ≠ MINA parents,",
        "  plus Z-up→Y-up baking into wrong locals → Mixer still plays but mesh shreds.",
        "",
        "## Approach (Tony authorized)",
        "MIN2 parent-local clip → helper Object3D/Bone hierarchy matching MINA parents →",
        "`THREE.SkeletonUtils.retargetClip` onto **primary body SkinnedMesh** → `AnimationMixer` on that mesh.",
        "No bone.decompose / apply_pose spray.",
        "",
        "## MIN2 charge parent graph (core)",
        "```",
        json.dumps(charge.get("diag_parents") or {}, ensure_ascii=False, indent=2),
        "```",
        "",
        f"- export bones={charge.get('bones')} tracks={charge.get('tracks')} frames={charge.get('frames')}",
        "",
        "## FBX note",
        "Name-only Map is wrong across duplicate Bone names; retargetClip iterates only",
        "`primary.skeleton.bones` (largest / hualuo-body biased SkinnedMesh).",
        "",
    ]
    diag.write_text("\n".join(lines), encoding="utf-8")
    print("wrote", diag)


def capture() -> dict:
    from urllib.parse import urlencode
    from fbx_actor import (
        load_fbx_actor,
        _capture_with_playwright,
        ensure_server,
    )

    actor = load_fbx_actor()
    ensure_server(actor)
    clip = ROOT / "web" / "runtime" / "clip_flws_charge.json"
    try:
        from fbx_actor import CLIP_JSON_FLWS_CHARGE
        if Path(CLIP_JSON_FLWS_CHARGE).is_file():
            clip = Path(CLIP_JSON_FLWS_CHARGE)
    except ImportError:
        pass

    def _url(closeup: bool) -> str:
        try:
            from fbx_actor import web_viewport_url
            return web_viewport_url(
                actor, closeup=closeup, clip_json=clip, t=0.68, playing=False
            )
        except TypeError:
            # Older web_viewport_url without clip_json — build query manually.
            q = {
                "fbx": "/samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx",
                "tex": "/samples/actor_presets/f1_hualuo/tex/",
                "closeup": "1" if closeup else "0",
                "clip": "/web/runtime/clip_flws_charge.json",
                "t": "0.6800",
            }
            return f"http://127.0.0.1:{actor._port}/web/fbx_viewport.html?{urlencode(q)}"

    url_mid = _url(True)
    url_wide = _url(False)
    mid = ROOT / "proof" / "compare" / "hualuo_flws_charge_mid.png"
    wide = ROOT / "proof" / "compare" / "hualuo_flws_charge_wide.png"
    print("URL_MID", url_mid)
    meta_mid = _capture_with_playwright(url_mid, mid)
    meta_wide = _capture_with_playwright(url_wide, wide)
    print("CAPTURE_MID", meta_mid)
    print("CAPTURE_WIDE", meta_wide)
    return {"mid": meta_mid, "wide": meta_wide, "url_mid": url_mid}


def main() -> None:
    # Ensure helper js + exporter present next to repo
    src_helper = ROOT / "web" / "_helper_retarget_fn.js"
    if not src_helper.is_file():
        raise SystemExit("missing web/_helper_retarget_fn.js")
    # Copy exporter if delivered as sibling
    for rel in ("export_min2_helper_clip.py", "mina.py", "min2.py"):
        pass

    for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
        p = ROOT / rel
        if p.is_file():
            patch_viewport(p)
        else:
            print("SKIP missing", p)

    for rel in ("web/fbx_viewport.html", "viewport_fbx/index.html", "viewport_fbx/fbx_viewport.html"):
        _bump_html(ROOT / rel)

    export_meta = export_clips()
    write_diag(export_meta)

    # Ensure CLIP_JSON constants exist (APPLY_FLWS may have added them)
    try:
        cap = capture()
        print("DONE_CAPTURE", json.dumps({k: (v if not isinstance(v, dict) else {kk: v.get(kk) for kk in ('png','bytes','mixer') if kk in v}) for k, v in cap.items()}, ensure_ascii=False))
    except Exception as exc:
        print("CAPTURE_FAIL", type(exc).__name__, exc)
        raise
    print("DONE_HELPER_RETARGET_ANDY")


if __name__ == "__main__":
    main()
