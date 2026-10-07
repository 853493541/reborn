"""V3: client install root resolution (frozen isolated install vs live install).

The V3 track runs the real client from the frozen install copy (JX3ZHENCHUAN). Every
runtime tool must resolve the client root through this module so the frozen and live
installs never get mixed (in particular: the server stubs derive the transport cipher
table from the client exe - it must come from the exact build being driven).

Set one of:
  RC_CLIENT_ROOT=C:\\JX3ZHENCHUAN\\Game\\JX3\\bin\\zhcn_hd   (client root directly)
  RC_V3_ROOT=C:\\JX3ZHENCHUAN                               (outer root; suffix appended)
Unset -> the live install root (V2 behavior, unchanged).
"""
from __future__ import annotations

import os

DEFAULT_CLIENT_ROOT = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd"


def _env_root():
    v = os.environ.get("RC_CLIENT_ROOT") or os.environ.get("RC_V3_ROOT")
    if not v:
        return None
    v = v.rstrip("\\/")
    if os.path.basename(v).lower() == "zhcn_hd":
        return v
    return os.path.join(v, "Game", "JX3", "bin", "zhcn_hd")


def root():
    """Client root (the directory that holds bin64/, logs/, clientconfig.ini, ...)."""
    return _env_root() or DEFAULT_CLIENT_ROOT


def exe():
    return os.path.join(root(), "bin64", "JX3ClientX64.exe")


def logs_dir():
    return os.path.join(root(), "logs")


def minidump_dir():
    return os.path.join(root(), "bin64", "minidump")


def pak_dir():
    """Pak store (clientconfig.ini: PakDir=../../PakV4 relative to the client root)."""
    return os.path.normpath(os.path.join(root(), "..", "..", "Pakv4"))
