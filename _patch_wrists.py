from pathlib import Path
ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")

HAND_FUNCS = r'''
/** Sleeve/hand fix: sync secondary arm bones from primary (body), gloves from hand mesh.
 *  Sort links parent-before-child; multi-pass update after Mixer. */
function boneDepth(bone) {
  let d = 0;
  let p = bone;
  while (p && p.isBone) {
    d += 1;
    p = p.parent;
  }
  return d;
}

function pushBoneLink(list, meshName, mesh, sourceBone, targetBone) {
  if (!sourceBone || !targetBone || sourceBone === targetBone) return;
  list.push({
    meshName,
    mesh,
    sourceBone,
    targetBone,
    parentInverse: new THREE.Matrix4(),
    targetWorld: new THREE.Matrix4(),
    localMatrix: new THREE.Matrix4(),
    position: new THREE.Vector3(),
    quaternion: new THREE.Quaternion(),
    scale: new THREE.Vector3(),
    _depth: boneDepth(targetBone),
  });
}

function createHandSleeveAttachments(root, headAttachments) {
  const primaryMesh = findPrimarySkinnedMesh(root);
  if (!primaryMesh) return headAttachments;

  const primaryBoneMap = new Map(
    primaryMesh.skeleton.bones.map((bone) => [String(bone.name || '').toLowerCase(), bone])
  );

  let handMesh = null;
  root.traverse((o) => {
    if (o.isSkinnedMesh && /hand_hdmesh/i.test(o.name) && !/glove/i.test(o.name)) {
      handMesh = o;
    }
  });
  const handBoneMap = new Map(
    (handMesh?.skeleton?.bones || []).map((bone) => [String(bone.name || '').toLowerCase(), bone])
  );

  const boneLinks = [...(headAttachments?.boneLinks || [])];
  const rootLinks = [...(headAttachments?.rootLinks || [])];
  const anchorHead = primaryMesh.skeleton.bones.find((b) => b.name === 'bip01_head') || null;

  root.traverse((object) => {
    if (!object.isSkinnedMesh || !object.skeleton?.bones?.length) return;
    if (object === primaryMesh) return;
    const name = String(object.name || '');
    const isHand = /hand_hdmesh/i.test(name) && !/glove/i.test(name);
    const isGlove = /glove_hdmesh/i.test(name) || /_lglove_|_rglove_/i.test(name);
    if (!isHand && !isGlove) return;

    for (const bone of object.skeleton.bones) {
      const key = String(bone.name || '').toLowerCase();
      // Prefer primary (body) for shared bip/armtwist; gloves also take from hand mesh.
      let source = primaryBoneMap.get(key);
      if (!source && isGlove) source = handBoneMap.get(key);
      // Hand mesh: only link bones that exist on body (clavicle/upperarm/armtwist).
      // Unique hand bones stay Mixer-driven.
      if (isHand && !primaryBoneMap.has(key)) continue;
      pushBoneLink(boneLinks, name, object, source, bone);
    }

    if (!isGlove) return;
    // Glove-only roots → follow L/R hand bone on hand mesh (or body if present).
    const boneSet = new Set(object.skeleton.bones);
    const roots = object.skeleton.bones.filter((bone) => !boneSet.has(bone.parent));
    const preferLeft = /lglove|_l_/i.test(name);
    const preferRight = /rglove|_r_/i.test(name);
    for (const rootBone of roots) {
      const key = String(rootBone.name || '').toLowerCase();
      if (primaryBoneMap.has(key) || handBoneMap.has(key)) continue;
      let anchor =
        (preferLeft && (handBoneMap.get('bip01_l_hand') || primaryBoneMap.get('bip01_l_hand'))) ||
        (preferRight && (handBoneMap.get('bip01_r_hand') || primaryBoneMap.get('bip01_r_hand'))) ||
        handBoneMap.get('bip01_l_hand') ||
        handBoneMap.get('bip01_r_hand') ||
        anchorHead;
      if (!anchor || anchor === rootBone) continue;
      const localOffset = new THREE.Matrix4()
        .copy(anchor.matrixWorld)
        .invert()
        .multiply(rootBone.matrixWorld);
      rootLinks.push({
        meshName: name,
        mesh: object,
        rootBone,
        anchorBone: anchor,
        defaultAnchorName: String(anchor.name || ''),
        selectedAnchorName: String(anchor.name || ''),
        rootRestWorld: rootBone.matrixWorld.clone(),
        defaultLocalOffset: localOffset.clone(),
        baseLocalOffset: new THREE.Matrix4().copy(localOffset),
        localOffset,
        tweakPosition: new THREE.Vector3(),
        tweakEuler: new THREE.Euler(0, 0, 0, 'XYZ'),
        tweakQuaternion: new THREE.Quaternion(),
        tweakScale: new THREE.Vector3(1, 1, 1),
        tweakMatrix: new THREE.Matrix4(),
        parentInverse: new THREE.Matrix4(),
        targetWorld: new THREE.Matrix4(),
        localMatrix: new THREE.Matrix4(),
        position: new THREE.Vector3(),
        quaternion: new THREE.Quaternion(),
        scale: new THREE.Vector3(),
      });
    }
  });

  boneLinks.sort((a, b) => (a._depth || 0) - (b._depth || 0));
  return {
    ...(headAttachments || {}),
    boneLinks,
    rootLinks,
    handSleeve: true,
  };
}

function updateAttachmentsMultiPass(attachments, root, passes = 3) {
  if (!attachments) return;
  for (let i = 0; i < passes; i++) {
    updateHeadAttachments(attachments, root);
    if (root) {
      root.updateMatrixWorld(true);
      root.traverse((o) => {
        if (o.isSkinnedMesh) o.skeleton?.update();
      });
    }
  }
}
'''

