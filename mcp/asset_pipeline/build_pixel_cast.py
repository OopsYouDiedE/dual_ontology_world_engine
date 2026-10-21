"""Pixelize an AnimeCast sheet into a native 32x48 action atlas on the scene's pixel grid.

The GPT-generated `AssetsSource/Internal/Characters/AnimeCast/<key>-compact.png` stays the
design master (same 3 rows front/back/left x 6 columns idle/walk1/walk2/half-sit/seated/talk).
Each pose is resampled at one shared scale by block majority over a hand-picked material
palette (no averaging, so no in-between colours), given a 1px dark silhouette outline, and
gets 1x2 pupils stamped where the master's irises are. Alpha is binary, every pose's
lowest pixel sits on row 46 of its 32x48 cell, and the runtime draws it at 1:1 with nearest
filtering, so character pixels match the furniture pixels.

Each character's material palette lives in AssetsSource/Internal/Characters/PixelCast/<key>.json:
  {"palette": {"line": [r,g,b], ...ramps...}, "skin": ["skin0", "skin1"],
   "pupil": [r,g,b],                                   # optional, lower pupil pixel
   "iris": {"color": [r,g,b], "tolerance": 18},         # optional; default finds warm brown irises
   "eye_pair_dy": 4,                                    # optional; max master-px height gap of a front eye pair
   "accents": {"gold0": 0.15},                          # optional; thin detail colours win at this block share
   "closed_eyes": ["front:4", "left:4"] or "all",       # optional; draw a 2px lid line instead of pupils
   "cell": [40, 56]}                                    # optional; wide/tall casts cross the 32x48 base
"line" must come first (ink colour; also used for the outline). Skin entries drive eye search.

Run: python mcp/asset_pipeline/build_pixel_cast.py [--only detective]
Writes ShareAsset/Internal/Characters/PixelCast/<key>.png/.tres and a 6x preview under
artifacts/pixel-cast/.
"""
from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from pathlib import Path

from asset_sources import require_originals

from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_anime_cast import ANIMATIONS, DIRECTIONS, HEIGHTS, WORLD_HEIGHT_SCALE, bounds  # noqa: E402

GAME = (Path(__file__).resolve().parents[2] / "godot_csharp")
SOURCE = GAME.parent / "mcp/asset_pipeline/sources/Internal/Characters/AnimeCast"
PALETTES = GAME.parent / "mcp/asset_pipeline/sources/Internal/Characters/PixelCast"
OUTPUT = GAME / "ShareAsset/Internal/Characters/PixelCast"
PREVIEW = GAME.parent / "artifacts/pixel-cast"
BASE_CELL = (32, 48)
DEFAULT_PUPIL = (96, 58, 40)


def load_spec(key: str) -> dict:
    spec = json.loads((PALETTES / f"{key}.json").read_text(encoding="utf-8"))
    palette = {name: tuple(rgb) for name, rgb in spec["palette"].items()}
    if next(iter(palette)) != "line":
        raise ValueError(f"{key}: palette must start with \"line\"")
    missing = [name for name in spec["skin"] if name not in palette]
    if missing:
        raise ValueError(f"{key}: skin entries {missing} not in palette")
    spec["palette"] = palette
    return spec


def lab(c: tuple[int, int, int]) -> tuple[float, float, float]:
    def linear(u: float) -> float:
        u /= 255
        return u / 12.92 if u <= 0.04045 else ((u + 0.055) / 1.055) ** 2.4
    r, g, b = (linear(v) for v in c[:3])
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.9505
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.089
    f = lambda t: t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116  # noqa: E731
    fx, fy, fz = f(x), f(y), f(z)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


def luma(c) -> float:
    return 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]


