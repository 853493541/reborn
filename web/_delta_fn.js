function animationClipFromMin2LocalDelta(payload, rootObj) {
  const fps = Number(payload.fps) || 30;
  const fc = Number(payload.frame_count) || 0;
  const duration = Number(payload.duration) || Math.max(0, (fc - 1) / fps);
  const times = Float32Array.from(Array.from({ length: fc }, (_, i) => i / fps));
  const byKey = new Map();
  for (const b of payload.bones || []) {
    for (const k of boneLookupKeys(b.name)) byKey.set(_normNameKey(k), b);
    byKey.set(_normNameKey(b.name), b);
  }
  const bones = [];
  const seen = new Set();
  for (const b of boneByName.values()) {
    if (seen.has(b.uuid)) continue;
    seen.add(b.uuid);
    bones.push(b);
  }
  const qRest = new THREE.Quaternion();
  const qAnim = new THREE.Quaternion();
  const qBind = new THREE.Quaternion();
  const qOut = new THREE.Quaternion();
  const qInv = new THREE.Quaternion();
  const tracks = [];
  let matched = 0;
  for (const b of bones) {
    const src = byKey.get(_normNameKey(b.name));
    if (!src) continue;
    const k = _normNameKey(b.name);
    if (!(k.startsWith('bip01') || k.startsWith('bone_'))) continue;
    if (k.includes('finger') || k.includes('toe')) continue;
    matched += 1;
    const qvals = [];
    const pvals = [];
    qBind.copy(b.quaternion);
    const bindPos = b.position.clone();
    const r0 = src.local_quats[0];
    qRest.set(r0[0], r0[1], r0[2], r0[3]).normalize();
    for (let fi = 0; fi < fc; fi++) {
      const qa = src.local_quats[fi];
      qAnim.set(qa[0], qa[1], qa[2], qa[3]).normalize();
      qInv.copy(qRest).invert();
      qOut.copy(qBind).multiply(qInv).multiply(qAnim);
      if (qvals.length >= 4) {
        const px = qvals[qvals.length - 4];
        const py = qvals[qvals.length - 3];
        const pz = qvals[qvals.length - 2];
        const pw = qvals[qvals.length - 1];
        if (px * qOut.x + py * qOut.y + pz * qOut.z + pw * qOut.w < 0) {
          qOut.set(-qOut.x, -qOut.y, -qOut.z, -qOut.w);
        }
      }
      qvals.push(qOut.x, qOut.y, qOut.z, qOut.w);
      pvals.push(bindPos.x, bindPos.y, bindPos.z);
    }
    tracks.push(new THREE.VectorKeyframeTrack(b.name + '.position', times, Float32Array.from(pvals)));
    tracks.push(new THREE.QuaternionKeyframeTrack(b.name + '.quaternion', times, Float32Array.from(qvals)));
  }
  const clip = new THREE.AnimationClip(payload.name || 'clip', duration, tracks);
  clip.userData = { worldRetargetMatched: matched };
  return clip;
}

