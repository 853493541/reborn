#!/usr/bin/env python3
"""MINA free player with the R4b companion-player chrome."""
from __future__ import annotations

import argparse
import re
import tkinter as tk
from pathlib import Path

import catalog
import tani

import matplotlib

matplotlib.use("TkAgg")
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure
from mpl_toolkits.mplot3d import Axes3D  # noqa: F401

from mina import load_mina, stick_edges
from min2 import load_min2_stick, sniff_magic
from transport import PlaybackClock  # Dev3: play/pause/seek/tick wall-clock API
from resolve_playable import ResolveError, resolve_playable_path  # Dev2 tani→ani
from pose_drive import load_clock_for_row, tick_pose  # Dev2 clock + FBX pose
from mesh import bone_name_map, load_mesh, normalize_influences, skin_positions

try:
    import actor_fbx
except ImportError:  # optional until FBX viewport lands
    actor_fbx = None
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
import numpy as np

SAMPLES_DEFAULT = Path(__file__).resolve().parent / "samples"
FPS_DEFAULT = 33.0  # clip header fps (MIN2 charge is 33); wall-clock playback uses clip.fps
# Body meshes for Play skinned viewport — pick by clip body chip (never M2 on F1).
MESH_BY_BODY = {
    "F1": SAMPLES_DEFAULT / "mesh" / "f1_1008_body_hd.mesh",
    "F2": SAMPLES_DEFAULT / "mesh" / "f1_1008_body_hd.mesh",  # interim until dedicated F2 body
    "M1": SAMPLES_DEFAULT / "mesh" / "m2_1018_body_hd.mesh",
    "M2": SAMPLES_DEFAULT / "mesh" / "m2_1018_body_hd.mesh",
}
DEFAULT_BODY_MESH = MESH_BY_BODY["M2"]

# Fluent dark tokens shared with the slim UI theme.
BG = "#202020"
PANEL = "#2C2C2C"
ACCENT = "#60CDFF"
VIEW = "#0C0C0C"
TEXT = "#FFFFFF"
TEXT_SECONDARY = "#C8C8C8"
TEXT_DISABLED = "#6D6D6D"
INPUT = "#1A1A1A"
STROKE = "#3F3F3F"
ACCENT_HOVER = "#6BC6F0"

BODIES = ("F1", "F2", "M1", "M2")
BODY_LABELS = {"F1": "小女孩", "F2": "大女孩", "M1": "小男孩", "M2": "大男孩"}
# Default character label per body when catalog has no character list (R4b chrome).
CHAR_BY_BODY = {
    "F1": "花萝",
    "F2": "大女孩",
    "M1": "小男孩",
    "M2": "大男孩",
}
TAB_TITLES = {"anim": "动作", "tani": "脸部(Tani)", "serial": "Serial"}
ORANGE = "#E8A05C"
ACCENT_BLUE = "#5B9BD5"
GOOD = "#8FCB86"


def _catalog_ui_path(samples_dir: Path) -> Path:
    return samples_dir / "player" / "catalog" / "ui_lists.json"


def _load_ui_lists(samples_dir: Path) -> dict | None:
    p = _catalog_ui_path(samples_dir)
    if not p.is_file():
        return None
    import json
    return json.loads(p.read_text(encoding="utf-8"))


def _playable_catalog_path(samples_dir: Path) -> Path:
    return samples_dir / "player" / "catalog" / "playable_f1_hualuo.json"


def _load_playable_catalog(samples_dir: Path) -> dict | None:
    """Dev1 resolve feed: default_clips + fenglaiwushan rows with playable_path."""
    p = _playable_catalog_path(samples_dir)
    if not p.is_file():
        return None
    import json
    return json.loads(p.read_text(encoding="utf-8"))


def _merge_playable(entry: dict, playable: dict | None) -> dict:
    """Overlay playable_path onto a ui_lists row (by id or local_path)."""
    if not playable or not entry:
        return entry
    eid = entry.get("id")
    local = (entry.get("local_path") or "").replace("\\", "/")
    rows = list(playable.get("fenglaiwushan") or []) + list(playable.get("default_clips") or [])
    for r in rows:
        if eid is not None and r.get("id") == eid:
            out = dict(entry)
            for k in ("playable_path", "playable"):
                if r.get(k):
                    out[k] = r[k]
            return out
        rlocal = (r.get("local_path") or "").replace("\\", "/")
        if local and rlocal and Path(local).name == Path(rlocal).name:
            out = dict(entry)
            for k in ("playable_path", "playable"):
                if r.get(k):
                    out[k] = r[k]
            return out
    fb = playable.get("first_bar") or {}
    if fb.get("playable_path") and ("蓄力" in local or "蓄力" in str(entry.get("anim_file") or "")):
        out = dict(entry)
        out["playable_path"] = fb["playable_path"]
        return out
    return entry


PAGE_SIZE = 100


def _entry_filename(entry: dict) -> str:
    fn = entry.get("filename") or ""
    if not fn:
        raw = str(entry.get("anim_file") or entry.get("local_path") or "")
        fn = raw.replace("\\", "/").replace("\\", "/").split("/")[-1]
    return fn or str(entry.get("id") or "?")


_BODY_RE = re.compile(r"^(f1|f2|m1|m2)", re.IGNORECASE)


def _body_for_path(path: Path) -> str | None:
    """Match f1_xxx and compact f1s07cj… / m2s07cj… MovieEditor names."""
    match = _BODY_RE.match(path.name)
    return match.group(1).upper() if match else None


def _character_for_path(path: Path) -> str:
    match = _BODY_RE.match(path.name)
    if not match:
        return "Unknown"
    rest = path.name[match.end() :]
    if rest.startswith("_"):
        rest = rest[1:]
    for suffix in (".mesh.ani", ".ani"):
        if rest.endswith(suffix):
            rest = rest[: -len(suffix)]
            break
    # First underscore segment, or whole rest for compact names.
    return (rest.split("_", 1)[0] or "Unknown")


def _clip_name(path: Path) -> str:
    """Use a friendly fallback without pretending a basename is an AniTable name."""
    name = path.name
    for suffix in (".mesh.ani", ".ani"):
        if name.endswith(suffix):
            name = name[: -len(suffix)]
            break
    return name




