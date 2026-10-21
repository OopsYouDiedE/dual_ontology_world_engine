"""生成界面纸面的像素纸纹（设计规范第六部分 §4）。

128×128 可平铺原生像素：纸色底上的低对比 4×4 有序抖色（随低频噪声起伏）、零星纤维与霉点。
输出按最近邻放大 2 倍（与地图的显示倍率一致），供主题里的 StyleBoxTexture 平铺使用。随机种子固定，可重建。

    paper_pixel.png   纸面（弹窗、抬头、对白条）
    paper_ruled.png   册子横格页：同一纸纹，每 16 原生像素（显示 32 px，即一个地图格）一道淡横格
    paper_plain.png   “纸面纹理”关闭时的纯纸色（同尺寸，便于原样替换）

    python mcp/asset_pipeline/build_ui_paper.py
"""
from __future__ import annotations

import random
from pathlib import Path

from PIL import Image

OUT = (Path(__file__).resolve().parents[2] / "godot_csharp") / "ShareAsset" / "Internal" / "Props" / "UIPaper"
N = 128
SCALE = 2
BASE = (0xEA, 0xE0, 0xC4)
MID = (0xE3, 0xD8, 0xB9)
DARK = (0xD9, 0xCC, 0xA9)
FIBER = (0xCF, 0xC1, 0x9C)
LIGHT = (0xF0, 0xE8, 0xD0)
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def value_noise(rng: random.Random, cells: int):
    grid = [[rng.random() for _ in range(cells)] for _ in range(cells)]

    def sample(x: int, y: int) -> float:
        cx, cy = x / N * cells, y / N * cells
        x0, y0 = int(cx) % cells, int(cy) % cells
        x1, y1 = (x0 + 1) % cells, (y0 + 1) % cells
        tx, ty = cx - int(cx), cy - int(cy)
        sx, sy = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        a = grid[y0][x0] * (1 - sx) + grid[y0][x1] * sx
        b = grid[y1][x0] * (1 - sx) + grid[y1][x1] * sx
        return a * (1 - sy) + b * sy
    return sample


def build() -> Image.Image:
    rng = random.Random(11)
    n1, n2, n3 = value_noise(rng, 4), value_noise(rng, 8), value_noise(rng, 16)
    im = Image.new("RGB", (N, N), BASE)
    px = im.load()
    for y in range(N):
        for x in range(N):
            v = 0.55 * n1(x, y) + 0.3 * n2(x, y) + 0.15 * n3(x, y)
            t = (BAYER[y % 4][x % 4] + 0.5) / 16
            if v > 0.6 and t < (v - 0.6) * 2.2:
                px[x, y] = MID
            elif v < 0.32 and t < (0.32 - v) * 1.6:
                px[x, y] = LIGHT
            if rng.random() < 0.008:
                px[x, y] = DARK
    for _ in range(22):
        x, y = rng.randrange(N), rng.randrange(N)
        dx, dy = rng.choice([(1, 0), (1, 1), (0, 1), (1, -1), (2, 1)])
        for k in range(rng.randint(2, 4)):
            px[(x + k * dx) % N, (y + k * dy) % N] = FIBER
    for _ in range(2):
        x, y = rng.randrange(N), rng.randrange(N)
        for ox, oy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            px[(x + ox) % N, (y + oy) % N] = DARK
    return im.resize((N * SCALE, N * SCALE), Image.NEAREST)


RULE = (0xD6, 0xC9, 0xA6)
RULE_STEP = 16


def ruled(base: Image.Image) -> Image.Image:
    im = base.copy()
    px = im.load()
    step = RULE_STEP * SCALE
    for y in range(step - SCALE, N * SCALE, step):
        for x in range(N * SCALE):
            for dy in range(SCALE):
                px[x, y + dy] = RULE
    return im


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    paper = build()
    outputs = {"paper_pixel.png": paper, "paper_ruled.png": ruled(paper), "paper_plain.png": Image.new("RGB", paper.size, BASE)}
    for name, im in outputs.items():
        im.save(OUT / name)
        print(f"wrote {OUT / name}")


if __name__ == "__main__":
    main()
