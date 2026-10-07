"""V2 P3: game-server capture stub.

The client's game transport uses the same wrapper factory (0x14079D100, mode 4): the
first packet must be the 42-byte hello (0x20 0x00 ...) and all later payloads are
encrypted with the stream cipher (state from the hello fields; constant 0xC9FFFFFF for
our zero hello). This listener sends the hello and logs the decrypted client packets.

Usage: python game_server_stub.py [--port 3725] [--log C:\\jx3tmp\\game_stub.log]
"""
import os
import socket
import struct
import sys
import threading
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gateway_cipher as gc

CLIENT_EXE = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe"

_lock = threading.Lock()
_logf = None
_rawf = None


def w(line):
    with _lock:
        _logf.write(line + "\n")
        _logf.flush()
        print(line, flush=True)


def wraw(line):
    if _rawf is None:
        return
    with _lock:
        _rawf.write(line + "\n")
        _rawf.flush()


def pframe(payload):
    return struct.pack("<H", len(payload) + 2) + payload


def hello():
    return pframe(bytes([0x20, 0x00]) + b"\x00" * 40)


RESPOND_ID = 0x2FE
ROLE_ID = int(os.environ.get("GAME_ROLE_ID", "1001"))


def id_frame(frame_id, size, role_id):
    """Build an S2C frame: [u16 id][u8 flags][u16 serial][u16 ack][u32 field] + payload.
    For id 4/10 the field u32 at +7 carries the role/entity id (the handler's lookup key).
    id 10 also carries the packed appearance qword at +0x4B (GAME_ID10_PACK hex) and its
    flag byte at +0x53 (GAME_ID10_FLAGS); the handler parses it (0x14016AD70) and the
    state-7 write is gated on the resulting fields."""
    p = bytearray(size)
    struct.pack_into("<H", p, 0, frame_id)
    struct.pack_into("<I", p, 7, role_id)
    if frame_id == 4:
        struct.pack_into("<I", p, 0x2C, int(os.environ.get("GAME_ID4_MAP", "0")))
        struct.pack_into("<I", p, 0x30, int(os.environ.get("GAME_ID4_REGION", "0")))
        struct.pack_into("<I", p, 0x34, int(os.environ.get("GAME_POS_X", "100")))
        struct.pack_into("<I", p, 0x38, int(os.environ.get("GAME_POS_Y", "100")))
        struct.pack_into("<I", p, 0x3C, int(os.environ.get("GAME_POS_Z", "0")))
        # +0xDF qword -> player+0xEC8 = global role id (KSO3World::AddPlayer registers it;
        # zero makes 0x140178730 log "Player GlobalID Error" and skip the world registration).
        struct.pack_into("<Q", p, 0xDF, int(os.environ.get("GAME_GLOBAL_ID", "1001")))
    if frame_id == 10:
        pack = int(os.environ.get("GAME_ID10_PACK", "0"), 16)
        flags = int(os.environ.get("GAME_ID10_FLAGS", "0"), 16)
        struct.pack_into("<Q", p, 0x4B, pack & 0xFFFFFFFFFFFFFFFF)
        p[0x53] = flags & 0xFF
    if frame_id == 7:
        struct.pack_into("<I", p, 7, int(os.environ.get("GAME_MAP_ID", "1")))
        struct.pack_into("<I", p, 0xB, int(os.environ.get("GAME_MAP_REGION", "1")))
        struct.pack_into("<I", p, 0xF, int(os.environ.get("GAME_POS_X", "100")))
        struct.pack_into("<I", p, 0x13, int(os.environ.get("GAME_POS_Y", "100")))
        struct.pack_into("<I", p, 0x17, int(os.environ.get("GAME_POS_Z", "0")))
    return bytes(p)


def id5_frame(role_id, sub=0, dtype=0, data=None):
    """S2C id 5 (per-player world data; handler 0x14015EB70, var size):
    [u16 id @+0][u8 flags @+2][u16 serial @+3][u16 ack @+5][u16 SIZE @+7]
    [player id dword @+9][sub-code byte @+0xD][type byte @+0xE][data @+0xF].
    Var-size protocols: KPlayerClient::ProcessPackage reads the packet length from the
    word at +7 and advances by it - a zero there makes the packet loop spin forever
    (live-proven 2026-10-06)."""
    if data is None:
        data = b"\x00" * 1024 if dtype == 0 else struct.pack("<I", 0)
    payload = struct.pack("<I", role_id) + bytes([sub & 0xFF, dtype & 0xFF]) + data
    p = bytearray(9 + len(payload))
    struct.pack_into("<H", p, 0, 5)
    struct.pack_into("<H", p, 7, len(p))
    p[9:9 + len(payload)] = payload
    return bytes(p)


