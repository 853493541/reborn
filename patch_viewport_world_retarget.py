# -*- coding: utf-8 -*-
"""Patch fbx_viewport.js: world-space MIN2 JSON → live-skeleton local tracks → Mixer."""
from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parent

NEW_FUNCS = r'''
function animationClipFromJson(payload, rootObj) {
  if (payload && payload.space === 'world') {
    return animationClipFromWorldJson(payload, rootObj || (typeof root !== 'undefined' ? root : null));
  }
  const tracks = [];
  for (const tr of payload.tracks || []) {
    const tname = normalizeTrackBoneName(tr.name);
    const times = Float32Array.from(tr.times || []);
    const values = Float32Array.from(tr.values || []);
    if (!times.length || !values.length) continue;
    if (tr.type === 'quaternion' || tname.endsWith('.quaternion')) {
      tracks.push(new THREE.QuaternionKeyframeTrack(tname, times, values));
    } else if (tr.type === 'vector' || tname.endsWith('.position') || tname.endsWith('.scale')) {
      tracks.push(new THREE.VectorKeyframeTrack(tname, times, values));
    }
  }
  const duration = Number(payload.duration) || timesMax(tracks);
  return new THREE.AnimationClip(payload.name || 'clip', duration, tracks);
}

function _normNameKey(n) {
  return String(n || '').toLowerCase().replace(/[\s\-]+/g, '_').trim();
}

function animationClipFromWorldJson(payload, rootObj) {
  // World MIN2 → parent-local using LIVE bone.matrixWorld (handles duplicate names).
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

  const bones = [];
  const seen = new Set();
  for (const b of boneByName.values()) {
    if (seen.has(b.uuid)) continue;
    seen.add(b.uuid);
    bones.push(b);
  }
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
  for (const b of bones) visit(b);

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

  let matched = 0;
  for (let fi = 0; fi < fc; fi++) {
    // Rest bind
    for (const b of ordered) {
      const bind = bindLocal.get(b.uuid);
      b.position.copy(bind.pos);
      b.quaternion.copy(bind.quat);
      b.scale.copy(bind.scl);
      b.matrixAutoUpdate = true;
    }
    if (rootObj) rootObj.updateMatrixWorld(true);
    else ordered[0]?.updateMatrixWorld?.(true);

    for (const b of ordered) {
      const src = worldByKey.get(_normNameKey(b.name));
      if (!src || !src.positions[fi] || !src.quats[fi]) continue;
      if (fi === 0) matched += 1;
      const p = src.positions[fi];
      const q = src.quats[fi];
      _pos.set(p[0], p[1], p[2]);
      _quat.set(q[0], q[1], q[2], q[3]).normalize();
      _scl.set(1, 1, 1);
      _min2.compose(_pos, _quat, _scl);

      let parentBone = b.parent;
      while (parentBone && !(parentBone.isBone || parentBone.type === 'Bone')) {
        parentBone = parentBone.parent;
      }
      if (parentBone && parentBone.uuid === b.uuid) parentBone = null;

      const parentWM = parentBone ? parentBone.matrixWorld : _ident;
      _inv.copy(parentWM).invert();
      _local.copy(_inv).multiply(_min2);
      _local.decompose(_pos, _quat, _scl);
      b.position.copy(_pos);
      b.quaternion.copy(_quat);
      // keep bind scale
      b.scale.copy(bindLocal.get(b.uuid).scl);
      b.updateMatrixWorld(true);
    }

    for (const b of ordered) {
      if (!worldByKey.has(_normNameKey(b.name))) continue;
      posSeries.get(b.uuid).push(b.position.x, b.position.y, b.position.z);
      const arr = quatSeries.get(b.uuid);
      const qx = b.quaternion.x;
      const qy = b.quaternion.y;
      const qz = b.quaternion.z;
      const qw = b.quaternion.w;
      if (arr.length >= 4) {
        const px = arr[arr.length - 4];
        const py = arr[arr.length - 3];
        const pz = arr[arr.length - 2];
        const pw = arr[arr.length - 1];
        if (px * qx + py * qy + pz * qz + pw * qw < 0) {
          arr.push(-qx, -qy, -qz, -qw);
          continue;
        }
      }
      arr.push(qx, qy, qz, qw);
    }
  }

  // Restore bind after baking keyframes
  for (const b of ordered) {
    const bind = bindLocal.get(b.uuid);
    b.position.copy(bind.pos);
    b.quaternion.copy(bind.quat);
    b.scale.copy(bind.scl);
  }
  if (rootObj) rootObj.updateMatrixWorld(true);

  const tracks = [];
  for (const b of ordered) {
    if (!worldByKey.has(_normNameKey(b.name))) continue;
    tracks.push(new THREE.VectorKeyframeTrack(`${b.name}.position`, times, Float32Array.from(posSeries.get(b.uuid))));
    tracks.push(new THREE.QuaternionKeyframeTrack(`${b.name}.quaternion`, times, Float32Array.from(quatSeries.get(b.uuid))));
  }
  const clip = new THREE.AnimationClip(payload.name || 'clip', duration, tracks);
  clip.userData = { ...(clip.userData || {}), worldRetargetMatched: matched };
  return clip;
}

async function startMixerClip(rootObj, clipUrl) {
  const payload = await loadClipJson(clipUrl);
  const clip = animationClipFromJson(payload, rootObj);
  clipDuration = clip.duration || Number(payload.duration) || 0;
  mixer = new THREE.AnimationMixer(rootObj);
  clipAction = mixer.clipAction(clip);
  clipAction.enabled = true;
  clipAction.setLoop(THREE.LoopRepeat, Infinity);
  clipAction.clampWhenFinished = false;
  clipAction.reset().play();
  let t = 0;
  if (CLIP_FRAME != null && Number.isFinite(CLIP_FRAME) && payload.fps) {
    t = CLIP_FRAME / Number(payload.fps);
  } else if (CLIP_TIME != null && Number.isFinite(CLIP_TIME)) {
    t = CLIP_TIME;
  } else if (clipDuration > 0) {
    t = clipDuration * 0.45;
  }
  mixer.setTime(Math.max(0, Math.min(t, Math.max(clipDuration, 0.0001))));
  mixer.update(0);
  rootObj.updateMatrixWorld(true);
  if (headAttachments) updateAttachmentsMultiPass(headAttachments, rootObj, 3);
  rootObj.traverse((o) => { if (o.isSkinnedMesh) o.skeleton?.update(); });
  useMixer = true;
  window.__MIXER_READY__ = {
    clip: payload.name,
    duration: clipDuration,
    time: mixer.time,
    tracks: clip.tracks.length,
    quatOnly: !!payload.quat_only,
    space: payload.space || 'local',
    matched: clip.userData?.worldRetargetMatched,
  };
  hud.textContent += ` · mixer ${payload.name} t=${mixer.time.toFixed(3)}s tracks=${clip.tracks.length}`;
}

'''


def patch_file(path: Path) -> None:
    src = path.read_text(encoding="utf-8")
    start = src.find("function animationClipFromJson")
    if start < 0:
        raise SystemExit(f"animationClipFromJson missing in {path}")
    end = src.find("async function startMixerClip", start)
    if end < 0:
        raise SystemExit("startMixerClip missing")
    brace = src.find("{", end)
    depth = 0
    j = brace
    while j < len(src):
        c = src[j]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                j += 1
                break
        j += 1
    path.write_text(src[:start] + NEW_FUNCS + src[j:], encoding="utf-8")
    print("patched", path)


def main() -> None:
    for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
        patch_file(ROOT / rel)
    print("DONE_PATCH_VIEWPORT")


if __name__ == "__main__":
    main()
