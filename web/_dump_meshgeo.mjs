import { chromium } from 'playwright-core';
const url = process.argv[2];
const chrome = process.argv[3];
const browser = await chromium.launch({ executablePath: chrome, headless: true, args: ['--no-sandbox'] });
const page = await browser.newPage();
page.on('pageerror', (e) => console.log('PAGEERROR', e.message));
await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 120000 });
for (let i = 0; i < 300; i++) {
  if (await page.evaluate(() => !!window.__FBX_READY__)) break;
  await page.waitForTimeout(200);
}
const info = await page.evaluate(() => {
  const root = window.__actor?.root;
  const meshes = [];
  root.traverse((o) => {
    if (!o.isMesh) return;
    let tris = 0;
    const g = o.geometry;
    if (g?.index) tris = g.index.count / 3;
    else if (g?.attributes?.position) tris = g.attributes.position.count / 3;
    meshes.push({
      name: o.name,
      skinned: !!o.isSkinnedMesh,
      visible: o.visible,
      tris: Math.round(tris),
      bones: o.isSkinnedMesh ? (o.skeleton?.bones?.length || 0) : 0,
    });
  });
  meshes.sort((a, b) => b.tris - a.tris);
  return { partVisibility: window.__actor?.partVisibility, meshes, ready: window.__FBX_READY__ };
});
console.log(JSON.stringify(info, null, 2));
await browser.close();
