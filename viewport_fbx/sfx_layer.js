/**
 * Minimal MovieEditor-compatible SFX layer.
 *
 * The timeline and PSS paths come from the real GATA/PAR data exposed by
 * /api/sfx/flws. Emitter definitions and cache textures are fetched from the
 * optional local map-viewer cache service. If that service is unavailable, the
 * layer keeps the real timeline and reports the missing dependency instead of
 * silently becoming a fake pass.
 */
import * as THREE from 'three';
import { DDSLoader } from 'three/addons/loaders/DDSLoader.js';

const DEFAULT_SERVICE = 'http://127.0.0.1:3015';
const MAX_EMITTERS = 32;
const MAX_PARTICLES = 4;

function withServiceBase(url, serviceBase) {
  if (!url) return '';
  if (/^https?:\/\//i.test(url)) return url;
  return `${serviceBase.replace(/\/+$/, '')}/${String(url).replace(/^\/+/, '')}`;
}

function makeFallbackTexture(color = '#7fdcff') {
  const canvas = document.createElement('canvas');
  canvas.width = 96;
  canvas.height = 96;
  const ctx = canvas.getContext('2d');
  const gradient = ctx.createRadialGradient(48, 48, 2, 48, 48, 46);
  gradient.addColorStop(0, '#ffffff');
  gradient.addColorStop(0.18, color);
  gradient.addColorStop(0.62, `${color}88`);
  gradient.addColorStop(1, '#00000000');
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, 96, 96);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

function loadTexture(url, ddsLoader, textureLoader) {
  if (!url) return Promise.resolve(null);
  const loader = /\.dds(?:$|\?)/i.test(url) ? ddsLoader : textureLoader;
  return new Promise((resolve) => {
    loader.load(
      url,
      (texture) => {
        texture.colorSpace = THREE.SRGBColorSpace;
        texture.flipY = false;
        resolve(texture);
      },
      undefined,
      () => resolve(null),
    );
  });
}

function emitterColor(index) {
  const colors = ['#a7e7ff', '#70c8ff', '#d8f3ff', '#8caeff', '#fff0ad'];
  return colors[index % colors.length];
}

function eventTiming(payload) {
  const event = payload?.events?.find((item) => Number.isFinite(item?.start_time_ms));
  return {
    startMs: Number.isFinite(event?.start_time_ms) ? event.start_time_ms : 0,
    durationMs: Number.isFinite(event?.play_duration_ms)
      ? event.play_duration_ms
      : Number(payload?.timeline_duration_ms) || 5000,
    totalMs: Number(payload?.timeline_duration_ms) || 8000,
  };
}

function findAnchor(root, findBone) {
  for (const name of [
    'bip01_r_hand',
    'bip01_r_weapon',
    'bip01_r_finger0',
    'bip01_pelvis',
  ]) {
    const bone = findBone(name);
    if (bone) return { bone, name };
  }
  return { bone: root, name: 'actor-root' };
}

function primaryTextureUrl(emitter, analysis, serviceBase) {
  const paths = emitter?.texturePaths || [];
  const textures = analysis?.textures || [];
  const texture = textures.find((item) => paths.includes(item?.texturePath) && item?.rawUrl)
    || textures.find((item) => item?.rawUrl);
  return texture ? withServiceBase(texture.rawUrl, serviceBase) : '';
}

function eventKind(event) {
  if (event?.event_kind) return event.event_kind;
  const path = String(event?.logical_path || '').toLowerCase();
  return path.includes('风车范围') || path.includes('\\状态\\') ? 'range' : 'blade';
}

function clamp01(value) {
  return Math.max(0, Math.min(1, Number(value) || 0));
}

function sampleColor(definition, progress, kind = 'blade') {
  const curve = definition?.colorCurve;
  let rgba = null;
  if (Array.isArray(curve) && curve.length) {
    const scaled = clamp01(progress) * (curve.length - 1);
    const leftIndex = Math.floor(scaled);
    const rightIndex = Math.min(curve.length - 1, leftIndex + 1);
    const mix = scaled - leftIndex;
    const left = curve[leftIndex] || curve[0] || [1, 1, 1, 1];
    const right = curve[rightIndex] || left;
    rgba = [0, 1, 2, 3].map((channel) => {
      const leftValue = Number(left[channel]);
      const rightValue = Number(right[channel]);
      const safeLeft = Number.isFinite(leftValue) ? leftValue : 1;
      const safeRight = Number.isFinite(rightValue) ? rightValue : safeLeft;
      return safeLeft + (safeRight - safeLeft) * mix;
    });
  } else if (Array.isArray(definition?.materialTint)) {
    rgba = definition.materialTint.slice(0, 4);
  }
  const values = rgba || (kind === 'range' || kind === 'blade'
    ? [0.82, 0.035, 0.02, 1]
    : [1, 1, 1, 1]);
  return {
    color: new THREE.Color(
      clamp01(values[0]),
      clamp01(values[1]),
      clamp01(values[2]),
    ),
    alpha: clamp01(values[3] ?? 1),
  };
}

function rangeRadius(definition, index) {
  const spatialScalar = Number(definition?.runtimeParams?.spatialScalar);
  if (Number.isFinite(spatialScalar) && spatialScalar > 0) {
    return Math.max(120, Math.min(320, spatialScalar * 12));
  }
  return index < 2 ? 260 : 210;
}

function bladeSize(definition) {
  const spatialScalar = Number(definition?.runtimeParams?.spatialScalar);
  if (Number.isFinite(spatialScalar) && spatialScalar > 0) {
    return Math.max(8, Math.min(28, spatialScalar * 0.8));
  }
  return 12 + (Number(definition?.layerCount) || 1) * 3;
}

function rangeSpriteKind(definition) {
  if (definition?.launcherClass !== 'KG3D_LauncherCirque') return 'range-glow';
  const paths = (definition?.texturePaths || []).map((path) => String(path).toLowerCase());
  const hasAuraTexture = paths.some((path) => path.includes('c_藏剑气场'));
  if (hasAuraTexture && Number(definition?.index) === 5) return 'range-ring';
  return paths.some((path) => path.includes('g_光亮') || path.includes('noise010hd'))
    || hasAuraTexture
    ? 'range-swirls'
    : 'range-ring';
}

function rangeScale(definition, cycle) {
  const curve = definition?.runtimeParams?.sizeCurve;
  if (!Array.isArray(curve) || curve.length < 3) return 1;
  const start = Number(curve[0]);
  const middle = Number(curve[1]);
  const end = Number(curve[2]);
  if (![start, middle, end].every(Number.isFinite)) return 1;
  if (cycle <= 0.5) return start + (middle - start) * (cycle * 2);
  return middle + (end - middle) * ((cycle - 0.5) * 2);
}

function rangeRotationSpeed(definition, renderKind) {
  if (renderKind !== 'range-ring' && renderKind !== 'range-swirls') return 0;
  const modules = Array.isArray(definition?.modules) ? definition.modules : [];
  if (!modules.includes('速度') && !modules.includes('颜色贴图速度')) return 0;
  // The two c_藏剑气场 emitters are authored as a long-lived sweep and a
  // short pulse. Keep their opposite directions and different rates so the
  // inner pattern does not collapse into a static decal.
  const index = Number(definition?.index);
  if (index === 5) return -Math.PI * 3.2;
  if (index === 3) return -Math.PI * 0.85;
  if (index === 4) return Math.PI * 1.15;
  return Math.PI * 0.45;
}

function makeRangeGeometry(definition, index, renderKind) {
  const radius = rangeRadius(definition, index);
  if (renderKind === 'range-ribbon') {
    return {
      geometry: new THREE.TorusGeometry(
        radius * 0.96,
        Math.max(4, radius * 0.018),
        8,
        128,
      ),
      radius,
    };
  }
  if (renderKind === 'range-glow') {
    return {
      geometry: new THREE.CircleGeometry(radius * 0.82, 128),
      radius,
    };
  }
  if (renderKind === 'range-swirls') {
    return {
      geometry: new THREE.CircleGeometry(radius * 0.85, 128),
      radius,
    };
  }
  // KG3D_LauncherCirque emits authored sprite particles around the circle.
  // Do not flatten those particles into a synthetic annulus: that destroys
  // the texture's directional inner-ring motion.
  return {
    geometry: new THREE.PlaneGeometry(radius * 0.16, radius * 0.28),
    radius,
  };
}

export async function createSfxLayer({
  scene,
  camera,
  root,
  findBone,
  apiUrl = './api/sfx/flws',
  serviceBase = DEFAULT_SERVICE,
  onStatus = () => {},
}) {
  const layer = {
    enabled: true,
    ready: false,
    payload: null,
    analysis: null,
    group: new THREE.Group(),
    handGroup: new THREE.Group(),
    groups: [],
    emitters: [],
    timing: { startMs: 0, durationMs: 5000, totalMs: 8000 },
    timeMs: 0,
    status: 'loading SFX timeline',
    update(timeMs) {
      this.timeMs = Number.isFinite(timeMs) ? Math.max(0, timeMs) : 0;
      const { startMs, durationMs } = this.timing;
      let rootActive = false;
      let handActive = false;
      for (const emitter of this.emitters) {
        const emitterStartMs = Number.isFinite(emitter.startMs) ? emitter.startMs : startMs;
        const emitterDurationMs = Number.isFinite(emitter.durationMs)
          ? emitter.durationMs
          : durationMs;
        const emitterActive = this.timeMs >= emitterStartMs
          && this.timeMs <= emitterStartMs + emitterDurationMs;
        if (emitter.kind === 'range') rootActive = rootActive || emitterActive;
        else handActive = handActive || emitterActive;
        const localMs = Math.max(0, this.timeMs - emitterStartMs);
        const progress = emitterDurationMs > 0
          ? Math.min(1, localMs / emitterDurationMs)
          : 0;
        const age = Math.max(0, localMs - emitter.delayMs);
        const cycle = Math.min(1, age / Math.max(1, emitter.lifeMs));
        const fade = emitterActive
          ? Math.min(1, Math.min(cycle * 8, (1 - cycle) * 4 + 0.05))
          : 0;
        emitter.group.visible = emitterActive && age >= 0;
        for (let i = 0; i < emitter.particles.length; i += 1) {
          const particle = emitter.particles[i];
          particle.mesh.visible = emitterActive && age >= 0;
          const color = sampleColor(emitter.definition, progress, emitter.kind);
          particle.material.color.copy(color.color);
          particle.material.opacity = fade * particle.maxOpacity * color.alpha;
          if (emitter.kind === 'range') {
            const pulse = emitter.renderKind === 'range-ring'
              && emitter.lifeMs < emitter.durationMs * 0.25
              ? 0.72 + cycle * 0.28
              : 1;
            if (emitter.renderKind === 'range-ring') {
              const angle = particle.phase + (localMs / 1000) * (particle.rotationSpeed || 0);
              particle.mesh.position.set(
                Math.cos(angle) * particle.radius,
                particle.groundY || 0,
                Math.sin(angle) * particle.radius,
              );
              // Cirque particles stay on the horizontal ground plane; a
              // camera-facing billboard would turn this into a vertical ring.
              particle.mesh.rotation.set(-Math.PI / 2, 0, angle + Math.PI / 2);
              particle.mesh.scale.setScalar(particle.size * pulse * rangeScale(emitter.definition, cycle));
            } else {
              particle.mesh.position.set(0, particle.groundY || 0, 0);
              particle.mesh.scale.setScalar(particle.size * pulse);
              particle.mesh.rotation.z = (particle.rotationOffset || 0)
                + (localMs / 1000) * (particle.rotationSpeed || 0);
            }
          } else {
            const phase = particle.phase + progress * (0.7 + emitter.index * 0.025);
            const radius = particle.radius * (0.35 + cycle * 0.85);
            particle.mesh.position.set(
              Math.cos(phase) * radius,
              Math.sin(phase * 1.7) * radius * 0.35 + particle.height * (0.7 + cycle * 0.3),
              Math.sin(phase) * radius,
            );
            particle.mesh.scale.setScalar(particle.size * (0.65 + cycle * 0.55));
            particle.mesh.quaternion.copy(camera.quaternion);
          }
        }
      }
      this.group.visible = rootActive;
      this.handGroup.visible = handActive;
      onStatus({
        status: this.status,
        timeMs: this.timeMs,
        startMs,
        durationMs,
        emitters: this.emitters.length,
        source: this.payload?.events?.[0]?.logical_path || '',
      });
    },
    dispose() {
      for (const group of this.groups) {
        group.traverse((object) => {
          if (object.geometry) object.geometry.dispose();
          if (object.material) {
            if (object.material.map?.dispose) object.material.map.dispose();
            object.material.dispose();
          }
        });
        group.removeFromParent();
      }
    },
  };

  const handAnchor = findAnchor(root, findBone);
  root.add(layer.group);
  handAnchor.bone.add(layer.handGroup);
  layer.groups = [layer.group, layer.handGroup];
  layer.group.position.set(0, 0, 0);
  layer.handGroup.position.set(0, 0, 0);
  layer.group.scale.setScalar(0.85);
  layer.handGroup.scale.setScalar(0.85);
  layer.group.visible = false;
  layer.handGroup.visible = false;
  onStatus({
    status: `SFX anchors: range actor-root · blade ${handAnchor.name}`,
    progress: 0,
    phase: 'starting',
  });

  try {
    onStatus({ status: 'Loading SFX timeline…', progress: 0.04, phase: 'timeline' });
    const timelineResponse = await fetch(`${apiUrl}?t=${Date.now()}`, { cache: 'no-store' });
    if (!timelineResponse.ok) throw new Error(`timeline HTTP ${timelineResponse.status}`);
    layer.payload = await timelineResponse.json();
    layer.timing = eventTiming(layer.payload);
  } catch (error) {
    layer.status = `SFX timeline unavailable: ${error.message}`;
    onStatus({ status: layer.status, error: true });
    return layer;
  }

  const events = (layer.payload.events || []).filter((item) => item?.logical_path);
  if (!events.length) {
    layer.status = 'SFX timeline has no PSS event';
    onStatus({ status: layer.status, error: true });
    return layer;
  }
  onStatus({
    status: `Found ${events.length} PSS events`,
    progress: 0.12,
    phase: 'timeline',
    loaded: events.length,
    total: events.length,
  });

  const analyses = [];
  for (let eventIndex = 0; eventIndex < events.length; eventIndex += 1) {
    const event = events[eventIndex];
    onStatus({
      status: `Analyzing PSS metadata ${eventIndex + 1}/${events.length}…`,
      progress: 0.12 + (0.18 * eventIndex) / events.length,
      phase: 'analysis',
      loaded: eventIndex,
      total: events.length,
    });
    const analysisUrl = event.analysis_url || `${serviceBase}/api/pss/analyze`;
    try {
      const response = await fetch(`${analysisUrl}&t=${Date.now()}`, { cache: 'no-store' });
      if (!response.ok) throw new Error(`PSS analysis HTTP ${response.status}`);
      analyses.push({ event, analysis: await response.json() });
    } catch (error) {
      onStatus({ status: `PSS metadata unavailable: ${error.message}`, warning: true });
      analyses.push({ event, analysis: { emitters: [], totalTextures: 0 } });
    }
  }
  layer.analysis = analyses[0]?.analysis || { emitters: [], totalTextures: 0 };

  const textureLoader = new THREE.TextureLoader();
  const ddsLoader = new DDSLoader();
  const sourceEmitters = analyses.flatMap(({ event, analysis }) => (
    (analysis?.emitters || [])
      .filter((emitter) => (
        emitter?.type === 'sprite'
        || (emitter?.type === 'mesh' && eventKind(event) === 'range')
      ))
      .map((definition) => ({ event, analysis, definition }))
  )).slice(0, MAX_EMITTERS);
  onStatus({
    status: `Loading SFX textures 0/${sourceEmitters.length}…`,
    progress: sourceEmitters.length ? 0.3 : 0.95,
    phase: 'textures',
    loaded: 0,
    total: sourceEmitters.length,
  });

  for (let index = 0; index < sourceEmitters.length; index += 1) {
    const { event, analysis, definition } = sourceEmitters[index];
    const kind = eventKind(event);
    const renderKind = kind === 'range'
      ? (definition.type === 'mesh' ? 'range-ribbon' : rangeSpriteKind(definition))
      : 'blade-sprite';
    const remoteTexture = await loadTexture(
      primaryTextureUrl(definition, analysis, serviceBase),
      ddsLoader,
      textureLoader,
    );
    const texture = remoteTexture || makeFallbackTexture(kind === 'range' ? '#d52d2d' : emitterColor(index));
    onStatus({
      status: `Loading SFX textures ${index + 1}/${sourceEmitters.length}…`,
      progress: 0.3 + (0.65 * (index + 1)) / Math.max(1, sourceEmitters.length),
      phase: 'textures',
      loaded: index + 1,
      total: sourceEmitters.length,
    });
    const blend = String(definition.blendMode || '').toLowerCase() === 'normal'
      ? THREE.NormalBlending
      : THREE.AdditiveBlending;
    const group = new THREE.Group();
    const particles = [];
    const rangeShape = kind === 'range'
      ? makeRangeGeometry(definition, index, renderKind)
      : null;
    const count = kind === 'range'
      ? (renderKind === 'range-ring'
        ? Math.max(24, Math.min(120, Number(definition.runtimeParams?.maxParticles) || 96))
        : 1)
      : Math.max(1, Math.min(MAX_PARTICLES, Number(definition.layerCount) || 1));
    const initialColor = sampleColor(definition, 0.5, kind);
    for (let particleIndex = 0; particleIndex < count; particleIndex += 1) {
      const material = new THREE.MeshBasicMaterial({
        map: texture,
        color: initialColor.color,
        transparent: true,
        depthWrite: false,
        side: THREE.DoubleSide,
        blending: blend,
        opacity: 0,
      });
      const meshGeometry = kind === 'range'
        ? rangeShape.geometry
        : new THREE.PlaneGeometry(1, 1.8);
      const mesh = new THREE.Mesh(meshGeometry, material);
      mesh.renderOrder = 12;
      if (kind === 'range') {
        mesh.rotation.x = -Math.PI / 2;
      }
      group.add(mesh);
      particles.push({
        mesh,
        material,
        groundY: 0,
        phase: renderKind === 'range-ring'
          ? (particleIndex / count) * Math.PI * 2
          : (index * 0.71) + (particleIndex * 2.1),
        radius: renderKind === 'range-ring'
          ? rangeShape.radius
          : (kind === 'range' ? 0 : 7 + (index % 4) * 4),
        height: kind === 'range' ? 0 : 3 + (index % 3) * 5,
        size: kind === 'range' ? 1 : bladeSize(definition),
        rotationSpeed: kind === 'range' ? rangeRotationSpeed(definition, renderKind) : 0,
        rotationOffset: kind === 'range' ? (particleIndex * Math.PI) : 0,
        maxOpacity: renderKind === 'range-glow'
          ? 0.22
          : renderKind === 'range-swirls'
            ? 0.72
            : (String(definition.blendMode || '').toLowerCase() === 'normal' ? 0.82 : 0.7),
      });
    }
    const parent = kind === 'range' ? layer.group : layer.handGroup;
    parent.add(group);
    const authoredLifeMs = Number(definition.runtimeParams?.lifetimeSeconds) * 1000;
    layer.emitters.push({
      index,
      kind,
      renderKind,
      definition,
      group,
      particles,
      startMs: Number.isFinite(event.start_time_ms) ? event.start_time_ms : layer.timing.startMs,
      durationMs: Number.isFinite(event.play_duration_ms)
        ? event.play_duration_ms
        : layer.timing.durationMs,
      delayMs: kind === 'range' ? 0 : (index % 5) * 80,
      lifeMs: Number.isFinite(authoredLifeMs) && authoredLifeMs > 0
        ? Math.min(authoredLifeMs, layer.timing.durationMs)
        : (kind === 'range' ? layer.timing.durationMs : 700 + (index % 4) * 500),
    });
  }

  layer.ready = true;
  const rangeMeshCount = layer.emitters.filter((item) => item.renderKind === 'range-ribbon').length;
  const rangeSpriteCount = layer.emitters.filter((item) => (
    item.renderKind === 'range-ring'
      || item.renderKind === 'range-swirls'
      || item.renderKind === 'range-glow'
  )).length;
  const rangeGlowCount = layer.emitters.filter((item) => item.renderKind === 'range-glow').length;
  const rangeSwirlCount = layer.emitters.filter((item) => item.renderKind === 'range-swirls').length;
  const bladeSpriteCount = layer.emitters.filter((item) => item.renderKind === 'blade-sprite').length;
  layer.status = layer.emitters.length
    ? `SFX ready · blade ${bladeSpriteCount} sprites · range ${rangeSpriteCount - rangeGlowCount - rangeSwirlCount} rings + ${rangeSwirlCount} center + ${rangeGlowCount} glow + ${rangeMeshCount} ribbons`
    : 'SFX timeline ready · no decoded emitters';
  onStatus({
    status: layer.status,
    progress: 1,
    phase: 'ready',
    loaded: sourceEmitters.length,
    total: sourceEmitters.length,
  });
  onStatus({
    status: layer.status,
    warning: layer.emitters.length === 0,
    source: events.map((item) => item.logical_path).join(', '),
    pssBytes: events.reduce((total, item) => total + (item.local_bytes || 0), 0),
    textureCount: analyses.reduce((total, item) => total + (item.analysis?.totalTextures || 0), 0),
  });
  return layer;
}
