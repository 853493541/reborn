"""Native PSS/PAR decoder.

The authoritative spec is the PSEF DataStorage XML that MovieEditor writes into
the original `.pss` bytes (PACK tail).  This module parses that XML plus the
binary header/TOC and exposes structured emitter/module/resource data.

See `PSS_FORMAT.md` and `SFX_GROUND_RULES.md` (map-viewer is not a reference).
"""
from __future__ import annotations

import io
import json
import struct
import zipfile
from dataclasses import dataclass, field
from functools import cached_property
from pathlib import Path
from typing import Any, Iterator
from xml.etree import ElementTree as ET

MAGIC = b"PAR\x00"
PACK_MAGIC = b"PACK"
XML_ENTRY = "PSEF"

TEXTURE_EXTS = (".tga", ".dds", ".png", ".jpg", ".bmp")
MATERIAL_EXTS = (".def", ".jsondef")
MESH_EXTS = (".mesh",)
ANIM_EXTS = (".ani",)
PATH_EXTS = TEXTURE_EXTS + MATERIAL_EXTS + MESH_EXTS + ANIM_EXTS


class PssError(ValueError):
    """Raised when a file is not a valid PSS/PAR container."""


@dataclass
class PssBlock:
    index: int
    type: int
    offset: int
    size: int

    def to_dict(self) -> dict:
        return {"index": self.index, "type": self.type, "offset": self.offset, "size": self.size}


@dataclass
class PssModule:
    type: str
    enable: bool
    element: ET.Element

    def strings(self) -> list[str]:
        out: list[str] = []
        for el in self.element.iter("PEVariant"):
            value = el.get("Value", "")
            if value:
                out.append(value)
        return out

    def keyframes(self) -> Iterator[dict]:
        for kp in self.element.iter("KeyPoint"):
            values = []
            for v in kp.findall("Value"):
                values.append(_typed_value(v.get("Type", ""), v.get("Value", "")))
            yield {
                "time": _number(kp.get("Time")),
                "values": values,
                "interpolation": kp.get("InterpolationType"),
                "attrs": {
                    k: _number(v)
                    for k, v in kp.attrib.items()
                    if k not in ("Time", "InterpolationType")
                },
            }

    def element_types(self) -> list[str]:
        out: list[str] = []
        for el in self.element.iter("Element"):
            t = el.get("Type")
            if t:
                out.append(t)
        return out

    def to_dict(self) -> dict:
        return {
            "type": self.type,
            "enable": self.enable,
            "strings": self.strings(),
            "elements": self.element_types(),
            "keyframes": list(self.keyframes()),
        }


