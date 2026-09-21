# -*- coding: utf-8 -*-
"""Wire Tk player 走路/跳跃 to skin+clipFbx Mixer (not MIN2 apply_pose)."""
from __future__ import annotations

from pathlib import Path

ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")

# --- fbx_actor.py: viewport_url + clip helpers ---
fa_path = ROOT / "fbx_actor.py"
fa = fa_path.read_text(encoding="utf-8")

if "def clip_fbx_for_locomotion" not in fa:
    helper = r'''

# Map-viewer locomotion clip-source FBXs (ASCII names under mapviewer_clips/).
CLIP_FBX_WALK = ROOT / "samples" / "actor_presets" / "f1_hualuo" / "mapviewer_clips" / "hualuo_walk.fbx"
CLIP_FBX_JUMP1 = ROOT / "samples" / "actor_presets" / "f1_hualuo" / "mapviewer_clips" / "hualuo_jump1.fbx"
CLIP_FBX_JUMP2 = ROOT / "samples" / "actor_presets" / "f1_hualuo" / "mapviewer_clips" / "hualuo_jump2.fbx"
CLIP_FBX_JUMP3 = ROOT / "samples" / "actor_presets" / "f1_hualuo" / "mapviewer_clips" / "hualuo_jump3.fbx"


def clip_fbx_for_locomotion(path_or_label: str | Path | None) -> Path | None:
    """Resolve 走路/跳跃 catalog rows / .ani names → embedded-clip FBX for Mixer.

    Returns None for non-locomotion clips (keep MIN2/debug paths elsewhere).
    """
    if path_or_label is None:
        return None
    s = str(path_or_label).replace("\\", "/").lower()
    name = Path(s).name
    # jump first (小跳 / 跳跃 / jump)
    if any(k in s for k in ("跳跃", "小跳", "jump", "二段跳")):
        if "2" in name or "b" in Path(name).stem[-2:]:
            if CLIP_FBX_JUMP2.is_file():
                return CLIP_FBX_JUMP2
        if "3" in name or "c" in Path(name).stem[-2:]:
            if CLIP_FBX_JUMP3.is_file():
                return CLIP_FBX_JUMP3
        return CLIP_FBX_JUMP1 if CLIP_FBX_JUMP1.is_file() else None
    if any(k in s for k in ("行走", "走路", "walk", "奔跑", "run")):
        # exclude attack-ish false positives
        if "攻击" in s:
            return None
        return CLIP_FBX_WALK if CLIP_FBX_WALK.is_file() else None
    return None


def web_viewport_url(
    actor: "FbxActor",
    *,
    closeup: bool = True,
    clip_fbx: Path | str | None = None,
    t: float | None = None,
    playing: bool = False,
) -> str:
    """Product URL on repo-root server: /web/fbx_viewport.html + skin + optional clipFbx."""
    ensure_server(actor)
    q: dict = {
        "fbx": "/samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx",
        "tex": "/samples/actor_presets/f1_hualuo/tex/",
        "closeup": "1" if closeup else "0",
    }
    # Prefer mapviewer_clips copy of no_anim if present (same bytes).
    no_anim_mv = ROOT / "samples" / "actor_presets" / "f1_hualuo" / "mapviewer_clips" / "hualuo_no_anim.fbx"
    if no_anim_mv.is_file():
        q["fbx"] = "/samples/actor_presets/f1_hualuo/mapviewer_clips/hualuo_no_anim.fbx"
    if clip_fbx:
        p = Path(clip_fbx)
        # URL under /samples/...
        try:
            rel = p.resolve().relative_to(ROOT.resolve()).as_posix()
            q["clipFbx"] = "/" + rel
        except Exception:
            q["clipFbx"] = "/samples/actor_presets/f1_hualuo/mapviewer_clips/" + p.name
    # Freeze at t when scrubbing; omit t while playing so Mixer advances.
    if not playing and t is not None and t >= 0:
        q["t"] = f"{float(t):.4f}"
    return f"http://127.0.0.1:{actor._port}/web/{VIEWPORT_HTML}?{urlencode(q)}"

'''
    # insert after DEFAULT_TEX / VIEWPORT_HTML block
    needle = 'VIEWPORT_HTML = "fbx_viewport.html"'
    if needle not in fa:
        raise SystemExit("VIEWPORT_HTML missing")
    fa = fa.replace(needle, needle + "\n" + helper, 1)

    # extend FbxActor.viewport_url to delegate
    old_vu = '''    def viewport_url(self, closeup: bool = True) -> str:
        if not self._port:
            raise RuntimeError("HTTP server not started; call ensure_server() or paint_to()")
        q = urlencode(
            {
                "fbx": f"/samples/actor_presets/f1_hualuo/{self.fbx_path.name}",
                "tex": "/samples/actor_presets/f1_hualuo/tex/",
                "closeup": "1" if closeup else "0",
            }
        )
        return f"http://127.0.0.1:{self._port}/web/{VIEWPORT_HTML}?{q}"'''
    new_vu = '''    def viewport_url(
        self,
        closeup: bool = True,
        *,
        clip_fbx: Path | str | None = None,
        t: float | None = None,
        playing: bool = False,
    ) -> str:
        if not self._port:
            raise RuntimeError("HTTP server not started; call ensure_server() or paint_to()")
        return web_viewport_url(
            self, closeup=closeup, clip_fbx=clip_fbx, t=t, playing=playing
        )'''
    if old_vu not in fa:
        raise SystemExit("viewport_url block missing")
    fa = fa.replace(old_vu, new_vu, 1)
    fa_path.write_text(fa, encoding="utf-8")
    print("fbx_actor.py patched")

