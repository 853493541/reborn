from pathlib import Path

ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")

# Replace createHeadAttachments section's HAND/HEAD logic and add applyAnimatedHeadVisibility

NEW_FUNCS = r'''
function findPrimarySkinnedMesh(root) {
  let bestMatch = null;
  root.traverse((object) => {
    if (!object.isSkinnedMesh || !Array.isArray(object.skeleton?.bones) || object.skeleton.bones.length === 0) {
      return;
    }
    const bones = object.skeleton.bones;
    const lowerNames = new Set(bones.map((bone) => String(bone.name || '').toLowerCase()));
    const score =
      (lowerNames.has('bip01_pelvis') ? 1000 : 0) +
      (lowerNames.has('bip01_head') ? 1000 : 0) +
      bones.length;
    if (!bestMatch || score > bestMatch.score) {
      bestMatch = { object, score };
    }
  });
  return bestMatch?.object || null;
}

/** Map-viewer animated-head: hide head/plait/bang when a faceroot face rig exists. */
function applyAnimatedHeadVisibility(root) {
  const headMeshes = [];
  const plaitMeshes = [];
  const bangMeshes = [];
  const faceSkinned = [];
  root.traverse((object) => {
    if (!object.isMesh) return;
    const n = String(object.name || '');
    if (/_head_hdmesh$/i.test(n) || /head_hdmesh/i.test(n)) headMeshes.push(object);
    if (/_plait_hdmesh$/i.test(n) || /plait_hdmesh/i.test(n)) plaitMeshes.push(object);
    if (/_bang_hdmesh$/i.test(n) || /bang_hdmesh/i.test(n)) bangMeshes.push(object);
    if (object.isSkinnedMesh && (/_face_hdmesh$/i.test(n) || /face_hdmesh/i.test(n))) {
      faceSkinned.push(object);
    }
  });
  const hasDetachedFaceRig = faceSkinned.some((object) => {
    const bones = object.skeleton?.bones || [];
    if (!bones.length) return false;
    const boneSet = new Set(bones);
    const roots = bones.filter((bone) => !boneSet.has(bone.parent));
    return roots.length === 1 && String(roots[0].name || '').toLowerCase() === 'faceroot';
  });
  if (!hasDetachedFaceRig) {
    return { mode: 'none', hidden: 0 };
  }
  for (const mesh of [...headMeshes, ...plaitMeshes, ...bangMeshes]) {
    mesh.visible = false;
  }
  return {
    mode: 'animated-head',
    hidden: headMeshes.length + plaitMeshes.length + bangMeshes.length,
    head: headMeshes.map((m) => m.name),
    plait: plaitMeshes.map((m) => m.name),
    bang: bangMeshes.map((m) => m.name),
  };
}

function findBoneOnPrimary(primaryBoneMap, names) {
  for (const n of names) {
    const b = primaryBoneMap.get(n);
    if (b) return b;
  }
  return null;
}

/** Map-viewer createHeadAttachments + hand/glove boneLinks + glove root→hand anchors. */
function createHeadAttachments(root) {
  const primaryMesh = findPrimarySkinnedMesh(root);
  const primaryBones = primaryMesh?.skeleton?.bones || [];
  const primaryRestWorldMap = new Map(
    primaryBones.map((bone) => [String(bone.name || '').toLowerCase(), bone.matrixWorld.clone()])
  );
  const anchorBone = primaryMesh?.skeleton?.bones?.find((bone) => bone.name === 'bip01_head') || null;
  if (!anchorBone) {
    return { boneLinks: [], rootLinks: [], primaryBones, primaryRestWorldMap };
  }

  const primaryBoneMap = new Map(
    primaryMesh.skeleton.bones.map((bone) => [String(bone.name || '').toLowerCase(), bone])
  );

  // Scene-wide bone map for hand anchors (body may lack hand bones).
  const sceneBoneMap = new Map();
  root.traverse((object) => {
    if (!object.isSkinnedMesh || !object.skeleton?.bones) return;
    for (const bone of object.skeleton.bones) {
      const key = String(bone.name || '').toLowerCase();
      if (!key || sceneBoneMap.has(key)) continue;
      sceneBoneMap.set(key, bone);
    }
  });

  root.updateMatrixWorld(true);

  const boneLinks = [];
  const rootLinks = [];
  const HEAD_RE = /head|face|bang|plait|hat/i;
  const HAND_RE = /hand|glove/i;
  root.traverse((object) => {
    if (!object.isSkinnedMesh || !object.skeleton?.bones?.length) return;
    if (object === primaryMesh) return;
    const isHeadPart = HEAD_RE.test(object.name);
    const isHandPart = HAND_RE.test(object.name);
    if (!isHeadPart && !isHandPart) return;

    for (const bone of object.skeleton.bones) {
      const sourceBone = primaryBoneMap.get(String(bone.name || '').toLowerCase());
      if (!sourceBone || sourceBone === bone) continue;

      boneLinks.push({
        meshName: object.name,
        mesh: object,
        sourceBone,
        targetBone: bone,
        parentInverse: new THREE.Matrix4(),
        targetWorld: new THREE.Matrix4(),
        localMatrix: new THREE.Matrix4(),
        position: new THREE.Vector3(),
        quaternion: new THREE.Quaternion(),
        scale: new THREE.Vector3(),
      });
    }

    const boneSet = new Set(object.skeleton.bones);
    const roots = object.skeleton.bones.filter((bone) => !boneSet.has(bone.parent));
    for (const rootBone of roots) {
      if (primaryBoneMap.has(String(rootBone.name || '').toLowerCase())) continue;
      if (sceneBoneMap.get(String(rootBone.name || '').toLowerCase()) === rootBone) {
        // unique name — may still need anchor
      }

      let useAnchor = null;
      if (isHeadPart) {
        useAnchor = anchorBone;
      } else if (isHandPart) {
        const n = String(object.name || '').toLowerCase();
        if (n.includes('lglove') || n.includes('_l_') || /(^|_)l(hand|glove)/i.test(object.name)) {
          useAnchor = sceneBoneMap.get('bip01_l_hand') || sceneBoneMap.get('bip01_l_upperarm');
        } else if (n.includes('rglove') || n.includes('_r_') || /(^|_)r(hand|glove)/i.test(object.name)) {
          useAnchor = sceneBoneMap.get('bip01_r_hand') || sceneBoneMap.get('bip01_r_upperarm');
        } else {
          // shared hand mesh: skip rootLinks (mixer drives unique bones)
          continue;
        }
      } else {
        continue;
      }
      if (!useAnchor || useAnchor === rootBone) continue;

      const localOffset = new THREE.Matrix4()
        .copy(useAnchor.matrixWorld)
        .invert()
        .multiply(rootBone.matrixWorld);

      rootLinks.push({
        meshName: object.name,
        mesh: object,
        rootBone,
        anchorBone: useAnchor,
        defaultAnchorName: String(useAnchor.name || ''),
        selectedAnchorName: String(useAnchor.name || ''),
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

  return { boneLinks, rootLinks, faceCalibration: null, primaryBones, primaryRestWorldMap };
}

function updateHeadAttachments(attachments, root) {
  if (!attachments) return;

  for (const attachment of attachments.rootLinks || []) {
    const parent = attachment.rootBone.parent;
    attachment.targetWorld.multiplyMatrices(attachment.anchorBone.matrixWorld, attachment.localOffset);

    if (parent) {
      attachment.parentInverse.copy(parent.matrixWorld).invert();
      attachment.localMatrix.multiplyMatrices(attachment.parentInverse, attachment.targetWorld);
    } else {
      attachment.localMatrix.copy(attachment.targetWorld);
    }

    attachment.localMatrix.decompose(
      attachment.position,
      attachment.quaternion,
      attachment.scale,
    );

    attachment.rootBone.position.copy(attachment.position);
    attachment.rootBone.quaternion.copy(attachment.quaternion);
    attachment.rootBone.scale.copy(attachment.scale);
  }

  if (root) {
    root.updateMatrixWorld(true);
  }

  for (const attachment of attachments.boneLinks || []) {
    const parent = attachment.targetBone.parent;
    attachment.targetWorld.copy(attachment.sourceBone.matrixWorld);

    if (parent) {
      attachment.parentInverse.copy(parent.matrixWorld).invert();
      attachment.localMatrix.multiplyMatrices(attachment.parentInverse, attachment.targetWorld);
    } else {
      attachment.localMatrix.copy(attachment.targetWorld);
    }

    attachment.localMatrix.decompose(
      attachment.position,
      attachment.quaternion,
      attachment.scale,
    );

    attachment.targetBone.position.copy(attachment.position);
    attachment.targetBone.quaternion.copy(attachment.quaternion);
    attachment.targetBone.scale.copy(attachment.scale);
  }
}

let headAttachments = null;
let partVisibility = null;
'''

