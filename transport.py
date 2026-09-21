"""Dev2 wall-clock playback transport for Ani Player viewport."""
from __future__ import annotations

from dataclasses import dataclass
from time import perf_counter
from typing import Any, Optional


@dataclass
class Sample:
    frame: int
    time: float
    positions: Any = None
    matrices: Any = None  # WORLD 3x4 — Mesh RQ LBS only
    quats: Any = None
    local_matrices: Any = None  # PARENT-space 3x4 — FBX bone.decompose


class PlaybackClock:
    def __init__(self, fps_fallback: float = 33.0, loop: bool = True, fps: float | None = None):
        # Accept both fps_fallback= (player) and fps= aliases.
        self.fps_fallback = float(fps if fps is not None else fps_fallback)
        self.loop = bool(loop)
        self._clip = None
        self._playing = False
        self._frame = 0
        self._origin = 0.0
        self._frame0 = 0

    @property
    def is_playing(self) -> bool:
        return self._playing

    @property
    def frame(self) -> int:
        return self._frame

    @property
    def clip(self):
        return self._clip

    @property
    def fps(self) -> float:
        clip = self._clip
        if clip is None:
            return self.fps_fallback
        return float(getattr(clip, "fps", None) or self.fps_fallback or 33.0)

    @property
    def frame_count(self) -> int:
        clip = self._clip
        if clip is None:
            return 0
        return int(getattr(clip, "frame_count", 0) or 0)

    def set_clip(self, clip, seek_start: bool = False) -> None:
        self._clip = clip
        if clip is None:
            self._playing = False
            self._frame = 0
            return
        if seek_start:
            self.seek(0)

    def play(self) -> None:
        if self._clip is None:
            return
        self._playing = True
        self._origin = perf_counter()
        self._frame0 = self._frame

    def pause(self) -> None:
        self._playing = False

    def toggle(self) -> None:
        if self._playing:
            self.pause()
        else:
            self.play()

    def stop(self) -> None:
        self._playing = False
        self.seek(0)

    def seek(self, frame: int) -> None:
        n = self.frame_count
        if n <= 0:
            self._frame = 0
        else:
            self._frame = max(0, min(int(frame), n - 1))
        self._origin = perf_counter()
        self._frame0 = self._frame

    def seek_time(self, seconds: float) -> None:
        self.seek(int(round(float(seconds) * self.fps)))

    def tick(self, now: float | None = None) -> int:
        if not self._playing or self._clip is None:
            return self._frame
        now = perf_counter() if now is None else float(now)
        elapsed = max(0.0, now - self._origin)
        target = self._frame0 + int(elapsed * self.fps)
        n = self.frame_count
        if n <= 0:
            self._frame = 0
            return self._frame
        if target >= n:
            if self.loop:
                target = target % n
                self._origin = now
                self._frame0 = target
            else:
                target = n - 1
                self._playing = False
        self._frame = target
        return self._frame

    def sample(self, now: float | None = None, with_pose: bool = False) -> Sample:
        frame = self.tick(now)
        positions = matrices = quats = local_matrices = None
        clip = self._clip
        if with_pose and clip is not None:
            if hasattr(clip, "positions_at"):
                try:
                    positions = clip.positions_at(frame)
                except Exception:
                    positions = None
            if hasattr(clip, "matrices_at"):
                try:
                    matrices = clip.matrices_at(frame)
                except Exception:
                    matrices = None
            if hasattr(clip, "local_matrices_at"):
                try:
                    local_matrices = clip.local_matrices_at(frame)
                except Exception:
                    local_matrices = None
            if hasattr(clip, "quats_at"):
                try:
                    quats = clip.quats_at(frame)
                except Exception:
                    quats = None
        return Sample(
            frame=frame,
            time=frame / self.fps if self.fps else 0.0,
            positions=positions,
            matrices=matrices,
            quats=quats,
            local_matrices=local_matrices,
        )


# Alias some teammates used in notes
PlaybackTransport = PlaybackClock
