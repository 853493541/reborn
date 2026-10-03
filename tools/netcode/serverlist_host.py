"""V2 P2: local server-list host.

Serves the client's server-list URL locally so the login UI shows OUR server:
  http://jx3comm.xoyocdn.com/jx3hd/zhcn_hd/serverlist/serverlist.ini
(and the wegameserverlist path) with a single entry pointing at 127.0.0.1:3724.

Point the hostname at 127.0.0.1 first (hosts file, admin):
  127.0.0.1 jx3comm.xoyocdn.com

Usage: python serverlist_host.py [--port 80]
"""
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

REGION = "本地区"
SERVER = "本地测试服"
LINE = "\t".join([REGION, SERVER, "71", "127.0.0.1", "3724", REGION, SERVER,
                  "0", "0", "z05", SERVER, REGION, "", "", "", "1"]) + "\n"


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        path = self.path.split("?")[0]
        if "serverlist.ini" in path:
            data = LINE.encode("gb18030")
            self.send_response(200)
            self.send_header("Content-Type", "text/plain")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
            print("[%s] served serverlist to %s" % (self.log_date_time_string(), self.client_address[0]), flush=True)
        else:
            self.send_response(404)
            self.end_headers()

    def log_message(self, fmt, *args):
        pass


def main():
    port = 80
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--port" and i + 1 < len(args):
            port = int(args[i + 1])
    srv = HTTPServer(("127.0.0.1", port), Handler)
    print("== server-list host on 127.0.0.1:%d ==" % port, flush=True)
    print("entry: %r" % LINE.strip(), flush=True)
    srv.serve_forever()


if __name__ == "__main__":
    main()
