#!/usr/bin/env python3
"""Reference follow-camera for the reborn camera spec (docs/netcode/REBORN_CAMERA_SPEC.md).

Modeled on the JX3 findings: per-mode parameter rows, yaw/pitch mouse camera,
anchor + rotated offset, SmoothTime exponential smoothing with dead zone,
movement-reactive pitch/yaw, sprint pull-back, and a cinematic spring camera.

Run: python tools/netcode/reference/camera_model.py
"""
from __future__ import annotations

import json
import math
import random
import sys
from dataclasses import dataclass, field
from pathlib import Path

DEG = math.pi / 180.0
EPS = 1e-6
# verified: height/pitch state machine moves at pi/3000 rad per ms = 60 deg/s
PITCH_RATE = 0.0010471976 * 1000.0  # rad per second

MODE_CHARACTER = "character"
MODE_SPRINT = "sprint"
MODE_CARRIER = "carrier"
MODE_AIR_COMBAT = "air_combat"
MODE_NPC_DIALOG = "npc_dialog"
MODE_GOD = "god"

MODE_ROWS = {
    MODE_CHARACTER: {
        "CameraHeight": 2.0, "TargetDistance": 6.0, "SmoothTime": 0.08,
        "MaxDragSpeed": 60.0, "RotationSpeed": 60.0,
        # verified DLL defaults (proof/netcode/camera_defaults_verified.txt):
        "CameraMaxDeltaYaw": 2.0 * math.pi, "CameraMaxDeltaPitch": 1.56,
        "ForbidStrafe": False, "ForbidRotation": False,
        "TurnCameraYawToObjectYaw": False,
        "InitCameraPitch": -20.0 * DEG, "InitCameraAngle": 0.0, "InitCameraDistance": 6.0,
        "CameraMovePitchApplyAngle": -12.0 * DEG,
        "CameraMovePitchSmoothTime": 0.25,
        "CameraMovePitchAdjustPitch": -20.0 * DEG,
        "CameraMovePitchAdjustMaxPitch": -45.0 * DEG,
        "CameraMovePitchApplyMaxPitch": -30.0 * DEG,
        "CameraMovePitchApplyTimeInterval": 0.4,
        "CameraAdjustYawWhenMoveTurn": 1.0,
        # verified DLL default: 0.26 rad = ~15 deg
        "CameraAdjustYawWhenMoveTurnDisableAngle": 0.26,
    },
    MODE_SPRINT: {
        "CameraHeight": 2.2, "TargetDistance": 8.0, "SmoothTime": 0.15,
        "MaxDragSpeed": 70.0, "RotationSpeed": 70.0,
        "CameraMaxDeltaYaw": 0.5, "CameraMaxDeltaPitch": 0.35,
        "SprintCameraMinTrackBackSpeed": 4.0, "SprintCameraMaxTrackBackSpeed": 14.0,
        "SprintCameraTrackBackSpeedSlope": 0.5,
        "SprintCameraMaxDistance": 9.0, "SprintCameraSmoothTime": 0.3,
    },
    MODE_CARRIER: {
        "CameraHeight": 3.0, "TargetDistance": 7.0, "SmoothTime": 0.12,
        "CarrierCameraMaxDistance": 8.5, "CarrierCameraDeltaHeight": 1.0,
        "ForbidStrafe": True, "TurnCameraYawToObjectYaw": True,
    },
    MODE_AIR_COMBAT: {
        "CameraHeight": 1.0, "TargetDistance": 9.0, "SmoothTime": 0.2,
        "CameraMaxDeltaYaw": 0.6, "CameraMaxDeltaPitch": 0.45,
    },
    MODE_NPC_DIALOG: {
        "CameraHeight": 1.6, "TargetDistance": 2.5, "SmoothTime": 0.35,
        "ForbidRotation": True,
    },
    MODE_GOD: {
        "CameraHeight": 5.0, "TargetDistance": 10.0, "SmoothTime": 0.05,
        "MaxDragSpeed": 200.0, "RotationSpeed": 200.0,
    },
}


@dataclass
class CameraState:
    yaw: float = 0.0
    pitch: float = -20.0 * DEG
    distance: float = 6.0
    pos: tuple[float, float, float] = (0.0, 2.0, -6.0)
    offset: tuple[float, float, float] = (0.0, 0.0, 0.0)
    look: tuple[float, float, float] = (0.0, 1.5, 0.0)
    last_t: float = 0.0


