"""V2 P4: scripted movement input for the world-movement test (no user mouse/keyboard).

Finds the running client's KGWin32App window, focuses it, and drives W/A/S/D/SPACE with
either posted window messages (--mode post) or synthesized system input (--mode sendinput,
default; many 3D clients read the async/raw keyboard state and ignore posted messages).

Watch the game stub log for the C2S move op: DoMoveExteriorRequest = wire id 0x1E8
(stub prints `proto=232`, its low byte), jump ids (DoCharacterJump) and 0xBF
(DoMoveViewPointRequest). The bind watcher (watch_world_bind.py) shows `player+0x10/14`
changing.

Usage:
  python drive_move.py [--mode post|sendinput] [--key w] [--hold 2.0] [--sequence default]
"""
import ctypes
import ctypes.wintypes as w
import sys
import time

u32 = ctypes.windll.user32
WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101
KEYEVENTF_KEYUP = 0x0002
KEYEVENTF_SCANCODE = 0x0008
INPUT_KEYBOARD = 1

SCAN = {"w": 0x11, "a": 0x1E, "s": 0x1F, "d": 0x20, "space": 0x39,
        "q": 0x10, "e": 0x12}
VK = {"w": 0x57, "a": 0x41, "s": 0x53, "d": 0x44, "space": 0x20,
      "q": 0x51, "e": 0x45}


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", w.WORD), ("wScan", w.WORD), ("dwFlags", w.DWORD),
                ("time", w.DWORD), ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))]


class INPUT(ctypes.Structure):
    class _U(ctypes.Union):
        _fields_ = [("ki", KEYBDINPUT), ("pad", ctypes.c_byte * 32)]

    _anonymous_ = ("u",)
    _fields_ = [("type", w.DWORD), ("u", _U)]


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


def focus(hwnd):
    """Gentle foreground attempt (no minimize/restore - that can kill fullscreen clients).

    Focus is NOT required for the game to see keys: the engine polls GetAsyncKeyState, which
    reflects the global async key state produced by SendInput regardless of the foreground
    window. This only improves the odds that the client also gets mouse activation."""
    k32 = ctypes.windll.kernel32
    SW_RESTORE = 9
    u32.AllowSetForegroundWindow(0xFFFFFFFF)
    for _ in range(3):
        if u32.GetForegroundWindow() == hwnd:
            return hwnd
        if u32.IsIconic(hwnd):
            u32.ShowWindow(hwnd, SW_RESTORE)
        fg = u32.GetForegroundWindow()
        tid_fg = u32.GetWindowThreadProcessId(fg, None)
        tid_us = k32.GetCurrentThreadId()
        u32.AttachThreadInput(tid_us, tid_fg, True)
        u32.SetForegroundWindow(hwnd)
        u32.AttachThreadInput(tid_us, tid_fg, False)
        time.sleep(0.4)
    return u32.GetForegroundWindow()


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", w.LONG), ("dy", w.LONG), ("mouseData", w.DWORD),
                ("dwFlags", w.DWORD), ("time", w.DWORD),
                ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))]


class MOUSE_INPUT(ctypes.Structure):
    class _U(ctypes.Union):
        _fields_ = [("mi", MOUSEINPUT), ("pad", ctypes.c_byte * 32)]

    _anonymous_ = ("u",)
    _fields_ = [("type", w.DWORD), ("u", _U)]


def click_center(hwnd):
    """Left-click the window center via SendInput (activates 3D input focus on some clients)."""
    r = w.RECT()
    u32.GetWindowRect(hwnd, ctypes.byref(r))
    x = (r.left + r.right) // 2
    y = (r.top + r.bottom) // 2
    SW = 3840
    SH = 2160
    ax = int(x * 65535 / (SW - 1))
    ay = int(y * 65535 / (SH - 1))
    MOUSEEVENTF_MOVE = 0x0001
    MOUSEEVENTF_ABSOLUTE = 0x8000
    MOUSEEVENTF_LEFTDOWN = 0x0002
    MOUSEEVENTF_LEFTUP = 0x0004
    for flags in (MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE,
                  MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTDOWN,
                  MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_LEFTUP):
        inp = MOUSE_INPUT()
        inp.type = 0
        inp.mi = MOUSEINPUT(ax, ay, 0, flags, 0, None)
        u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(MOUSE_INPUT))
        time.sleep(0.08)


def post_key(hwnd, key, down):
    vk = VK[key]
    scan = SCAN[key]
    if down:
        u32.PostMessageW(hwnd, WM_KEYDOWN, vk, (scan << 16) | 1)
    else:
        u32.PostMessageW(hwnd, WM_KEYUP, vk, (scan << 16) | 0xC0000001)


def send_key(key, down):
    inp = INPUT()
    inp.type = INPUT_KEYBOARD
    inp.ki = KEYBDINPUT(0, SCAN[key], KEYEVENTF_SCANCODE | (KEYEVENTF_KEYUP if not down else 0), 0, None)
    u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))


def drive(mode, hwnd, key, hold):
    down = (lambda k: post_key(hwnd, k, True)) if mode == "post" else (lambda k: send_key(k, True))
    up = (lambda k: post_key(hwnd, k, False)) if mode == "post" else (lambda k: send_key(k, False))
    print("  %s down (hold %.1fs)" % (key, hold), flush=True)
    down(key)
    time.sleep(hold)
    up(key)
    time.sleep(0.25)


def main():
    mode = "sendinput"
    sequence = "default"
    key = None
    hold = 2.0
    click = False
    do_focus = False
    args = sys.argv[1:]
    i = 0
    while i < len(args):
        if args[i] == "--mode" and i + 1 < len(args):
            mode = args[i + 1]; i += 2
        elif args[i] == "--sequence" and i + 1 < len(args):
            sequence = args[i + 1]; i += 2
        elif args[i] == "--key" and i + 1 < len(args):
            key = args[i + 1]; i += 2
        elif args[i] == "--hold" and i + 1 < len(args):
            hold = float(args[i + 1]); i += 2
        elif args[i] == "--click":
            click = True; i += 1
        elif args[i] == "--focus":
            do_focus = True; i += 1
        else:
            i += 1
    wins = find_window()
    if not wins:
        print("no KGWin32App window")
        return 1
    hwnd = wins[0]
    fg = focus(hwnd) if do_focus else u32.GetForegroundWindow()
    print("window hwnd=%s focus now=%s mode=%s" % (hwnd, fg, mode), flush=True)
    if do_focus and fg != hwnd:
        print("note: focus not held; SendInput still feeds GetAsyncKeyState (global async state)")
    if click:
        click_center(hwnd)
        print("clicked window center (activation)", flush=True)
        time.sleep(0.4)
    if key:
        drive(mode, hwnd, key, hold)
    elif sequence == "default":
        for k, h in (("w", 2.0), ("a", 1.0), ("d", 1.0), ("s", 1.0), ("space", 0.2)):
            drive(mode, hwnd, k, h)
    print("input sequence done", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
