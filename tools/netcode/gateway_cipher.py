"""V2 P2: gateway transport stream cipher (JX3ClientX64 exe+0x7A2590).

Reverse-engineered from the client's send/receive transform callbacks:
  state r10d = [inner+0x3c] (send) / [inner+0x40] (receive) - same value for both,
  seeded from the 42-byte connect hello fields. For our constant zero-hello the
  observed state is 0xFFFFFFC9 (probe poll, passive run).
  per packet of N bytes: words = N>>2, tail = N&3; keystream word k (k = words-1..0):
      idx = (state + k) % 0x5679
      state = TABLE[idx] + 0x2E6D23C1
      payload_word ^= state
  tail bytes: state ^= TABLE[tail]; then per byte: payload_byte ^= state&0xFF; state >>= 8
  The state is NOT persisted between packets (each packet re-derives from state0).
  XOR is symmetric: same function encrypts and decrypts.

Table source: JX3ClientX64.exe VA 0x140A34530, 0x5679 dwords (read-only).
"""
import struct

import pefile

TABLE_VA = 0x140A34530
DIVISOR = 0x162F
CIPHER_ADD = 0x2E6D23C1
STATE0 = 0xC9FFFFFF

_table = None


def load_table(exe_path):
    global _table
    pe = pefile.PE(exe_path, fast_load=True)
    base = pe.OPTIONAL_HEADER.ImageBase
    data = pe.get_data(TABLE_VA - base, DIVISOR * 4)
    if len(data) != DIVISOR * 4:
        raise ValueError("cipher table read failed")
    _table = struct.unpack("<%dI" % DIVISOR, data)
    return _table


def cipher(buf, state=STATE0):
    if _table is None:
        raise RuntimeError("load_table() first")
    buf = bytearray(buf)
    r10 = state & 0xFFFFFFFF
    n = len(buf)
    words = n >> 2
    tail = n & 3
    i = 0
    r11 = words
    while r11 > 0:
        r11 -= 1
        idx = (r10 + r11) % DIVISOR
        r10 = (_table[idx] + CIPHER_ADD) & 0xFFFFFFFF
        v = struct.unpack_from("<I", buf, i)[0] ^ r10
        struct.pack_into("<I", buf, i, v)
        i += 4
    r10 ^= _table[tail % DIVISOR]
    for _ in range(tail):
        buf[i] ^= (r10 & 0xFF)
        i += 1
        r10 >>= 8
    return bytes(buf)


GAME_CIPHER_MUL = 0x1F
GAME_CIPHER_ADD = 0x8088405


def game_cipher(buf, state):
    """Game-transport cipher (exe+0x7A26D0): table lookup once, linear keystream, and
    the state PERSISTS (updated per packet: state = state*0x1F + 0x8088405 mod 2^32).

    Returns (bytes, next_state).
    """
    if _table is None:
        raise RuntimeError("load_table() first")
    buf = bytearray(buf)
    n = len(buf)
    words = n >> 2
    tail = n & 3
    eax = _table[(state + words) % DIVISOR]
    i = 0
    rcx = words
    while rcx > 0:
        rcx -= 1
        eax = (eax + CIPHER_ADD + rcx) & 0xFFFFFFFF
        v = struct.unpack_from("<I", buf, i)[0] ^ eax
        struct.pack_into("<I", buf, i, v)
        i += 4
    eax = (eax + CIPHER_ADD) & 0xFFFFFFFF
    for _ in range(tail):
        buf[i] ^= (eax & 0xFF)
        i += 1
        eax >>= 8
    next_state = (state * GAME_CIPHER_MUL + GAME_CIPHER_ADD) & 0xFFFFFFFF
    return bytes(buf), next_state