class FollowCamera:
    def __init__(self, rows: dict = MODE_ROWS, state: CameraState | None = None,
                 config_path: str | Path | None = None) -> None:
        self.rows = {k: dict(v) for k, v in rows.items()}
        self.state = state or CameraState()
        if config_path is None:
            candidate = Path(__file__).with_name("camera.json")
            config_path = candidate if candidate.is_file() else None
        if config_path is not None:
            self.load_config(config_path)
        self.mode = MODE_CHARACTER
        self.sprint_speed = 0.0
        self.move_pitch_applied = False
        self.last_pitch_apply = 0.0
        self.t = 0.0
        self.follow_action_target: tuple[float, float, float] | None = None
        self.events: list[str] = []

    def load_config(self, path: str | Path) -> None:
        """Overlay real per-mode values from a JSON file (drop-in for the
        JX3 krl values that no longer ship with the client)."""
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        for mode, row in data.items():
            self.rows.setdefault(mode, {}).update(row)

    def save_template(self, path: str | Path) -> None:
        Path(path).write_text(json.dumps(self.rows, indent=2), encoding="utf-8")

    @property
    def row(self) -> dict:
        return self.rows[self.mode]

    def mouse(self, dx: float, dy: float, sens: float = 1.0) -> None:
        row = self.row
        if not row.get("ForbidRotation", False):
            self.state.yaw -= dx * sens
        if not row.get("ForbidStrafe", False):
            pass
        self.state.yaw = (self.state.yaw + math.pi) % (2 * math.pi) - math.pi
        max_pitch = row.get("CameraMaxDeltaPitch", 0.35)
        self.state.pitch = max(-math.pi / 2 + 0.05, min(math.pi / 2 - 0.05,
                                                        self.state.pitch - dy * sens))
        self.state.pitch = max(self.state.pitch - max_pitch, min(self.state.pitch + max_pitch,
                                                                self.state.pitch))

    def switch_mode(self, mode: str, smooth: bool = True) -> None:
        if mode not in self.rows:
            raise KeyError(mode)
        self.events.append(f"leave:{self.mode}")
        self.mode = mode
        self.events.append(f"enter:{mode}")
        row = self.row
        if smooth:
            pass
        if "InitCameraPitch" in row:
            self.state.pitch = row["InitCameraPitch"]
        if "InitCameraDistance" in row:
            self.state.distance = row["InitCameraDistance"]

    def set_max_distance(self, v: float) -> None:
        self.rows[MODE_CHARACTER]["TargetDistance"] = v

    def set_drag_speed(self, v: float) -> None:
        self.rows[MODE_CHARACTER]["MaxDragSpeed"] = v

    def set_follow_mode(self, mode: str) -> None:
        self.switch_mode(mode)

    def set_pitch(self, pitch_deg: float) -> None:
        self.state.pitch = pitch_deg * DEG

    def set_follow_action(self, target: tuple[float, float, float] | None) -> None:
        self.follow_action_target = target

    def update(self, dt: float, anchor: tuple[float, float, float],
               moving: bool = False, turn_angle: float = 0.0,
               char_yaw: float = 0.0,
               obstruction=None) -> None:
        self.t += dt
        row = self.row
        st = self.state
        if st.last_t == 0.0:
            st.last_t = self.t
            dt = 0.0
        else:
            dt = min(dt, 0.1)

        distance = row.get("TargetDistance", 6.0)
        height = row.get("CameraHeight", 2.0)

        if self.mode == MODE_SPRINT and self.sprint_speed > 0.0:
            smin = row.get("SprintCameraMinTrackBackSpeed", 4.0)
            smax = row.get("SprintCameraMaxTrackBackSpeed", 14.0)
            slope = row.get("SprintCameraTrackBackSpeedSlope", 0.5)
            pull = smin + (smax - smin) * min(1.0, slope * self.sprint_speed)
            target = row.get("SprintCameraMaxDistance", 9.0)
            st.distance += min(1.0, dt / row.get("SprintCameraSmoothTime", 0.3)) * (target - st.distance)
            distance = st.distance
        else:
            st.distance += min(1.0, dt / row.get("SmoothTime", 0.1)) * (distance - st.distance)
            distance = st.distance

        if moving:
            apply_angle = row.get("CameraMovePitchApplyAngle", -12.0 * DEG)
            # verified rate-limited approach (pi/3000 rad per ms, snap on reach)
            step = PITCH_RATE * dt
            if abs(apply_angle - st.pitch) <= step:
                st.pitch = apply_angle
            else:
                st.pitch += math.copysign(step, apply_angle - st.pitch)
            self.move_pitch_applied = True
        else:
            adjust = row.get("CameraMovePitchAdjustPitch", -20.0 * DEG)
            step = PITCH_RATE * dt
            if abs(adjust - st.pitch) <= step:
                st.pitch = adjust
            else:
                st.pitch += math.copysign(step, adjust - st.pitch)
            self.move_pitch_applied = False

        if moving and abs(turn_angle) > row.get("CameraAdjustYawWhenMoveTurnDisableAngle", 15.0 * DEG):
            st.yaw += row.get("CameraAdjustYawWhenMoveTurn", 1.0) * turn_angle

        yaw, pitch = st.yaw, st.pitch
        # JX3-exact convention (machine-verified, SetCharacterCameraPosition
        # @ 0x180B0F1EE..0x180B0F2B0, pi/2 const @ 0x180C8DAC8):
        #   offset = ( cos(yaw)*sin(pol)*A + sin(yaw)*B,
        #              cos(pol)*A + C,
        #              sin(yaw)*sin(pol)*A - cos(yaw)*B )
        # pol = angle from vertical-down; pitch = pi/2 - pol (from horizontal).
        # yaw = 0 -> +X, yaw+ -> +Z. A = distance, C = height, B = lateral (0).
        desired = (
            math.cos(yaw) * math.cos(pitch) * distance,
            math.sin(pitch) * distance + height,
            math.sin(yaw) * math.cos(pitch) * distance,
        )
        smooth_time = max(row.get("SmoothTime", 0.1), 1e-3)
        current = list(st.offset)
        for i in range(3):
            delta = desired[i] - current[i]
            if abs(delta) > EPS and abs(delta) > abs(delta) * dt / smooth_time:
                current[i] += delta * dt / smooth_time
            else:
                current[i] = desired[i]
        st.offset = tuple(current)

        st.pos = (anchor[0] + current[0], anchor[1] + current[1], anchor[2] + current[2])
        if obstruction is not None:
            hit = obstruction(anchor, st.pos)
            if hit is not None:
                dx, dy, dz = (st.pos[0] - anchor[0], st.pos[1] - anchor[1], st.pos[2] - anchor[2])
                length = math.dist((0, 0, 0), (dx, dy, dz)) or 1e-6
                pull = max(0.2, hit - 0.2)
                if length > pull:
                    scale = pull / length
                    st.pos = (anchor[0] + dx * scale, anchor[1] + dy * scale, anchor[2] + dz * scale)
        st.look = (anchor[0], anchor[1] + height * 0.35, anchor[2])
        if self.follow_action_target is not None:
            st.look = self.follow_action_target
        st.last_t = self.t


