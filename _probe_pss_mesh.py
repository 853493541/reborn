"""Probe extracted PSS .Mesh files with our own mesh.py parser."""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))


def main() -> None:
    from mesh import load_mesh  # type: ignore

    files = list(Path("assets/sfx/extract").rglob("*.Mesh"))
    print(f"{len(files)} mesh files")
    for p in files:
        try:
            m = load_mesh(p)
            print(
                f"  OK {p.name}: verts={m.vertex_count} faces={m.face_count} bones={len(m.bones)}"
            )
        except Exception as exc:
            data = p.read_bytes()
            print(f"  FAIL {p.name} bytes={len(data)} head={data[:16].hex()} err={exc}")


if __name__ == "__main__":
    main()
