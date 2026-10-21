"""Register normal-proportion upper-body mood portraits without changing PNG pixels.

Input: AssetsSource/Internal/Characters/DialogueCast/<key>-sheet.png,
two rows x three columns: calm/thoughtful/worried, angry/warm/hooked.
Output: unchanged runtime PNG and six already-framed AtlasTexture resources.
Run: python mcp/asset_pipeline/build_dialogue_cast.py [--only detective ...]
"""
from __future__ import annotations

import argparse
import shutil
from pathlib import Path

from asset_sources import require_originals

from PIL import Image
from build_anime_cast import ALPHA_BOUND_THRESHOLD, HEIGHTS, gutter_cut

GAME = (Path(__file__).resolve().parents[2] / "godot_csharp")
SOURCE = GAME.parent / "mcp/asset_pipeline/sources/Internal/Characters/DialogueCast"
OUTPUT = GAME / "ShareAsset/Internal/Characters/DialogueCast"
MOODS = ("calm", "thoughtful", "worried", "angry", "warm", "hooked")


def build(key: str) -> None:
    path = SOURCE / f"{key}-sheet.png"
    require_originals([path], "DialogueCast")
    with Image.open(path) as image:
        if "A" not in image.getbands():
            raise ValueError(f"{path.name}: transparent alpha required")
        alpha = image.getchannel("A")
        pixels = list(alpha.getdata())
        transparent_fraction = sum(value < ALPHA_BOUND_THRESHOLD for value in pixels) / len(pixels)
        if transparent_fraction < 0.20:
            raise ValueError(f"{path.name}: only {transparent_fraction:.1%} transparent area; background removal required")
        width, height = image.size
        border_values = []
        for strip in ((0, 0, width, 2), (0, height - 2, width, height), (0, 0, 2, height), (width - 2, 0, width, height)):
            border_values.extend(alpha.crop(strip).getdata())
        border_clear = sum(value < ALPHA_BOUND_THRESHOLD for value in border_values) / len(border_values)
        if border_clear < 0.98:
            raise ValueError(f"{path.name}: border only {border_clear:.1%} transparent; inspect opaque background or clipped gestures")
        mask = alpha.point(lambda value: 255 if value >= ALPHA_BOUND_THRESHOLD else 0)
        middle = gutter_cut(mask, "y", height / 2, round(height / 2 * 0.18))
        boxes = []
        for row in range(2):
            top, bottom = (0, middle) if row == 0 else (middle, height)
            row_mask = mask.crop((0, top, width, bottom))
            edges = [0] + [gutter_cut(row_mask, "x", width * i / 3, round(width / 3 * 0.22)) for i in (1, 2)] + [width]
            for column in range(3):
                left, right = edges[column:column + 2]
                column_mask = mask.crop((left, 0, right, height))
                split = gutter_cut(column_mask, "y", height / 2, round(height / 2 * 0.18))
                frame_top, frame_bottom = (0, split) if row == 0 else (split, height)
                box = mask.crop((left, frame_top, right, frame_bottom)).getbbox()
                mood = MOODS[row * 3 + column]
                if box is None:
                    raise ValueError(f"{path.name}: empty {mood}")
                if box[0] == 0 or box[1] == 0 or box[2] == right - left or box[3] == frame_bottom - frame_top:
                    raise ValueError(f"{path.name}: {mood} touches boundary; inspect source spacing")
                boxes.append((left + box[0], frame_top + box[1], left + box[2], frame_top + box[3]))
    # Common canvas keeps shoulder placement steady when the expression changes.
    canvas_width = max(right - left for left, top, right, bottom in boxes)
    canvas_height = max(bottom - top for left, top, right, bottom in boxes)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, OUTPUT / path.name)
    uri = f"res://ShareAsset/Internal/Characters/DialogueCast/{path.name}"
    for mood, (left, top, right, bottom) in zip(MOODS, boxes):
        width, height = right - left, bottom - top
        resource = f'''[gd_resource type="AtlasTexture" load_steps=2 format=3]

[ext_resource type="Texture2D" path="{uri}" id="sheet"]

[resource]
atlas = ExtResource("sheet")
region = Rect2({left}, {top}, {width}, {height})
margin = Rect2({(canvas_width - width) / 2}, {canvas_height - height}, {canvas_width - width}, {canvas_height - height})
filter_clip = true
metadata/portrait_bust = true
'''
        (OUTPUT / f"{key}_{mood}.tres").write_text(resource, encoding="utf-8")
    print(f"{key}: six upper-body moods registered; source PNG copied unchanged")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="+", choices=list(HEIGHTS))
    args = parser.parse_args()
    keys = list(args.only or HEIGHTS)
    require_originals((SOURCE / f"{key}-sheet.png" for key in keys), "DialogueCast")
    errors = []
    for key in keys:
        try:
            build(key)
        except (ValueError, FileNotFoundError) as error:
            errors.append(str(error))
            print(error)
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
