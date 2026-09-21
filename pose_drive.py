"""PlaybackClock + FBX pose publish for Dev3/Dev4.

Public API::

    from pose_drive import load_clock_for_row, tick_pose

    clock = load_clock_for_row(row)   # resolve playable_path → MIN2 → set_clip
    sample = tick_pose(clock, actor=optional_fbx_actor, now=None)
    # Prefer sample.local_matrices (parent-space) for FBX apply_pose.
    # sample.matrices (world) is Mesh RQ LBS only — do not feed FBX decompose.
"""
from __future__ import annotations

from pathlib import Path
from typing import Any, Mapping, Optional, Union

from resolve_playable import (
    ROOT,
    load_playable_clip,
    resolve_playable_path,
)
from transport import PlaybackClock, Sample

RowOrPath = Union[Mapping[str, Any], str, Path]


def load_clock_for_row(
    row_or_path: RowOrPath,
    *,
    root: Path = ROOT,
    clock: Optional[PlaybackClock] = None,
    loop: bool = True,
    fps_fallback: float = 33.0,
) -> PlaybackClock:
    """Resolve playable .ani, load MIN2 stick, bind ``PlaybackClock``.

    Returns the clock (new or reused via ``clock=``) with ``set_clip`` applied.
    The loaded ``Min2SkelClip`` is on ``clock.clip`` (includes ``bone_names``).
    """
    path = resolve_playable_path(row_or_path, root=root)
    clip = load_playable_clip(path)
    if clock is None:
        clock = PlaybackClock(fps_fallback=fps_fallback, loop=loop)
        clock.set_clip(clip, seek_start=True)
    else:
        clock.set_clip(clip, seek_start=True)
        clock.loop = bool(loop)
    # Stash resolved path for callers / smoke.
    try:
        clock._playable_path = path  # type: ignore[attr-defined]
    except Exception:
        pass
    return clock


def tick_pose(
    clock: PlaybackClock,
    actor: Any = None,
    now: Optional[float] = None,
    *,
    with_pose: bool = True,
) -> Sample:
    """``clock.sample(with_pose=True)`` then ``apply_pose`` when ``actor`` + local mats.

    Prefers ``sample.local_matrices`` (parent-space) for FBX. World ``matrices``
    are only used if local is missing (and should be avoided for FBX decompose).
    Always passes ``bone_names=clip.bone_names`` into ``apply_pose``.
    """
    sample = clock.sample(now, with_pose=with_pose)
    if actor is None or not with_pose:
        return sample
    mats = getattr(sample, "local_matrices", None)
    if mats is None:
        # Do not apply world matrices to FBX — they explode bone.decompose.
        # Callers that need world LBS should use sample.matrices directly.
        return sample
    clip = getattr(clock, "clip", None) or getattr(clock, "_clip", None)
    bone_names = list(getattr(clip, "bone_names", None) or [])
    try:
        from fbx_actor import apply_pose

        apply_pose(actor, mats, bone_names=bone_names, frame=int(sample.frame))
    except Exception:
        # Soft-fail: transport still returns the sample for Dev3/UI.
        pass
    return sample


__all__ = [
    "load_clock_for_row",
    "tick_pose",
    "Sample",
    "PlaybackClock",
]