# --- player.py ---
pl_path = ROOT / "player.py"
pl = pl_path.read_text(encoding="utf-8")

# Add mixer state fields after _fbx_frame
if "self._fbx_mixer_clip" not in pl:
    pl = pl.replace(
        "self._fbx_frame = None",
        "self._fbx_frame = None\n"
        "        self._fbx_mixer_clip = None  # Path|None — clipFbx when locomotion Mixer mode\n"
        "        self._fbx_mixer_url = None\n"
        "        self._fbx_last_nav = None",
        1,
    )

# Replace _init_fbx_viewport to prefer fbx_actor repo server
old_init = '''    def _init_fbx_viewport(self) -> None:
        """Host Dev4 Three.js 花萝 FBX on locked viewport_fbx/ :8765 (not blue LBS)."""
        from urllib.parse import urlencode

        try:
            from fbx_actor import load_fbx_actor, paint_to
        except Exception as exc:
            self.vp_label.configure(text=f"FBX actor unavailable — {exc}")
            self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=4, pady=4)
            return
        try:
            self._fbx_actor = load_fbx_actor()
            # Dev4 bridge: viewport_fbx/ http.server on :8765 + pose_by_name.json poller.
            q = urlencode(
                {
                    "fbx": "./samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx",
                    "tex": "./samples/actor_presets/f1_hualuo/tex/",
                    "frame": "bind",
                }
            )
            locked = f"http://127.0.0.1:8765/index.html?{q}"
            # Prefer locked host; fall back to paint_to webview URL if :8765 is down.
            try:
                import urllib.request
                with urllib.request.urlopen(locked, timeout=0.4) as resp:
                    ok = 200 <= getattr(resp, "status", 200) < 500
            except Exception:
                ok = False
            if ok:
                self._fbx_url = locked
            else:
                meta = paint_to(self._fbx_actor, "webview")
                self._fbx_url = meta.get("url")
        except Exception as exc:
            self.vp_label.configure(text=f"FBX load failed — {exc}")
            self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=4, pady=4)
            return'''

new_init = '''    def _init_fbx_viewport(self) -> None:
        """Host Three.js 花萝 on fbx_actor repo-root server (skin+clipFbx Mixer path)."""
        try:
            from fbx_actor import load_fbx_actor, ensure_server, web_viewport_url
        except Exception as exc:
            self.vp_label.configure(text=f"FBX actor unavailable — {exc}")
            self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=4, pady=4)
            return
        try:
            self._fbx_actor = load_fbx_actor()
            ensure_server(self._fbx_actor)
            self._fbx_url = web_viewport_url(self._fbx_actor, closeup=True)
            self._fbx_mixer_clip = None
            self._fbx_mixer_url = self._fbx_url
            self._fbx_last_nav = self._fbx_url
        except Exception as exc:
            self.vp_label.configure(text=f"FBX load failed — {exc}")
            self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=4, pady=4)
            return'''

if old_init not in pl:
    raise SystemExit("_init_fbx_viewport block missing/changed")
pl = pl.replace(old_init, new_init, 1)

