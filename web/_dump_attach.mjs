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
// let a few frames run
await page.waitForTimeout(500);
const info = await page.evaluate(() => {
  const root = window.__actor?.root;
  const att = window.__actor?.attachments;
  const bones = {};
  const pick = (meshName, boneName) => {
    let found = null;
    root.traverse((o) => {
      if (!o.isSkinnedMesh || o.name !== meshName) return;
      const b = o.skeleton.bones.find((x) => String(x.name).toLowerCase() === boneName);
      if (b) found = b;
    });
    if (!found) return null;
    const p = new THREE.Vector3();
    found.getWorldPosition(p);
    return { mesh: meshName, bone: found.name, world: [p.x, p.y, p.z], parent: found.parent?.name || null };
  };
  // THREE is module-scoped; use bone.matrixWorld instead
  const pick2 = (meshName, boneName) => {
    let found = null;
    root.traverse((o) => {
      if (!o.isSkinnedMesh || o.name !== meshName) return;
      const b = o.skeleton.bones.find((x) => String(x.name).toLowerCase() === boneName);
      if (b) found = b;
    });
    if (!found) return null;
    const e = found.matrixWorld.elements;
    return { mesh: meshName, bone: found.name, worldPos: [e[12], e[13], e[14]], parent: found.parent?.isBone ? found.parent.name : found.parent?.type };
  };
  return {
    attachments: {
      boneLinks: (att?.boneLinks || []).map((l) => ({
        mesh: l.meshName,
        src: l.sourceBone?.name,
        tgt: l.targetBone?.name,
      })),
      rootLinks: (att?.rootLinks || []).map((l) => ({
        mesh: l.meshName,
        root: l.rootBone?.name,
        anchor: l.anchorBone?.name,
      })),
    },
    heads: [
      pick2('f1_2227_body_hdmesh', 'bip01_head'),
      pick2('f1_1004_head_hdmesh', 'bip01_head'),
      pick2('f1_1004_bang_hdmesh', 'bip01_head'),
      pick2('f1_1004_plait_hdmesh', 'bip01_head'),
    ],
    arms: [
      pick2('f1_2227_body_hdmesh', 'bip01_l_upperarm'),
      pick2('f1_2227_hand_hdmesh', 'bip01_l_upperarm'),
      pick2('f1_2227_hand_hdmesh', 'bip01_l_hand'),
      pick2('f1_1004_lglove_hdmesh', 'bip01_l_hand'),
    ],
  };
});
console.log(JSON.stringify(info, null, 2));
await browser.close();
