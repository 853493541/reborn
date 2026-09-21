/** MIN2 parent-local → helper Bone hierarchy → SkeletonUtils.retargetClip → Mixer (Tony path).
 * Axis/bind offsets go INTO the helper BEFORE retargetClip (authorized).
 */
function _normNameKey(n) {
  return String(n || '').toLowerCase().replace(/[\s\-]+/g, '_').trim();
}

function _aliasKeys(n) {
  const k = _normNameKey(n);
  const out = new Set([k]);
  out.add(k.replace(/^bip01_/, ''));
  out.add(k.replace(/^bone_/, ''));
  // space/underscore already normalized; also try bip01 + rest without extra underscores
  if (k.startsWith('bip01') && !k.startsWith('bip01_')) out.add('bip01_' + k.slice(5));
  if (k.startsWith('bone') && !k.startsWith('bone_')) out.add('bone_' + k.slice(4));
  // common JX3 ↔ FBX aliases
  const swaps = [
    [/^bip01_l_/, 'bip01_l'],
    [/^bip01_r_/, 'bip01_r'],
  ];
  for (const [re, rep] of swaps) {
    if (re.test(k)) out.add(k.replace(re, rep));
  }
  // bone_spine ↔ bip01_spine etc.
  if (k.startsWith('bone_')) out.add('bip01_' + k.slice(5));
  if (k.startsWith('bip01_')) out.add('bone_' + k.slice(6));
  return [...out];
}

function findBodySkinnedMeshForRetarget(rootObj) {
  let best = null;
  let bestN = -1;
  let bestName = '';
  rootObj.traverse((o) => {
    if (!o?.isSkinnedMesh || !o.skeleton?.bones?.length) return;
    const n = o.skeleton.bones.length;
    const nm = String(o.name || '').toLowerCase();
    const bodyBias = /hualuo|body|f1|player/.test(nm) ? 1000 : 0;
    const score = n + bodyBias;
    if (score > bestN) {
      best = o;
      bestN = score;
      bestName = o.name || '';
    }
  });
  return { mesh: best, boneCount: best?.skeleton?.bones?.length || 0, name: bestName };
}

function buildMin2HelperArmature(payload) {
  const hier = payload.hierarchy || [];
  if (!hier.length) throw new Error('min2HelperRetarget: empty hierarchy');

  const byExact = new Map();
  const byNorm = new Map();
  for (const h of hier) {
    const bone = new THREE.Bone();
    bone.name = h.name;
    bone.matrixAutoUpdate = true;
    byExact.set(h.name, bone);
    for (const a of _aliasKeys(h.name)) byNorm.set(a, bone);
    byNorm.set(_normNameKey(h.name), bone);
  }

  const roots = [];
  for (const h of hier) {
    const bone = byExact.get(h.name);
    const pname = h.parent;
    if (pname) {
      const parent = byExact.get(pname) || byNorm.get(_normNameKey(pname));
      if (parent && parent !== bone) parent.add(bone);
      else roots.push(bone);
    } else {
      roots.push(bone);
    }
  }

  const firstPos = new Map();
  const firstQuat = new Map();
  for (const tr of payload.tracks || []) {
    const name = String(tr.name || '');
    const dot = name.lastIndexOf('.');
    if (dot < 0) continue;
    const bname = name.slice(0, dot);
    const prop = name.slice(dot + 1);
    const vals = tr.values || [];
    if (prop === 'position' && vals.length >= 3) {
      firstPos.set(bname, new THREE.Vector3(vals[0], vals[1], vals[2]));
    } else if (prop === 'quaternion' && vals.length >= 4) {
      firstQuat.set(bname, new THREE.Quaternion(vals[0], vals[1], vals[2], vals[3]).normalize());
    }
  }
  for (const h of hier) {
    const bone = byExact.get(h.name);
    const p = firstPos.get(h.name);
    const q = firstQuat.get(h.name);
    if (p) bone.position.copy(p);
    if (q) bone.quaternion.copy(q);
    bone.scale.set(1, 1, 1);
  }

  const helperRoot = new THREE.Group();
  helperRoot.name = '__min2HelperArmature';
  for (const r of roots) helperRoot.add(r);

  // (1) Z-up → Y-up to match 花萝 FBX (Seasun MIN2 is Z-up).
  helperRoot.quaternion.setFromAxisAngle(new THREE.Vector3(1, 0, 0), -Math.PI / 2);
  helperRoot.updateMatrixWorld(true);

  const boneList = [];
  helperRoot.traverse((o) => {
    if (o.isBone) boneList.push(o);
  });
  helperRoot.skeleton = new THREE.Skeleton(boneList);
  helperRoot.updateMatrixWorld(true);
  return helperRoot;
}

