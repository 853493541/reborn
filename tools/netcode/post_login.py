"""V2 P2: scripted login via posted window messages (no mouse movement, no threads).

Posts WM_LBUTTONDOWN/UP + WM_CHAR to the client's KGWin32App window to fill the login
form (account/password) and press the login button. Does not move the real cursor.

Usage: python post_login.py [--account admin] [--password admin]
"""
import ctypes
import ctypes.wintypes as w
import sys
import time

u32 = ctypes.windll.user32
WM_LBUTTONDOWN = 0x0201
WM_LBUTTONUP = 0x0202
WM_MOUSEMOVE = 0x0200
WM_CHAR = 0x0102
WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101
VK_RETURN = 0x0D


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
    time.sleep(0.05)


def post_text(hwnd, text):
    for ch in text:
        u32.PostMessageW(hwnd, WM_CHAR, ord(ch), 1)
        time.sleep(0.02)


def post_enter(hwnd):
    u32.PostMessageW(hwnd, WM_KEYDOWN, VK_RETURN, 1)
    u32.PostMessageW(hwnd, WM_KEYUP, VK_RETURN, 0xC0000001)


def main():
    account = "admin"
    password = "admin"
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--account" and i + 1 < len(args):
            account = args[i + 1]
        elif a == "--password" and i + 1 < len(args):
            password = args[i + 1]
    wins = find_window()
    if not wins:
        print("no KGWin32App window")
        return 1
    hwnd = wins[0]
    # robust foreground: attach to the current foreground thread, then set focus
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
    print("window hwnd=%s rect=(%d,%d,%d,%d)" % (hwnd, r.left, r.top, r.right, r.bottom))
    sw = r.right - r.left
    sh = r.bottom - r.top
    sx, sy = sw / 1280.0, sh / 960.0
    cx = sw // 2
    cy = sh // 2
    px = cx - 567 * sx / 2
    py = cy - 210 * sy / 2
    acct = (int(px + 272 * sx), int(py + 31 * sy))
    pwd = (int(px + 272 * sx), int(py + 66 * sy))
    ok = (int(px + 288 * sx), int(py + 138 * sy))
    print("account=%s password=%s ok=%s" % (acct, pwd, ok))
    post_click(hwnd, *acct)
    post_text(hwnd, account)
    post_click(hwnd, *pwd)
    post_text(hwnd, password)
    time.sleep(0.2)
    post_click(hwnd, *ok)
    time.sleep(0.2)
    post_enter(hwnd)
    print("posted login")
    return 0


if __name__ == "__main__":
    sys.exit(main())
