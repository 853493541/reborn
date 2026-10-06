#!/usr/bin/env python3
"""Runtime-state gap report: what the replay records vs what the viewer consumes.

Scans `ui-process-app/Data/runtime_state/*.tsv` (the replay mutation logs) and
classifies every recorded call against the viewer's runtime consumer
(`LayoutPlanBuilder.ApplyRuntimeMutations`, ui-process-app/Engine/LayoutPlan.cs).
Calls the viewer does not consume are the measured gap:

  * item-creation  - Append*/CreateItemData/Clear/... (lists stay empty -> missing items)
  * arrangement    - Format*/CorrectPos/SetPoint/SetScrollPos/... (items at authored
                     coords -> wrongly placed)
  * render         - FromUITex/SetImageType/animations (image source/mode changes)
  * state          - Enable/Check/Expand/ActivePage (control state)
  * consumed       - the 14 property methods the viewer applies today
  * noise          - Lookup/RegisterEvent/Get*/Is* (engine plumbing, no state)

Joins per-window render status (placeholders/unresolved/outOfBounds) and writes a
Markdown report (docs/ui/UI_RUNTIME_GAP.md) with the full per-window table, the
dropped-call detail for every window, and the fix plan per method.

Usage:
  python tools/ui/runtime_gap_report.py [--state-dir DIR] [--status FILE] [--out FILE]
"""

from __future__ import annotations

import argparse
import os
import sys
from collections import Counter, defaultdict

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

CONSUMED = {
    "SetSize", "SetW", "SetH", "SetRelPos", "SetAbsPos", "SetRelX", "SetRelY",
    "SetFrame", "SetText", "SetFontScheme", "SetAlpha", "Show", "Hide", "SetVisible",
}
ITEM_CREATION = {
    "AppendItemFromIni", "AppendContentFromIni", "CreateItemData", "AppendItemFromData",
    "AppendItemFromString", "Clear", "RemoveItem", "SetObject", "SetObjectIcon",
    "SetObjectSelected",
}
ARRANGEMENT = {
    "FormatAllItemPos", "FormatAllContentPos", "CorrectPos", "SetPoint", "SetScrollPos",
    "SetSizeByAllItemSize", "SetOverTextPosition", "SetStepCount", "EnableScroll",
    "SetItemStartRelPos", "SetHAlign", "Scale", "SetScale", "SetScrollVerStepSize",
    "SetDragArea", "RegisterLButtonDrag", "EnableDrag", "SetAreaTestFile", "SetMapPath",
}
RENDER = {
    "SetFontColor", "SetTextAutoTipEnabled", "SetOverTextFontScheme", "SetOverText",
    "FormatTextForDraw", "SetPercentage", "SetAnimation", "SetAnimateGroupMouseDown",
    "SetAnimateGroupMouseOver", "SetAnimateGroupNormal", "SetLoopCount", "SetImageType",
    "FromUITex", "FromIconID", "FromTextureFile", "SetAlwaysTop",
}
STATE = {"Enable", "Check", "Expand", "ActivePage"}

CATEGORY_ORDER = ["item-creation", "arrangement", "render", "state", "consumed", "noise"]


def category(method: str) -> str:
    if method in ITEM_CREATION:
        return "item-creation"
    if method in ARRANGEMENT:
        return "arrangement"
    if method in RENDER:
        return "render"
    if method in STATE:
        return "state"
    if method in CONSUMED:
        return "consumed"
    return "noise"


