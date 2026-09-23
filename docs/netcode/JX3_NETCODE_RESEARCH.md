# JX3 netcode — static research notes

**Branch:** `research/jx3-netcode`
**Date:** 2026-09-21
**Method:** static only — no live capture, no hooking, no client modification.
**Evidence root:** `proof/netcode/` (raw keyword dumps + PE import tables with byte offsets).

Confidence labels:

| Label | Meaning |
|---|---|
| **HIGH** | exact string/import present on disk, offset cited |
| **MED** | symbol present; role inferred from naming/context |
| **LOW** | hypothesis, no direct evidence yet |

---

## 1. Evidence inventory

| File | Source binary | Contents |
|---|---|---|
| `JX3ClientX64_exe_net_strings.txt` | `bin64\JX3ClientX64.exe` (10.8 MB) | 9,110 hits; **KGatewayClient + KPlayerClient live here** |
| `JX3LogicEditOperation_net_strings.txt` | `JX3LogicEditOperationX64.dll` (10.7 MB) | 8,412 hits; full `KPlayerClient` protocol surface |
| `JX3RepresentX64_net_strings.txt` | `JX3RepresentX64.dll` (16.9 MB) | SO3Represent: local/remote character, interpolation |
| `KBaseX64_net_strings.txt` | `KBaseX64.dll` (899 KB) | **CoreNet** socket stack, AES classes |
| `SIMWorldX64_net_strings.txt` | `SIMWorldX64.dll` (416 KB) | PhysX gameplay world, character controller inputs |
| `DLBT_net_strings.txt` | `DLBT.dll` (8.1 MB) | BitTorrent downloader — **not gameplay net** |
| `NAVX64_net_strings.txt` | `NAVX64.dll` | PathEngine nav only |
| `net_import_modules.txt` | all 339 bin64 binaries | winsock/winhttp importers |
| `pe_imports.py` outputs (`imports_*.txt`) | key DLLs | direct import tables |
| `protocol_surface.txt` | Logic dump | 55 C2S/S2C names referenced in asserts |
| `symbols_KPlayerClient.txt` | Logic dump | **923 `KPlayerClient` methods** |
| `symbols_KBaseX64_net.txt` | KBase dump | 136 CoreNet/timer/crypto symbols |
| `symbols_JX3LogicEditOperation_net.txt` | Logic dump | 571 net/sync/compress symbols |

`JX3ClientX64Base.dll` has **only one import** (`LoadLibraryA`, `imports_JX3ClientX64Base.txt`) — it resolves everything dynamically and exposes almost no strings; not useful statically so far.

