# JX3 game-server protocol — static reverse-engineering spec

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Method:** static analysis only (disassembly via capstone; no live capture, no hooking).
**Primary binary:** `C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3LogicEditOperationX64.dll`
(string set also present in `JX3ClientX64.exe`; image base `0x180000000`.)
**Raw evidence:** `proof/netcode/disasm/*.txt`, `proof/netcode/c2s_protocol_catalog.tsv`.

Confidence: **HIGH** = read directly from disassembly with cited addresses; **MED** = inferred from structure/strings; **LOW** = lead only.

---

## 1. Transport and threading

| Item | Finding | Evidence | Conf. |
|---|---|---|---|
| Socket object | `KPlayerClient` holds `m_piSocketStream` at `this+0xE3F8`, a `KNetDelegate`-style stream object | `RealSend` @ `0x1801AC802` (`"m_piSocketStream"` assert), `ThreadFunction` @ `0x1801AD550` | HIGH |
| Net thread | Dedicated thread per player session, first arg string `"NetToGSThread"` | `ThreadFunction` @ `0x1801AD0F7` | HIGH |
| Loop | net thread loops: wait readable (vtable `+0x38`) → recv packet (`+0x40`) → publish to logic queue → handle acks/timeouts; waits on a condition up to 1 s | `0x1801AD550`–`0x1801ADA24` | MED |
| Send | logic thread calls `KPlayerClient::Send`/`RealSend`; `RealSend` (`0x1801AC7D0`) sends via `m_piSocketStream` vtable `+0x30`; returns when `nRetCode == 1` | `0x1801AC8B0`, `0x1801AC91B` | HIGH |
| Socket stream API (vtable) | `+0x30` send, `+0x38` readable/wait (`-1` error, `0` none, `>0` data), `+0x40` receive (`-1` error, `-2` none, `1` packet), `+0x68` GetLastError | `0x1801AC8B0`, `0x1801AD566`, `0x1801AD588`, `0x1801ADAC0` | HIGH |

Transport itself is TCP (winsock via `CoreNet::KNetSocket` in `KBaseX64.dll`); this layer adds application-level reliability on top.

---

## 2. Packet frame

Requests are built as a fixed prefix buffer and passed to `KPlayerClient::SendPacket`
(`0x1801ACDA0`, caller-supplied total size) or sent as a `KNetBuffer` via `RealSend`.

Observed prefix layout (17-byte scratch path, e.g. `DoRoutineSync` @ `0x1801785D0`):

| Off | Size | Field | Evidence |
|---|---|---|---|
| `+0x00` | u16 | **protocol ID** | `DoRoutineSync`: `mov word [this+0x18CD8], 0x6E` @ `0x18017863A`; `DoScheduleMapAppointmentRequest`: `0x1C7` @ `0x180178733`; handshake `1` @ `0x1801AD161` |
| `+0x02` | u8 | **flags** (bit0 = retransmit, bit1 = carries ack/window) | `and byte [data+2], 0xFC` @ `0x1801AD178`; retransmit sets `or byte [data+2], 3` @ `0x1801AD6C9`; ping sets bit1 @ `0x1801AD995` |
| `+0x03` | u16 | **send serial** (retransmit path: ring serial) | `mov word [data+3], cx` @ `0x1801AD6DB` |
| `+0x05` | u16 | **ack serial** (last serial received from peer) | `mov word [data+5], r15w` (our recv serial) @ `0x1801AD6CD`; handshake writes `[this+0xE408]` @ `0x1801AD18C`; log reads it as `RecvSerial` @ `0x1801AD1DD` |
| `+0x07` | u32 | reserved / send-side field, 0 on control packets | handshake `mov dword [data+7], 0` @ `0x1801AD17F`; ping @ `0x1801AD9B4` |
| `+0x0B` | u32 | **protocol parameter** (RoleID for handshake, timestamp for ping) | handshake writes `[this+0xE434]` (RoleID) @ `0x1801AD16B`; ping writes tick @ `0x1801AD99C`; log prints `[data+0xB]` as RoleID @ `0x1801AD1E5` |
| `+0x0F` | — | protocol-specific tail starts here | routine sync: `u16 size` @ `+0xF`, payload @ `+0x11` @ `0x180178641`/`0x180178660`; handshake: 16-byte session key @ `+0xF`, bool @ `+0x1F` @ `0x1801AD183`/`0x1801AD17C` |

Packet size limits: `MAX_EXTERNAL_PACKAGE_SIZE = 0x8000` (32768); `DoRoutineSync`
rejects `size + 0x11 >= 0x8000` (`0x1801785E9`).

---

## 3. Reliability layer (application-level ACK)

JX3 runs its own serial/ack/retransmit on top of the socket.

