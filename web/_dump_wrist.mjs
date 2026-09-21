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
await page.waitForTimeout(400);
const info = await page.evaluate(() => {
  const root = window.__actor?.root;
  let hand, body, lglove;
  root.traverse((o) => {
    if (!o.isSkinnedMesh) return;
    if (/hand_hdmesh/i.test(o.name) && !/glove/i.test(o.name)) hand = o;
    if (/body_hdmesh/i.test(o.name)) body = o;
    if (/lglove/i.test(o.name)) lglove = o;
  });
  const hb = (n) => hand.skeleton.bones.find((b) => b.name.toLowerCase() === n);
  const bb = (n) => body.skeleton.bones.find((b) => b.name.toLowerCase() === n || b.name.toLowerCase() === '__body__' + n);
  const handUpper = hb('bip01_l_upperarm');
  const bodyClav = bb('bip01_l_clavicle');
  const bodyUpper = bb('bip01_l_upperarm');
  const handHand = hb('bip01_l_hand');
  const e = (b) => b ? [b.matrixWorld.elements[12], b.matrixWorld.elements[13], b.matrixWorld.elements[14]] : null;
  // list body bones renamed
  const renamed = body.skeleton.bones.filter((b) => b.name.startsWith('__body__')).map((b) => b.name);
  // is hand upperarm.parent the same object as some body bone?
  const parent = handUpper?.parent;
  let parentOwner = null;
  root.traverse((o) => {
    if (!o.isSkinnedMesh || !o.skeleton) return;
    if (o.skeleton.bones.includes(parent)) parentOwner = o.name;
  });
  return {
    armBind: window.__actor?.armBind,
    renamed,
    handUpperParent: parent?.name,
    parentOwner,
    parentIsBodyClav: parent === bodyClav,
    bodyClavName: bodyClav?.name,
    bodyUpperName: bodyUpper?.name,
    pos: {
      handUpper: e(handUpper),
      handHand: e(handHand),
      bodyClav: e(bodyClav),
      bodyUpper: e(bodyUpper),
      sleeveEndGuess: e(bodyUpper),
    },
    attachments: {
      boneLinks: (window.__actor?.attachments?.boneLinks || []).length,
      sample: (window.__actor?.attachments?.boneLinks || []).slice(0, 8).map((l) => ({
        mesh: l.meshName, src: l.sourceBone?.name, tgt: l.targetBone?.name,
      })),
      armSync: (window.__actor?.attachments?.boneLinks || [])
        .filter((l) => String(l.targetBone?.name || '').startsWith('__body__'))
        .map((l) => ({ src: l.sourceBone?.name, tgt: l.targetBone?.name })),
    },
  };
});
console.log(JSON.stringify(info, null, 2));
await browser.close();
