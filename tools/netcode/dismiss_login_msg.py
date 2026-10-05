"""V2: dismiss the client's modal error dialog (LoginMessage panel) then retry login.

The LoginMessage panel (420x112, Topmost1) has Btn_Close at panel+(352,10) 19x19.
Posts Esc/Enter (dialogs usually close on these) and clicks a grid of likely close
positions near the panel's top-right; then runs post_login and reports.

Usage: python dismiss_login_msg.py
"""
import ctypes
import ctypes.wintypes as w
import subprocess
import sys
import time

u32 = ctypes.windll.user32
WM_LBUTTONDOWN = 0x0201
WM_LBUTTONUP = 0x0202
WM_MOUSEMOVE = 0x0200
WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101
VK_ESCAPE = 0x1B
VK_RETURN = 0x0D

VENV = r"C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"
TOOLS = r"C:\Users\Zhibin Ren\Desktop\reborn-iso-v2\tools\netcode"
GW_LOG = r"C:\jx3tmp\gw_stdout.txt"


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


def key(hwnd, vk):
    u32.PostMessageW(hwnd, WM_KEYDOWN, vk, 1)
    u32.PostMessageW(hwnd, WM_KEYUP, vk, 0xC0000001)


def click(hwnd, x, y):
    lp = (y << 16) | (x & 0xFFFF)
    u32.PostMessageW(hwnd, WM_MOUSEMOVE, 0, lp)
    u32.PostMessageW(hwnd, WM_LBUTTONDOWN, 1, lp)
    time.sleep(0.03)
    u32.PostMessageW(hwnd, WM_LBUTTONUP, 0, lp)


def read(path):
    try:
        return open(path, "rb").read().decode("utf-8", "replace")
    except OSError:
        return ""


def main():
    wins = find_window()
    if not wins:
        print("no window")
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
    r = w.RECT()
    u32.GetWindowRect(hwnd, ctypes.byref(r))
    sw, sh = r.right - r.left, r.bottom - r.top
    sx, sy = sw / 1280.0, sh / 960.0
    print("focus: target=%s now=%s rect=%dx%d" % (hwnd, u32.GetForegroundWindow(), sw, sh))
    # 1. Esc / Enter a few times
    for _ in range(3):
        key(hwnd, VK_ESCAPE)
        time.sleep(0.15)
        key(hwnd, VK_RETURN)
        time.sleep(0.15)
    # 2. click likely close-button positions (canvas space): the panel is 420x112,
    #    anchored top-center-ish; Btn_Close at panel+(352..371, 10..29).
    cands = []
    for px in (430, 460, 490, 640, 810, 850):
        for py in (20, 40, 60, 80):
            cands.append((px, py))
    for cx, cy in cands:
        click(hwnd, int(cx * sx), int(cy * sy))
        time.sleep(0.08)
    # 3. try login
    start = len(read(GW_LOG))
    for attempt in range(3):
        subprocess.run([VENV, "-u", TOOLS + r"\post_login.py"], capture_output=True, timeout=60)
        time.sleep(5)
        if "RECV proto=3 " in read(GW_LOG)[start:]:
            print("LOGIN LANDED after dismiss (attempt %d)" % attempt)
            return 0
    print("login still not landing")
    return 1


if __name__ == "__main__":
    sys.exit(main())
