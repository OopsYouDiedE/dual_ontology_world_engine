"""Native dot/line recipes for the paper reasoning UI. Never resamples artwork.

The MCP ui_workflow builder owns publication, nine-slice metadata and Aseprite
sources. This module owns the pixels, alongside the existing room recipes.
"""

from pathlib import Path
from shutil import copyfile

from PIL import Image, ImageDraw


PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / "AssetsSource/Internal/Props/FirstAwakeningInterior/mind_seal.png"
RUNTIME = PROJECT / "ShareAsset/Internal/Props/FirstAwakeningInterior/mind_seal.png"

PAPER_COLORS = dict(ink='#303930', shadow='#c2b499', iron='#eee5d2', edge='#9c8c70',
                   light='#fbf5e8', gold='#8a5d36', paper='#f2ecdf', text='#303930',
                   muted='#62665a', hover='#e4dac3', pressed='#d3c3a4')


def paper_slice(kind: str) -> Image.Image:
    """Hand-placed 1px marks; centres and border strips repeat exactly."""
    p = PAPER_COLORS
    n = 16 if kind.startswith('button') else 24
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    if kind == 'focus':
        # Keyboard focus is a single ink stroke, never a box around the control.
        d.line((8, n-2, n-9, n-2), fill=p['ink'])
        return im
    if kind.startswith('button'):
        if kind in ('button-hover', 'button-pressed'):
            d.rectangle((1, 1, n-2, n-2), fill=p['hover' if kind == 'button-hover' else 'pressed'])
        return im
    if kind == 'editor':
        # Completely quiet writing surface, no noise beneath glyphs.
        return im
    d.rectangle((1, 1, n-2, n-2), fill=p['paper'])
    if kind == 'dossier':
        d.line((3, 0, n-4, 0), fill=p['edge'])
        d.line((0, 3, 0, n-4), fill=p['edge'])
        d.line((n-1, 3, n-1, n-4), fill=p['shadow'])
        d.line((3, n-1, n-4, n-1), fill=p['shadow'])
        for x,y in ((1,2),(2,1),(n-3,1),(n-2,2),(1,n-3),(2,n-2),(n-3,n-2),(n-2,n-3)):
            d.point((x,y), fill=p['edge'])
    return im


def notebook() -> Image.Image:
    """640x496 native page, displayed at exactly 2x in the 1280x992 UI."""
    p = PAPER_COLORS
    im = Image.new('RGBA', (640, 496), '#a39378')
    d = ImageDraw.Draw(im)
    # Two sheets with a deliberately stepped edge and a narrow stitched gutter.
    for left, right in ((4, 344), (350, 635)):
        d.polygon([(left+4,3),(right-3,3),(right,6),(right,487),(right-4,491),
                   (left+3,491),(left,488),(left,7)], fill=p['paper'])
        d.line([(left+4,3),(right-3,3),(right,6)], fill=p['light'])
        d.line([(left,7),(left,488),(left+3,491),(right-4,491)], fill=p['edge'])
        d.line([(left+3,493),(right-4,493),(right+1,488)], fill=p['shadow'])
        # Fibres are chosen marks at the outer margin only, away from all text.
        for x,y in ((left+2,37),(left+3,132),(right-2,268),(left+2,385),(right-3,450)):
            d.line((x,y,x,y+3), fill='#e1d7c3')
    for x,c in ((344,'#d5c8af'),(345,'#c2b499'),(346,'#ae9d80'),(347,'#95866f'),(348,'#b9a88b'),(349,'#ded2ba')):
        d.line((x,7,x,487), fill=c)
    for y in (49,132,216,300,383,466):
        d.line((345,y,349,y+1),fill='#80755e')
        d.point((346,y+2),fill='#f2ecdf')
    # Tiny binding-corner strokes are manual integer marks.
    for x,y,sx,sy in ((14,10,1,1),(628,10,-1,1),(14,482,1,-1),(628,482,-1,-1)):
        d.line((x,y,x+sx*5,y), fill=p['gold'])
        d.line((x,y,x,y+sy*5), fill=p['gold'])
    return im


def build() -> None:
    image = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    dark = "#20372f"
    rim = "#bea46d"
    gold = "#ead59b"
    shadow = "#6b664d"
    teal = "#78977d"

    # A small watchmaker's seal: octagonal casing, iris, and central aperture.
    draw.polygon([(8, 1), (15, 1), (22, 8), (22, 15), (15, 22), (8, 22), (1, 15), (1, 8)], fill=shadow, outline=rim)
    draw.polygon([(9, 4), (14, 4), (19, 9), (19, 14), (14, 19), (9, 19), (4, 14), (4, 9)], fill=dark, outline=gold)
    draw.line([(12, 5), (12, 8)], fill=rim)
    draw.line([(12, 15), (12, 18)], fill=rim)
    draw.line([(5, 12), (8, 12)], fill=rim)
    draw.line([(15, 12), (18, 12)], fill=rim)
    draw.polygon([(12, 8), (16, 12), (12, 16), (8, 12)], fill=teal, outline=gold)
    draw.rectangle((11, 11, 12, 12), fill=dark)
    for x, y in ((11, 1), (11, 21), (1, 11), (21, 11)):
        draw.rectangle((x, y, x + 1, y + 1), fill=gold)

    SOURCE.parent.mkdir(parents=True, exist_ok=True)
    RUNTIME.parent.mkdir(parents=True, exist_ok=True)
    image.save(SOURCE)
    copyfile(SOURCE, RUNTIME)


if __name__ == "__main__":
    build()
