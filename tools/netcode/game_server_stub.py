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


def w(line):
    with _lock:
        _logf.write(line + "\n")
        _logf.flush()
        print(line, flush=True)


def pframe(payload):
    return struct.pack("<H", len(payload) + 2) + payload


def hello():
    return pframe(bytes([0x20, 0x00]) + b"\x00" * 40)


RESPOND_ID = 0x2FF


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


def handle(conn, addr):
    t0 = time.time()
    w("[%s] GAME CONNECT from %s:%d" % (time.strftime("%H:%M:%S"), addr[0], addr[1]))
    conn.settimeout(0.2)
    buf = b""
    sess = GameSession()
    try:
        time.sleep(0.2)
        conn.sendall(hello())
        w("[%s] SENT game hello (42B)" % time.strftime("%H:%M:%S"))
        while True:
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
                if proto == 1 and os.environ.get("GAME_AUTORESP", "1") == "1":
                    time.sleep(0.2)
                    resp = handshake_respond()
                    conn.sendall(sess.encrypt(resp))
                    w("[%s] SENT handshake respond id=0x%X len=%d"
                      % (time.strftime("%H:%M:%S"), RESPOND_ID, len(resp)))
    except Exception as e:
        w("[%s] error after %.1fs: %s" % (time.strftime("%H:%M:%S"), time.time() - t0, e))
    finally:
        conn.close()


def main():
    global _logf
    port = 3725
    logpath = r"C:\jx3tmp\game_stub.log"
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--port" and i + 1 < len(args):
            port = int(args[i + 1])
        elif a == "--log" and i + 1 < len(args):
            logpath = args[i + 1]
    _logf = open(logpath, "a", encoding="utf-8")
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