def id3_frame(ts=None):
    """S2C id 3 (11 B fixed) - used as the benign keepalive when testing whether the
    synthetic id-5 (OnSyncQuestData) payloads are what crash the client."""
    if ts is None:
        ts = int(time.time() * 1000) & 0xFFFFFFFF
    p = bytearray(11)
    struct.pack_into("<H", p, 0, 3)
    struct.pack_into("<I", p, 7, ts)
    return bytes(p)


def id7_frame(ip="127.0.0.1", port=3725, map_id=296, gs_id=1):
    """S2C id 7 OnSwitchGS (0x14014C9B0, fixed 37 B): dword @+7 id, IP (network order,
    inet_ntoa) @+0x1B, u16 port @+0x1F, dword @+0x21 (map). The client tears down the
    session, reconnects to the GS and re-enters (real login -> world-GS handoff)."""
    p = bytearray(37)
    struct.pack_into("<H", p, 0, 7)
    struct.pack_into("<I", p, 7, gs_id)
    p[0x1B:0x1F] = socket.inet_aton(ip)
    struct.pack_into("<H", p, 0x1F, port)
    struct.pack_into("<I", p, 0x21, map_id)
    return bytes(p)


def handshake_respond(server_name=b"127.0.0.1", timeout=30, recover=1, flag2=1, success=1):
    """S2C id 0x2FF, min size 71 (0x47). Handler 0x140143A30 (OnHandShakeRespond):
    reads dwords at +7/+0xB/+0xF/+0x13, copies 32B ServerName from +0x17 to
    global+0xA8, [ +0x37 ] -> manager+0xE404 (ReconnectTimeout), and checks three
    dwords at +0x3B/+0x3F/+0x43 (all nonzero -> main path)."""
    p = bytearray(0x47)
    struct.pack_into("<H", p, 0, RESPOND_ID)
    struct.pack_into("<I", p, 0x37, timeout)
    struct.pack_into("<I", p, 0x3B, recover)
    struct.pack_into("<I", p, 0x3F, flag2)
    struct.pack_into("<I", p, 0x43, success)
    p[0x17:0x17 + len(server_name)] = server_name[:31]
    return bytes(p)


class GameSession(object):
    """Game transport cipher: same table, state starts at 0xC9FFFFFF for both
    directions and advances per packet (state = state*0x1F + 0x8088405)."""

    def __init__(self):
        self.cs = gc.STATE0   # client -> server (client's send state)
        self.sc = gc.STATE0   # server -> client (client's receive state)

    def decrypt(self, payload):
        out, self.cs = gc.game_cipher(payload, self.cs)
        return out

    def encrypt(self, payload):
        out, self.sc = gc.game_cipher(payload, self.sc)
        return pframe(out)


CMD_FILE = r"C:\jx3tmp\gsend.hex"
PORT = 3725
ID7_DONE = [False]


