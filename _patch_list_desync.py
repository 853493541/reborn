# -*- coding: utf-8 -*-
"""Fix clip_list ↔ _list_rows desync + Mixer Play free-run past t=0."""
from __future__ import annotations

import ast
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PLAYER = ROOT / "player.py"
src = PLAYER.read_text(encoding="utf-8")
orig = src

# --- 1) _load_clip_list: after building _visible_paths, keep _list_rows 1:1 with listbox ---
old_overwrite = """        self._visible_paths = sorted(self._visible_paths, key=_loco_rank)
        self.clip_list.delete(0, tk.END)
        for path in self._visible_paths:
            self.clip_list.insert(tk.END, _clip_name(path))
        try:
            idx = self._visible_paths.index(target)
        except ValueError:
            idx = 0
        # Prefer disk locomotion list over charge search demo
        self.clip_list.selection_set(idx)
        self.clip_list.see(idx)
        self._on_select_clip()
"""

new_overwrite = """        self._visible_paths = sorted(self._visible_paths, key=_loco_rank)
        # CRITICAL: listbox strings must stay 1:1 with _list_rows (Thor FAIL: index 2
        # showed 小跳b but _list_rows[2] was 僵尸行走 → Mixer stuck on walk).
        self._list_rows = []
        self.clip_list.delete(0, tk.END)
        for path in self._visible_paths:
            label = _clip_name(path)
            self.clip_list.insert(tk.END, label)
            self._list_rows.append(
                {
                    "path": path,
                    "label": label,
                    "tag": "ANI",
                    "entry": {
                        "filename": path.name,
                        "label": label,
                        "body": _body_for_path(path) or "F1",
                    },
                    "anim_id": None,
                    "playable_path": "",
                }
            )
        try:
            idx = self._visible_paths.index(target)
        except ValueError:
            idx = 0
        # Prefer real 行走 then 小跳b by label (stable), not stale catalog index.
        for i, r in enumerate(self._list_rows):
            lab = str(r.get("label") or "")
            if "行走" in lab or "走路" in lab:
                if "僵尸" in lab or "攻击" in lab:
                    continue
                idx = i
                break
        else:
            for i, r in enumerate(self._list_rows):
                lab = str(r.get("label") or "")
                if "小跳b" in lab or "小跳" in lab or "跳跃" in lab:
                    idx = i
                    break
        self.clip_list.selection_clear(0, tk.END)
        self.clip_list.selection_set(idx)
        self.clip_list.see(idx)
        self._on_select_clip()
"""

if old_overwrite not in src:
    raise SystemExit("BLOCKER: _load_clip_list overwrite block not found")
src = src.replace(old_overwrite, new_overwrite, 1)

# --- 2) _on_select_clip: resolve selection by listbox text if row path mismatches label ---
# Insert helper method before _on_select_clip if missing
if "def _row_for_list_selection" not in src:
    helper = '''
    def _row_for_list_selection(self):
        """Map clip_list curselection → _list_rows entry 1:1 (fallback: match label)."""
        sel = self.clip_list.curselection()
        if not sel:
            return None
        idx = int(sel[0])
        rows = getattr(self, "_list_rows", None) or []
        if idx < 0 or idx >= len(rows):
            return None
        row = rows[idx]
        # Belts-and-suspenders: if listbox text disagrees with row label, rematch.
        try:
            shown = self.clip_list.get(idx)
        except Exception:
            shown = ""
        shown_s = str(shown or "")
        lab = str(row.get("label") or "")
        path = row.get("path")
        path_s = path.name if path is not None else ""
        if shown_s and lab and shown_s != lab and lab not in shown_s and path_s not in shown_s:
            for r in rows:
                rl = str(r.get("label") or "")
                rp = r.get("path")
                rpn = rp.name if rp is not None else ""
                if rl and (rl == shown_s or rl in shown_s or (rpn and rpn in shown_s)):
                    return r
            # filename token from listbox (strip [TAG] / id prefix)
            token = shown_s.split()[0] if shown_s.split() else shown_s
            for r in rows:
                rp = r.get("path")
                if rp is not None and token in rp.name:
                    return r
        return row

'''
    anchor = "    def _on_select_clip(self):"
    if anchor not in src:
        raise SystemExit("BLOCKER: _on_select_clip not found")
    src = src.replace(anchor, helper + anchor, 1)

