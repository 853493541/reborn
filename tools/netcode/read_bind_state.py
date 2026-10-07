"""V2: one-shot read of the world-bind state (no polling, no hang risk).

Usage: python read_bind_state.py
Prints client/registry/player/scene pointers and the bind fields (p60, state, pos, cells).
"""
import ctypes
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import watch_world_bind as W

k32 = ctypes.windll.kernel32


def main():
    pid = W.find_client_pid()
    if not pid:
        print("no JX3ClientX64.exe")
        return 1
    base = W.module_base(pid, "jx3client")
    hp = k32.OpenProcess(0x438, False, pid)
    if not hp:
        print("OpenProcess failed")
        return 1
    r = W.Reader(hp)
    info = W.snapshot(r, base)
    print("pid=%s base=0x%X" % (pid, base))
    print(W.render(info))
    if info and info.get("player"):
        print("  player=0x%X scene=0x%X" % (info["player"], info.get("scene") or 0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
