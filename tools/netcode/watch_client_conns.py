"""V2: watch the real client's TCP endpoints during a login attempt.

Polls the JX3ClientX64 process connections every 2 s and logs every new
remote endpoint (and any 127.0.0.1:80 / :3724 activity) to a file, so a
single user click can be correlated afterwards.

Usage: python watch_client_conns.py [--seconds 900] [--out C:\\jx3tmp\\client_conns.log]
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from real_launch_watch import tcp_rows, find_pid, ip, port


def main():
    secs = 900
    out_path = r"C:\jx3tmp\client_conns.log"
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--seconds" and i + 1 < len(args):
            secs = int(args[i + 1])
        elif a == "--out" and i + 1 < len(args):
            out_path = args[i + 1]
    f = open(out_path, "a", encoding="utf-8")
    def w(line):
        f.write(line + "\n")
        f.flush()
        print(line, flush=True)
    w("== watch start %s ==" % time.strftime("%H:%M:%S"))
    seen = set()
    end = time.time() + secs
    while time.time() < end:
        procs = find_pid()
        pids = set(p[0] for p in procs if p[2].lower() == "jx3clientx64.exe")
        if pids:
            rows = [r for r in tcp_rows() if r.dwOwningPid in pids]
            for r in rows:
                key = (r.dwOwningPid, r.dwState, ip(r.dwLocalAddr), port(r.dwLocalPort),
                       ip(r.dwRemoteAddr), port(r.dwRemotePort))
                if key in seen:
                    continue
                seen.add(key)
                rem = "%s:%d" % (ip(r.dwRemoteAddr), port(r.dwRemotePort))
                loc = "%s:%d" % (ip(r.dwLocalAddr), port(r.dwLocalPort))
                if r.dwState == 2:
                    w("%s NEW connect %s -> %s" % (time.strftime("%H:%M:%S"), loc, rem))
                else:
                    w("%s state=%d %s -> %s" % (time.strftime("%H:%M:%S"), r.dwState, loc, rem))
        time.sleep(2)
    w("== watch end ==")
    f.close()


if __name__ == "__main__":
    main()
