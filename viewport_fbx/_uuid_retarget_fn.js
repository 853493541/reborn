function animationClipFromWorldUuid(payload, rootObj) {
  // UUID-safe retarget: temporarily uniquify duplicate bone names so PropertyBinding
  // hits every skinned influence, bake world→local with live parent.matrixWorld.
  const fps = Number(payload.fps) || 30;
  const fc = Number(payload.frame_count) || 0;
  const duration = Number(payload.duration) || Math.max(0, (fc - 1) / fps);
  const times = Float32Array.from(Array.from({ length: fc }, (_, i) => i / fps));

  const worldByKey = new Map();
  for (const b of payload.bones || []) {
    const entry = { positions: b.positions || [], quats: b.quats || [], name: b.name };
    for (const k of boneLookupKeys(b.name)) worldByKey.set(_normNameKey(k), entry);
    worldByKey.set(_normNameKey(b.name), entry);
  }

  // Collect EVERY Bone under root (including duplicate names).
  const allBones = [];
  const walk = (obj) => {
    if (!obj) return;
    if (obj.isBone || obj.type === 'Bone') allBones.push(obj);
    for (const c of obj.children || []) walk(c);
  };
  walk(rootObj);

  // Snapshot original names; uniquify duplicates for binding.
  const origNames = new Map();
  const nameCount = new Map();
  for (const b of allBones) {
    origNames.set(b.uuid, b.name);
    const base = b.name || 'bone';
    const n = (nameCount.get(base) || 0) + 1;
    nameCount.set(base, n);
    if (n > 1) b.name = `${base}__u${n}_${b.uuid.slice(0, 6)}`;
    // first keeps original name (PropertyBinding + findBone compatible for primary)
  }

  // Parents-first using live parent pointers (uuid-correct).
  const ordered = [];
  const placed = new Set();
  const visit = (b) => {
    if (!b || placed.has(b.uuid)) return;
    let p = b.parent;
    while (p && !(p.isBone || p.type === 'Bone')) p = p.parent;
    if (p && p.uuid !== b.uuid) visit(p);
    if (!placed.has(b.uuid)) {
      placed.add(b.uuid);
      ordered.push(b);
    }
  };
  for (const b of allBones) visit(b);

  const bindLocal = new Map();
  for (const b of ordered) {
    bindLocal.set(b.uuid, {
      pos: b.position.clone(),
      quat: b.quaternion.clone(),
      scl: b.scale.clone(),
    });
  }

  const posSeries = new Map();
  const quatSeries = new Map();
  for (const b of ordered) {
    posSeries.set(b.uuid, []);
    quatSeries.set(b.uuid, []);
  }

  const _min2 = new THREE.Matrix4();
  const _local = new THREE.Matrix4();
  const _inv = new THREE.Matrix4();
  const _pos = new THREE.Vector3();
  const _quat = new THREE.Quaternion();
  const _scl = new THREE.Vector3(1, 1, 1);
  const _ident = new THREE.Matrix4();
  const _qx90 = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(1, 0, 0), Math.PI / 2);
  const _worldAlign = new THREE.Matrix4();

  const baseName = (b) => origNames.get(b.uuid) || b.name;
  const isCore = (n) => {
    const k = _normNameKey(n);
    if (!(k.startsWith('bip01') || k.startsWith('bone_'))) return false;
    if (k.includes('finger') || k.includes('toe')) return false;
    return true;
  };

  // Align MIN2 Z-up → FBX bip01 bind.
  for (const b of ordered) {
    const bind = bindLocal.get(b.uuid);
    b.position.copy(bind.pos);
    b.quaternion.copy(bind.quat);
    b.scale.copy(bind.scl);
  }
  if (rootObj) rootObj.updateMatrixWorld(true);
  {
    const rootBone = ordered.find((b) => _normNameKey(baseName(b)) === 'bip01');
    const rootSrc = worldByKey.get('bip01');
    if (rootBone && rootSrc?.positions?.[0] && rootSrc?.quats?.[0]) {
      const p0 = rootSrc.positions[0];
      const q0 = rootSrc.quats[0];
      _pos.set(p0[0], p0[2], -p0[1]);
      _quat.set(q0[0], q0[1], q0[2], q0[3]).normalize().premultiply(_qx90);
      _min2.compose(_pos, _quat, _scl.set(1, 1, 1));
      _worldAlign.copy(rootBone.matrixWorld).multiply(_min2.clone().invert());
    }
  }

  let matched = 0;
  for (let fi = 0; fi < fc; fi++) {
    for (const b of ordered) {
      const bind = bindLocal.get(b.uuid);
      b.position.copy(bind.pos);
      b.quaternion.copy(bind.quat);
      b.scale.copy(bind.scl);
      b.matrixAutoUpdate = true;
    }
    if (rootObj) rootObj.updateMatrixWorld(true);

    for (const b of ordered) {
      const bn = baseName(b);
      if (!isCore(bn)) continue;
      const src = worldByKey.get(_normNameKey(bn));
      if (!src?.positions?.[fi] || !src?.quats?.[fi]) continue;
      if (fi === 0) matched += 1;
      const p = src.positions[fi];
      const q = src.quats[fi];
      _pos.set(p[0], p[2], -p[1]);
      _quat.set(q[0], q[1], q[2], q[3]).normalize().premultiply(_qx90);
      _min2.compose(_pos, _quat, _scl.set(1, 1, 1));
      _min2.premultiply(_worldAlign);

      let parentBone = b.parent;
      while (parentBone && !(parentBone.isBone || parentBone.type === 'Bone')) parentBone = parentBone.parent;
      if (parentBone && parentBone.uuid === b.uuid) parentBone = null;
      const parentWM = parentBone ? parentBone.matrixWorld : _ident;
      _inv.copy(parentWM).invert();
      _local.copy(_inv).multiply(_min2);
      _local.decompose(_pos, _quat, _scl);
      b.position.copy(_pos);
      b.quaternion.copy(_quat);
      b.scale.copy(bindLocal.get(b.uuid).scl);
      b.updateMatrixWorld(true);
    }

    for (const b of ordered) {
      const bn = baseName(b);
      if (!isCore(bn) || !worldByKey.has(_normNameKey(bn))) continue;
      posSeries.get(b.uuid).push(b.position.x, b.position.y, b.position.z);
      const arr = quatSeries.get(b.uuid);
      let qx = b.quaternion.x, qy = b.quaternion.y, qz = b.quaternion.z, qw = b.quaternion.w;
      if (arr.length >= 4) {
        const px = arr[arr.length - 4], py = arr[arr.length - 3], pz = arr[arr.length - 2], pw = arr[arr.length - 1];
        if (px * qx + py * qy + pz * qz + pw * qw < 0) { qx = -qx; qy = -qy; qz = -qz; qw = -qw; }
      }
      arr.push(qx, qy, qz, qw);
    }
  }

  // Restore bind locals before building tracks (track names use uniquified names).
  for (const b of ordered) {
    const bind = bindLocal.get(b.uuid);
    b.position.copy(bind.pos);
    b.quaternion.copy(bind.quat);
    b.scale.copy(bind.scl);
  }
  if (rootObj) rootObj.updateMatrixWorld(true);

  const tracks = [];
  for (const b of ordered) {
    const bn = baseName(b);
    if (!isCore(bn) || !worldByKey.has(_normNameKey(bn))) continue;
    // Use current (possibly uniquified) name so EVERY duplicate gets a track.
    tracks.push(new THREE.VectorKeyframeTrack(`${b.name}.position`, times, Float32Array.from(posSeries.get(b.uuid))));
    tracks.push(new THREE.QuaternionKeyframeTrack(`${b.name}.quaternion`, times, Float32Array.from(quatSeries.get(b.uuid))));
  }

  const clip = new THREE.AnimationClip(payload.name || 'clip', duration, tracks);
  clip.userData = {
    worldRetargetMatched: matched,
    uniquified: true,
    // Restore original names after mixer binds — caller must call restore after clipAction.
    restoreBoneNames: () => {
      for (const b of allBones) {
        const n = origNames.get(b.uuid);
        if (n != null) b.name = n;
      }
    },
    keepUniqueNames: true, // keep uniquified names while mixer runs
  };
  // NOTE: do NOT restore names yet — Mixer PropertyBinding needs unique names.
  return clip;
}
