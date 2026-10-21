"""Generate table art from the existing native pixel recipes, keeping footprints.

This writes production art and manifest hashes; it does not run Godot or a session.
"""
from pathlib import Path
import hashlib
import json
from first_awakening_art import props
from chapter2_art import props_core

GAME = (Path(__file__).resolve().parents[2] / "godot_csharp")


def main():
    sources = [
        ("FirstAwakeningInterior", {"study_table": props.study_table(), "worktable": props.worktable()}),
        ("Chapter2", {"long_table": props_core.long_table()}),
    ]
    for directory, canvases in sources:
        root = GAME / "ShareAsset/Internal/Props" / directory
        manifest_path = root / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        for name, canvas in canvases.items():
            target = root / ("props" if directory == "Chapter2" else "") / f"{name}.png"
            canvas.save(target)
            # Keep the manifest's existing schema and placement metadata.
            table = manifest["props"] if directory == "Chapter2" else manifest["sprites"]
            if "sha256" in table[name]:
                table[name]["sha256"] = hashlib.sha256(target.read_bytes()).hexdigest()
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=1 if directory == "Chapter2" else 2), encoding="utf-8")


if __name__ == "__main__":
    main()
