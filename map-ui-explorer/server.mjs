import http from 'node:http';
import { readFile, readdir, stat } from 'node:fs/promises';
import { extname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const APP_ROOT = fileURLToPath(new URL('.', import.meta.url));
const REPO_ROOT = resolve(APP_ROOT, '..');
const PROOF_ROOT = resolve(REPO_ROOT, 'proof', 'minimap');
const PORT = Number(process.env.PORT || 3037);

const maps = [
  {
    id: 'longmen-day', display: '龙门绝境', resource: '龙门寻宝', variant: '日间',
    folder: '龙门寻宝minimap_mb', ids: '296 / 676 / 677', players: '118 / 108',
    tileCount: 1296, tileSheet: 'screenshots/08_minimap_mosaic_correct_half.png',
    note: '龙门绝境基础图；日间资源。',
  },
  {
    id: 'longmen-night', display: '龙门绝境·夜', resource: '龙门寻宝_夜晚', variant: '夜晚',
    folder: '龙门寻宝_夜晚minimap_mb', ids: '297', players: '118',
    tileCount: 1296, tileSheet: null,
    note: '夜图有独立 middlemap、样本 tiles 与 loadinglmjj2。',
  },
  {
    id: 'cangming', display: '沧溟绝境', resource: '海岛绝境', variant: '标准',
    folder: '海岛绝境minimap_mb', ids: '410', players: '118',
    tileCount: 1296, tileSheet: null,
    note: '目录 config.ini 的 name 字段沿用“龙门寻宝”；核对海岛图对位时需注意。',
  },
  {
    id: 'bailong', display: '白龙绝境', resource: '白龙绝境', variant: '标准',
    folder: '白龙绝境minimap_mb', ids: '512', players: '118',
    tileCount: 1296, tileSheet: null,
    note: '完整 middlemap/config/area/npc 描述已提取。',
  },
  {
    id: 'tianyuan', display: '天原绝境', resource: '天原绝境', variant: '标准',
    folder: '天原绝境minimap_mb', ids: '532', players: '118',
    tileCount: 1296, tileSheet: null,
    note: '完整 middlemap/config/area/npc 描述已提取。',
  },
  {
    id: 'erhai', display: '洱海绝境', resource: '洱海绝境', variant: '标准',
    folder: '洱海绝境minimap_mb', ids: '645', players: '108',
    tileCount: 1296, tileSheet: null,
    note: '另有 doodad/trafficline/trafficnode 表。',
  },
  {
    id: 'linhai', display: '林海绝境', resource: '林海绝境', variant: '奇境寻宝',
    folder: '林海绝境minimap_mb', ids: '709 / 715', players: '118',
    tileCount: 2704, tileSheet: null,
    note: '52×52 tiles；config uses width=256, scale=0.01, offsets=-1024.',
  },
];

function send(res, status, body, type = 'text/plain; charset=utf-8') {
  res.writeHead(status, {
    'Content-Type': type,
    'Cache-Control': 'no-cache',
    'X-Content-Type-Options': 'nosniff',
  });
  res.end(body);
}

function mime(file) {
  const ext = extname(file).toLowerCase();
  return ({
    '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
    '.json': 'application/json; charset=utf-8', '.txt': 'text/plain; charset=utf-8',
    '.ini': 'text/plain; charset=utf-8', '.lua': 'text/plain; charset=utf-8',
    '.tab': 'text/plain; charset=utf-8', '.png': 'image/png', '.jpg': 'image/jpeg',
    '.jpeg': 'image/jpeg', '.gif': 'image/gif', '.svg': 'image/svg+xml',
    '.tga': 'application/octet-stream', '.dds': 'application/octet-stream',
  })[ext] || 'application/octet-stream';
}

function parseIni(text) {
  const out = {};
  let section = '';
  for (const raw of text.replace(/^\uFEFF/, '').split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith(';') || line.startsWith('#')) continue;
    const match = line.match(/^\[([^\]]+)\]$/);
    if (match) {
      section = match[1];
      out[section] ||= {};
      continue;
    }
    const eq = line.indexOf('=');
    if (eq > 0 && section) out[section][line.slice(0, eq).trim()] = line.slice(eq + 1).trim();
  }
  return out;
}

function parseTab(text) {
  const rows = text.replace(/^\uFEFF/, '').split(/\r?\n/).filter(Boolean).map(line => line.split('\t'));
  if (!rows.length) return [];
  const heads = rows[0].map(s => s.trim());
  return rows.slice(1).map(parts => Object.fromEntries(heads.map((h, i) => [h, (parts[i] || '').trim()])))
    .filter(row => row.name);
}

async function readGb(path) {
  const buf = await readFile(path);
  return new TextDecoder('gb18030').decode(buf);
}

