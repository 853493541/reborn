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


def handle(conn, addr):
    t0 = time.time()
    w("[%s] GAME CONNECT from %s:%d" % (time.strftime("%H:%M:%S"), addr[0], addr[1]))
    conn.settimeout(None)
    buf = b""
    sess = GameSession()
    try:
        time.sleep(0.2)
        conn.sendall(hello())
        w("[%s] SENT game hello (42B)" % time.strftime("%H:%M:%S"))
        while True:
            data = conn.recv(65536)
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