@dataclass
class PssEmitter:
    index: int
    attrs: dict
    modules: list[PssModule]

    @property
    def name(self) -> str:
        return str(self.attrs.get("Name", ""))

    @property
    def id(self) -> str:
        return str(self.attrs.get("ID", ""))

    def module(self, module_type: str) -> PssModule | None:
        for mod in self.modules:
            if mod.type == module_type:
                return mod
        return None

    def module_active(self, module_type: str) -> str | None:
        mod = self.module(module_type)
        if mod is None:
            return None
        for el in mod.element.iter("EMData"):
            active = el.get("ActiveType")
            if active:
                return active
        return None

    def material(self) -> str | None:
        for value in self._material_strings():
            if value.lower().endswith(MATERIAL_EXTS):
                return value
        return None

    def textures(self) -> list[dict]:
        out: list[dict] = []
        label: str | None = None
        for value in self._material_strings():
            low = value.lower()
            if low.endswith(MATERIAL_EXTS):
                continue
            if low.endswith(TEXTURE_EXTS):
                out.append({"label": label, "path": value})
                label = None
            else:
                label = value
        return out

    def _material_strings(self) -> list[str]:
        mod = self.module("Particle Material")
        return mod.strings() if mod else []

    def mesh(self) -> str | None:
        for mod in self.modules:
            for value in mod.strings():
                if value.lower().endswith(MESH_EXTS):
                    return value
        return None

    def kind(self) -> str:
        if self.mesh():
            return "mesh"
        active = self.module_active("Particle Type") or ""
        if "Mesh" in active:
            return "mesh-unresolved"
        return "sprite"

    def timing(self) -> dict:
        return {
            "duration_ms": _number(self.attrs.get("DurationTime")),
            "delay_ms": _number(self.attrs.get("DelayTime")),
            "interval_ms": _number(self.attrs.get("Interval")),
            "repeat_times": _number(self.attrs.get("RepeatTimes")),
            "forever": _bool(self.attrs.get("ParticleForever", "0")),
        }

    def resources(self) -> dict:
        return {
            "material": self.material(),
            "textures": self.textures(),
            "mesh": self.mesh(),
        }

    def to_dict(self, *, include_modules: bool = True) -> dict:
        modules = [
            m.to_dict() if include_modules else {
                "type": m.type,
                "enable": m.enable,
                "keyframes": sum(1 for _ in m.keyframes()),
            }
            for m in self.modules
        ]
        return {
            "index": self.index,
            "name": self.name,
            "id": self.id,
            "attrs": self.attrs,
            "kind": self.kind(),
            "shape": self.module_active("Emitter Shape"),
            "particle_type": self.module_active("Particle Type"),
            "motion": self.attrs.get("MotionType"),
            "face": self.attrs.get("FaceType"),
            "blend": self.attrs.get("BlendType"),
            "uv_motion": self.attrs.get("UVMotionType"),
            "timing": self.timing(),
            "resources": self.resources(),
            "modules": modules,
        }


@dataclass
class PssEffect:
    path: Path
    version: int
    header_u16: int
    block_region_end: int
    blocks: list[PssBlock]
    xml: ET.Element
    warnings: list[str] = field(default_factory=list)

    @cached_property
    def emitters(self) -> list[PssEmitter]:
        out = []
        for i, em in enumerate(self.xml.findall("Emitter")):
            modules = [
                PssModule(
                    type=mod.get("ModuleType", ""),
                    enable=_bool(mod.get("Enable", "0")),
                    element=mod,
                )
                for mod in em.findall("Module")
            ]
            out.append(PssEmitter(index=i, attrs=dict(em.attrib), modules=modules))
        return out

    @property
    def globals(self) -> dict:
        return dict(self.xml.attrib)

    def all_strings(self) -> list[str]:
        out: list[str] = []
        for el in self.xml.iter("PEVariant"):
            value = el.get("Value", "")
            if value:
                out.append(value)
        return out

    def resources(self) -> dict:
        textures: list[str] = []
        materials: list[str] = []
        meshes: list[str] = []
        for value in self.all_strings():
            low = value.lower()
            if low.endswith(TEXTURE_EXTS):
                if value not in textures:
                    textures.append(value)
            elif low.endswith(MATERIAL_EXTS):
                if value not in materials:
                    materials.append(value)
            elif low.endswith(MESH_EXTS):
                if value not in meshes:
                    meshes.append(value)
        return {"textures": textures, "materials": materials, "meshes": meshes}

    def to_dict(self, *, include_modules: bool = True) -> dict:
        return {
            "path": str(self.path),
            "version": self.version,
            "header_u16": self.header_u16,
            "block_region_end": self.block_region_end,
            "blocks": [b.to_dict() for b in self.blocks],
            "globals": self.globals,
            "resources": self.resources(),
            "emitters": [e.to_dict(include_modules=include_modules) for e in self.emitters],
            "warnings": list(self.warnings),
        }


def _number(raw: Any) -> float | int | None:
    if raw is None:
        return None
    if isinstance(raw, (int, float)):
        return raw
    text = str(raw).strip()
    if not text:
        return None
    try:
        return int(text)
    except ValueError:
        pass
    try:
        return float(text)
    except ValueError:
        return None


