#!/usr/bin/env python3
"""Regression smoke for the real FLWS SFX timeline metadata path."""
from __future__ import annotations

from sfx_runtime import build_sfx_payload


def main() -> None:
    payload = build_sfx_payload()
    assert payload["ok"], payload
    assert payload["magic"] == "GATA"
    assert len(payload["events"]) == 2, payload
    event = payload["events"][0]
    assert "刀光01b红色.pss" in event["logical_path"], event
    assert event["local_bytes"] > 0, event
    assert event["start_time_ms"] == 2000, event
    assert event["play_duration_ms"] == 5000, event
    assert event["total_duration_ms"] == 8000, event
    assert event["texture_refs"], event
    assert "风车范围_红色.pss" in payload["events"][1]["logical_path"], payload
    assert payload["events"][1]["local_bytes"] == 35213, payload
    print(
        "SFX smoke PASS:",
        f"events={len(payload['events'])}",
        f"pss_bytes={event['local_bytes']}",
        f"textures={len(event['texture_refs'])}",
        f"meshes={len(event['mesh_refs'])}",
        f"window={event['start_time_ms']}..{event['start_time_ms'] + event['play_duration_ms']}ms",
    )


if __name__ == "__main__":
    main()
