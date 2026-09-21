# -*- coding: utf-8 -*-
"""Replace helper-retarget block in both viewports; bump cache; capture mid."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
HELPER = (ROOT / "web" / "_helper_retarget_fn.js").read_text(encoding="utf-8")
if "alignHelperBindToFbx" not in HELPER:
    raise SystemExit("helper js missing alignHelperBindToFbx")


def patch_viewport(path: Path) -> None:
    t = path.read_text(encoding="utf-8")
    # Ensure SkeletonUtils import
    if "SkeletonUtils" not in t:
        t = t.replace(
            "import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';",
            "import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';\n"
            "import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';",
            1,
        )

    # Remove prior helper block: from Tony-path comment or findBodySkinnedMeshForRetarget /
    # findPrimary (renamed) through end of retargetClipViaHelper function.
    # Prefer markers.
    start_marks = [
        "/** MIN2 parent-local",
        "function findBodySkinnedMeshForRetarget",
        "function findPrimarySkinnedMesh(rootObj)",  # old name if present
    ]
    start = -1
    for m in start_marks:
        i = t.find(m)
        if i >= 0 and (start < 0 or i < start):
            # only take findPrimary if it returns { mesh, boneCount
            if "findPrimarySkinnedMesh(rootObj)" in m:
                if "{ mesh:" not in t[i : i + 400] and "boneCount" not in t[i : i + 400]:
                    continue
            start = i
    if start < 0:
        raise SystemExit(f"no helper start in {path}")

    end_pat = re.compile(r"\nfunction retargetClipViaHelper\b")
    m = end_pat.search(t, start)
    if not m:
        # maybe already only partial — find function retargetClipViaHelper anywhere after start
        m = re.search(r"function retargetClipViaHelper\b", t[start:])
        if not m:
            raise SystemExit(f"no retargetClipViaHelper in {path}")
        abs_i = start + m.start()
    else:
        abs_i = m.start()

    # Find closing brace of retargetClipViaHelper: scan from function start
    fn_start = t.find("{", abs_i)
    depth = 0
    i = fn_start
    while i < len(t):
        c = t[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                i += 1
                break
        i += 1
    else:
        raise SystemExit(f"unclosed retargetClipViaHelper in {path}")

    # Skip trailing newlines
    while i < len(t) and t[i] in "\r\n":
        i += 1

    new_t = t[:start] + HELPER.strip() + "\n\n" + t[i:]

    # Ensure space hook
    if "min2HelperRetarget" not in new_t:
        raise SystemExit(f"lost min2HelperRetarget hook in {path}")
    if "alignHelperBindToFbx" not in new_t:
        raise SystemExit(f"insert failed {path}")

    # Deduplicate _normNameKey if helper brought one and file had one earlier
    decls = [m.start() for m in re.finditer(r"function _normNameKey\b", new_t)]
    if len(decls) > 1:
        # remove first (old) short one before helper — keep the one inside HELPER block
        # remove declarations that are NOT the one we just inserted (near start marker position)
        helper_pos = new_t.find("alignHelperBindToFbx")
        for d in sorted(decls, reverse=True):
            if d < helper_pos - 5000 or d > helper_pos:
                # remove this decl block (3 lines)
                end = new_t.find("\n}", d)
                if end > 0:
                    end = new_t.find("\n", end + 2) + 1
                    new_t = new_t[:d] + new_t[end:]

    path.write_text(new_t, encoding="utf-8")
    print("patched", path, "hasAlign", "alignHelperBindToFbx" in path.read_text(encoding="utf-8"))


def bump_html(p: Path) -> None:
    if not p.is_file():
        return
    t = p.read_text(encoding="utf-8")

    def bump(m):
        return f"fbx_viewport.js?v={int(m.group(1)) + 1}"

    nt, c = re.subn(r"fbx_viewport\.js\?v=(\d+)", bump, t)
    if c:
        p.write_text(nt, encoding="utf-8")
        print("bumped", p)


def capture() -> dict:
    sys.path.insert(0, str(ROOT))
    from fbx_actor import (  # type: ignore
        CLIP_JSON_FLWS_CHARGE,
        _capture_with_playwright,
        ensure_server,
        load_fbx_actor,
        web_viewport_url,
    )

    actor = load_fbx_actor()
    ensure_server(actor)
    mid = ROOT / "proof/compare/hualuo_flws_charge_mid.png"
    wide = ROOT / "proof/compare/hualuo_flws_charge_wide.png"
    meta = _capture_with_playwright(
        web_viewport_url(actor, clip_json=CLIP_JSON_FLWS_CHARGE, t=0.68, closeup=True),
        mid,
        timeout_ms=180000,
    )
    meta2 = _capture_with_playwright(
        web_viewport_url(actor, clip_json=CLIP_JSON_FLWS_CHARGE, t=0.68, closeup=False),
        wide,
        timeout_ms=120000,
    )
    return {"mid": meta, "wide": meta2, "mid_path": str(mid), "wide_path": str(wide)}


def main() -> None:
    # Prefer repo root when copied beside project files
    global ROOT
    if not (ROOT / "web" / "fbx_viewport.js").is_file() and (ROOT.parent / "web" / "fbx_viewport.js").is_file():
        ROOT = ROOT.parent
    for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
        patch_viewport(ROOT / rel)
    bump_html(ROOT / "web" / "fbx_viewport.html")
    bump_html(ROOT / "viewport_fbx" / "index.html")
    print("CAPTURE", capture())
    print("DONE_AXIS_BIND")


if __name__ == "__main__":
    main()
