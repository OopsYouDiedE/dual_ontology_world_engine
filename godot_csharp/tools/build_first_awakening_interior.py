"""Rebuild shared interior textures and their audit layout from native 1x recipes.

The retired FirstAwakening gameplay scene is not generated here. Runtime maps
consume the shared textures; layout.json remains an audit artifact. Nothing
resamples an image: each pixel is placed by tools/first_awakening_art/ at game scale.

Run from the repository root:  python godot_csharp/tools/build_first_awakening_interior.py
"""
import hashlib
import json
import shutil
import sys
from pathlib import Path
from xml.etree import ElementTree

GAME = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(GAME / "tools"))

from first_awakening_art import architecture, layout, placement, props, red_token  # noqa: E402

TARGET = GAME / "ShareAsset/Internal/Props/FirstAwakeningInterior"
SOURCE = GAME / "AssetsSource/Internal/Props/FirstAwakeningInterior"
def collision_rects() -> list[list[int]]:
    """Merge every non-walkable cell of the map into row runs."""
    rects = []
    for y in range(layout.H):
        x = 0
        while x < layout.W:
            if layout.walkable(x, y):
                x += 1
                continue
            start = x
            while x < layout.W and not layout.walkable(x, y):
                x += 1
            rects.append([start * layout.CELL, y * layout.CELL, (x - start) * layout.CELL, layout.CELL])
    return rects


def render() -> tuple[dict, dict, dict]:
    """Render in memory so CLI and MCP share one recipe and can verify before writing."""
    wing, anchors = architecture.build()
    architecture.bake_floor_work(wing, placement.FURNITURE, placement.RUGS, placement.SHOES)
    sprites = props.build()
    data = {
        "cell": layout.CELL,
        "size": [layout.W, layout.H],
        "rows": layout.ROWS,
        "solids": collision_rects(),
        "furniture": placement.FURNITURE,
        "gate": placement.GATE,
        "stop": placement.STOP,
        "lights": placement.LIGHTS,
        "windows": anchors["windows"],
        "sconces": anchors["sconces"],
        "dorm_doors": [[x + 32, 384] for x in architecture.DORM_DOORS],
    }
    manifest = {
        "process": "native 1x pixel recipes in tools/first_awakening_art and authored native-size SVG copied from AssetsSource; no resampling",
        "entry": "tools/build_first_awakening_interior.py",
        "anchor": "bottom-centre of each sprite is its floor contact point",
        "architecture": {"file": "life_wing.png", "size": [wing.w, wing.h]},
        "sprites": {name: {"size": [c.w, c.h]} for name, c in sprites.items()},
    }
    # The bell and plaque are an authored integer-pixel SVG, not a generated PNG.
    # Record the bytes of the actual production source before publishing its copy.
    plaque_source = SOURCE / "alarm_regulations.svg"
    plaque_bytes = plaque_source.read_bytes()
    plaque_root = ElementTree.fromstring(plaque_bytes)
    if (plaque_root.get("width"), plaque_root.get("height")) != ("32", "48"):
        raise ValueError(f"Alarm regulations sprite must retain its native 32x48 canvas: {plaque_source}")
    manifest["sprites"]["alarm_regulations"] = {
        "file": "alarm_regulations.svg",
        "size": [32, 48],
        "source": plaque_source.relative_to(GAME).as_posix(),
        "runtime": (TARGET / "alarm_regulations.svg").relative_to(GAME).as_posix(),
        "process": "authored native 1x integer-pixel SVG; byte-for-byte source copy",
        "sha256": hashlib.sha256(plaque_bytes).hexdigest(),
        "anchor": "top-left; centered=false",
        "position": [536, 962],
    }
    return {"life_wing": wing, **sprites}, data, manifest


def main() -> None:
    canvases, data, manifest = render()
    manifest["red_token_mechanisms"] = red_token.publish(GAME)
    TARGET.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(SOURCE / "alarm_regulations.svg", TARGET / "alarm_regulations.svg")
    for name, canvas in canvases.items():
        canvas.save(TARGET / f"{name}.png")
    (TARGET / "layout.json").write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    (TARGET / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"sprites": len(manifest["sprites"]), "solids": len(data["solids"]), "furniture": len(placement.FURNITURE)}))


if __name__ == "__main__":
    main()
