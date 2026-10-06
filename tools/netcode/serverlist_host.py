"""V2 P2: local server-list host.

Serves the client's server-list URL locally:
  http://jx3comm.xoyocdn.com/jx3hd/zhcn_hd/serverlist/serverlist.ini

Strategy: serve the REAL shipped list (a template copy) with exactly one entry
patched to 127.0.0.1:3724, so the login UI shows a normal-looking server whose
gateway is our stub. Patching only the IP keeps every region/name field valid.

Point the hostname at 127.0.0.1 first (hosts file, admin):
  127.0.0.1 jx3comm.xoyocdn.com

Usage:
  python serverlist_host.py [--port 80] [--template C:\\jx3tmp\\real_serverlist.ini]
                            [--target 乾坤一掷]
"""
import sys
import zlib
from http.server import BaseHTTPRequestHandler, HTTPServer

DEFAULT_TEMPLATE = r"C:\jx3tmp\patched_serverlist.ini"
DEFAULT_TARGET = "乾坤一掷"
DEFAULT_CRC = b"3764339900"
FALLBACK = "\t".join(["本地区", "本地测试服", "71", "127.0.0.1", "3724", "本地区",
                      "本地测试服", "0", "0", "z05", "本地测试服", "本地区",
                      "", "", "", "1"]) + "\n"


def build_body(template_path, target):
    try:
        raw = open(template_path, "rb").read().decode("gb18030")
    except OSError:
        return FALLBACK.encode("gb18030"), "fallback single line"
    lines = raw.split("\n")
    out = []
    patched = 0
    for line in lines:
        fields = line.split("\t")
        if len(fields) >= 5:
            # Patch EVERY server entry to our local gateway. The client restores the
            # last-played server (possibly a real one) - a single-entry patch left every
            # other server with its real IP, so a login could reach the real server.
            fields[3] = "127.0.0.1"
            fields[4] = "3724"
            out.append("\t".join(fields))
            patched += 1
        else:
            out.append(line)
    note = "patched ALL %d entries to 127.0.0.1:3724" % patched if patched else "no patch applied"
    return "\n".join(out).encode("gb18030"), note


class Handler(BaseHTTPRequestHandler):
    body = b""
    note = ""
    crc_body = DEFAULT_CRC

    def do_GET(self):
        path = self.path.split("?")[0]
        ua = self.headers.get("User-Agent", "")
        print("[%s] REQUEST path=%r ua=%r hdrs=%s"
              % (self.log_date_time_string(), self.path, ua[:80],
                 {k: v for k, v in self.headers.items()
                  if k.lower() in ("if-modified-since", "if-none-match", "accept", "host")}),
              flush=True)
        if path.endswith(".crc"):
            data = self.crc_body
            self.send_response(200)
            self.send_header("Content-Type", "text/plain")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
            print("[%s] served .crc=%r to %s"
                  % (self.log_date_time_string(), data, self.client_address[0]), flush=True)
        elif "serverlist.ini" in path:
            self.send_response(200)
            self.send_header("Content-Type", "text/plain")
            self.send_header("Content-Length", str(len(self.body)))
            self.end_headers()
            self.wfile.write(self.body)
            print("[%s] served serverlist (%s, %d bytes) to %s"
                  % (self.log_date_time_string(), self.note, len(self.body),
                     self.client_address[0]), flush=True)
        else:
            self.send_response(404)
            self.end_headers()

    def log_message(self, fmt, *args):
        pass


def main():
    port = 80
    template = DEFAULT_TEMPLATE
    target = DEFAULT_TARGET
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--port" and i + 1 < len(args):
            port = int(args[i + 1])
        elif a == "--template" and i + 1 < len(args):
            template = args[i + 1]
        elif a == "--target" and i + 1 < len(args):
            target = args[i + 1]
    Handler.body, Handler.note = build_body(template, target)
    Handler.crc_body = str(zlib.crc32(Handler.body) & 0xffffffff).encode()
    srv = HTTPServer(("127.0.0.1", port), Handler)
    print("== server-list host on 127.0.0.1:%d ==" % port, flush=True)
    print("template=%s target=%s note=%s bytes=%d crc=%s"
          % (template, target, Handler.note, len(Handler.body), Handler.crc_body.decode()), flush=True)
    srv.serve_forever()


if __name__ == "__main__":
    main()
