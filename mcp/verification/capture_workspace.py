"""Shared launch preparation and source manifests for rendered capture tools."""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "mcp"))
from godot_workspace import prepare, build, import_assets


def prepare_capture(godot: Path, output: Path) -> Path:
    work = prepare(csharp=True)
    build(work)
    import_assets(work, godot, log_path=output / "asset-import.log")
    return work


def fingerprint(work: Path) -> dict[str, str]:
    names = subprocess.check_output(
        ["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard",
         "godot_csharp", "dv_core", "mcp/verification", "mcp/build", "mcp/godot_workspace.py"],
        cwd=ROOT).decode().split("\0")
    extensions = {".gd", ".gdshader", ".cs", ".csproj", ".sln", ".props", ".targets",
                  ".tscn", ".tres", ".dv", ".py", ".godot", ".cfg", ".json", ".uid",
                  ".png", ".webp", ".glb", ".ttf", ".otf", ".ogg", ".wav", ".translation",
                  ".po", ".svg", ".jpg", ".jpeg", ".res", ".import"}
    paths = {ROOT / name for name in names if name and Path(name).suffix.lower() in extensions}
    paths.update((work / ".godot/mono/temp/bin/Debug").glob("*"))
    paths.update((work / ".godot/imported").glob("*"))
    paths.update((work / "DuelView.Tools.csproj", work / "project.godot", work / "NuGet.Config"))
    for extension in ("*.ogg", "*.wav"):
        paths.update((ROOT / "godot_csharp/ShareAsset").rglob(extension))
    result = {}
    for path in sorted(paths):
        if path.is_file():
            digest = hashlib.sha256()
            with path.open("rb") as source:
                for chunk in iter(lambda: source.read(1024 * 1024), b""):
                    digest.update(chunk)
            result[path.relative_to(ROOT).as_posix()] = digest.hexdigest()
    return result


def user_sessions() -> Path:
    if os.name == "nt":
        data = Path(os.environ["APPDATA"]) / "Godot"
    elif sys.platform == "darwin":
        data = Path.home() / "Library/Application Support/Godot"
    else:
        data = Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share"))) / "godot"
    return data / "app_userdata/Dual Ontology World Engine/dv-sessions"
