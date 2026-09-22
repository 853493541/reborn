#!/usr/bin/env python3
"""Regression smoke for the native FLWS PSS/SFX data path."""
from __future__ import annotations

from sfx_runtime import build_sfx_payload


def main() -> None:
    payload = build_sfx_payload()
    assert payload["ok"], payload
    assert payload["provider"] == "native-pss", payload
    events = payload["events"]
    assert len(events) == 2, payload

    blade, range_ = events
    assert "刀光01b红色.pss" in blade["logical_path"], blade["logical_path"]
    assert "风车范围_红色.pss" in range_["logical_path"], range_["logical_path"]

    assert blade["block_count"] == 41, blade["block_count"]
    assert range_["block_count"] == 11, range_["block_count"]
    assert len(blade["emitters"]) == 20, len(blade["emitters"])
    assert len(range_["emitters"]) == 5, len(range_["emitters"])

    blade_meshes = [e["resources"]["mesh"] for e in blade["emitters"] if e["resources"]["mesh"]]
    assert len(blade_meshes) == 7, blade_meshes
    assert all(e["resources"]["mesh"] is None for e in range_["emitters"]), range_["emitters"]

    textured = [e for e in range_["emitters"] if e["resources"]["textures"]]
    assert len(textured) == 5, textured
    staged = [a for a in range_["assets"].values() if a["staged"]]
    assert staged, "no staged assets resolved"
    assert all(a["url"] for a in staged), staged[:3]

    print(
        "native PSS smoke PASS:",
        f"events={len(events)}",
        f"blade_emitters={len(blade['emitters'])}",
        f"range_emitters={len(range_['emitters'])}",
        f"staged_assets={len(staged)}",
        f"timing_source={blade['timing_source']}",
    )


if __name__ == "__main__":
    main()
