createHeadAttachments(exportInfo, root) {
    const primaryMesh = this.findPrimarySkinnedMesh(root);
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