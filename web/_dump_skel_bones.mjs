import { chromium } from "playwright-core";
import { writeFileSync } from "fs";
const url = process.argv[2];
const chrome = process.argv[3];
const out = process.argv[4];
const browser = await chromium.launch({ executablePath: chrome, headless: true, args: ["--no-sandbox","--disable-gpu","--use-gl=angle","--use-angle=swiftshader"] });
const page = await browser.newPage();
await page.goto(url, { waitUntil: "domcontentloaded", timeout: 120000 });
for (let i=0;i<300;i++){ if (await page.evaluate(()=>!!(window.__FBX_READY__&&window.__actor?.root))) break; await page.waitForTimeout(200); }
const result = await page.evaluate(() => {
  const root = window.__actor.root;
  const meshes = [];
  root.traverse((o) => {
    if (o.isSkinnedMesh && o.skeleton?.bones?.length) {
      meshes.push({ name: o.name, n: o.skeleton.bones.length });
    }
  });
  meshes.sort((a,b)=>b.n-a.n);
  let skel = null;
  root.traverse((o) => {
    if (!skel && o.isSkinnedMesh && o.skeleton?.bones?.length === meshes[0]?.n) skel = o.skeleton;
  });
  if (!skel) return { error: "no skeleton", meshes, boneCount: 0, bones: [] };
  const bones = skel.bones.map((b) => {
    let p = b.parent;
    while (p && !(p.isBone || p.type === "Bone")) p = p.parent;
    if (p && p.uuid === b.uuid) p = null;
    return {
      name: b.name,
      uuid: b.uuid,
      parentUuid: p ? p.uuid : null,
      parentName: p ? p.name : null,
      pos: [b.position.x, b.position.y, b.position.z],
      quat: [b.quaternion.x, b.quaternion.y, b.quaternion.z, b.quaternion.w],
      scale: [b.scale.x, b.scale.y, b.scale.z],
    };
  });
  return { boneCount: bones.length, bones, meshes };
});
writeFileSync(out, JSON.stringify(result));
console.log("META_JSON=" + JSON.stringify({ boneCount: result.boneCount, meshes: result.meshes?.slice(0,8) }));
await browser.close();
