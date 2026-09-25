#!/usr/bin/env python3
"""Parse the real JX3 jump/gravity tables and replay calibrated trajectories.

Reads the extracted game tables:

  settings/JumpParam.tab        per-school jump constants (VelocityZ, Gravity,
                                JumpSpeedXY, wall/horse/dash variants)
  settings/JumpFrameParam.tab   per-(school, jump count, double player)
                                per-frame velocity keyframes (the real 轻功 arcs)

Calibration (see docs/JX3_GRAVITY_RESEARCH.md):
  * logic tick = 66.7 ms (15 ticks/s) -- 11-frame jump animation == 0.733 s
  * 1 m = 192 units (1 尺 = 64 units)
  * single jump: VelocityZ=90 u/tick, Gravity=11 u/tick^2
      -> v0 = 90*15/192 = 7.03 m/s, g = 11*15*15/192 = 12.89 m/s^2,
         apex ~2.1 m, matches the in-game 跳跃

Usage:
  python tools/gravity/parse_jump_tables.py --summary
  python tools/gravity/parse_jump_tables.py --school 0 --json proof/gravity/parsed/jump.json
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROOF = ROOT / "proof" / "gravity"

TICK_S = 1.0 / 15.0
UNITS_PER_M = 192.0
UNITS_PER_CM = UNITS_PER_M / 100.0

JUMP_PARAM = PROOF / "JumpParam.tab"
JUMP_FRAME_PARAM = PROOF / "JumpFrameParam.tab"
SPRINT_TAB = PROOF / "Sprint.tab"
SKILLMOVE_TAB = PROOF / "SkillMove.tab"

MAX_JUMP = 24
MAX_VELOCITY_XY = 127
MAX_VELOCITY_Z = 2047


def _read_tsv(path: Path) -> tuple[list[str], list[list[str]]]:
    data = path.read_bytes()
    text = data.decode("gb18030", errors="replace")
    lines = [line.rstrip("\r") for line in text.splitlines() if line.strip()]
    header = lines[0].split("\t")
    rows = [line.split("\t") for line in lines[1:]]
    return header, rows


def _i(cells: list[str], idx: int) -> int | None:
    if idx >= len(cells) or not cells[idx].strip():
        return None
    try:
        return int(cells[idx])
    except ValueError:
        return None


def load_jump_param(path: Path = JUMP_PARAM) -> list[dict]:
    header, rows = _read_tsv(path)
    schools = []
    for cells in rows:
        sid = _i(cells, 0)
        if sid is None:
            continue
        school = {
            "school": sid,
            "max_jump_count": _i(cells, 1),
            "weapon_mask": _i(cells, 2),
            "jumps": [],
            "wall": [],
            "horse": [],
            "dash": None,
        }
        base = 3
        for j in range(MAX_JUMP):
            vxy = _i(cells, base + j * 6)
            vz = _i(cells, base + j * 6 + 1)
            g = _i(cells, base + j * 6 + 2)
            vxy_e = _i(cells, base + j * 6 + 3)
            vz_e = _i(cells, base + j * 6 + 4)
            g_e = _i(cells, base + j * 6 + 5)
            if vxy is None and vz is None and g is None:
                continue
            school["jumps"].append(
                {
                    "index": j,
                    "jump_speed_xy": vxy,
                    "velocity_z": vz,
                    "gravity": g,
                    "jump_speed_xy_end": vxy_e,
                    "velocity_z_end": vz_e,
                    "gravity_end": g_e,
                }
            )
        wall_base = 3 + MAX_JUMP * 6
        for j in range(4):
            vxy = _i(cells, wall_base + j * 3)
            vz = _i(cells, wall_base + j * 3 + 1)
            g = _i(cells, wall_base + j * 3 + 2)
            if vxy is None and vz is None and g is None:
                continue
            school["wall"].append({"index": j, "speed_xy": vxy, "velocity_z": vz, "gravity": g})
        horse_base = wall_base + 4 * 3
        for j in range(4):
            vxy = _i(cells, horse_base + j * 3)
            vz = _i(cells, horse_base + j * 3 + 1)
            g = _i(cells, horse_base + j * 3 + 2)
            if vxy is None and vz is None and g is None:
                continue
            school["horse"].append({"index": j, "speed_xy": vxy, "velocity_z": vz, "gravity": g})
        dash_base = horse_base + 4 * 3
        school["dash"] = {
            "dash_velocity": _i(cells, dash_base),
            "jump_speed_xy": _i(cells, dash_base + 1),
            "velocity_z": _i(cells, dash_base + 2),
            "gravity": _i(cells, dash_base + 3),
        }
        knocked_back_frame = header.index("KnockedBackFrame")
        school["knocked_back_frame"] = _i(cells, knocked_back_frame)
        school["knocked_back_speed"] = _i(cells, knocked_back_frame + 1)
        school["kick_range"] = _i(cells, knocked_back_frame + 2)
        schools.append(school)
    return schools


def load_jump_frame_param(path: Path = JUMP_FRAME_PARAM) -> list[dict]:
    header, rows = _read_tsv(path)
    out = []
    for cells in rows:
        sid = _i(cells, 0)
        if sid is None:
            continue
        entry = {
            "school": sid,
            "jump_count": _i(cells, 1),
            "total_frame": _i(cells, 2),
            "double_player": _i(cells, 3),
            "velocity_xy": [],
            "velocity_z": [],
            "direction_xy": [],
        }
        for i in range(128):
            base = 4 + i * 4
            frame = _i(cells, base)
            if frame is None:
                continue
            entry["velocity_xy"].append(_i(cells, base + 1))
            entry["velocity_z"].append(_i(cells, base + 2))
            entry["direction_xy"].append(_i(cells, base + 3))
        out.append(entry)
    return out


def integrate_curve(entry: dict) -> list[dict]:
    """Integrate per-frame velocity keyframes into a position curve.

    Single jump (jump_count 0) is ballistic: the curve is the real arc.
    Multi-jump curves (轻功) are scripted dive/launch/hover arcs; their
    absolute vertical scale still needs a live capture to calibrate (see
    research doc open items), so we report both raw and per-meter values.
    """
    frames = []
    y = 0.0
    x = 0.0
    vz = entry["velocity_z"]
    vxy = entry["velocity_xy"]
    for i in range(len(vz)):
        y += (vz[i] or 0)
        x += (vxy[i] or 0)
        frames.append(
            {
                "frame": i,
                "t_s": round(i * TICK_S, 4),
                "velocity_xy": vxy[i],
                "velocity_z": vz[i],
                "pos_y_units": y,
                "pos_x_units": x,
                "pos_y_m": round(y / UNITS_PER_M, 3),
                "pos_x_m": round(x / UNITS_PER_M, 3),
            }
        )
    return frames


def ballistics(jump: dict) -> dict:
    vz = jump["velocity_z"] or 0
    g = jump["gravity"] or 0
    apex_units = None
    apex_t_s = None
    land_t_s = None
    v0_ms = vz / TICK_S / UNITS_PER_M if vz else None
    g_ms2 = g / TICK_S**2 / UNITS_PER_M if g else None
    if vz and g:
        apex_units = round(vz * vz / (2.0 * g), 1)
        apex_t_s = round(vz / g * TICK_S, 3)
        land_t_s = round(2.0 * vz / g * TICK_S, 3)
    return {
        "velocity_z_units_per_tick": vz,
        "gravity_units_per_tick2": g,
        "v0_m_s": round(v0_ms, 3) if v0_ms else None,
        "g_m_s2": round(g_ms2, 3) if g_ms2 else None,
        "apex_units": apex_units,
        "apex_m": round(apex_units / UNITS_PER_M, 3) if apex_units is not None else None,
        "time_to_apex_s": apex_t_s,
        "air_time_s": land_t_s,
    }


def load_sprint_tab(path: Path = SPRINT_TAB) -> list[dict]:
    """Fall/sprint velocity caps per school (settings/Sprint.tab).

    Loader `KGJumpList::LoadSprintTab` stores 16-bit words per school and
    asserts MinVelocityXY >= 1, MaxVelocityXY <= 127 (MAX_VELOCITY_XY),
    MinVelocityZ >= 1, MaxVelocityZ < 2047 (MAX_VELOCITY_Z).
    """
    header, rows = _read_tsv(path)
    out = []
    for cells in rows:
        sid = _i(cells, 0)
        if sid is None:
            continue
        out.append(
            {
                "school": sid,
                "min_velocity_xy": _i(cells, 1),
                "max_velocity_xy": _i(cells, 2),
                "min_velocity_z": _i(cells, 3),
                "max_velocity_z": _i(cells, 4),
                "ani_frame": _i(cells, 5),
                "search_direction": _i(cells, 6),
            }
        )
    return out


def load_skill_move_flags(path: Path = SKILLMOVE_TAB) -> list[dict]:
    """Per-skill-move gravity/death flags from settings/SkillMove.tab."""
    header, rows = _read_tsv(path)
    out = []
    for cells in rows:
        sid = _i(cells, 0)
        if sid is None:
            continue
        out.append(
            {
                "skill_move_id": sid,
                "ignore_gravity": _i(cells, 1),
                "total_frame": _i(cells, 2),
                "only_fly": _i(cells, 4),
                "skill_move_death": _i(cells, 5),
                "end_but_keep_velocity": _i(cells, 6),
                "can_jump": _i(cells, 7),
            }
        )
    return out


def summary() -> None:
    print(f"logic tick: {TICK_S*1000:.1f} ms (15/s)   1 m = {UNITS_PER_M:.0f} units")
    print()
    schools = load_jump_param()
    print("JumpParam.tab -- single jump (jump 0) calibration per school:")
    print(f"{'school':>7} {'Vz':>5} {'G':>4} {'v0 m/s':>8} {'g m/s^2':>9} {'apex m':>8} {'air s':>7}")
    for s in schools:
        j0 = s["jumps"][0]
        b = ballistics(j0)
        print(
            f"{s['school']:>7} {j0['velocity_z']:>5} {j0['gravity']:>4} "
            f"{b['v0_m_s']:>8} {b['g_m_s2']:>9} {b['apex_m']:>8} {b['air_time_s']:>7}"
        )
    print()
    print("JumpFrameParam.tab curves:")
    for e in load_jump_frame_param():
        frames = integrate_curve(e)
        peak = max(frames, key=lambda f: f["pos_y_units"])
        trough = min(frames, key=lambda f: f["pos_y_units"])
        print(
            f"  school {e['school']:>2} jump {e['jump_count']} double {e['double_player']}: "
            f"frames {len(frames)} ({len(frames)*TICK_S:.2f} s) "
            f"y [{trough['pos_y_units']}..{peak['pos_y_units']}] units "
            f"x {frames[-1]['pos_x_units']} units"
        )
    print()
    print(f"Sprint.tab -- fall/sprint caps (MAX_VELOCITY_XY={MAX_VELOCITY_XY}, MAX_VELOCITY_Z={MAX_VELOCITY_Z}):")
    print(f"{'school':>7} {'minXY':>6} {'maxXY':>6} {'minZ':>5} {'maxZ':>5} {'aniFrame':>9} {'search':>7}")
    for s in load_sprint_tab():
        print(
            f"{s['school']:>7} {s['min_velocity_xy']:>6} {s['max_velocity_xy']:>6} "
            f"{s['min_velocity_z']:>5} {s['max_velocity_z']:>5} {s['ani_frame']:>9} {s['search_direction']:>7}"
        )
    moves = load_skill_move_flags()
    ignore = [m for m in moves if m["ignore_gravity"]]
    death = [m for m in moves if m["skill_move_death"]]
    print()
    print(f"SkillMove.tab: {len(moves)} rows, {len(ignore)} ignore-gravity moves, {len(death)} death moves")
    for m in death:
        print(f"  death move: id={m['skill_move_id']} frames={m['total_frame']} ignoreGravity={m['ignore_gravity']}")


def dump(school: int, out: Path) -> None:
    schools = load_jump_param()
    row = next((s for s in schools if s["school"] == school), None)
    if row is None:
        print(f"school {school} not found", file=sys.stderr)
        raise SystemExit(2)
    curves = [
        e for e in load_jump_frame_param() if e["school"] == school and e["double_player"] == 0
    ]
    result = {
        "tick_s": TICK_S,
        "units_per_m": UNITS_PER_M,
        "school": row,
        "jumps_calibrated": [ballistics(j) for j in row["jumps"]],
        "curves": [
            {"meta": {k: e[k] for k in ("school", "jump_count", "total_frame", "double_player")},
             "frames": integrate_curve(e)}
            for e in curves
        ],
    }
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(f"school {school}: {len(curves)} frame curves -> {out}")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--summary", action="store_true")
    ap.add_argument("--school", type=int, default=0)
    ap.add_argument("--json", type=Path)
    args = ap.parse_args(argv)

    if args.json:
        dump(args.school, args.json)
    else:
        summary()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