function buildRetargetNameMap(targetBones, sourceBones) {
  const srcByNorm = new Map();
  for (const b of sourceBones) {
    for (const a of _aliasKeys(b.name)) srcByNorm.set(a, b.name);
    if (typeof boneLookupKeys === 'function') {
      for (const k of boneLookupKeys(b.name)) {
        for (const a of _aliasKeys(k)) srcByNorm.set(a, b.name);
      }
    }
  }
  const names = {};
  let matched = 0;
  const unmatched = [];
  for (const b of targetBones) {
    let hit = null;
    for (const a of _aliasKeys(b.name)) {
      hit = srcByNorm.get(a);
      if (hit) break;
    }
    if (hit) {
      names[b.name] = hit;
      matched += 1;
    } else {
      unmatched.push(b.name);
    }
  }
  return { names, matched, total: targetBones.length, unmatched };
}

function animationClipFromTracksPayload(payload) {
  const tracks = [];
  for (const tr of payload.tracks || []) {
    const tname = String(tr.name || '');
    const times = Float32Array.from(tr.times || []);
    const values = Float32Array.from(tr.values || []);
    if (!times.length || !values.length) continue;
    if (tr.type === 'quaternion' || tname.endsWith('.quaternion')) {
      tracks.push(new THREE.QuaternionKeyframeTrack(tname, times, values));
    } else if (tr.type === 'vector' || tname.endsWith('.position') || tname.endsWith('.scale')) {
      tracks.push(new THREE.VectorKeyframeTrack(tname, times, values));
    }
  }
  const duration = Number(payload.duration) || (typeof timesMax === 'function' ? timesMax(tracks) : 0);
  return new THREE.AnimationClip(payload.name || 'clip', duration, tracks);
}

/** Convert absolute local tracks → deltas from frame 0 (so helper rest can be FBX bind). */
function clipToRestDeltas(clip) {
  const out = [];
  for (const tr of clip.tracks) {
    const values = tr.values;
    const times = tr.times;
    if (tr instanceof THREE.QuaternionKeyframeTrack || tr.name.endsWith('.quaternion')) {
      const q0 = new THREE.Quaternion(values[0], values[1], values[2], values[3]).normalize();
      const inv0 = q0.clone().invert();
      const nv = new Float32Array(values.length);
      const q = new THREE.Quaternion();
      const qd = new THREE.Quaternion();
      for (let i = 0; i < values.length; i += 4) {
        q.set(values[i], values[i + 1], values[i + 2], values[i + 3]).normalize();
        qd.copy(inv0).multiply(q);
        if (i >= 4) {
          const px = nv[i - 4], py = nv[i - 3], pz = nv[i - 2], pw = nv[i - 1];
          if (px * qd.x + py * qd.y + pz * qd.z + pw * qd.w < 0) {
            qd.x = -qd.x; qd.y = -qd.y; qd.z = -qd.z; qd.w = -qd.w;
          }
        }
        nv[i] = qd.x; nv[i + 1] = qd.y; nv[i + 2] = qd.z; nv[i + 3] = qd.w;
      }
      out.push(new THREE.QuaternionKeyframeTrack(tr.name, times, nv));
    } else if (tr instanceof THREE.VectorKeyframeTrack || tr.name.endsWith('.position')) {
      const nv = new Float32Array(values.length);
      const x0 = values[0], y0 = values[1], z0 = values[2];
      for (let i = 0; i < values.length; i += 3) {
        nv[i] = values[i] - x0;
        nv[i + 1] = values[i + 1] - y0;
        nv[i + 2] = values[i + 2] - z0;
      }
      out.push(new THREE.VectorKeyframeTrack(tr.name, times, nv));
    } else {
      out.push(tr);
    }
  }
  return new THREE.AnimationClip(clip.name, clip.duration, out);
}

/** Parent-first order for helper bones. */
function _helperBonesOrdered(helper) {
  const bones = helper.skeleton?.bones || [];
  const ordered = [];
  const placed = new Set();
  const visit = (b) => {
    if (!b || placed.has(b.uuid)) return;
    let p = b.parent;
    while (p && !p.isBone) p = p.parent;
    if (p && p.isBone) visit(p);
    if (!placed.has(b.uuid)) {
      placed.add(b.uuid);
      ordered.push(b);
    }
  };
  for (const b of bones) visit(b);
  return ordered;
}

/**
 * (2) Rest/bind offset: set helper locals so world bind matches FBX bind for mapped bones.
 * Call AFTER Z-up on helperRoot; uses mesh bind pose. Animation must be rest-deltas.
 */
