from pathlib import Path

ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")
frag = Path(r"C:\Users\Zhibin Ren\jx3-ani-player\_head_attach.jsfrag").read_text(encoding="utf-8")

MARKER = "/* === HEAD_ATTACHMENTS_BEGIN === */"
ENDMARK = "/* === HEAD_ATTACHMENTS_END === */"

for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
    p = ROOT / rel
    js = p.read_text(encoding="utf-8")

    # Remove previous patch if re-run
    if MARKER in js:
        a = js.find(MARKER)
        b = js.find(ENDMARK)
        if b < 0:
            raise SystemExit(f"orphan marker in {rel}")
        b = b + len(ENDMARK)
        js = js[:a] + js[b:]

    # Insert functions before `async function loadAnimationsFromClipFbx` or first `async function startMixer`
    insert_at = js.find("async function loadAnimationsFromClipFbx")
    if insert_at < 0:
        insert_at = js.find("async function startMixerClip")
    if insert_at < 0:
        raise SystemExit(f"insert point missing in {rel}")

    block = f"\n{MARKER}\n{frag}\n{ENDMARK}\n\n"
    js = js[:insert_at] + block + js[insert_at:]

    # After window.__actor = ... create attachments
    needle = "window.__actor = { root, placement, bonesByName: boneByName };"
    if needle not in js:
        raise SystemExit(f"__actor assign missing in {rel}")
    inject = (
        needle
        + "\n      headAttachments = createHeadAttachments(root);\n"
        + "      window.__actor.attachments = headAttachments;\n"
        + "      updateHeadAttachments(headAttachments, root);\n"
        + "      if (window.__FBX_READY__) { /* placeholder */ }\n"
    )
    # Actually don't touch FBX_READY yet — inject cleanly
    inject = (
        needle
        + "\n      headAttachments = createHeadAttachments(root);\n"
        + "      window.__actor.attachments = headAttachments;\n"
        + "      updateHeadAttachments(headAttachments, root);\n"
    )
    js = js.replace(needle, inject, 1)

    # In frame(), after mixer.update, call updateHeadAttachments
    old_frame = """function frame() {
  resize();
  if (useMixer && mixer) {
    if (CLIP_TIME == null && CLIP_FRAME == null) {
      mixer.update(mixerClock.getDelta());
    } else {
      mixer.update(0);
    }
  } else {
    pollPose();
  }
  renderer.render(scene, camera);
  requestAnimationFrame(frame);
}"""
    new_frame = """function frame() {
  resize();
  if (useMixer && mixer) {
    if (CLIP_TIME == null && CLIP_FRAME == null) {
      mixer.update(mixerClock.getDelta());
    } else {
      mixer.update(0);
    }
    if (headAttachments && window.__actor?.root) {
      updateHeadAttachments(headAttachments, window.__actor.root);
      window.__actor.root.traverse((o) => { if (o.isSkinnedMesh) o.skeleton?.update(); });
    }
  } else {
    pollPose();
  }
  renderer.render(scene, camera);
  requestAnimationFrame(frame);
}"""
    if old_frame not in js:
        raise SystemExit(f"frame() block mismatch in {rel}")
    js = js.replace(old_frame, new_frame, 1)

    # Also after mixer setup (setTime/update) call attachments once — in startMixerFromClips after mixer.update(0)
    tag = "  rootObj.updateMatrixWorld(true);\n  rootObj.traverse((o) => { if (o.isSkinnedMesh) o.skeleton?.update(); });\n  useMixer = true;"
    tag_new = (
        "  rootObj.updateMatrixWorld(true);\n"
        "  if (headAttachments) updateHeadAttachments(headAttachments, rootObj);\n"
        "  rootObj.traverse((o) => { if (o.isSkinnedMesh) o.skeleton?.update(); });\n"
        "  useMixer = true;"
    )
    count = js.count(tag)
    if count < 1:
        raise SystemExit(f"mixer settle tag missing in {rel} count={count}")
    js = js.replace(tag, tag_new)

    # Attach counts into mixer ready / FBX ready for debug
    ready_line = "window.__FBX_READY__ = { bones: bc, fbx: FBX_URL, tex: TEX_BASE, diffuseMapped: mapped };"
    if ready_line not in js:
        raise SystemExit(f"ready line missing {rel}")
    js = js.replace(
        ready_line,
        ready_line
        + "\n      if (headAttachments) {\n"
        + "        window.__FBX_READY__.attachments = {\n"
        + "          boneLinks: headAttachments.boneLinks?.length || 0,\n"
        + "          rootLinks: headAttachments.rootLinks?.length || 0,\n"
        + "        };\n"
        + "      }",
        1,
    )

    p.write_text(js, encoding="utf-8")
    print(rel, "patched", "HEAD_ATTACHMENTS" in p.read_text(encoding="utf-8"))
