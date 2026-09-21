# -*- coding: utf-8 -*-
"""Wire FLWS 蓄力/释放 to MIN2→AnimationClip Mixer (?clip= JSON)."""
from __future__ import annotations

import ast
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def patch_fbx_actor() -> None:
    p = ROOT / "fbx_actor.py"
    src = p.read_text(encoding="utf-8")
    if "clip_json" in src and "CLIP_JSON_FLWS_CHARGE" in src:
        print("fbx_actor already has clip_json wiring")
    else:
        # Add constants near CLIP_FBX_WALK
        needle = "CLIP_FBX_WALK"
        i = src.find(needle)
        if i < 0:
            raise SystemExit("CLIP_FBX_WALK missing")
        # insert after jump3 block — find CLIP_FBX_JUMP3 assignment end
        m = re.search(r"CLIP_FBX_JUMP3\s*=\s*[^\n]+\n", src)
        if not m:
            raise SystemExit("CLIP_FBX_JUMP3 missing")
        insert_at = m.end()
        consts = (
            "CLIP_JSON_FLWS_CHARGE = ROOT / \"web\" / \"runtime\" / \"clip_flws_charge.json\"\n"
            "CLIP_JSON_FLWS_CAST = ROOT / \"web\" / \"runtime\" / \"clip_flws_cast.json\"\n"
        )
        if "CLIP_JSON_FLWS_CHARGE" not in src:
            src = src[:insert_at] + consts + src[insert_at:]

        # Extend web_viewport_url signature + query
        old_sig = (
            "def web_viewport_url(\n"
            "    actor: \"FbxActor\",\n"
            "    *,\n"
            "    closeup: bool = True,\n"
            "    clip_fbx: Path | str | None = None,\n"
            "    t: float | None = None,\n"
            "    playing: bool = False,\n"
            ") -> str:"
        )
        new_sig = (
            "def web_viewport_url(\n"
            "    actor: \"FbxActor\",\n"
            "    *,\n"
            "    closeup: bool = True,\n"
            "    clip_fbx: Path | str | None = None,\n"
            "    clip_json: Path | str | None = None,\n"
            "    t: float | None = None,\n"
            "    playing: bool = False,\n"
            ") -> str:"
        )
        if old_sig not in src:
            raise SystemExit("web_viewport_url sig not found")
        src = src.replace(old_sig, new_sig, 1)

        old_clip_block = (
            "    if clip_fbx:\n"
            "        p = Path(clip_fbx)\n"
            "        # URL under /samples/...\n"
            "        try:\n"
            "            rel = p.resolve().relative_to(ROOT.resolve()).as_posix()\n"
            "            q[\"clipFbx\"] = \"/\" + rel\n"
            "        except Exception:\n"
            "            q[\"clipFbx\"] = \"/samples/actor_presets/f1_hualuo/mapviewer_clips/\" + p.name\n"
        )
        new_clip_block = (
            "    if clip_fbx:\n"
            "        p = Path(clip_fbx)\n"
            "        # URL under /samples/...\n"
            "        try:\n"
            "            rel = p.resolve().relative_to(ROOT.resolve()).as_posix()\n"
            "            q[\"clipFbx\"] = \"/\" + rel\n"
            "        except Exception:\n"
            "            q[\"clipFbx\"] = \"/samples/actor_presets/f1_hualuo/mapviewer_clips/\" + p.name\n"
            "    if clip_json:\n"
            "        jp = Path(clip_json)\n"
            "        try:\n"
            "            rel = jp.resolve().relative_to(ROOT.resolve()).as_posix()\n"
            "            q[\"clip\"] = \"/\" + rel\n"
            "        except Exception:\n"
            "            q[\"clip\"] = \"/web/runtime/\" + jp.name\n"
        )
        if old_clip_block not in src:
            raise SystemExit("clip_fbx URL block not found")
        src = src.replace(old_clip_block, new_clip_block, 1)

        # clip_json_for_flws helper after clip_fbx_for_locomotion
        if "def clip_json_for_flws" not in src:
            helper = '''

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

'''
            # insert before web_viewport_url
            j = src.find("def web_viewport_url")
            src = src[:j] + helper + src[j:]

        ast.parse(src)
        p.write_text(src, encoding="utf-8")
        print("patched fbx_actor")


