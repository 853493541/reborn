# -*- coding: utf-8 -*-
from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parent
FN = (ROOT / "web" / "_uuid_retarget_fn.js").read_text(encoding="utf-8")


def patch(path: Path) -> None:
    t = path.read_text(encoding="utf-8")
    if "function animationClipFromWorldUuid" not in t:
        # Insert before animationClipFromWorldJson or animationClipFromJson
        mark = "function animationClipFromWorldJson"
        if mark not in t:
            mark = "function animationClipFromJson"
        if mark not in t:
            raise SystemExit(f"no insert mark in {path}")
        t = t.replace(mark, FN + "\n" + mark, 1)

    # Ensure _normNameKey exists
    if "function _normNameKey" not in t:
        t = t.replace(
            "function animationClipFromJson",
            "function _normNameKey(n) {\n"
            "  return String(n || '').toLowerCase().replace(/[\\s\\-]+/g, '_').trim();\n"
            "}\n\nfunction animationClipFromJson",
            1,
        )

    # Hook space worldUuid in animationClipFromJson
    if "worldUuid" not in t.split("function animationClipFromJson", 1)[-1][:800]:
        old = "  if (payload && payload.space === 'world') {"
        if old in t:
            t = t.replace(
                old,
                "  if (payload && payload.space === 'worldUuid') {\n"
                "    return animationClipFromWorldUuid(payload, rootObj || (typeof root !== 'undefined' ? root : null));\n"
                "  }\n"
                "  if (payload && payload.space === 'world') {",
                1,
            )
        else:
            # insert at start of animationClipFromJson body
            needle = "function animationClipFromJson(payload, rootObj) {\n"
            if needle not in t:
                needle = "function animationClipFromJson(payload) {\n"
            t = t.replace(
                needle,
                needle
                + "  if (payload && payload.space === 'worldUuid') {\n"
                + "    return animationClipFromWorldUuid(payload, rootObj || (typeof root !== 'undefined' ? root : null));\n"
                + "  }\n",
                1,
            )

    # After startMixerClip creates action, re-index bones (unique names) and set mixer flag
    if "indexBones(rootObj)" not in t[t.find("async function startMixerClip") : t.find("async function startMixerClip") + 2000]:
        old_play = "  clipAction.reset().play();"
        # find within startMixerClip only
        i = t.find("async function startMixerClip")
        j = t.find(old_play, i)
        if j > 0:
            t = (
                t[:j]
                + "  // Keep uniquified bone names so PropertyBinding hits every duplicate node.\n"
                + "  if (typeof indexBones === 'function') indexBones(rootObj);\n"
                + "  clipAction.reset().play();"
                + t[j + len(old_play) :]
            )

    # Update __MIXER_READY__ space reporting already uses payload.space

    path.write_text(t, encoding="utf-8")
    print("patched", path, "hasUuidFn", "function animationClipFromWorldUuid" in path.read_text(encoding="utf-8"))


def main() -> None:
    for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
        patch(ROOT / rel)
    print("DONE_PATCH_UUID")


if __name__ == "__main__":
    main()
