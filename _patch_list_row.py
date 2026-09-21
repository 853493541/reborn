from pathlib import Path
import ast

p = Path(__file__).resolve().parent / "player.py"
src = p.read_text(encoding="utf-8")
old = (
    "        # Product: 走路/跳跃 → skin+clipFbx Mixer (map-viewer). Else MIN2 pose feed.\n"
    "        mix = self._resolve_mixer_clip_fbx(path)\n"
    "        if mix is not None and self.character_mode == \"fbx\" and self._fbx_actor is not None:\n"
    "            self._nav_fbx_viewport(clip_fbx=mix, t=0.0, playing=False, label=_clip_name(path))\n"
)
new = (
    "        # Product: 走路/跳跃 → skin+clipFbx Mixer (map-viewer). Else MIN2 pose feed.\n"
    "        # Pass list row so label/clip_fbx/driver hit even when resolved .ani path is opaque.\n"
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
if old not in src:
    i = src.find("_resolve_mixer_clip_fbx(path)")
    print("OLD NOT FOUND", repr(src[i - 80 : i + 220]))
    raise SystemExit(1)
p.write_text(src.replace(old, new, 1), encoding="utf-8")
ast.parse(p.read_text(encoding="utf-8"))
print("PATCHED_OK")