class CameraShake:
    """Camera shake per the recovered JX3 updater (SetCharacterCameraPosition core).

    Engine evidence (proof/netcode/disasm/camera_setcore.txt @ 0x180B10A70):
      mode 1 = burst: per-cycle amplitude *= decay, cos curve over period,
      scaled rotation + position jitter, ends after max cycles;
      mode 0 = random jitter via 3x rand() within +/- amplitude.
    """

    def __init__(self, seed: int = 1234) -> None:
        self.rng = random.Random(seed)
        self.mode = 0
        self.amp = 0.0
        self.period = 0.5
        self.decay = 0.9
        self.max_cycles = 6
        self.cycles = 0
        self.t = 0.0
        self.rot_scale = 0.02
        self.offset: tuple[float, float, float] = (0.0, 0.0, 0.0)
        self.rotation = 0.0

    def start(self, amp: float, period: float = 0.5, decay: float = 0.9, cycles: int = 6) -> None:
        self.mode = 1
        self.amp = amp
        self.period = period
        self.decay = decay
        self.max_cycles = cycles
        self.cycles = 0
        self.t = 0.0

    def update(self, dt: float) -> None:
        if self.mode == 1:
            self.t += dt
            w = math.cos((self.t % self.period) / self.period * 2.0 * math.pi) * self.amp
            self.rotation = w * self.rot_scale
            self.offset = (w * 0.5, w * 0.25, w * 0.5)
            if self.t >= self.period * (self.cycles + 1):
                self.amp *= self.decay
                self.cycles += 1
                if self.cycles >= self.max_cycles:
                    self.mode = 0
                    self.amp = 0.0
                    self.offset = (0.0, 0.0, 0.0)
                    self.rotation = 0.0
        elif self.amp > 0.0:
            self.offset = tuple((self.rng.random() * 2.0 - 1.0) * self.amp for _ in range(3))
        else:
            self.offset = (0.0, 0.0, 0.0)


class TrackCamera:
    """Cinematic camera: damped spring toward a track position (JX3 TrackCameraFrameMove)."""

    def __init__(self) -> None:
        self.pos = [0.0, 0.0, 0.0]
        self.vel = [0.0, 0.0, 0.0]

    def update(self, dt: float, target: tuple[float, float, float],
               k: float = 60.0, c: float = 12.0) -> tuple[float, float, float]:
        for i in range(3):
            accel = k * (target[i] - self.pos[i]) - c * self.vel[i]
            self.vel[i] += accel * dt
            self.pos[i] += self.vel[i] * dt
        return tuple(self.pos)


