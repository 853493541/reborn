#!/usr/bin/env python3
"""Reference server + client for the reborn netcode model (see docs/netcode/REBORN_SERVER_SPEC.md).

Single-file, stdlib-only, asyncio. Implements:
  - 15-byte framed protocol with size-prefixed JSON payloads
  - serial/ack reliability with retransmit and duplicate suppression
  - handshake with session key + serial resume, ping/dead timeout
  - 30 Hz world tick, 10 Hz move state/snapshots, client prediction + reconciliation
  - server-authoritative skill cast with movement-state rejection and cooldowns
  - distance-based interest management

Run:  python tools/netcode/reference/jx3_model.py
"""
from __future__ import annotations

import asyncio
import json
import math
import struct
import sys
import time
from dataclasses import dataclass, field

OP_HANDSHAKE = 0x0001
OP_HANDSHAKE_RESULT = 0x0002
OP_PING = 0x0006
OP_PONG = 0x0007
OP_ACK = 0x0008
OP_JOIN_WORLD = 0x0010
OP_MOVE_INPUT = 0x0020
OP_MOVE_STATE = 0x0021
OP_MOVE_CORRECTION = 0x0022
OP_MOVE_CTRL = 0x0023
OP_ENTITY_ADD = 0x0030
OP_ENTITY_REMOVE = 0x0031
OP_ENTITY_SNAPSHOT = 0x0032
OP_CAST_SKILL = 0x0040
OP_SKILL_PREPARE = 0x0041
OP_SKILL_CAST = 0x0042
OP_SKILL_EFFECT = 0x0043
OP_SKILL_REJECT = 0x0044
OP_COOLDOWN = 0x0045
OP_BUFF_SYNC = 0x0046
OP_ROUTINE_SYNC = 0x006E

FLAG_RETRANSMIT = 0x01
FLAG_HAS_ACK = 0x02

HEADER = struct.Struct("<HBHHII")
MAX_PAYLOAD = 0x8000
TICK_HZ = 30.0
SNAPSHOT_EVERY = 3
PING_MS = 3000.0
DEAD_TIMEOUT_MS = 12000.0
RECONNECT_MS = 60000.0
RTO_MS = 250.0
MAX_TRIES = 8
WINDOW = 2048
AOI_RANGE = 100.0
MOVE_SPEED = 5.0
K_FWD, K_BACK, K_LEFT, K_RIGHT = 1, 2, 4, 8

REJECT_MOVE_STATE = 1
REJECT_COOLDOWN = 2
REJECT_RANGE = 3

SKILLS = {
    1: {"cast_ms": 400, "cd_ms": 2000, "move_forbidden": True, "range": 6.0},
    2: {"cast_ms": 200, "cd_ms": 800, "move_forbidden": False, "range": 6.0},
}


def now() -> float:
    return time.monotonic()


def apply_input(pos: tuple[float, float, float], keys: int, dt: float) -> tuple[float, float, float]:
    dx = (1 if keys & K_RIGHT else 0) - (1 if keys & K_LEFT else 0)
    dz = (1 if keys & K_FWD else 0) - (1 if keys & K_BACK else 0)
    n = math.hypot(dx, dz)
    if not n:
        return pos
    return (pos[0] + dx / n * MOVE_SPEED * dt, pos[1], pos[2] + dz / n * MOVE_SPEED * dt)


def dist(a, b) -> float:
    return math.dist(a, b)


def encode(op: int, seq: int, ack: int, param: int, payload: bytes = b"", flags: int = 0, sid: int = 0) -> bytes:
    return HEADER.pack(op, flags, seq & 0xFFFF, ack & 0xFFFF, sid & 0xFFFFFFFF, param & 0xFFFFFFFF) + struct.pack("<H", len(payload)) + payload


def decode(buf: bytes):
    op, flags, seq, ack, sid, param = HEADER.unpack_from(buf, 0)
    (size,) = struct.unpack_from("<H", buf, HEADER.size)
    payload = buf[HEADER.size + 2: HEADER.size + 2 + size]
    return op, flags, seq, ack, sid, param, payload


