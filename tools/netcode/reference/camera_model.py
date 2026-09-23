#!/usr/bin/env python3
"""Reference follow-camera for the reborn camera spec (docs/netcode/REBORN_CAMERA_SPEC.md).

Modeled on the JX3 findings: per-mode parameter rows, yaw/pitch mouse camera,
anchor + rotated offset, SmoothTime exponential smoothing with dead zone,
movement-reactive pitch/yaw, sprint pull-back, and a cinematic spring camera.

Run: python tools/netcode/reference/camera_model.py
"""
from __future__ import annotations

import math
import sys
from dataclasses import dataclass, field

DEG = math.pi / 180.0
EPS = 1e-6

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
        "CameraMaxDeltaYaw": 0.5, "CameraMaxDeltaPitch": 0.35,
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
        "CameraAdjustYawWhenMoveTurnDisableAngle": 15.0 * DEG,
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
    def __init__(self, rows: dict = MODE_ROWS, state: CameraState | None = None) -> None:
        self.rows = {k: dict(v) for k, v in rows.items()}
        self.state = state or CameraState()
        self.mode = MODE_CHARACTER
        self.sprint_speed = 0.0
        self.move_pitch_applied = False
        self.last_pitch_apply = 0.0
        self.t = 0.0
        self.follow_action_target: tuple[float, float, float] | None = None
        self.events: list[str] = []

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
               char_yaw: float = 0.0) -> None:
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
            smooth = row.get("CameraMovePitchSmoothTime", 0.25)
            if self.t - self.last_pitch_apply >= row.get("CameraMovePitchApplyTimeInterval", 0.4):
                self.last_pitch_apply = self.t
            st.pitch += min(1.0, dt / smooth) * (apply_angle - st.pitch)
            self.move_pitch_applied = True
        else:
            adjust = row.get("CameraMovePitchAdjustPitch", -20.0 * DEG)
            smooth = row.get("CameraMovePitchSmoothTime", 0.25)
            st.pitch += min(1.0, dt / smooth) * (adjust - st.pitch)
            self.move_pitch_applied = False

        if moving and abs(turn_angle) > row.get("CameraAdjustYawWhenMoveTurnDisableAngle", 15.0 * DEG):
            st.yaw += row.get("CameraAdjustYawWhenMoveTurn", 1.0) * turn_angle

        yaw, pitch = st.yaw, st.pitch
        desired = (
            math.cos(pitch) * math.sin(yaw) * distance,
            math.sin(pitch) * distance + height,
            math.cos(pitch) * math.cos(yaw) * distance,
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
        st.look = (anchor[0], anchor[1] + height * 0.35, anchor[2])
        if self.follow_action_target is not None:
            st.look = self.follow_action_target
        st.last_t = self.t


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
    expected_z = math.cos(p) * 6.0
    expected_y = math.sin(p) * 6.0 + 2.0
    check("camera settles behind player",
          abs(cam.state.pos[2] - expected_z) < 0.01 and abs(cam.state.pos[0]) < 0.01
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
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(smoke())