# Add helper methods before _sync_fbx_pose
helpers = '''
    def _resolve_mixer_clip_fbx(self, path: Path | None):
        """走路/跳跃 → map-viewer clip-source FBX; else None (MIN2 pose path)."""
        try:
            from fbx_actor import clip_fbx_for_locomotion
        except Exception:
            return None
        if path is None:
            return None
        return clip_fbx_for_locomotion(path)

    def _nav_fbx_viewport(self, *, clip_fbx=None, t=None, playing: bool = False, label: str = "") -> None:
        """Reload HtmlFrame / open URL for skin+optional clipFbx Mixer."""
        if self._fbx_actor is None:
            return
        try:
            from fbx_actor import web_viewport_url
            url = web_viewport_url(
                self._fbx_actor,
                closeup=True,
                clip_fbx=clip_fbx,
                t=t,
                playing=playing,
            )
        except Exception:
            return
        if url == getattr(self, "_fbx_last_nav", None) and not playing:
            # still allow t scrub updates
            if t is None:
                return
        self._fbx_url = url
        self._fbx_mixer_url = url
        self._fbx_last_nav = url
        self._fbx_mixer_clip = clip_fbx
        try:
            if self._fbx_frame is not None:
                self._fbx_frame.load_url(url)
        except Exception:
            pass
        if hasattr(self, "vp_label"):
            tag = Path(clip_fbx).name if clip_fbx else "bind"
            extra = label or tag
            mode = "Mixer play" if playing else ("Mixer t=" + f"{t:.2f}" if t is not None else "Mixer")
            self.vp_label.configure(text=f"花萝 FBX · {mode} · {extra}")

    def _sync_fbx_pose(self, sample) -> None:
        """MIN2 locals → apply_pose only when NOT in locomotion Mixer mode."""
        if self._fbx_actor is None or sample is None:
            return
        # Locomotion uses AnimationMixer + clipFbx — never push MIN2 matrices.
        if getattr(self, "_fbx_mixer_clip", None) is not None:
            return
'''

# Replace existing _sync_fbx_pose with helpers + new sync (avoid duplicate)
old_sync = '''    def _sync_fbx_pose(self, sample) -> None:
        """Publish local matrices → fbx_actor.apply_pose (FBX needs parent-space)."""
        if self._fbx_actor is None or sample is None:
            return
        # Prefer local_matrices; world matrices explode bone.decompose on FBX.
        mats = getattr(sample, "local_matrices", None)
        if mats is None:
            return
        try:
            from fbx_actor import apply_pose
            bone_names = list(getattr(self.clip, "bone_names", None) or [])
            apply_pose(
                self._fbx_actor,
                mats,
                bone_names=bone_names,
                frame=int(getattr(sample, "frame", 0) or 0),
            )
        except Exception:
            pass'''

new_sync_tail = '''
        # Prefer local_matrices; world matrices explode bone.decompose on FBX.
        mats = getattr(sample, "local_matrices", None)
        if mats is None:
            return
        try:
            from fbx_actor import apply_pose
            bone_names = list(getattr(self.clip, "bone_names", None) or [])
            apply_pose(
                self._fbx_actor,
                mats,
                bone_names=bone_names,
                frame=int(getattr(sample, "frame", 0) or 0),
            )
        except Exception:
            pass'''

if old_sync not in pl:
    raise SystemExit("_sync_fbx_pose missing")
pl = pl.replace(old_sync, helpers + new_sync_tail, 1)

# After successful clip load in the method that sets self.clip — find _draw_frame call after load
# Hook: after `self._draw_frame()` following clip load (around 1463)
load_hook = '''        self.clip_hint.configure(text=f"{_clip_name(path)}  ·  Ani  ·  {self.body_var.get()}")
        self._draw_frame()'''

load_hook_new = '''        self.clip_hint.configure(text=f"{_clip_name(path)}  ·  Ani  ·  {self.body_var.get()}")
        # Product: 走路/跳跃 → skin+clipFbx Mixer (map-viewer). Else MIN2 pose feed.
        mix = self._resolve_mixer_clip_fbx(path)
        if mix is not None and self.character_mode == "fbx" and self._fbx_actor is not None:
            self._nav_fbx_viewport(clip_fbx=mix, t=0.0, playing=False, label=_clip_name(path))
        else:
            self._fbx_mixer_clip = None
            if self.character_mode == "fbx" and self._fbx_actor is not None:
                self._nav_fbx_viewport(clip_fbx=None, t=None, playing=False, label="bind")
        self._draw_frame()'''

if load_hook not in pl:
    raise SystemExit("load hook missing")
pl = pl.replace(load_hook, load_hook_new, 1)

# play(): when mixer mode, nav with playing=True
old_play = '''        # Drive live 花萝 FBX viewport via apply_pose each tick.
        if self._fbx_actor is not None and hasattr(self, "vp_label"):
            self.vp_label.configure(text=f"Play · FBX 花萝 · {getattr(self.clip, 'tex_hint', '') or 'MIN2'}")
        self.clock.play()'''

new_play = '''        # Locomotion: Mixer free-run. Other clips: MIN2 apply_pose each tick.
        if self._fbx_actor is not None and getattr(self, "_fbx_mixer_clip", None) is not None:
            self._nav_fbx_viewport(
                clip_fbx=self._fbx_mixer_clip,
                t=None,
                playing=True,
                label=_clip_name(getattr(self, "_clip_path", None) or Path(".")),
            )
        elif self._fbx_actor is not None and hasattr(self, "vp_label"):
            self.vp_label.configure(text=f"Play · FBX 花萝 · {getattr(self.clip, 'tex_hint', '') or 'MIN2'}")
        self.clock.play()'''

if old_play not in pl:
    raise SystemExit("play hook missing")
