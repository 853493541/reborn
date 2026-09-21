# -*- coding: utf-8 -*-
"""Thor FIX on Andy: (1) jump→jump1 mapping (2) list-select sets Mixer.

Run on Andy:
  cd "C:\\Users\\Zhibin Ren\\jx3-ani-player"
  .venv\\Scripts\\python.exe APPLY_THOR_FIXES.py
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT_CANDIDATES = [
    Path(r"C:\Users\Zhibin Ren\jx3-ani-player"),
    Path(r"C:\Users\Zhibin Ren\jx3-ani-player"),
]


def find_root() -> Path:
    for r in ROOT_CANDIDATES:
        if (r / "player.py").is_file():
            return r
    cwd = Path.cwd()
    if (cwd / "player.py").is_file():
        return cwd
    raise SystemExit("player.py not found — cd to Andy jx3-ani-player first")


def find_actor(root: Path) -> Path:
    for name in ("fbx_actor.py", "fbx_actor.py"):
        p = root / name
        if p.is_file():
            return p
    raise SystemExit("fbx_actor.py missing")


def discover_clips(root: Path) -> dict[str, Path]:
    want = {
        "walk": "hualuo_walk.fbx",
        "jump1": "hualuo_jump1.fbx",
        "jump2": "hualuo_jump2.fbx",
        "jump3": "hualuo_jump3.fbx",
    }
    prefer = [
        root / "samples" / "actor_presets" / "f1_hualuo" / "mapviewer_clips",
        root / "samples" / "player" / "actors" / "f1_hualuo" / "mapviewer_clips",
        root / "samples" / "actor_presets" / "f1_hualuo",
        root / "samples" / "player" / "actors" / "f1_hualuo",
    ]
    out: dict[str, Path] = {}
    samples = root / "samples"
    for key, fname in want.items():
        hit = None
        for d in prefer:
            p = d / fname
            if p.is_file():
                hit = p
                break
        if hit is None and samples.is_dir():
            for p in samples.rglob(fname):
                hit = p
                break
        if hit is None:
            raise SystemExit(f"missing {fname} under {samples}")
        out[key] = hit
    return out


def rel_expr(root: Path, p: Path) -> str:
    parts = p.resolve().relative_to(root.resolve()).parts
    return " / ".join(repr(x) for x in parts)


CLIP_FN = r'''
def clip_fbx_for_locomotion(path_or_label: str | Path | None) -> Path | None:
    """Resolve 走路/跳跃 catalog rows / .ani names → embedded-clip FBX for Mixer.

    Contract: 小跳 / 小跳b / jump / 跳跃 / f1b02yd小跳*.ani → hualuo_jump1.fbx.
    jump2/jump3 ONLY when the name explicitly contains jump2/跳跃2/小跳2 (etc.).
    Never treat letter suffixes (小跳b) or digits inside f1b02yd as jump2/3.
    """
    if path_or_label is None:
        return None
    s = str(path_or_label).replace(chr(92), "/").lower()
    if any(k in s for k in ("跳跃", "小跳", "jump", "二段跳")):
        explicit2 = ("jump2" in s) or ("跳跃2" in s) or ("小跳2" in s)
        explicit3 = (
            ("jump3" in s)
            or ("跳跃3" in s)
            or ("小跳3" in s)
            or (("二段" in s) and ("跳" in s))
        )
        if explicit2 and CLIP_FBX_JUMP2.is_file():
            return CLIP_FBX_JUMP2
        if explicit3 and CLIP_FBX_JUMP3.is_file():
            return CLIP_FBX_JUMP3
        return CLIP_FBX_JUMP1 if CLIP_FBX_JUMP1.is_file() else None
    if any(k in s for k in ("行走", "走路", "walk")):
        if "攻击" in s:
            return None
        return CLIP_FBX_WALK if CLIP_FBX_WALK.is_file() else None
    return None
'''.lstrip("\n")


def patch_actor(text: str, root: Path, clips: dict[str, Path]) -> str:
    # constants
    assignments = {
        "CLIP_FBX_WALK": clips["walk"],
        "CLIP_FBX_JUMP1": clips["jump1"],
        "CLIP_FBX_JUMP2": clips["jump2"],
        "CLIP_FBX_JUMP3": clips["jump3"],
    }
    for name, path in assignments.items():
        line = f"{name} = ROOT / {rel_expr(root, path)}"
        if re.search(rf"^{name}\s*=", text, re.M):
            text = re.sub(rf"^{name}\s*=\s*.+$", line, text, count=1, flags=re.M)
        else:
            m = re.search(r"^ROOT\s*=\s*.+$", text, re.M)
            if not m:
                raise SystemExit("ROOT = ... not found in actor module")
            text = text[: m.end()] + "\n" + line + text[m.end() :]

    start = text.find("def clip_fbx_for_locomotion")
    if start < 0:
        text = text.rstrip() + "\n\n" + CLIP_FN + "\n"
    else:
        m = re.search(r"\ndef [A-Za-z_]", text[start + 1 :])
        end = start + 1 + m.start() if m else len(text)
        text = text[:start] + CLIP_FN + "\n" + text[end:]
    return text


PLAYER_HELPERS = '''
    def _resolve_mixer_clip_fbx(self, path, row=None):
        """行走/小跳 / driver=clipFbx → map-viewer clip FBX; else None (MIN2)."""
        try:
            from fbx_actor import clip_fbx_for_locomotion
        except ImportError:
            from fbx_actor import clip_fbx_for_locomotion
        if isinstance(row, dict):
            for key in ("clip_fbx", "clipFbx", "clip_fbx_path"):
                v = row.get(key)
                if v:
                    p = Path(v)
                    if not p.is_file():
                        p = Path(__file__).resolve().parent / v
                    if p.is_file():
                        return p
            for blob in (
                row.get("label"),
                row.get("name"),
                (row.get("entry") or {}).get("filename"),
                (row.get("entry") or {}).get("label"),
                row.get("driver"),
            ):
                if blob:
                    hit = clip_fbx_for_locomotion(blob)
                    if hit is not None:
                        return hit
        if path is not None:
            return clip_fbx_for_locomotion(path)
        return None

    def _nav_fbx_viewport(self, *, clip_fbx=None, clipFbx=None, t=None, playing: bool = False, label: str = "") -> None:
        """Reload viewport URL for skin + optional clipFbx Mixer (forced-API twin)."""
        clip_fbx = clip_fbx if clip_fbx is not None else clipFbx
        actor = getattr(self, "_fbx_actor", None)
        if actor is None:
            return
        try:
            try:
                from fbx_actor import web_viewport_url as _url_fn
            except ImportError:
                from fbx_actor import web_viewport_url as _url_fn
            url = _url_fn(actor, closeup=True, clip_fbx=clip_fbx, t=t, playing=playing)
        except Exception:
            return
        self._fbx_url = url
        self._fbx_mixer_url = url
        self._fbx_last_nav = url
        self._fbx_mixer_clip = clip_fbx
        frame = getattr(self, "_fbx_frame", None)
        try:
            if frame is not None and hasattr(frame, "load_url"):
                frame.load_url(url)
        except Exception:
            pass
        tag = Path(clip_fbx).name if clip_fbx else "bind"
        extra = label or tag
        mode = "Mixer play" if playing else (f"Mixer t={t:.2f}" if t is not None else "Mixer")
        for attr in ("vp_label", "vp_hint"):
            w = getattr(self, attr, None)
            if w is not None and hasattr(w, "configure"):
                try:
                    w.configure(text=f"花萝 FBX · {mode} · {extra}")
                except Exception:
                    pass

'''


SELECT_HOOK = '''        # Thor FIX2: list select → Mixer for 行走/小跳 (same path as forced _nav_fbx_viewport)
        _row = None
        try:
            _sel = self.clip_list.curselection()
            if _sel and hasattr(self, "_list_rows"):
                _row = self._list_rows[_sel[0]]
        except Exception:
            _row = locals().get("row")
        mix = self._resolve_mixer_clip_fbx(path, _row)
        if mix is not None:
            _lab = ""
            try:
                _lab = _clip_name(path)
            except Exception:
                _lab = Path(path).name if path is not None else ""
            self._nav_fbx_viewport(clip_fbx=mix, t=0.0, playing=False, label=_lab)
        else:
            self._fbx_mixer_clip = None
        self._draw_frame()'''


def patch_player(text: str) -> str:
    # state fields
    if "self._fbx_mixer_clip" not in text:
        for needle in (
            "self._fbx_frame = None",
            "self._fbx_url = None",
            "self._fbx_actor = None",
        ):
            if needle in text:
                text = text.replace(
                    needle,
                    needle
                    + "\n        self._fbx_mixer_clip = None  # Path|None clipFbx Mixer mode\n"
                    + "        self._fbx_mixer_url = None\n"
                    + "        self._fbx_last_nav = None",
                    1,
                )
                break

    # helpers: replace or insert
    if "def _resolve_mixer_clip_fbx" in text:
        start = text.find("def _resolve_mixer_clip_fbx")
        start = text.rfind("\n", 0, start) + 1
        nav = text.find("def _nav_fbx_viewport", start)
        if nav < 0:
            raise SystemExit("_resolve present but _nav_fbx_viewport missing")
        rest = text[nav + 1 :]
        m = re.search(r"\n    def ", rest)
        end = nav + 1 + m.start() + 1 if m else len(text)
        text = text[:start] + PLAYER_HELPERS.lstrip("\n") + text[end:]
    else:
        for anchor in ("def _sync_fbx_pose", "def _on_select_clip", "def _draw_frame"):
            i = text.find(anchor)
            if i >= 0:
                i = text.rfind("\n", 0, i) + 1
                text = text[:i] + PLAYER_HELPERS + text[i:]
                break
        else:
            raise SystemExit("no insert point for mixer helpers")

    # pose sync skip
    if 'getattr(self, "_fbx_mixer_clip"' not in text:
        for sig in (
            "def _sync_fbx_pose(self, sample) -> None:",
            "def _sync_fbx_pose(self, sample):",
        ):
            if sig in text:
                text = text.replace(
                    sig,
                    sig
                    + "\n        if getattr(self, \"_fbx_mixer_clip\", None) is not None:\n"
                    + "            return  # Mixer owns locomotion; skip MIN2",
                    1,
                )
                break

    # wire select
    sel_start = text.find("def _on_select_clip")
    if sel_start < 0:
        raise SystemExit("_on_select_clip not found")
    m = re.search(r"\n    def ", text[sel_start + 1 :])
    sel_end = sel_start + 1 + m.start() if m else len(text)
    body = text[sel_start:sel_end]
    if "_resolve_mixer_clip_fbx" in body and "_nav_fbx_viewport" in body:
        print("select already wired")
    else:
        idx = body.rfind("self._draw_frame()")
        if idx < 0:
            raise SystemExit("_on_select_clip: no self._draw_frame()")
        body = body[:idx] + SELECT_HOOK + body[idx + len("self._draw_frame()") :]
        text = text[:sel_start] + body + text[sel_end:]
        print("select wired")
    return text


def patch_catalog(root: Path, clips: dict[str, Path]) -> None:
    paths = []
    for pat in ("playable_f1_hualuo.json", "playable_f1_hualuo.json", "*hualuo*.json", "*playable*.json"):
        for pth in root.joinpath("samples").rglob(pat):
            if pth not in paths:
                paths.append(pth)
    if not paths:
        print("WARN no catalog")
        return
    walk_rel = clips["walk"].resolve().relative_to(root.resolve()).as_posix()
    jump_rel = clips["jump1"].resolve().relative_to(root.resolve()).as_posix()
    jump2_rel = clips["jump2"].resolve().relative_to(root.resolve()).as_posix()
    jump3_rel = clips["jump3"].resolve().relative_to(root.resolve()).as_posix()
    for cat in paths:
        try:
            data = json.loads(cat.read_text(encoding="utf-8"))
        except Exception as e:
            print("skip", cat, e)
            continue
        rows = None
        for key in ("default_clips", "clips", "items", "rows"):
            if isinstance(data.get(key), list):
                rows = data[key]
                break
        if rows is None and isinstance(data, list):
            rows = data
        if not rows:
            continue
        n = 0
        for row in rows:
            if not isinstance(row, dict):
                continue
            lab = str(row.get("label") or row.get("name") or "")
            blob = lab + json.dumps(row, ensure_ascii=False)
            if any(k in blob for k in ("行走", "走路", "walk")) and "攻击" not in blob:
                row["clip_fbx"] = walk_rel
                row["driver"] = "clipFbx"
                n += 1
            if any(k in blob for k in ("跳跃", "小跳", "jump")):
                if any(k in lab for k in ("jump2", "跳跃2", "小跳2")):
                    row["clip_fbx"] = jump2_rel
                elif any(k in lab for k in ("jump3", "跳跃3", "小跳3", "二段")):
                    row["clip_fbx"] = jump3_rel
                else:
                    row["clip_fbx"] = jump_rel  # 小跳b → jump1
                row["driver"] = "clipFbx"
                n += 1
        cat.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"catalog {cat}: {n} rows")


def verify(root: Path, actor: Path) -> None:
    sys.path.insert(0, str(root))
    import importlib

    mod_name = actor.stem
    sys.modules.pop(mod_name, None)
    mod = importlib.import_module(mod_name)
    fn = mod.clip_fbx_for_locomotion
    expect = [
        ("f1b02yd小跳a.ani", "hualuo_jump1.fbx"),
        ("小跳", "hualuo_jump1.fbx"),
        ("小跳b", "hualuo_jump1.fbx"),
        ("跳跃", "hualuo_jump1.fbx"),
        ("jump", "hualuo_jump1.fbx"),
        ("f1b02yd小跳b.ani", "hualuo_jump1.fbx"),
        ("行走", "hualuo_walk.fbx"),
        ("走路", "hualuo_walk.fbx"),
        ("小跳2", "hualuo_jump2.fbx"),
        ("jump3", "hualuo_jump3.fbx"),
    ]
    for inp, exp in expect:
        got = fn(inp)
        name = got.name if got else None
        print(f"MAP {inp!r} → {name}")
        if name != exp:
            raise SystemExit(f"FAIL {inp}: {name} != {exp}")
    pl = (root / "player.py").read_text(encoding="utf-8")
    compile(pl, "player.py", "exec")
    compile(actor.read_text(encoding="utf-8"), actor.name, "exec")
    for need in ("_fbx_mixer_clip", "_nav_fbx_viewport", "_resolve_mixer_clip_fbx"):
        if need not in pl:
            raise SystemExit(f"player missing {need}")
    sel = pl[pl.find("def _on_select_clip") :]
    if "_nav_fbx_viewport" not in sel[:12000]:
        raise SystemExit("select path does not call _nav_fbx_viewport")
    print("VERIFY_OK")


def main() -> None:
    root = find_root()
    print("ROOT", root)
    actor = find_actor(root)
    print("ACTOR", actor)
    clips = discover_clips(root)
    for k, v in clips.items():
        print("CLIP", k, v)

    actor.write_text(patch_actor(actor.read_text(encoding="utf-8"), root, clips), encoding="utf-8")
    print("patched actor")

    pl_path = root / "player.py"
    pl_path.write_text(patch_player(pl_path.read_text(encoding="utf-8")), encoding="utf-8")
    print("patched player")

    patch_catalog(root, clips)
    verify(root, actor)
    print("DONE_THOR_FIXES")


if __name__ == "__main__":
    main()
