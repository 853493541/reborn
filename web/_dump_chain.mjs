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
await page.waitForTimeout(300);
const info = await page.evaluate(() => {
  const root = window.__actor?.root;
  const fmt = (b) => {
    if (!b) return null;
    const e = b.matrixWorld.elements;
    const finite = e.every((x) => Number.isFinite(x));
    return {
      name: b.name,
      parent: b.parent?.name || b.parent?.type,
      pos: b.position.toArray(),
      scale: b.scale.toArray(),
      worldPos: [e[12], e[13], e[14]],
      finite,
      worldScale: (() => {
        const sx = Math.hypot(e[0], e[1], e[2]);
        const sy = Math.hypot(e[4], e[5], e[6]);
        const sz = Math.hypot(e[8], e[9], e[10]);
        return [sx, sy, sz];
      })(),
    };
  };
  const find = (meshName, boneName) => {
    let found = null;
    root.traverse((o) => {
      if (o.isSkinnedMesh && o.name === meshName) {
        const b = o.skeleton.bones.find((x) => x.name.toLowerCase() === boneName);
        if (b) found = b;
      }
    });
    return found;
  };
  // Also find first bone by name in scene (PropertyBinding style)
  const findAny = (boneName) => {
    let found = null;
    root.traverse((o) => {
      if (found) return;
      if (o.isBone && o.name.toLowerCase() === boneName) found = o;
    });
    return found;
  };
  const hand = find('f1_2227_hand_hdmesh', 'bip01_l_hand');
  const chain = [];
  let cur = hand;
  for (let i = 0; i < 12 && cur; i++) {
    chain.push(fmt(cur));
    cur = cur.parent?.isBone ? cur.parent : null;
  }
  return {
    bodyHead: fmt(find('f1_2227_body_hdmesh', 'bip01_head')),
    headHead: fmt(find('f1_1004_head_hdmesh', 'bip01_head')),
    bodyUpper: fmt(find('f1_2227_body_hdmesh', 'bip01_l_upperarm')),
    handUpper: fmt(find('f1_2227_hand_hdmesh', 'bip01_l_upperarm')),
    handHand: fmt(hand),
    handChain: chain,
    anyHead: fmt(findAny('bip01_head')),
    placementScale: (() => {
      const a = window.__actor?.placement;
      return a ? { pos: a.position?.toArray?.(), scale: a.scale?.toArray?.() } : null;
    })(),
  };
});
console.log(JSON.stringify(info, null, 2));
await browser.close();
