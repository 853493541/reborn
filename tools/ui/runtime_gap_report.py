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
    "AppendItemFromData",
    "FormatAllItemPos", "FormatAllContentPos", "SetSizeByAllItemSize", "SetPoint",
    "SetOverTextPosition", "SetOverTextFontScheme", "FromUITex", "SetImageType",
    "SetPercentage", "SetFontColor", "Enable", "Check",
    "CorrectPos", "SetScrollPos", "SetStepCount", "EnableScroll", "SetHAlign",
    "Scale", "SetScale", "SetOverText", "SetMapPath", "SetItemStartRelPos",
    "CreateItemData", "SetObject", "SetObjectIcon", "SetObjectSelected", "RemoveItem",
    "FromTextureFile",
    "SetAnimateGroupMouseDown", "SetAnimateGroupMouseOver", "SetAnimateGroupNormal",
    "SetAnimation", "SetLoopCount", "SetTextAutoTipEnabled",
    "SetDragArea", "RegisterLButtonDrag", "EnableDrag", "RegisterScrollControl",
    "Expand", "ActivePage",
}
ITEM_CREATION = set()
ARRANGEMENT = set()
# Drag registration wires interaction, not static layout (the viewer's click/hover
# server dispatches by control type; drag regions need the interaction layer).
INTERACTION = set()
RENDER = {
    "FromIconID",  # data-blocked: the icon id needs the server icon table
}
# FormatTextForDraw is an engine draw-time text hint; the viewer renders text natively.
ANIMATION = set()
# SetAlwaysTop is a window z-order flag - no visual state in a static render.
STATE = set()

CATEGORY_ORDER = ["item-creation", "arrangement", "render", "state", "animation", "interaction", "consumed", "noise"]


def category(method: str) -> str:
    if method in ITEM_CREATION:
        return "item-creation"
    if method in ARRANGEMENT:
        return "arrangement"
    if method in RENDER:
        return "render"
    if method in ANIMATION:
        return "animation"
    if method in INTERACTION:
        return "interaction"
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
    lines.append("# UI runtime gap report �?what the replay records vs what the viewer consumes")
    lines.append("")
    lines.append("Generated by `tools/ui/runtime_gap_report.py` from")
    lines.append("`ui-process-app/Data/runtime_state/*.tsv` (the replay mutation logs) joined with")
    lines.append("`ui-process-app/Data/render_status.tsv` (placeholders/unresolved/outOfBounds).")
    lines.append("")
    lines.append("**Status (2026-10-05):** the viewer's runtime consumer")
    lines.append("(`LayoutPlanBuilder.ApplyRuntimeMutations`, `Engine/LayoutPlan.cs`) now executes the")
    lines.append("property methods, the item-creation pass (materializing the engine's appended list")
    lines.append("rows), the arrangement pass (format/point/scroll/clamp) and the control-state pass.")
    lines.append("This report measures what is **still dropped** �?the remaining calls per window.")
    lines.append("")
    lines.append("## Totals")
    lines.append("")
    lines.append("| category | calls | meaning |")
    lines.append("|---|---|---|")
    meanings = {
        "consumed": "applied by the viewer today",
        "item-creation": "DROPPED —list rows never materialize (missing items)",
        "arrangement": "DROPPED —engine position/size passes (wrong placement)",
        "render": "DROPPED — runtime image source/mode (animation has its own row)",
        "animation": "DROPPED — animation group/state (static render uses the authored frame)",
        "state": "DROPPED —control state (enable/check/expand/page)",
        "interaction": "DROPPED —drag/interaction registration (not static layout)",
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
    lines.append("- **Done (2026-10-05):** item-creation (`Clear` clears only the runtime items and")
    lines.append("  defers to the next append; `AppendItemFromIni`/`AppendContentFromIni`/")
    lines.append("  `AppendItemFromString` materialize the cloned subtree, `$RuntimeItem`-marked;")
    lines.append("  `RemoveItem`; per-item `SetText` after an append lands on the newest clone's")
    lines.append("  Text child), arrangement (`FormatAllItemPos`/`FormatAllContentPos` flow the")
    lines.append("  runtime items only - not the authored background children - and wrap at the")
    lines.append("  authored width, `SetSizeByAllItemSize` capped by the authored box, `SetPoint` ->")
    lines.append("  AnchorArgs, `SetOverTextPosition`/`SetOverTextFontScheme`, `CorrectPos` clamp,")
    lines.append("  `SetScrollPos` content shift, `SetStepCount`/`EnableScroll`), render (`FromUITex`,")
    lines.append("  `SetImageType`, `SetPercentage`, `SetFontColor`, `SetOverText`), state (`Enable`")
    lines.append("  disabled frame, `Check` checked frame, `Expand`, `ActivePage`).")
    lines.append("- **Interaction wired (2026-10-07):** `SetDragArea`/`RegisterLButtonDrag`/`EnableDrag`")
    lines.append("  feed the viewer's item drag / drag-handle sequence and `RegisterScrollControl` feeds the")
    lines.append("  wheel + scrollbar-thumb offset (see `UI_INTERACTION_REPLAY.md` §6).")
    lines.append("- **Remaining:** `AppendItemFromData` (the arg is a function returning item data; needs")
    lines.append("  the function's own row shape) and the animation/icon render calls")
    lines.append("  (`SetAnimation`/`SetAnimateGroup*`/`SetLoopCount`/`FromIconID`/`FromTextureFile`/")
    lines.append("  `SetAlwaysTop`/`FormatTextForDraw`/`SetTextAutoTipEnabled` — frame animation")
    lines.append("  and item-icon sources; `FromIconID` is data-blocked, animations keep the authored frame).")
    lines.append("- The recorded calls are the client's own; the fix is execution, not approximation.")
    lines.append("")
    lines.append("## Viewer guard policies (2026-10-05)")
    lines.append("")
    lines.append("- **Runtime items only in the flow**: a runtime-arranged container flows its")
    lines.append("  `$RuntimeItem` clones, not authored background children (BigBagPanel's filter")
    lines.append("  container has a ~595px `Handle_BG` that pushed the checkboxes off-window).")
    lines.append("- **Clear defers**: `Clear` clears only items added by earlier appends and waits")
    lines.append("  for the next append, so a replay whose data-driven loop under-recorded cannot")
    lines.append("  blank a list.")
    lines.append("- **Never empty a container / never collapse a window**: a hide whose parent")
    lines.append("  would have no visible child is reverted, and a hide set that would leave less")
    lines.append("  than half the authored sections visible is dropped entirely (stub-session reset")
    lines.append("  hides whose mode-driven Show never ran - LuckyMeeting 145 -> 3).")
    lines.append("- **Open phase**: after the entry chain the module's `Open` runs best-effort")
    lines.append("  (the engine opens the window after creating it).")
    lines.append("")

    out = args.out
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))
    print("windows=%d  dropped(item+arrange+render+state)=%d  -> %s" % (len(windows), dropped, out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
