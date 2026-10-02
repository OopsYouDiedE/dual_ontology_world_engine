"""Publish the revised fixtures without rewriting scene topology or layout.

Run from the repository root:
    python godot_csharp/tools/redraw_first_awakening_props.py

The editable 1x recipes are first_awakening_art/red_token.py and props.py.
Production PNGs and runtime PNGs receive the same native pixels. This entry
does not invoke Godot, the full interior builder or any inspection workflow.
"""
from pathlib import Path

from PIL import Image, ImageDraw

from first_awakening_art import props, red_token


GAME = Path(__file__).resolve().parents[1]
FAMILY = "Internal/Props/FirstAwakeningInterior"


def publish() -> None:
    mechanisms = red_token.build()
    names = [
        "reward_cabinet", "return_box", "spring_handle", "spring_handle_pressed",
        "filter_basin", "dual_inlet", "capture_plate", "inspect_basin",
        "inspect_box", "inspect_box_received", "inspect_box_stamped", "inspect_box_white",
    ]
    canvases = {"red_" + name: mechanisms[name] for name in names}
    canvases["transmitter"] = props.transmitter()
    for name, canvas in canvases.items():
        for root in ("AssetsSource", "ShareAsset"):
            canvas.save(GAME / root / FAMILY / (name + ".png"))

    # Native contact sheet is an art deliverable, with no resampling in assets.
    sheet = Image.new("RGBA", (560, 560), (224, 217, 200, 255))
    draw = ImageDraw.Draw(sheet)
    for index, (name, canvas) in enumerate(canvases.items()):
        x, y = (index % 4) * 140, (index // 4) * 140
        draw.text((x + 6, y + 5), name.removeprefix("red_"), fill=(48, 43, 39, 255))
        sheet.alpha_composite(canvas.image(), (x + (140 - canvas.w) // 2, y + 24))
    output = GAME.parent / "artifacts/first-awakening"
    output.mkdir(parents=True, exist_ok=True)
    sheet.save(output / "red-token-props-v3-native.png")
    sheet.resize((1120, 1120), Image.Resampling.NEAREST).save(output / "red-token-props-v3-2x.png")
    print("Published 13 native fixture textures and two art contact sheets.")


if __name__ == "__main__":
    publish()
