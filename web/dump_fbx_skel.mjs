import { chromium } from 'playwright-core';
import { writeFileSync, mkdirSync } from 'fs';
import { dirname } from 'path';
function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  if (i >= 0 && process.argv[i + 1]) return process.argv[i + 1];
  return fallback;
}
const url = arg('--url');
const out = arg('--out');
const chrome = arg('--chrome');
const timeout = Number(arg('--timeout', '90000'));
mkdirSync(dirname(out), { recursive: true });
const browser = await chromium.launch({
  executablePath: chrome, headless: true,
  args: ['--no-sandbox', '--disable-gpu', '--use-gl=angle', '--use-angle=swiftshader'],
});
try {
  const page = await browser.newPage({ viewport: { width: 800, height: 600 } });
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout });
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const ok = await page.evaluate(() => !!(window.__FBX_READY__ && window.__actor?.bonesByName));
    if (ok) break;
    await page.waitForTimeout(200);
  }
  const result = await page.evaluate(() => {
    const map = window.__actor.bonesByName;
    const byUuid = new Map();
    for (const b of map.values()) byUuid.set(b.uuid, b);
    const bones = [];
    const seen = new Set();
    for (const b of map.values()) {
      if (seen.has(b.uuid)) continue;
      seen.add(b.uuid);
      b.updateWorldMatrix(true, false);
      let p = b.parent;
      while (p && !(p.isBone || p.type === 'Bone')) p = p.parent;
      if (p && p.uuid === b.uuid) p = null;
      bones.push({
        name: b.name,
        parent: p ? p.name : null,
        parentUuid: p ? p.uuid : null,
        pos: [b.position.x, b.position.y, b.position.z],
        quat: [b.quaternion.x, b.quaternion.y, b.quaternion.z, b.quaternion.w],
        scale: [b.scale.x, b.scale.y, b.scale.z],
      });
    }
    return { boneCount: bones.length, bones };
  });
  writeFileSync(out, JSON.stringify(result));
  console.log('META_JSON=' + JSON.stringify({ boneCount: result.boneCount }));
} finally {
  await browser.close();
}
