"""Copy transparent cast sheets unchanged and register read-only alpha bounds as Godot atlases.

Input: AnimeCast/<key>-compact.png, three rows (front/back/left), six columns
(idle, walk one, walk two, sit transition, seated, talk). No pixel edits occur.
Run: python godot_csharp/tools/build_anime_cast.py [--only detective engineer ...]
Interaction/inspection use the supplied talk pose, not dedicated action drawings.
"""
from __future__ import annotations

import argparse
import shutil
from pathlib import Path

from PIL import Image

GAME = Path(__file__).resolve().parents[1]
SOURCE = GAME / "AssetsSource/Internal/Characters/AnimeCast"
OUTPUT = GAME / "ShareAsset/Internal/Characters/AnimeCast"
HEIGHTS = {
    "detective": 54, "engineer": 60, "chef": 56, "musician": 52,
    "poet": 56, "archaeologist": 57, "thief": 50, "lawyer": 57,
    "naturalist": 53, "chessplayer": 55, "dancer": 54, "painter": 55,
    "chemist": 54, "marksman": 59, "architect": 56, "doctor": 52,
}
# HEIGHTS are relative body heights; the tallest (60) fills a 32x48 cell's rows 0-46.
WORLD_HEIGHT_SCALE = 47 / 60
DIRECTIONS = ("front", "back", "left")
ANIMATIONS = {
    "idle": ([0], True, 4.0),
    "walk": ([1, 0, 2, 0], True, 7.5),
    "sit": ([0, 3, 4], False, 7.0),
    "talk": ([0, 5], True, 4.0),
    "interact": ([0, 5, 0], False, 5.0),
    "inspect": ([0, 5, 0], False, 5.0),
}
ALPHA_BOUND_THRESHOLD = 16


def gutter_cut(mask: Image.Image, axis: str, ideal: float, radius: int) -> int:
    """Register an actual transparent gutter near a nominal grid edge."""
    width, height = mask.size
    limit = height if axis == "y" else width
    runs = []
    start = None
    for position in range(max(1, round(ideal) - radius), min(limit - 1, round(ideal) + radius) + 1):
        strip = ((0, position, width, position + 1) if axis == "y"
                 else (position, 0, position + 1, height))
        empty = mask.crop(strip).getbbox() is None
        if empty and start is None:
            start = position
        if not empty and start is not None:
            runs.append((start, position))
            start = None
    if start is not None:
        runs.append((start, min(limit - 1, round(ideal) + radius) + 1))
    if not runs:
        # The caller's boundary check will report a touching body, never crop it silently.
        return round(ideal)
    begin, end = max(runs, key=lambda run: (run[1] - run[0], -abs((run[0] + run[1]) / 2 - ideal)))
    return (begin + end) // 2


def bounds(path: Path) -> list[list[tuple[int, int, int, int]]]:
    """Only inspect alpha; the original PNG is later copied byte-for-byte."""
    with Image.open(path) as image:
        if "A" not in image.getbands():
            raise ValueError(f"{path.name}: a transparent alpha channel is required")
        alpha = image.getchannel("A")
        if alpha.getextrema()[0] != 0:
            raise ValueError(f"{path.name}: no fully transparent background")
        width, height = image.size
        # Low-alpha generation halos do not define the body bounds. This temporary
        # inspection mask never becomes an asset and does not alter the copied PNG.
        body_alpha = alpha.point(lambda value: 255 if value >= ALPHA_BOUND_THRESHOLD else 0)
        row_edges = [0] + [gutter_cut(body_alpha, "y", height * i / 3, round(height / 3 * 0.18)) for i in (1, 2)] + [height]
        rows = []
        for y in range(3):
            top, bottom = row_edges[y:y + 2]
            row_alpha = body_alpha.crop((0, top, width, bottom))
            # Row-specific column gutters allow independently spaced direction drawings.
            column_edges = [0] + [gutter_cut(row_alpha, "x", width * i / 6, round(width / 6 * 0.22)) for i in range(1, 6)] + [width]
            frames = []
            for x in range(6):
                left, right = column_edges[x:x + 2]
                # Some sheets stagger head/boot heights between columns so no
                # single full-width row gutter exists. Register that column's
                # own transparent vertical gaps instead of clipping its feet.
                column_alpha = body_alpha.crop((left, 0, right, height))
                frame_top = 0 if y == 0 else gutter_cut(column_alpha, "y", height * y / 3, round(height / 3 * 0.18))
                frame_bottom = height if y == 2 else gutter_cut(column_alpha, "y", height * (y + 1) / 3, round(height / 3 * 0.18))
                box = body_alpha.crop((left, frame_top, right, frame_bottom)).getbbox()
                if box is None:
                    raise ValueError(f"{path.name}: empty {DIRECTIONS[y]} column {x}")
                if box[0] == 0 or box[1] == 0 or box[2] == right - left or box[3] == frame_bottom - frame_top:
                    raise ValueError(f"{path.name}: {DIRECTIONS[y]} column {x} touches cell boundary; inspect layout/clipping")
                frames.append((left + box[0], frame_top + box[1], left + box[2], frame_top + box[3]))
            rows.append(frames)
        return rows


