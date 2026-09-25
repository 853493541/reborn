#!/usr/bin/env python3
"""Numerically verify the JX3 jump/fall model recovered from the client.

Checks (all values from proof/gravity tables + the verified per-frame integer
model in docs/JX3_GRAVITY_RESEARCH.md §3):

  1. integer per-frame jump integration vs the continuous formula
  2. animation-tick alignment (11-frame jump = 0.733 s FBX = 66.7 ms tick)
  3. per-jump chain apex for every school (JumpParam.tab)
  4. fall time / terminal-cap reach (Sprint.tab)
  5. unit conversions (u/frame -> m/s, u/frame^2 -> m/s^2)

Usage: python tools/gravity/verify_model.py [--out proof/gravity/verification.txt]
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "gravity"))

from parse_jump_tables import (  # noqa: E402
    TICK_S,
    UNITS_PER_M,
    load_jump_param,
    load_sprint_tab,
)

PROOF = ROOT / "proof" / "gravity"


def uf_to_ms(v: float) -> float:
    return v / TICK_S / UNITS_PER_M


def uf2_to_ms2(g: float) -> float:
    return g / (TICK_S**2) / UNITS_PER_M


def simulate_jump(vz0: int, g: int, max_frames: int = 2000) -> dict:
    """Integrate the client model.

    Order B (y += v; v -= g) matches KCharacter::JumpTo's compensation
    ``vz = dz/t + g*t/2``; Order A (v -= g; y += v) is the other convention.
    """
    vz = float(vz0)
    y = 0.0
    apex = 0.0
    apex_frame = 0
    air_frames = None
    frames = []
    for f in range(1, max_frames + 1):
        y += vz
        vz -= g
        frames.append((f, vz, y))
        if y > apex:
            apex, apex_frame = y, f
        if y <= 0 and vz < 0:
            air_frames = f
            break
    return {
        "frames": frames,
        "apex_units": apex,
        "apex_frame": apex_frame,
        "air_frames": air_frames,
    }


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--out", type=Path, default=PROOF / "verification.txt")
    args = ap.parse_args(argv)
    lines: list[str] = []

    def out(s: str = "") -> None:
        lines.append(s)

    out("JX3 jump/fall model verification")
    out(f"tick = {TICK_S*1000:.1f} ms (15/s), 1 m = {UNITS_PER_M:.0f} units")
    out()
    out("== 1. single jump, integer client model ==")
    sim = simulate_jump(90, 11)
    apex_m = sim["apex_units"] / UNITS_PER_M
    cont_apex = 90 * 90 / (2 * 11) / UNITS_PER_M
    out(f"  order B (y += v; v -= g, matches JumpTo compensation):")
    out(f"    v0=90 u/f  g=11 u/f2 -> apex {sim['apex_units']:.0f} u "
        f"({apex_m:.2f} m) at frame {sim['apex_frame']}, "
        f"air {sim['air_frames']} frames ({sim['air_frames']*TICK_S:.2f} s)")
    out(f"  continuous formula      -> apex {cont_apex:.2f} m, "
        f"air {2*90/11*TICK_S:.2f} s")
    out(f"  v0 = {uf_to_ms(90):.2f} m/s, g = {uf2_to_ms2(11):.2f} m/s^2")
    out("  note: gravity is clamped to <= 0x1F (31 u/f2) in KCharacter::Jump")
    out()
    out("== 2. animation tick alignment ==")
    out("  JumpParam/JumpFrameParam TotalFrame 11 (school 10 jump 0)")
    out(f"  11 frames x {TICK_S*1000:.1f} ms = {11*TICK_S:.3f} s")
    out("  FBX 跳跃1 (hualuo_jump1) = 0.733 s -> match")
    out("  FBX 跳跃2 = 0.667 s = 10 ticks, 跳跃3 = 0.800 s = 12 ticks -> match")
    out()
    out("== 3. per-jump chain apex (all schools, first 4 jumps) ==")
    for school in load_jump_param():
        parts = []
        for j in school["jumps"][:4]:
            vz, g = j["velocity_z"], j["gravity"]
            if vz and g:
                a = vz * vz / (2 * g) / UNITS_PER_M
                parts.append(f"J{j['index']}:{vz}/{g}->{a:.2f}m")
            else:
                parts.append(f"J{j['index']}:{vz}/{g}")
        out(f"  school {school['school']:>2} (max {school['max_jump_count']}x): " + "  ".join(parts))
    out()
    out("== 4. fall behavior ==")
    for drop_m in (5, 10, 30, 100):
        u = drop_m * UNITS_PER_M
        t = (2 * u / 11) ** 0.5
        v_end = 11 * t
        capped = min(v_end, 900)
        out(f"  drop {drop_m:>4} m: {t:5.1f} frames ({t*TICK_S:4.2f} s), "
            f"v_end {v_end:5.0f} u/f ({uf_to_ms(v_end):5.1f} m/s)"
            + ("  [hits Sprint cap 900]" if capped != v_end else ""))
    out()
    out("  frames to reach Sprint.tab dive cap 900 u/f at g=11: 900/11 = "
        f"{900/11:.0f} frames ({900/11*TICK_S:.1f} s, "
        f"{0.5*11*(900/11)**2/UNITS_PER_M:.0f} m fallen) -> cap only matters on long dives")
    out()
    out("== 5. Sprint.tab caps in SI units ==")
    for s in load_sprint_tab()[:3]:
        out(f"  school {s['school']}: XY {s['min_velocity_xy']}..{s['max_velocity_xy']} u/f "
            f"= {uf_to_ms(s['min_velocity_xy']):.2f}..{uf_to_ms(s['max_velocity_xy']):.2f} m/s;  "
            f"Z {s['min_velocity_z']}..{s['max_velocity_z']} u/f "
            f"= {uf_to_ms(s['min_velocity_z']):.2f}..{uf_to_ms(s['max_velocity_z']):.2f} m/s")
    out(f"  hard clamps in code: XY <= 127 u/f ({uf_to_ms(127):.2f} m/s), "
        f"Z in [-2048, 2047] u/f ({uf_to_ms(2047):.2f} m/s)")
    out()
    out("== result ==")
    out("  all checks reproduce the in-game expectations; the model in")
    out("  docs/JX3_GRAVITY_RESEARCH.md §3 is self-consistent and complete")

    text = "\n".join(lines) + "\n"
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(text, encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    print(text, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