def handle(conn, addr):
    t0 = time.time()
    w("[%s] GAME CONNECT from %s:%d" % (time.strftime("%H:%M:%S"), addr[0], addr[1]))
    conn.settimeout(0.2)
    buf = b""
    sess = GameSession()
    sync_step = 0
    next_t = 0.0
    confirmed = [False]
    bind_left = [0]
    next_bind_t = [0.0]
    id7_sent = ID7_DONE
    id7_at = [0.0]
    bind_at = [0.0]
    bind_stage = [0]
    port_used = PORT
    try:
        time.sleep(0.2)
        conn.sendall(hello())
        w("[%s] SENT game hello (42B)" % time.strftime("%H:%M:%S"))
        while True:
            now = time.time()
            if next_t and now >= next_t:
                if sync_step == 1:
                    # First id 4 (map from GAME_ID4_MAP, default 0): triggers the client's
                    # world-entry/loading flow. Sent ONCE (a repeated identical id 4 makes the
                    # client reset the connection); the real map arrives via the post-op3 id 4.
                    conn.sendall(sess.encrypt(id_frame(4, 343, ROLE_ID)))
                    w("[%s] SYNC step1 id=4 initial (map=%s) - triggers loading"
                      % (time.strftime("%H:%M:%S"), os.environ.get("GAME_ID4_MAP", "0")))
                    sync_step = 1.5
                    next_t = now + 5.0
                elif sync_step == 1.5:
                    next_t = 0.0
                elif sync_step == 2:
                    # Keepalive after the enter-scene id 4: id 5 only (id 4 must not repeat).
                    # GAME_KEEPALIVE=0 disables it (test whether the id-5 payload is what jams
                    # the client's packet pump / logging loop).
                    if os.environ.get("GAME_KEEPALIVE", "1") == "1":
                        # id-5 (OnSyncQuestData) synthetic payloads are the crash suspect ->
                        # keepalive = benign id 3 unless GAME_KEEPALIVE_ID5=1.
                        if os.environ.get("GAME_KEEPALIVE_ID5", "0") == "1":
                            conn.sendall(sess.encrypt(id5_frame(ROLE_ID)))
                            w("[%s] SYNC keepalive id=5" % time.strftime("%H:%M:%S"))
                        else:
                            conn.sendall(sess.encrypt(id3_frame()))
                            w("[%s] SYNC keepalive id=3" % time.strftime("%H:%M:%S"))
                    next_t = now + 6.0
            if bind_left[0] > 0 and now >= next_bind_t[0]:
                p188 = bytearray(7)
                struct.pack_into("<H", p188, 0, 188)
                conn.sendall(sess.encrypt(bytes(p188)))
                bind_left[0] -= 1
                next_bind_t[0] = now + 10.0
                w("[%s] SYNC id=188 repeat bind (left=%d)" % (time.strftime("%H:%M:%S"), bind_left[0]))
            if bind_at[0] and now >= bind_at[0]:
                if bind_stage[0] == 0:
                    bind_stage[0] = 1
                    bind_at[0] = now + 1.5
                    if os.environ.get("GAME_SEND187", "1") == "1":
                        p187 = bytearray(8)
                        struct.pack_into("<H", p187, 0, 187)
                        conn.sendall(sess.encrypt(bytes(p187)))
                        w("[%s] SYNC id=187 sent (delayed bind stage 1)" % time.strftime("%H:%M:%S"))
                else:
                    bind_stage[0] = 0
                    bind_at[0] = 0.0
                    p188 = bytearray(7)
                    struct.pack_into("<H", p188, 0, 188)
                    conn.sendall(sess.encrypt(bytes(p188)))
                    w("[%s] SYNC id=188 delayed world-bind sent" % time.strftime("%H:%M:%S"))
                    bind_left[0] = int(os.environ.get("GAME_BIND_REPEATS", "4"))
                    next_bind_t[0] = now + 8.0
            if id7_at[0] and now >= id7_at[0] and not id7_sent[0]:
                id7_sent[0] = True
                conn.sendall(sess.encrypt(id7_frame(
                    ip=os.environ.get("GAME_ID7_IP", "127.0.0.1"),
                    port=int(os.environ.get("GAME_ID7_PORT", str(port_used))),
                    map_id=int(os.environ.get("GAME_ID4_MAP", "296")),
                    gs_id=int(os.environ.get("GAME_ID7_GSID", "1")))))
                w("[%s] SENT id=7 OnSwitchGS (delayed) -> %s:%s (client should reconnect)"
                  % (time.strftime("%H:%M:%S"), os.environ.get("GAME_ID7_IP", "127.0.0.1"),
                     os.environ.get("GAME_ID7_PORT", str(port_used))))
            if os.path.exists(CMD_FILE):
                try:
                    hx = open(CMD_FILE).read().strip()
                    os.remove(CMD_FILE)
                    payload = bytes.fromhex(hx)
                    conn.sendall(sess.encrypt(payload))
                    w("[%s] SENT cmd id=%d len=%d (plain %s)" % (time.strftime("%H:%M:%S"), payload[0] if payload else -1, len(payload), payload[:32].hex()))
                except Exception as e:
                    w("cmd error: %s" % e)
            try:
                data = conn.recv(65536)
            except socket.timeout:
                continue
            if not data:
                w("[%s] CLOSE by peer after %.1fs" % (time.strftime("%H:%M:%S"), time.time() - t0))
                break
            buf += data
            while len(buf) >= 2:
                total = struct.unpack_from("<H", buf, 0)[0]
                if total < 2 or len(buf) < total:
                    break
                payload = buf[2:total]
                buf = buf[total:]
                raw4 = payload[:4].hex()
                payload = sess.decrypt(payload)
                proto = payload[0] if payload else -1
                w("[%s] RECV proto=%d len=%d raw=%s pt=%s%s"
                  % (time.strftime("%H:%M:%S"), proto, len(payload), raw4,
                     payload[:64].hex(), "..." if len(payload) > 64 else ""))
                wraw("[%s] RECV proto=%d len=%d raw=%s pt=%s"
                     % (time.strftime("%H:%M:%S"), proto, len(payload), raw4,
                        payload.hex()))
                if proto == 1 and os.environ.get("GAME_AUTORESP", "1") == "1":
                    time.sleep(0.2)
                    resp = handshake_respond()
                    conn.sendall(sess.encrypt(resp))
                    w("[%s] SENT handshake respond id=0x%X len=%d"
                      % (time.strftime("%H:%M:%S"), RESPOND_ID, len(resp)))
                    if os.environ.get("GAME_SYNC", "1") == "1":
                        sync_step = 1
                        next_t = time.time() + 0.5
                        w("[%s] SYNC armed (id4 -> await confirm -> id5 keepalive)" % time.strftime("%H:%M:%S"))
                elif proto == 5 and os.environ.get("GAME_SYNC", "1") == "1":
                    confirmed[0] = True
                    if os.environ.get("GAME_CONFIRM_REPLY", "0") == "1":
                        time.sleep(0.2)
                        resp = id5_frame(ROLE_ID)
                        conn.sendall(sess.encrypt(resp))
                        w("[%s] SENT id=5 world data reply (sub=0 type=1) after client confirm"
                          % time.strftime("%H:%M:%S"))
                    else:
                        w("[%s] confirm received; id-5 reply DISABLED (GAME_CONFIRM_REPLY=0)"
                          % time.strftime("%H:%M:%S"))
                elif proto == 3 and os.environ.get("GAME_SYNC", "1") == "1":
                    # Client ApplyEnterScene (DoApplyEnterScene, 15B). The loading screen is
                    # now up; answer with the S2C id 3 time sync and (re)send id 4 so the
                    # scene bind runs while the sandbox scene exists.
                    # S2C id 3 table size = 11 (fixed): the client's packet loop advances by
                    # the table size, so the frame must be exactly 11 bytes.
                    ts = int(time.time() * 1000) & 0xFFFFFFFF
                    p = bytearray(11)
                    struct.pack_into("<H", p, 0, 3)
                    struct.pack_into("<I", p, 7, ts)
                    conn.sendall(sess.encrypt(bytes(p)))
                    w("[%s] SENT id=3 time sync after client ApplyEnterScene" % time.strftime("%H:%M:%S"))
                    time.sleep(0.3)
                    # S2C id 188 (0xBC, min 7 B) = OnSyncRoleDataOver ("Sync role data over !",
                    # handler 0x14015FA70) = the local-player world bind: ValidateRegions
                    # (0x1401830B0) on the scene, then the guard (0x140173D90) stores
                    # player+0x60 (the ConfirmClientReady prerequisite).
                    # GAME_SEND188=0 skips it (baseline: does the loading itself block the pump?)
                    # Real-flow order: S2C 187 OnSyncRoleDataSectionCheckRequest (8 B) starts the
                    # UI role-data sync (SYNC_ROLE_DATA_BEGIN); the client answers with its role
                    # data sections (C2S 0x6D); then 188 OnSyncRoleDataOver ends it
                    # (SYNC_ROLE_DATA_END). Our old order (188 only) may leave the UI's role-data
                    # flow stuck -> world UI/input never activates.
                    bdelay = float(os.environ.get("GAME_BIND_DELAY", "0"))
                    if bdelay > 0 and os.environ.get("GAME_SEND188", "1") == "1":
                        # Key experiment: the bind's position validator (0x1403D5220) needs the
                        # scene cell terrain data; an early bind fails it and SetMainPlayer (the
                        # world input layer) never fires. Delay the bind past the region load.
                        bind_at[0] = time.time() + bdelay
                        w("[%s] SYNC bind scheduled in %.0fs (delayed world bind)" % (
                            time.strftime("%H:%M:%S"), bdelay))
                    else:
                        if os.environ.get("GAME_SEND187", "1") == "1":
                            p187 = bytearray(8)
                            struct.pack_into("<H", p187, 0, 187)
                            conn.sendall(sess.encrypt(bytes(p187)))
                            w("[%s] SYNC id=187 role-data section check sent" % time.strftime("%H:%M:%S"))
                            time.sleep(1.5)
                        if os.environ.get("GAME_SEND188", "1") == "1":
                            # id 188 table size = 7 (fixed): must be exactly 7 bytes.
                            p188 = bytearray(7)
                            struct.pack_into("<H", p188, 0, 188)
                            conn.sendall(sess.encrypt(bytes(p188)))
                            w("[%s] SYNC id=188 world-bind sent" % time.strftime("%H:%M:%S"))
                            bind_left[0] = int(os.environ.get("GAME_BIND_REPEATS", "4"))
                            next_bind_t[0] = time.time() + 8.0
                        else:
                            w("[%s] SYNC id=188 SKIPPED (GAME_SEND188=0)" % time.strftime("%H:%M:%S"))
                    time.sleep(0.3)
                    if os.environ.get("GAME_SEND_ID7", "1") == "1" and not id7_sent[0]:
                        # Real login -> world-GS handoff: the client tears down and reconnects
                        # to the GS (live-verified 2026-10-07). GAME_ID7_DELAY warms the first
                        # session before the switch (a warm switch confirmed at 216 s vs cold
                        # 300-1657 s), so subsequent sessions iterate fast.
                        delay = float(os.environ.get("GAME_ID7_DELAY", "0"))
                        if delay > 0:
                            id7_at[0] = time.time() + delay
                            w("[%s] SYNC id=7 scheduled in %.0fs" % (time.strftime("%H:%M:%S"), delay))
                        else:
                            id7_sent[0] = True
                            conn.sendall(sess.encrypt(id7_frame(
                                ip=os.environ.get("GAME_ID7_IP", "127.0.0.1"),
                                port=int(os.environ.get("GAME_ID7_PORT", str(port_used))),
                                map_id=int(os.environ.get("GAME_ID4_MAP", "296")),
                                gs_id=int(os.environ.get("GAME_ID7_GSID", "1")))))
                            w("[%s] SENT id=7 OnSwitchGS -> %s:%s (client should reconnect)"
                              % (time.strftime("%H:%M:%S"), os.environ.get("GAME_ID7_IP", "127.0.0.1"),
                                 os.environ.get("GAME_ID7_PORT", str(port_used))))
                    sync_step = 2
                    next_t = time.time() + 6.0
    except Exception as e:
        w("[%s] error after %.1fs: %s" % (time.strftime("%H:%M:%S"), time.time() - t0, e))
    finally:
        conn.close()


def main():
    global _logf, _rawf, PORT
    port = 3725
    logpath = r"C:\jx3tmp\game_stub.log"
    rawpath = None
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--port" and i + 1 < len(args):
            port = int(args[i + 1])
        elif a == "--log" and i + 1 < len(args):
            logpath = args[i + 1]
        elif a == "--rawlog" and i + 1 < len(args):
            rawpath = args[i + 1]
    PORT = port
    _logf = open(logpath, "a", encoding="utf-8")
    if rawpath:
        _rawf = open(rawpath, "a", encoding="utf-8")
    gc.load_table(CLIENT_EXE)
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", port))
    srv.listen(16)
    w("== game stub listening on 127.0.0.1:%d at %s ==" % (port, time.strftime("%H:%M:%S")))
    while True:
        try:
            conn, addr = srv.accept()
        except Exception as e:
            w("accept error: %s" % e)
            time.sleep(0.5)
            continue
        t = threading.Thread(target=handle, args=(conn, addr), daemon=True)
        t.start()


if __name__ == "__main__":
    main()
