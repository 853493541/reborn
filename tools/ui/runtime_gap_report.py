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
    # 2026-10-05: executed by ApplyRuntimeMutations (the item/layout/state pass).
    "Clear", "AppendItemFromIni", "AppendContentFromIni", "AppendItemFromString",
    "FormatAllItemPos", "FormatAllContentPos", "SetSizeByAllItemSize", "SetPoint",
    "SetOverTextPosition", "SetOverTextFontScheme", "FromUITex", "SetImageType",
    "SetPercentage", "SetFontColor", "Enable", "Check",
    "CorrectPos", "SetScrollPos", "SetStepCount", "EnableScroll", "SetHAlign",
    "Scale", "SetScale", "SetOverText", "SetMapPath", "SetItemStartRelPos",
    "CreateItemData", "SetObject", "SetObjectIcon", "SetObjectSelected", "RemoveItem",
    "Expand", "ActivePage",
}
ITEM_CREATION = {
    "AppendItemFromData",
}
ARRANGEMENT = {
    "SetScrollVerStepSize", "SetDragArea", "RegisterLButtonDrag", "EnableDrag",
}
RENDER = {
    "SetTextAutoTipEnabled", "FormatTextForDraw", "SetAnimation",
    "SetAnimateGroupMouseDown", "SetAnimateGroupMouseOver", "SetAnimateGroupNormal",
    "SetLoopCount", "FromIconID", "FromTextureFile", "SetAlwaysTop",
}
STATE = set()

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
    lines.append("**Status (2026-10-05):** the viewer's runtime consumer")
    lines.append("(`LayoutPlanBuilder.ApplyRuntimeMutations`, `Engine/LayoutPlan.cs`) now executes the")
    lines.append("property methods, the item-creation pass (materializing the engine's appended list")
    lines.append("rows), the arrangement pass (format/point/scroll/clamp) and the control-state pass.")
    lines.append("This report measures what is **still dropped** — the remaining calls per window.")
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

    lines.append("## Fix status")
    lines.append("")
    lines.append("- **Done (2026-10-05):** item-creation (`Clear`, `AppendItemFromIni`,")
    lines.append("  `AppendContentFromIni`, `AppendItemFromString` materialize the cloned subtree;")
    lines.append("  `RemoveItem`), arrangement (`FormatAllItemPos`/`FormatAllContentPos` flow pass,")
    lines.append("  `SetSizeByAllItemSize`, `SetPoint` -> AnchorArgs, `SetOverTextPosition`/")
    lines.append("  `SetOverTextFontScheme`, `CorrectPos` clamp, `SetScrollPos` content shift,")
    lines.append("  `SetStepCount`/`EnableScroll`), render (`FromUITex`, `SetImageType`,")
    lines.append("  `SetPercentage`, `SetFontColor`, `SetOverText`), state (`Enable` disabled frame,")
    lines.append("  `Check` checked frame, `Expand`, `ActivePage`).")
    lines.append("- **Remaining (26 calls):** `AppendItemFromData` (5, SocialPanel — the arg is a")
    lines.append("  function returning item data; needs the function's own row shape), the drag")
    lines.append("  registrations (`SetDragArea`/`RegisterLButtonDrag`/`EnableDrag`/`SetScrollVerStepSize`,")
    lines.append("  8 — interaction config, no visual), and the animation/icon render calls")
    lines.append("  (`SetAnimation`/`SetAnimateGroup*`/`SetLoopCount`/`FromIconID`/`FromTextureFile`/")
    lines.append("  `SetAlwaysTop`/`FormatTextForDraw`/`SetTextAutoTipEnabled`, 13 — frame animation")
    lines.append("  and item-icon sources).")
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
