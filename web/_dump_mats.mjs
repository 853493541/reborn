import { chromium } from 'playwright-core';
const url = process.argv[2];
const chrome = process.argv[3];
const browser = await chromium.launch({ executablePath: chrome, headless: true, args: ['--no-sandbox'] });
const page = await browser.newPage();
await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 120000 });
for (let i = 0; i < 300; i++) {
  if (await page.evaluate(() => !!window.__FBX_READY__)) break;
  await page.waitForTimeout(200);
}
const info = await page.evaluate(() => {
  const root = window.__actor?.root;
  const out = [];
  root.traverse((o) => {
    if (!o.isSkinnedMesh) return;
    if (!/body|hand|glove|sleeve|arm/i.test(o.name)) return;
    const mats = (Array.isArray(o.material) ? o.material : [o.material]).map((m) => ({
      name: m?.name,
      type: m?.type,
      map: !!(m?.map),
      mapName: m?.map?.name || m?.map?.image?.src?.slice?.(-40),
      transparent: m?.transparent,
      opacity: m?.opacity,
      alphaTest: m?.alphaTest,
      side: m?.side,
      color: m?.color ? [m.color.r, m.color.g, m.color.b] : null,
      visible: o.visible,
    }));
    out.push({ mesh: o.name, visible: o.visible, mats });
  });
  return out;
});
console.log(JSON.stringify(info, null, 2));
await browser.close();