def _resolve_playable_ani(
    path: Path | None,
    samples_dir: Path,
    entry: dict | None = None,
) -> Path | None:
    """Never feed .tani into MIN2 — resolve a local skeletal .ani.

    Order: row local_path if .ani → parse_tani(local/.tani selection) →
    resolve_local_ani under moves/ → skill15 / 蓄力 basename heuristics.
    """
    entry = entry or {}
    moves = samples_dir / "player" / "moves"
    search = [
        moves / "fenglaiwushan",
        moves,
        samples_dir / "player",
        samples_dir,
    ]

    def as_ani(p: Path | None) -> Path | None:
        if p is not None and p.is_file() and p.suffix.lower() == ".ani":
            return p
        return None

    # 0) Dev1 catalog playable_path (preferred — never feed .tani into MIN2)
    playable = (entry.get("playable_path") or entry.get("playable") or "").replace("\\", "/")
    if playable:
        for cand in (
            samples_dir / "player" / playable,
            moves / Path(playable).name,
            moves / "fenglaiwushan" / Path(playable).name,
        ):
            hit = as_ani(cand)
            if hit is not None:
                return hit

    def from_tani(tp: Path) -> Path | None:
        try:
            info = tani.parse_tani(tp)
        except Exception:
            return None
        return tani.resolve_local_ani(info, search)

    # 1) catalog local_path
    local = (entry.get("local_path") or "").replace("\\", "/")
    if local:
        for cand in (
            samples_dir / "player" / local,
            moves / Path(local).name,
            moves / "fenglaiwushan" / Path(local).name,
        ):
            hit = as_ani(cand)
            if hit is not None:
                return hit
        for cand in (
            samples_dir / "player" / local,
            moves / Path(local).name,
            moves / "fenglaiwushan" / Path(local).name,
        ):
            if cand.is_file() and cand.suffix.lower() == ".tani":
                hit = from_tani(cand)
                if hit is not None:
                    return hit

    # 2) selected path already .ani
    hit = as_ani(path)
    if hit is not None:
        return hit

    # 3) selected .tani
    if path is not None and path.is_file() and path.suffix.lower() == ".tani":
        hit = from_tani(path)
        if hit is not None:
            return hit

    # 4) heuristics: prefer 蓄力 / 重剑技能15.ani for this body
    raw = entry.get("filename") or entry.get("anim_file") or (path.name if path else "")
    name = Path(str(raw).replace("\\", "/")).name
    prefer_charge = "蓄力" in name or "charge" in name.lower()
    body = (entry.get("body") or "F1").lower()
    cands: list[Path] = []
    for d in search:
        if not d.is_dir():
            continue
        for p in d.rglob("*.ani"):
            nl = p.name.lower()
            if body not in nl:
                continue
            if "s07cj" in nl and "15" in nl:
                cands.append(p)
    if not cands:
        return None

    def score(p: Path) -> tuple:
        n = p.name
        nl = n.lower()
        s = 0
        if prefer_charge and "蓄力" in n:
            s += 100
        if (not prefer_charge) and "蓄力" not in n and nl.endswith("15.ani"):
            s += 90
        if "奇穴" in n:
            s += 5
        return (-s, len(n))

    return sorted(cands, key=score)[0]

def _is_flws_clip(path: Path) -> bool:
    """Default catalog: only 风来吴山 / 重剑技能15 related .ani."""
    s = str(path).lower()
    name = path.name.lower()
    if 'fenglaiwushan' in s.replace('\\', '/'):
        return path.suffix.lower() == '.ani'
    keys = ('风来吴山', '重剑技能15', 's07cj重剑技能15', 's07cj', 'flws')
    # keep ascii-safe keys too
    keys_ascii = ('fenglai', 'wushan', 's07cj')
    if any(k.lower() in name for k in ('风来吴山', '重剑技能15')):
        return True
    if 's07cj' in name and path.suffix.lower() == '.ani':
        return True
    if any(k in s for k in keys_ascii) and path.suffix.lower() == '.ani':
        return True
    return False


def _catalog_dir(samples_dir: Path) -> Path:
    return samples_dir / "player" / "catalog"


def _resolve_via_mapviewer_catalog(samples_dir: Path, body: str = "f1") -> Path | None:
    """Same path the map-viewer PSS/player-anim page uses: official table → .tani → .ani."""
    cat = _catalog_dir(samples_dir)
    if not cat.is_dir():
        return None
    entry = catalog.preferred_fenglaiwushan(cat, body=body)
    moves = samples_dir / "player" / "moves"
    search = [moves / "fenglaiwushan", moves]
    search = [d for d in search if d.is_dir()]

    def pick_tani() -> Path | None:
        # Prefer HD 风来吴山 tani for this body, ignoring encoding mismatches in table path
        body_l = body.lower()
        cands: list[Path] = []
        for d in search:
            for pth in d.rglob("*.tani"):
                n = pth.name
                nl = n.lower()
                if body_l not in nl:
                    continue
                if "s07cj" not in nl or "15" not in nl:
                    continue
                if "风来吴山" not in n and "fenglai" not in nl:
                    # still allow if table basename matches exactly
                    continue
                cands.append(pth)
        if not cands and entry is not None:
            want = Path(entry.anim_file.replace("\\", "/")).name.lower()
            for d in search:
                for pth in d.rglob("*.tani"):
                    if pth.name.lower() == want:
                        cands.append(pth)
        if not cands:
            return None
        def score(pth: Path) -> tuple:
            n = pth.name.lower()
            return (0 if "hd" in n else 1, 0 if "奇穴" not in pth.name else 1, len(n))
        return sorted(cands, key=score)[0]

    tani_path = pick_tani()
    if tani_path is not None:
        info = tani.parse_tani(tani_path)
        resolved = tani.resolve_local_ani(info, search)
        if resolved is not None:
            return resolved

    # direct .ani fallback for body skill15 (non-奇穴)
    body_l = body.lower()
    ani_cands = []
    for d in search:
        for pth in d.rglob("*.ani"):
            nl = pth.name.lower()
            if body_l in nl and "s07cj" in nl and "15" in nl and "奇穴" not in pth.name:
                ani_cands.append(pth)
    if ani_cands:
        return sorted(ani_cands, key=lambda pth: (0 if pth.stem.lower().endswith("15") else 1, pth.stat().st_size))[0]
    return None


def _flws_default_path(paths: list[Path], samples_dir: Path | None = None) -> Path | None:
    """Prefer map-viewer catalog resolution; fall back to F1 蓄力 / body clip heuristics."""
    if samples_dir is not None:
        via = _resolve_via_mapviewer_catalog(samples_dir, body="f1")
        if via is not None:
            return via
    if not paths:
        return None
    scored = []
    for path in paths:
        n = path.name.lower()
        score = 0
        if "风来吴山" in path.name:
            score += 200
        if path.suffix.lower() == ".ani" and "s07cj" in n and "15" in n and "奇穴" not in path.name:
            score += 150
        if "蓄力" in path.name or "charge" in n:
            score += 100
        if n.startswith("f1") or "/f1" in str(path).lower().replace("\\", "/"):
            score += 50
        if "奇穴" in path.name:
            score -= 20
        if path.suffix.lower() == ".ani":
            score += 5
        scored.append((score, path))
    scored.sort(key=lambda x: (-x[0], str(x[1]).lower()))
    return scored[0][1]




def _loco_rank(path: Path) -> tuple:
    """Lower = higher in 动作 list. 行走/走路 first, then 跳跃 (小跳b preferred)."""
    n = path.name
    nl = n.lower()
    if "行走" in n or "走路" in n:
        return (0, 0 if "f1b02yd行走" in n else 1, nl)
    if "小跳b" in n or ("跳跃" in n and "小跳b" in n):
        return (1, 0, nl)
    if "小跳" in n or "跳跃" in n or "二段跳" in n:
        return (1, 1 if "小跳b" not in n else 0, nl)
    if "风来吴山" in n or "蓄力" in n or "s07cj" in nl:
        return (9, 0, nl)  # demote charge
    return (5, 0, nl)


def _loco_default_path(paths: list[Path], samples_dir: Path | None = None) -> Path | None:
    """Prefer 花萝 行走, then 小跳b; never charge as product default."""
    preferred = []
    if samples_dir is not None:
        base = samples_dir / "player" / "moves" / "from_mapviewer" / "f1" / "动作"
        preferred = [
            base / "f1b02yd行走.ani",
            base / "f1b02yd小跳b.ani",
            base / "f1b04ty行走.ani",
        ]
        for p in preferred:
            if p.is_file():
                return p
    if not paths:
        return None
    ranked = sorted(paths, key=_loco_rank)
    return ranked[0]

