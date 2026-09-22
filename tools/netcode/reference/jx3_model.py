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
import random
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
OP_DOODAD_ADD = 0x0050
OP_DOODAD_REMOVE = 0x0051
OP_DOODAD_STATE = 0x0052
OP_LOOT_OPEN = 0x0053
OP_LOOT_LIST = 0x0054
OP_LOOT_TAKE = 0x0055
OP_LOOT_RESULT = 0x0056
OP_ITEM_ADD = 0x0057
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

# ---------------------------------------------------------------- loot rules
# Mirrors the JX3 shape observed statically:
#   - fixed anchor set per map, only a subset active per match (semi-random)
#   - tiered containers (1st/2nd/3rd tier gear, weapon cases, meds, supply, secret)
#   - rich zone in the centre, poor ring outside; hotspot clusters
#   - wind-up before the loot list arrives (OpenPrepareFrame analog)
#   - per-container weighted drop table (contents rolled on open)
#   - container removed when emptied, respawns on a timer (DoodadReviveDelay analog)

LOOT_RANGE = 5.0
LOOT_ANCHOR_COUNT = 48
LOOT_ACTIVE_RATIO = 0.55
LOOT_JITTER = 6.0
LOOT_EXTENT = 120.0
LOOT_HOTSPOTS = 6

CONTAINERS = {
    1: {"name": "一阶装备", "tier": 1, "prepare_ms": 240, "respawn_s": 25.0, "table": "eq1"},
    2: {"name": "二阶装备", "tier": 2, "prepare_ms": 240, "respawn_s": 45.0, "table": "eq2"},
    3: {"name": "三阶装备", "tier": 3, "prepare_ms": 400, "respawn_s": 70.0, "table": "eq3"},
    4: {"name": "武器匣", "tier": 2, "prepare_ms": 240, "respawn_s": 45.0, "table": "wp2"},
    5: {"name": "药品囊", "tier": 1, "prepare_ms": 180, "respawn_s": 20.0, "table": "med"},
    6: {"name": "补给箱", "tier": 2, "prepare_ms": 500, "respawn_s": 60.0, "table": "supply"},
    7: {"name": "秘宝匣", "tier": 3, "prepare_ms": 700, "respawn_s": 90.0, "table": "secret"},
}

# table entries: (name, rarity, weight, min_count, max_count)
LOOT_TABLES = {
    "eq1": [("布甲护腕", "common", 100, 1, 1), ("皮甲护腕", "common", 80, 1, 1),
            ("青铜重靴", "common", 70, 1, 1), ("粗铁剑", "common", 60, 1, 1),
            ("轻功残页", "rare", 8, 1, 1)],
    "eq2": [("精铁护腕", "rare", 90, 1, 1), ("玄铁重靴", "rare", 80, 1, 1),
            ("百炼剑", "rare", 70, 1, 1), ("回气散", "rare", 50, 1, 2),
            ("绝境秘卷", "epic", 8, 1, 1)],
    "eq3": [("寒月护腕", "epic", 80, 1, 1), ("赤霄战靴", "epic", 70, 1, 1),
            ("龙渊剑", "epic", 60, 1, 1), ("天阶秘匣", "legendary", 10, 1, 1)],
    "wp2": [("连环弩", "rare", 80, 1, 1), ("破军长枪", "rare", 70, 1, 1),
            ("风雷双刃", "epic", 20, 1, 1)],
    "med": [("金疮药", "common", 100, 1, 3), ("止血草", "common", 90, 1, 3),
            ("麻布绷带", "common", 90, 1, 2), ("行气散", "rare", 30, 1, 1)],
    "supply": [("金疮药", "common", 80, 2, 4), ("回气散", "rare", 50, 1, 2),
               ("匿踪烟", "rare", 35, 1, 1), ("伪装的秘籍", "common", 40, 1, 1)],
    "secret": [("觅踪窥影烟", "epic", 60, 1, 1), ("流萤魂返丹", "epic", 40, 1, 1),
               ("铁血宝匣", "legendary", 12, 1, 1)],
}

# template weights by zone tier (centre rich, rim poor) and anchor hotspot flag
ZONE_WEIGHTS = {
    3: {1: 10, 2: 30, 3: 40, 4: 25, 5: 5, 6: 30, 7: 12},
    2: {1: 35, 2: 35, 3: 12, 4: 25, 5: 20, 6: 15, 7: 4},
    1: {1: 60, 2: 18, 3: 3, 4: 12, 5: 30, 6: 6, 7: 1},
}
HOTSPOT_BIAS = {1: 0.5, 2: 1.2, 3: 2.5, 4: 1.5, 5: 0.7, 6: 1.2, 7: 0.5}