for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
    p = ROOT / rel
    js = p.read_text(encoding="utf-8")

    # Insert hand funcs before ENDMARK if not present
    if "function createHandSleeveAttachments" not in js:
        end = "/* === HEAD_ATTACHMENTS_END === */"
        if end not in js:
            raise SystemExit(f"no end mark {rel}")
        js = js.replace(end, HAND_FUNCS + "\n" + end, 1)

    # Replace wire: enable hand sleeve on clipFbx path; keep arm-rename off
    old = """      // Arm-rename disabled: hurt embedded; map-viewer uses name-matched Mixer only.
      window.__actor.armBind = { renamed: 0, note: 'disabled' };
      headAttachments = createHeadAttachments(root);
      window.__actor.attachments = headAttachments;
      updateHeadAttachments(headAttachments, root);"""

    new = """      window.__actor.armBind = { renamed: 0, note: 'disabled-use-sleeve-links' };
      headAttachments = createHeadAttachments(root);
      // Always sync hand/glove sleeves from body (fixes wrist gap on skin+clipFbx).
      headAttachments = createHandSleeveAttachments(root, headAttachments);
      window.__actor.attachments = headAttachments;
      updateAttachmentsMultiPass(headAttachments, root, 3);"""

    if old not in js:
        # try alternate already-patched?
        if "createHandSleeveAttachments" in js and "updateAttachmentsMultiPass(headAttachments" in js:
            print(rel, "already wired")
        else:
            raise SystemExit(f"wire block missing in {rel}")
    else:
        js = js.replace(old, new, 1)

    # frame() should use multi-pass too
    old_frame = """    if (headAttachments && window.__actor?.root) {
      updateHeadAttachments(headAttachments, window.__actor.root);
      window.__actor.root.traverse((o) => { if (o.isSkinnedMesh) o.skeleton?.update(); });
    }"""
    new_frame = """    if (headAttachments && window.__actor?.root) {
      updateAttachmentsMultiPass(headAttachments, window.__actor.root, 2);
    }"""
    if old_frame in js:
        js = js.replace(old_frame, new_frame, 1)
    elif "updateAttachmentsMultiPass(headAttachments, window.__actor.root" in js:
        pass
    else:
        print(rel, "WARN frame block not updated")

    # after mixer settle, multi-pass
    old_settle = "  if (headAttachments) updateHeadAttachments(headAttachments, rootObj);"
    new_settle = "  if (headAttachments) updateAttachmentsMultiPass(headAttachments, rootObj, 3);"
    js = js.replace(old_settle, new_settle)

    p.write_text(js, encoding="utf-8")
    print(rel, "ok", "createHandSleeveAttachments" in js)
