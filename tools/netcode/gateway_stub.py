"""V2 P2: gateway stub — full login conversation (static decode, no discovery runs).

Wire frame: [u16 LE total][payload]; payload[0] = protocol id.
Conversation (see JX3_CLIENT_LAUNCH_AND_SESSION.md sec.48):
  on connect          -> proto 2 handshake respond (result 0)
  op 2 handshake      -> proto 2 respond again (idempotent)
  op 3 account verify -> proto 3 account-ok (2 bytes) + proto 9 role list (0x324)
  op 10 login game    -> proto 14 login key with pcszGameServerIP @0xe
Anything else is logged and ignored.

Usage: python gateway_stub.py [--port 3724] [--log C:\\jx3tmp\\gateway_stub.log]
                              [--game-ip 127.0.0.1]
"""
import socket
import struct
import sys
import threading
import time

DEFAULT_PORT = 3724
DEFAULT_LOG = r"C:\jx3tmp\gateway_stub.log"
GAME_IP = "127.0.0.1"
ROLE_ID = 1001
ROLE_LIST_SIZE = 0x324

_lock = threading.Lock()
_logf = None


def w(line):
    with _lock:
        _logf.write(line + "\n")
        _logf.flush()
        print(line, flush=True)


def frame(payload):
    return struct.pack("<H", len(payload) + 2) + payload


def handshake_respond():
    return frame(bytes([2, 0]) + b"\x00" * 12)


def handshake_respond_v4():
    # proto 4 (client handshake is 229 B) -> server respond >= 252 B
    # result code dword at +1 (switch 1..0x5B); fields per handler 0x1879D0;
    # account strings at +0x5B (0x81) and +0xDC (0x20)
    p = bytearray(252)
    p[0] = 4
    struct.pack_into("<I", p, 1, 1)
    p[5] = 2
    p[8:8 + 9] = b"127.0.0.1"
    acc = "admin".encode("gb18030") + b"\x00"
    p[0x5B:0x5B + len(acc)] = acc
    p[0xDC:0xDC + len(acc)] = acc
    return frame(bytes(p))


def connect_hello():
    # connection-layer hello required by the wrapper factory (0x1407A0B00):
    # payload size 0x2A (42) and first bytes 0x20 0x00
    return frame(bytes([0x20, 0x00]) + b"\x00" * 40)


def account_ok():
    return frame(bytes([3, 0]))


def role_list():
    p = bytearray(ROLE_LIST_SIZE)
    p[0] = 9
    struct.pack_into("<I", p, 5, 1)
    entry = 9
    struct.pack_into("<I", p, entry + 0x40, ROLE_ID)
    name = "测试角色".encode("gb18030")
    p[entry + 0x44:entry + 0x44 + len(name)] = name
    return frame(bytes(p))


def login_key():
    p = bytes([14, 0]) + struct.pack("<III", ROLE_ID, 0, 0) + GAME_IP.encode() + b"\x00"
    return frame(p)


def handle(conn, addr):
    t0 = time.time()
    w("[%s] CONNECT from %s:%d" % (time.strftime("%H:%M:%S"), addr[0], addr[1]))
    conn.settimeout(None)
    buf = b""
    try:
        time.sleep(0.2)
        conn.sendall(connect_hello())
        w("[%s] SENT connect hello (0x20 0x00, 42 bytes)" % time.strftime("%H:%M:%S"))
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
                proto = payload[0] if payload else -1
                hx = payload[:48].hex()
                w("[%s] RECV proto=%d len=%d %s%s"
                  % (time.strftime("%H:%M:%S"), proto, len(payload), hx,
                     "..." if len(payload) > 48 else ""))
                if proto == 4:
                    conn.sendall(handshake_respond_v4())
                    w("   -> proto4 handshake respond (252 bytes)")
                elif proto == 39:
                    conn.sendall(frame(payload))
                    w("   -> proto39 ping echo")
                elif proto == 3:
                    conn.sendall(account_ok())
                    w("   -> proto3 account ok")
                    time.sleep(0.05)
                    conn.sendall(role_list())
                    w("   -> proto9 role list (%d bytes)" % ROLE_LIST_SIZE)
                elif proto == 10:
                    conn.sendall(login_key())
                    w("   -> proto14 login key (game ip %s)" % GAME_IP)
                else:
                    w("   (no response implemented for proto %d)" % proto)
    except Exception as e:
        w("[%s] error after %.1fs: %s" % (time.strftime("%H:%M:%S"), time.time() - t0, e))
    finally:
        conn.close()


def main():
    global _logf, GAME_IP
    port = DEFAULT_PORT
    logpath = DEFAULT_LOG
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--port" and i + 1 < len(args):
            port = int(args[i + 1])
        elif a == "--log" and i + 1 < len(args):
            logpath = args[i + 1]
        elif a == "--game-ip" and i + 1 < len(args):
            GAME_IP = args[i + 1]
    _logf = open(logpath, "a", encoding="utf-8")
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", port))
    srv.listen(16)
    w("== gateway stub listening on 127.0.0.1:%d at %s (conversation: proto2/3/9/14) =="
      % (port, time.strftime("%H:%M:%S")))
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
