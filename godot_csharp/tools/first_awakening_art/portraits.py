"""Dialogue portraits for the twelve candidates without painted busts.

Each portrait is the front idle frame of the native pixel atlas
(animation_v2/<key>.png, cell row 0 column 0), cropped to head and
shoulders and enlarged with NEAREST at an integer factor so the pixels
stay crisp. The canvas matches the painted four (1024x1536); the dialogue
box shows only the bust region Rect2(0.2, 0.02, 0.6, 0.4), i.e.
x 205-819, y 31-645, so the figure is fitted into that square: head one
row below its top edge, shoulders reaching its bottom edge, centred on the
figure's own pixel bounds. The background is a quiet vertical gradient
tinted by the character's coat colour toward the charcoal of the painted
set.

Run from godot_csharp/tools:  python -m first_awakening_art.portraits
"""
from __future__ import annotations

import colorsys
from pathlib import Path

import numpy as np
from PIL import Image

from .cast import CELL, LOOKS

KEYS = ("poet", "archaeologist", "thief", "lawyer", "naturalist", "chessplayer",
        "dancer", "painter", "chemist", "marksman", "architect", "doctor")

W, H = 1024, 1536
BUST = (205, 31, 819, 645)       # Rect2(0.2, 0.02, 0.6, 0.4) in pixels
BUST_ROWS = 23                   # figure rows shown: hair top to upper chest
MARGIN_ROWS = 1                  # empty rows above the hair


def _hex(c: str) -> tuple[float, float, float]:
    c = c.lstrip("#")
    return tuple(int(c[i:i + 2], 16) / 255 for i in (0, 2, 4))


def background(key: str) -> Image.Image:
    """Dark desaturated coat tint, lighter behind the head, darker below."""
    r, g, b = _hex(LOOKS[key].mats["coat"])
    h, s, _ = colorsys.rgb_to_hsv(r, g, b)
    tint = np.array(colorsys.hsv_to_rgb(h, min(s, 0.6) * 0.45, 1.0))
    neutral = np.array([0.97, 0.97, 1.0])        # charcoal hue of the painted set
    ys = np.linspace(0.0, 1.0, H)[:, None, None]
    xs = np.linspace(-1.0, 1.0, W)[None, :, None]
    # value falls from ~0.21 at the top to ~0.10 at the foot
    value = 0.21 - 0.11 * ys
    # soft glow centred behind the head in the bust region
    gy = (np.linspace(0, H, H)[:, None, None] - 260) / 520
    glow = 0.045 * np.exp(-(xs ** 2 / 0.35 + gy ** 2))
    base = neutral * 0.55 + tint * 0.45
    col = (base / base.max()) * (value + glow)
    arr = np.clip(col * 255, 0, 255).astype(np.uint8)
    arr = np.broadcast_to(arr, (H, W, 3))
    alpha = np.full((H, W, 1), 255, np.uint8)
    return Image.fromarray(np.concatenate([arr, alpha], axis=2), "RGBA")


def bust_crop(key: str, atlas_dir: Path) -> Image.Image:
    atlas = Image.open(atlas_dir / f"{key}.png").convert("RGBA")
    cell = atlas.crop((0, 0, CELL, CELL))
    alpha = np.array(cell)[:, :, 3]
    top = int(np.nonzero(alpha.any(axis=1))[0][0])
    rows = cell.crop((0, top, CELL, top + BUST_ROWS))
    return rows


def portrait(key: str, atlas_dir: Path) -> Image.Image:
    rows = bust_crop(key, atlas_dir)
    bw, bh = BUST[2] - BUST[0], BUST[3] - BUST[1]
    scale = bh // (BUST_ROWS + MARGIN_ROWS)
    alpha = np.array(rows)[:, :, 3]
    xs = np.nonzero(alpha.any(axis=0))[0]
    centre = (xs[0] + xs[-1] + 1) / 2            # in cell pixels
    big = rows.resize((rows.width * scale, rows.height * scale), Image.NEAREST)
    ox = round(BUST[0] + bw / 2 - centre * scale)
    oy = BUST[3] - big.height                    # shoulders meet the bottom edge
    canvas = background(key)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    layer.paste(big, (ox, oy))
    canvas.alpha_composite(layer)
    return canvas


def publish(game: Path, keys=KEYS) -> list[Path]:
    atlas_dir = game / "ShareAsset/Internal/Characters/animation_v2"
    runtime = game / "ShareAsset/Internal/Characters/FirstAwakeningPortraits"
    source = game / "AssetsSource/Internal/Characters/FirstAwakeningPortraits"
    runtime.mkdir(parents=True, exist_ok=True)
    source.mkdir(parents=True, exist_ok=True)
    out = []
    for key in keys:
        im = portrait(key, atlas_dir)
        for d in (source, runtime):
            im.save(d / f"{key}.png", optimize=True)
        out.append(runtime / f"{key}.png")
        print(f"{key}: portrait published")
    return out


def contact_sheet(paths: list[Path], dest: Path, size: int = 172) -> None:
    sheet = Image.new("RGBA", (size * len(paths), size), (0, 0, 0, 255))
    for i, p in enumerate(paths):
        tile = Image.open(p).convert("RGBA").crop(BUST).resize((size, size), Image.LANCZOS)
        sheet.paste(tile, (i * size, 0))
    sheet.save(dest)


if __name__ == "__main__":
    publish(Path(__file__).resolve().parents[2])
