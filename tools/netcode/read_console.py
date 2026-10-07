"""Read a process's console screen buffer (read-only AttachConsole).

Usage: python read_console.py <pid>
"""
import ctypes
import ctypes.wintypes as w
import sys

k32 = ctypes.windll.kernel32

STD_OUTPUT_HANDLE = -11
GENERIC_READ = 0x80000000
GENERIC_WRITE = 0x40000000
FILE_SHARE_READ = 1
FILE_SHARE_WRITE = 2
OPEN_EXISTING = 3


class COORD(ctypes.Structure):
    _fields_ = [("X", ctypes.c_short), ("Y", ctypes.c_short)]


class SMALL_RECT(ctypes.Structure):
    _fields_ = [("Left", ctypes.c_short), ("Top", ctypes.c_short),
                ("Right", ctypes.c_short), ("Bottom", ctypes.c_short)]


class CONSOLE_SCREEN_BUFFER_INFO(ctypes.Structure):
    _fields_ = [("dwSize", COORD), ("dwCursorPosition", COORD),
                ("wAttributes", w.WORD), ("srWindow", SMALL_RECT),
                ("dwMaximumWindowSize", COORD)]


k32.AttachConsole.argtypes = [w.DWORD]
k32.CreateFileW.restype = ctypes.c_void_p
k32.CreateFileW.argtypes = [w.LPCWSTR, w.DWORD, w.DWORD, ctypes.c_void_p, w.DWORD, w.DWORD,
                            ctypes.c_void_p]
k32.GetConsoleScreenBufferInfo.argtypes = [ctypes.c_void_p,
                                           ctypes.POINTER(CONSOLE_SCREEN_BUFFER_INFO)]
k32.ReadConsoleOutputCharacterW.argtypes = [ctypes.c_void_p, w.LPWSTR, w.DWORD, COORD,
                                            ctypes.POINTER(w.DWORD)]
k32.ReadConsoleOutputCharacterW.restype = w.BOOL


def main():
    pid = int(sys.argv[1])
    k32.FreeConsole()
    if not k32.AttachConsole(pid):
        print("AttachConsole failed err=%d" % k32.GetLastError())
        return 1
    try:
        h = k32.CreateFileW("CONOUT$", GENERIC_READ | GENERIC_WRITE,
                            FILE_SHARE_READ | FILE_SHARE_WRITE, None, OPEN_EXISTING, 0, None)
        if h == -1 or h == 0xFFFFFFFFFFFFFFFF:
            print("CONOUT$ open failed err=%d" % k32.GetLastError())
            return 1
        info = CONSOLE_SCREEN_BUFFER_INFO()
        if not k32.GetConsoleScreenBufferInfo(h, ctypes.byref(info)):
            print("GetConsoleScreenBufferInfo failed")
            return 1
        width = info.dwSize.X
        height = info.dwSize.Y
        print("console buffer %dx%d cursor=(%d,%d)" % (width, height,
                                                       info.dwCursorPosition.X, info.dwCursorPosition.Y))
        nread = w.DWORD()
        lines = []
        buf = ctypes.create_unicode_buffer(width)
        for y in range(height):
            ok = k32.ReadConsoleOutputCharacterW(h, buf, width, COORD(0, y), ctypes.byref(nread))
            if not ok:
                break
            lines.append(buf[:nread.value].rstrip())
        # trim trailing blank lines
        while lines and not lines[-1]:
            lines.pop()
        for line in lines[-400:]:
            print(line)
        return 0
    finally:
        k32.FreeConsole()


if __name__ == "__main__":
    sys.exit(main())