for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
    p = ROOT / rel
    js = p.read_text(encoding="utf-8")
    MARKER = "/* === HEAD_ATTACHMENTS_BEGIN === */"
    ENDMARK = "/* === HEAD_ATTACHMENTS_END === */"
    a = js.find(MARKER)
    b = js.find(ENDMARK)
    if a < 0 or b < 0:
        raise SystemExit(f"markers missing in {rel}")
    b = b + len(ENDMARK)
    js = js[:a] + f"{MARKER}\n{NEW_FUNCS}\n{ENDMARK}" + js[b:]

    # Wire visibility before attachments
    old = (
        "      headAttachments = createHeadAttachments(root);\n"
        "      window.__actor.attachments = headAttachments;\n"
        "      updateHeadAttachments(headAttachments, root);\n"
    )
    new = (
        "      partVisibility = applyAnimatedHeadVisibility(root);\n"
        "      window.__actor.partVisibility = partVisibility;\n"
        "      headAttachments = createHeadAttachments(root);\n"
        "      window.__actor.attachments = headAttachments;\n"
        "      updateHeadAttachments(headAttachments, root);\n"
    )
    if old not in js:
        raise SystemExit(f"wire block missing in {rel}")
    js = js.replace(old, new, 1)

    # Meta: include partVisibility
    meta_att = (
        "      if (headAttachments) {\n"
        "        window.__FBX_READY__.attachments = {\n"
        "          boneLinks: headAttachments.boneLinks?.length || 0,\n"
        "          rootLinks: headAttachments.rootLinks?.length || 0,\n"
        "        };\n"
        "      }"
    )
    meta_new = (
        "      if (headAttachments) {\n"
        "        window.__FBX_READY__.attachments = {\n"
        "          boneLinks: headAttachments.boneLinks?.length || 0,\n"
        "          rootLinks: headAttachments.rootLinks?.length || 0,\n"
        "        };\n"
        "      }\n"
        "      if (partVisibility) window.__FBX_READY__.partVisibility = partVisibility;"
    )
    if meta_att not in js:
        raise SystemExit(f"meta att missing {rel}")
    js = js.replace(meta_att, meta_new, 1)

    p.write_text(js, encoding="utf-8")
    print(rel, "ok", "applyAnimatedHeadVisibility" in js)
