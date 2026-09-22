"""Textured F1 花萝 actor via map-viewer FBX path (not blue LBS mesh.py).

Dev3 API:
    from fbx_actor import load_fbx_actor, apply_pose, paint_to
    actor = load_fbx_actor(Path('samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx'))
    apply_pose(actor, sample.matrices, bone_names=clip.bone_names)  # follow-up bind
    paint_to(actor, surface)  # Path → PNG via Three viewport; Axes → status note

Primary surface is the Three.js page at web/fbx_viewport.html (FBXLoader +
HemisphereLight/DirectionalLight + prepareAnchorRigMaterials). mesh.py RQ LBS
remains the matplotlib fallback only — never claim LBS as visual parity.
"""
from __future__ import annotations

import json
import shutil
import subprocess
import threading
import time
import webbrowser
from dataclasses import dataclass, field
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, List, Optional, Sequence, Union
from urllib.parse import urlencode, urlparse

ROOT = Path(__file__).resolve().parent
DEFAULT_FBX = ROOT / "samples" / "actor_presets" / "f1_hualuo" / "hualuo_no_anim.fbx"
DEFAULT_TEX = ROOT / "samples" / "actor_presets" / "f1_hualuo" / "tex"
WEB_DIR = ROOT / "web"
VIEWPORT_HTML = "fbx_viewport.html"


# Map-viewer locomotion clip-source FBXs (ASCII names under mapviewer_clips/).
CLIP_FBX_WALK = ROOT / 'samples' / 'actor_presets' / 'f1_hualuo' / 'mapviewer_clips' / 'hualuo_walk.fbx'
CLIP_FBX_JUMP1 = ROOT / 'samples' / 'actor_presets' / 'f1_hualuo' / 'mapviewer_clips' / 'hualuo_jump1.fbx'
CLIP_FBX_JUMP2 = ROOT / 'samples' / 'actor_presets' / 'f1_hualuo' / 'mapviewer_clips' / 'hualuo_jump2.fbx'
CLIP_FBX_JUMP3 = ROOT / 'samples' / 'actor_presets' / 'f1_hualuo' / 'mapviewer_clips' / 'hualuo_jump3.fbx'
CLIP_JSON_FLWS_CHARGE = ROOT / "web" / "runtime" / "clip_flws_charge.json"
CLIP_JSON_FLWS_CAST = ROOT / "web" / "runtime" / "clip_flws_cast.json"


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




def clip_json_for_flws(path_or_label: str | Path | None) -> Path | None:
    """风来吴山·蓄力/释放 → MIN2 AnimationClip JSON for Mixer (no FBX take)."""
    if path_or_label is None:
        return None
    s = str(path_or_label).replace(chr(92), "/").lower()
    # charge / 蓄力
    if ("蓄力" in s) or ("charge" in s) or ("奇穴" in s and "f1s07" in s):
        return CLIP_JSON_FLWS_CHARGE if CLIP_JSON_FLWS_CHARGE.is_file() else None
    # cast / 释放 / fenglaiwushan cast (not 蓄力)
    if (
        ("释放" in s)
        or ("风来吴山" in s)
        or ("fenglaiwushan" in s)
        or ("flws" in s)
        or ("f1s07cj" in s and "蓄力" not in s)
    ):
        # Prefer cast JSON; if label is generic 风来吴山 without 蓄力, cast.
        if "蓄力" in s:
            return CLIP_JSON_FLWS_CHARGE if CLIP_JSON_FLWS_CHARGE.is_file() else None
        return CLIP_JSON_FLWS_CAST if CLIP_JSON_FLWS_CAST.is_file() else None
    return None

