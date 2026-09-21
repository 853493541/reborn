def _refresh_catalog(self, select_first=False):
        selected_body = self.body_var.get()
        self._paint_body_buttons()
        needle = (self.search_var.get() or "").strip().lower()
        tab = self.catalog_tab.get() if hasattr(self, "catalog_tab") else "anim"
        ui = getattr(self, "_ui_lists", None)

        entries: list[dict] = []
        if ui:
            key = f"{selected_body.lower()}_list"
            demo = ui.get("search_demo_fenglaiwushan") or {}
            demo_needle = (demo.get("needle") or "").lower()
            if (
                tab != "serial"
                and needle
                and demo_needle
                and needle in demo_needle
                and (demo.get("body") or "F1").upper() == selected_body
            ):
                raw_hits = list(demo.get("hits") or [])
                pref = demo.get("preferred")
                if pref:
                    raw_hits = [pref] + [h for h in raw_hits if h.get("id") != pref.get("id")]
                for e in raw_hits:
                    if tab == "tani" and (e.get("badge") or "").upper() != "TANI":
                        continue
                    e = _merge_playable(e, getattr(self, "_playable", None))
                    fn = _entry_filename(e)
                    local = (e.get("local_path") or "").replace("\\", "/")
                    path = None
                    if local:
                        for cand in (
                            self.samples_dir / "player" / local,
                            self.samples_dir / "player" / "moves" / Path(local).name,
                        ):
                            if cand.is_file():
                                path = cand
                                break
                    entries.append({
                        "path": path,
                        "label": fn,
                        "tag": e.get("badge") or "TANI",
                        "entry": e,
                        "anim_id": e.get("id"),
                        "playable_path": e.get("playable_path") or "",
                    })
                self._tab_counts = {
                    "anim": int((ui.get("body_counts") or {}).get(selected_body) or 0),
                    "tani": sum(1 for e in (ui.get(key) or []) if (e.get("badge") or "").upper() == "TANI"),
                    "serial": len(ui.get("serial") or []),
                }
                self._filtered_entries = entries
                self._paint_tab_buttons()
                total = len(entries)
                pages = max(1, (total + PAGE_SIZE - 1) // PAGE_SIZE)
                if self._page >= pages:
                    self._page = max(0, pages - 1)
                start = self._page * PAGE_SIZE
                page_rows = entries[start : start + PAGE_SIZE]
                self._list_rows = page_rows
                self._visible_paths = [r["path"] for r in page_rows]
                self.clip_list.delete(0, tk.END)
                for r in page_rows:
                    aid = r.get("anim_id")
                    prefix = f"{aid}  " if aid is not None and aid != "" else ""
                    self.clip_list.insert(tk.END, f"{prefix}{r['label']}    [{r['tag']}]")
                if hasattr(self, "status_count"):
                    self.status_count.configure(text=f"{total} clips · page {self._page+1}/{pages}")
                if hasattr(self, "clip_hint"):
                    self.clip_hint.configure(text=f"search demo · {demo.get('needle')} · {total} hits")
                self._refresh_character_rail()
                if page_rows and select_first:
                    # Prefer 行走 then 跳跃 (Andy RESET); demote 蓄力.
                    idx = 0
                    for i, r in enumerate(page_rows):
                        lab = str(r.get("label") or "") + str((r.get("entry") or {}).get("local_path") or "")
                        if "行走" in lab or "走路" in lab:
                            idx = i
                            break
                    else:
                        for i, r in enumerate(page_rows):
                            lab = str(r.get("label") or "")
                            if "小跳b" in lab or "跳跃" in lab or "小跳" in lab:
                                idx = i
                                break
                    self.clip_list.selection_clear(0, tk.END)
                    self.clip_list.selection_set(idx)
                    self.clip_list.see(idx)
                    self._on_select_clip()
                return
            if tab == "serial":
                raw = ui.get("serial") or []
                for e in raw:
                    desc = e.get("desc") or ""
                    sid = e.get("serial_id", "")
                    label = f"{sid}  {desc}"
                    if needle and needle not in label.lower():
                        continue
                    entries.append({
                        "path": None,
                        "label": label,
                        "tag": "SERIAL",
                        "entry": e,
                        "anim_id": sid,
                    })
            else:
                raw = list(ui.get(key) or [])
                # Prefer local_moves for this body at front when searching FLWS / empty needle uses table
                locals_ = [e for e in (ui.get("local_moves") or []) if (e.get("body") or "").upper() == selected_body]
                if tab == "tani":
                    raw = [e for e in raw if (e.get("badge") or "").upper() == "TANI"]
                    locals_ = [e for e in locals_ if (e.get("badge") or "").upper() == "TANI"]
                elif tab == "anim":
                    # Anim Table shows table rows (mostly TANI paths) — keep all; badge shown per row
                    pass
                # merge: locals first then table (dedupe by filename)
                seen = set()
                merged = []
                for e in locals_ + raw:
                    fn = _entry_filename(e)
                    if not fn:
                        fn = str(e.get("id"))
                    if fn in seen:
                        continue
                    seen.add(fn)
                    merged.append(e)
                for e in merged:
                    e = _merge_playable(e, getattr(self, "_playable", None))
                    fn = _entry_filename(e)
                    label = fn
                    hay = f"{e.get('id','')} {fn} {e.get('anim_file','')}".lower()
                    if needle and needle not in hay:
                        continue
                    local = (e.get("local_path") or "").replace("\\", "/")
                    path = None
                    if local:
                        cand = self.samples_dir / "player" / local
                        if cand.is_file():
                            path = cand
                        else:
                            cand2 = self.samples_dir / "player" / "moves" / Path(local).name
                            if cand2.is_file():
                                path = cand2
                    entries.append({
                        "path": path,
                        "label": label,
                        "tag": e.get("badge") or "ANI",
                        "entry": e,
                        "anim_id": e.get("id"),
                        "playable_path": e.get("playable_path") or "",
                    })
            def _entry_loco_key(r):
                p = r.get("path")
                if p is not None:
                    return _loco_rank(p)
                lab = str(r.get("label") or "")
                if "行走" in lab or "走路" in lab:
                    return (0, 0, lab.lower())
                if "小跳b" in lab:
                    return (1, 0, lab.lower())
                if "小跳" in lab or "跳跃" in lab:
                    return (1, 1, lab.lower())
                if "蓄力" in lab or "风来吴山" in lab:
                    return (9, 0, lab.lower())
                return (5, 0, lab.lower())
            if tab != "serial":
                entries.sort(key=_entry_loco_key)
            self._tab_counts = {
                "anim": int((ui.get("body_counts") or {}).get(selected_body) or 0),
                "tani": sum(1 for e in (ui.get(key) or []) if (e.get("badge") or "").upper() == "TANI"),
                "serial": len(ui.get("serial") or []),
            }
        else:
            # fallback: filesystem clips (走路/跳跃 sort)
            for path in self._paths:
                body = _body_for_path(path)
                if body not in (None, selected_body):
                    continue
                name = path.name
                is_tani = name.lower().endswith(".tani")
                if tab == "anim" and is_tani:
                    continue
                if tab == "tani" and not is_tani:
                    continue
                if tab == "serial":
                    continue
                label = _clip_name(path)
                if needle and needle not in label.lower() and needle not in name.lower():
                    continue
                entries.append({"path": path, "label": label, "tag": "TANI" if is_tani else "ANI", "entry": {}, "anim_id": None})
            entries.sort(key=lambda r: _loco_rank(r["path"]) if r.get("path") else (5, 0, ""))
            body_paths = [p for p in self._paths if _body_for_path(p) in (None, selected_body)]
            self._tab_counts = {
                "anim": sum(1 for p in body_paths if not p.name.lower().endswith(".tani")),
                "tani": sum(1 for p in body_paths if p.name.lower().endswith(".tani")),
                "serial": 0,
            }

        self._filtered_entries = entries
        self._paint_tab_buttons()

        # pagination
        total = len(entries)
        pages = max(1, (total + PAGE_SIZE - 1) // PAGE_SIZE)
        if self._page >= pages:
            self._page = max(0, pages - 1)
        start = self._page * PAGE_SIZE
        page_rows = entries[start : start + PAGE_SIZE]
        self._list_rows = page_rows
        self._visible_paths = [r["path"] for r in page_rows]  # may contain None

        self.clip_list.delete(0, tk.END)
        for r in page_rows:
            aid = r.get("anim_id")
            prefix = f"{aid}  " if aid is not None and aid != "" else ""
            self.clip_list.insert(tk.END, f"{prefix}{r['label']}    [{r['tag']}]")
        if hasattr(self, "status_count"):
            self.status_count.configure(text=f"{total} clips · page {self._page+1}/{pages}")
        if hasattr(self, "status_body"):
            self.status_body.configure(text=f"Body: {selected_body}")
        self._refresh_character_rail()
        if hasattr(self, "clip_hint"):
            self.clip_hint.configure(
                text=f"{selected_body} · {tab} · {total} rows (showing {len(page_rows)})"
            )

        if page_rows and select_first:
            idx = 0
            for i, r in enumerate(page_rows):
                lab = str(r.get("label") or "") + str((r.get("entry") or {}).get("local_path") or "")
                if "行走" in lab or "走路" in lab:
                    idx = i
                    break
            else:
                for i, r in enumerate(page_rows):
                    lab = str(r.get("label") or "")
                    if "小跳b" in lab or "跳跃" in lab or "小跳" in lab:
                        idx = i
                        break
            self.clip_list.selection_clear(0, tk.END)
            self.clip_list.selection_set(idx)
            self.clip_list.see(idx)
            self._on_select_clip()
        elif not page_rows:
            self.meta.configure(text="No 动作 (clip) for this body / tab / search.")

