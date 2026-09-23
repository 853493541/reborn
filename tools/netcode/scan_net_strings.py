#!/usr/bin/env python3
"""Scan a JX3 binary for netcode / replication keywords and dump grouped hits.

Buckets are intentionally broad: transport, prediction/correction, protocol,
domain, and Chinese GBK client copy. Output is a text evidence file under
proof/netcode/ that docs/netcode/JX3_NETCODE_RESEARCH.md cites.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from extract_strings import extract  # noqa: E402

BUCKETS: dict[str, list[str]] = {
    "transport": [
        "WSA", "socket", "connect", "send", "recv", "TCP", "UDP", "KCP",
        "select", "bind", "listen", "getaddrinfo", "sockaddr", "inet_addr",
        "crypt", "encrypt", "compress", "packet", "channel",
    ],
    "prediction_correction": [
        "predict", "correct", "reconcil", "interp", "extrapol", "snapshot",
        "delta", "ack", "rollback", "lag", "rtt", "ping", "heartbeat",
        "latency", "tick", "replay", "rebase",
    ],
    "protocol": [
        "opcode", "protocol", "packet id", "msg id", "message id", "serialize",
        "marshal", "dispatch", "session", "gateway", "gate", "login server",
        "world server", "gameserver", "zone server", "reconnect", "timeout",
    ],
    "domain": [
        "Move", "StopMove", "Cast", "Skill", "Cooldown", "GCD", "Buff",
        "Damage", "Hit", "Sync", "Position", "Velocity", "Snapshot",
        "Actor", "Entity", "Player", "NPC", "Monster", "Combat",
    ],
    "cn_client": [
        "拉回", "位置", "修正", "延迟", "同步", "读条", "打断", "距离",
        "面向", "移动", "攻击", "服务器", "连接", "断线", "重连", "网络",
        "客户端", "延迟高", "武功", "冷却", "公共冷却", "无法释放", "超出范围",
    ],
}


def scan(path: Path, min_len: int = 4) -> dict[str, list[tuple[int, str, str]]]:
    hits: dict[str, list[tuple[int, str, str]]] = {k: [] for k in BUCKETS}
    for off, enc, s in extract(path, min_len):
        lowered = s.lower()
        for bucket, keys in BUCKETS.items():
            for kw in keys:
                if kw.isascii():
                    if kw.lower() in lowered:
                        hits[bucket].append((off, enc, s))
                        break
                elif kw in s:
                    hits[bucket].append((off, enc, s))
                    break
    return hits


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("binary", type=Path)
    ap.add_argument("--out", type=Path, required=True, help="evidence .txt path")
    ap.add_argument("--min-len", type=int, default=4)
    args = ap.parse_args(argv)

    if not args.binary.is_file():
        print(f"not a file: {args.binary}", file=sys.stderr)
        return 2

    hits = scan(args.binary, args.min_len)
    lines = [f"# {args.binary.name} netcode keyword scan", f"# source: {args.binary}", ""]
    total = 0
    for bucket, rows in hits.items():
        seen: set[tuple[int, str]] = set()
        uniq = []
        for off, enc, s in rows:
            key = (off, s)
            if key in seen:
                continue
            seen.add(key)
            uniq.append((off, enc, s))
        total += len(uniq)
        lines.append(f"## {bucket} ({len(uniq)})")
        if not uniq:
            lines.append("(none)")
        for off, enc, s in uniq:
            lines.append(f"0x{off:08X}\t{enc}\t{s}")
        lines.append("")

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text("\n".join(lines), encoding="utf-8", errors="replace")
    print(f"{args.binary.name}: {total} hits -> {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
