"""V2 P2: scripted role select + enter-game button.

The LoginRoleList panel: role entries on the right; Btn_EnterGame anchored
BOTTOMCENTER,BOTTOMCENTER,0,-37 (WndEnterGame 230x50, Btn_EnterGame 600x48).
Single click selects a role, then the enter button starts the login-game request (op 10).
Also tries double-clicks on role entries (OnLButtonDBClick).

Usage: python role_enter.py [--log C:\\jx3tmp\\gw_stdout.txt]
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


def click(hwnd, x, y):
    lp = (y << 16) | (x & 0xFFFF)
    u32.PostMessageW(hwnd, WM_MOUSEMOVE, 0, lp)
    u32.PostMessageW(hwnd, WM_LBUTTONDOWN, 1, lp)
    time.sleep(0.03)
    u32.PostMessageW(hwnd, WM_LBUTTONUP, 0, lp)
    time.sleep(0.12)


def main():
    logpath = r"C:\jx3tmp\gw_stdout.txt"
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

    def A(cx, cy):
        return int(cx * sx), int(cy * sy)

    def check():
        try:
            with open(logpath, "rb") as f:
                f.seek(start)
                return "RECV proto=10 " in f.read().decode("utf-8", "replace")
        except OSError:
            return False

    try:
        start = os.path.getsize(logpath)
    except OSError:
        start = 0
    # role entry candidates (canvas): right panel area, several rows/cols
    roles = [(760, 240), (820, 250), (880, 260), (940, 270), (760, 300), (880, 310),
             (760, 220), (900, 240), (800, 280), (860, 290)]
    enters = [(640, 905), (640, 895), (640, 915), (600, 905), (680, 905)]
    hit = None
    for (rx, ry) in roles:
        ax, ay = A(rx, ry)
        click(hwnd, ax, ay)          # select
        if check():
            hit = ("select-click", rx, ry)
            break
        ex, ey = A(*enters[0])
        click(hwnd, ex, ey)          # enter game
        if check():
            hit = ("enter-click", rx, ry)
            break
        click(hwnd, ax, ay)          # double click variant
        click(hwnd, ax, ay)
        if check():
            hit = ("double-click", rx, ry)
            break
    if hit:
        print("OPCODE 10 after %s at role=(%d,%d)" % hit)
    else:
        print("no opcode-10 after %d role candidates" % len(roles))
    return 0


if __name__ == "__main__":
    sys.exit(main())