def smoke() -> int:
    ok = True

    def check(name: str, cond: bool, detail: str = "") -> None:
        nonlocal ok
        print(f"{'PASS' if cond else 'FAIL'}: {name}{' - ' + detail if detail else ''}")
        ok = ok and cond

    cam = FollowCamera()
    anchor = (0.0, 0.0, 0.0)
    for _ in range(60):
        cam.update(1 / 60.0, anchor)
    p = -20.0 * DEG
    expected_x = math.cos(p) * 6.0
    expected_y = math.sin(p) * 6.0 + 2.0
    check("camera settles behind player",
          abs(cam.state.pos[0] - expected_x) < 0.01 and abs(cam.state.pos[2]) < 0.01
          and abs(cam.state.pos[1] - expected_y) < 0.01,
          f"pos={tuple(round(v, 2) for v in cam.state.pos)}")

    cam.mouse(0.2, 0.0)
    cam.update(1 / 60.0, anchor)
    check("mouse yaw orbits camera", abs(cam.state.yaw + 0.2) < 1e-3, f"yaw={cam.state.yaw:.3f}")

    for _ in range(120):
        cam.update(1 / 60.0, anchor, moving=True)
    check("moving applies pitch", cam.move_pitch_applied and
          abs(cam.state.pitch - MODE_ROWS[MODE_CHARACTER]["CameraMovePitchApplyAngle"]) < 0.02,
          f"pitch={cam.state.pitch:.3f}")

    cam.switch_mode(MODE_SPRINT)
    cam.sprint_speed = 10.0
    for _ in range(120):
        cam.update(1 / 60.0, anchor, moving=True)
    check("sprint pulls camera back", cam.state.distance > 7.5,
          f"dist={cam.state.distance:.2f}")

    cam.switch_mode(MODE_CARRIER)
    cam.update(1 / 60.0, anchor)
    row = cam.row
    check("mode rows switch params", row.get("ForbidStrafe") is True and row.get("TurnCameraYawToObjectYaw") is True)

    cam.set_max_distance(12.0)
    check("script hook set_max_distance", cam.rows[MODE_CHARACTER]["TargetDistance"] == 12.0)
    cam.set_follow_mode(MODE_NPC_DIALOG)
    check("follow mode event log", cam.events[-1] == f"enter:{MODE_NPC_DIALOG}", cam.events[-1])

    cam.set_follow_action((0.0, 1.0, 0.0))
    cam.update(1 / 60.0, anchor)
    check("lock/follow-action aims at target", cam.state.look == (0.0, 1.0, 0.0))

    track = TrackCamera()
    tgt = (10.0, 5.0, -10.0)
    pos = (0.0, 0.0, 0.0)
    for _ in range(600):
        pos = track.update(1 / 120.0, tgt)
    check("track camera spring converges",
          all(abs(pos[i] - tgt[i]) < 0.01 for i in range(3)),
          f"pos={tuple(round(v, 2) for v in pos)}")

    shake = CameraShake()
    shake.start(2.0, period=0.5, decay=0.8, cycles=3)
    maxoff = 0.0
    for _ in range(10):
        shake.update(1 / 60.0)
        maxoff = max(maxoff, math.dist((0, 0, 0), shake.offset))
    check("camera shake offsets bounded by amplitude", maxoff <= 1.6, f"maxoff={maxoff:.2f}")
    for _ in range(200):
        shake.update(1 / 60.0)
    check("camera shake ends after max cycles", shake.mode == 0 and shake.amp == 0.0 and shake.offset == (0.0, 0.0, 0.0))

    cam2 = FollowCamera()
    for _ in range(60):
        cam2.update(1 / 60.0, anchor)
    blocked = cam2.update(1 / 60.0, anchor, obstruction=lambda a, p: 2.0 if math.dist(a, p) > 2.0 else None)
    cam2.update(1 / 60.0, anchor, obstruction=lambda a, p: 2.0 if math.dist(a, p) > 2.0 else None)
    d = math.dist(anchor, cam2.state.pos)
    check("obstruction pulls camera in", 1.6 <= d <= 1.9, f"dist={d:.2f}")
    cam2.update(1 / 60.0, anchor)
    check("camera recovers when clear", math.dist(anchor, cam2.state.pos) > 3.0,
          f"dist={math.dist(anchor, cam2.state.pos):.2f}")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(smoke())
