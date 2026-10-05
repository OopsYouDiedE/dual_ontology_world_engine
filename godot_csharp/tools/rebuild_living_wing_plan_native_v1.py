"""Rebuild the native living-wing structural plan from its retained draw_asset recipe.
Run: python godot_csharp/tools/rebuild_living_wing_plan_native_v1.py
Only native rect/line/point pixels are rendered; painted overview images are unrelated.
"""
from pathlib import Path
import hashlib
import io
import json
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
RECIPE = ROOT / "godot_csharp/AssetsSource/Internal/Props/MapOptimization/living_wing_plan_native_v1.drawing.json"
EXPECTED_SHA256 = "992faa8ba6c85affc988671821ed7f3b1509906f63a34bb24c08cd36b25cf69c"
DESTINATIONS = (
    ROOT / "godot_csharp/AssetsSource/Internal/Props/MapOptimization/living_wing_plan_native_v1.png",
    ROOT / "artifacts/map-optimization/living_wing_plan_native_v1.png",
)

def main():
    recipe = json.loads(RECIPE.read_text(encoding="utf-8"))
    canvas = Image.new("RGBA", (recipe["width"], recipe["height"]), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    for command in recipe["commands"]:
        color = recipe["palette"][command["color"]]
        xy = tuple(command["xy"])
        if command["op"] == "rect":
            draw.rectangle(xy, fill=color)
        elif command["op"] == "line":
            draw.line(xy, fill=color, width=1)
        elif command["op"] == "point":
            draw.point(xy, fill=color)
        else:
            raise ValueError(command["op"])
    stream = io.BytesIO()
    canvas.save(stream, format="PNG")
    content = stream.getvalue()
    digest = hashlib.sha256(content).hexdigest()
    assert digest == EXPECTED_SHA256, f"Rebuilt PNG differs: {digest}"
    # Check every existing destination before writing either output.
    for destination in DESTINATIONS:
        if destination.exists():
            assert hashlib.sha256(destination.read_bytes()).hexdigest() == digest, (
                f"Existing destination differs; preserved: {destination}"
            )
    for destination in DESTINATIONS:
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists():
            destination.write_bytes(content)
        print(f"{destination}: {digest}")

if __name__ == "__main__":
    main()

