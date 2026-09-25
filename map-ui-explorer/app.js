const $ = (q, root = document) => root.querySelector(q);
const $$ = (q, root = document) => [...root.querySelectorAll(q)];

const state = { maps: [], selected: 'longmen-day', mapInfo: null, view: 'overview', zoom: 1, showAreas: false };
const artifact = (relative) => `/artifact/${relative.split('/').map(encodeURIComponent).join('/')}`;

const ui = {
  'mapmark': { title: 'MapMark atlas', path: 'ui/Image/Minimap/MapMark_half.png', detail: 'MiddleMap marks, flags, banners, frames' },
  'minimap': { title: 'Minimap atlas', path: 'ui/Image/Minimap/Minimap_half.png', detail: 'Top-right lens icons and point sprites' },
  'minimap2': { title: 'Minimap2 atlas', path: 'ui/Image/Minimap/Minimap2_half.png', detail: 'Additional minimap UI elements' },
  'minimap3': { title: 'Minimap3 atlas', path: 'ui/Image/Minimap/Minimap3_half.png', detail: 'Mode/event icons; player marker frame 36' },
  'minimap4': { title: 'Minimap4 atlas', path: 'ui/Image/Minimap/Minimap4_half.png', detail: 'Panels, bars, compact icons' },
};

const views = [
  ['overview', 'Overview'], ['middlemap', 'M · MiddleMap'], ['minimap', 'Top-right · Minimap'],
  ['battlefield', 'Draggable · Battlefield'], ['assets', 'UI atlases'], ['components', 'Components'],
];

function statusTag(text, kind = 'ok') { return `<span class="tag ${kind}">${text}</span>`; }

async function getSelected() {
  const res = await fetch(`/api/map/${encodeURIComponent(state.selected)}`);
  if (!res.ok) throw new Error('map data unavailable');
  return res.json();
}

function drawMapList() {
  const root = $('#map-list');
  root.innerHTML = state.maps.map(m => `
    <button class="map-select ${m.id === state.selected ? 'active' : ''}" data-map="${m.id}">
      <span><span class="map-name">${m.display}</span><span class="map-resource">${m.resource} · ${m.variant}</span></span>
      <span class="map-badge" title="Tile count indexed in the game CDN">${m.tileCount.toLocaleString()} indexed</span>
    </button>`).join('');
  $$('.map-select', root).forEach(btn => btn.addEventListener('click', () => {
    state.selected = btn.dataset.map;
    state.zoom = 1;
    drawMapList();
    getSelected().then(map => { state.mapInfo = map; render(map); }).catch(showLoadError);
  }));
}

function drawTabs() {
  const nav = $('.tabs');
  nav.innerHTML = views.map(([id, label]) => `<button class="tab ${state.view === id ? 'active' : ''}" data-view="${id}">${label}</button>`).join('');
  $$('.tab', nav).forEach(btn => btn.addEventListener('click', () => {
    state.view = btn.dataset.view;
    drawTabs();
    render(state.mapInfo).catch(showLoadError);
  }));
}

function setHeading(map) {
  $('#map-kicker').textContent = `绝境地图 · ${map.variant}`;
  $('#map-title').textContent = map.display;
  $('#map-subtitle').textContent = `${map.resource} · internal map resource · ${map.tileCount.toLocaleString()} CDN tiles indexed`;
  $('#map-id').textContent = `Map ID ${map.ids}  ·  max ${map.players}`;
}

function stat(label, value, sub = '') {
  return `<div class="stat"><label>${label}</label><strong>${value}</strong>${sub ? `<em>${sub}</em>` : ''}</div>`;
}

function fileLink(path, label = path) {
  return `<a class="file-link" href="${artifact(path)}" target="_blank" rel="noreferrer">${label}</a>`;
}

function imageCard(title, path, caption, extra = '') {
  return `<div class="card image-card">
    <div class="image-toolbar"><strong>${title}</strong><span class="spacer"></span>${extra}</div>
    <div class="image-viewport"><img class="expandable-image" src="${artifact(path)}" alt="${title}" data-caption="${caption}"></div>
    <div class="image-caption">${caption}</div>
  </div>`;
}

