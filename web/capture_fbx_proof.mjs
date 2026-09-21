/**
 * Headless capture of web/fbx_viewport.html after window.__FBX_READY__.
 * Usage: node capture_fbx_proof.mjs --url URL --out PNG [--chrome PATH] [--timeout MS]
 */
import { chromium } from 'playwright-core';
import { mkdirSync } from 'fs';
import { dirname } from 'path';

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  if (i >= 0 && process.argv[i + 1]) return process.argv[i + 1];
  return fallback;
}

const url = arg('--url');
const out = arg('--out');
const chrome = arg('--chrome', '/usr/bin/google-chrome');
const timeout = Number(arg('--timeout', '90000'));

if (!url || !out) {
  console.error('Usage: --url URL --out PNG');
  process.exit(2);
}

mkdirSync(dirname(out), { recursive: true });

const browser = await chromium.launch({
  executablePath: chrome,
  headless: true,
  args: ['--no-sandbox', '--disable-gpu', '--use-gl=angle', '--use-angle=swiftshader'],
});

try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  page.on('console', (msg) => {
    const t = msg.text();
    if (/error|fail|FBX/i.test(t)) console.log('PAGE:', t);
  });
  page.on('pageerror', (err) => console.log('PAGEERROR:', err.message));

  await page.goto(url, { waitUntil: 'domcontentloaded', timeout });

  const deadline = Date.now() + timeout;
  let ready = false;
  let err = null;
  let meta = null;
  while (Date.now() < deadline) {
    const state = await page.evaluate(() => ({
      ready: !!window.__FBX_READY__,
      error: window.__FBX_ERROR__ || null,
      meta: window.__FBX_META__ || null,
    }));
    if (state.error) {
      err = state.error;
      break;
    }
    if (state.ready) {
      ready = true;
      meta = state.meta;
      break;
    }
    await page.waitForTimeout(200);
  }

  if (err) {
    console.error('FBX_ERROR', err);
    process.exit(1);
  }
  if (!ready) {
    console.error('TIMEOUT waiting for __FBX_READY__');
    // still dump a screenshot for debugging
    await page.screenshot({ path: out, type: 'png' });
    process.exit(1);
  }

  // Pull latest pose_by_name (written by fbx_actor.apply_pose) and apply once.
  try {
    const pose = await page.evaluate(async () => {
      const urls = ['./runtime/pose_by_name.json', './runtime/pose.json'];
      for (const u of urls) {
        try {
          const res = await fetch(u + '?t=' + Date.now(), { cache: 'no-store' });
          if (!res.ok) continue;
          return await res.json();
        } catch (_) {}
      }
      return null;
    });
    if (pose) {
      await page.evaluate((pose) => {
        if (typeof window.__applyPose === 'function') {
          if (pose.bones) window.__applyPose(pose);
          else if (pose.matrices) window.__applyPose({ bones: null, matrices: pose.matrices, bone_names: pose.bone_names });
        }
      }, pose);
      await page.waitForTimeout(200);
    }
  } catch (e) {
    console.log('POSE_APPLY_WARN', e.message || e);
  }

  // Extra frames for texture settle + pose
  await page.waitForTimeout(1000);
  await page.screenshot({ path: out, type: 'png' });
  console.log('META_JSON=' + JSON.stringify(meta || {}));
  console.log('WROTE', out);
} finally {
  await browser.close();
}