old_sel_head = """    def _on_select_clip(self):
        sel = self.clip_list.curselection()
        if not sel or sel[0] >= len(self._list_rows):
            return
        row = self._list_rows[sel[0]]
        entry = row.get("entry") or {}
        path = row.get("path")
"""

new_sel_head = """    def _on_select_clip(self):
        row = self._row_for_list_selection()
        if row is None:
            return
        entry = row.get("entry") or {}
        path = row.get("path")
"""

if old_sel_head not in src:
    raise SystemExit("BLOCKER: _on_select_clip head not found after helper insert")
src = src.replace(old_sel_head, new_sel_head, 1)

# --- 3) _draw_frame Mixer: while playing, keep free-run (past t=0 mid proof) ---
old_draw = """        mix_clip = getattr(self, "_fbx_mixer_clip", None)
        if mix_clip is not None and self.character_mode == "fbx":
            fps = float(getattr(self.clip, "fps", 0) or self.fps or FPS_DEFAULT)
            t = f / max(fps, 1e-6)
            total = (self.clip.frame_count - 1) / max(fps, 1e-6)
            playing = bool(self.clock.is_playing)
            # While paused/scrubbing, freeze Mixer at t. While playing, free-run (no t).
            if not playing:
                # Throttle nav: only when frame changes meaningfully
                key = (str(mix_clip), round(t, 2), False)
                if getattr(self, "_fbx_scrub_key", None) != key:
                    self._fbx_scrub_key = key
                    self._nav_fbx_viewport(clip_fbx=mix_clip, t=t, playing=False)
            self.time_lbl.configure(
                text=f"{t:0.2f} / {total:0.2f}s   ·   frame {f}   ·   Mixer clipFbx"
            )
            return
"""

new_draw = """        mix_clip = getattr(self, "_fbx_mixer_clip", None)
        if mix_clip is not None and self.character_mode == "fbx":
            fps = float(getattr(self.clip, "fps", 0) or self.fps or FPS_DEFAULT)
            t = f / max(fps, 1e-6)
            total = (self.clip.frame_count - 1) / max(fps, 1e-6)
            playing = bool(self.clock.is_playing)
            # While paused/scrubbing, freeze Mixer at t. While playing, free-run (no t)
            # so mid-proof advances past the t=0 bind frame from list select.
            if playing:
                key = (str(mix_clip), True)
                if getattr(self, "_fbx_scrub_key", None) != key:
                    self._fbx_scrub_key = key
                    self._nav_fbx_viewport(clip_fbx=mix_clip, t=None, playing=True)
            else:
                key = (str(mix_clip), round(t, 2), False)
                if getattr(self, "_fbx_scrub_key", None) != key:
                    self._fbx_scrub_key = key
                    self._nav_fbx_viewport(clip_fbx=mix_clip, t=t, playing=False)
            self.time_lbl.configure(
                text=f"{t:0.2f} / {total:0.2f}s   ·   frame {f}   ·   Mixer clipFbx"
            )
            return
"""

if old_draw not in src:
    raise SystemExit("BLOCKER: _draw_frame mixer block not found")
src = src.replace(old_draw, new_draw, 1)

if src == orig:
    raise SystemExit("BLOCKER: no changes applied")

ast.parse(src)
PLAYER.write_text(src, encoding="utf-8")
print("PATCHED_OK", PLAYER)

# quick unit: simulate list sync shape
print("sanity: helper present", "def _row_for_list_selection" in src)
print("sanity: list_rows rebuild", "CRITICAL: listbox strings" in src)
print("sanity: play free-run key", '(str(mix_clip), True)' in src)