function areasOverlay(map) {
  const cfg = map.config?.middlemap0;
  if (!state.showAreas || !cfg || !map.areas?.length) return '';
  const width = Number(cfg.width) || 1024;
  const height = Number(cfg.height) || 896;
  const scale = Number(cfg.scale) || 1;
  const startx = Number(cfg.startx) || 0;
  const starty = Number(cfg.starty) || 0;
  return map.areas.filter(a => a.name && a.id !== 0).map(a => {
    const left = (a.x - startx) * scale / width * 100;
    const top = (height - (a.y - starty) * scale) / height * 100;
    if (left < 0 || left > 100 || top < 0 || top > 100) return '';
    return `<button class="area-pin" style="left:${left}%;top:${top}%" title="${a.name} (${a.x}, ${a.y})"><span>${a.name}</span></button>`;
  }).join('');
}

function renderOverview(map) {
  const tileState = map.tileSheetAvailable
      ? statusTag(`Full-grid mosaic · ${map.tileFilesExtracted} loose samples`)
    : map.tileFilesExtracted
      ? statusTag(`${map.tileFilesExtracted} loose tile samples`, 'partial')
      : statusTag('Tiles indexed · no loose samples', 'partial');
  const bundleState = map.descriptorsAvailable
    ? statusTag('Map art + descriptors extracted')
    : statusTag('Map bundle partially extracted', 'partial');
  return `
    <div class="stats">
      ${stat('Internal resource', map.resource, `variant: ${map.variant}`)}
      ${stat('Map IDs', map.ids, `max players: ${map.players}`)}
       ${stat('CDN tiles indexed', map.tileCount.toLocaleString(), `${map.tileFilesExtracted} loose tile files in this workspace`)}
      ${stat('Area records', map.areaCount ?? '…', `NPC table: ${map.npcBytes ?? 0} bytes`)}
    </div>
    <div class="grid-3">
      <article class="card card-pad component-card"><div class="component-num">01 · M KEY</div><h3>MiddleMap</h3><p>Current-region map art, area labels, flags, quest marks and storm lines.</p><code>MiddleMap.ini + MiddleMap.lua</code><div style="margin-top:12px">${bundleState}</div></article>
      <article class="card card-pad component-card"><div class="component-num">02 · TOP RIGHT</div><h3>Minimap lens</h3><p>Native WndMinimap control. Reads per-map config and streams image tiles around the player.</p><code>MiniMap.ini + Minimap.lua</code><div style="margin-top:12px">${tileState}</div></article>
      <article class="card card-pad component-card"><div class="component-num">03 · DRAGGABLE</div><h3>BattleFieldMap</h3><p>Moveable 305×299 WndFrame with heat-map, player marker and storm/zone overlays.</p><code>BattleFieldMap.ini + BattleFieldMap.lua</code><div style="margin-top:12px">${statusTag('Static panel + real gameplay crop')}</div></article>
    </div>
    <div class="grid-2" style="margin-top:14px">
      <div class="card card-pad"><div class="card-title">Extracted data status ${bundleState}</div><div class="body-copy">${map.note || 'Map bundle data is present in proof/minimap/extracted.'}</div><div class="tag-row" style="margin-top:12px"><span class="tag">middlemap ${map.middlemapAvailable ? '✓' : '—'}</span><span class="tag">config ${Object.keys(map.config || {}).length ? '✓' : '—'}</span><span class="tag">area.tab ${map.areaBytes ? '✓' : '—'}</span><span class="tag">npc.tab ${map.npcBytes === 0 ? 'empty' : (map.npcBytes ? 'present' : '—')}</span></div></div>
      <div class="card card-pad"><div class="card-title">Component flow</div><div class="component-flow"><span>MapList.tab</span><i>→</i><span>UI config + Lua</span><i>→</i><span>KWndMinimap / MiddleMap</span><i>→</i><span>minimap bundle</span><i>→</i><span>mark / heat events</span></div><p class="tiny" style="margin:14px 0 0">All components are shared between maps; only each map's art/config/tile bundle varies.</p></div>
    </div>
    <div class="footer-note">Static assets and screenshots are served from proof/minimap/; no game process is running.</div>`;
}