def read_status(path: str) -> dict:
    status = {}
    if not path or not os.path.exists(path):
        return status
    with open(path, encoding="utf-8", errors="replace") as f:
        first = True
        for line in f:
            if first:
                first = False
                continue
            parts = line.rstrip("\n").split("\t")
            if len(parts) < 10:
                continue
            try:
                status[parts[0].lower()] = {
                    "stage": parts[1],
                    "placeholders": int(parts[7]),
                    "unresolved": int(parts[8]),
                    "oob": int(parts[9]),
                }
            except ValueError:
                continue
    return status


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--state-dir", default=os.path.join(REPO, "ui-process-app", "Data", "runtime_state"))
    ap.add_argument("--status", default=os.path.join(REPO, "ui-process-app", "Data", "render_status.tsv"))
    ap.add_argument("--out", default=os.path.join(REPO, "docs", "ui", "UI_RUNTIME_GAP.md"))
    args = ap.parse_args(argv)

    status = read_status(args.status)
    windows = []
    totals = Counter()
    method_counts = Counter()
    method_windows = defaultdict(set)
    detail = defaultdict(list)  # stem -> list of (category, method, section, args)

    for fn in sorted(os.listdir(args.state_dir)):
        if not fn.lower().endswith(".tsv") or fn == "replay_summary.tsv":
            continue
        stem = fn[:-4]
        per = Counter()
        with open(os.path.join(args.state_dir, fn), encoding="utf-8", errors="replace") as f:
            first = True
            for line in f:
                if first:
                    first = False
                    continue
                parts = line.rstrip("\n").split("\t")
                if len(parts) < 2:
                    continue
                section, method = parts[0], parts[1]
                cat = category(method)
                per[cat] += 1
                totals[cat] += 1
                if cat not in ("consumed", "noise"):
                    method_counts[method] += 1
                    method_windows[method].add(stem)
                    detail[stem].append((cat, method, section, parts[2:]))
        windows.append((stem, per))

    gap_windows = sorted(windows, key=lambda kv: -(kv[1]["item-creation"] + kv[1]["arrangement"]))

    lines = []
    lines.append("# UI runtime gap report — what the replay records vs what the viewer consumes")
    lines.append("")
    lines.append("Generated by `tools/ui/runtime_gap_report.py` from")
    lines.append("`ui-process-app/Data/runtime_state/*.tsv` (the replay mutation logs) joined with")
    lines.append("`ui-process-app/Data/render_status.tsv` (placeholders/unresolved/outOfBounds).")
    lines.append("")
    lines.append("**The measured answer to \"why are items missing / wrongly placed\":** the viewer's")
    lines.append("runtime consumer (`LayoutPlanBuilder.ApplyRuntimeMutations`, `Engine/LayoutPlan.cs`)")
    lines.append("applies only the 14 property methods; every **item-creation** call (the engine")
    lines.append("building list rows) and every **arrangement** call (the engine positioning them)")
    lines.append("is dropped, so lists keep only their authored prototype and items sit at authored")
    lines.append("coordinates.")
    lines.append("")
    lines.append("## Totals")
    lines.append("")
    lines.append("| category | calls | meaning |")
    lines.append("|---|---|---|")
    meanings = {
        "consumed": "applied by the viewer today",
        "item-creation": "DROPPED — list rows never materialize (missing items)",
        "arrangement": "DROPPED — engine position/size passes (wrong placement)",
        "render": "DROPPED — runtime image source/mode/animation",
        "state": "DROPPED — control state (enable/check/expand/page)",
        "noise": "engine plumbing, no visual state (lookup/events/getters)",
    }
    for cat in CATEGORY_ORDER:
        lines.append("| %s | %d | %s |" % (cat, totals[cat], meanings[cat]))
    lines.append("")
    dropped = totals["item-creation"] + totals["arrangement"] + totals["render"] + totals["state"]
    lines.append("State-bearing dropped calls: **%d** (item-creation %d + arrangement %d + render %d + state %d)."
                 % (dropped, totals["item-creation"], totals["arrangement"], totals["render"], totals["state"]))
    lines.append("")

    lines.append("## Dropped methods (distinct, with the windows using them)")
    lines.append("")
    lines.append("| method | category | calls | windows |")
    lines.append("|---|---|---|---|")
    for method, n in sorted(method_counts.items(), key=lambda kv: (-kv[1], kv[0])):
        ws = sorted(method_windows[method])
        shown = ", ".join(ws[:6]) + (" (+%d)" % (len(ws) - 6) if len(ws) > 6 else "")
        lines.append("| `%s` | %s | %d | %s |" % (method, category(method), n, shown))
    lines.append("")

    lines.append("## Per-window gap (sorted by missing-item + wrong-placement calls)")
    lines.append("")
    lines.append("| window | stage | consumed | item-creation | arrangement | render | state | placeholders | unresolved | oob |")
    lines.append("|---|---|---|---|---|---|---|---|---|---|")
    for stem, per in gap_windows:
        st = status.get(stem.lower(), {})
        lines.append("| %s | %s | %d | %d | %d | %d | %d | %s | %s | %s |" % (
            stem, st.get("stage", "?"), per["consumed"], per["item-creation"], per["arrangement"],
            per["render"], per["state"], st.get("placeholders", "?"), st.get("unresolved", "?"),
            st.get("oob", "?")))
    lines.append("")

    lines.append("## Full dropped-call detail (every window, item-creation/arrangement/render/state)")
    lines.append("")
    for stem, per in gap_windows:
        calls = detail.get(stem, [])
        if not calls:
            continue
        lines.append("### %s" % stem)
        lines.append("")
        for cat, method, section, cargs in calls:
            argtext = " ".join(cargs[:3])
            lines.append("- `%s` %s %s %s" % (cat, method, section, argtext))
        lines.append("")

    lines.append("## Fix plan (implement the client's own calls in the viewer)")
    lines.append("")
    lines.append("- **item-creation**: materialize `AppendItemFromIni`/`AppendContentFromIni`")
    lines.append("  (clone the named INI subtree under the container), `CreateItemData`/")
    lines.append("  `AppendItemFromData` (rows from the real table data), `Clear`/`RemoveItem`")
    lines.append("  (drop existing clones). This is what fills every list (bag rows, friend rows,")
    lines.append("  mail list, skill rows, activity rows).")
    lines.append("- **arrangement**: after materializing items, run the engine's passes in recorded")
    lines.append("  order — `FormatAllItemPos`/`FormatAllContentPos` (grid/flow placement),")
    lines.append("  `SetSizeByAllItemSize`, `CorrectPos`, `SetPoint` (anchor placement),")
    lines.append("  `SetScrollPos`/`SetStepCount`/`EnableScroll` (scroll offset/range),")
    lines.append("  `SetOverTextPosition`/`SetOverTextFontScheme`.")
    lines.append("- **render**: `FromUITex`/`FromIconID`/`FromTextureFile` switch the image source at")
    lines.append("  runtime (missing-art counts change with the real source), `SetImageType` the")
    lines.append("  render mode, the Animate* calls the frame animation.")
    lines.append("- **state**: `Enable`/`Check`/`Expand`/`ActivePage` mirror the control state into")
    lines.append("  the render (disabled look, checked frame, expanded groups, active page).")
    lines.append("- The recorded calls are the client's own; the fix is execution, not approximation.")
    lines.append("")

    out = args.out
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))
    print("windows=%d  dropped(item+arrange+render+state)=%d  -> %s" % (len(windows), dropped, out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
