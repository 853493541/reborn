import { chromium } from 'playwright-core';
const url = process.argv[2];
const chrome = process.argv[3];
const browser = await chromium.launch({ executablePath: chrome, headless: true, args: ['--no-sandbox'] });
const page = await browser.newPage();
await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 120000 });
for (let i = 0; i < 300; i++) {
  const s = await page.evaluate(() => !!window.__FBX_READY__);
  if (s) break;
  await page.waitForTimeout(200);
}
const info = await page.evaluate(() => {
  const root = window.__actor?.root;
  if (!root) return { err: 'no root' };
  const meshes = [];
  root.traverse((o) => {
    if (!o.isSkinnedMesh || !o.skeleton?.bones?.length) return;
    const names = o.skeleton.bones.map((b) => b.name);
    const lower = new Set(names.map((n) => String(n || '').toLowerCase()));
    meshes.push({
      name: o.name,
      bones: names.length,
      hasPelvis: lower.has('bip01_pelvis'),
      hasHead: lower.has('bip01_head'),
      sample: names.slice(0, 12),
    });
  });
  meshes.sort((a, b) => (Number(b.hasPelvis) + Number(b.hasHead)) - (Number(a.hasPelvis) + Number(a.hasHead)) || b.bones - a.bones);
  return { count: meshes.length, meshes };
});
console.log(JSON.stringify(info, null, 2));
await browser.close();