function renderMiddleMap(map) {
  if (!map.middlemapUrl) return `<div class="missing-panel"><div><strong>MiddleMap art not materialized</strong><p>Descriptor entry exists in the CDN index. The app has no extracted middlemap image for this selection yet.</p></div></div>`;
  const areaToggle = `<button class="small-btn" id="toggle-areas">${state.showAreas ? 'Hide' : 'Show'} area coordinates</button>`;
  const imagePath = `extracted/data/source/maps/${map.folder}/middlemap.png`;
  const pins = areasOverlay(map);
  return `<div class="split-view">
    <div class="card image-card">
      <div class="image-toolbar"><strong>MiddleMap artwork</strong><span class="tag ok">EXTRACTED · ${map.resource}</span><span class="spacer"></span>${areaToggle}<button class="small-btn zoom-out">−</button><button class="small-btn zoom-in">+</button><button class="small-btn zoom-reset">Fit</button></div>
      <div class="image-viewport map-viewport"><div class="map-canvas" style="transform:scale(${state.zoom})"><img class="expandable-image" src="${artifact(imagePath)}" alt="${map.display} MiddleMap" data-caption="${map.resource} middlemap.png"><div class="area-overlay">${pins}</div></div></div>
      <div class="image-caption">${imagePath} · logical 1024×896, PNG 2048×1792 · click labels/pins for zone coordinates</div>
    </div>
    <aside class="card card-pad"><div class="card-title">MiddleMap transform</div>
      ${configRows(map.config?.middlemap0 || {})}
      <div class="note" style="margin-top:12px">M opens the current-region MiddleMap via <code>ToggleMiddleMap()</code>. The UI image is a separate authored map; area pins use the INI transform.</div>
      <div class="card-title" style="margin-top:18px">Area anchors <span class="tag">${map.areaCount || 0}</span></div>
      <div class="data-list">${map.areas?.length ? map.areas.filter(a=>a.name).map(a=>`<div class="data-row"><span>${a.name}</span><code>${a.x}, ${a.y}</code></div>`).join('') : '<div class="empty-note">No named area rows extracted for this map.</div>'}</div>
    </aside>
  </div>`;
}

function configRows(obj) {
  const keys = ['image','width','height','scale','startx','starty','copy','battlefield','offsetx','offsety'];
  const present = keys.filter(k => obj[k] !== undefined);
  return `<div class="data-list">${present.map(k=>`<div class="data-row"><span>${k}</span><code>${escapeHtml(obj[k])}</code></div>`).join('') || '<div class="empty-note">Config not extracted.</div>'}</div>`;
}

function renderMinimap(map) {
  const lens = imageCard('Real top-right lens crop', 'screenshots/05_minimap_ingame_crop.png', 'Existing in-game 龙门绝境 screenshot crop: circular lens, full-map button, storm ring and player marker.');
  const mosaic = map.tileSheetAvailable
    ? imageCard('Assembled minimap tile mosaic', map.tileSheet, 'Verified full-grid mosaic assembled from the 1,296-tile CDN set; loose source files are not all retained. Preview image is 2304×2304.')
    : `<div class="card"><div class="card-pad"><div class="card-title">Tile art status ${statusTag('Indexed; mosaic not assembled', 'partial')}</div><div class="empty-note">${map.tileCount.toLocaleString()} tiles are listed in the local CDN resource index for ${map.resource}; ${map.tileFilesExtracted} loose tile sample files are materialized here. The actual MiddleMap artwork is shown below.</div></div><div class="image-viewport" style="min-height:270px"><img class="expandable-image" src="${map.middlemapUrl || ''}" alt="${map.display} map art" data-caption="${map.resource} map art"></div></div>`;
  return `<div class="grid-2">${lens}${mosaic}</div>
    <div class="card card-pad" style="margin-top:14px"><div class="card-title">Native tile control</div><div class="component-flow"><span>MiniMap.ini · WndMinimap</span><i>→</i><span>Minimap.lua</span><i>→</i><span>KGUIX64.dll</span><i>→</i><span>config.ini + 0_Z_X tiles</span></div><p class="tiny" style="margin:12px 0 0">The native control reads [config] scale/width/offsetx/offsety, computes <code>map=scale×position+offset</code>, then selects <code>floor(map/width)</code>. Zoom is 0.5–2.0 (default 2).</p></div>`;
}

