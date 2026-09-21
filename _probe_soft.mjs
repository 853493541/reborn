import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage({ viewport:{width:1280,height:900} });
page.on('pageerror', e => console.log('PAGEERROR', e.message));
await page.goto(process.argv[3], { waitUntil:'domcontentloaded', timeout:60000 });
for (let i=0;i<80;i++){
  if (await page.evaluate(() => !!window.__FBX_READY__)) break;
  await page.waitForTimeout(200);
}
const info = await page.evaluate(() => {
  const root = window.__FBX_PLACEMENT__ || window.__FBX_ROOT__;
  let soft=0, hard=0, opaque=0, hidden=0, withN=0;
  const face=[];
  root?.traverse((o)=>{
    if (!o.visible) { if (o.isMesh||o.isSkinnedMesh) hidden++; return; }
    if (!o.isMesh && !o.isSkinnedMesh) return;
    for (const m of (Array.isArray(o.material)?o.material:[o.material])) {
      if (!m) continue;
      if (m.normalMap) withN++;
      if (!m.transparent && !(m.alphaTest>0)) opaque++;
      else if (m.alphaTest>0) hard++;
      else soft++;
      if (/face/i.test(m.name||'')) face.push({mat:m.name,t:m.transparent,a:m.alphaTest,dw:m.depthWrite,side:m.side,nrm:!!m.normalMap});
    }
  });
  return { hud: document.getElementById('hud')?.textContent, opaque, soft, hard, hidden, withN, face };
});
console.log(JSON.stringify(info,null,2));

// Full body shot
await page.waitForTimeout(400);
await page.screenshot({ path:'proof/compare/aniplayer_f1_hualuo_closeup.png', type:'png' });

// True face closeup via camera
await page.evaluate(() => {
  const THREE = window.THREE || null;
  // THREE may be module-scoped; use placement bbox upper body
  const placement = window.__FBX_PLACEMENT__;
  const camera = window.__FBX_CAMERA__;
  if (!placement || !camera) return 'missing';
  // compute bbox
  let min=[Infinity,Infinity,Infinity], max=[-Infinity,-Infinity,-Infinity];
  placement.updateMatrixWorld(true);
  placement.traverse((o)=>{
    if (!o.isMesh && !o.isSkinnedMesh) return;
    const g=o.geometry; if (!g) return;
    if (!g.boundingBox) g.computeBoundingBox();
    const b=g.boundingBox; if (!b) return;
    const corners=[[b.min.x,b.min.y,b.min.z],[b.max.x,b.max.y,b.max.z]];
    // world approx via object world matrix
    o.updateWorldMatrix(true,false);
    const e=o.matrixWorld.elements;
    for (const [x0,y0,z0] of [[b.min.x,b.min.y,b.min.z],[b.max.x,b.max.y,b.max.z],[b.min.x,b.max.y,b.min.z],[b.max.x,b.min.y,b.max.z]]) {
      const x=e[0]*x0+e[4]*y0+e[8]*z0+e[12];
      const y=e[1]*x0+e[5]*y0+e[9]*z0+e[13];
      const z=e[2]*x0+e[6]*y0+e[10]*z0+e[14];
      if (x<min[0])min[0]=x; if(y<min[1])min[1]=y; if(z<min[2])min[2]=z;
      if (x>max[0])max[0]=x; if(y>max[1])max[1]=y; if(z>max[2])max[2]=z;
    }
  });
  const cx=(min[0]+max[0])/2, cy=min[1]+(max[1]-min[1])*0.82, cz=(min[2]+max[2])/2;
  const h=max[1]-min[1];
  camera.position.set(cx, cy, cz + h*0.35);
  camera.near=0.1; camera.far=5000; camera.updateProjectionMatrix();
  camera.lookAt(cx, cy, cz);
  return {cx,cy,cz,h};
});
await page.waitForTimeout(200);
await page.screenshot({ path:'proof/compare/aniplayer_f1_hualuo_face.png', type:'png' });
await browser.close();
