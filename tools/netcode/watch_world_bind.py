"""V2 P3: external reader of the world-bind state (player+0x60 / state / scene cells).

Polls the running raw client through ReadProcessMemory (0x438 rights, proven recipe from
watch_game_mgr.py) and reports the bind chain set by S2C id 188 (OnSyncRoleDataOver):

  client      = [JX3ClientX64.exe+0xA755B8]
  map/region  = client+0x14 / client+0x18
  registry    = [client+0x5673D8]   (player map head @+0x20, scene map head @+8)
  node        : left @+0, parent @+8, right @+0x10, color @+0x19, key dword @+0x20,
                value qword @+0x28 (player map); scene tree value = scene object whose
                +0x10/+0x14 hold (map, region) with region 0 = wildcard
  player      : +0x60 scene, +0xFDC state, +0x10/+0x14/+0x18 pos
  scene       : +0x64 radius, +0x790/+0x794 dims, +0x20DCC/+0x20DD0 stored cell,
                +0x7a8 cell grid ([index = y*128+x] cell ptr; cell +0x44 loaded,
                +0x3c/+0x48 touch stamps)

Usage: python watch_world_bind.py [--secs 60] [--interval 0.5] [--log C:\\jx3tmp\\bind.txt]
"""
import ctypes
import ctypes.wintypes as w
import struct
import sys
import time

k32 = ctypes.windll.kernel32
TH32CS_SNAPPROCESS = 0x2
TH32CS_SNAPMODULE = 0x8
RIGHTS = 0x438
CLIENT_GLOBAL_RVA = 0xA755B8
REG_OFF = 0x5673D8


class PROCESSENTRY32(ctypes.Structure):
    _fields_ = [("dwSize", w.DWORD), ("cntUsage", w.DWORD), ("th32ProcessID", w.DWORD),
                ("th32DefaultHeapID", ctypes.POINTER(ctypes.c_ulong)), ("th32ModuleID", w.DWORD),
                ("cntThreads", w.DWORD), ("th32ParentProcessID", w.DWORD),
                ("pcPriClassBase", ctypes.c_long), ("dwFlags", w.DWORD),
                ("szExeFile", ctypes.c_char * 260)]


class MODULEENTRY32(ctypes.Structure):
    _fields_ = [("dwSize", w.DWORD), ("th32ModuleID", w.DWORD), ("th32ProcessID", w.DWORD),
                ("GlblcntUsage", w.DWORD), ("ProccntUsage", w.DWORD),
                ("modBaseAddr", ctypes.POINTER(ctypes.c_byte)), ("modBaseSize", w.DWORD),
                ("hModule", w.HMODULE), ("szModule", ctypes.c_char * 256),
                ("szExePath", ctypes.c_char * 260)]


def find_client_pid():
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    pe = PROCESSENTRY32()
    pe.dwSize = ctypes.sizeof(PROCESSENTRY32)
    pid = None
    if k32.Process32First(snap, ctypes.byref(pe)):
        while True:
            if pe.szExeFile.decode("gb18030", "replace").lower() == "jx3clientx64.exe":
                pid = pe.th32ProcessID
            if not k32.Process32Next(snap, ctypes.byref(pe)):
                break
    k32.CloseHandle(snap)
    return pid


def module_base(pid, prefix):
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid)
    me = MODULEENTRY32()
    me.dwSize = ctypes.sizeof(MODULEENTRY32)
    base = None
    if k32.Module32First(snap, ctypes.byref(me)):
        while True:
            if me.szModule.decode("gb18030", "replace").lower().startswith(prefix):
                base = ctypes.cast(me.modBaseAddr, ctypes.c_void_p).value
                break
            if not k32.Module32Next(snap, ctypes.byref(me)):
                break
    k32.CloseHandle(snap)
    return base


class Reader(object):
    def __init__(self, h):
        self.h = h
        self.cache = {}

    def mem(self, addr, size):
        key = (addr, size)
        if key in self.cache:
            return self.cache[key]
        buf = ctypes.create_string_buffer(size)
        n = ctypes.c_size_t()
        if k32.ReadProcessMemory(self.h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)) and n.value == size:
            v = buf.raw
        else:
            v = None
        self.cache[key] = v
        return v

    def u32(self, addr):
        d = self.mem(addr, 4)
        return struct.unpack("<I", d)[0] if d else None

    def i32(self, addr):
        d = self.mem(addr, 4)
        return struct.unpack("<i", d)[0] if d else None

    def u64(self, addr):
        d = self.mem(addr, 8)
        return struct.unpack("<Q", d)[0] if d else None

    def u8(self, addr):
        d = self.mem(addr, 1)
        return d[0] if d else None

    def clear(self):
        self.cache.clear()


def is_node(r, p):
    if not p:
        return False
    c = r.u8(p + 0x19)
    return c == 0


def tree_walk(r, head, limit=4096):
    """Yield (key, value, node) for real nodes of an MSVC std::map (left@0, right@0x10)."""
    out = []
    if not head:
        return out
    root = r.u64(head + 8)
    stack = []
    node = root
    while (node or stack) and len(out) < limit:
        while is_node(r, node):
            stack.append(node)
            node = r.u64(node)
        if stack:
            node = stack.pop()
            out.append((r.u32(node + 0x20), r.u64(node + 0x28), node))
            node = r.u64(node + 0x10)
        else:
            break
    return out