class Master:
    def __init__(self, key: str, source: Path = SOURCE):
        path = source / f"{key}-compact.png"
        self.image = Image.open(path).convert("RGBA")
        self.boxes = bounds(path)
        self.alpha = self.image.getchannel("A").load()
        self.raw = self.image.load()
        flat = Image.new("RGB", self.image.size, (0, 0, 0))
        flat.paste(self.image, mask=self.image.getchannel("A"))
        # Median removes the master's fabric grain before palette mapping.
        self.smooth = flat.filter(ImageFilter.MedianFilter(5)).load()
        spec = load_spec(key)
        palette = spec["palette"]
        self.names = list(palette)
        self.colors = list(palette.values())
        self.outline = self.colors[0]
        self.pupil = tuple(spec.get("pupil", DEFAULT_PUPIL))
        self.eye_pair_dy = spec.get("eye_pair_dy", 4)
        self.accents = {self.names.index(name): share for name, share in spec.get("accents", {}).items()}
        self.closed_eyes = spec.get("closed_eyes", [])
        self.skin_ids = {self.names.index(name) for name in spec["skin"]}
        iris = spec.get("iris")
        self.iris_lab = (lab(tuple(iris["color"])), iris.get("tolerance", 18)) if iris else None
        self.labs = [lab(c) for c in self.colors]
        self.cache: dict[tuple[int, int, int], int] = {}
        skin = Image.new("L", self.image.size, 0)
        sk = skin.load()
        for y in range(self.image.height):
            for x in range(self.image.width):
                c = self.raw[x, y]
                if c[3] >= 128 and self.nearest(c) in self.skin_ids:
                    sk[x, y] = 255
        self.skin_share = skin.filter(ImageFilter.BoxBlur(9)).load()
        standing = max(row[0][3] - row[0][1] for row in self.boxes)
        self.scale = round(HEIGHTS[key] * WORLD_HEIGHT_SCALE) / standing
        self.cell_w, self.cell_h = spec.get("cell", BASE_CELL)
        self.foot_row = self.cell_h - 2

    def nearest(self, c) -> int:
        k = (c[0], c[1], c[2])
        if k not in self.cache:
            L = lab(c)
            # Chroma weighs extra so olive vest, navy trousers and brown coat stay apart.
            self.cache[k] = min(range(len(self.colors)), key=lambda i: (L[0] - self.labs[i][0]) ** 2
                                + 2.5 * ((L[1] - self.labs[i][1]) ** 2 + (L[2] - self.labs[i][2]) ** 2))
        return self.cache[k]

    def irises(self, l: int, t: int, r: int, b: int) -> list[tuple[float, float, int]]:
        """Warm mid-brown blobs inside skin in the upper half: eyes, plus ears/hands/mouth."""
        mask = {(x, y) for y in range(t, t + (b - t) // 2) for x in range(l, r)
                if self.alpha[x, y] >= 200 and self.skin_share[x, y] > 70 and self.is_iris(self.raw[x, y])}
        blobs = []
        while mask:
            stack, points = [mask.pop()], []
            while stack:
                p = stack.pop()
                points.append(p)
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        n = (p[0] + dx, p[1] + dy)
                        if n in mask:
                            mask.remove(n)
                            stack.append(n)
            if len(points) >= 15:
                blobs.append((sum(p[0] for p in points) / len(points), sum(p[1] for p in points) / len(points), len(points)))
        return blobs

    def is_iris(self, c) -> bool:
        if self.iris_lab is None:
            return 60 < luma(c) < 150 and c[0] > 110 and c[0] - c[2] > 45
        (L, A, B), tolerance = self.iris_lab
        l2, a2, b2 = lab(c[:3])
        return ((L - l2) ** 2 + (A - a2) ** 2 + (B - b2) ** 2) ** 0.5 < tolerance

    def eyes(self, direction: str, box) -> list[tuple[float, float]]:
        blobs = self.irises(*box)
        if direction == "back" or not blobs:
            return []
        if direction == "front":
            pairs = [(a[2] + b[2], a, b) for a in blobs for b in blobs
                     if a[0] < b[0] and abs(a[1] - b[1]) < self.eye_pair_dy and 25 < b[0] - a[0] < 50]
            if not pairs:
                return []
            _, a, b = max(pairs)
            return [a[:2], b[:2]]
        # Left profile: the eye is the foremost (leftmost) blob near the top of the head;
        # ears sit behind it and a gesturing hand sits lower.
        top = min(blob[1] for blob in blobs)
        head = [blob for blob in blobs if blob[1] < top + 20]
        return [min(head)[:2]]

    def pose(self, direction: str, column: int, box) -> Image.Image:
        l, t, r, b = box
        s = self.scale
        w, h = round((r - l) * s), round((b - t) * s)
        out = Image.new("RGBA", (w, h))
        op = out.load()
        skin_ids = self.skin_ids
        for y in range(h):
            for x in range(w):
                votes, total, opaque = Counter(), 0, 0
                for yy in range(int(t + y / s), int(t + (y + 1) / s)):
                    for xx in range(int(l + x / s), int(l + (x + 1) / s)):
                        total += 1
                        if self.alpha[xx, yy] >= 128:
                            opaque += 1
                            votes[self.nearest(self.smooth[xx, yy])] += 1
                if opaque * 2 < total:
                    continue
                pick = votes.most_common(1)[0][0]
                # Thin identity details (piping, glasses, buttons) survive at their configured share.
                accent = max(((votes[i] / opaque / share, i) for i, share in self.accents.items()
                              if votes[i] >= share * opaque), default=None)
                if accent is not None:
                    pick = accent[1]
                # Thin ink lines survive when they cover a fifth of the block (not across skin).
                elif votes[0] >= 0.2 * opaque and pick not in skin_ids and luma(self.colors[pick]) > 45:
                    pick = 0
                op[x, y] = self.colors[pick] + (255,)
        closed = self.closed_eyes == "all" or f"{direction}:{column}" in self.closed_eyes
        for ex, ey in self.eyes(direction, box):
            x, y = int((ex - l) * s), int((ey - t) * s - 0.5)
            # Open: ink over pupil (1x2). Closed: a 2px lid line on the pupil row.
            marks = (((x, y + 1), self.outline), ((x + (-1 if direction == "left" else 1), y + 1), self.outline))                 if closed else (((x, y), self.outline), ((x, y + 1), self.pupil))
            for (px, py), colour in marks:
                if 0 <= px < w and 0 <= py < h and op[px, py][3]:
                    op[px, py] = colour + (255,)
        return out


def outline(sheet: Image.Image, ink: tuple[int, int, int]) -> None:
    px = sheet.load()
    w, h = sheet.size
    edge = [(x, y) for y in range(h) for x in range(w) if px[x, y][3] and any(
        not (0 <= x + dx < w and 0 <= y + dy < h) or not px[x + dx, y + dy][3]
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for x, y in edge:
        px[x, y] = ink + (255,)


def frames_resource(key: str, cell_w: int, cell_h: int) -> str:
    uri = f"res://ShareAsset/Internal/Characters/PixelCast/{key}.png"
    lines = ['[gd_resource type="SpriteFrames" load_steps=20 format=3]', "",
             f'[ext_resource type="Texture2D" path="{uri}" id="sheet"]', ""]
    for y in range(3):
        for x in range(6):
            lines += [f'[sub_resource type="AtlasTexture" id="frame_{y}_{x}"]', 'atlas = ExtResource("sheet")',
                      f"region = Rect2({x * cell_w}, {y * cell_h}, {cell_w}, {cell_h})", ""]
    # Centered cell: the bottom edge of the foot row (cell_h - 2) is cell_h/2 - 1 below the centre.
    lines += ["[resource]", "metadata/visual_scale = 1.0",
              f"metadata/foot_offset = {-(cell_h - 1 - cell_h / 2)}", "metadata/linear_filter = false",
              "animations = ["]
    animations = []
    for y, direction in enumerate(DIRECTIONS):
        for action, (columns, loop, speed) in ANIMATIONS.items():
            frames = ",\n".join('{\n"duration": 1.0,\n"texture": SubResource("frame_%d_%d")\n}' % (y, c) for c in columns)
            animations.append('{\n"frames": [' + frames + '\n],\n'
                              + f'"loop": {str(loop).lower()},\n"name": &"{action}_{direction}",\n"speed": {speed}\n' + '}')
    lines += [",\n".join(animations), "]", ""]
    return "\n".join(lines)


def build(key: str, source: Path = SOURCE, preview: Path = PREVIEW) -> None:
    require_originals([source / f"{key}-compact.png", PALETTES / f"{key}.json"], "PixelCast master and palette")
    master = Master(key, source)
    cw, ch, foot = master.cell_w, master.cell_h, master.foot_row
    sheet = Image.new("RGBA", (cw * 6, ch * 3))
    for y, (direction, row) in enumerate(zip(DIRECTIONS, master.boxes)):
        for x, box in enumerate(row):
            pose = master.pose(direction, x, box)
            if pose.width > cw - 2 or pose.height > foot + 1:
                raise ValueError(f"{key} {direction} column {x}: {pose.size} exceeds the {cw}x{ch} cell; set \"cell\"")
            sheet.alpha_composite(pose, (x * cw + (cw - pose.width) // 2, y * ch + foot + 1 - pose.height))
    outline(sheet, master.outline)
    pixels = list(sheet.get_flattened_data() if hasattr(sheet, "get_flattened_data") else sheet.getdata())
    colours = {c for c in pixels if c[3]}
    assert {c[3] for c in pixels} <= {0, 255}
    OUTPUT.mkdir(parents=True, exist_ok=True)
    preview.mkdir(parents=True, exist_ok=True)
    sheet.save(OUTPUT / f"{key}.png")
    (OUTPUT / f"{key}.tres").write_text(frames_resource(key, cw, ch), encoding="utf-8")
    review = Image.new("RGBA", sheet.size, (150, 150, 150, 255))
    review.alpha_composite(sheet)
    review.resize((sheet.width * 6, sheet.height * 6), Image.NEAREST).save(preview / f"{key}-x6.png")
    print(f"{key}: 18 poses in {cw}x{ch} cells, standing {round(HEIGHTS[key] * WORLD_HEIGHT_SCALE)}px, {len(colours)} colours")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="+", choices=list(HEIGHTS))
    parser.add_argument("--source-dir", type=Path, default=SOURCE, help="Versioned ImageGen masters; same 3x6 layout")
    parser.add_argument("--preview-dir", type=Path, default=PREVIEW)
    args = parser.parse_args()
    keys = args.only or sorted(path.stem for path in PALETTES.glob("*.json"))
    require_originals([path for key in keys
                       for path in (args.source_dir / f"{key}-compact.png", PALETTES / f"{key}.json")],
                      "PixelCast master and palette")
    for key in keys:
        build(key, args.source_dir, args.preview_dir)


if __name__ == "__main__":
    main()
