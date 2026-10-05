"""V2 P2: scripted role selection — PostMessage click grid over the role-list panel.

After the login chain (verify respond + role list), the LoginRoleList panel shows on the
right side. This posts clicks on a canvas-space grid (1280x960 design canvas scaled to the
window) and watches the stub log for the client's opcode-10 (login game) request.

Usage: python role_click.py [--log C:\\jx3tmp\\gw_stdout.txt] [--x0 640 --x1 1060 --y0 180 --y1 360]
"""
import ctypes
import ctypes.wintypes as w
import os
import sys
import time

u32 = ctypes.windll.user32
WM_LBUTTONDOWN = 0x0201
WM_LBUTTONUP = 0x0202
WM_MOUSEMOVE = 0x0200


def find_window():
    found = []

    @ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    def cb(hwnd, lparam):
        buf = ctypes.create_unicode_buffer(256)
        u32.GetClassNameW(hwnd, buf, 256)
        if buf.value == "KGWin32App" and u32.IsWindowVisible(hwnd):
            found.append(hwnd)
        return True

    u32.EnumWindows(cb, 0)
    return found


def post_click(hwnd, x, y):
    lp = (y << 16) | (x & 0xFFFF)
    u32.PostMessageW(hwnd, WM_MOUSEMOVE, 0, lp)
    u32.PostMessageW(hwnd, WM_LBUTTONDOWN, 1, lp)
    time.sleep(0.03)
    u32.PostMessageW(hwnd, WM_LBUTTONUP, 0, lp)


def main():
    logpath = r"C:\jx3tmp\gw_stdout.txt"
    x0, x1, y0, y1 = 640, 1060, 180, 360
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--log" and i + 1 < len(args):
            logpath = args[i + 1]
    wins = find_window()
    if not wins:
        print("no KGWin32App window")
        return 1
    hwnd = wins[0]
    fg = u32.GetForegroundWindow()
    if fg != hwnd:
        tid_fg = u32.GetWindowThreadProcessId(fg, None)
        tid_us = ctypes.windll.kernel32.GetCurrentThreadId()
        u32.AttachThreadInput(tid_us, tid_fg, True)
        u32.SetForegroundWindow(hwnd)
        u32.BringWindowToTop(hwnd)
        u32.AttachThreadInput(tid_us, tid_fg, False)
        time.sleep(0.4)
    print("focus: target=%s now=%s" % (hwnd, u32.GetForegroundWindow()))
    r = w.RECT()
    u32.GetWindowRect(hwnd, ctypes.byref(r))
    sw, sh = r.right - r.left, r.bottom - r.top
    sx, sy = sw / 1280.0, sh / 960.0
    print("window rect=(%d,%d,%d,%d) scale=%.2f/%.2f" % (r.left, r.top, r.right, r.bottom, sx, sy))
    try:
        start = os.path.getsize(logpath)
    except OSError:
        start = 0
    found = None
    n = 0
    y = y0
    while y <= y1 and not found:
        x = x0
        while x <= x1 and not found:
            ax, ay = int(x * sx), int(y * sy)
            post_click(hwnd, ax, ay)
            n += 1
            time.sleep(0.35)
            try:
                with open(logpath, "rb") as f:
                    f.seek(start)
                    new = f.read().decode("utf-8", "replace")
            except OSError:
                new = ""
            if "RECV proto=10 " in new:
                found = (x, y, ax, ay)
                break
            x += 50
        y += 25
    if found:
        print("ROLE CLICK HIT at canvas=(%d,%d) actual=(%d,%d) after %d clicks" % (found + (n,)))
    else:
        print("no opcode-10 after %d clicks" % n)
    return 0


if __name__ == "__main__":
    sys.exit(main())
