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
import os
import socket
import struct
import sys
import threading
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gateway_cipher as gc

CLIENT_EXE = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe"

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


def pframe(payload):
    # plaintext frame: only the connect hello uses this (pre-session-setup packet)
    return struct.pack("<H", len(payload) + 2) + payload


def frame(payload):
    # encrypted frame: after the hello the client enables the stream cipher
    # (exe+0x7A2590, state from the hello fields) on send AND receive.
    return pframe(gc.cipher(payload))


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
    # payload size 0x2A (42) and first bytes 0x20 0x00; PLAINTEXT (it establishes
    # the session cipher state, which for this constant hello is 0xC9FFFFFF)
    return pframe(bytes([0x20, 0x00]) + b"\x00" * 40)


def account_ok():
    return frame(bytes([3, 0]))


def role_list():
    # entry layout per the client parse 0x140185A20: name1 @+4 (32B), name2 @+0x24 (32B),
    # role id @+0x40 (docs) and a qword id @+0x4c; count @+5; [1] must equal [5] to fire
    # the second role-list event (0x32).
    p = bytearray(ROLE_LIST_SIZE)
    p[0] = 9
    struct.pack_into("<I", p, 1, 1)
    struct.pack_into("<I", p, 5, 1)
    entry = 9
    name = "测试角色".encode("gb18030")
    p[entry + 4:entry + 4 + len(name)] = name
    p[entry + 0x24:entry + 0x24 + len(name)] = name
    struct.pack_into("<I", p, entry + 0, ROLE_ID)
    struct.pack_into("<I", p, entry + 0x40, ROLE_ID)
    struct.pack_into("<Q", p, entry + 0x4C, ROLE_ID)
    struct.pack_into("<I", p, entry + 0x73, 100)
    struct.pack_into("<I", p, entry + 0x77, 0)
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
                raw4 = payload[:4].hex()
                payload = gc.cipher(payload)
                proto = payload[0] if payload else -1
                hx = payload[:48].hex()
                w("[%s] RECV proto=%d len=%d raw=%s pt=%s%s"
                  % (time.strftime("%H:%M:%S"), proto, len(payload), raw4, hx,
                     "..." if len(payload) > 48 else ""))
                if proto == 2:
                    if os.environ.get("GW_MODE") == "p2":
                        conn.sendall(handshake_respond())
                        w("   -> proto2 respond only (GW_MODE=p2)")
                    else:
                        burst = handshake_respond() + handshake_respond_v4() + role_list()
                        conn.sendall(burst)
                        w("   -> BURST: proto2 respond + proto4 verify + proto9 role list (%d bytes)" % len(burst))
                elif proto == 1:
                    conn.sendall(frame(payload))
                    w("   -> proto1 ping echo")
                elif proto == 3:
                    conn.sendall(handshake_respond_v4())
                    w("   -> proto4 verify respond (252 bytes)")
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
    gc.load_table(CLIENT_EXE)
    w("== cipher table loaded (state 0x%08X) ==" % gc.STATE0)
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