function renderBattlefield(map) {
  const crop = imageCard('Actual draggable BattlefieldMap panel', 'screenshots/06_battlefieldmap_ingame_crop.png', 'In-game crop from 龙门绝境: green battlefield map, player marker, waypoint label.');
  const rel = 'ui/Config/Default/BattleField/BattleFieldMap.ini';
  const iniUrl = artifact(rel);
  return `<div class="split-view">
      ${crop}
      <aside class="card card-pad"><div class="card-title">BattleFieldMap window ${statusTag('Moveable=1')}</div><div class="data-list">
        <div class="data-row"><span>Layout</span><code>UI/Config/Default/BattleField/BattleFieldMap.ini</code></div>
        <div class="data-row"><span>Driver</span><code>BattleFieldMap.lua</code></div>
        <div class="data-row"><span>Type / parent</span><code>WndFrame / Normal</code></div>
        <div class="data-row"><span>Drag area</span><code>305×40 px</code></div>
        <div class="data-row"><span>Shown in modes</span><code>1, 4, 5, 17</code></div>
        <div class="data-row"><span>State</span><code>Anchor, bTurnOn, bExpand, fAlpha, bShowHeatMap</code></div>
        <div class="data-row"><span>Panel art</span><code>BattleMinimap*.UITex + storm lines</code></div>
      </div><p class="tiny" style="margin-top:13px">The panel is base UI, not a JX add-on. Heat-map values arrive at runtime; Lua calls GetMapHeatInfo(mapID).</p><a class="file-link" href="${iniUrl}" target="_blank" rel="noreferrer">Open extracted INI →</a></aside>
    </div>`;
}

function renderAssets() {
  const assets = Object.values(ui);
  const cards = assets.map(a => `<article class="card asset-card" data-full="${a.path.replace('_half.png','.png')}" data-caption="${a.title}"><div class="asset-preview"><img src="${artifact(a.path)}" alt="${a.title}"></div><div class="asset-meta"><b>${a.title}</b><small>${a.detail}</small></div></article>`).join('');
  return `<div class="card card-pad" style="margin-bottom:14px"><div class="card-title">Extracted UI atlas previews <span class="tag ok">PakV4 · original game assets</span></div><p class="tiny">Click a sheet to enlarge. Frame coordinates are in the adjacent .UITex descriptor.</p><div class="asset-grid">${cards}</div></div>
    <div class="grid-2"><div class="card card-pad"><div class="card-title">Battlefield-map textures</div><p class="tiny">The game textures are stored as TGA/DDS; their .UITex files hold atlas frame rectangles.</p><div class="data-list">
      <div class="data-row"><span>BattleMinimap</span><a class="file-link" href="${artifact('ui/Image/Minimap/BattleMinimap.DDS')}" download>Download DDS</a></div>
      <div class="data-row"><span>BattleMinimap2</span><a class="file-link" href="${artifact('ui/UI/Image/Minimap/BattleMinimap2.TGA')}" download>Download TGA</a></div>
      <div class="data-row"><span>BattleMinimap3 · player marker frame 36</span><a class="file-link" href="${artifact('ui/UI/Image/Minimap/BattleMinimap3.TGA')}" download>Download TGA</a></div>
      <div class="data-row"><span>Frame descriptor</span><a class="file-link" href="${artifact('ui/UI/Image/Minimap/BattleMinimap3.UITex')}" download>Download UITex</a></div>
    </div></div><div class="card card-pad"><div class="card-title">Other extracted map UI assets</div><div class="tag-row"><span class="tag">MapWindow*.UITex</span><span class="tag">LinkLine.tga</span><span class="tag">41 storm-line DDS segments</span><span class="tag">NewWorldMap layers</span><span class="tag">Minimap*.UITex</span></div><p class="tiny" style="margin-top:12px">Additional original assets are stored under <code>proof/minimap/ui/</code>.</p></div></div>`;
}

