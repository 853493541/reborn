12177|function parseMin2AniHeader(buf) {
12178|  if (!buf || buf.length < 0x42) return { error: 'Buffer too small', size: buf?.length || 0 };
12179|  const magic = buf.subarray(0, 4).toString('latin1');
12180|  if (magic !== 'MIN2') return { error: `Wrong magic: ${magic}`, size: buf.length };
12181|
12182|  const fileSize = buf.readUInt32LE(0x04);
12183|  const version = buf.readUInt32LE(0x08);
12184|  const boneCount = buf.readUInt32LE(0x0C);
12185|  const boneNameRaw = buf.subarray(0x10, 0x2E);
12186|  const nullIdx = boneNameRaw.indexOf(0);
12187|  const boneName = GB18030_DECODER.decode(boneNameRaw.subarray(0, nullIdx >= 0 ? nullIdx : 30));
12188|  const vertexCount = buf.readUInt32LE(0x2E);
12189|  const vertexCount2 = buf.readUInt32LE(0x32);
12190|  const frameCount = buf.readUInt32LE(0x36);
12191|  const fps = buf.readFloatLE(0x3A);
12192|  const vertexCount3 = buf.readUInt32LE(0x3E);
12193|
12194|  const dataStart = 0x42 + vertexCount * 4 * 2;
12195|  const expectedPosBytes = vertexCount * frameCount * 12;
12196|  const actualDataBytes = buf.length - dataStart;
12197|  const dataRatio = expectedPosBytes > 0 ? actualDataBytes / expectedPosBytes : 0;
12198|  const hasNormals = dataRatio > 1.5;
12199|
12200|  // Extract additional bone names for multi-bone files
12201|  const boneNames = [boneName];
12202|  if (boneCount > 1 && buf.length > dataStart) {
12203|    // Try to extract bone-related strings from the data region
12204|    const searchRegion = buf.subarray(0x42, Math.min(buf.length, 0x42 + 2000));
12205|    let pos = 0;
12206|    while (pos < searchRegion.length - 4) {
12207|      // Look for ASCII strings (bone names like "Bip01", "tongue", "smile_r")
12208|      if (searchRegion[pos] >= 0x20 && searchRegion[pos] < 0x7f) {
12209|        let end = pos;
12210|        while (end < searchRegion.length && searchRegion[end] >= 0x20 && searchRegion[end] < 0x7f) end++;
12211|        if (end - pos >= 3) {
12212|          const str = searchRegion.subarray(pos, end).toString('ascii');
12213|          if (/^[A-Za-z_][A-Za-z0-9_\-.]*$/.test(str) && !boneNames.includes(str)) {
12214|            boneNames.push(str);
12215|          }
12216|        }
12217|        pos = end + 1;
12218|      } else {
12219|        pos++;
12220|      }
12221|    }
12222|  }
12223|
12224|  return {
12225|    magic,
12226|    fileSize: buf.length,
12227|    headerFileSize: fileSize,
12228|    version,
12229|    boneCount,
12230|    boneName,
12231|    boneNames: boneNames.slice(0, 30),
12232|    vertexCount,
12233|    vertexCount2,
12234|    frameCount,
12235|    fps,
12236|    vertexCount3,
12237|    dataStart,
12238|    actualDataBytes,
12239|    expectedPosBytes,
12240|    dataRatio: Math.round(dataRatio * 1000) / 1000,
12241|    hasNormals,
12242|    canPlayVertexAnim: boneCount === 1 && vertexCount > 0 && frameCount > 1,
12243|    duration: frameCount > 1 && fps > 0 ? (frameCount - 1) / fps : 0,
12244|    headerHex: buf.subarray(0, Math.min(128, buf.length)).toString('hex'),
12245|  };
12246|}
12247|
12248|function sendJson(res, status, obj) {
12249|  const body = JSON.stringify(obj);
12250|  res.writeHead(status, {
12251|    'Content-Type': 'application/json; charset=utf-8',
12252|    'Content-Length': Buffer.byteLength(body),
12253|    'Access-Control-Allow-Origin': '*',
12254|    'Cache-Control': 'no-cache',
12255|  });
12256|  res.end(body);
12257|}
12258|
12259|function sendText(res, status, text) {
12260|  const body = String(text);