def web_viewport_url(
    actor: "FbxActor",
    *,
    closeup: bool = True,
    clip_fbx: Path | str | None = None,
    clip_json: Path | str | None = None,
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
    if clip_json:
        jp = Path(clip_json)
        try:
            rel = jp.resolve().relative_to(ROOT.resolve()).as_posix()
            q["clip"] = "/" + rel
        except Exception:
            q["clip"] = "/web/runtime/" + jp.name
        # The FLWS clip rows are the first real SFX vertical slice. The web
        # viewport keeps the existing body mixer and adds the PSS timeline
        # against the same actor/transport. Keep the character visible: with
        # sfx=flws the page would otherwise default to the SFX-only box mode.
        q["sfx"] = "flws"
        q["model"] = "fbx"
        # The authored FLWS range is much wider than the close-up character
        # framing; keep the full red ring in view for the focused FLWS path.
        q["closeup"] = "0"
    # Freeze at t when scrubbing; omit t while playing so Mixer advances.
    if not playing and t is not None and t >= 0:
        q["t"] = f"{float(t):.4f}"
    return f"http://127.0.0.1:{actor._port}/web/{VIEWPORT_HTML}?{urlencode(q)}"




@dataclass
class FbxActor:
    """Handle for the MovieEditor 花萝 FBX + tex/ preset."""

    fbx_path: Path
    tex_dir: Path
    preset_name: str = "花萝"
    bone_names: List[str] = field(default_factory=list)
    pose_matrices: Any = None  # optional MIN2 stick matrices (follow-up)
    pose_bone_names: Optional[List[str]] = None
    _server: Any = field(default=None, repr=False)
    _port: int = 0
    meta: dict = field(default_factory=dict)

    @property
    def ready(self) -> bool:
        return self.fbx_path.is_file() and self.tex_dir.is_dir()

    def viewport_url(
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
        )


def _resolve_tex_dir(fbx_path: Path) -> Path:
    sibling = fbx_path.parent / "tex"
    if sibling.is_dir():
        return sibling
    if DEFAULT_TEX.is_dir():
        return DEFAULT_TEX
    raise FileNotFoundError(f"No tex/ next to {fbx_path} and no default at {DEFAULT_TEX}")


def load_fbx_actor(path: Union[str, Path] = DEFAULT_FBX) -> FbxActor:
    """Load 花萝 actor preset (FBX + tex/). Does not parse mesh into numpy LBS."""
    fbx_path = Path(path).resolve()
    if not fbx_path.is_file():
        raise FileNotFoundError(
            f"FBX not found: {fbx_path} (expected samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx)"
        )
    tex_dir = _resolve_tex_dir(fbx_path)
    n_tex = sum(1 for _ in tex_dir.iterdir() if _.is_file())
    actor = FbxActor(
        fbx_path=fbx_path,
        tex_dir=tex_dir,
        meta={"texture_files": n_tex, "fbx_bytes": fbx_path.stat().st_size},
    )
    return actor


_POSE_JSON = WEB_DIR / "runtime" / "pose.json"
_POSE_BY_NAME = ROOT / "viewport_fbx" / "runtime" / "pose_by_name.json"
# web/fbx_viewport.html polls ./runtime/ under /web/ — twin required.
_POSE_BY_NAME_WEB = WEB_DIR / "runtime" / "pose_by_name.json"
_NAV_JSON = WEB_DIR / "runtime" / "nav.json"
_NAV_JSON_VIEWPORT = ROOT / "viewport_fbx" / "runtime" / "nav.json"
_pose_ver = 0
_nav_ver = 0


def publish_nav(url: str) -> None:
    """Broadcast the current viewport URL so external browser tabs follow clip
    selection made in the Tk app (embedded webview cannot run WebGL)."""
    global _nav_ver
    _nav_ver += 1
    payload = json.dumps({"v": _nav_ver, "url": url}) + "\n"
    for dest in (_NAV_JSON, _NAV_JSON_VIEWPORT):
        dest.parent.mkdir(parents=True, exist_ok=True)
        tmp = dest.with_suffix(".tmp")
        tmp.write_text(payload, encoding="utf-8")
        tmp.replace(dest)


def mat3x4_to_mat4_colmajor(m12: Sequence[float]) -> List[float]:
    """Dev2 ``matrices_at`` 3×4 row-major R|t → THREE.Matrix4 column-major 16 floats."""
    if m12 is None or len(m12) < 12:
        return [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    r = [float(x) for x in m12[:12]]
    # row0: r0 r1 r2 tx | row1: r4 r5 r6 ty | row2: r8 r9 r10 tz
    return [
        r[0], r[4], r[8], 0.0,
        r[1], r[5], r[9], 0.0,
        r[2], r[6], r[10], 0.0,
        r[3], r[7], r[11], 1.0,
    ]


def pose_for_webview(
    matrices: Any,
    bone_names: Optional[Sequence[str]] = None,
) -> dict:
    """Payload for Dev3 ``window.__applyPose({ boneName: Float32Array(16) })`` (JSON lists)."""
    names = list(bone_names or [])
    mats = list(matrices or [])
    out: dict = {}
    for i in range(min(len(names), len(mats))):
        name = str(names[i] or "")
        if not name:
            continue
        m16 = mat3x4_to_mat4_colmajor(mats[i])
        out[name] = m16
        out[name.lower()] = m16
    return out


def apply_pose(
    actor: FbxActor,
    matrices: Any,
    bone_names: Optional[Sequence[str]] = None,
    *,
    frame: int = 0,
) -> FbxActor:
    """Publish pose for Dev3 viewport host + legacy poller.

    Writes ``viewport_fbx/runtime/pose_by_name.json`` for
    ``window.__applyPose`` and ``web/runtime/pose.json`` for the older page.
    """
    global _pose_ver
    actor.pose_matrices = matrices
    if bone_names is not None:
        actor.pose_bone_names = list(bone_names)
        if not actor.bone_names:
            actor.bone_names = list(bone_names)
    names = list(actor.pose_bone_names or bone_names or [])
    by_name = pose_for_webview(matrices, names)
    _pose_ver += 1

    _POSE_JSON.parent.mkdir(parents=True, exist_ok=True)
    legacy = {
        "v": _pose_ver,
        "frame": int(frame),
        "bone_names": names,
        "matrices": [list(map(float, m)) for m in (matrices or [])],
    }
    tmp = _POSE_JSON.with_suffix(".tmp")
    tmp.write_text(json.dumps(legacy) + "\n", encoding="utf-8")
    tmp.replace(_POSE_JSON)

    payload = {"v": _pose_ver, "frame": int(frame), "bones": by_name}
    text = json.dumps(payload) + "\n"
    for dest in (_POSE_BY_NAME, _POSE_BY_NAME_WEB):
        dest.parent.mkdir(parents=True, exist_ok=True)
        tmp = dest.with_suffix(".tmp")
        tmp.write_text(text, encoding="utf-8")
        tmp.replace(dest)
    actor.meta["pose_by_name_path"] = str(_POSE_BY_NAME_WEB)
    actor.meta["pose_bone_entries"] = len(by_name)
    return actor


class _RootHandler(SimpleHTTPRequestHandler):
    """Serve repo root so /web/* and /samples/* share one origin."""

    def __init__(self, *args, directory: str = str(ROOT), **kwargs):
        super().__init__(*args, directory=directory, **kwargs)

    def log_message(self, fmt: str, *args: Any) -> None:  # quieter
        pass

    def do_GET(self):  # noqa: N802
        parsed = urlparse(self.path)
        if parsed.path in ("/api/sfx/flws", "/api/sfx/timeline"):
            try:
                from sfx_runtime import flws_payload

                body = json.dumps(
                    flws_payload(),
                    ensure_ascii=False,
                    separators=(",", ":"),
                ).encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Access-Control-Allow-Origin", "*")
                self.send_header("Cache-Control", "no-store")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            except Exception as exc:
                body = json.dumps(
                    {"ok": False, "error": str(exc)},
                    ensure_ascii=False,
                ).encode("utf-8")
                self.send_response(500)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Access-Control-Allow-Origin", "*")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            return
        if parsed.path == "/api/sfx/asset":
            try:
                from urllib.parse import parse_qs

                from pss_assets import load_asset_index

                logical = (parse_qs(parsed.query).get("path") or [""])[0]
                index = load_asset_index()
                local = index.get(logical.replace("/", "\\").lower()) or index.get(logical.lower())
                if not local or not Path(local).is_file():
                    self.send_response(404)
                    self.send_header("Access-Control-Allow-Origin", "*")
                    self.end_headers()
                    return
                data = Path(local).read_bytes()
                ext = Path(local).suffix.lower()
                mime = {
                    ".dds": "image/vnd-ms.dds",
                    ".tga": "image/x-tga",
                    ".jsondef": "application/json",
                    ".def": "application/json",
                    ".mesh": "application/octet-stream",
                }.get(ext, "application/octet-stream")
                self.send_response(200)
                self.send_header("Content-Type", mime)
                self.send_header("Access-Control-Allow-Origin", "*")
                self.send_header("Cache-Control", "public, max-age=3600")
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)
            except Exception as exc:
                body = json.dumps({"ok": False, "error": str(exc)}).encode("utf-8")
                self.send_response(500)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            return
        if parsed.path == "/api/sfx/mesh":
            try:
                from urllib.parse import parse_qs

                from mesh import load_mesh
                from pss_assets import load_asset_index

                logical = (parse_qs(parsed.query).get("path") or [""])[0]
                index = load_asset_index()
                local = index.get(logical.replace("/", "\\").lower()) or index.get(logical.lower())
                if not local or not Path(local).is_file():
                    self.send_response(404)
                    self.send_header("Access-Control-Allow-Origin", "*")
                    self.end_headers()
                    return
                parsed_mesh = load_mesh(local)
                payload = {
                    "path": logical,
                    "vertex_count": parsed_mesh.vertex_count,
                    "face_count": parsed_mesh.face_count,
                    "positions": parsed_mesh.positions.astype("float32").ravel().tolist(),
                    "faces": parsed_mesh.faces.astype("uint32").ravel().tolist(),
                }
                body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Access-Control-Allow-Origin", "*")
                self.send_header("Cache-Control", "public, max-age=3600")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            except Exception as exc:
                body = json.dumps({"ok": False, "error": str(exc)}).encode("utf-8")
                self.send_response(500)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            return
        if parsed.path.startswith("/api/f1_hualuo_textures"):
            tex = DEFAULT_TEX
            files = sorted(p.name for p in tex.iterdir() if p.is_file()) if tex.is_dir() else []
            body = json.dumps(files).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        # Map /fbx_viewport.html → web/fbx_viewport.html convenience
        if self.path == "/" or self.path.startswith(f"/{VIEWPORT_HTML}"):
            # rewrite to /web/fbx_viewport.html keeping query
            q = ""
            if "?" in self.path:
                q = "?" + self.path.split("?", 1)[1]
            self.path = f"/web/{VIEWPORT_HTML}{q}"
        return super().do_GET()


def ensure_server(actor: FbxActor, port: int = 0) -> int:
    """Start a background ThreadingHTTPServer on ROOT if not already running."""
    if actor._server is not None and actor._port:
        return actor._port

    class Handler(_RootHandler):
        pass

    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    actor._port = server.server_address[1]
    actor._server = server
    t = threading.Thread(target=server.serve_forever, daemon=True)
    t.start()
    return actor._port


def open_viewport(actor: FbxActor, closeup: bool = True) -> str:
    """Open the Three.js textured viewport in the default browser; return URL."""
    ensure_server(actor)
    url = actor.viewport_url(closeup=closeup)
    webbrowser.open(url)
    return url


def _capture_with_playwright(url: str, out_png: Path, timeout_ms: int = 60000) -> dict:
    """Headless Chromium screenshot after window.__FBX_READY__."""
    out_png = Path(out_png)
    out_png.parent.mkdir(parents=True, exist_ok=True)
    script = ROOT / "web" / "capture_fbx_proof.mjs"
    chrome = (
        shutil.which("google-chrome")
        or shutil.which("chromium")
        or shutil.which("chrome")
        or shutil.which("msedge")
    )
    if not chrome:
        for cand in (
            r"C:\Program Files\Google\Chrome\Application\chrome.exe",
            r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            "/usr/bin/google-chrome",
        ):
            if Path(cand).is_file():
                chrome = cand
                break
    if not chrome:
        chrome = "/usr/bin/google-chrome"
    cmd = [
        "node",
        str(script),
        "--url",
        url,
        "--out",
        str(out_png),
        "--chrome",
        chrome,
        "--timeout",
        str(timeout_ms),
    ]
    proc = subprocess.run(cmd, cwd=str(ROOT), capture_output=True, text=True, timeout=timeout_ms / 1000 + 30)
    if proc.returncode != 0:
        raise RuntimeError(
            f"capture failed rc={proc.returncode}\nstdout={proc.stdout}\nstderr={proc.stderr}"
        )
    meta = {}
    for line in proc.stdout.splitlines():
        if line.startswith("META_JSON="):
            meta = json.loads(line[len("META_JSON=") :])
    meta["png"] = str(out_png)
    meta["bytes"] = out_png.stat().st_size if out_png.is_file() else 0
    return meta


def paint_to(
    actor: FbxActor,
    surface: Any,
    *,
    closeup: bool = True,
    open_browser: bool = False,
) -> dict:
    """Paint textured 花萝.

    - If surface is a Path/str → write close-up PNG via Three viewport (proof path).
    - If surface has a matplotlib-like interface → write a status note only
      (do NOT draw blue LBS as parity); still ensure server + optional browser.
    - Returns capture/meta dict.
    """
    ensure_server(actor)
    url = actor.viewport_url(closeup=closeup)

    if open_browser:
        webbrowser.open(url)

    # Special surface: "webview" / "browser" → serve Three URL (not a PNG path).
    if isinstance(surface, str) and surface.lower() in ("webview", "browser"):
        ensure_server(actor)
        url = actor.viewport_url(closeup=closeup)
        if open_browser or surface.lower() == "browser":
            webbrowser.open(url)
        return {"url": url, "mode": "webview", "preset": actor.preset_name, "port": actor._port}

    # Path → PNG proof
    if isinstance(surface, (str, Path)):
        out = Path(surface)
        # Give server a moment
        time.sleep(0.15)
        meta = _capture_with_playwright(url, out)
        actor.meta.update(meta)
        return meta

    # Matplotlib Axes / Figure: refuse to fake textures with blue LBS
    note = (
        f"FBX textured viewport: {url}\n"
        f"(花萝 / hualuo_no_anim.fbx — not mesh.py RQ LBS)\n"
        f"Call paint_to(actor, Path('proof/...png')) for Three close-up."
    )
    try:
        # Best-effort status text on axes if present
        if hasattr(surface, "text"):
            surface.clear()
            surface.axis("off")
            surface.text(0.5, 0.5, note, ha="center", va="center", color="#7ec8ff", wrap=True)
        elif hasattr(surface, "text2D"):  # 3d axes
            surface.text2D(0.05, 0.5, note, transform=surface.transAxes, color="#7ec8ff")
    except Exception:
        pass
    return {"url": url, "note": note, "mode": "viewport-link"}


def stop_server(actor: FbxActor) -> None:
    if actor._server is not None:
        actor._server.shutdown()
        actor._server = None
        actor._port = 0


if __name__ == "__main__":
    import argparse

    ap = argparse.ArgumentParser(description="Load 花萝 FBX and dump Three close-up proof")
    ap.add_argument(
        "--fbx",
        type=Path,
        default=DEFAULT_FBX,
        help="Default: samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx",
    )
    ap.add_argument(
        "--out",
        type=Path,
        default=ROOT / "proof" / "compare" / "aniplayer_f1_hualuo_closeup.png",
    )
    ap.add_argument("--open", action="store_true", help="Also open browser")
    args = ap.parse_args()
    actor = load_fbx_actor(args.fbx)
    print(f"loaded fbx={actor.fbx_path} tex_files={actor.meta.get('texture_files')}")
    meta = paint_to(actor, args.out, open_browser=args.open)
    print(json.dumps(meta, indent=2, ensure_ascii=False))