const textExtensions = new Set(['.txt', '.ini', '.lua', '.tab']);

async function mapDetails(map) {
  const base = join(PROOF_ROOT, 'extracted', 'data', 'source', 'maps', map.folder);
  const configPath = join(base, 'config.ini');
  const areaPath = join(base, 'area.tab');
  const npcPath = join(base, 'npc.tab');
  const middlePath = join(base, 'middlemap.png');
  let config = {};
  let areas = [];
  let npcBytes = 0;
  let areaBytes = 0;
  let tileFilesExtracted = 0;
  try { config = parseIni(await readGb(configPath)); } catch {}
  try { areas = parseTab(await readGb(areaPath)).map(row => ({
    id: Number(row.id) || 0, name: row.name,
    x: Number(row.x), y: Number(row.y), z: Number(row.z),
    middlemap: row.middlemap, type: row.type,
  })).filter(row => Number.isFinite(row.x) && Number.isFinite(row.y)); } catch {}
  try { npcBytes = (await stat(npcPath)).size; } catch {}
  try { areaBytes = (await stat(areaPath)).size; } catch {}
  let middlemapAvailable = false;
  try { middlemapAvailable = (await stat(middlePath)).isFile(); } catch {}
  try {
    const files = await readdir(base);
    tileFilesExtracted = files.filter(name => /^0_-?\d+_-?\d+\.(png|dds|tga)$/i.test(name)).length;
  } catch {}
  let tileSheetAvailable = false;
  if (map.tileSheet) {
    try { tileSheetAvailable = (await stat(join(PROOF_ROOT, map.tileSheet))).isFile(); } catch {}
  }
  const descriptorsAvailable = middlemapAvailable && Object.keys(config).length > 0 && areaBytes > 0;
  const route = rel => `/artifact/${rel.split('/').map(encodeURIComponent).join('/')}`;
  return {
    ...map,
    config,
    middlemapAvailable,
    descriptorsAvailable,
    areaBytes,
    tileFilesExtracted,
    tileSheetAvailable,
    middlemapUrl: middlemapAvailable ? route(`extracted/data/source/maps/${map.folder}/middlemap.png`) : null,
    configUrl: route(`extracted/data/source/maps/${map.folder}/config.ini`),
    areaUrl: route(`extracted/data/source/maps/${map.folder}/area.tab`),
    npcUrl: route(`extracted/data/source/maps/${map.folder}/npc.tab`),
    areas,
    areaCount: areas.length,
    npcBytes,
  };
}

async function serveArtifact(res, encodedPath) {
  let rel;
  try { rel = decodeURIComponent(encodedPath); } catch { return send(res, 400, 'bad path'); }
  const full = resolve(PROOF_ROOT, rel);
  if (full !== PROOF_ROOT && !full.startsWith(PROOF_ROOT + sep)) return send(res, 403, 'forbidden');
  try {
    const bytes = await readFile(full);
    const ext = extname(full).toLowerCase();
    if (textExtensions.has(ext) && !(ext === '.lua' && bytes.subarray(0, 4).toString('latin1') === '\x1bLua')) {
      let text;
      try { text = new TextDecoder('utf-8', { fatal: true }).decode(bytes); }
      catch { text = new TextDecoder('gb18030').decode(bytes); }
      return send(res, 200, text, mime(full));
    }
    send(res, 200, bytes, mime(full));
  } catch {
    send(res, 404, `not found: ${rel}`);
  }
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url || '/', `http://${req.headers.host || 'localhost'}`);
  if (url.pathname === '/api/maps') {
    send(res, 200, JSON.stringify(await Promise.all(maps.map(mapDetails)), null, 2), 'application/json; charset=utf-8');
    return;
  }
  if (url.pathname.startsWith('/api/map/')) {
    const id = decodeURIComponent(url.pathname.slice('/api/map/'.length));
    const map = maps.find(item => item.id === id);
    if (!map) return send(res, 404, JSON.stringify({ error: 'unknown map' }), 'application/json; charset=utf-8');
    send(res, 200, JSON.stringify(await mapDetails(map), null, 2), 'application/json; charset=utf-8');
    return;
  }
  if (url.pathname.startsWith('/artifact/')) {
    return serveArtifact(res, url.pathname.slice('/artifact/'.length));
  }
  const staticFiles = {
    '/': 'index.html', '/index.html': 'index.html',
    '/app.js': 'app.js', '/styles.css': 'styles.css',
  };
  const file = staticFiles[url.pathname];
  if (!file) return send(res, 404, 'not found');
  try { send(res, 200, await readFile(join(APP_ROOT, file)), mime(file)); }
  catch { send(res, 404, 'missing app file'); }
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`JX3 Map UI Explorer: http://127.0.0.1:${PORT}`);
  console.log(`Serving evidence from ${PROOF_ROOT}`);
});
