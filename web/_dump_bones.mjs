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
  const want = ['bip01_head','bip01_neck','bip01_l_hand','bip01_r_hand','bip01_l_forearm','bip01_r_forearm','bip01_l_upperarm','bip01_pelvis','bip01_l_finger0'];
  const meshes = [];
  root.traverse((o) => {
    if (!o.isSkinnedMesh || !o.skeleton?.bones?.length) return;
    const lower = new Map(o.skeleton.bones.map(b => [String(b.name||'').toLowerCase(), b.name]));
    const hit = {};
    for (const w of want) hit[w] = lower.has(w);
    meshes.push({ name: o.name, bones: o.skeleton.bones.length, hit, allBip: o.skeleton.bones.map(b=>b.name).filter(n=>/^bip01/i.test(n)) });
  });
  return meshes;
});
console.log(JSON.stringify(info, null, 2));
await browser.close();