pl = pl.replace(old_play, new_play, 1)

# _draw_frame: when mixer mode and scrubbing, update t=
old_draw_fbx = '''        # Dev4 FBX actor — write pose.json for Three poller each paint.
        self._sync_fbx_pose(sample)

        # When FBX host is primary and Mesh RQ is off, skip matplotlib LBS.
        if (
            self.character_mode == "fbx"
            and self._fbx_actor is not None
            and not self.skinned.get()
        ):
            fps = float(getattr(self.clip, "fps", 0) or self.fps or FPS_DEFAULT)
            t = f / fps
            total = (self.clip.frame_count - 1) / fps
            self.time_lbl.configure(
                text=f"{t:0.2f} / {total:0.2f}s   ·   frame {f}   ·   fbx 花萝"
            )
            return'''

new_draw_fbx = '''        # Dev4 FBX: Mixer locomotion OR MIN2 apply_pose.
        mix_clip = getattr(self, "_fbx_mixer_clip", None)
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

        self._sync_fbx_pose(sample)

        # When FBX host is primary and Mesh RQ is off, skip matplotlib LBS.
        if (
            self.character_mode == "fbx"
            and self._fbx_actor is not None
            and not self.skinned.get()
        ):
            fps = float(getattr(self.clip, "fps", 0) or self.fps or FPS_DEFAULT)
            t = f / fps
            total = (self.clip.frame_count - 1) / fps
            self.time_lbl.configure(
                text=f"{t:0.2f} / {total:0.2f}s   ·   frame {f}   ·   fbx 花萝"
            )
            return'''

if old_draw_fbx not in pl:
    raise SystemExit("draw fbx block missing")
pl = pl.replace(old_draw_fbx, new_draw_fbx, 1)

# Store clip path on load for play label — find where path is used in load
# Add self._clip_path = path near start of successful load — after magic sniff success
if "self._clip_path = path" not in pl:
    pl = pl.replace(
        "self.clip = self.clock.clip\n            else:",
        "self.clip = self.clock.clip\n                self._clip_path = path\n            else:",
        1,
    )
    pl = pl.replace(
        "self.clip = load_mina(path)\n                self.clock.set_clip(self.clip, seek_start=True)",
        "self.clip = load_mina(path)\n                self._clip_path = path\n                self.clock.set_clip(self.clip, seek_start=True)",
        1,
    )

pl_path.write_text(pl, encoding="utf-8")
print("player.py patched")

# --- catalog: add clip_fbx fields ---
cat_path = ROOT / "samples" / "player" / "catalog" / "playable_f1_hualuo.json"
import json
cat = json.loads(cat_path.read_text(encoding="utf-8"))
for row in cat.get("default_clips", []):
    lab = row.get("label") or ""
    kind = row.get("kind") or ""
    if kind == "walk" or "行走" in lab or "走路" in lab:
        row["clip_fbx"] = "samples/actor_presets/f1_hualuo/mapviewer_clips/hualuo_walk.fbx"
        row["driver"] = "clipFbx"
    if kind in ("jump", "run") or "跳跃" in lab or "小跳" in lab:
        row["clip_fbx"] = "samples/actor_presets/f1_hualuo/mapviewer_clips/hualuo_jump1.fbx"
        row["driver"] = "clipFbx"
# ensure jump entry exists
labels = {r.get("label") for r in cat.get("default_clips", [])}
if "跳跃" not in labels and "小跳" not in "".join(labels):
    cat.setdefault("default_clips", []).append({
        "label": "跳跃",
        "kind": "jump",
        "body": "F1",
        "playable_path": "moves/from_mapviewer/f1/动作/f1b02yd小跳a.ani",
        "filename": "f1b02yd小跳a.ani",
        "badge": "ANI",
        "preset": "花萝",
        "clip_fbx": "samples/actor_presets/f1_hualuo/mapviewer_clips/hualuo_jump1.fbx",
        "driver": "clipFbx",
    })
cat_path.write_text(json.dumps(cat, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("catalog updated")

# smoke: import + resolve
import sys
sys.path.insert(0, str(ROOT))
from fbx_actor import clip_fbx_for_locomotion, load_fbx_actor, ensure_server, web_viewport_url
assert clip_fbx_for_locomotion("f1b02yd行走.ani").name == "hualuo_walk.fbx"
assert clip_fbx_for_locomotion("F1_A081_行走.ani").name == "hualuo_walk.fbx"
assert clip_fbx_for_locomotion("f1b02yd小跳a.ani").name.startswith("hualuo_jump")
actor = load_fbx_actor()
ensure_server(actor)
url = web_viewport_url(actor, clip_fbx=clip_fbx_for_locomotion("行走"), t=0.63)
print("URL", url)
assert "clipFbx" in url and "hualuo_walk" in url
print("SMOKE OK")
