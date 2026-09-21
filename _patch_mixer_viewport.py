from pathlib import Path
import re

CLIP_FN = r'''
/** Build THREE.AnimationClip from exported MIN2 clip JSON; drive via AnimationMixer (map-viewer GT). */
function normalizeTrackBoneName(trackName) {
  const name = String(trackName || '');
  const propertyIndex = name.lastIndexOf('.');
  if (propertyIndex < 0) return name;
  const bindingPath = name.slice(0, propertyIndex);
  const propertyName = name.slice(propertyIndex);
  const bone = findBone(bindingPath) || findBone(bindingPath.split('/').pop());
  if (bone) return `${bone.name}${propertyName}`;
  return name;
}

async function loadClipJson(url) {
  const res = await fetch(`${url}?t=${Date.now()}`, { cache: 'no-store' });
  if (!res.ok) throw new Error(`clip HTTP ${res.status}`);
  return res.json();
}

function timesMax(tracks) {
  let m = 0;
  for (const t of tracks) {
    const arr = t.times;
    if (arr && arr.length) m = Math.max(m, arr[arr.length - 1]);
  }
  return m;
}

function animationClipFromJson(payload) {
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

async function startMixerClip(rootObj, clipUrl) {
  const payload = await loadClipJson(clipUrl);
  const clip = animationClipFromJson(payload);
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
  rootObj.traverse((o) => { if (o.isSkinnedMesh) o.skeleton?.update(); });
  useMixer = true;
  window.__MIXER_READY__ = {
    clip: payload.name,
    duration: clipDuration,
    time: mixer.time,
    tracks: clip.tracks.length,
    quatOnly: !!payload.quat_only,
  };
  hud.textContent += ` · mixer ${payload.name} t=${mixer.time.toFixed(3)}s tracks=${clip.tracks.length}`;
}

'''

def patch(path: Path) -> bool:
    js = path.read_text(encoding='utf-8')
    old_params = (
        "const params = new URLSearchParams(location.search);\n"
        "const FBX_URL = params.get('fbx') || './samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx';\n"
        "const TEX_BASE = (params.get('tex') || './samples/actor_presets/f1_hualuo/tex/').replace(/\\/?$/, '/');\n"
        "const POSE_URL = params.get('pose') || './runtime/pose.json';\n"
        "const CLOSEUP = params.get('closeup') === '1';"
    )
    new_params = (
        "const params = new URLSearchParams(location.search);\n"
        "const FBX_URL = params.get('fbx') || './samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx';\n"
        "const TEX_BASE = (params.get('tex') || './samples/actor_presets/f1_hualuo/tex/').replace(/\\/?$/, '/');\n"
        "const POSE_URL = params.get('pose') || './runtime/pose.json';\n"
        "const CLOSEUP = params.get('closeup') === '1';\n"
        "// Map-viewer GT path: AnimationMixer + clip JSON (MIN2→AnimationClip export).\n"
        "const CLIP_URL = params.get('clip') || '';\n"
        "const CLIP_TIME = params.has('t') ? Number(params.get('t')) : (params.has('frame') ? null : 0);\n"
        "const CLIP_FRAME = params.has('frame') ? Number(params.get('frame')) : null;\n"
        "let mixer = null;\n"
        "let clipAction = null;\n"
        "let clipDuration = 0;\n"
        "let mixerClock = new THREE.Clock(false);\n"
        "let useMixer = false;"
    )
    if old_params not in js:
        print(path, 'params missing')
        return False
    js = js.replace(old_params, new_params, 1)

    if 'startMixerClip' not in js:
        marker = 'function applyPoseByName(bones)'
        if marker not in js:
            print(path, 'applyPoseByName missing')
            return False
        js = js.replace(marker, CLIP_FN + marker, 1)

    old_ready = (
        "      window.__FBX_READY__ = { bones: bc, fbx: FBX_URL, tex: TEX_BASE, diffuseMapped: mapped };\n"
        "      window.__FBX_META__ = window.__FBX_READY__;\n"
        "      window.__fbxReady = true;\n"
        "      window.__actor = { root, placement, bonesByName: boneByName };\n"
        "      window.__applyPose = (matricesByName) => {\n"
        "        if (matricesByName?.bones) applyPoseByName(matricesByName.bones);\n"
        "        else applyPoseByName(matricesByName);\n"
        "      };\n"
        "      window.dispatchEvent(new Event('fbx-ready'));"
    )
    new_ready = (
        "      window.__FBX_READY__ = { bones: bc, fbx: FBX_URL, tex: TEX_BASE, diffuseMapped: mapped };\n"
        "      window.__FBX_META__ = window.__FBX_READY__;\n"
        "      window.__fbxReady = true;\n"
        "      window.__actor = { root, placement, bonesByName: boneByName };\n"
        "      window.__applyPose = (matricesByName) => {\n"
        "        if (useMixer) return; // Mixer is GT — ignore decompose pose feed\n"
        "        if (matricesByName?.bones) applyPoseByName(matricesByName.bones);\n"
        "        else applyPoseByName(matricesByName);\n"
        "      };\n"
        "      if (CLIP_URL) {\n"
        "        try {\n"
        "          await startMixerClip(root, CLIP_URL);\n"
        "          if (window.__MIXER_READY__) {\n"
        "            window.__FBX_READY__.mixer = window.__MIXER_READY__;\n"
        "          }\n"
        "        } catch (err) {\n"
        "          console.error('mixer clip failed', err);\n"
        "          window.__FBX_ERROR__ = `mixer: ${err?.message || err}`;\n"
        "          hud.textContent += ` · mixer FAIL`;\n"
        "        }\n"
        "      }\n"
        "      window.dispatchEvent(new Event('fbx-ready'));"
    )
    if old_ready not in js:
        print(path, 'ready block missing')
        return False
    js = js.replace(old_ready, new_ready, 1)

    old_frame = (
        "function frame() {\n"
        "  resize();\n"
        "  pollPose();\n"
        "  renderer.render(scene, camera);\n"
        "  requestAnimationFrame(frame);\n"
        "}"
    )
    new_frame = (
        "function frame() {\n"
        "  resize();\n"
        "  if (useMixer && mixer) {\n"
        "    if (CLIP_TIME == null && CLIP_FRAME == null) {\n"
        "      mixer.update(mixerClock.getDelta());\n"
        "    } else {\n"
        "      mixer.update(0);\n"
        "    }\n"
        "  } else {\n"
        "    pollPose();\n"
        "  }\n"
        "  renderer.render(scene, camera);\n"
        "  requestAnimationFrame(frame);\n"
        "}"
    )
    if old_frame not in js:
        print(path, 'frame missing')
        return False
    js = js.replace(old_frame, new_frame, 1)

    js, n = re.subn(
        r'(loader\.load\(\s*FBX_URL,\s*)(\(?obj\)?\s*=>\s*\{)',
        r'\1async (obj) => {',
        js,
        count=1,
    )
    # If already async, leave it
    if n == 0 and 'async (obj)' not in js[js.find('loader.load'):js.find('loader.load') + 80]:
        print(path, 'WARN could not make load callback async')
    path.write_text(js, encoding='utf-8')
    print(path, 'OK async_n=', n)
    return True

ok = True
for rel in ('web/fbx_viewport.js', 'viewport_fbx/fbx_viewport.js'):
    ok = patch(Path(r'C:\Users\Zhibin Ren\jx3-ani-player') / rel) and ok
raise SystemExit(0 if ok else 1)
