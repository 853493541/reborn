from pathlib import Path
ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")

# Expand preferHandArmBinding to include clavicle; wire it ON for CLIP_FBX only;
# createHandSleeveAttachments should not body→hand link bones that were renamed.

for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
    p = ROOT / rel
    js = p.read_text(encoding="utf-8")

    # Expand ARM_NAMES set in preferHandArmBinding
    old_set = """  const ARM_NAMES = new Set([
    'bip01_l_upperarm', 'bip01_r_upperarm',
    'bip01_l_foretwist', 'bip01_r_foretwist',
    'bip01_l_foretwist1', 'bip01_r_foretwist1',
    'bip01_l_hand', 'bip01_r_hand',
    'bone_l_armtwist', 'bone_r_armtwist',
  ]);"""
    new_set = """  const ARM_NAMES = new Set([
    'bip01_l_clavicle', 'bip01_r_clavicle',
    'bip01_l_upperarm', 'bip01_r_upperarm',
    'bip01_l_foretwist', 'bip01_r_foretwist',
    'bip01_l_foretwist1', 'bip01_r_foretwist1',
    'bip01_l_hand', 'bip01_r_hand',
    'bone_l_armtwist', 'bone_r_armtwist',
  ]);"""
    if old_set not in js:
        raise SystemExit(f"ARM_NAMES missing {rel}")
    js = js.replace(old_set, new_set, 1)

    # Also expand isArmish for clavicle
    if "if (/^bip01_[lr]_clavicle/.test(n)) return true;" not in js:
        js = js.replace(
            "if (/^bip01_[lr]_finger/.test(n)) return true;",
            "if (/^bip01_[lr]_clavicle/.test(n)) return true;\n    if (/^bip01_[lr]_finger/.test(n)) return true;",
            1,
        )

    # Fix createHandSleeveAttachments: for hand mesh, only link if primary still has the name
    # (after rename primary won't — skip). Already: `if (isHand && !primaryBoneMap.has(key)) continue;`
    # After rename, primary has __body__ names so has() fails for bip01_l_upperarm — good, no body→hand.

    # Wire: enable preferHandArmBinding when CLIP_FBX_URL set
    old_wire = """      window.__actor.armBind = { renamed: 0, note: 'disabled-use-sleeve-links' };
      headAttachments = createHeadAttachments(root);
      // Always sync hand/glove sleeves from body (fixes wrist gap on skin+clipFbx).
      headAttachments = createHandSleeveAttachments(root, headAttachments);
      window.__actor.attachments = headAttachments;
      updateAttachmentsMultiPass(headAttachments, root, 3);"""

    new_wire = """      // skin+clipFbx: rename body arm stubs so Mixer binds tracks to hand mesh chain,
      // then sync body sleeves from hand (map-viewer-style name match on the full arm).
      let armBind = { renamed: 0, note: 'skipped-embedded' };
      if (CLIP_FBX_URL) {
        armBind = preferHandArmBinding(root);
      }
      window.__actor.armBind = armBind;
      headAttachments = createHeadAttachments(root);
      if (CLIP_FBX_URL) {
        const armSync = createArmBodySyncLinks(root);
        headAttachments.boneLinks = [...(headAttachments.boneLinks || []), ...armSync];
      }
      headAttachments = createHandSleeveAttachments(root, headAttachments);
      // parent-before-child
      headAttachments.boneLinks = (headAttachments.boneLinks || []).slice().sort(
        (a, b) => (a._depth || boneDepth(a.targetBone)) - (b._depth || boneDepth(b.targetBone))
      );
      window.__actor.attachments = headAttachments;
      updateAttachmentsMultiPass(headAttachments, root, 3);"""

    if old_wire not in js:
        raise SystemExit(f"wire missing {rel}")
    js = js.replace(old_wire, new_wire, 1)

    # Meta armBind already handled
    p.write_text(js, encoding="utf-8")
    print(rel, "patched")
