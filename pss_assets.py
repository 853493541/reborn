"""Extract original PSS-referenced assets with the official Seasun tool.

Rules (`SFX_GROUND_RULES.md`): original game resources only, extracted with
official tools.  MovieEditor's PSEF XML references logical names like
`....tga`; the engine actually stores `....dds`, so the extractor tries the
declared path plus extension-swapped candidates.

Outputs land under `assets/sfx/extract/` (gitignored) together with a
`manifest.json` recording the logical path, local file, bytes and magic.
"""
from __future__ import annotations

import json
import shutil
import struct
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path

from pss import PssEffect, parse_pss

ROOT = Path(__file__).resolve().parent
PAKV4_EXE = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PakV4SfxExtract.exe")
WORK_ROOT = Path(r"C:\jx3tmp\pss_assets")
DEST_ROOT = ROOT / "assets" / "sfx" / "extract"

TEXTURE_EXTS = (".tga", ".dds", ".png", ".jpg", ".bmp")
MATERIAL_EXTS = (".def", ".jsondef")
MESH_EXTS = (".mesh",)
ANIM_EXTS = (".ani",)

GBK = "gb18030"


@dataclass
class ExtractRequest:
    logical: str
    tried: list[str] = field(default_factory=list)


def candidate_paths(logical: str) -> list[str]:
    """Declared logical path plus extension-swapped engine-storage variants."""
    logical = logical.replace("/", "\\")
    low = logical.lower()
    out = [logical]
    if low.endswith(".tga"):
        out.append(logical[: -len(".tga")] + ".dds")
        out.append(logical[: -len(".tga")] + ".TGA")
    elif low.endswith(".dds"):
        out.append(logical[: -len(".dds")] + ".tga")
    elif low.endswith(".def"):
        out.append(logical + ".jsondef")
        out.append(logical[: -len(".def")] + ".jsondef")
    elif low.endswith(".jsondef"):
        out.append(logical[: -len(".jsondef")] + ".def")
    return out


def collect_requests(effect: PssEffect) -> list[ExtractRequest]:
    seen: dict[str, ExtractRequest] = {}
    for value in effect.all_strings():
        low = value.lower()
        if not low.endswith(TEXTURE_EXTS + MATERIAL_EXTS + MESH_EXTS + ANIM_EXTS):
            continue
        key = value.lower()
        if key in seen:
            continue
        seen[key] = ExtractRequest(logical=value, tried=candidate_paths(value))
    return list(seen.values())


def _write_pathlist(path: Path, entries: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = "\r\n".join(entries) + "\r\n"
    path.write_bytes(text.encode(GBK, errors="replace"))


def run_pakv4(entries: list[str], work: Path = WORK_ROOT) -> dict[str, bytes]:
    """Run PakV4SfxExtract for ``entries``; return logical-ish path -> bytes."""
    if not PAKV4_EXE.is_file():
        raise FileNotFoundError(f"PakV4SfxExtract not found: {PAKV4_EXE}")
    if not entries:
        return {}
    list_path = work / "pathlist.txt"
    out_dir = work / "out"
    _write_pathlist(list_path, entries)
    if out_dir.exists():
        shutil.rmtree(out_dir, ignore_errors=True)
    out_dir.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [str(PAKV4_EXE), str(list_path), str(out_dir)],
        cwd=str(PAKV4_EXE.parent),
        capture_output=True,
        text=True,
        timeout=300,
    )
    found: dict[str, bytes] = {}
    for file in out_dir.rglob("*"):
        if file.is_file() and not file.name.startswith("_"):
            rel = file.relative_to(out_dir).as_posix()
            found[rel] = file.read_bytes()
    return found


def _magic(data: bytes) -> str:
    if len(data) < 4:
        return "short"
    head = data[:4]
    if head in (b"DDS ",):
        return "DDS"
    if head[:2] == b"\xff\xd8":
        return "JPEG"
    if head[:4] == b"PAR\x00":
        return "PAR"
    if head[:4] == b"MIN2":
        return "MIN2"
    if head[:4] == b"MINA":
        return "MINA"
    if data[0x54:0x58] == b"HSEM" or b"HSEM" in data[:0x60]:
        return "HSEM"
    if head[:2] == b"PK":
        return "ZIP"
    return head.hex()


def _dest_for(logical: str, matched: str) -> Path:
    # matched is the extractor-relative path, e.g. data/source/other/.../x.dds
    rel = matched.replace("\\", "/")
    return DEST_ROOT / rel


def extract_for_effect(effect: PssEffect, *, dry_run: bool = False) -> dict:
    requests = collect_requests(effect)
    all_candidates: list[str] = []
    for req in requests:
        for candidate in req.tried:
            if candidate not in all_candidates:
                all_candidates.append(candidate)
    found = {} if dry_run else run_pakv4(all_candidates)
    lower_index = {key.lower().replace("\\", "/"): key for key in found}
    name_index: dict[str, str] = {}
    for key in found:
        name_index.setdefault(Path(key).name.lower(), key)

    manifest: list[dict] = []
    for req in requests:
        match_key = None
        for candidate in req.tried:
            key = candidate.replace("\\", "/").lower()
            hit = lower_index.get(key)
            if hit is None:
                hit = name_index.get(Path(candidate).name.lower())
            if hit is not None:
                match_key = hit
                break
        entry = {"logical": req.logical, "tried": req.tried, "local": None}
        if match_key is not None:
            data = found[match_key]
            dest = _dest_for(req.logical, match_key)
            if not dry_run:
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_bytes(data)
            entry.update(
                {
                    "local": str(dest.relative_to(ROOT)),
                    "bytes": len(data),
                    "magic": _magic(data),
                }
            )
        manifest.append(entry)

    summary = {
        "pss": str(effect.path),
        "requested": len(requests),
        "found": sum(1 for m in manifest if m.get("local")),
        "missing": [m["logical"] for m in manifest if not m.get("local")],
        "entries": manifest,
    }
    if not dry_run:
        manifests = DEST_ROOT / "manifests"
        manifests.mkdir(parents=True, exist_ok=True)
        (manifests / f"{effect.path.stem}.json").write_text(
            json.dumps(summary, ensure_ascii=False, indent=1), encoding="utf-8"
        )
    return summary


def load_asset_index(dest_root: Path = DEST_ROOT) -> dict[str, str]:
    """Merge staged manifests into logical-path(lower) -> local file index."""
    index: dict[str, str] = {}
    manifests = dest_root / "manifests"
    if not manifests.is_dir():
        return index
    for manifest_path in sorted(manifests.glob("*.json")):
        try:
            data = json.loads(manifest_path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        for entry in data.get("entries", []):
            logical = str(entry.get("logical") or "")
            local = entry.get("local")
            if logical and local:
                index.setdefault(logical.replace("\\", "/").lower(), str(ROOT / local))
    return index


def main() -> None:
    import argparse

    ap = argparse.ArgumentParser(description="Extract PSS-referenced original assets")
    ap.add_argument("pss", type=Path, nargs="+")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--report", type=Path, default=None)
    args = ap.parse_args()
    reports = []
    for p in args.pss:
        effect = parse_pss(p)
        report = extract_for_effect(effect, dry_run=args.dry_run)
        reports.append(report)
        print(f"{p.name}: found {report['found']}/{report['requested']}")
        for missing in report["missing"]:
            print(f"  MISSING {missing}")
    if args.report:
        args.report.write_text(json.dumps(reports, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"wrote {args.report}")


if __name__ == "__main__":
    main()