def patch_player() -> None:
    p = ROOT / "player.py"
    src = p.read_text(encoding="utf-8")

    # Extend _nav_fbx_viewport to accept clip_json
    old_nav = (
        "    def _nav_fbx_viewport(self, *, clip_fbx=None, clipFbx=None, t=None, playing: bool = False, label: str = \"\") -> None:\n"
        "        \"\"\"Reload viewport URL for skin + optional clipFbx Mixer (forced-API twin).\"\"\"\n"
        "        clip_fbx = clip_fbx if clip_fbx is not None else clipFbx\n"
        "        actor = getattr(self, \"_fbx_actor\", None)\n"
        "        if actor is None:\n"
        "            return\n"
        "        try:\n"
        "            try:\n"
        "                from fbx_actor import web_viewport_url as _url_fn\n"
        "            except ImportError:\n"
        "                from fbx_actor import web_viewport_url as _url_fn\n"
        "            url = _url_fn(actor, closeup=True, clip_fbx=clip_fbx, t=t, playing=playing)\n"
    )
    new_nav = (
        "    def _nav_fbx_viewport(self, *, clip_fbx=None, clipFbx=None, clip_json=None, clipJson=None, t=None, playing: bool = False, label: str = \"\") -> None:\n"
        "        \"\"\"Reload viewport URL for skin + clipFbx or MIN2 clip-JSON Mixer.\"\"\"\n"
        "        clip_fbx = clip_fbx if clip_fbx is not None else clipFbx\n"
        "        clip_json = clip_json if clip_json is not None else clipJson\n"
        "        actor = getattr(self, \"_fbx_actor\", None)\n"
        "        if actor is None:\n"
        "            return\n"
        "        try:\n"
        "            try:\n"
        "                from fbx_actor import web_viewport_url as _url_fn\n"
        "            except ImportError:\n"
        "                from fbx_actor import web_viewport_url as _url_fn\n"
        "            url = _url_fn(actor, closeup=True, clip_fbx=clip_fbx, clip_json=clip_json, t=t, playing=playing)\n"
    )
    if old_nav not in src:
        raise SystemExit("player _nav_fbx_viewport head not found")
    src = src.replace(old_nav, new_nav, 1)

    # After setting _fbx_mixer_clip = clip_fbx, also allow clip_json marker
    src = src.replace(
        "        self._fbx_mixer_clip = clip_fbx\n",
        "        self._fbx_mixer_clip = clip_fbx or clip_json\n",
        1,
    )

    # Extend _resolve_mixer_clip_fbx → also resolve FLWS JSON; return tuple or add sibling method
    if "def _resolve_mixer_clip_json" not in src:
        helper = '''
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

'''
        anchor = "    def _resolve_mixer_clip_fbx(self, path, row=None):"
        if anchor not in src:
            raise SystemExit("resolve mixer fbx missing")
        src = src.replace(anchor, helper + anchor, 1)

    # In _on_select_clip after mix = resolve fbx, also try json
    old_mix = (
        "        mix = self._resolve_mixer_clip_fbx(\n"
        "            path,\n"
        "            row={\n"
        "                **entry,\n"
        "                **{k: row.get(k) for k in (\"label\", \"name\", \"path\", \"playable_path\", \"clip_fbx\", \"clipFbx\", \"driver\", \"filename\") if row.get(k) is not None},\n"
        "            },\n"
        "        )\n"
        "        if mix is not None and self.character_mode == \"fbx\" and self._fbx_actor is not None:\n"
        "            self._nav_fbx_viewport(clip_fbx=mix, t=0.0, playing=False, label=_clip_name(path) or str(row.get(\"label\") or \"\"))\n"
    )
    new_mix = (
        "        row_for_mix = {\n"
        "            **entry,\n"
        "            **{k: row.get(k) for k in (\"label\", \"name\", \"path\", \"playable_path\", \"clip_fbx\", \"clipFbx\", \"clip_json\", \"clipJson\", \"driver\", \"filename\", \"kind\") if row.get(k) is not None},\n"
        "        }\n"
        "        mix = self._resolve_mixer_clip_fbx(path, row=row_for_mix)\n"
        "        mix_json = self._resolve_mixer_clip_json(path, row=row_for_mix)\n"
        "        if mix is not None and self.character_mode == \"fbx\" and self._fbx_actor is not None:\n"
        "            self._nav_fbx_viewport(clip_fbx=mix, t=0.0, playing=False, label=_clip_name(path) or str(row.get(\"label\") or \"\"))\n"
        "        elif mix_json is not None and self.character_mode == \"fbx\" and self._fbx_actor is not None:\n"
        "            self._nav_fbx_viewport(clip_json=mix_json, t=0.0, playing=False, label=_clip_name(path) or str(row.get(\"label\") or \"\"))\n"
    )
    if old_mix not in src:
        raise SystemExit("on_select mix block not found")
    src = src.replace(old_mix, new_mix, 1)

    # play() free-run should pass clip_json when mixer clip is json
    old_play = (
        "        if self._fbx_actor is not None and getattr(self, \"_fbx_mixer_clip\", None) is not None:\n"
        "            self._nav_fbx_viewport(\n"
        "                clip_fbx=self._fbx_mixer_clip,\n"
        "                t=None,\n"
        "                playing=True,\n"
        "                label=_clip_name(getattr(self, \"_clip_path\", None) or Path(\".\")),\n"
        "            )\n"
    )
    new_play = (
        "        if self._fbx_actor is not None and getattr(self, \"_fbx_mixer_clip\", None) is not None:\n"
        "            mc = self._fbx_mixer_clip\n"
        "            is_json = str(mc).lower().endswith(\".json\")\n"
        "            self._nav_fbx_viewport(\n"
        "                clip_fbx=None if is_json else mc,\n"
        "                clip_json=mc if is_json else None,\n"
        "                t=None,\n"
        "                playing=True,\n"
        "                label=_clip_name(getattr(self, \"_clip_path\", None) or Path(\".\")),\n"
        "            )\n"
    )
    if old_play not in src:
        raise SystemExit("play mixer block not found")
    src = src.replace(old_play, new_play, 1)

    # _draw_frame mixer scrub/play similarly
    old_draw_nav_play = (
        "                    self._nav_fbx_viewport(clip_fbx=mix_clip, t=None, playing=True)\n"
    )
    new_draw_nav_play = (
        "                    _is_j = str(mix_clip).lower().endswith(\".json\")\n"
        "                    self._nav_fbx_viewport(\n"
        "                        clip_fbx=None if _is_j else mix_clip,\n"
        "                        clip_json=mix_clip if _is_j else None,\n"
        "                        t=None,\n"
        "                        playing=True,\n"
        "                    )\n"
    )
    if old_draw_nav_play in src:
        src = src.replace(old_draw_nav_play, new_draw_nav_play, 1)

    old_draw_nav_scrub = (
        "                    self._nav_fbx_viewport(clip_fbx=mix_clip, t=t, playing=False)\n"
    )
    new_draw_nav_scrub = (
        "                    _is_j = str(mix_clip).lower().endswith(\".json\")\n"
        "                    self._nav_fbx_viewport(\n"
        "                        clip_fbx=None if _is_j else mix_clip,\n"
        "                        clip_json=mix_clip if _is_j else None,\n"
        "                        t=t,\n"
        "                        playing=False,\n"
        "                    )\n"
    )
    if old_draw_nav_scrub in src:
        src = src.replace(old_draw_nav_scrub, new_draw_nav_scrub, 1)

    ast.parse(src)
    p.write_text(src, encoding="utf-8")
    print("patched player")