class AniPlayer(tk.Tk):
    def __init__(self, samples_dir: Path, fps: float = FPS_DEFAULT, all_clips: bool = False, character_mode: str = "fbx"):
        super().__init__()
        self.title("JX3 Ani Player")
        self.configure(bg=BG)
        self.geometry("1100x720")
        self.minsize(900, 600)
        self.samples_dir = samples_dir
        self.fps = fps
        self._all_clips = all_clips
        self.clip = None
        self.loop = tk.BooleanVar(value=True)
        # Shared wall-clock transport (Dev3 API lives in transport.PlaybackClock).
        self.clock = PlaybackClock(fps_fallback=fps, loop=True)
        self.body_var = tk.StringVar(value="F1")
        self._after_id = None
        self.edges = []
        self._paths: list[Path] = []
        self._visible_paths: list[Path] = []
        self._character_ids: list[str] = []
        self.skinned = tk.BooleanVar(value=False)  # Mesh RQ LBS on matplotlib (native desktop path)
        # Product default: fbx = textured 花萝 character (map-viewer parity). mesh/stick = debug stickers only.
        self.character_mode = (character_mode or "fbx").lower()
        if self.character_mode == "stick":
            self.character_mode = "mesh"  # stick alias → debug matplotlib host
        self._fbx_actor = None
        self._fbx_url = None
        self._fbx_frame = None
        self._fbx_mixer_clip = None  # Path|None — clipFbx when locomotion Mixer mode
        self._fbx_mixer_url = None
        self._fbx_last_nav = None
        self._mesh_fallback = tk.BooleanVar(value=False)  # alias for UI
        self._mesh = None
        self._mesh_map: dict = {}
        self._stick_rest = None
        self._mesh_label = ""
        self.catalog_tab = tk.StringVar(value="anim")  # anim | tani | serial
        self.search_var = tk.StringVar(value="")
        self._body_counts = {b: 0 for b in BODIES}
        self._tab_counts = {"anim": 0, "tani": 0, "serial": 0}
        self._list_rows: list[dict] = []  # {path, label, tag, anim_id, kind, entry}
        self._show_grid = tk.BooleanVar(value=True)
        self._ui_lists = _load_ui_lists(samples_dir)
        self._playable = _load_playable_catalog(samples_dir)
        self._page = 0
        self._filtered_entries: list[dict] = []

        self._build()
        self._load_clip_list()

    # ---------- R4b chrome ----------
    def _flat_button(self, parent, text, command, *, primary=False, width=None):
        bg = ACCENT if primary else PANEL
        fg = "#000000" if primary else TEXT
        button = tk.Button(
            parent,
            text=text,
            command=command,
            font=("Sans", 11),
            bg=bg,
            fg=fg,
            activebackground=ACCENT_HOVER if primary else "#353535",
            activeforeground="#000000" if primary else TEXT,
            disabledforeground=TEXT_DISABLED,
            relief="flat",
            borderwidth=0,
            highlightthickness=0 if primary else 1,
            highlightbackground=STROKE,
            highlightcolor=ACCENT,
            padx=13,
            pady=6,
            cursor="hand2",
        )
        if width is not None:
            button.configure(width=width)
        return button

    def _build(self):
        """R4b companion chrome: Body → Character → 动作 → Play (desktop Tk)."""
        root = tk.Frame(self, bg=BG)
        root.pack(fill=tk.BOTH, expand=True)

        # ---- global header ----
        header = tk.Frame(root, bg="#060A0F", height=58)
        header.pack(fill=tk.X)
        header.pack_propagate(False)
        title_col = tk.Frame(header, bg="#060A0F")
        title_col.pack(side=tk.LEFT, padx=14, pady=6)
        tk.Label(
            title_col,
            text="JX3 Ani Player · free play companion",
            bg="#060A0F",
            fg="#D8E5F5",
            font=("Sans", 12, "bold"),
        ).pack(anchor="w")
        tk.Label(
            title_col,
            text="动作 = clip  ·  Body → Character → 动作 → Play",
            bg="#060A0F",
            fg=TEXT_SECONDARY,
            font=("Sans", 9),
        ).pack(anchor="w")
        # Current mode label only — no fake Movie Player / PSS nav.
        mode = tk.Label(
            header,
            text="Animation Player",
            bg="#141E2A",
            fg="#D5DEEA",
            font=("Sans", 9),
            padx=10,
            pady=4,
            highlightthickness=1,
            highlightbackground=STROKE,
        )
        mode.pack(side=tk.RIGHT, padx=12)

        body = tk.Frame(root, bg=BG)
        body.pack(fill=tk.BOTH, expand=True, padx=12, pady=(10, 0))

        self._build_left_rail(body)
        self._build_right_stage(body)

        # ---- status bar ----
        status = tk.Frame(root, bg="#060A0F", height=28)
        status.pack(fill=tk.X, side=tk.BOTTOM)
        status.pack_propagate(False)
        self.status_conn = tk.Label(status, text="● Connected", bg="#060A0F", fg=GOOD, font=("Sans", 9))
        self.status_conn.pack(side=tk.LEFT, padx=(12, 10))
        self.status_body = tk.Label(status, text="Body: F1", bg="#060A0F", fg=TEXT_SECONDARY, font=("Sans", 9))
        self.status_body.pack(side=tk.LEFT, padx=8)
        self.status_count = tk.Label(status, text="0 clips", bg="#060A0F", fg=TEXT_SECONDARY, font=("Sans", 9))
        self.status_count.pack(side=tk.LEFT, padx=8)
        self.status_renderer = tk.Label(status, text="Renderer: idle", bg="#060A0F", fg=TEXT_SECONDARY, font=("Sans", 9))
        self.status_renderer.pack(side=tk.LEFT, padx=8)

    def _section_label(self, parent, text, side=tk.TOP):
        tk.Label(
            parent,
            text=text,
            bg=PANEL,
            fg=TEXT_SECONDARY,
            font=("Sans", 10),
        ).pack(side=side, anchor="w")

    def _build_left_rail(self, parent):
        rail = tk.Frame(parent, bg=PANEL, highlightthickness=1, highlightbackground=STROKE, width=360)
        rail.pack(side=tk.LEFT, fill=tk.Y, padx=(0, 10))
        rail.pack_propagate(False)
        content = tk.Frame(rail, bg=PANEL)
        content.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        # Body 体型
        self._section_label(content, "Body 体型")
        chips = tk.Frame(content, bg=PANEL)
        chips.pack(fill=tk.X, pady=(4, 10))
        self.body_buttons: dict[str, tk.Button] = {}
        for body in BODIES:
            label = f"{BODY_LABELS[body]} ({body})"
            button = tk.Button(
                chips,
                text=label + "\n0",
                command=lambda b=body: self._select_body(b),
                font=("Sans", 8),
                padx=2,
                pady=4,
                bg=INPUT,
                fg=TEXT,
                activebackground=ACCENT_HOVER,
                activeforeground="#000000",
                relief="flat",
                borderwidth=0,
                highlightthickness=2,
                highlightbackground=STROKE,
                cursor="hand2",
                justify="center",
            )
            button.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=2)
            self.body_buttons[body] = button

        # Character 角色
        self._section_label(content, "Character 角色")
        self.character_label = tk.Label(
            content,
            text="No character selected",
            bg=PANEL,
            fg=TEXT_SECONDARY,
            anchor="w",
            font=("Sans", 10),
        )
        self.character_label.pack(fill=tk.X, pady=(4, 4))
        self.character_list = tk.Listbox(
            content,
            height=3,
            bg=INPUT,
            fg=TEXT,
            selectbackground=ACCENT_BLUE,
            selectforeground="#FFFFFF",
            highlightthickness=1,
            highlightbackground=STROKE,
            relief="flat",
            borderwidth=0,
            activestyle="none",
            font=("Sans", 9),
            exportselection=False,
        )
        self.character_list.pack(fill=tk.X, pady=(0, 10))
        self.character_list.bind("<<ListboxSelect>>", self._on_character_select)

        # 动作 Clip catalog
        self._section_label(content, "动作 Clip")
        tabs = tk.Frame(content, bg=PANEL)
        tabs.pack(fill=tk.X, pady=(4, 6))
        self.tab_buttons: dict[str, tk.Button] = {}
        for key, title in TAB_TITLES.items():
            b = tk.Button(
                tabs,
                text=f"{title} 0",
                command=lambda k=key: self._select_catalog_tab(k),
                font=("Sans", 9),
                padx=6,
                pady=4,
                bg=INPUT,
                fg=TEXT,
                relief="flat",
                borderwidth=0,
                highlightthickness=1,
                highlightbackground=STROKE,
                cursor="hand2",
            )
            b.pack(side=tk.LEFT, expand=True, fill=tk.X, padx=1)
            self.tab_buttons[key] = b

        # search
        search_row = tk.Frame(content, bg=PANEL)
        search_row.pack(fill=tk.X, pady=(2, 6))
        self.search_entry = tk.Entry(
            search_row,
            textvariable=self.search_var,
            bg=INPUT,
            fg=TEXT,
            insertbackground=TEXT,
            relief="flat",
            font=("Sans", 10),
            highlightthickness=1,
            highlightbackground=STROKE,
        )
        self.search_entry.pack(fill=tk.X, ipady=4)
        self.search_entry.insert(0, "")
        self.search_var.trace_add("write", lambda *_: self._refresh_catalog())

        self.clip_hint = tk.Label(
            content,
            text="Ani / Tani resource · basename fallback",
            bg=PANEL,
            fg=TEXT_SECONDARY,
            anchor="w",
            font=("Sans", 8),
        )
        self.clip_hint.pack(fill=tk.X)
        list_frame = tk.Frame(content, bg=PANEL)
        list_frame.pack(fill=tk.BOTH, expand=True, pady=(4, 0))
        self.clip_list = tk.Listbox(
            list_frame,
            bg=INPUT,
            fg=TEXT,
            selectbackground=ACCENT_BLUE,
            selectforeground="#FFFFFF",
            highlightthickness=1,
            highlightbackground=STROKE,
            relief="flat",
            borderwidth=0,
            activestyle="none",
            font=("Sans", 9),
            exportselection=False,
        )
        scroll = tk.Scrollbar(list_frame, command=self.clip_list.yview)
        self.clip_list.configure(yscrollcommand=scroll.set)
        self.clip_list.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.clip_list.bind("<<ListboxSelect>>", lambda _e: self._on_select_clip())

        # hidden bone widgets kept for API compatibility
        self.bone_list = tk.Listbox(content)
        self.bone_count_label = tk.Label(content, text="", bg=PANEL, fg=TEXT_SECONDARY)
        self._paint_body_buttons()
        self._paint_tab_buttons()
        self._refresh_character_rail()

    def _build_right_stage(self, parent):
        stage = tk.Frame(parent, bg=BG)
        stage.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        viewport_card = tk.Frame(stage, bg=VIEW, highlightthickness=1, highlightbackground=STROKE)
        viewport_card.pack(fill=tk.BOTH, expand=True)

        # viewport toolbar
        vp_bar = tk.Frame(viewport_card, bg="#0A0E12")
        vp_bar.pack(fill=tk.X)
        self.vp_label = tk.Label(vp_bar, text="No effect loaded —", bg="#0A0E12", fg=ORANGE, font=("Sans", 9))
        self.vp_label.pack(side=tk.LEFT, padx=8, pady=4)
        for text_, cmd in (
            ("风来吴山·红", self.play_flws_red),
            ("Reset", self.stop),
            ("Replay", self.play),
            ("Grid", self._toggle_grid),
            ("Open 3D", self._open_fbx_external),
            ("Bones", lambda: None),
            ("Debug", lambda: None),
        ):
            b = tk.Button(
                vp_bar,
                text=text_,
                command=cmd,
                font=("Sans", 8),
                padx=8,
                pady=2,
                bg="#141E2A",
                fg=TEXT,
                relief="flat",
                highlightthickness=1,
                highlightbackground=STROKE,
                cursor="hand2",
            )
            b.pack(side=tk.RIGHT, padx=3, pady=3)

        self._viewport_card = viewport_card
        self.fig = Figure(figsize=(6, 3.6), dpi=100, facecolor=VIEW)
        self.ax = self.fig.add_subplot(111, projection="3d", facecolor=VIEW)
        self.canvas = FigureCanvasTkAgg(self.fig, master=viewport_card)
        self.canvas.get_tk_widget().configure(bg=VIEW, highlightthickness=0, borderwidth=0)
        # Product host: 花萝 FBX character view (Andy RESET — sticks are FAIL).
        # Debug: --character-mode mesh|stick for matplotlib stickers only.
        self._fbx_host = tk.Frame(viewport_card, bg=VIEW, highlightthickness=0)
        if self.character_mode == "fbx":
            self._fbx_host.pack(fill=tk.BOTH, expand=True, padx=4, pady=4)
            self._init_fbx_viewport()
        else:
            self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=4, pady=4)
            self.vp_label.configure(text="DEBUG stick/mesh viewport · not product default")

        # timeline bar (map-viewer style)
        transport = tk.Frame(viewport_card, bg="#060A0F", highlightthickness=1, highlightbackground=STROKE)
        transport.pack(fill=tk.X, padx=6, pady=(0, 6))
        controls = tk.Frame(transport, bg="#060A0F")
        controls.pack(fill=tk.X, padx=8, pady=6)
        self.play_button = self._flat_button(controls, "▶ Play", self.play, primary=True)
        self.play_button.pack(side=tk.LEFT, padx=(0, 4))
        self._flat_button(controls, "⏹ Stop", self.stop).pack(side=tk.LEFT, padx=(0, 4))
        self._flat_button(controls, "⏸ Pause", self.pause).pack(side=tk.LEFT, padx=(0, 4))
        self.loop_button = tk.Checkbutton(
            controls,
            text="☑ Loop",
            variable=self.loop,
            command=self._paint_loop_button,
            indicatoron=False,
            font=("Sans", 10),
            padx=8,
            pady=2,
            relief="flat",
            borderwidth=0,
            highlightthickness=1,
            highlightbackground=ACCENT,
            bg="#060A0F",
            fg=ACCENT,
            activebackground="#353535",
            activeforeground=TEXT,
            selectcolor="#060A0F",
            cursor="hand2",
        )
        self.loop_button.pack(side=tk.LEFT, padx=(0, 6))
        # Mesh RQ LBS — native skinned path on matplotlib (primary desktop character view).
        self.skinned = self._mesh_fallback if hasattr(self, "_mesh_fallback") else self.skinned
        self.skinned_button = tk.Checkbutton(
            controls,
            text="Mesh RQ",
            variable=self.skinned,
            command=self._paint_skinned_button,
            indicatoron=False,
            font=("Sans", 9),
            padx=8,
            pady=2,
            relief="flat",
            highlightthickness=1,
            highlightbackground=STROKE,
            bg="#060A0F",
            fg=TEXT,
            selectcolor="#060A0F",
            cursor="hand2",
        )
        self.skinned_button.pack(side=tk.LEFT, padx=(0, 8))
        self._paint_loop_button()
        self._paint_skinned_button()

        scrub_row = tk.Frame(transport, bg="#060A0F")
        scrub_row.pack(fill=tk.X, padx=8, pady=(0, 6))
        self.scrub = tk.Scale(
            scrub_row,
            from_=0,
            to=0,
            orient=tk.HORIZONTAL,
            showvalue=False,
            bg="#060A0F",
            fg=TEXT,
            troughcolor=INPUT,
            activebackground=ACCENT,
            highlightthickness=0,
            borderwidth=0,
            sliderlength=14,
            command=self._on_scrub,
        )
        self.scrub.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=(0, 8))
        self.time_lbl = tk.Label(scrub_row, text="0.00 / 0.00s", bg="#060A0F", fg=TEXT_SECONDARY, font=("Sans", 9))
        self.time_lbl.pack(side=tk.RIGHT)

        # detail / info panel
        detail = tk.Frame(stage, bg=PANEL, highlightthickness=1, highlightbackground=STROKE)
        detail.pack(fill=tk.X, pady=(8, 0))
        self.meta = tk.Label(
            detail,
            text="Choose a 动作 (clip) to play.",
            bg=PANEL,
            fg=TEXT,
            anchor="w",
            font=("Sans", 10, "bold"),
            padx=10,
            pady=6,
        )
        self.meta.pack(fill=tk.X)
        props = tk.Frame(detail, bg=PANEL)
        props.pack(fill=tk.X, padx=10, pady=(0, 8))
        self.detail_anim = tk.Label(props, text="Clip ID: —", bg=PANEL, fg=TEXT_SECONDARY, font=("Sans", 9), anchor="w")
        self.detail_anim.pack(fill=tk.X)
        self.detail_kind = tk.Label(props, text="Kind / Sheath: —", bg=PANEL, fg=TEXT_SECONDARY, font=("Sans", 9), anchor="w")
        self.detail_kind.pack(fill=tk.X)
        self.detail_loop = tk.Label(props, text="Loop: —", bg=PANEL, fg=TEXT_SECONDARY, font=("Sans", 9), anchor="w")
        self.detail_loop.pack(fill=tk.X)
        self.detail_speed = tk.Label(props, text="Speed / Ratio: —", bg=PANEL, fg=TEXT_SECONDARY, font=("Sans", 9), anchor="w")
        self.detail_speed.pack(fill=tk.X)
        self.clip_hint_right = tk.Label(props, text="", bg=PANEL, fg=TEXT_SECONDARY, font=("Sans", 8), anchor="w")
        self.clip_hint_right.pack(fill=tk.X)

    def _select_catalog_tab(self, key: str):
        self.catalog_tab.set(key)
        self._paint_tab_buttons()
        self._refresh_catalog(select_first=True)

    def _paint_tab_buttons(self):
        cur = self.catalog_tab.get()
        for key, btn in self.tab_buttons.items():
            active = key == cur
            count = self._tab_counts.get(key, 0)
            btn.configure(
                text=f"{TAB_TITLES[key]} {count}",
                bg=ACCENT_BLUE if active else INPUT,
                fg="#FFFFFF" if active else TEXT,
                highlightbackground=ACCENT_BLUE if active else STROKE,
            )

    def _toggle_grid(self):
        self._show_grid.set(not self._show_grid.get())
        self._draw_frame()

    def _paint_loop_button(self):
        selected = self.loop.get()
        self.clock.loop = bool(selected)
        self.loop_button.configure(
            bg=ACCENT if selected else PANEL,
            fg="#000000" if selected else TEXT,
            highlightbackground=ACCENT if selected else STROKE,
            activeforeground="#000000" if selected else TEXT,
        )


    def _init_fbx_viewport(self) -> None:
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
            return
        try:
            from tkinterweb import HtmlFrame
            frame = HtmlFrame(self._fbx_host, messages_enabled=False)
            frame.pack(fill=tk.BOTH, expand=True)
            frame.load_url(self._fbx_url)
            self._fbx_frame = frame
            self.vp_label.configure(text=f"花萝 FBX · external browser (see Open 3D)")
            # TkinterWeb cannot run three.js WebGL — drive an external browser
            # tab instead so the user actually sees the character.
            self._open_fbx_external(quiet=True)
        except Exception as exc:
            # No embeddable webview — open external + keep status in matplotlib.
            import webbrowser
            webbrowser.open(self._fbx_url)
            self.ax.clear()
            self.ax.axis("off")
            self.ax.text2D(
                0.5, 0.55,
                f"花萝 FBX viewport\n{self._fbx_url}\n(embed unavailable: {exc})",
                transform=self.ax.transAxes, ha="center", va="center", color="#7ec8ff", fontsize=9,
            )
            self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=4, pady=4)
            self.canvas.draw_idle()
            self.vp_label.configure(text=f"花萝 FBX (external) · {self._fbx_url}")



    def _open_fbx_external(self, quiet: bool = False) -> None:
        """Open (or re-open) the Three.js viewport in the system browser.

        The Tk embed cannot run WebGL, so the browser tab is the real stage.
        Clip clicks publish ``runtime/nav.json``; that tab follows via pollNav.
        """
        url = getattr(self, "_fbx_url", None)
        if not url:
            return
        try:
            import webbrowser

            webbrowser.open(url, new=2)
        except Exception:
            if not quiet and hasattr(self, "vp_label"):
                self.vp_label.configure(text=f"Open in browser: {url}")
            return
        if hasattr(self, "vp_label"):
            self.vp_label.configure(text="花萝 FBX · external browser tab · Open 3D")

    def play_flws_red(self) -> None:
        """One-click 风来吴山红色: red body clip + red PSS SFX, full timeline."""
        from fbx_actor import CLIP_JSON_FLWS_CAST

        if getattr(self, "_fbx_actor", None) is None:
            return
        if not CLIP_JSON_FLWS_CAST.is_file():
            if hasattr(self, "meta"):
                self.meta.configure(text=f"Missing {CLIP_JSON_FLWS_CAST.name}")
            return
        self._fbx_mixer_clip = str(CLIP_JSON_FLWS_CAST)
        self._clip_path = CLIP_JSON_FLWS_CAST
        self._nav_fbx_viewport(
            clip_json=CLIP_JSON_FLWS_CAST,
            t=None,
            playing=True,
            label="风来吴山·红色",
        )
        if hasattr(self, "vp_label"):
            self.vp_label.configure(text="花萝 FBX · 风来吴山·红色 (body + SFX)")
        if hasattr(self, "clip_hint"):
            self.clip_hint.configure(text="风来吴山·红色  ·  body + SFX")
        self._open_fbx_external(quiet=True)

    def _resolve_mixer_clip_json(self, path, row=None):
        """风来吴山·蓄力/释放 → MIN2 AnimationClip JSON; else None."""
        try:
            from fbx_actor import clip_json_for_flws
        except ImportError:
            return None
        if isinstance(row, dict):
            for key in ("clip_json", "clipJson", "animation_clip_json"):
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
                row.get("kind"),
            ):
                if blob:
                    hit = clip_json_for_flws(blob)
                    if hit is not None:
                        return hit
        if path is not None:
            return clip_json_for_flws(path)
        return None

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

    def _nav_fbx_viewport(self, *, clip_fbx=None, clipFbx=None, clip_json=None, clipJson=None, t=None, playing: bool = False, label: str = "") -> None:
        """Reload viewport URL for skin + clipFbx or MIN2 clip-JSON Mixer.

        Also publishes ``runtime/nav.json`` so an external browser tab follows
        the Tk selection (the embedded webview cannot run WebGL).
        """
        clip_fbx = clip_fbx if clip_fbx is not None else clipFbx
        clip_json = clip_json if clip_json is not None else clipJson
        actor = getattr(self, "_fbx_actor", None)
        if actor is None:
            return
        try:
            try:
                from fbx_actor import web_viewport_url as _url_fn
            except ImportError:
                from fbx_actor import web_viewport_url as _url_fn
            url = _url_fn(actor, closeup=True, clip_fbx=clip_fbx, clip_json=clip_json, t=t, playing=playing)
        except Exception:
            return
        self._fbx_url = url
        self._fbx_mixer_url = url
        self._fbx_last_nav = url
        self._fbx_mixer_clip = clip_fbx or clip_json
        frame = getattr(self, "_fbx_frame", None)
        try:
            if frame is not None and hasattr(frame, "load_url"):
                frame.load_url(url)
        except Exception:
            pass
        try:
            from fbx_actor import publish_nav

            publish_nav(url)
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

    def _sync_fbx_pose(self, sample) -> None:
        """MIN2 locals → apply_pose only when NOT in locomotion Mixer mode."""
        if self._fbx_actor is None or sample is None:
            return
        # Locomotion uses AnimationMixer + clipFbx — never push MIN2 matrices.
        if getattr(self, "_fbx_mixer_clip", None) is not None:
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
            pass

    def _paint_skinned_button(self):
        selected = self.skinned.get()
        self.skinned_button.configure(
            text="Mesh RQ" + (" ON" if selected else ""),
            bg="#2A3A4A" if selected else "#060A0F",
            fg=ACCENT if selected else TEXT,
            highlightbackground=ACCENT if selected else STROKE,
        )
        # Mesh RQ ON → show matplotlib; OFF → keep FBX Three host when available.
        if self.character_mode == "fbx" and self._fbx_host is not None:
            if selected:
                self._fbx_host.pack_forget()
                if self.canvas.get_tk_widget().winfo_manager() == "":
                    self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=4, pady=4)
            else:
                self.canvas.get_tk_widget().pack_forget()
                if self._fbx_host.winfo_manager() == "":
                    self._fbx_host.pack(fill=tk.BOTH, expand=True, padx=4, pady=4)
        if self.clip is not None:
            self._draw_frame()


    def _paint_body_buttons(self):
        selected = self.body_var.get()
        counts = {b: 0 for b in BODIES}
        ui = getattr(self, "_ui_lists", None) or {}
        bc = ui.get("body_counts") or {}
        for b in BODIES:
            counts[b] = int(bc.get(b) or 0)
        if not any(counts.values()):
            for p in getattr(self, "_paths", []):
                bb = _body_for_path(p)
                if bb in counts:
                    counts[bb] += 1
        self._body_counts = counts
        for body, button in self.body_buttons.items():
            n = counts.get(body, 0)
            label = f"{BODY_LABELS.get(body, body)} ({body})\n{n}"
            button.configure(
                text=label,
                bg=ACCENT_BLUE if body == selected else INPUT,
                fg="#FFFFFF" if body == selected else TEXT,
                highlightbackground=ORANGE if body == selected else STROKE,
            )
        if hasattr(self, "status_body"):
            self.status_body.configure(text=f"Body: {selected}")

    # ---------- Catalog / selection ----------
    def _load_clip_list(self):
        # Product list: map-viewer locomotion (.ani) first; --all-clips for everything.
        candidates = []
        moves_dir = self.samples_dir / "player" / "moves"
        loco_dir = moves_dir / "from_mapviewer" / "f1" / "动作"
        if getattr(self, "_all_clips", False):
            for pattern in ("*.ani", "*.mesh.ani"):
                candidates.extend(self.samples_dir.glob(pattern))
            for sub in ("plot", "pss", "player"):
                subdir = self.samples_dir / sub
                if subdir.is_dir():
                    candidates.extend(subdir.glob("*.ani"))
                    candidates.extend(subdir.glob("*.mesh.ani"))
            if moves_dir.is_dir():
                candidates.extend(moves_dir.rglob("*.ani"))
                candidates.extend(moves_dir.rglob("*.mesh.ani"))
        else:
            if loco_dir.is_dir():
                candidates.extend(loco_dir.glob("*.ani"))
            if moves_dir.is_dir():
                # also include other F1 .ani (demoted in sort)
                candidates.extend(moves_dir.rglob("*.ani"))
            # Prefer locomotion names; keep others but demote charge via sort
            candidates = [p for p in candidates if p.suffix.lower() == ".ani"]
        seen = set()
        self._paths = []
        for path in sorted(candidates, key=_loco_rank):
            resolved = path.resolve()
            if resolved not in seen:
                seen.add(resolved)
                self._paths.append(path)
        default = _loco_default_path(self._paths, samples_dir=self.samples_dir)
        inferred = (_body_for_path(default) if default else None) or next(
            (_body_for_path(path) for path in self._paths if _body_for_path(path)), "F1"
        )
        self.body_var.set(inferred)
        # Clear charge search demo — surface 走路/跳跃
        if hasattr(self, "search_var"):
            self.search_var.set("")
        self._page = 0
        self._refresh_catalog(select_first=False)
        if not self._paths:
            self.meta.configure(text=f"No 走路/跳跃 .ani under {loco_dir}")
            return
        target = default or self._paths[0]
        self._visible_paths = [p for p in self._paths if _body_for_path(p) in (None, self.body_var.get())]
        if target not in self._visible_paths:
            self._visible_paths = list(self._paths)
        self._visible_paths = sorted(self._visible_paths, key=_loco_rank)
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
        self.title("JX3 Ani Player · free play companion")
        self.meta.configure(
            text=f"Ready: {_clip_name(self._visible_paths[idx])} — 走路/跳跃 first · click Play"
        )
        self._paint_body_buttons()
        self._paint_tab_buttons()
        self._refresh_character_rail()
        if hasattr(self, "status_count"):
            self.status_count.configure(text=f"{len(self._visible_paths)} clips")
        if hasattr(self, "vp_label"):
            self.vp_label.configure(text=f"{_clip_name(self._visible_paths[idx])} · ANI")


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


    def _select_body(self, body: str):
        self.pause()
        self.body_var.set(body)
        self.clip = None
        self.clock.set_clip(None)
        self._refresh_catalog(select_first=True)
        self._paint_body_buttons()

    def _refresh_character_rail(self):
        """Visible Character 角色 list from clip paths, else body default label."""
        selected_body = self.body_var.get() if hasattr(self, "body_var") else "F1"
        body_paths = [
            p for p in getattr(self, "_paths", [])
            if p is not None and _body_for_path(p) in (None, selected_body)
        ]
        inferred = sorted({_character_for_path(p) for p in body_paths if p is not None})
        # Drop opaque Unknown when we have a body default.
        inferred = [c for c in inferred if c and c != "Unknown"]
        default_name = CHAR_BY_BODY.get(selected_body, BODY_LABELS.get(selected_body, selected_body))
        if not inferred:
            inferred = [default_name]
        self._character_ids = inferred
        if hasattr(self, "character_list"):
            self.character_list.delete(0, tk.END)
            for character in inferred:
                self.character_list.insert(tk.END, f"•  {character}  ({selected_body})")
            self.character_list.selection_clear(0, tk.END)
            self.character_list.selection_set(0)
        if hasattr(self, "character_label"):
            self.character_label.configure(text=f"{inferred[0]}  ·  {selected_body}")

    def _on_character_select(self, _event=None):
        selection = self.character_list.curselection()
        if selection and selection[0] < len(self._character_ids):
            self.character_label.configure(
                text=f"{self._character_ids[selection[0]]}  ·  {self.body_var.get()}"
            )


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

    def _on_select_clip(self):
        row = self._row_for_list_selection()
        if row is None:
            return
        entry = row.get("entry") or {}
        path = row.get("path")
        # fill detail panel from catalog row
        if hasattr(self, "detail_anim"):
            self.detail_anim.configure(text=f"Clip ID: {entry.get('id', row.get('anim_id', '—'))}")
            self.detail_kind.configure(
                text=f"Kind / Sheath: {entry.get('kind_id', '—')} / {entry.get('sheath_type', entry.get('sheath_type', '—'))}"
            )
            loop_v = entry.get("is_loop")
            self.detail_loop.configure(text=f"Loop: {'Yes' if loop_v == 1 else ('No' if loop_v == 0 else '—')}")
            self.detail_speed.configure(
                text=f"Speed / Ratio: {entry.get('anim_speed') or '—'} / {entry.get('anim_ratio') or '—'}"
            )
        if hasattr(self, "vp_label"):
            self.vp_label.configure(text=f"{row.get('label')} · {row.get('tag')}")
        # Dev2: resolve playable_path / .tani → skeletal .ani, then load_clock_for_row (MIN2).
        resolve_src = {
            **entry,
            "label": row.get("label") or entry.get("label"),
            "path": path,
            "playable_path": row.get("playable_path") or entry.get("playable_path") or "",
            "body": entry.get("body") or row.get("body") or self.body_var.get(),
            "filename": entry.get("filename") or row.get("label"),
            "local_path": entry.get("local_path") or "",
            "anim_file": entry.get("anim_file") or "",
        }
        play_path = None
        try:
            play_path = resolve_playable_path(resolve_src, root=Path(__file__).resolve().parent)
        except ResolveError:
            play_path = _resolve_playable_ani(path, self.samples_dir, entry)
        if play_path is None:
            self.clip = None
            if hasattr(self, "clock"):
                self.clock.set_clip(None)
            src = path.name if path is not None else (row.get("label") or "row")
            self.meta.configure(
                text=f"No resolved .ani for {src} — check moves/ or tani.ani_path"
            )
            return
        via_tani = (
            (path is not None and path.suffix.lower() == ".tani")
            or (row.get("tag") or "").upper() == "TANI"
            or (entry.get("badge") or "").upper() == "TANI"
        )
        path = play_path
        self.body_var.set(_body_for_path(path) or entry.get("body") or self.body_var.get())
        self._paint_body_buttons()
        try:
            magic = sniff_magic(path)
            if magic == "MINA":
                self.clip = load_mina(path)
                self._clip_path = path
                self.clock.set_clip(self.clip, seek_start=True)
            elif magic == "MIN2":
                # Dev2 surface: resolve + PlaybackClock.set_clip via pose_drive.
                load_clock_for_row(path, root=Path(__file__).resolve().parent, clock=self.clock)
                self.clip = self.clock.clip
                self._clip_path = path
            else:
                raise ValueError(f"unsupported ani magic {magic!r} from {path.name}")
        except Exception as error:  # keep missing/bad resources inline, not modal
            self.clip = None
            if hasattr(self, "clock"):
                self.clock.set_clip(None)
            self.meta.configure(text=f"Clip indexed but unavailable: {error}")
            return
        self.edges = stick_edges(self.clip.bone_names)
        self.clock.loop = bool(self.loop.get())
        self.scrub.configure(to=max(0, self.clip.frame_count - 1))
        self.scrub.set(0)
        self.bone_list.delete(0, tk.END)
        for name in self.clip.bone_names:
            self.bone_list.insert(tk.END, name)
        self.bone_count_label.configure(text=f"{self.clip.bone_count} bones")
        self._bind_body_mesh(path, magic)
        mesh_note = f"  ·  skinned {self._mesh_label}" if self._mesh is not None else ""
        via = "  ·  via tani" if via_tani else ""
        self.meta.configure(
            text=f"Clip: {_clip_name(path)}  ·  {self.clip.frame_count} frames  ·  {self.clip.tex_hint or magic}{mesh_note}{via}"
        )
        self.clip_hint.configure(text=f"{_clip_name(path)}  ·  Ani  ·  {self.body_var.get()}")
        # Product: 走路/跳跃 → skin+clipFbx Mixer (map-viewer). Else MIN2 pose feed.
        # Pass list row so label/clip_fbx/driver hit even when resolved .ani path is opaque.
        row_for_mix = {
            **entry,
            **{k: row.get(k) for k in ("label", "name", "path", "playable_path", "clip_fbx", "clipFbx", "clip_json", "clipJson", "driver", "filename", "kind") if row.get(k) is not None},
        }
        mix = self._resolve_mixer_clip_fbx(path, row=row_for_mix)
        mix_json = self._resolve_mixer_clip_json(path, row=row_for_mix)
        if mix is not None and self.character_mode == "fbx" and self._fbx_actor is not None:
            self._nav_fbx_viewport(clip_fbx=mix, t=0.0, playing=False, label=_clip_name(path) or str(row.get("label") or ""))
        elif mix_json is not None and self.character_mode == "fbx" and self._fbx_actor is not None:
            self._nav_fbx_viewport(clip_json=mix_json, t=0.0, playing=False, label=_clip_name(path) or str(row.get("label") or ""))
        else:
            self._fbx_mixer_clip = None
            if self.character_mode == "fbx" and self._fbx_actor is not None:
                self._nav_fbx_viewport(clip_fbx=None, t=None, playing=False, label="bind")
        self._draw_frame()

    def _bind_body_mesh(self, clip_path: Path, magic: str) -> None:
        """Attach body mesh matching clip body chip for MIN2; clear otherwise."""
        self._mesh = None
        self._mesh_map = {}
        self._stick_rest = None
        self._mesh_label = ""
        if magic != "MIN2":
            return
        body = _body_for_path(clip_path) or self.body_var.get()
        mesh_path = MESH_BY_BODY.get(body, DEFAULT_BODY_MESH)
        # Hard rule: never put M2 沈眠风 mesh on an F1/F2 clip.
        if body in ("F1", "F2") and "m2_" in mesh_path.name.lower():
            self._mesh_label = f"F1/F2 mesh missing for {body}"
            return
        if not mesh_path.is_file():
            self._mesh_label = f"mesh missing ({mesh_path.name})"
            return
        try:
            mesh = load_mesh(mesh_path)
            normalize_influences(mesh)
            mapping = bone_name_map(mesh, list(self.clip.bone_names))
            if len(mapping) < 8:
                self._mesh_label = f"map too sparse ({len(mapping)})"
                return
            self._mesh = mesh
            self._mesh_map = mapping
            # Prefer RQ matrices_at bind; fall back to positions if clip lacks API.
            if hasattr(self.clip, "matrices_at"):
                self._stick_rest = self.clip.matrices_at(0)
            else:
                self._stick_rest = self.clip.positions_at(0)
            fbx_note = ""
            if actor_fbx is not None and actor_fbx.fbx_ready() and (body or "").upper() == "F1":
                fbx_note = f" · FBX actor {actor_fbx.FBX_PATH.name}"
            self._mesh_label = f"{mesh_path.name} ({len(mapping)}/{len(mesh.bones)} bones){fbx_note}"
        except Exception as error:
            self._mesh_label = f"mesh bind fail: {error}"

    # ---------- Transport (delegates to transport.PlaybackClock) ----------
    def _sync_loop(self):
        self.clock.loop = bool(self.loop.get())

    def _on_scrub(self, val):
        if not self.clip:
            return
        f = int(float(val))
        # Ignore echo from scrub.set() during _tick (same frame) so we do not
        # re-seek/rebase every poll and stall wall-clock playback.
        if f == self.clock.frame:
            return
        # Timeline scrub → seek (rebases wall clock if currently playing).
        self.clock.seek(f)
        if not self.clock.is_playing:
            self._draw_frame()

    def play(self):
        if not self.clip:
            return
        self._sync_loop()
        if self.clock.is_playing:
            return
        # Locomotion: Mixer free-run. Other clips: MIN2 apply_pose each tick.
        if self._fbx_actor is not None and getattr(self, "_fbx_mixer_clip", None) is not None:
            mc = self._fbx_mixer_clip
            is_json = str(mc).lower().endswith(".json")
            self._nav_fbx_viewport(
                clip_fbx=None if is_json else mc,
                clip_json=mc if is_json else None,
                t=None,
                playing=True,
                label=_clip_name(getattr(self, "_clip_path", None) or Path(".")),
            )
        elif self._fbx_actor is not None and hasattr(self, "vp_label"):
            self.vp_label.configure(text=f"Play · FBX 花萝 · {getattr(self.clip, 'tex_hint', '') or 'MIN2'}")
        self.clock.play()
        self._tick()

    def pause(self):
        self.clock.pause()
        if self._after_id:
            self.after_cancel(self._after_id)
            self._after_id = None

    def stop(self):
        self.pause()
        self.clock.stop()
        self.scrub.set(0)
        self._draw_frame()

    def _tick(self):
        if not self.clock.is_playing or not self.clip:
            return
        self._sync_loop()
        # Wall-clock via pose_drive.tick_pose — sample + optional FBX apply_pose.
        # Mixer locomotion: do not feed MIN2 locals into fbx_actor.
        actor = None if getattr(self, '_fbx_mixer_clip', None) is not None else self._fbx_actor
        sample = tick_pose(self.clock, actor=actor)
        self.scrub.set(sample.frame)
        self._draw_frame(sample=sample)
        if not self.clock.is_playing:
            # Hit end with loop=False.
            self._after_id = None
            return
        # short poll; catch-up happens via elapsed*fps inside clock.tick
        self._after_id = self.after(8, self._tick)

    def _draw_frame(self, sample=None):
        if not self.clip:
            return
        # Prefer Dev2 sample(with_pose=True) so paint shares one pose fetch with the clock.
        if sample is None:
            sample = self.clock.sample(with_pose=True)
        f = sample.frame
        positions = sample.positions or self.clip.positions_at(f)

        # Dev4 FBX: Mixer locomotion OR MIN2 apply_pose.
        mix_clip = getattr(self, "_fbx_mixer_clip", None)
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
                    _is_j = str(mix_clip).lower().endswith(".json")
                    self._nav_fbx_viewport(
                        clip_fbx=None if _is_j else mix_clip,
                        clip_json=mix_clip if _is_j else None,
                        t=None,
                        playing=True,
                    )
            else:
                key = (str(mix_clip), round(t, 2), False)
                if getattr(self, "_fbx_scrub_key", None) != key:
                    self._fbx_scrub_key = key
                    _is_j = str(mix_clip).lower().endswith(".json")
                    self._nav_fbx_viewport(
                        clip_fbx=None if _is_j else mix_clip,
                        clip_json=mix_clip if _is_j else None,
                        t=t,
                        playing=False,
                    )
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
            return

        self.ax.clear()
        self.ax.set_facecolor(VIEW)

        drew_mesh = False
        if self.skinned.get() and self._mesh is not None and self._stick_rest is not None:
            try:
                if sample.matrices is not None:
                    pose_for_skin = sample.matrices
                elif hasattr(self.clip, "matrices_at"):
                    pose_for_skin = self.clip.matrices_at(f)
                else:
                    pose_for_skin = positions
                verts = skin_positions(self._mesh, self._stick_rest, pose_for_skin, self._mesh_map)
                # Y-up display remap (x, z, y) matching Dev4 proof renderer.
                disp = np.column_stack([verts[:, 0], verts[:, 2], verts[:, 1]])
                tris = disp[self._mesh.faces]
                coll = Poly3DCollection(
                    tris,
                    linewidths=0.02,
                    edgecolors=(0.2, 0.22, 0.25, 0.15),
                    facecolors=(0.55, 0.72, 0.88, 0.92),
                )
                self.ax.add_collection3d(coll)
                mins = disp.min(axis=0)
                maxs = disp.max(axis=0)
                center = (mins + maxs) * 0.5
                span = float((maxs - mins).max()) * 0.55 + 1.0
                self.ax.set_xlim(center[0] - span, center[0] + span)
                self.ax.set_ylim(center[1] - span, center[1] + span)
                self.ax.set_zlim(center[2] - span, center[2] + span)
                self.ax.view_init(elev=12, azim=-60)
                drew_mesh = True
            except Exception:
                drew_mesh = False

        if not drew_mesh:
            xs = [p[0] for p in positions]
            ys = [p[1] for p in positions]
            zs = [p[2] for p in positions]
            self.ax.scatter(xs, ys, zs, c=ACCENT, s=18, depthshade=True)
            for pi, ci in self.edges:
                self.ax.plot(
                    [positions[pi][0], positions[ci][0]],
                    [positions[pi][1], positions[ci][1]],
                    [positions[pi][2], positions[ci][2]],
                    color="#AAAAAA",
                    linewidth=1.2,
                )
            allv = xs + ys + zs
            if allv:
                lo, hi = min(allv), max(allv)
                pad = (hi - lo) * 0.1 + 1.0
                self.ax.set_xlim(lo - pad, hi + pad)
                self.ax.set_ylim(lo - pad, hi + pad)
                self.ax.set_zlim(lo - pad, hi + pad)

        self.ax.set_xticks([])
        self.ax.set_yticks([])
        self.ax.set_zticks([])
        self.ax.xaxis.pane.fill = False
        self.ax.yaxis.pane.fill = False
        self.ax.zaxis.pane.fill = False
        fps = float(getattr(self.clip, "fps", 0) or self.fps or FPS_DEFAULT)
        t = f / fps
        total = (self.clip.frame_count - 1) / fps
        mode = "skinned" if drew_mesh else "stick"
        self.time_lbl.configure(text=f"{t:0.2f} / {total:0.2f}s   ·   frame {f}   ·   {mode}")
        self.canvas.draw_idle()