Client logs (`logs\JX3Client_2052-zhcn\2026_*\`) contain only log-init lines (174 bytes each) — no net telemetry. Log sweep is a dead end.

---

## 2. Layer map (as evidenced)

```
JX3ClientX64.exe  (+ JX3LogicEditOperationX64.dll — same logic set)
├─ KGatewayClient        login / account / role list / gateway handshake
│    Login_SetGatewayAddress, Connect to %s:%u, TWAccountVerify, WGAccountVerify,
│    StreamingAccountVerify, OnSyncRoleList, DoLoginGameRequest,
│    DoQueryMapQueueInfo, OnSyncLoginKey, DoPingSignal
└─ KPlayerClient         game-server session (923 methods)
     ├─ movement        OnMoveCharacter, OnAdjustPlayerMove,
     │                  OnSyncMoveState / OnSyncMoveCtrl / OnSyncMoveParam,
     │                  OnSyncPostureState, OnSyncTiltAngle, OnSyncRunSpeedLimit,
     │                  DoNavTo / DoNavStop / OnNavResult
     ├─ skills          OnSkillPrepare, OnSkillCast, OnSkillChannel,
     │                  OnSkillEffectResult, OnSkillBeatBack, OnSkillRayEffect,
     │                  OnSkillChainEffect, OnPointChainSkillEffect,
     │                  DoCharacterSkill, DoCastProfessionSkill, DoCastHoardSkill,
     │                  OnResetCooldown, OnPauseCDTimer, OnAccelerateCDTimer,
     │                  OnCoolDownOverDraftNotify
     ├─ world repl.     OnSyncEntity / OnSyncEntityGroup / OnSyncNewPlayer /
     │                  OnSyncNewNpc / OnSyncSimpleObject / OnRemoveDoodad
     ├─ state           OnSyncBuffList / OnSyncBuffSingle / OnSyncPlayerStateInfo /
     │                  OnSyncPlayerOperationMask / OnSyncStealthCharacter ...
     ├─ scheduling      DoRoutineSync, AddNextSyncTime, NeedSync,
     │                  SetPlayerSyncFrameInterval (+ Lua wrapper)
     ├─ reconnect       DoHandshakeRequest(RoleID, RecvSerial, Count),
     │                  OnHandShakeRespond(bRecover, ServerName, ReconnectTimeout),
     │                  TriggerReconnect, OnSwitchGS, GetGSIP
     └─ net health      GetPingValue, DoUploadPingStatData,
                        dwNowTime < m_dwLastRecvPackTime + cdwPingInterval * 4
     NetCompress: GetNetCompressThreshold, IsOpenNetCompress,
                  PackCompressHead, CompressPackage
JX3ClientX64.exe stack (login layer)
KBaseX64.dll
  CoreNet::KNetDelegate  Connect/Disconnect/Tick(float)/Send/SendAndRelease/
                         DoRead/DoWrite/SetSoTimeout/OnConnected/OnDisconnected/
                         OnConnectTimeout/OnExceptionCaught
  CoreNet::KNetSocket    Init/Connect/Read/Write/Close/IsConnected/SetInetAddress
  CoreNet::KNetBuffer    typed Read/Write Int8..UInt64, Float, Double, String;
                         reader/writer pos, Clone, DiscardReadContent
  KAESCryptor / KAESFile / KSimpleCryptor   AES128_Encrypt, Simple_Encrypt
  KDeltaTimer / KTimer / KMicroTimer, KAsyncTaskManager::Tick
JX3RepresentX64.dll  (SO3Represent — the "view" of the world)
  KRLLocalCharacter::CastSkill / PrepareCastSkill / UpdateSkillBuff /
                     UpdateJumpEndPosition / UpdateLookAtPosition
  KRLRemoteCharacter::UpdatePosition / CastSkill / PrepareCastSkill / UpdateSkillBuff
  KRLLocalCharacterFrameData::Interpolate
  KRLRemoteCharacterFrameData::Interpolate
  KRLCharacterFrameData::AdjustPosition / ConvertFramePosition
  "disable remote character interpolate"          (runtime toggle string)
  KREPRESENT_EVENT_RECONNECTED  <- krlEventAdaptor::HandleReconnected
  KGameWorldHandler::MouseControlMoveEnable / AutoMoveToPoint / AutoMoveToTarget
  KGameWorldCharacterController::GetMoveInfo
SIMWorldX64.dll  (PhysX 3.3.4 gameplay world — collision/physics, not network)
  PxWorld::RayCast / GetFloorHeight / SetVelocity / GetPlayerPos /
  OnPositionChanged / OnFixedModePositionChanged / npclist / remoteplayerlist
  character controller inputs:
    inputDeltaTime, inputCharacterTrajectoryDeltaPos, inputCharacterTrajectoryDeltaQuat,
    inputAchievedRequestedMovement, solverFlagUnevenTerrian, solverFlagTwoBone
```

`DLBT.dll` is a **BitTorrent-based downloader** (`DLBT_Downloader_*`, trackers, DHT, `EnableUDPTransfer`) — patcher/streaming only.

---

## 3. Findings (evidence-backed)

| # | Finding | Evidence (offset in dump) | Conf. |
|---|---|---|---|
| 1 | Two-stage session: login **gateway** then **game server**, with server switching | `KGatewayClient::*`, `KPlayerClient::GetGSIP`, `OnSwitchGS`, `OnSwitchMap` (`Logic 0x0077...`) | HIGH |
| 2 | Handshake carries **RoleID + last-received serial + count** and server answers with **recover flag + reconnect timeout** | `[KPlayerClient] DoHandshakeRequest RoleID:%u, RecvSerial:%d, Count:%d` (Logic `0x0076C210`), `OnHandShakeRespond bRecover=%d, ServerName=%s, ReconnectTimeout=%d` (Logic `0x0077B410`) | HIGH |
| 3 | Liveness = **ping interval × 4** since last received packet; explicit reconnect trigger | `dwNowTime < m_dwLastRecvPackTime + cdwPingInterval * 4` (Logic `0x0076C098`), `TriggerReconnect` (Logic `0x0076BFB8`), `GetPingValue`, `DoUploadPingStatData` | HIGH |
| 4 | **ID-based fixed-size binary protocol**: every protocol ID has a table size, packets validated for exact length; direction split `Do*`/`On*`; per-domain ranges | `uDataLen == m_nProtocolSize[nProtocolID]` (Logic `0x00793198`, `0x00795258`, `0x007A25A0`), ranges `nap`, `pfp_s2c`, `grp_s2c`, `hpc`, `emgp_s2c`, `wpspt`, `vmpc`, `vr2c`, `cs2c`, `eccrq`, `apc`, `spc`, `evp` (Logic `0x007...`) | HIGH |
| 5 | Typed little-endian serialization helpers (no protobuf on this path) | `CoreNet::KNetBuffer` Read/Write `Int8..UInt64`, `Float`, `Double`, `String` (KBase `0x00286F..0x0029xx`) | HIGH |
| 6 | Packet **compression** with size threshold; **AES128 / simple crypto** available | `NetCompress`, `GetNetCompressThreshold`, `IsOpenNetCompress`, `PackCompressHead`, `CompressPackage`; `KAESCryptor::Encrypt`, `KSimpleCryptor::Encrypt` | MED (game stream usage inferred) |
| 7 | Movement is **state-mask synchronised** and server-adjustable | `OnSyncMoveState`, `OnSyncMoveCtrl`, `OnSyncMoveParam`, `OnAdjustPlayerMove`, `SelfMoveStateMask/SelfBackupMoveStateMask/TargetMoveStateMask`, `ullSelfMoveStateMask`, `dwSelfMoveStateMaskLow/High` (Logic `0x007806A8..0x00784388`) | HIGH |
| 8 | Server can **lock movement control** (stacked counter) | `KPlayer::MoveCtrl`, `KNpc::MoveCtrl`, `KGSO3WorldClientInterface::MoveCtrl`, `nDisableMoveCtrlCounter` (Logic `0x007A58D8`, `0x007DDCD8`) | HIGH |
| 9 | Server **rejects actions based on move state** | `MOVE_STATE_ERROR`, `MOVE_STATE_INVALID`, `YOU_MOVE_STATE_WRONG`, `TARGET_MOVE_STATE_WRONG`, `DST_MOVE_STATE_ERROR`, `ADD_DAMAGE_BY_DST_MOVE_STATE` (Logic `0x007F2770..0x0080BDF0`) | HIGH |
| 10 | **Local vs remote character are separate classes** with **independent interpolation** and position adjustment | `KRLLocalCharacterFrameData::Interpolate`, `KRLRemoteCharacterFrameData::Interpolate`, `KRLRemoteCharacter::UpdatePosition`, `KRLCharacterFrameData::AdjustPosition`, `ConvertFramePosition`, `disable remote character interpolate` (Represent `0x00CCCAC8..0x00CCCC40`, `0x00CB4530`) | HIGH |
| 11 | Character controller feeds **requested vs achieved movement** and trajectory deltas | `inputAchievedRequestedMovement`, `inputCharacterTrajectoryDeltaPos/Quat`, `inputDeltaTime` (SIMWorld `0x0004FBD0..0x0004FCB8`) | MED-HIGH |
| 12 | Skill lifecycle is **server-arbitrated**, client gets prepare/cast/channel/effect/back events | `OnSkillPrepare`, `OnSkillCast`, `OnSkillChannel`, `OnSkillEffectResult`, `OnSkillBeatBack`, `OnSkillRayEffect`, `OnSkillChainEffect` (`symbols_KPlayerClient.txt`) | HIGH |
| 13 | Cooldowns are server-owned; server can reset/pause/accelerate them | `OnResetCooldown`, `OnPauseCDTimer`, `OnAccelerateCDTimer`, `OnCoolDownOverDraftNotify`, `s2c_reset_cooldown`, `s2c_pause_cd_timer`, `s2c_accelerate_cd_timer` (Logic `0x007B2938..0x007B2B70`) | HIGH |
| 14 | Periodic sync scheduler + **configurable player sync frame interval** | `DoRoutineSync`, `AddNextSyncTime`, `NeedSync`, `SetPlayerSyncFrameInterval` + `LuaSetPlayerSyncFrameInterval` (Logic `0x007C0AC0`, `0x007C7688`) | HIGH |
| 15 | Representation **reacts to reconnect** (re-sync/rebuild path) | `KREPRESENT_EVENT_RECONNECTED`, `krlEventAdaptor::HandleReconnected` (Represent `0x00CFF1F0`, `0x00CC8030`) | HIGH |
| 16 | Protocol **recorder/replayer** exists (dev/test facility) | `KProtocolRecorder::Push`, `KProtocolReplayer::Pop/Cache`, `UIProtocolRecorder/Replayer` (Logic `0x007AF018..0x007AF158`, `0x009BC038..`) | HIGH |
| 17 | Dedicated network thread in the player session client | `KPlayerClient::ThreadFunction`, `Send`, `RealSend` (`symbols_KPlayerClient.txt`) | HIGH |
| 18 | `CoreNet` + crypto are **exported library APIs**, not just internal | `exports_KBaseX64.txt` (2,022 exports): `KNetDelegate/KNetSocket/KNetBuffer/KNetInetAddress` ctor/dtor/methods, `KAESCryptor::Encrypt/Decrypt`, `KSimpleCryptor::Encrypt/Decrypt`, `KGGetNetworkState`, timers/threads | HIGH |
| 19 | `KSimpleCryptor` is a fast custom cipher: bit rotation + 16-byte group exchange | `cycleShiftLeft/Right`, `exchangeGroupBy16Byte`, `getBitMask@KSimpleCryptor`, `Simple_Encrypt` (KBase exports) | MED (usage on game stream inferred) |

---

## 4. Model interpretation (labeled — not a claim)

Combining the above, the JX3 client-server model is the **classic server-authoritative MMO**:

1. **Login:** client → gateway (account verify, role list, `OnSyncLoginKey`) → game server `Connect`, with serial-based resume on reconnect (`RecvSerial`).
2. **Own movement:** client runs its own character locally (`KRLLocalCharacter`, `MoveCtrl`, physics CCT with requested-vs-achieved movement) while sending action/intent messages; the server periodically answers `OnSyncMoveState/MoveCtrl/MoveParam` plus `OnAdjustPlayerMove` corrections. The `Self*Backup*` masks look like the last server-acked state kept for reconciliation.
3. **Other actors:** replicated by `OnSyncEntity/OnSyncNewPlayer/OnSyncNewNpc`, rendered by `KRLRemoteCharacter` with its own `Interpolate` (and a runtime "disable remote character interpolate" toggle).
4. **Combat:** `OnSkillPrepare/Cast/Channel/EffectResult` — the server arbitrates; cooldown timers are pushed/paused/reset by server messages; action attempts can be rejected with `MOVE_STATE_*` errors.
5. **Scheduling:** `RoutineSync` + `SetPlayerSyncFrameInterval` control snapshot cadence; `NetCompress` with threshold controls bandwidth.
6. **Liveness:** ping interval, 4× ping timeout, `TriggerReconnect`, recoverable handshake.

Not lockstep, not client-authoritative, not peer-to-peer. No protobuf on the game path (fixed-size binary via `KNetBuffer`).

---

## 5. Unknowns (need live capture — out of static scope)

| Question | Why static can't answer |
|---|---|
| ~~Actual tick rates: ping interval, disconnect timeout~~ **SOLVED**: ping 3000 ms, dead timeout 12000 ms (see §9) | — |
| Routine sync interval and sync-frame defaults | set at runtime via Lua; not literal strings |
| Whether own-movement uses full rollback-replay or simpler smoothing | Needs behavior test with added latency |
| Whether gameplay stream is AES-encrypted, compressed, or both, and in which order | Code paths, not strings |
| Server-side process topology (gateway/GS/room/battle servers — `R2C`/`L2C` message prefixes hint tiers) | No server binaries installed locally |
| Snapshot content per protocol ID (field layout) | Protocol table is compiled data; names only appear in asserts |

---

## 6. What this means for reborn (draft, to expand into REBORN_SERVER_SPEC.md)

- Server of record for: session/handshake, actor spawn/despawn, move-state corrections, skill arbitration, cooldown authority, buff/state lists.
- Client of record for: input, own-actor simulation, animation, remote-actor interpolation.
- Message model: ID-based fixed-size packets over one ordered connection per tier, with typed primitives (`KNetBuffer` equivalent), optional compression threshold, optional AES.
- Reconnect must be first-class: serial-based resume with recover flag and a reconnect timeout (JX3 ships it; consumers expect it).
- Do not copy: the wire format, encryption keys, or exact opcodes — only the architecture.

---

## 7. Reproduce

```powershell
# system python is fine for string scans; .venv has pefile/capstone
$bin = 'C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64'

python tools\netcode\scan_net_strings.py "$bin\JX3LogicEditOperationX64.dll" --out proof\netcode\JX3LogicEditOperation_net_strings.txt
python tools\netcode\scan_net_strings.py "$bin\KBaseX64.dll"               --out proof\netcode\KBaseX64_net_strings.txt
python tools\netcode\scan_net_strings.py "$bin\JX3RepresentX64.dll"       --out proof\netcode\JX3RepresentX64_net_strings.txt
python tools\netcode\scan_net_strings.py "$bin\SIMWorldX64.dll"           --out proof\netcode\SIMWorldX64_net_strings.txt
python tools\netcode\scan_net_strings.py "$bin\JX3ClientX64.exe"          --out proof\netcode\JX3ClientX64_exe_net_strings.txt

.\.venv\Scripts\python.exe tools\netcode\find_net_modules.py "$bin" --out proof\netcode\net_import_modules.txt
.\.venv\Scripts\python.exe tools\netcode\pe_imports.py "$bin\KBaseX64.dll" --functions -o proof\netcode\imports_KBaseX64.txt
```

## 8. Next static steps (if continued)

1. ~~Scan `JX3UIX64.dll`~~ done — no sync/compress/ping setters surfaced (`JX3UIX64_net_strings.txt`).
2. ~~Export table of `KBaseX64.dll`~~ done — see finding 18 (`exports_KBaseX64.txt`).
3. UI addon Lua (303 files under `interface\`) contains no net/sync config strings — runtime defaults likely live in PakV4-packed UI scripts or compiled logic; defer.
4. Locate callers of `SetPlayerSyncFrameInterval` / `SetNetCompressThreshold` in `JX3ClientX64.exe` disassembly to recover default values (capstone is available).
5. Disassemble `KPlayerClient::DoRoutineSync` scheduling to recover the default routine interval.

---

## 9. Disassembly addendum (second static pass)

Full detail lives in **`docs/netcode/JX3_PROTOCOL_SPEC.md`**; raw transcripts in
`proof/netcode/disasm/` (`handshake.txt`, `routine_sync.txt`, `sendpath.txt`,
`threadfn_tail.txt`). New HIGH-confidence findings:

| # | Finding | Evidence |
|---|---|---|
| 20 | **Frame prefix**: base **11 B** = `u16 id; u8 flags; u16 send_seq; u16 ack_seq; u32 field7`; protocols that need it add `u32 param` at `+0xB` (15 B total), tail from `+0xF` | handshake `0x1801AD161`–`0x1801AD18C`; routine sync `0x18017862F`; ping `0x1801AD990`; 11-B examples: proto 2 `0x180170E60`, proto 0x164 `0x1801A9503` |
| 21 | **Reliability layer**: 2048-slot unconfirmed ring (`this+0xE448`, 0x800 mask), cumulative ack via frame `+0x5`, retransmit with `flags |= 3`, "Unconfirm send buffer full!" | `0x1801AD5BD`–`0x1801AD6E2`, `0x1801AD49E` |
| 22 | **Ping = protocol 6 every 3000 ms** (`0xBB8`), payload = u32 tick at `+0xB`, 15 bytes | `0x1801AD95B`, `0x1801AD990` |
| 23 | **Dead timeout = 12000 ms** (`0x2EE0`), i.e. ping interval × 4 as the assert states | `0x1801AD948`, `0x1801ADB3A` |
| 24 | **Handshake = protocol 1**: RoleID at `+0xB`, 16-byte session blob at `+0xF`, resume bool at `+0x1F`, resume serial at `+5`; response carries `bRecover` + `ReconnectTimeout` | `0x1801AD165`–`0x1801AD18C` |
| 25 | **Routine sync = protocol 0x6E**: `u32 param @+0xB`, `u16 size @+0xF`, payload `@+0x11`, max frame 0x8000 | `0x18017862F`–`0x180178660` |
| 26 | **Ack/window control protocol = 0x2FE**, handled before retransmit loop | `0x1801AD643`, `0x1801AD670` |
| 27 | **Net thread = `NetToGSThread`**, separate from logic; received buffers published to a 20-bitsequence ring at `this+0x18CB0` | `0x1801AD0F7`, `0x1801AD827`–`0x1801AD88B` |
| 28 | Socket stream vtable: `+0x30` send, `+0x38` readable, `+0x40` recv, `+0x68` GetLastError | `0x1801AC8B0`, `0x1801AD566`, `0x1801AD588` |

**C2S protocol catalog:** `proof/netcode/c2s_protocol_catalog.tsv` (429 rows, heuristic
attribution — only the four IDs above are HIGH). Built by `tools/netcode/dump_protocol_ids.py`.

**Recreation artifacts added in this pass:**

- `docs/netcode/JX3_PROTOCOL_SPEC.md` — full wire/session spec with addresses.
- `docs/netcode/REBORN_SERVER_SPEC.md` — implementable server+client contract (our own protocol).
- `tools/netcode/reference/jx3_model.py` — runnable reference server+client implementing the
  model: 10/10 smoke checks pass (handshake, prediction/reconciliation, AOI, skill lifecycle,
  move-state reject, retransmit recovery, reconnect resume).
