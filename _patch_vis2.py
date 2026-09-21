from pathlib import Path
ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")

# 1) Make applyAnimatedHeadVisibility a no-op (show all meshes) — wrong without actor export parts
# 2) Narrow createHeadAttachments: only head|face|bang|plait|hat (map-viewer exact), NO hand boneLinks

NEW = r'''
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

/** Without MovieEditor actor-parts JSON, do not hide meshes (hiding head caused bald scalp). */
function applyAnimatedHeadVisibility(root) {
  return { mode: 'none', hidden: 0, note: 'skipped-no-actor-parts' };
}

/** Map-viewer createHeadAttachments (head/face/bang/plait/hat only). */
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

  root.updateMatrixWorld(true);

  const boneLinks = [];
  const rootLinks = [];
  root.traverse((object) => {
    if (!object.isSkinnedMesh || !object.skeleton?.bones?.length) return;
    if (object === primaryMesh) return;
    if (!/head|face|bang|plait|hat/i.test(object.name)) return;

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

      const localOffset = new THREE.Matrix4()
        .copy(anchorBone.matrixWorld)
        .invert()
        .multiply(rootBone.matrixWorld);

      rootLinks.push({
        meshName: object.name,
        mesh: object,
        rootBone,
        anchorBone,
        defaultAnchorName: String(anchorBone.name || ''),
        selectedAnchorName: String(anchorBone.name || ''),
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
    b = js.find(ENDMARK) + len(ENDMARK)
    js = js[:a] + f"{MARKER}\n{NEW}\n{ENDMARK}" + js[b:]
    p.write_text(js, encoding="utf-8")
    print(rel, "rewrote attachments (head-only, no mesh hide)")