def main():
    ap = argparse.ArgumentParser(description="MINA .mesh.ani free player (R4b companion UI)")
    ap.add_argument(
        "--samples",
        type=Path,
        default=SAMPLES_DEFAULT,
        help="Directory of .ani / .mesh.ani samples",
    )
    ap.add_argument("--fps", type=float, default=FPS_DEFAULT, help="Fallback FPS when clip has none (MIN2 usually embeds fps)")
    ap.add_argument(
        "--character-mode",
        choices=("mesh", "stick", "fbx"),
        default="fbx",
        help="Viewport: fbx=花萝 character (default); mesh/stick=debug matplotlib stickers only",
    )
    ap.add_argument(
        "--all-clips",
        action="store_true",
        help="Show full samples catalog. Default surfaces 花萝 走路/跳跃 first (charge demoted).",
    )
    args = ap.parse_args()
    samples = args.samples
    if not samples.exists():
        alt = Path("/workspace/jx3-movie-editor-research/samples")
        if alt.exists():
            samples = alt
    app = AniPlayer(
        samples,
        fps=args.fps,
        all_clips=args.all_clips,
        character_mode=args.character_mode,
    )
    mode = "mesh" if args.character_mode == "stick" else args.character_mode
    if mode == "fbx":
        print(f"[character-mode=fbx] product 花萝 viewport — {getattr(app, '_fbx_url', None)}")
    else:
        print("[character-mode=mesh] DEBUG stickers only — not Andy product default")
    app.mainloop()


if __name__ == "__main__":
    main()
