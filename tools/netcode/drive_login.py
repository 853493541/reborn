"""V2: drive the login form in the running client window (account/password/Enter).

Sends synthetic keyboard input to the real client's main window so the login
attempt does not depend on a human click. Read-only with respect to the game
process: input goes through the normal window message path.

Usage: python drive_login.py [--account admin] [--password admin] [--pid N]
"""
import ctypes
import ctypes.wintypes as w
import sys
import time

u32 = ctypes.windll.user32
k32 = ctypes.windll.kernel32

INPUT_KEYBOARD = 1
KEYEVENTF_KEYUP = 0x0002
KEYEVENTF_UNICODE = 0x0004
VK_TAB = 0x09
VK_RETURN = 0x0D
VK_CONTROL = 0x11
VK_A = 0x41


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", w.WORD), ("wScan", w.WORD), ("dwFlags", w.DWORD),
                ("time", w.DWORD), ("dwExtraInfo", ctypes.POINTER(w.ULONG))]


class _INPUTUNION(ctypes.Union):
    _fields_ = [("ki", KEYBDINPUT), ("pad", ctypes.c_byte * 24)]


class INPUT(ctypes.Structure):
    _fields_ = [("type", w.DWORD), ("u", _INPUTUNION)]


def key(vk, up=False):
    inp = INPUT(type=INPUT_KEYBOARD)
    inp.u.ki.wVk = vk
    inp.u.ki.dwFlags = KEYEVENTF_KEYUP if up else 0
    u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))


def unicode_char(ch):
    for up in (False, True):
        inp = INPUT(type=INPUT_KEYBOARD)
        inp.u.ki.wScan = ord(ch)
        inp.u.ki.dwFlags = KEYEVENTF_UNICODE | (KEYEVENTF_KEYUP if up else 0)
        u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))


def tap(vk):
    key(vk)
    key(vk, up=True)


def ctrl_a():
    key(VK_CONTROL)
    tap(VK_A)
    key(VK_CONTROL, up=True)


def type_text(text):
    for ch in text:
        unicode_char(ch)
        time.sleep(0.02)


def find_window(pid):
    found = []

    @ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    def cb(hwnd, lparam):
        p = w.DWORD()
        u32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
        if p.value != pid or not u32.IsWindowVisible(hwnd):
            return True
        buf = ctypes.create_unicode_buffer(256)
        u32.GetClassNameW(hwnd, buf, 256)
        cls = buf.value
        u32.GetWindowTextW(hwnd, buf, 256)
        title = buf.value
        if cls == "KGWin32App" or "剑网3" in title:
            found.append((hwnd, cls, title))
        return True

    u32.EnumWindows(cb, 0)
    return found


def main():
    account = "admin"
    password = "admin"
    pid = 0
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--account" and i + 1 < len(args):
            account = args[i + 1]
        elif a == "--password" and i + 1 < len(args):
            password = args[i + 1]
        elif a == "--pid" and i + 1 < len(args):
            pid = int(args[i + 1])
    if not pid:
        import subprocess
        out = subprocess.check_output(
            ["powershell", "-NoProfile", "-Command",
             "(Get-Process JX3ClientX64 -ErrorAction SilentlyContinue).Id"], text=True)
        pid = int(out.strip().splitlines()[0])
    wins = find_window(pid)
    print("windows:", wins)
    if not wins:
        print("no client window found")
        return 1
    hwnd = wins[0][0]
    u32.SetForegroundWindow(hwnd)
    u32.SetActiveWindow(hwnd)
    time.sleep(0.8)
    print("driving login: ctrl+a, account, tab, ctrl+a, password, enter")
    ctrl_a()
    type_text(account)
    time.sleep(0.2)
    tap(VK_TAB)
    time.sleep(0.2)
    ctrl_a()
    type_text(password)
    time.sleep(0.3)
    tap(VK_RETURN)
    print("done")
    return 0


if __name__ == "__main__":
    sys.exit(main())