| Element | Finding | Evidence |
|---|---|---|
| Unconfirmed send ring | 2048 slots (`0x800`), head at `+0x8002`, count at `+0x8000`, window end `+0x8006`; base `this+0xE448` | `0x1801AD5BD`–`0x1801AD640` |
| Ack handling on receive | if incoming flags bit1 set: walk unconfirmed ring, confirm serials using incoming `ack` field at `+0x5` | `0x1801AD5B3`, `0x1801AD5CC`, `0x1801AD670` |
| Retransmit | for each still-unconfirmed slot: set `flags |= 3`, rewrite `ack = our recv serial`, rewrite serial, `RealSend` | `0x1801AD6C9`–`0x1801AD6E2` |
| Ack/window control ID | special-cased protocol `0x2FE` calls a confirm helper before retransmit loop | `0x1801AD643`, `call 0x180169AB0` @ `0x1801AD670` |
| Send queue full | `"[KPlayerClient] Unconfirm send buffer full!"`; flushes/resets unconfirmed buffer then continues | `0x1801AD49E`, `0x1801AD4BF` |
| Unconfirmed index | 20-bit sequence field in a lock-free 64-bit slot (`mask 0xFFFFF`, window size limit at `this+0x18CC0`), flag bits 60/61 | `0x1801AD2A0`–`0x1801AD360` |
| Logic handoff ring | received `KNetBuffer*` published into array at `this+0x18CB0` indexed by 20-bit sequence (`this+0x18CA8` window, `this+0x18CB8` head) | `0x1801AD827`–`0x1801AD88B` |

Interpretation (MED): with TCP underneath, this layer exists to know what the
**peer application has processed**, not merely what the kernel delivered —
i.e. ack-of-processing. Retransmission of unacked messages is bounded by the
ring (2048 outstanding).

---

## 4. Session lifecycle

### 4.1 Connect + handshake

`DoHandshakeRequest` (built inline in `ThreadFunction`, `0x1801AD0C0`–`0x1801AD210`):

- protocol ID `1` (`0x1801AD161`)
- `RoleID` at `+0xB` from `this+0xE434` (`0x1801AD165`)
- 16-byte session key/blob at `+0xF` from `this+0xE438` (`0x1801AD171`, `0x1801AD183`)
- flags cleared (`& 0xFC`) (`0x1801AD178`)
- resume bool at `+0x1F` = `(RecvSerial != 0)` (`0x1801AD16E`, `0x1801AD17C`)
- `RecvSerial` (u16) at `+0x5` from `this+0xE408` (`0x1801AD18C`)
- log: `[KPlayerClient] DoHandshakeRequest RoleID:%u, RecvSerial:%d, Count:%d`

Response handling: `[KPlayerClient] OnHandShakeRespond bRecover=%d, ServerName=%s, ReconnectTimeout=%d, Success=%d` (`0x1801AD...`/`0x0077B410`).

Meaning (MED): client can resume a session by presenting the last received serial;
server answers whether it recovered the session (`bRecover`) and how long the
client may keep trying (`ReconnectTimeout`).

### 4.2 Ping / liveness

Ping builder in `ThreadFunction` (`0x1801AD964`–`0x1801AD9BC`):

- fires when `now >= lastPingTime + 0xBB8` (**3000 ms**) (`0x1801AD95B`)
- 15-byte packet: protocol ID `6` (`0x1801AD990`, `mov eax, 6`), flags bit1 set, `ack = recv serial`, param `+0xB = now tick` (`0x1801AD99C`)
- `lastPingTime` stored at `this+0x28EE8`

Timeout (`0x1801AD939`–`0x1801AD94F`):

- if `this+0xE404 != 0`: disconnect when `now >= lastRecv + 0x2EE0` (**12000 ms**)
- assert text ties it together: `dwNowTime < m_dwLastRecvPackTime + cdwPingInterval * 4` (`0x1801AD...`, string @ `0x1801ADB3A`)
- ⇒ **ping interval = 3000 ms, dead timeout = 12000 ms** (HIGH)

Reconnect: `TriggerReconnect` (`0x0076BFB8` string), `[KPlayerClient] GS disconnected. bWaitReconnect = %s.` (`0x0076BF30`), representation event `KREPRESENT_EVENT_RECONNECTED`.

### 4.3 Timeouts and error handling

- vtable `+0x38` returning `-1` → disconnect path (`0x1801AD569` → `0x1801ADB63`)
- recv `-1` → error, `-2` → no packet; errors logged via `MLogProcessError` with function/line
- loop condition-variable wait ≈ 1 s (`0x1801ADA08`: `+0x989680` = 10,000,000 × 100 ns)

---

## 5. Compression and crypto

| Item | Finding | Evidence | Conf. |
|---|---|---|---|
| Compression toggle/threshold | `NetCompress`, `GetNetCompressThreshold`, `IsOpenNetCompress`, `SetNetCompressThreshold`, `CompressPackage`, `PackCompressHead`, `PrintNetCompressRate`, Lua setters | `symbols_JX3LogicEditOperation_net.txt` | HIGH (existence) / MED (defaults) |
| Symmetric crypto | `KAESCryptor::Encrypt/Decrypt`, `KAESFile`, `KSimpleCryptor::Encrypt/Decrypt` (custom rotate + 16-byte group exchange, `cycleShiftLeft/Right`, `exchangeGroupBy16Byte`, `getBitMask`) | `exports_KBaseX64.txt` | HIGH (existence) / LOW (game-stream usage) |
| Wire order | not determinable statically | — | — |

