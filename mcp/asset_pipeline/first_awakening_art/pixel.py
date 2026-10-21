"""Small 1x pixel canvas with hue-shifted shading and selective outlines."""
from __future__ import annotations

import colorsys
from pathlib import Path

import numpy as np
from PIL import Image

Color = tuple[int, int, int, int]


def hexc(value: str, alpha: int = 255) -> Color:
    value = value.lstrip("#")
    return (int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), alpha)


def mix(a: Color, b: Color, t: float) -> Color:
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(4))  # type: ignore[return-value]


def shift(color: Color, light: float, hue_toward: float | None = None, sat: float = 0.0) -> Color:
    """Lighten (positive) or darken (negative) with a painterly hue drift.

    Shadows drift toward violet, highlights toward warm yellow, which keeps
    ramps alive instead of greying out.
    """
    r, g, b, a = color
    h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
    target = hue_toward if hue_toward is not None else (0.72 if light < 0 else 0.13)
    delta = ((target - h + 0.5) % 1.0) - 0.5
    h = (h + delta * min(abs(light) * 0.55, 0.35)) % 1.0
    l = min(max(l + light * (l if light < 0 else (1 - l)), 0.0), 1.0)
    s = min(max(s + sat + (0.06 if light < 0 else -0.04) * abs(light), 0.0), 1.0)
    rr, gg, bb = colorsys.hls_to_rgb(h, l, s)
    return (int(rr * 255), int(gg * 255), int(bb * 255), a)


def ramp(base: str, steps: tuple[float, ...] = (-0.62, -0.38, -0.18, 0.0, 0.2, 0.42)) -> list[Color]:
    color = hexc(base)
    return [shift(color, s) if s else color for s in steps]


class Canvas:
    def __init__(self, width: int, height: int, fill: Color | None = None):
        self.w, self.h = width, height
        self.a = np.zeros((height, width, 4), dtype=np.uint8)
        if fill:
            self.a[:, :] = fill

    # ---- primitives -------------------------------------------------------
    def inside(self, x: int, y: int) -> bool:
        return 0 <= x < self.w and 0 <= y < self.h

    def px(self, x: int, y: int, c: Color) -> None:
        if self.inside(x, y):
            if c[3] == 255:
                self.a[y, x] = c
            elif c[3] > 0:
                self.a[y, x] = self._blend(self.a[y, x], c)

    def get(self, x: int, y: int) -> Color:
        return tuple(int(v) for v in self.a[y, x])  # type: ignore[return-value]

    @staticmethod
    def _blend(dst: np.ndarray, c: Color) -> np.ndarray:
        alpha = c[3] / 255
        out = dst.astype(float)
        out[:3] = out[:3] * (1 - alpha) + np.array(c[:3]) * alpha
        out[3] = max(dst[3], c[3]) if dst[3] else c[3]
        return out.astype(np.uint8)

    def rect(self, x: int, y: int, w: int, h: int, c: Color) -> None:
        x0, y0, x1, y1 = max(x, 0), max(y, 0), min(x + w, self.w), min(y + h, self.h)
        if x1 <= x0 or y1 <= y0:
            return
        if c[3] == 255:
            self.a[y0:y1, x0:x1] = c
        else:
            region = self.a[y0:y1, x0:x1].astype(float)
            alpha = c[3] / 255
            region[..., :3] = region[..., :3] * (1 - alpha) + np.array(c[:3]) * alpha
            region[..., 3] = np.maximum(region[..., 3], c[3])
            self.a[y0:y1, x0:x1] = region.astype(np.uint8)

    def hline(self, x: int, y: int, w: int, c: Color) -> None:
        self.rect(x, y, w, 1, c)

    def vline(self, x: int, y: int, h: int, c: Color) -> None:
        self.rect(x, y, 1, h, c)

    def frame(self, x: int, y: int, w: int, h: int, c: Color) -> None:
        self.hline(x, y, w, c)
        self.hline(x, y + h - 1, w, c)
        self.vline(x, y, h, c)
        self.vline(x + w - 1, y, h, c)

    def line(self, x0: int, y0: int, x1: int, y1: int, c: Color) -> None:
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.px(x0, y0, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def ellipse(self, cx: float, cy: float, rx: float, ry: float, c: Color) -> None:
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                    self.px(x, y, c)

    def poly(self, points: list[tuple[float, float]], c: Color) -> None:
        ys = [p[1] for p in points]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            yc = y + 0.5
            nodes = []
            for i, (x0, y0) in enumerate(points):
                x1, y1 = points[(i + 1) % len(points)]
                if (y0 <= yc < y1) or (y1 <= yc < y0):
                    nodes.append(x0 + (yc - y0) / (y1 - y0) * (x1 - x0))
            nodes.sort()
            for i in range(0, len(nodes) - 1, 2):
                for x in range(int(round(nodes[i])), int(round(nodes[i + 1]))):
                    self.px(x, y, c)

    def dither(self, x: int, y: int, w: int, h: int, c: Color, density: float = 0.5, phase: int = 0) -> None:
        """Ordered 4x4 Bayer dither for soft gradients without anti-aliasing."""
        bayer = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                if bayer[(yy + phase) % 4][(xx + phase) % 4] < density * 16:
                    self.px(xx, yy, c)

    def blit(self, other: "Canvas", x: int, y: int) -> None:
        for yy in range(other.h):
            ty = y + yy
            if not 0 <= ty < self.h:
                continue
            for xx in range(other.w):
                tx = x + xx
                if 0 <= tx < self.w:
                    c = other.a[yy, xx]
                    if c[3]:
                        self.px(tx, ty, tuple(int(v) for v in c))  # type: ignore[arg-type]

    def flipped(self) -> "Canvas":
        out = Canvas(self.w, self.h)
        out.a = self.a[:, ::-1].copy()
        return out

    # ---- finishing --------------------------------------------------------
    def outline(self, strength: float = -0.62, hue: float = 0.78, only_below: bool = False) -> None:
        """Selective outline: each rim pixel takes a darkened hue of what it wraps."""
        alpha = self.a[..., 3] > 0
        out = self.a.copy()
        for y in range(self.h):
            for x in range(self.w):
                if alpha[y, x]:
                    continue
                neighbours = []
                for dx, dy in ((0, -1), (0, 1), (-1, 0), (1, 0)):
                    nx, ny = x + dx, y + dy
                    if only_below and dy == 1:
                        continue
                    if 0 <= nx < self.w and 0 <= ny < self.h and alpha[ny, nx]:
                        neighbours.append(tuple(int(v) for v in self.a[ny, nx]))
                if neighbours:
                    base = neighbours[0]
                    out[y, x] = shift(base, strength, hue, 0.05)
                    out[y, x, 3] = 255
        self.a = out

    def harden(self) -> None:
        self.a[..., 3] = np.where(self.a[..., 3] >= 128, 255, 0)

    def image(self) -> Image.Image:
        return Image.fromarray(self.a, "RGBA")

    def save(self, path: Path) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        self.image().save(path)


def rng(seed: int) -> np.random.Generator:
    return np.random.default_rng(seed)