class ReliableChannel:
    """Serial/ack window with retransmit and duplicate suppression."""

    def __init__(self) -> None:
        self.send_seq = 0
        self.recv_ack = 0
        self.unacked: dict[int, list] = {}
        self.seen: set[int] = set()
        self.drop_once: set[int] = set()
        self.retransmits = 0

    def build(self, op: int, param: int = 0, payload: bytes = b"", sid: int = 0) -> bytes:
        seq = self.send_seq
        self.send_seq = (self.send_seq + 1) & 0xFFFF
        flags = FLAG_HAS_ACK
        frame = encode(op, seq, self.recv_ack, param, payload, flags, sid)
        self.unacked[seq] = [frame, now(), 0]
        if op in self.drop_once:
            self.drop_once.discard(op)
            return b""
        return frame

    def on_frame(self, seq: int, ack: int) -> bool:
        self.recv_ack = seq
        for s in [s for s in self.unacked if s <= ack]:
            del self.unacked[s]
        if seq in self.seen:
            return False
        self.seen.add(seq)
        if len(self.seen) > 4096:
            self.seen = {s for s in self.seen if s > seq - 2048}
        return True

    def due(self) -> list[bytes]:
        out = []
        t = now()
        for seq, rec in list(self.unacked.items()):
            if (t - rec[1]) * 1000.0 >= RTO_MS and rec[2] < MAX_TRIES:
                rec[1] = t
                rec[2] += 1
                op, flags, s, ack, sid, param = HEADER.unpack_from(rec[0], 0)
                out.append(encode(op, s, self.recv_ack, param, rec[0][HEADER.size + 2:], flags | FLAG_RETRANSMIT, sid))
                self.retransmits += 1
        return out


@dataclass
class Entity:
    eid: int
    name: str
    pos: tuple[float, float, float] = (0.0, 0.0, 0.0)
    vel: tuple[float, float, float] = (0.0, 0.0, 0.0)
    keys: int = 0
    facing: int = 0
    last_input_seq: int = 0
    ctrl_lock_until: float = 0.0
    casting_until: float = 0.0
    cooldowns: dict[int, float] = field(default_factory=dict)
    buffs: list[int] = field(default_factory=list)
    hp: int = 100

    def moving(self) -> bool:
        return self.keys != 0

    def locked(self) -> bool:
        return now() < self.ctrl_lock_until


@dataclass
class SessionState:
    key: bytes
    entity: Entity
    resume_seq: int
    expires: float


