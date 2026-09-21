import { chromium } from "playwright-core";
const url = process.argv[2];
const chrome = process.argv[3];
const browser = await chromium.launch({ executablePath: chrome, headless: true, args: ["--no-sandbox","--disable-gpu","--use-gl=angle","--use-angle=swiftshader"] });
const page = await browser.newPage();
await page.goto(url, { waitUntil: "domcontentloaded", timeout: 120000 });
for (let i=0;i<300;i++){ if (await page.evaluate(()=>!!(window.__FBX_READY__&&window.__actor?.bonesByName))) break; await page.waitForTimeout(200); }
const rows = await page.evaluate(() => {
  const map = window.__actor.bonesByName;
  const seen = new Set();
  const out = [];
  for (const b of map.values()) {
    if (seen.has(b.uuid)) continue; seen.add(b.uuid);
    let p = b.parent;
    while (p && !(p.isBone || p.type === "Bone")) p = p.parent;
    out.push({
      name: b.name,
      uuid: b.uuid.slice(0,8),
      parentName: p ? p.name : null,
      parentUuid: p ? p.uuid.slice(0,8) : null,
      sameUuid: !!(p && p.uuid === b.uuid),
      sameName: !!(p && p.name === b.name),
      pos: [+b.position.x.toFixed(3), +b.position.y.toFixed(3), +b.position.z.toFixed(3)],
    });
  }
  return out;
});
const weird = rows.filter(r => r.sameName || r.sameUuid).slice(0,10);
console.log("total", rows.length, "sameName", rows.filter(r=>r.sameName).length, "sameUuid", rows.filter(r=>r.sameUuid).length);
console.log("sample weird", JSON.stringify(weird,null,2));
console.log("sample pelvis", rows.filter(r=>/pelvis|spine1|bip01$/i.test(r.name)).slice(0,8));
await browser.close();