def zone_of(x: float, z: float, extent: float = LOOT_EXTENT) -> int:
    d = math.hypot(x, z) / extent
    if d < 0.35:
        return 3
    if d < 0.7:
        return 2
    return 1


def build_anchors(map_id: int, count: int = LOOT_ANCHOR_COUNT, seed: int = 0x5EED,
                  extent: float = LOOT_EXTENT, hotspots: int = LOOT_HOTSPOTS):
    rng = random.Random((seed * 1_000_003) ^ (map_id * 7919))
    anchors: list[dict] = []

    def add(x: float, z: float, hotspot: bool) -> None:
        anchors.append({"x": x, "z": z, "zone": zone_of(x, z, extent), "hotspot": hotspot})

    for _ in range(hotspots):
        cx = rng.uniform(-0.75, 0.75) * extent
        cz = rng.uniform(-0.75, 0.75) * extent
        for _ in range(4):
            add(cx + rng.gauss(0, 9), cz + rng.gauss(0, 9), True)
    while len(anchors) < count:
        add(rng.uniform(-extent, extent), rng.uniform(-extent, extent), False)
    return anchors


def roll_template(anchor: dict, phase: int, rng: random.Random) -> int:
    weights = dict(ZONE_WEIGHTS[anchor["zone"]])
    if anchor["hotspot"]:
        for tid in weights:
            weights[tid] *= HOTSPOT_BIAS[tid]
    if phase >= 2:
        weights[2] = weights.get(2, 0) * 1.4
        weights[3] = weights.get(3, 0) * 1.6
    ids = list(weights)
    return rng.choices(ids, weights=[weights[i] for i in ids], k=1)[0]


def roll_contents(table: str, rng: random.Random, rolls: int = 2) -> list[dict]:
    entries = LOOT_TABLES[table]
    names = [e[0] for e in entries]
    weights = [e[2] for e in entries]
    out = []
    for _ in range(rolls):
        pick = rng.choices(range(len(entries)), weights=weights, k=1)[0]
        name, rarity, _w, lo, hi = entries[pick]
        out.append({"name": name, "rarity": rarity, "count": rng.randint(lo, hi)})
    return out


