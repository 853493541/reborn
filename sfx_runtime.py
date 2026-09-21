"""MovieEditor-compatible SFX timeline discovery for the companion player.

This module deliberately keeps the data path separate from the renderer:

    GATA .tani -> PSS logical paths -> PakV4/cache asset -> timeline event

The MovieEditor binary owns the authoritative PSS renderer.  The companion
uses this metadata layer to reproduce its scheduling and attachment contract,
while the browser can optionally consume the existing local map-viewer cache
API for decoded emitter/texture data.
"""
from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass, field
from pathlib import Path
from urllib.parse import quote

import tani


ROOT = Path(__file__).resolve().parent
DEFAULT_TANI = (
    ROOT
    / "samples"
    / "player"
    / "moves"
    / "f1_f1s07cj重剑技能15_风来吴山红色hd.tani"
)
DEFAULT_PSS_DIR = ROOT / "proof" / "compare" / "sfx_pakv4_flws_red"
TIMING_PROOF = ROOT / "proof" / "sfx_tani_parse_flws_red.json"
PSS_SERVICE = "http://127.0.0.1:3015"

_PATH_RE = re.compile(
    rb"data[\\/][^\x00\r\n\"'<>]{3,240}?\."
    rb"(?:pss|tga|dds|jsondef|jsoninspack|mesh|ani)",
    re.IGNORECASE,
)


@dataclass
class SfxEvent:
    logical_path: str
    event_kind: str = "generic"
    placement_mode: str = "actor-root"
    anchor_hint: str = "actor-root"
    start_time_ms: int | None = None
    play_duration_ms: int | None = None
    total_duration_ms: int | None = None
    timing_source: str = "unresolved"
    local_file: str | None = None
    local_bytes: int = 0
    referenced_assets: list[str] = field(default_factory=list)
    texture_refs: list[str] = field(default_factory=list)
    mesh_refs: list[str] = field(default_factory=list)
    analysis_url: str | None = None

    def to_dict(self) -> dict:
        return asdict(self)


def _decode_path(raw: bytes) -> str:
    for encoding in ("gb18030", "gbk", "utf-8"):
        try:
            return raw.decode(encoding)
        except UnicodeDecodeError:
            continue
    return raw.decode("latin1", errors="replace")


def _path_key(value: str) -> str:
    return str(value or "").replace("/", "\\").strip().lower()


def _basename(value: str) -> str:
    return _path_key(value).rsplit("\\", 1)[-1]


def _loose_key(value: str) -> str:
    """Key that still works when a GBK path was decoded with the wrong codec."""
    return re.sub(r"[^a-z0-9]+", "", _basename(value))


def extract_pss_references(path: Path) -> list[str]:
    """Extract authored asset paths from a PAR/PSS binary.

    This is an audit helper, not a PSS decoder.  It preserves the paths needed
    to compare MovieEditor/PakV4 dependency resolution without inventing
    emitter values.
    """
    if not path.is_file():
        return []
    data = path.read_bytes()
    found: list[str] = []
    for match in _PATH_RE.finditer(data):
        value = _decode_path(match.group(0)).replace("/", "\\")
        if value not in found:
            found.append(value)
    return found


def _manifest_files(pss_dir: Path) -> list[dict]:
    manifest = pss_dir / "manifest.json"
    if not manifest.is_file():
        return []
    try:
        data = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return []
    return [item for item in data if isinstance(item, dict)]


def _find_local_pss(logical_path: str, pss_dir: Path) -> Path | None:
    """Find a staged PSS by manifest basename, then by a tolerant key."""
    wanted = _basename(logical_path)
    files = _manifest_files(pss_dir)
    for item in files:
        manifest_name = str(item.get("file") or "")
        if _path_key(manifest_name) == wanted:
            candidate = pss_dir / manifest_name
            if candidate.is_file():
                return candidate

    candidates = sorted(pss_dir.glob("*.pss"))
    loose = _loose_key(wanted)
    exact = [p for p in candidates if _loose_key(p.name) == loose]
    if exact:
        return exact[0]

    # The staged FLWS manifest is authoritative for paths whose Chinese
    # characters were decoded differently by a caller.
    for item in files:
        rel = str(item.get("rel") or "")
        if _loose_key(rel) == loose:
            manifest_name = str(item.get("file") or "")
            candidate = pss_dir / manifest_name
            if candidate.is_file():
                return candidate
    return None


