"""V2 P2: minimal gateway stub.

Listens on a TCP port, logs every received packet (hex + ascii + timestamp), and can send
scripted responses once the packet formats are decoded. Used to capture the real client's
gateway handshake/verify traffic when the client is pointed at this port.

Usage: python gateway_stub.py [--port 3724] [--log C:\\jx3tmp\\gateway_stub.log]
"""
import socket
import sys
import time

DEFAULT_PORT = 3724
DEFAULT_LOG = r"C:\jx3tmp\gateway_stub.log"


def main():
    port = DEFAULT_PORT
    logpath = DEFAULT_LOG
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--port" and i + 1 < len(args):
            port = int(args[i + 1])
        elif a == "--log" and i + 1 < len(args):
            logpath = args[i + 1]

    log = open(logpath, "a", encoding="utf-8")

    def w(line):
        log.write(line + "\n")
        log.flush()
        print(line, flush=True)

    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", port))
    srv.listen(4)
    w("== gateway stub listening on 127.0.0.1:%d at %s ==" % (port, time.strftime("%H:%M:%S")))

    while True:
        conn, addr = srv.accept()
        w("[%s] CONNECT from %s:%d" % (time.strftime("%H:%M:%S"), addr[0], addr[1]))
        conn.settimeout(0.5)
        last = time.time()
        try:
            while True:
                try:
                    data = conn.recv(65536)
                except socket.timeout:
                    if time.time() - last > 20:
                        w("   (no data for 20s, closing)")
                        break
                    continue
                if not data:
                    w("   connection closed by peer")
                    break
                last = time.time()
                hx = data.hex()
                asc = "".join(chr(b) if 32 <= b < 127 else "." for b in data)
                w("[%s] RECV %d bytes: %s" % (time.strftime("%H:%M:%S.%f")[:-3], len(data), hx))
                w("        ascii: %s" % asc)
        except Exception as e:
            w("   error: %s" % e)
        finally:
            conn.close()


if __name__ == "__main__":
    main()
