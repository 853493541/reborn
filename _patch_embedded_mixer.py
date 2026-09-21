from pathlib import Path
import re

ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")

START_EMBEDDED = r'''
/** Play first embedded FBX clip via AnimationMixer (map-viewer actor-viewer path). */
async function startEmbeddedMixer(rootObj) {
  const raw = Array.isArray(rootObj.animations) ? rootObj.animations : [];
  if (!raw.length) return false;
  const clips = raw.map((clip) => {
    const prepared = clip.clone();
    prepared.tracks = prepared.tracks.map((track) => {
      const t = track.clone();
      t.name = normalizeTrackBoneName(t.name);
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
    // frame index at ~30fps if not specified — prefer duration fraction
    const fps = Number(params.get('fps')) || (clip.tracks[0]?.times?.length > 1
      ? (clip.tracks[0].times.length - 1) / clipDuration
      : 30);
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
    source: 'fbx-embedded',
  };
  hud.textContent += ` · mixer ${clip.name} t=${mixer.time.toFixed(3)}s tracks=${clip.tracks.length} (embedded)`;
  return true;
}

'''

def patch(path: Path) -> bool:
    js = path.read_text(encoding='utf-8')
    if 'startEmbeddedMixer' not in js:
        if 'async function startMixerClip' not in js:
            print(path, 'startMixerClip missing — run mixer patch first')
            return False
        # insert after startMixerClip function ends — before applyPoseByName
        marker = 'function applyPoseByName(bones)'
        if marker not in js:
            print(path, 'no applyPoseByName')
            return False
        js = js.replace(marker, START_EMBEDDED + marker, 1)

    # Prefer embedded clips over CLIP_URL JSON
    old = """      if (CLIP_URL) {
        try {
          await startMixerClip(root, CLIP_URL);
          if (window.__MIXER_READY__) {
            window.__FBX_READY__.mixer = window.__MIXER_READY__;
          }
        } catch (err) {
          console.error('mixer clip failed', err);
          window.__FBX_ERROR__ = `mixer: ${err?.message || err}`;
          hud.textContent += ` · mixer FAIL`;
        }
      }"""
    new = """      // Prefer FBX-embedded clips (map-viewer GT). Fall back to ?clip= JSON.
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
    if old not in js:
        print(path, 'CLIP_URL block missing')
        return False
    js = js.replace(old, new, 1)
    path.write_text(js, encoding='utf-8')
    print(path, 'OK')
    return True

ok = True
for rel in ('web/fbx_viewport.js', 'viewport_fbx/fbx_viewport.js'):
    ok = patch(ROOT / rel) and ok
raise SystemExit(0 if ok else 1)
