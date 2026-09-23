"""Build the approved front-idle atlas from its ImageGen source. Requires Pillow."""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "godot_csharp/ShareAsset/Images/characters"
# Approved source bounds and native body heights, in the established 01–21 order.
FRAMES = [
    ((51, 40, 182, 343), 44), ((262, 52, 420, 344), 43),
    ((472, 51, 655, 344), 43), ((702, 29, 852, 344), 46),
    ((912, 51, 1084, 345), 46), ((1146, 52, 1297, 345), 44),
    ((1361, 52, 1515, 344), 44), ((56, 399, 177, 671), 42),
    ((260, 406, 414, 671), 39), ((477, 373, 646, 671), 44),
    ((689, 373, 861, 671), 44), ((931, 399, 1065, 671), 42),
    ((1154, 392, 1288, 671), 43), ((1371, 385, 1511, 671), 44),
    ((54, 726, 177, 983), 37), ((264, 694, 408, 983), 44),
    ((484, 701, 631, 984), 44), ((699, 694, 877, 984), 43),
    ((922, 708, 1067, 983), 42), ((1151, 708, 1289, 983), 43),
    ((1373, 708, 1511, 983), 44),
]


def build():
    source = Image.open(ASSETS / "source/front-standing-generated.png").convert("RGBA")
    atlas = Image.new("RGBA", (224, 144))
    for index, (bounds, height) in enumerate(FRAMES):
        sprite = source.crop(bounds)
        width = min(28, round(sprite.width * height / sprite.height))
        sprite = sprite.resize((width, height), Image.Resampling.NEAREST)
        sprite.putalpha(sprite.getchannel("A").point(lambda a: 255 if a >= 200 else 0))
        colours = [pixel[:3] for pixel in sprite.getdata() if pixel[3]]
        samples = Image.new("RGB", (len(colours), 1))
        samples.putdata(colours)
        local_palette = samples.quantize(colors=16, method=Image.Quantize.MEDIANCUT)
        reduced = sprite.convert("RGB").quantize(palette=local_palette, dither=Image.Dither.NONE).convert("RGBA")
        reduced.putalpha(sprite.getchannel("A"))
        sprite = reduced
        atlas.paste(sprite, (index % 7 * 32 + (32 - width) // 2,
                             index // 7 * 48 + 47 - height))

    # One shared palette, with no dithering or interpolated edge pixels.
    opaque = [rgb[:3] for rgb in atlas.getdata() if rgb[3]]
    swatches = Image.new("RGB", (len(opaque), 1))
    swatches.putdata(opaque)
    palette = swatches.quantize(colors=96, method=Image.Quantize.MEDIANCUT)
    result = atlas.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE).convert("RGBA")
    result.putalpha(atlas.getchannel("A"))
    result.putdata([pixel if pixel[3] else (0, 0, 0, 0) for pixel in result.getdata()])
    result.save(ASSETS / "characters-21-front-master.png")

    # A labelled 4x nearest-neighbour review image, never used as a game texture.
    names = ["Wordcraft", "Ritual", "Mentor", "Head", "Demon", "Detective", "Poet",
             "Musician", "Naturalist", "Engineer", "Chef", "Archaeologist", "Thief", "Lawyer",
             "Chess player", "Chemist", "Sharpshooter", "Dancer", "Painter", "Architect", "Doctor"]
    preview = Image.new("RGB", (7 * 144, 3 * 220), "#b8b9b2")
    draw = ImageDraw.Draw(preview)
    font = ImageFont.load_default(size=13)
    for index, name in enumerate(names):
        x, y = index % 7 * 144, index // 7 * 220
        frame = result.crop((index % 7 * 32, index // 7 * 48,
                             index % 7 * 32 + 32, index // 7 * 48 + 48))
        frame = frame.resize((128, 192), Image.Resampling.NEAREST)
        preview.paste(frame, (x + 8, y + 3), frame)
        draw.text((x + 72, y + 199), f"{index+1:02} {name}", font=font, anchor="mt", fill="#303332")
    preview.save(ASSETS / "front-standing-preview.png")
    print("Saved 224x144 atlas: 21 frames, 32x48, <=16 colours/frame, 96 shared colours, binary alpha.")


if __name__ == "__main__":
    build()