function alignHelperBindToFbx(helper, mesh, names) {
  mesh.skeleton.pose();
  if (mesh.parent) mesh.updateMatrixWorld(true);
  else mesh.updateMatrixWorld(true);
  helper.updateMatrixWorld(true);

  // sourceName → targetBone
  const tgtBySrc = new Map();
  for (const [tName, sName] of Object.entries(names)) {
    const tb = mesh.skeleton.bones.find((b) => b.name === tName);
    if (tb) tgtBySrc.set(sName, tb);
  }

  const _local = new THREE.Matrix4();
  const _inv = new THREE.Matrix4();
  const _pos = new THREE.Vector3();
  const _quat = new THREE.Quaternion();
  const _scl = new THREE.Vector3();

  let aligned = 0;
  for (const bone of _helperBonesOrdered(helper)) {
    const tgt = tgtBySrc.get(bone.name);
    if (!tgt) continue;
    const desired = tgt.matrixWorld;
    let parentBone = bone.parent;
    while (parentBone && !parentBone.isBone && parentBone !== helper) parentBone = parentBone.parent;
    if (parentBone && parentBone.isBone) {
      _inv.copy(parentBone.matrixWorld).invert();
      _local.copy(_inv).multiply(desired);
    } else {
      // under helperRoot (has Z-up quat) — parent is Group
      const p = bone.parent;
      if (p) {
        _inv.copy(p.matrixWorld).invert();
        _local.copy(_inv).multiply(desired);
      } else {
        _local.copy(desired);
      }
    }
    _local.decompose(_pos, _quat, _scl);
    bone.position.copy(_pos);
    bone.quaternion.copy(_quat);
    bone.scale.set(1, 1, 1);
    bone.updateMatrixWorld(true);
    aligned += 1;
  }

  // Uniform scale: match mean bone length helper↔target on matched pairs (hip→spine etc.)
  let sumH = 0, sumT = 0, n = 0;
  for (const bone of helper.skeleton.bones) {
    const tgt = tgtBySrc.get(bone.name);
    if (!tgt || !bone.parent?.isBone || !tgt.parent?.isBone) continue;
    const hl = bone.position.length();
    const tl = tgt.position.length();
    if (hl > 1e-6 && tl > 1e-6) {
      sumH += hl;
      sumT += tl;
      n += 1;
    }
  }
  let scale = 1;
  if (n >= 4 && sumH > 1e-6) {
    scale = sumT / sumH;
    // Only scale root bone positions slightly via helperRoot — keep if sane
    if (scale > 0.05 && scale < 50) {
      helper.scale.setScalar(scale);
      helper.updateMatrixWorld(true);
    } else {
      scale = 1;
    }
  }

  return { aligned, scale, pairs: n };
}

/**
 * Tony path: helper skel matching MIN2 hierarchy → SkeletonUtils.retargetClip onto primary body SkinnedMesh.
 * Returns { clip, mixerRoot, meta }. mixerRoot MUST be the SkinnedMesh (tracks are .bones[name].*).
 */
function retargetClipViaHelper(payload, rootObj) {
  if (typeof SkeletonUtils === 'undefined' || !SkeletonUtils.retargetClip) {
    throw new Error('SkeletonUtils.retargetClip unavailable — import three/addons/utils/SkeletonUtils.js');
  }
  const { mesh, boneCount, name: meshName } = findBodySkinnedMeshForRetarget(rootObj);
  if (!mesh) throw new Error('no SkinnedMesh on FBX root');

  const helper = buildMin2HelperArmature(payload);
  const absClip = animationClipFromTracksPayload(payload);
  const { names, matched, total, unmatched } = buildRetargetNameMap(
    mesh.skeleton.bones,
    helper.skeleton.bones
  );
  const mapPct = total ? (100 * matched) / total : 0;
  if (matched < 8) {
    throw new Error(
      `retargetClip bone map too low: matched=${matched}/${total} (${mapPct.toFixed(1)}%) mesh=${meshName} unmatched=${(unmatched || []).slice(0, 12).join(',')}`
    );
  }

  // Bind-align helper to FBX, then play MIN2 deltas on that rest.
  const bindInfo = alignHelperBindToFbx(helper, mesh, names);
  const srcClip = clipToRestDeltas(absClip);

  let hipTarget =
    mesh.skeleton.bones.find((b) => _normNameKey(b.name) === 'bip01') ||
    mesh.skeleton.bones.find((b) => /pelvis|hip/.test(_normNameKey(b.name)));
  const hip = hipTarget ? names[hipTarget.name] || hipTarget.name : 'bip01';

  const fps = Number(payload.fps) || 30;
  mesh.skeleton.pose();
  rootObj.updateMatrixWorld(true);
  helper.updateMatrixWorld(true);

  let retargeted;
  try {
    retargeted = SkeletonUtils.retargetClip(mesh, helper, srcClip, {
      names,
      hip,
      fps,
      preserveHipPosition: false,
      useFirstFramePosition: true,
    });
  } catch (err) {
    throw new Error(
      `SkeletonUtils.retargetClip threw matched=${matched}/${total} hip=${hip} scale=${bindInfo.scale} aligned=${bindInfo.aligned}: ${err?.message || err}`
    );
  }

  mesh.skeleton.pose();
  rootObj.updateMatrixWorld(true);

  retargeted.name = payload.name || retargeted.name || 'clip';
  if (!retargeted.duration || retargeted.duration < 0) {
    retargeted.duration = Number(payload.duration) || srcClip.duration || 0;
  }
  const meta = {
    matched,
    total,
    mapPct,
    meshName,
    boneCount,
    hip,
    tracks: retargeted.tracks.length,
    space: 'min2HelperRetarget',
    driver: 'SkeletonUtils.retargetClip',
    axis: 'zUpToYUp',
    bindAligned: bindInfo.aligned,
    helperScale: bindInfo.scale,
    unmatchedSample: (unmatched || []).slice(0, 8),
  };
  retargeted.userData = { ...(retargeted.userData || {}), ...meta };
  return { clip: retargeted, mixerRoot: mesh, meta, helper };
}