function renderComponents(map) {
  return `<div class="grid-2">
    <article class="card card-pad"><div class="card-title">UI control → data</div><div class="component-flow"><span>UI config / Lua</span><i>→</i><span>WndMinimap</span><i>→</i><span>KGUIX64.dll</span><i>→</i><span>tile grid</span></div><p class="body-copy" style="margin-top:14px">MiniMap.lua obtains scene/player state and mark events. The native KWndMinimap control reads each map's config.ini, selects layer_Z_X tiles and draws its point/arrow overlays.</p></article>
    <article class="card card-pad"><div class="card-title">World representation</div><div class="component-flow"><span>MapList.tab</span><i>→</i><span>KMapListFile</span><i>→</i><span>Represent scene</span><i>→</i><span>UI map windows</span></div><p class="body-copy" style="margin-top:14px">KGameWorldHandler::GetMinimapLayer and scene coordinate conversion live in JX3RepresentX64.dll. KMapListFile reads the map catalog and sub-map table.</p></article>
    <article class="card card-pad"><div class="card-title">Dynamic overlays</div><div class="data-list"><div class="data-row"><span>NPC / doodad marks</span><code>KNpc/KDoodad::UpdateMiniMapMark</code></div><div class="data-row"><span>Mid-map sync</span><code>OnSyncMidMapMark / OnSyncMapMarkInfo</code></div><div class="data-row"><span>Battle heat map</span><code>KScene::UnPackHeatMapBinaryData</code></div><div class="data-row"><span>Radar</span><code>SetMinimapRadar / MINI_RADAR_TYPE</code></div></div></article>
    <article class="card card-pad"><div class="card-title">Extracted analysis</div><div class="data-list">${[
      ['Minimap layout', 'ui/Config/Default/MiniMap.ini'], ['Minimap driver', 'ui/Config/Default/decompiled/Minimap.decompiled.lua'],
      ['BattleFieldMap layout', 'ui/Config/Default/BattleField/BattleFieldMap.ini'], ['BattleFieldMap driver', 'ui/Config/Default/BattleField/BattleFieldMap.decompiled.lua'],
      ['KWndMinimap tile loader', 'recon/kgui_tile_loader.txt'], ['Heat-map unpack', 'recon/logic_unpack_heatmap.txt'],
    ].map(([label,path])=>`<div class="data-row"><span>${label}</span><a class="file-link" href="${artifact(path)}" target="_blank" rel="noreferrer">view</a></div>`).join('')}</div></article>
  </div>
  <div class="note" style="margin-top:14px">Selected map: ${map.display} (${map.resource}). The static components and formats are understood; dynamic mark/heat values are received at runtime.</div>`;
}

async function render(map = null) {
  if (!map) map = state.mapInfo || await getSelected();
  state.mapInfo = map;
  setHeading(map);
  const root = $('#view-root');
  if (state.view === 'overview') root.innerHTML = renderOverview(map);
  else if (state.view === 'middlemap') root.innerHTML = renderMiddleMap(map);
  else if (state.view === 'minimap') root.innerHTML = renderMinimap(map);
  else if (state.view === 'battlefield') root.innerHTML = renderBattlefield(map);
  else if (state.view === 'assets') root.innerHTML = renderAssets();
  else root.innerHTML = renderComponents(map);
  wireControls();
}

function showLoadError(err) {
  $('#view-root').innerHTML = `<div class="missing-panel"><div><strong>Could not render this view</strong><p>${escapeHtml(err.message)}</p></div></div>`;
}

function wireControls() {
  const toggle = $('#toggle-areas');
  if (toggle) toggle.addEventListener('click', () => { state.showAreas = !state.showAreas; render(); });
  $$('.zoom-in').forEach(b => b.addEventListener('click', () => { state.zoom = Math.min(2.5, state.zoom + 0.15); render(); }));
  $$('.zoom-out').forEach(b => b.addEventListener('click', () => { state.zoom = Math.max(0.65, state.zoom - 0.15); render(); }));
  $$('.zoom-reset').forEach(b => b.addEventListener('click', () => { state.zoom = 1; render(); }));
  $$('.expandable-image').forEach(img => img.addEventListener('click', () => openLightbox(img.src, img.dataset.caption || img.alt)));
  $$('.asset-card').forEach(card => card.addEventListener('click', () => openLightbox(artifact(card.dataset.full), card.dataset.caption)));
}

function openLightbox(src, caption) {
  $('#lightbox-image').src = src;
  $('#lightbox-caption').textContent = caption;
  $('#lightbox').classList.remove('hidden');
}

function closeLightbox() { $('#lightbox').classList.add('hidden'); }
function escapeHtml(value) { return String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); }

$('#lightbox-close').addEventListener('click', closeLightbox);
$('#lightbox').addEventListener('click', e => { if (e.target.id === 'lightbox') closeLightbox(); });
document.addEventListener('keydown', e => { if (e.key === 'Escape') closeLightbox(); });

drawTabs();
fetch('/api/maps').then(r => r.json()).then(async maps => {
  state.maps = maps;
  drawMapList();
  state.mapInfo = await getSelected();
  render(state.mapInfo);
}).catch(err => {
  $('#view-root').innerHTML = `<div class="missing-panel"><div><strong>Could not load map evidence</strong><p>${escapeHtml(err.message)}. Start the app with <code>node server.mjs</code> from <code>map-ui-explorer/</code>.</p></div></div>`;
});
