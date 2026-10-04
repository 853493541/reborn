"""V2 P2: minimal gateway stub (threaded, byte-exact capture).

Listens on a TCP port, accepts every connection on its own thread, and logs
every received chunk immediately (hex + ascii + ms timestamps) before any
peer reset can discard it. Used to capture the real client's gateway traffic
when the client is pointed at this port.

Usage: python gateway_stub.py [--port 3724] [--log C:\\jx3tmp\\gateway_stub.log]
"""
import socket
import sys
import threading
import time

DEFAULT_PORT = 3724
DEFAULT_LOG = r"C:\jx3tmp\gateway_stub.log"
HELLO_HEX = "00"
_lock = threading.Lock()
_logf = None


def w(line):
    with _lock:
        _logf.write(line + "\n")
        _logf.flush()
        print(line, flush=True)


def handle(conn, addr):
    t0 = time.time()
    w("[%s] CONNECT from %s:%d" % (time.strftime("%H:%M:%S"), addr[0], addr[1]))
    conn.settimeout(None)
    if HELLO_HEX:
        time.sleep(0.2)
        try:
            hello = bytes.fromhex(HELLO_HEX)
            conn.sendall(hello)
            w("[%s] SENT hello %s" % (time.strftime("%H:%M:%S"), HELLO_HEX))
        except Exception as e:
            w("[%s] hello send failed: %s" % (time.strftime("%H:%M:%S"), e))
    total = 0
    try:
        while True:
            data = conn.recv(65536)
            if not data:
                w("[%s] CLOSE by peer after %.1fs, %d bytes total"
                  % (time.strftime("%H:%M:%S"), time.time() - t0, total))
                break
            total += len(data)
            hx = data.hex()
            asc = "".join(chr(b) if 32 <= b < 127 else "." for b in data)
            w("[%s.%03d] RECV %d bytes (total %d): %s"
              % (time.strftime("%H:%M:%S"), int((time.time() % 1) * 1000), len(data), total, hx))
            w("        ascii: %s" % asc)
    except Exception as e:
        w("[%s] error after %.1fs, %d bytes: %s"
          % (time.strftime("%H:%M:%S"), time.time() - t0, total, e))
    finally:
        conn.close()


def main():
    global _logf, HELLO_HEX
    port = DEFAULT_PORT
    logpath = DEFAULT_LOG
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--port" and i + 1 < len(args):
            port = int(args[i + 1])
        elif a == "--log" and i + 1 < len(args):
            logpath = args[i + 1]
        elif a == "--hello-hex" and i + 1 < len(args):
            HELLO_HEX = args[i + 1]
    _logf = open(logpath, "a", encoding="utf-8")
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", port))
    srv.listen(16)
    w("== gateway stub listening on 127.0.0.1:%d at %s ==" % (port, time.strftime("%H:%M:%S")))
    while True:
        conn, addr = srv.accept()
        t = threading.Thread(target=handle, args=(conn, addr), daemon=True)
        t.start()


if __name__ == "__main__":
    main()