def patch_catalog() -> None:
    p = ROOT / "samples/player/catalog/playable_f1_hualuo.json"
    d = json.loads(p.read_text(encoding="utf-8"))
    changed = 0
    for row in d.get("default_clips") or []:
        lab = str(row.get("label") or "")
        if "蓄力" in lab and "风来" in lab:
            row["clip_json"] = "web/runtime/clip_flws_charge.json"
            row["driver"] = "min2AnimationClip"
            changed += 1
        elif "释放" in lab and "风来" in lab:
            row["clip_json"] = "web/runtime/clip_flws_cast.json"
            row["driver"] = "min2AnimationClip"
            changed += 1
    p.write_text(json.dumps(d, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("catalog rows", changed)


def main() -> None:
    # export clips first
    from export_min2_clip_json import main as export_main

    export_main()
    patch_fbx_actor()
    patch_player()
    patch_catalog()
    # verify resolve
    from fbx_actor import clip_json_for_flws, web_viewport_url, load_fbx_actor, ensure_server

    c = clip_json_for_flws("风来吴山·蓄力")
    cast = clip_json_for_flws("风来吴山·释放")
    print("resolve charge", c)
    print("resolve cast", cast)
    assert c and c.is_file()
    assert cast and cast.is_file()
    actor = load_fbx_actor()
    ensure_server(actor)
    url = web_viewport_url(actor, clip_json=c, t=0.45)
    print("URL", url)
    print("DONE_APPLY_FLWS")


if __name__ == "__main__":
    main()
