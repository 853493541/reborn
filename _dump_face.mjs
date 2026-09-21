import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage({ viewport:{width:1100,height:900} });
page.on('console', m => { if (/error|face|Diffuse|FBX/i.test(m.text())) console.log('CONSOLE', m.text().slice(0,200)); });
await page.goto('http://127.0.0.1:8765/index.html', { waitUntil:'networkidle', timeout:90000 });
await page.waitForFunction(() => window.__FBX_READY__ === true || window.__FBX_ERROR__, null, { timeout:120000 }).catch(()=>'timeout');
const err = await page.evaluate(() => window.__FBX_ERROR__ || null);
console.log('ERR', err);
const face = await page.evaluate(() => {
  const root = window.__FBX_ROOT__;
  if (!root) return { noRoot: true, ready: window.__FBX_READY__, keys: Object.keys(window).filter(k=>/FBX|THREE|SCENE/i.test(k)) };
  const mats = [];
  const meshes = [];
  root.traverse((o) => {
    if (!o.isMesh && !o.isSkinnedMesh) return;
    const list = Array.isArray(o.material) ? o.material : [o.material];
    const isFace = list.some(m => /face/i.test(m?.name||'')) || /face/i.test(o.name||'');
    if (!isFace) return;
    const geo = o.geometry;
    let avgN = null;
    if (geo?.attributes?.normal) {
      const n = geo.attributes.normal.array;
      let x=0,y=0,z=0,c=0;
      for (let i=0;i<Math.min(n.length,3000);i+=3){ x+=n[i]; y+=n[i+1]; z+=n[i+2]; c++; }
      avgN = {x:+(x/c).toFixed(3), y:+(y/c).toFixed(3), z:+(z/c).toFixed(3), samples:c};
    }
    meshes.push({
      name: o.name, visible: o.visible, renderOrder: o.renderOrder,
      groups: (geo?.groups||[]).map(g=>({start:g.start,count:g.count,mat:g.materialIndex})),
      verts: geo?.attributes?.position?.count || 0,
      hasVertexColors: !!geo?.attributes?.color,
      avgNormal: avgN,
      matNames: list.map(m => m?.name),
    });
    for (const m of list) {
      if (!m) continue;
      let mapUrl = null;
      try {
        const img = m.map?.image;
        mapUrl = m.map?.source?.data?.src || img?.src || img?.currentSrc || m.map?.name || null;
        if (!mapUrl && m.map?.source?.data?.data) mapUrl = '(embedded-buffer)';
      } catch {}
      mats.push({
        name: m.name, type: m.type,
        color: m.color && [m.color.r,m.color.g,m.color.b].map(v=>+v.toFixed(3)),
        transparent: m.transparent, opacity: m.opacity, alphaTest: m.alphaTest,
        depthWrite: m.depthWrite, side: m.side,
        metalness: m.metalness, roughness: m.roughness,
        emissive: m.emissive && [m.emissive.r,m.emissive.g,m.emissive.b].map(v=>+v.toFixed(3)),
        mapUrl, hasNormal: !!m.normalMap, hasSpecular: !!m.specularMap,
        shininess: m.shininess,
      });
    }
  });
  return { meshes, mats, status: document.querySelector('#status')?.textContent || document.body.innerText.slice(0,200) };
});
console.log(JSON.stringify(face, null, 2));
await page.screenshot({ path: 'proof/compare/aniplayer_face_runtime.png', type:'png' });
await browser.close();