def _bool(raw: Any) -> bool:
    return str(raw).strip().lower() not in ("", "0", "false", "none")


def _typed_value(type_name: str, raw: str) -> Any:
    t = type_name.lstrip("/")
    if t.lower() == "string":
        return raw
    if t.lower() in ("bool",):
        return _bool(raw)
    num = _number(raw)
    return num if num is not None else raw


def _parse_binary(data: bytes) -> tuple[int, int, int, list[PssBlock]]:
    if len(data) < 16 or data[:4] != MAGIC:
        raise PssError("not a PAR container")
    version = struct.unpack_from("<H", data, 4)[0]
    header_u16 = struct.unpack_from("<H", data, 6)[0]
    block_region_end = struct.unpack_from("<I", data, 8)[0]
    count = struct.unpack_from("<I", data, 12)[0]
    toc_end = 16 + count * 12
    if toc_end > len(data):
        raise PssError(f"TOC truncated: count={count}")
    blocks: list[PssBlock] = []
    cursor = toc_end
    for i in range(count):
        t, off, size = struct.unpack_from("<III", data, 16 + i * 12)
        if off + size > len(data):
            raise PssError(f"block {i} out of range")
        blocks.append(PssBlock(index=i, type=t, offset=off, size=size))
        cursor = max(cursor, off + size)
    if block_region_end and block_region_end > len(data):
        raise PssError(f"block_region_end {block_region_end} beyond file")
    return version, header_u16, block_region_end or cursor, blocks


def _extract_xml(data: bytes, blocks: list[PssBlock]) -> bytes:
    cursor = max((b.offset + b.size for b in blocks), default=16)
    if data[cursor : cursor + 4] != PACK_MAGIC:
        raise PssError("PACK tail missing")
    (pack_size,) = struct.unpack_from("<I", data, cursor + 4)
    blob = data[cursor + 8 : cursor + 8 + pack_size]
    if len(blob) != pack_size:
        raise PssError("PACK tail truncated")
    with zipfile.ZipFile(io.BytesIO(blob)) as zf:
        names = zf.namelist()
        if not names:
            raise PssError("PACK zip is empty")
        entry = XML_ENTRY if XML_ENTRY in names else names[0]
        return zf.read(entry)


def parse_pss(path: Path | str) -> PssEffect:
    path = Path(path)
    data = path.read_bytes()
    version, header_u16, block_region_end, blocks = _parse_binary(data)
    xml_bytes = _extract_xml(data, blocks)
    warnings: list[str] = []
    try:
        xml = ET.fromstring(xml_bytes)
    except ET.ParseError as exc:
        raise PssError(f"PSEF XML parse failed: {exc}") from exc
    if xml.tag != "DataStorage":
        warnings.append(f"unexpected XML root {xml.tag!r}")
    return PssEffect(
        path=path,
        version=version,
        header_u16=header_u16,
        block_region_end=block_region_end,
        blocks=blocks,
        xml=xml,
        warnings=warnings,
    )


def iter_asset_paths(effect: PssEffect) -> Iterator[str]:
    seen: set[str] = set()
    for value in effect.all_strings():
        low = value.lower()
        if low.endswith(PATH_EXTS) and value not in seen:
            seen.add(value)
            yield value


def main() -> None:
    import argparse

    ap = argparse.ArgumentParser(description="Parse a raw PSS and emit JSON")
    ap.add_argument("pss", type=Path)
    ap.add_argument("--json", type=Path, default=None, help="write JSON here")
    ap.add_argument("--summary", action="store_true", help="omit per-module keyframe trees")
    args = ap.parse_args()
    effect = parse_pss(args.pss)
    payload = effect.to_dict(include_modules=not args.summary)
    text = json.dumps(payload, ensure_ascii=False, indent=1)
    if args.json:
        args.json.write_text(text, encoding="utf-8")
        print(f"wrote {args.json} ({args.json.stat().st_size} bytes)")
    else:
        print(text)


if __name__ == "__main__":
    main()
