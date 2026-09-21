function loadPlayerAnimationTable(bodyType) {
  const key = `anim_${bodyType}`;
  if (PLAYER_ANIM_CACHE.has(key)) return PLAYER_ANIM_CACHE.get(key);
  const filePath = join(PLAYER_ANIM_TABLE_DIR, `player_animation_${bodyType}.txt`);
  if (!existsSync(filePath)) return null;
  const content = readGb2312File(filePath);
  const lines = content.split(/\r?\n/);
  const header = lines[0];
  const cols = header.split('\t');
  const entries = [];
  for (let i = 1; i < lines.length; i++) {
    const line = lines[i];
    if (!line.trim()) continue;
    const parts = line.split('\t');
    const animId = parseInt(parts[0], 10);
    if (isNaN(animId)) continue;
    const animFile = (parts[6] || '').trim();
    if (!animFile) continue; // skip entries with no animation file
    entries.push({
      id: animId,
      kindId: parseInt(parts[1], 10) || 0,
      sheathType: parseInt(parts[2], 10) || 0,
      animRatio: parts[3] || '',
      animSpeed: parts[4] || '',
      isLoop: parseInt(parts[5], 10) || 0,
      animFile,
      shadowFile: (parts[7] || '').trim(),
      noAutoTurn: parseInt(parts[8], 10) || 0,
      lookAtCamera: parseInt(parts[9], 10) || 0,
      poseState: (parts[10] || '').trim(),
      lockFacing: (parts[11] || '').trim(),
    });
  }
  PLAYER_ANIM_CACHE.set(key, entries);
  return entries;
}

function loadSerialAnimationTable() {
  const key = 'serial_table';
  if (PLAYER_ANIM_CACHE.has(key)) return PLAYER_ANIM_CACHE.get(key);
  const filePath = join(PLAYER_ANIM_TABLE_DIR, 'player_serial_animation_table.txt');
  if (!existsSync(filePath)) return null;
  const content = readGb2312File(filePath);
  const lines = content.split(/\r?\n/);
  const entries = [];
  for (let i = 1; i < lines.length; i++) {
    const line = lines[i];
    if (!line.trim()) continue;
    const parts = line.split('\t');
    const serialId = parseInt(parts[0], 10);
    if (isNaN(serialId)) continue;
    entries.push({
      serialId,
      desc: (parts[1] || '').trim(),
      phaseA: parseInt(parts[2], 10) || 0,
      phaseB: parseInt(parts[3], 10) || 0,
      phaseC: parseInt(parts[4], 10) || 0,
      haste: parseInt(parts[5], 10) || 0,
    });
  }
  PLAYER_ANIM_CACHE.set(key, entries);
  return entries;
}

function loadTaniCatalog() {
  const key = 'tani_catalog';
  if (PLAYER_ANIM_CACHE.has(key)) return PLAYER_ANIM_CACHE.get(key);
  if (!existsSync(TANI_RT_PATH)) return null;
  const content = readGb2312File(TANI_RT_PATH);
  const lines = content.split(/\r?\n/);
  const entries = [];
  for (let i = 1; i < lines.length; i++) {
    const line = lines[i];
    if (!line.trim()) continue;
    const parts = line.split('\t');
    entries.push({
      id: parseInt(parts[0], 10) || 0,
      name: (parts[1] || '').trim(),
      sourcePath: (parts[2] || '').trim(),
      shellPath: (parts[3] || '').trim(),
    });
  }
  PLAYER_ANIM_CACHE.set(key, entries);
  return entries;
}

// ─── Actor Animation Player helpers ──────────────────────────────────────────