---

## 6. Direction and dispatch model

- Client→server requests are `KPlayerClient::Do*` (≈460 methods), each building
  a packet and calling `SendPacket`/`RealSend`. Catalog of extracted IDs:
  `proof/netcode/c2s_protocol_catalog.tsv` (429 rows; attribution is heuristic —
  treat IDs as **LOW/MED** until manually verified, like handshake/routine sync).
- Server→client notifications are `KPlayerClient::On*` (≈450 methods) and are
  size-validated against a compiled table: `uDataLen == m_nProtocolSize[nProtocolID]`
  (`0x00793198`, `0x00795258`, `0x007A25A0`); ranges `nap`, `pfp_s2c`, `grp_s2c`,
  `hpc`, `emgp_s2c`, `wpspt`, `vmpc`, `vr2c`, `cs2c`, `eccrq`, `apc`, `spc`, `evp`.
- Protocol recorder/replayer exists for tests: `KProtocolRecorder::Push`,
  `KProtocolReplayer::Pop/Cache`.

### Message families (S2C `On*`, from `symbols_KPlayerClient.txt`)

| Family | Examples |
|---|---|
| world replication | `OnSyncEntity`, `OnSyncEntityGroup`, `OnSyncNewPlayer`, `OnSyncNewNpc`, `OnSyncSimpleObject`, `OnRemoveDoodad`, `OnSyncNpcDisappearFrame` |
| movement | `OnMoveCharacter`, `OnAdjustPlayerMove`, `OnSyncMoveState`, `OnSyncMoveCtrl`, `OnSyncMoveParam`, `OnSyncPostureState`, `OnSyncTiltAngle`, `OnSyncRunSpeedLimit`, `OnNavResult` |
| combat | `OnSkillPrepare`, `OnSkillCast`, `OnSkillChannel`, `OnSkillEffectResult`, `OnSkillBeatBack`, `OnSkillRayEffect`, `OnSkillChainEffect`, `OnPointChainSkillEffect`, `OnStartHoardSkill` |
| cooldowns/buffs | `OnResetCooldown`, `OnPauseCDTimer`, `OnAccelerateCDTimer`, `OnCoolDownOverDraftNotify`, `OnSyncBuffList`, `OnSyncBuffSingle` |
| session | `OnHandShakeRespond`, `OnSwitchGS`, `OnKickAccountNotify`, `OnSyncPlayerLoginCSInfo` |
| periodic state | ~300 `OnSync*` methods (items, quests, talents, team, arena, ...) |

---

## 7. Known protocol numbers

| ID | Direction | Meaning | Conf. | Evidence |
|---|---|---|---|---|
| `0x0001` | C→S | handshake request (RoleID, key, resume serial) | HIGH | `0x1801AD161` |
| `0x0006` | C→S | ping (u32 timestamp at `+0xB`, 15 bytes, every 3000 ms) | HIGH | `0x1801AD990` |
| `0x006E` | C→S | routine sync (`u32 param @ +0xB`, `u16 size @ +0xF`, payload @ `+0x11`) | HIGH | `0x18017863A` |
| `0x02FE` | both | ack/window control packet | MED | `0x1801AD643` |
| `0x00A2` | C→S | example request builder `DoSaveMoneyInTongRequest` (single u32 at `+0xB`, 15 B total) | HIGH | `0x1801786A3` |
| `0x01C7` | C→S | example `DoScheduleMapAppointmentRequest` (single u32 at `+0xB`, 15 B) | HIGH | `0x180178733` |
| `0x0049` | C→S | `DoCastProfessionSkill` (packet 0x20 = 32 B) | MED | catalog row |
| `0x01BF` | C→S | `DoNavTo` (size 0x5B = 91 B) | MED | catalog row |

The full `c2s_protocol_catalog.tsv` contains 429 (function, id) rows but function
attribution and some IDs are heuristic; verify each used ID by disassembling its
builder like we did for the four HIGH rows above.

---

## 8. What is proven vs. still unknown

**Proven (enough to recreate the model):** transport object and thread model,
frame layout for the common prefix, serial/ack/retransmit reliability, handshake
with resume, ping/timeout values, routine-sync packet shape, message families and
direction naming, fixed-size protocol table dispatch, compression/crypto existence.

**Still unknown (needs live capture or table extraction; out of static scope):**
- field layouts of the ~900 individual protocols (only shapes of 4 are decoded)
- exact `m_nProtocolSize` table contents and ID→handler mapping
- whether AES / custom crypt / compression are applied to the game stream and in what order
- server-side process topology (`R2C`, `L2C`, `G2C` prefixes imply tiers)

For reborn we do **not** need JX3's exact bytes or IDs; we recreate the model
with our own opcode plan. See `REBORN_SERVER_SPEC.md`.
