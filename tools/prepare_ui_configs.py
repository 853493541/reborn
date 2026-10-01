"""Stage the layout INIs referenced by the ui-process-app inventory from PakV4.

The app's `assets/ui/Config/**` tree is a git-ignored local extraction. This tool
reads `ui-process-app/Data/ui_inventory.json`, extracts every referenced INI
(ui-root windows plus their list prototypes, and the pak-root settlement files)
through the official PakV4 extractor, and writes them where the app expects:

  ui-root  -> ui-process-app/assets/ui/<relative path, subfolders preserved>
  pak-root -> ui-process-app/assets/pak/<name>

Together with `tools/prepare_ui_text.py` (text assets) and
`tools/prepare_ui_fonts.py` (fonts + scheme tables) this makes the `--selftest`
UI gate runnable from a fresh checkout. Run with the venv Python:

  .venv\\Scripts\\python.exe tools\\prepare_ui_configs.py
"""

import json
import pathlib
import sys

REPO = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO))

import pss_assets  # noqa: E402

INVENTORY = REPO / "ui-process-app" / "Data" / "ui_inventory.json"
WORK = REPO / "proof" / "ui" / "battle_hud" / "prepare_configs_work"
UI_OUT = REPO / "ui-process-app" / "assets" / "ui" / "Config" / "Default"
PAK_OUT = REPO / "ui-process-app" / "assets" / "pak"


def collect():
    inv = json.loads(INVENTORY.read_text(encoding="utf-8"))
    ui_paths, pak_paths = [], []
    for stage in inv.get("stages", []):
        for w in stage.get("windows", []):
            path = (w.get("path") or "").replace("\\", "/").lstrip("/")
            if not path:
                continue
            if w.get("root") == "pak":
                pak_paths.append(path)
            else:
                ui_paths.append(path)
            for item in w.get("lists", []) or []:
                if item.get("ini"):
                    ui_paths.append(item["ini"].replace("\\", "/").lstrip("/"))
    ui_candidates = []
    for path in sorted(set(ui_paths)):
        if path.lower().startswith("config/"):
            ui_candidates.append("ui/" + path)
        else:
            ui_candidates.extend([
                "ui/Config/Default/" + path,
                "ui/Config/Default/BattleField/" + path,
            ])
    pak_candidates = []
    for path in sorted(set(pak_paths)):
        name = path.rsplit("/", 1)[-1]
        pak_candidates.append("ui/Config/Default/" + name)
        pak_candidates.append(path)
    return ui_candidates, pak_candidates


def main() -> int:
    if not INVENTORY.is_file():
        print("MISS inventory:", INVENTORY)
        return 1
    ui_candidates, pak_candidates = collect()
    UI_OUT.mkdir(parents=True, exist_ok=True)
    PAK_OUT.mkdir(parents=True, exist_ok=True)

    total = 0
    ui_root = REPO / "ui-process-app" / "assets" / "ui"
    for candidates, out in ((ui_candidates, ui_root), (pak_candidates, PAK_OUT)):
        by_name = {pathlib.PurePosixPath(c).name.lower(): c for c in candidates}
        for i in range(0, len(candidates), 100):
            chunk = candidates[i:i + 100]
            try:
                found = pss_assets.run_pakv4(chunk, work=WORK)
            except Exception as exc:  # noqa: BLE001
                print("ERR", exc)
                continue
            for rel, data in found.items():
                name = pathlib.PurePosixPath(rel).name
                cand = by_name.get(name.lower())
                if out == PAK_OUT:
                    dest = out / name
                else:
                    rel_out = cand[3:] if cand and cand.lower().startswith("ui/") else name
                    dest = out / pathlib.PurePosixPath(rel_out)
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_bytes(data)
                total += 1
                print("HIT %-4s %-40s %d" % (out.name, pathlib.PurePosixPath(dest).name, len(data)))
    print("staged %d files" % total)
    return 0 if total else 1


if __name__ == "__main__":
    raise SystemExit(main())