def player_lookup(r, head, pid):
    """Replica of 0x140174D10: lower_bound on key @+0x20, return value @+0x28."""
    if not head:
        return None
    root = r.u64(head + 8)
    cand = head
    node = root
    while is_node(r, node):
        k = r.u32(node + 0x20) or 0
        if k < pid:
            node = r.u64(node + 0x10)
        else:
            cand = node
            node = r.u64(node)
    if not is_node(r, cand):
        return None
    k = r.u32(cand + 0x20) or 0
    if pid < k:
        return None
    return r.u64(cand + 0x28)


def fmt(v, width=8):
    if v is None:
        return "None"
    return hex(v)


def snapshot(r, base):
    client = r.u64(base + CLIENT_GLOBAL_RVA)
    if not client:
        return None
    info = {"client": client}
    info["local_id"] = r.u32(client + 4)
    info["map"] = r.u32(client + 0x14)
    info["region"] = r.u32(client + 0x18)
    info["skip"] = r.u64(client + 0x1B108)
    reg = r.u64(client + REG_OFF)
    info["reg"] = reg
    player = None
    scene = None
    if reg:
        player = player_lookup(r, reg + 0x20, info["local_id"] or 0)
        for k, v, node in tree_walk(r, reg + 8):
            if not v:
                continue
            m, rg = r.u32(v + 0x10), r.u32(v + 0x14)
            if m == info["map"] and (rg == info["region"] or rg == 0):
                scene = v
                break
    info["player"] = player
    info["scene"] = scene
    if player:
        info["p60"] = r.u64(player + 0x60)
        info["state"] = r.u32(player + 0xFDC)
        info["px"] = r.i32(player + 0x10)
        info["py"] = r.i32(player + 0x14)
        info["pz"] = r.i32(player + 0x18)
        info["minor"] = (r.u32(player + 0x2C), r.u32(player + 0x30))
    if scene:
        info["sc64"] = r.u32(scene + 0x64)
        info["dims"] = (r.i32(scene + 0x790), r.i32(scene + 0x794))
        info["cell"] = (r.i32(scene + 0x20DCC), r.i32(scene + 0x20DD0))
        if player:
            cx, cy = (info.get("px") or 0) >> 11, (info.get("py") or 0) >> 11
            cells = []
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    x, y = cx + dx, cy + dy
                    idx = y * 128 + x
                    ptr = r.u64(scene + 0x7A8 + idx * 8)
                    if ptr:
                        cells.append(((x, y), ptr, r.u32(ptr + 0x44), r.u32(ptr + 0x3C)))
            info["cells"] = cells
            info["cell_origin"] = (cx, cy)
    return info


def render(info):
    if not info:
        return "no client global"
    parts = []
    parts.append("map=%s reg=%s id=%s state=%s" % (info.get("map"), info.get("region"),
                                                   info.get("local_id"), info.get("state")))
    parts.append("player=%s p60=%s pos=(%s,%s,%s) minor=%s" % (
        fmt(info.get("player")), fmt(info.get("p60")), info.get("px"), info.get("py"),
        info.get("pz"), info.get("minor")))
    parts.append("scene=%s dims=%s cell=%s stored=%s" % (
        fmt(info.get("scene")), info.get("dims"), info.get("cell_origin"), info.get("cell")))
    cells = info.get("cells")
    if cells:
        desc = ", ".join("%s->%s load=%s" % (c[0], fmt(c[1]), c[2]) for c in cells[:9])
        parts.append("cells(%d): %s" % (len(cells), desc))
    return "\n  ".join(parts)


def main():
    secs, interval, logpath = 60.0, 0.5, None
    args = sys.argv[1:]
    i = 0
    while i < len(args):
        if args[i] == "--secs" and i + 1 < len(args):
            secs = float(args[i + 1]); i += 2
        elif args[i] == "--interval" and i + 1 < len(args):
            interval = float(args[i + 1]); i += 2
        elif args[i] == "--log" and i + 1 < len(args):
            logpath = args[i + 1]; i += 2
        else:
            i += 1
    pid = find_client_pid()
    if not pid:
        print("no JX3ClientX64.exe")
        return 1
    base = module_base(pid, "jx3client")
    hp = k32.OpenProcess(RIGHTS, False, pid)
    if not hp:
        print("OpenProcess failed err=%d" % ctypes.get_last_error())
        return 1
    print("watching pid=%d base=0x%X" % (pid, base), flush=True)
    logf = open(logpath, "a", encoding="utf-8") if logpath else None
    t0 = time.time()
    last = None
    while time.time() - t0 < secs:
        r = Reader(hp)
        info = snapshot(r, base)
        txt = render(info)
        if txt != last:
            last = txt
            line = "[%.1f] %s" % (time.time() - t0, txt)
            print(line, flush=True)
            if logf:
                logf.write(line + "\n")
                logf.flush()
        time.sleep(interval)
    if logf:
        logf.close()
    print("done", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