class GameServer:
    def __init__(self, ping_ms: float = PING_MS, dead_ms: float = DEAD_TIMEOUT_MS) -> None:
        self.entities: dict[int, Entity] = {}
        self.sessions: dict[bytes, SessionState] = {}
        self.peers: set["Peer"] = set()
        self.next_eid = 1
        self.ping_ms = ping_ms
        self.dead_ms = dead_ms
        self.server_tick = 0
        self._server = None
        self._task = None

    async def start(self, host: str = "127.0.0.1", port: int = 0) -> int:
        self._server = await asyncio.start_server(self._handle, host, port)
        self._task = asyncio.create_task(self._tick())
        return self._server.sockets[0].getsockname()[1]

    async def stop(self) -> None:
        if self._task:
            self._task.cancel()
        if self._server:
            self._server.close()
            await self._server.wait_closed()

    async def _handle(self, reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
        peer = Peer(self, reader, writer)
        self.peers.add(peer)
        try:
            await peer.run()
        finally:
            self.peers.discard(peer)
            if peer.entity and peer.session_key:
                st = self.sessions.get(peer.session_key)
                if st:
                    st.resume_seq = peer.channel.recv_ack
                    st.expires = now() + RECONNECT_MS / 1000.0
            writer.close()

    def spawn(self, name: str) -> Entity:
        eid = self.next_eid
        self.next_eid += 1
        ent = Entity(eid=eid, name=name)
        self.entities[eid] = ent
        return ent

    async def _tick(self) -> None:
        dt = 1.0 / TICK_HZ
        n = 0
        while True:
            await asyncio.sleep(dt)
            self.server_tick += 1
            n += 1
            for ent in self.entities.values():
                if ent.locked() or now() < ent.casting_until:
                    ent.vel = (0.0, 0.0, 0.0)
                    continue
                old = ent.pos
                ent.pos = apply_input(ent.pos, ent.keys, dt)
                ent.vel = ((ent.pos[0] - old[0]) / dt, 0.0, (ent.pos[2] - old[2]) / dt)
                if ent.pos[1] < 0.0:
                    ent.pos = (ent.pos[0], 0.0, ent.pos[2])
            if n % SNAPSHOT_EVERY:
                continue
            for peer in self.peers:
                await peer.push_state()


class Peer:
    def __init__(self, server: GameServer, reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
        self.server = server
        self.reader = reader
        self.writer = writer
        self.channel = ReliableChannel()
        self.entity: Entity | None = None
        self.session_key: bytes | None = None
        self.last_recv = now()
        self.known: set[int] = set()
        self.task = asyncio.create_task(self._ticker())

    async def _ticker(self) -> None:
        try:
            while True:
                await asyncio.sleep(0.05)
                if self.entity and now() - self.last_recv > self.server.dead_ms / 1000.0:
                    self.writer.close()
                    return
                for frame in self.channel.due():
                    self.writer.write(frame)
                await self.writer.drain()
        except (asyncio.CancelledError, ConnectionError):
            return

    async def send(self, op: int, param: int = 0, payload: dict | list | None = None) -> None:
        data = b"" if payload is None else json.dumps(payload, separators=(",", ":")).encode()
        frame = self.channel.build(op, param, data)
        if frame:
            self.writer.write(frame)
            await self.writer.drain()

    async def run(self) -> None:
        try:
            while True:
                try:
                    head = await self.reader.readexactly(HEADER.size)
                    (size,) = struct.unpack("<H", await self.reader.readexactly(2))
                    body = await self.reader.readexactly(size) if size else b""
                except (asyncio.IncompleteReadError, ConnectionError, asyncio.CancelledError):
                    return
                op, flags, seq, ack, sid, param, payload = decode(head + struct.pack("<H", size) + body)
                self.last_recv = now()
                if not self.channel.on_frame(seq, ack):
                    continue
                await self.dispatch(op, param, payload)
        finally:
            self.task.cancel()

    async def dispatch(self, op: int, param: int, payload: bytes) -> None:
        if op == OP_HANDSHAKE:
            key, resume_seq, resume = struct.unpack("<16sHB", payload[:19])
            self.session_key = key
            st = self.server.sessions.get(key)
            if st and resume and now() < st.expires:
                self.entity = st.entity
                st.expires = now() + RECONNECT_MS / 1000.0
                recovered = 1
            else:
                self.entity = self.server.spawn(key.hex()[:8])
                self.server.sessions[key] = SessionState(key, self.entity, 0, now() + RECONNECT_MS / 1000.0)
                recovered = 0
            await self.send(OP_HANDSHAKE_RESULT, recovered)
            await self.send(OP_JOIN_WORLD, self.entity.eid, {
                "eid": self.entity.eid,
                "pos": list(self.entity.pos),
                "hp": self.entity.hp,
                "server_tick": self.server.server_tick,
            })
            self.known = set()
        elif op == OP_PING:
            await self.send(OP_PONG, int(param))
        elif op == OP_MOVE_INPUT:
            if not self.entity:
                return
            data = json.loads(payload) if payload else {}
            self.entity.keys = int(data.get("keys", 0))
            self.entity.facing = int(data.get("facing", 0))
            self.entity.last_input_seq = int(param)
        elif op == OP_CAST_SKILL:
            await self._cast(json.loads(payload) if payload else {})
        elif op == OP_ACK:
            pass

    async def _cast(self, msg: dict) -> None:
        ent = self.entity
        if not ent:
            return
        skill_id = int(msg.get("skill_id", 0))
        spec = SKILLS.get(skill_id)
        if not spec:
            await self.send(OP_SKILL_REJECT, REJECT_RANGE, {"reason": "unknown"})
            return
        if spec["move_forbidden"] and ent.moving():
            await self.send(OP_SKILL_REJECT, REJECT_MOVE_STATE, {"reason": "move_state"})
            return
        ready = ent.cooldowns.get(skill_id, 0.0)
        if now() < ready:
            await self.send(OP_SKILL_REJECT, REJECT_COOLDOWN, {"reason": "cooldown"})
            return
        target = int(msg.get("target", 0))
        tgt = self.server.entities.get(target)
        if tgt is not None and dist(ent.pos, tgt.pos) > spec["range"]:
            await self.send(OP_SKILL_REJECT, REJECT_RANGE, {"reason": "range"})
            return
        cast_s = spec["cast_ms"] / 1000.0
        ent.casting_until = now() + cast_s
        if spec["move_forbidden"]:
            ent.ctrl_lock_until = ent.casting_until
            await self.send(OP_MOVE_CTRL, 1, {"locked": True, "duration_ms": spec["cast_ms"]})
        cast_id = self.server.server_tick
        await self.send(OP_SKILL_PREPARE, cast_id, {"skill_id": skill_id, "cast_ms": spec["cast_ms"]})

        async def finish() -> None:
            await asyncio.sleep(cast_s)
            ent.casting_until = 0.0
            ent.ctrl_lock_until = 0.0
            ent.cooldowns[skill_id] = now() + spec["cd_ms"] / 1000.0
            await self.send(OP_MOVE_CTRL, 0, {"locked": False, "duration_ms": 0})
            await self.send(OP_SKILL_CAST, cast_id, {"skill_id": skill_id})
            await self.send(OP_SKILL_EFFECT, cast_id, {
                "skill_id": skill_id,
                "targets": [] if tgt is None else [{"eid": tgt.eid, "damage": 10}],
            })
            await self.send(OP_COOLDOWN, skill_id, {"skill_id": skill_id, "ready_at": ent.cooldowns[skill_id]})

        asyncio.create_task(finish())

    async def push_state(self) -> None:
        ent = self.entity
        if not ent:
            return
        await self.send(OP_MOVE_STATE, ent.last_input_seq, {
            "pos": list(ent.pos),
            "vel": list(ent.vel),
            "flags": 1 if ent.locked() else 0,
            "server_tick": self.server.server_tick,
        })
        visible = [e for e in self.server.entities.values()
                   if e.eid != ent.eid and dist(e.pos, ent.pos) <= AOI_RANGE]
        current = {e.eid for e in visible}
        for eid in self.known - current:
            await self.send(OP_ENTITY_REMOVE, eid, {"eid": eid})
        for e in visible:
            if e.eid not in self.known:
                await self.send(OP_ENTITY_ADD, e.eid, {"eid": e.eid, "name": e.name, "pos": list(e.pos), "hp": e.hp})
        self.known = current
        if visible:
            await self.send(OP_ENTITY_SNAPSHOT, 0, {"entities": [
                {"eid": e.eid, "pos": list(e.pos), "facing": e.facing, "hp": e.hp} for e in visible]})


class GameClient:
    def __init__(self, name: str) -> None:
        self.name = name
        self.channel = ReliableChannel()
        self.reader = None
        self.writer = None
        self.pos = (0.0, 0.0, 0.0)
        self.server_pos = (0.0, 0.0, 0.0)
        self.eid = 0
        self.input_seq = 0
        self.keys = 0
        self.history: list[tuple[int, int, float]] = []
        self.events: asyncio.Queue = asyncio.Queue()
        self.remote: dict[int, dict] = {}
        self.last_state_tick = 0
        self.recovered = 0
        self.session_key = b"\x00" * 16
        self.resume_seq = 0
        self._task = None

    async def connect(self, port: int, key: bytes, resume: bool = False) -> None:
        self.session_key = key
        self.reader, self.writer = await asyncio.open_connection("127.0.0.1", port)
        self._task = asyncio.create_task(self._run())
        self._task.add_done_callback(self._on_task_done)
        payload = struct.pack("<16sHB", key, self.resume_seq, 1 if resume else 0)
        frame = self.channel.build(OP_HANDSHAKE, 0, payload)
        self.writer.write(frame)
        await self.writer.drain()

    @staticmethod
    def _on_task_done(task: asyncio.Task) -> None:
        if task.cancelled():
            return
        exc = task.exception()
        if exc is not None:
            print(f"client receive task crashed: {exc!r}")

    async def close(self) -> None:
        if self._task:
            self._task.cancel()
        if self.writer:
            self.writer.close()

    async def _run(self) -> None:
        while True:
            try:
                head = await self.reader.readexactly(HEADER.size)
                (size,) = struct.unpack("<H", await self.reader.readexactly(2))
                body = await self.reader.readexactly(size) if size else b""
            except (asyncio.IncompleteReadError, ConnectionError, asyncio.CancelledError):
                return
            op, flags, seq, ack, sid, param, payload = decode(head + struct.pack("<H", size) + body)
            if not self.channel.on_frame(seq, ack):
                continue
            await self._dispatch(op, param, payload)
            for frame in self.channel.due():
                self.writer.write(frame)
            await self.writer.drain()

    async def _dispatch(self, op: int, param: int, payload: bytes) -> None:
        msg = json.loads(payload) if payload else {}
        if op == OP_HANDSHAKE_RESULT:
            self.recovered = param
            await self.events.put(("handshake", self.recovered))
        elif op == OP_JOIN_WORLD:
            self.eid = int(param)
            self.pos = tuple(msg["pos"])
            self.server_pos = self.pos
            await self.events.put(("join", msg))
        elif op == OP_MOVE_STATE:
            self.server_pos = tuple(msg["pos"])
            last = int(param)
            self.last_state_tick = msg.get("server_tick", 0)
            remaining = [(s, k, dt) for (s, k, dt) in self.history if s > last]
            pos = self.server_pos
            for _, k, dt in remaining:
                pos = apply_input(pos, k, dt)
            drift = dist(pos, self.pos)
            self.history = remaining
            self.pos = pos
            if drift > 0.5:
                await self.events.put(("correction", drift))
        elif op == OP_MOVE_CTRL:
            await self.events.put(("move_ctrl", msg))
        elif op == OP_ENTITY_ADD:
            self.remote[int(param)] = msg
            await self.events.put(("entity_add", msg))
        elif op == OP_ENTITY_REMOVE:
            self.remote.pop(int(param), None)
            await self.events.put(("entity_remove", msg))
        elif op == OP_ENTITY_SNAPSHOT:
            for rec in msg.get("entities", []):
                self.remote[rec["eid"]] = rec
        elif op in (OP_SKILL_PREPARE, OP_SKILL_CAST, OP_SKILL_EFFECT, OP_SKILL_REJECT, OP_COOLDOWN):
            await self.events.put(("skill", (op, msg)))
        elif op == OP_PONG:
            await self.events.put(("pong", msg))

    def move(self, keys: int, dt: float = 1.0 / TICK_HZ) -> None:
        self.keys = keys
        self.input_seq += 1
        self.history.append((self.input_seq, keys, dt))
        self.pos = apply_input(self.pos, keys, dt)
        data = json.dumps({"keys": keys, "facing": 0}, separators=(",", ":")).encode()
        self.writer.write(self.channel.build(OP_MOVE_INPUT, self.input_seq, data))

    def cast(self, skill_id: int, target: int = 0) -> None:
        data = json.dumps({"skill_id": skill_id, "target": target}, separators=(",", ":")).encode()
        frame = self.channel.build(OP_CAST_SKILL, 0, data)
        if frame:
            self.writer.write(frame)

    def ping(self) -> None:
        frame = self.channel.build(OP_PING, int(now() * 1000) & 0xFFFFFFFF)
        if frame:
            self.writer.write(frame)

    async def wait(self, kind: str, timeout: float = 3.0):
        t0 = now()
        while True:
            left = timeout - (now() - t0)
            if left <= 0:
                raise asyncio.TimeoutError(f"waiting for {kind}")
            k, payload = await asyncio.wait_for(self.events.get(), left)
            if k == kind:
                return payload


async def _flush(seconds: float = 0.1) -> None:
    await asyncio.sleep(seconds)


async def smoke() -> int:
    ok = True

    def check(name: str, cond: bool, detail: str = "") -> None:
        nonlocal ok
        print(f"{'PASS' if cond else 'FAIL'}: {name}{(' - ' + detail) if detail else ''}")
        ok = ok and cond

    server = GameServer()
    port = await server.start()
    key1 = b"k" * 16

    c1 = GameClient("c1")
    await c1.connect(port, key1)
    recovered = await c1.wait("handshake")
    join = await c1.wait("join")
    check("handshake new session", recovered == 0, f"eid={c1.eid}")
    check("join world spawns entity", c1.eid > 0 and "pos" in join)

    for _ in range(30):
        c1.move(K_FWD)
        await _flush(1.0 / TICK_HZ)
    c1.move(0)
    await _flush(0.4)
    drift = dist(c1.pos, c1.server_pos)
    check("prediction converges after reconciling", drift < 0.6, f"drift={drift:.3f} m, pos={c1.server_pos}")
    check("moved forward", c1.server_pos[2] > 3.0, f"z={c1.server_pos[2]:.2f}")

    c2 = GameClient("c2")
    await c2.connect(port, b"j" * 16)
    await c2.wait("handshake")
    await c2.wait("join")
    add1 = await c1.wait("entity_add", timeout=1.0)
    add2 = await c2.wait("entity_add", timeout=1.0)
    check("aoi entity add both ways", add1["eid"] == c2.eid and add2["eid"] == c1.eid)

    c1.cast(2)
    prepare = await c1.wait("skill")
    cast_ev = await c1.wait("skill")
    effect = await c1.wait("skill")
    cd = await c1.wait("skill")
    check("skill lifecycle prepare/cast/effect", (prepare[0], cast_ev[0], effect[0], cd[0]) ==
          (OP_SKILL_PREPARE, OP_SKILL_CAST, OP_SKILL_EFFECT, OP_COOLDOWN))

    c1.move(K_FWD)
    await _flush(0.05)
    c1.cast(1)
    reject = None
    t0 = now()
    while now() - t0 < 1.0:
        k, msg = await c1.wait("skill", timeout=1.0)
        if k == OP_SKILL_REJECT:
            reject = msg
            break
    check("moving cast rejected with move_state", reject is not None and reject.get("reason") == "move_state")

    c1.channel.drop_once.add(OP_CAST_SKILL)
    c1.move(0)
    await _flush(1.0)
    c1.cast(2)
    retried = False
    t0 = now()
    while now() - t0 < 2.0 and not retried:
        try:
            k, msg = await c1.wait("skill", timeout=0.5)
        except asyncio.TimeoutError:
            continue
        if k == OP_SKILL_EFFECT:
            retried = True
    check("dropped cast recovered by retransmit", retried and c1.channel.retransmits > 0,
          f"retried={retried} retransmits={c1.channel.retransmits} unacked={len(c1.channel.unacked)}")

    pos_before = c1.server_pos
    resume_seq = c1.channel.recv_ack
    c1.resume_seq = resume_seq
    await c1.close()
    await _flush(0.3)
    c1b = GameClient("c1b")
    await c1b.connect(port, key1, resume=True)
    recovered2 = await c1b.wait("handshake")
    await c1b.wait("join")
    check("reconnect recovers session", recovered2 == 1)
    check("resumed entity keeps position", dist(c1b.server_pos, pos_before) < 0.01,
          f"pos={c1b.server_pos}")

    await c1b.close()
    await c2.close()
    await server.stop()
    await _flush(0.2)
    return 0 if ok else 1


if __name__ == "__main__":
    try:
        sys.exit(asyncio.run(smoke()))
    except KeyboardInterrupt:
        sys.exit(130)