def _timing_override(tani_path: Path) -> dict:
    """Load measured timing only for the TANI it was captured from."""
    if (
        TIMING_PROOF.is_file()
        and tani_path.name.lower() == "f1_f1s07cj重剑技能15_风来吴山红色hd.tani"
    ):
        try:
            data = json.loads(TIMING_PROOF.read_text(encoding="utf-8"))
            if isinstance(data, dict) and data.get("pssEntries"):
                entry = data["pssEntries"][0]
                if isinstance(entry, dict):
                    return {
                        "start_time_ms": entry.get("effectiveStartTimeMs"),
                        "play_duration_ms": entry.get("pssPlayDurationMs"),
                        "total_duration_ms": entry.get("pssTotalDurationMs"),
                        "timing_source": entry.get("timingSource")
                        or "pss-global-delay",
                    }
        except (OSError, ValueError, IndexError):
            pass

    return {}


def _event_profile(logical_path: str) -> tuple[str, str, str]:
    """Classify the checked-in FLWS events without pretending PSS has a socket."""
    key = _path_key(logical_path)
    if "\\状态\\" in key or "风车范围" in key:
        return "range", "ground", "actor-root"
    if "\\发招\\" in key or "刀光" in key:
        return "blade", "bone", "bip01_r_hand"
    return "generic", "actor-root", "actor-root"


def _event_for_path(logical_path: str, tani_path: Path, pss_dir: Path) -> SfxEvent:
    local = _find_local_pss(logical_path, pss_dir)
    refs = extract_pss_references(local) if local else []
    timing = _timing_override(tani_path)
    event_kind, placement_mode, anchor_hint = _event_profile(logical_path)
    encoded = quote(logical_path.replace("\\", "/"), safe="")
    return SfxEvent(
        logical_path=logical_path,
        event_kind=event_kind,
        placement_mode=placement_mode,
        anchor_hint=anchor_hint,
        start_time_ms=timing.get("start_time_ms"),
        play_duration_ms=timing.get("play_duration_ms"),
        total_duration_ms=timing.get("total_duration_ms"),
        timing_source=timing.get("timing_source", "unresolved"),
        local_file=str(local.relative_to(ROOT)) if local else None,
        local_bytes=local.stat().st_size if local else 0,
        referenced_assets=refs,
        texture_refs=[p for p in refs if p.lower().endswith((".tga", ".dds"))],
        mesh_refs=[p for p in refs if p.lower().endswith((".mesh", ".ani", ".jsoninspack"))],
        analysis_url=f"{PSS_SERVICE}/api/pss/analyze?sourcePath={encoded}",
    )


def build_sfx_payload(
    tani_path: Path | str = DEFAULT_TANI,
    *,
    pss_dir: Path | str = DEFAULT_PSS_DIR,
) -> dict:
    """Return a JSON-safe MovieEditor-style SFX timeline description."""
    tani_path = Path(tani_path)
    pss_dir = Path(pss_dir)
    info = tani.parse_tani(tani_path)
    events: list[SfxEvent] = []
    seen: set[str] = set()
    for logical_path in info.pss_paths:
        key = _path_key(logical_path)
        # GATA also carries a truncated "her\\..." twin path. MovieEditor
        # resolves the full data\\source path; do not create a duplicate event
        # for the incomplete alias.
        if not (key.startswith("data\\") or key.startswith("data/")):
            continue
        if key in seen or "\\pss\\" not in key:
            continue
        seen.add(key)
        events.append(_event_for_path(logical_path, tani_path, pss_dir))

    known_totals = [
        e.total_duration_ms
        for e in events
        if isinstance(e.total_duration_ms, (int, float)) and e.total_duration_ms > 0
    ]
    return {
        "ok": info.error is None,
        "provider": "MovieEditor-compatible PSS timeline",
        "host": "MovieEditorHD",
        "tani": str(tani_path.relative_to(ROOT)) if tani_path.is_relative_to(ROOT) else str(tani_path),
        "magic": "GATA",
        "version": info.version,
        "ani_path": info.ani_path,
        "sfx_tags": list(info.sfx_tags),
        "sound_events": list(info.sound_events),
        "events": [event.to_dict() for event in events],
        "timeline_duration_ms": max(known_totals, default=0),
        "ground_truth": {
            "visual_reference": "MovieEditorHD/client",
            "map_viewer_is_gt": False,
            "timing_note": "Per-PSS start time is measured from the checked-in S1 proof when available.",
        },
        "dependency_note": (
            "PSS bytes are real PAR assets. Emitter/texture decoding is delegated "
            "to the optional local map-viewer cache API until a native decoder exists."
        ),
    }


def flws_payload() -> dict:
    return build_sfx_payload()


__all__ = [
    "DEFAULT_PSS_DIR",
    "DEFAULT_TANI",
    "SfxEvent",
    "build_sfx_payload",
    "extract_pss_references",
    "flws_payload",
]
