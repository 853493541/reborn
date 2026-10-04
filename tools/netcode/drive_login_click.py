"""V2: drive the login form with real mouse clicks + typing (no human needed).

Coordinates derive from the extracted LoginPassword.ini layout:
  WndPassword 567x210 anchored CENTER,CENTER in the client window.
  Edit_Account (157,24,230x21), Edit_Password (157,59,230x21), Btn_OK (141,123,294x36)
  relative to Wnd_PasswordContent (0,-3) inside WndPassword.
The UI design canvas is 1280x960 -> actual 3840x2160.

Usage: python drive_login_click.py [--mode scaled|unscaled] [--account admin] [--password admin]
"""
import ctypes
import ctypes.wintypes as w
import sys
import time

u32 = ctypes.windll.user32

INPUT_MOUSE = 0
INPUT_KEYBOARD = 1
KEYEVENTF_KEYUP = 0x0002
KEYEVENTF_UNICODE = 0x0004
MOUSEEVENTF_MOVE = 0x0001
MOUSEEVENTF_ABSOLUTE = 0x8000
MOUSEEVENTF_LEFTDOWN = 0x0002
MOUSEEVENTF_LEFTUP = 0x0004
VK_TAB = 0x09
VK_RETURN = 0x0D
VK_CONTROL = 0x11
VK_A = 0x41


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", w.WORD), ("wScan", w.WORD), ("dwFlags", w.DWORD),
                ("time", w.DWORD), ("dwExtraInfo", ctypes.POINTER(w.ULONG))]


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", w.LONG), ("dy", w.LONG), ("mouseData", w.DWORD),
                ("dwFlags", w.DWORD), ("time", w.DWORD),
                ("dwExtraInfo", ctypes.POINTER(w.ULONG))]


class _INPUTUNION(ctypes.Union):
    _fields_ = [("ki", KEYBDINPUT), ("mi", MOUSEINPUT), ("pad", ctypes.c_byte * 32)]


class INPUT(ctypes.Structure):
    _fields_ = [("type", w.DWORD), ("u", _INPUTUNION)]


def key(vk, up=False):
    inp = INPUT(type=INPUT_KEYBOARD)
    inp.u.ki.wVk = vk
    inp.u.ki.dwFlags = KEYEVENTF_KEYUP if up else 0
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
        for up in (False, True):
            inp = INPUT(type=INPUT_KEYBOARD)
            inp.u.ki.wScan = ord(ch)
            inp.u.ki.dwFlags = KEYEVENTF_UNICODE | (KEYEVENTF_KEYUP if up else 0)
            u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))
        time.sleep(0.02)


def click(x, y, screen_w, screen_h):
    ax = int(x * 65535 / screen_w)
    ay = int(y * 65535 / screen_h)
    for flags in (MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTDOWN,
                  MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTUP):
        inp = INPUT(type=INPUT_MOUSE)
        inp.u.mi.dx = ax
        inp.u.mi.dy = ay
        inp.u.mi.dwFlags = flags
        u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))
        time.sleep(0.05)


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
        if buf.value == "KGWin32App":
            found.append(hwnd)
        return True

    u32.EnumWindows(cb, 0)
    return found


def main():
    mode = "scaled"
    account = "admin"
    password = "admin"
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--mode" and i + 1 < len(args):
            mode = args[i + 1]
        elif a == "--account" and i + 1 < len(args):
            account = args[i + 1]
        elif a == "--password" and i + 1 < len(args):
            password = args[i + 1]
    import subprocess
    out = subprocess.check_output(
        ["powershell", "-NoProfile", "-Command",
         "(Get-Process JX3ClientX64 -ErrorAction SilentlyContinue).Id"], text=True)
    pid = int(out.strip().splitlines()[0])
    wins = find_window(pid)
    if not wins:
        print("no KGWin32App window")
        return 1
    hwnd = wins[0]
    rect = w.RECT()
    u32.GetWindowRect(hwnd, ctypes.byref(rect))
    sw = rect.right - rect.left
    sh = rect.bottom - rect.top
    u32.ShowWindow(hwnd, 9)
    u32.SetForegroundWindow(hwnd)
    time.sleep(0.5)
    fg = u32.GetForegroundWindow()
    if fg != hwnd:
        tap(0x12)
        time.sleep(0.2)
        u32.SetForegroundWindow(hwnd)
        time.sleep(0.3)
        fg = u32.GetForegroundWindow()
    print("foreground target=%s now=%s focused=%s" % (hwnd, fg, fg == hwnd))
    u32.SetActiveWindow(hwnd)
    time.sleep(0.8)
    cx = rect.left + sw // 2
    cy = rect.top + sh // 2
    # panel top-left = center - panel_size/2 (anchor CENTER,CENTER)
    if mode == "scaled":
        sx, sy = sw / 1280.0, sh / 960.0
        panel_w, panel_h = 567 * sx, 210 * sy
    else:
        sx, sy = 1.0, 1.0
        panel_w, panel_h = 567.0, 210.0
    px = cx - panel_w / 2
    py = cy - panel_h / 2
    acct = (px + (157 + 115) * sx, py + (-3 + 24 + 10) * sy)
    pwd = (px + (157 + 115) * sx, py + (-3 + 59 + 10) * sy)
    ok = (px + (141 + 147) * sx, py + (-3 + 123 + 18) * sy)
    print("window rect=(%d,%d,%d,%d) mode=%s" % (rect.left, rect.top, rect.right, rect.bottom, mode))
    if mode == "tab":
        # click the panel background between the fields and the button, then
        # navigate with Tab (EnableTabChangeFocus=1 in LoginPassword.ini).
        click(cx, cy - 20, sw, sh)
        time.sleep(0.4)
        tap(VK_TAB)
        time.sleep(0.2)
        ctrl_a()
        type_text(account)
        time.sleep(0.2)
        tap(VK_TAB)
        time.sleep(0.2)
        ctrl_a()
        type_text(password)
        time.sleep(0.2)
        tap(VK_TAB)
        time.sleep(0.2)
        tap(VK_RETURN)
        print("done (tab mode)")
        return 0
    print("account=%s password=%s ok=%s" % (acct, pwd, ok))
    click(acct[0], acct[1], sw, sh)
    time.sleep(0.3)
    ctrl_a()
    type_text(account)
    time.sleep(0.2)
    tap(VK_TAB)
    time.sleep(0.2)
    ctrl_a()
    type_text(password)
    time.sleep(0.3)
    click(ok[0], ok[1], sw, sh)
    time.sleep(0.3)
    tap(VK_RETURN)
    print("done")
    return 0


if __name__ == "__main__":
    sys.exit(main())
