import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage();
await page.goto(process.argv[3], { waitUntil:'domcontentloaded', timeout:60000 });
for (let i=0;i<60;i++){
  const ready = await page.evaluate(() => !!window.__FBX_READY__);
  if (ready) break;
  await page.waitForTimeout(200);
}
const dump = await page.evaluate(() => {
  const out = [];
  const root = window.__FBX_ROOT__ || window.__FBX_PLACEMENT__;
  if (!root) return { err: 'no root', keys: Object.keys(window).filter(k=>/FBX|fbx/.test(k)) };
  root.traverse((o) => {
    if (!o.isMesh && !o.isSkinnedMesh) return;
    const mats = Array.isArray(o.material) ? o.material : [o.material];
    for (const m of mats) {
      if (!m) continue;
      const src = m.map?.image?.currentSrc || m.map?.image?.src || m.map?.source?.data?.src || m.map?.name || null;
      out.push({
        mesh: o.name,
        mat: m.name,
        type: m.type,
        mapSrc: src,
        color: m.color ? m.color.getHexString() : null,
        metalness: m.metalness,
        roughness: m.roughness,
        hasNormal: !!m.normalMap,
        hasEmissiveMap: !!m.emissiveMap,
        emissive: m.emissive ? m.emissive.getHexString() : null,
      });
    }
  });
  return out;
});
console.log(JSON.stringify(dump, null, 2));
await browser.close();
