from pathlib import Path

ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")

# Add CLIP_FBX_URL param and loader that fetches second FBX for animations only.
PARAM_OLD = (
    "const CLIP_URL = params.get('clip') || '';\n"
    "const CLIP_TIME = params.has('t') ? Number(params.get('t')) : (params.has('frame') ? null : 0);\n"
    "const CLIP_FRAME = params.has('frame') ? Number(params.get('frame')) : null;"
)
PARAM_NEW = (
    "const CLIP_URL = params.get('clip') || '';\n"
    "const CLIP_FBX_URL = params.get('clipFbx') || params.get('clip_fbx') || '';\n"
    "const CLIP_TIME = params.has('t') ? Number(params.get('t')) : (params.has('frame') ? null : 0);\n"
    "const CLIP_FRAME = params.has('frame') ? Number(params.get('frame')) : null;"
)

LOAD_CLIP_FBX = r'''
/** Map-viewer clip-source: load another FBX only for root.animations, play on skin root. */
async function loadAnimationsFromClipFbx(url) {
  return new Promise((resolve, reject) => {
    const loader = new FBXLoader();
    loader.load(
      url,
      (obj) => resolve(Array.isArray(obj.animations) ? obj.animations : []),
      undefined,
      reject,
    );
  });
}

async function startMixerFromClips(rootObj, rawClips, sourceLabel) {
  const raw = Array.isArray(rawClips) ? rawClips : [];
  if (!raw.length) return false;
  const clips = raw.map((clip) => {
    const prepared = clip.clone();
    prepared.tracks = prepared.tracks.map((track) => {
      const t = track.clone();
      const name = String(track.name || '');
      const propertyIndex = name.lastIndexOf('.');
      if (propertyIndex >= 0) {
        const bindingPath = name.slice(0, propertyIndex);
        const propertyName = name.slice(propertyIndex);
        const normalizedBindingPath = bindingPath
          .split('/')
          .map((segment) => String(segment || '').split(':').pop())
          .join('/');
        t.name = normalizedBindingPath + propertyName;
      }
      return t;
    });
    prepared.resetDuration();
    return prepared;
  });
  const clip = clips[0];
  clipDuration = clip.duration || 0;
  mixer = new THREE.AnimationMixer(rootObj);
  clipAction = mixer.clipAction(clip);
  clipAction.enabled = true;
  clipAction.setLoop(THREE.LoopRepeat, Infinity);
  clipAction.clampWhenFinished = false;
  clipAction.reset().play();
  let t = 0;
  if (CLIP_FRAME != null && Number.isFinite(CLIP_FRAME) && clipDuration > 0) {
    const fps = Number(params.get('fps')) || 30;
    t = CLIP_FRAME / fps;
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
    clip: clip.name,
    duration: clipDuration,
    time: mixer.time,
    tracks: clip.tracks.length,
    source: sourceLabel || 'clips',
  };
  hud.textContent += ` · mixer ${clip.name} t=${mixer.time.toFixed(3)}s tracks=${clip.tracks.length} (${sourceLabel || 'clips'})`;
  return true;
}

'''

def patch(path: Path) -> bool:
    js = path.read_text(encoding='utf-8')
    if PARAM_OLD not in js:
        print(path, 'param block missing')
        return False
    js = js.replace(PARAM_OLD, PARAM_NEW, 1)

    if 'loadAnimationsFromClipFbx' not in js:
        marker = 'async function startEmbeddedMixer'
        if marker not in js:
            print(path, 'startEmbeddedMixer missing')
            return False
        js = js.replace(marker, LOAD_CLIP_FBX + marker, 1)

    # Rewrite startEmbeddedMixer body to use startMixerFromClips
    # And change the ready block preference order: clipFbx > embedded > clip json
    old_ready = """      // Prefer FBX-embedded clips (map-viewer GT). Fall back to ?clip= JSON.
      try {
        const embedded = await startEmbeddedMixer(root);
        if (!embedded && CLIP_URL) {
          await startMixerClip(root, CLIP_URL);
        }
        if (window.__MIXER_READY__) {
          window.__FBX_READY__.mixer = window.__MIXER_READY__;
        }
      } catch (err) {
        console.error('mixer clip failed', err);
        window.__FBX_ERROR__ = `mixer: ${err?.message || err}`;
        hud.textContent += ` · mixer FAIL`;
      }"""
    new_ready = """      // Map-viewer: skin FBX + clip-source FBX animations. Then embedded. Then ?clip= JSON.
      try {
        let ok = false;
        if (CLIP_FBX_URL) {
          const anims = await loadAnimationsFromClipFbx(CLIP_FBX_URL);
          ok = await startMixerFromClips(root, anims, 'clipFbx');
        }
        if (!ok) {
          ok = await startEmbeddedMixer(root);
        }
        if (!ok && CLIP_URL) {
          await startMixerClip(root, CLIP_URL);
        }
        if (window.__MIXER_READY__) {
          window.__FBX_READY__.mixer = window.__MIXER_READY__;
        }
      } catch (err) {
        console.error('mixer clip failed', err);
        window.__FBX_ERROR__ = `mixer: ${err?.message || err}`;
        hud.textContent += ` · mixer FAIL`;
      }"""
    if old_ready not in js:
        print(path, 'ready mixer block missing')
        return False
    js = js.replace(old_ready, new_ready, 1)

    # Simplify startEmbeddedMixer to delegate
    old_emb = None
    # Leave startEmbeddedMixer as-is if it still works; clipFbx path is primary.

    path.write_text(js, encoding='utf-8')
    print(path, 'OK')
    return True

ok = True
for rel in ('web/fbx_viewport.js', 'viewport_fbx/fbx_viewport.js'):
    ok = patch(ROOT / rel) and ok
raise SystemExit(0 if ok else 1)