class Spawner:
    """Anchor + weighted-roll container spawner with despawn/respawn timers."""

    def __init__(self, map_id: int = 1, count: int = LOOT_ANCHOR_COUNT,
                 active_ratio: float = LOOT_ACTIVE_RATIO, jitter: float = LOOT_JITTER,
                 respawn_scale: float = 1.0, seed: int = 0x5EED) -> None:
        self.map_id = map_id
        self.anchors = build_anchors(map_id, count, seed)
        self.rng = random.Random(seed ^ map_id)
        self.active_ratio = active_ratio
        self.jitter = jitter
        self.respawn_scale = respawn_scale
        self.containers: dict[int, dict] = {}
        self.next_id = 0x400000
        self.phase = 1
        self.spawn_match()

    def _new_container(self, anchor_idx: int, phase: int | None = None) -> dict:
        anchor = self.anchors[anchor_idx]
        template = roll_template(anchor, self.phase if phase is None else phase, self.rng)
        spec = CONTAINERS[template]
        cid = self.next_id
        self.next_id += 1
        pos = (
            anchor["x"] + self.rng.gauss(0, self.jitter),
            0.0,
            anchor["z"] + self.rng.gauss(0, self.jitter),
        )
        container = {
            "id": cid,
            "template": template,
            "name": spec["name"],
            "tier": spec["tier"],
            "pos": pos,
            "anchor": anchor_idx,
            "state": "active",
            "contents": None,
            "taken": set(),
            "respawn_at": None,
            "prepare_ms": spec["prepare_ms"],
        }
        self.containers[cid] = container
        return container

    def spawn_match(self) -> None:
        for idx in range(len(self.anchors)):
            anchor = self.anchors[idx]
            chance = self.active_ratio * (1.25 if anchor["hotspot"] else 1.0)
            if self.rng.random() < chance:
                self._new_container(idx)

    def force_spawn(self, pos, template: int = 1) -> int:
        """Test/tutorial helper: place a container at an exact position."""
        idx = len(self.anchors)
        self.anchors.append({"x": pos[0], "z": pos[2], "zone": zone_of(pos[0], pos[2]),
                             "hotspot": False, "forced": True})
        self.phase = self.phase
        anchor = self.anchors[idx]
        spec = CONTAINERS[template]
        cid = self.next_id
        self.next_id += 1
        self.containers[cid] = {
            "id": cid, "template": template, "name": spec["name"], "tier": spec["tier"],
            "pos": (float(pos[0]), 0.0, float(pos[2])), "anchor": idx,
            "state": "active", "contents": None, "taken": set(), "respawn_at": None,
            "prepare_ms": spec["prepare_ms"],
        }
        return cid

    def tick(self, t: float) -> None:
        for container in list(self.containers.values()):
            if container["state"] == "gone" and container["respawn_at"] and t >= container["respawn_at"]:
                anchor = self.anchors[container["anchor"]]
                spec = CONTAINERS[container["template"]]
                container.update({
                    "state": "active", "contents": None, "taken": set(),
                    "respawn_at": None,
                    "pos": (anchor["x"] + self.rng.gauss(0, self.jitter), 0.0,
                            anchor["z"] + self.rng.gauss(0, self.jitter)),
                    "prepare_ms": spec["prepare_ms"],
                })

    def visible(self, pos, range_: float) -> list[dict]:
        return [c for c in self.containers.values()
                if c["state"] == "active" and dist(c["pos"], pos) <= range_]

    def deplete(self, container: dict, t: float) -> None:
        spec = CONTAINERS[container["template"]]
        container["state"] = "gone"
        container["respawn_at"] = t + spec["respawn_s"] * self.respawn_scale


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
    def __init__(self, ping_ms: float = PING_MS, dead_ms: float = DEAD_TIMEOUT_MS,
                 loot: Spawner | None = None, loot_respawn_s: float = 1.0) -> None:
        self.entities: dict[int, Entity] = {}
        self.sessions: dict[bytes, SessionState] = {}
        self.peers: set["Peer"] = set()
        self.next_eid = 1
        self.ping_ms = ping_ms
        self.dead_ms = dead_ms
        self.server_tick = 0
        self.loot = loot or Spawner(respawn_scale=loot_respawn_s)
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
            self.loot.tick(now())
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
        self.known_doodads: set[int] = set()
        self.opened: set[int] = set()
        self.inventory: list[dict] = []
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
        elif op == OP_LOOT_OPEN:
            await self._loot_open(json.loads(payload) if payload else {})
        elif op == OP_LOOT_TAKE:
            await self._loot_take(json.loads(payload) if payload else {})
        elif op == OP_ACK:
            pass

    async def _loot_open(self, msg: dict) -> None:
        ent = self.entity
        if not ent:
            return
        cid = int(msg.get("id", 0))
        container = self.server.loot.containers.get(cid)
        if not container or container["state"] != "active":
            await self.send(OP_LOOT_RESULT, cid, {"id": cid, "ok": False, "reason": "gone"})
            return
        if dist(ent.pos, container["pos"]) > LOOT_RANGE:
            await self.send(OP_LOOT_RESULT, cid, {"id": cid, "ok": False, "reason": "range"})
            return
        if cid in self.opened:
            await self.send(OP_LOOT_RESULT, cid, {"id": cid, "ok": False, "reason": "already"})
            return
        if container["contents"] is None:
            container["contents"] = roll_contents(CONTAINERS[container["template"]]["table"],
                                                  self.server.loot.rng)
        self.opened.add(cid)

        async def deliver() -> None:
            await asyncio.sleep(container["prepare_ms"] / 1000.0)
            items = [{"slot": i, **it} for i, it in enumerate(container["contents"])
                     if i not in container["taken"]]
            await self.send(OP_LOOT_LIST, cid, {
                "id": cid,
                "template": container["template"],
                "name": container["name"],
                "items": items,
            })

        asyncio.create_task(deliver())

    async def _loot_take(self, msg: dict) -> None:
        ent = self.entity
        if not ent:
            return
        cid = int(msg.get("id", 0))
        slot = int(msg.get("slot", -1))
        container = self.server.loot.containers.get(cid)
        if not container or container["state"] != "active" or container["contents"] is None:
            await self.send(OP_LOOT_RESULT, cid, {"id": cid, "ok": False, "reason": "gone"})
            return
        if dist(ent.pos, container["pos"]) > LOOT_RANGE:
            await self.send(OP_LOOT_RESULT, cid, {"id": cid, "ok": False, "reason": "range"})
            return
        if slot in container["taken"] or slot < 0 or slot >= len(container["contents"]):
            await self.send(OP_LOOT_RESULT, cid, {"id": cid, "ok": False, "reason": "slot"})
            return
        item = container["contents"][slot]
        container["taken"].add(slot)
        self.inventory.append(dict(item))
        await self.send(OP_ITEM_ADD, slot, {"name": item["name"], "rarity": item["rarity"],
                                            "count": item["count"],
                                            "inventory_size": len(self.inventory)})
        await self.send(OP_LOOT_RESULT, cid, {"id": cid, "ok": True, "slot": slot,
                                              "item": item})
        if len(container["taken"]) >= len(container["contents"]):
            self.server.loot.deplete(container, now())
            await self.send(OP_DOODAD_STATE, cid, {"id": cid, "state": "gone"})

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
        doodads = self.server.loot.visible(ent.pos, AOI_RANGE)
        current_d = {c["id"] for c in doodads}
        for cid in self.known_doodads - current_d:
            await self.send(OP_DOODAD_REMOVE, cid, {"id": cid})
        for c in doodads:
            if c["id"] not in self.known_doodads:
                await self.send(OP_DOODAD_ADD, c["id"], {
                    "id": c["id"], "template": c["template"], "name": c["name"],
                    "tier": c["tier"], "pos": [round(v, 2) for v in c["pos"]],
                })
        self.known_doodads = current_d


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
        self.doodads: dict[int, dict] = {}
        self.inventory: list[dict] = []
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
        elif op == OP_DOODAD_ADD:
            self.doodads[int(param)] = msg
            await self.events.put(("doodad_add", msg))
        elif op == OP_DOODAD_REMOVE:
            self.doodads.pop(int(param), None)
            await self.events.put(("doodad_remove", msg))
        elif op == OP_DOODAD_STATE:
            await self.events.put(("doodad_state", msg))
        elif op == OP_LOOT_LIST:
            await self.events.put(("loot_list", msg))
        elif op == OP_LOOT_RESULT:
            await self.events.put(("loot_result", msg))
        elif op == OP_ITEM_ADD:
            self.inventory.append(msg)
            await self.events.put(("item_add", msg))
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

    def loot_open(self, cid: int) -> None:
        data = json.dumps({"id": cid}, separators=(",", ":")).encode()
        self.writer.write(self.channel.build(OP_LOOT_OPEN, 0, data))

    def loot_take(self, cid: int, slot: int) -> None:
        data = json.dumps({"id": cid, "slot": slot}, separators=(",", ":")).encode()
        self.writer.write(self.channel.build(OP_LOOT_TAKE, 0, data))

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

    server = GameServer(loot=Spawner(count=12, active_ratio=0.35, respawn_scale=0.02))
    port = await server.start()
    key1 = b"k" * 16

    s1 = Spawner(count=20, seed=123)
    s2 = Spawner(count=20, seed=123)
    det = (s1.anchors == s2.anchors
           and [ (c["template"], round(c["pos"][0], 3), round(c["pos"][2], 3)) for c in s1.containers.values() ]
           == [ (c["template"], round(c["pos"][0], 3), round(c["pos"][2], 3)) for c in s2.containers.values() ])
    check("spawn rules deterministic per seed", det and len(s1.anchors) >= 20)

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

    cid = server.loot.force_spawn((0.0, 0.0, 3.0), template=1)
    add = await c1.wait("doodad_add", timeout=2.0)
    check("container spawns in aoi", add["id"] == cid and add["name"] == "一阶装备",
          f"id={add.get('id')} name={add.get('name')} pos={add.get('pos')}")

    c1.loot_open(cid)
    lst = await c1.wait("loot_list", timeout=2.0)
    check("loot list arrives after windup", lst["id"] == cid and len(lst["items"]) >= 1,
          f"items={lst.get('items')}")

    first = lst["items"][0]
    c1.loot_take(cid, first["slot"])
    item = await c1.wait("item_add", timeout=1.0)
    res = await c1.wait("loot_result", timeout=1.0)
    check("take grants item to inventory", res.get("ok") and len(c1.inventory) == 1
          and item["name"] == first["name"], f"item={item} inv={len(c1.inventory)}")

    for it in lst["items"][1:]:
        c1.loot_take(cid, it["slot"])
        await c1.wait("item_add", timeout=1.0)
        await c1.wait("loot_result", timeout=1.0)
    rem = await c1.wait("doodad_remove", timeout=1.5)
    check("emptied container despawns", rem.get("id") == cid,
          f"inv={len(c1.inventory)} removed={rem.get('id')}")

    respawn = await c1.wait("doodad_add", timeout=3.0)
    check("container respawns on timer", respawn["id"] == cid and respawn["template"] == 1,
          f"respawn={respawn.get('name')} pos={respawn.get('pos')}")

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