function listActorPlots() {
  if (!existsSync(ACTOR_PLOT_ROOT) || !statSync(ACTOR_PLOT_ROOT).isDirectory()) return [];
  const plots = readdirSync(ACTOR_PLOT_ROOT, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => {
      const plotDir = join(ACTOR_PLOT_ROOT, d.name);
      let totalAni = 0;
      let totalAudio = 0;
      let subPlotCount = 0;
      try {
        for (const sub of readdirSync(plotDir, { withFileTypes: true })) {
          if (!sub.isDirectory()) continue;
          subPlotCount++;
          const aniDir = join(plotDir, sub.name, '\u52A8\u4F5C');
          const audioDir = join(plotDir, sub.name, '\u97F3\u9891\u8D44\u6E90');
          try {
            if (existsSync(aniDir) && statSync(aniDir).isDirectory()) {
              totalAni += readdirSync(aniDir).filter(f => f.toLowerCase().endsWith('.ani')).length;
            }
          } catch {}
          try {
            if (existsSync(audioDir) && statSync(audioDir).isDirectory()) {
              totalAudio += readdirSync(audioDir).filter(f => /\.(wav|mp3|ogg|wem)$/i.test(f)).length;
            }
          } catch {}
        }
      } catch {}
      return { name: d.name, subPlotCount, totalAni, totalAudio };
    })
    .sort((a, b) => b.totalAni - a.totalAni || a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
  return plots;
}

function getActorPlotContents(plotName) {
  const plotDir = safePathUnder(ACTOR_PLOT_ROOT, plotName);
  if (!plotDir || !existsSync(plotDir) || !statSync(plotDir).isDirectory()) return null;

  const result = { plotName, subPlots: [] };

  for (const entry of readdirSync(plotDir, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const subDir = join(plotDir, entry.name);
    const aniDir = join(subDir, '\u52A8\u4F5C'); // 动作
    const audioDir = join(subDir, '\u97F3\u9891\u8D44\u6E90'); // 音频资源

    const actorFiles = [];
    for (const f of readdirSync(subDir, { withFileTypes: true })) {
      if (f.isFile() && f.name.toLowerCase().endsWith('.actor')) actorFiles.push(f.name);
    }
    actorFiles.sort();

    const aniFiles = [];
    if (existsSync(aniDir) && statSync(aniDir).isDirectory()) {
      for (const f of readdirSync(aniDir, { withFileTypes: true })) {
        if (f.isFile() && f.name.toLowerCase().endsWith('.ani')) {
          aniFiles.push({ name: f.name, size: statSync(join(aniDir, f.name)).size });
        }
      }
      aniFiles.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
    }

    const audioFiles = [];
    if (existsSync(audioDir) && statSync(audioDir).isDirectory()) {
      for (const f of readdirSync(audioDir, { withFileTypes: true })) {
        if (f.isFile() && /\.(wav|mp3|ogg|wem)$/i.test(f.name)) {
          audioFiles.push({ name: f.name, size: statSync(join(audioDir, f.name)).size });
        }
      }
      audioFiles.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
    }

    result.subPlots.push({
      name: entry.name,
      actorFiles,
      aniCount: aniFiles.length,
      aniFiles: aniFiles.slice(0, 500), // cap for large plots
      audioFiles,
    });
  }

  // Also check for .actor files directly in the plot root
  const rootActors = [];
  for (const f of readdirSync(plotDir, { withFileTypes: true })) {
    if (f.isFile() && f.name.toLowerCase().endsWith('.actor')) rootActors.push(f.name);
  }
  rootActors.sort();
  if (rootActors.length > 0) {
    result.rootActorFiles = rootActors;
  }

  return result;
}

/**
 * Parse a GATA .tani binary.
 * Format: "GATA" magic, u32 version, then null-terminated GB2312 .ani path,
 * followed by runtime data + embedded PSS/SFX/sound strings.
 * We do best-effort string extraction since the format contains runtime pointers.
 */
function parseTaniBinary(buf, originalPath) {
  if (!buf || buf.length < 8) return { error: 'Buffer too small', size: buf?.length || 0 };
  const magic = buf.subarray(0, 4).toString('latin1');
  if (magic !== 'GATA') return { error: `Wrong magic: ${magic}`, size: buf.length };

  const version = buf.readUInt32LE(4);

  // Read null-terminated GB2312 string for .ani path starting at offset 8
  let nullPos = 8;
  while (nullPos < buf.length && buf[nullPos] !== 0) nullPos++;
  const aniPath = GB18030_DECODER.decode(buf.subarray(8, nullPos));

  // Scan entire buffer for embedded paths and tags using string extraction
  const pssPaths = [];
  const pssEntries = [];  // [{path, startTimeMs}]
  const sfxTags = [];
  const soundEvents = [];
  const userTags = [];
  const otherPaths = [];

  // Extract all readable GB2312 strings of length >= 6
  let i = nullPos + 1;
  while (i < buf.length) {
    // Check if we're at a printable ASCII or GB2312 multi-byte start
    if ((buf[i] >= 0x20 && buf[i] < 0x7F) || (buf[i] >= 0xA1 && buf[i] <= 0xFE && i + 1 < buf.length && buf[i + 1] >= 0x40)) {
      let end = i;
      while (end < buf.length) {
        if (buf[end] >= 0x20 && buf[end] < 0x7F) {
          end++;
        } else if (buf[end] >= 0xA1 && buf[end] <= 0xFE && end + 1 < buf.length && buf[end + 1] >= 0x40) {
          end += 2;
        } else {
          break;
        }
      }
      if (end - i >= 6) {
        const str = GB18030_DECODER.decode(buf.subarray(i, end));
        const lower = str.toLowerCase();
        if (lower.endsWith('.pss') && lower.startsWith('data\\')) {
          if (!pssPaths.includes(str)) {
            pssPaths.push(str);
            // No silent fallback: GATA per-PSS start time is not engine-verified.
            // The binary contains live C++ runtime pointers before each PSS path, so any
            // fixed-offset float read would hit vtable/heap data. Per the no-silent-fallback
            // policy (issue #8), we no longer fabricate a 0 ms start time. The entry carries
            // startTimeMs=null plus a structured warning. Downstream code may still derive a
            // real time from the source PSS globalStartDelay (timingSource='pss-global-delay');
            // when that derivation is not possible the entry's effectiveStartTimeMs stays null
            // and the player surfaces it as an unresolved warning.
            pssEntries.push({
              path: str,
              startTimeMs: null,
              gataTimingWarning: 'GATA per-PSS start time is not engine-verified; binary holds live runtime pointers in the per-entry header. No fabricated 0 fallback. effectiveStartTimeMs is set only when the source PSS globalStartDelay can be read.',
            });
          }
        } else if (lower.endsWith('.pss') && lower.includes('\\pss\\')) {
          // Partial path (missing prefix)
          // Skip duplicates from partial references
        } else if (str.startsWith('New SFX Tag')) {
          if (!sfxTags.includes(str)) sfxTags.push(str);
        } else if (str === 'User Define Tag') {
          userTags.push(str);
        } else if (str.startsWith('JX3_Skill') || str.startsWith('JX3_')) {
          if (!soundEvents.includes(str)) soundEvents.push(str);
        } else if (str.includes('/skill/') || str.includes('/Skill/')) {
          if (!soundEvents.includes(str)) soundEvents.push(str);
        } else if ((lower.endsWith('.ani') || lower.endsWith('.tani')) && lower.startsWith('data\\')) {
          if (str !== aniPath && !otherPaths.includes(str)) otherPaths.push(str);
        }
      }
      i = end;
    } else {
      i++;
    }
  }

  // Combine JX3_Skill prefix with its event path
  const soundEntries = [];
  for (let s = 0; s < soundEvents.length; s++) {
    if (soundEvents[s].startsWith('JX3_') && s + 1 < soundEvents.length && soundEvents[s + 1].includes('/')) {
      soundEntries.push({ system: soundEvents[s], event: soundEvents[s + 1] });
      s++;
    } else {
      soundEntries.push({ system: '', event: soundEvents[s] });
    }
  }

  for (const entry of pssEntries) {
    const derivedTiming = deriveTaniPssTimingFromSource(entry.path);
    if (!derivedTiming) {
      // No silent fallback. GATA didn't give us a real start time and the source
      // PSS could not be resolved either, so effectiveStartTimeMs stays null and the
      // gataTimingWarning attached at extraction time remains the authoritative note.
      entry.effectiveStartTimeMs = null;
      entry.timingSource = 'unresolved-no-source-pss';
      continue;
    }
    entry.effectiveStartTimeMs = derivedTiming.effectiveStartTimeMs;
    entry.pssStartDelayMs = derivedTiming.pssStartDelayMs;
    entry.pssPlayDurationMs = derivedTiming.pssPlayDurationMs;
    entry.pssTotalDurationMs = derivedTiming.pssTotalDurationMs;
    entry.timingSource = derivedTiming.timingSource;
  }

  // Top-level acknowledgement so any consumer (UI, downstream tooling) can see
  // the format gap without having to re-discover it on every entry.
  const gataTimingStatus = {
    perEntryStartTime: 'not-extracted',
    reason: 'GATA per-PSS start time field offsets are not engine-verified. No silent 0 fallback (issue #8). effectiveStartTimeMs comes only from the source PSS globalStartDelay; when that is unresolvable, it stays null.',
    unresolvedCount: pssEntries.filter((e) => e.timingSource === 'unresolved-no-source-pss').length,
    derivedCount: pssEntries.filter((e) => e.effectiveStartTimeMs != null).length,
  };

  return {
    magic,
    version,
    fileSize: buf.length,
    path: originalPath || '',
    aniPath,
    pssPaths,
    pssEntries,
    sfxTags: sfxTags.length,
    userTags: userTags.length,
    sfxTagList: sfxTags,
    userTagList: userTags,
    soundEntries,
    otherPaths,
    gataTimingStatus,
  };
}

function formatHexOffset(offset, width = 6) {
  const n = Number(offset) || 0;
  return `0x${n.toString(16).padStart(width, '0')}`;
}

function hexBytes(buffer, start = 0, end = buffer?.length || 0) {
  const out = [];
  const safeStart = Math.max(0, start | 0);
  const safeEnd = Math.max(safeStart, Math.min(end | 0, buffer?.length || 0));
  for (let i = safeStart; i < safeEnd; i++) out.push(buffer[i].toString(16).padStart(2, '0'));
  return out.join(' ');
}

function asciiPreview(buffer, start = 0, end = buffer?.length || 0) {
  let out = '';
  const safeStart = Math.max(0, start | 0);
  const safeEnd = Math.max(safeStart, Math.min(end | 0, buffer?.length || 0));
  for (let i = safeStart; i < safeEnd; i++) {
    const b = buffer[i];
    out += b >= 0x20 && b < 0x7f ? String.fromCharCode(b) : '.';
  }
  return out;
}

function buildFullHexDumpLines(buffer, bytesPerLine = 16, baseOffset = 0) {
  const lines = [];
  if (!buffer) return lines;
  for (let offset = 0; offset < buffer.length; offset += bytesPerLine) {
    const end = Math.min(offset + bytesPerLine, buffer.length);
    const hex = hexBytes(buffer, offset, end).padEnd(bytesPerLine * 3 - 1, ' ');
    const ascii = asciiPreview(buffer, offset, end);
    lines.push(`${formatHexOffset(baseOffset + offset)}  ${hex}  ${ascii}`);
  }
  return lines;
}

function decodeGb18030Safe(bytes) {
  try {
    return GB18030_DECODER.decode(Buffer.from(bytes)).replace(/\0+$/g, '');
  } catch {
    return Buffer.from(bytes).toString('latin1').replace(/\0+$/g, '');
  }
}

function categorizeTaniText(value) {
  const text = String(value || '').trim();
  const lower = text.toLowerCase();
  if (/\.pss$/i.test(text)) return 'pss-path';
  if (/\.ani$/i.test(text)) return 'ani-path';
  if (/\.tani$/i.test(text)) return 'tani-path';
  if (/^jx3_/i.test(text)) return 'wwise-system';
  if (/\/(skill|music|sound)\//i.test(text)) return 'wwise-event';
  if (/tag/i.test(text)) return 'timeline-tag';
  if (/^data[\\/]/i.test(text)) return 'asset-path';
  if (/\.(?:mesh|track|jsondef|dds|tga|sfx)$/i.test(lower)) return 'asset-path';
  if (MODULE_NAME_WHITELIST.has(text)) return 'pss-module';
  return 'text';
}

function shouldKeepReadableStringRun(text, category) {
  const value = String(text || '').trim();
  if (value.length < 3) return false;
  if (/[\u0000-\u001f\u007f\ufffd\ue000-\uf8ff]/u.test(value)) return false;
  if (category === 'asset-path' || category === 'pss-path' || category === 'ani-path' || category === 'tani-path') {
    if (!/^data[\\/]/i.test(value)) return false;
    if (category === 'pss-path') return /\.pss$/i.test(value);
    if (category === 'ani-path') return /\.ani$/i.test(value);
    if (category === 'tani-path') return /\.tani$/i.test(value);
    return /\.(?:mesh|track|jsondef|jsoninspack|dds|tga|sfx|pss|ani|tani)$/i.test(value);
  }
  if (category !== 'text') return true;
  if (MODULE_NAME_WHITELIST.has(value)) return true;

  // Generic GB18030 binary scans are very noisy: arbitrary float/int bytes can
  // decode into plausible Hanzi. Keep only plain ASCII labels here; PSS module
  // names are added separately from parser-confirmed offsets.
  if (!/[A-Za-z0-9]/.test(value)) return false;
  if (/^[+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?$/i.test(value)) return false;
  if (!/^[A-Za-z0-9_ .:/\\\-()[\]+#]+$/.test(value)) return false;
  if (/^\[[A-Za-z0-9_ .:/\\\-()[\]+#]{3,80}\]$/.test(value)) return true;
  if (/^Microsoft \(R\) HLSL Shader Compiler \d+(?:\.\d+)*$/i.test(value)) return true;
  if (/^Texture(?:1D|2D|3D|Cube)?\s+[A-Za-z_][A-Za-z0-9_]*\s*:\s*register\(\s*[A-Za-z]\d+\s*\)$/i.test(value)) return true;
  if (/^skillremake_[A-Za-z0-9_]+$/i.test(value)) return true;
  return false;
}

function findAsciiDataPathStart(bytes) {
  for (let index = 0; index + 4 < bytes.length; index++) {
    if (bytes[index] === 0x64
      && bytes[index + 1] === 0x61
      && bytes[index + 2] === 0x74
      && bytes[index + 3] === 0x61
      && (bytes[index + 4] === 0x2f || bytes[index + 4] === 0x5c)) {
      return index;
    }
  }
  return -1;
}

function extractTaniReadableStringRuns(buffer, minBytes = 4) {
  const runs = [];
  if (!buffer) return runs;
  let bytes = [];
  let start = 0;

  const flush = () => {
    if (bytes.length >= minBytes) {
      let rowBytes = bytes;
      let rowStart = start;
      let text = decodeGb18030Safe(rowBytes).trim();
      let category = categorizeTaniText(text);
      if (category === 'asset-path') {