def build(key: str) -> None:
    source = SOURCE / f"{key}-compact.png"
    boxes = bounds(source)
    canvas_width = max(b[2] - b[0] for row in boxes for b in row)
    canvas_height = max(b[3] - b[1] for row in boxes for b in row)
    standing_height = max(row[0][3] - row[0][1] for row in boxes)
    scale = HEIGHTS[key] * WORLD_HEIGHT_SCALE / standing_height
    uri = f"res://ShareAsset/Internal/Characters/AnimeCast/{key}.png"
    lines = ['[gd_resource type="SpriteFrames" load_steps=20 format=3]', "",
             f'[ext_resource type="Texture2D" path="{uri}" id="sheet"]', ""]
    for y, row in enumerate(boxes):
        for x, (left, top, right, bottom) in enumerate(row):
            width, height = right - left, bottom - top
            # Atlas margin aligns every pose's opaque bottom to the same world feet.
            margin_x = (canvas_width - width) / 2
            margin_y = canvas_height - height
            lines += [f'[sub_resource type="AtlasTexture" id="frame_{y}_{x}"]',
                      'atlas = ExtResource("sheet")',
                      f"region = Rect2({left}, {top}, {width}, {height})",
                      f"margin = Rect2({margin_x}, {margin_y}, {canvas_width - width}, {canvas_height - height})",
                      "filter_clip = true", ""]
    lines += ["[resource]", f"metadata/visual_scale = {scale:.10f}",
              f"metadata/foot_offset = {-canvas_height * scale / 2:.10f}",
              "metadata/linear_filter = true", "animations = ["]
    animations = []
    for y, direction in enumerate(DIRECTIONS):
        for action, (columns, loop, speed) in ANIMATIONS.items():
            frames = ",\n".join('{"duration": 1.0, "texture": SubResource("frame_%d_%d")}' % (y, x) for x in columns)
            animations.append('{\n"frames": [\n' + frames + '\n],\n'
                              + f'"loop": {str(loop).lower()},\n"name": &"{action}_{direction}",\n"speed": {speed}\n' + '}')
    lines += [",\n".join(animations), "]", ""]
    OUTPUT.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, OUTPUT / f"{key}.png")
    (OUTPUT / f"{key}.tres").write_text("\n".join(lines), encoding="utf-8")
    left, top, right, bottom = boxes[0][0]
    # Front head/shoulders atlas is already a bust; dialogue must not crop it again.
    bust_height = round((bottom - top) * 0.55)
    portrait = f'''[gd_resource type="AtlasTexture" load_steps=2 format=3]

[ext_resource type="Texture2D" path="{uri}" id="sheet"]

[resource]
atlas = ExtResource("sheet")
region = Rect2({left}, {top}, {right - left}, {bust_height})
filter_clip = true
metadata/portrait_bust = true
'''
    (OUTPUT / f"{key}_portrait.tres").write_text(portrait, encoding="utf-8")
    print(f"{key}: registered 18 poses, body {HEIGHTS[key] * WORLD_HEIGHT_SCALE:.1f} world px, scale {scale:.5f}; PNG copied unchanged")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="+", choices=list(HEIGHTS))
    args = parser.parse_args()
    errors = []
    for key in args.only or HEIGHTS:
        try:
            build(key)
        except (ValueError, FileNotFoundError) as error:
            errors.append(str(error))
            print(error)
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
