"""Rasterize the CLI's cell-positioned SVG export; does not launch a session.

Supports only the explicit rect/text subset emitted by TerminalView.ExportSvg.
This is a rendering export, not a capture of a physical terminal window.
"""
from pathlib import Path
import argparse
import math
import os
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw, ImageFont


def draw_text_run(image, attrs, text, font, stroke_width):
    """Scale the glyph run around its recorded left-baseline anchor."""
    if not text:
        return
    if "\n" in text or "\r" in text:
        raise ValueError("Multiline SVG text is outside the CLI export subset")
    x, y = float(attrs["x"]), float(attrs["y"])
    advance = float(font.getlength(text))
    scale = 1.0
    if "textLength" in attrs:
        if attrs.get("lengthAdjust", "spacing") != "spacingAndGlyphs":
            raise ValueError("Only textLength with lengthAdjust=spacingAndGlyphs is supported")
        target = float(attrs["textLength"])
        if not math.isfinite(target) or target <= 0 or advance <= 0:
            raise ValueError("SVG textLength and natural font advance must be finite positive values")
        scale = target / advance
    if not all(math.isfinite(value) for value in (x, y, advance, scale)):
        raise ValueError("Invalid SVG text positioning")
    left, top, right, bottom = font.getbbox(text, anchor="ls", stroke_width=stroke_width)
    left, top = math.floor(min(0, left)), math.floor(top)
    right, bottom = math.ceil(max(advance, right)), math.ceil(bottom)
    if right <= left or bottom <= top:
        return
    # Include glyph overhangs while computing the scale from advance, not the
    # ink bounding box. The vertical baseline and font weight stay unchanged.
    strip = Image.new("RGBA", (right - left, bottom - top), (0, 0, 0, 0))
    ImageDraw.Draw(strip).text((-left, -top), text, font=font,
                              fill=attrs.get("fill", "#dbe6ed"), anchor="ls",
                              stroke_width=stroke_width)
    if scale != 1.0:
        strip = strip.resize((max(1, round(strip.width * scale)), strip.height),
                             resample=Image.Resampling.LANCZOS)
    image.paste(strip, (round(x + left * scale), round(y + top)), strip)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--svg", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--font", type=Path, help="CJK-capable font; defaults to Microsoft YaHei on Windows")
    args = parser.parse_args()
    if args.out.exists():
        parser.error("Use a new output PNG; existing evidence is never overwritten")
    font_path = args.font or Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts/msyh.ttc"
    if not font_path.is_file():
        parser.error("Supply --font with a CJK-capable TrueType/OpenType font")
    root = ET.parse(args.svg).getroot()
    width, height = int(float(root.attrib["width"])), int(float(root.attrib["height"]))
    if not 1 <= width <= 12000 or not 1 <= height <= 12000:
        parser.error("Invalid SVG canvas size")
    image = Image.new("RGB", (width, height), "#101824")
    draw = ImageDraw.Draw(image)
    fonts = {}
    for element in root.iter():
        tag = element.tag.rsplit("}", 1)[-1]
        attrs = element.attrib
        if tag in ("svg", "title", "desc"):
            continue
        if tag == "rect":
            x, y = float(attrs.get("x", 0)), float(attrs.get("y", 0))
            w, h = float(attrs["width"]), float(attrs["height"])
            if attrs.get("fill", "none") != "none":
                draw.rectangle((x, y, x + w - 1, y + h - 1), fill=attrs["fill"])
        elif tag == "text":
            size = int(float(attrs.get("font-size", "16")))
            bold = attrs.get("font-weight", "400") in ("bold", "600", "700", "800", "900")
            bold_path = font_path.with_name("msyhbd.ttc") if font_path.name.lower() == "msyh.ttc" else font_path
            actual_font = bold_path if bold and bold_path.is_file() else font_path
            key = (str(actual_font), size)
            if key not in fonts:
                fonts[key] = ImageFont.truetype(str(actual_font), size)
            synthetic_bold = 1 if bold and actual_font == font_path else 0
            if len(element):
                raise ValueError("Nested SVG text is outside the CLI export subset")
            draw_text_run(image, attrs, element.text or "", fonts[key], synthetic_bold)
        else:
            raise ValueError(f"Unsupported SVG element {tag}; use the CLI's cell export")
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("xb") as stream:
        image.save(stream, format="PNG")
    print(f"Rendered {width}x{height} SVG raster export to {args.out}; not a physical terminal screenshot")


if __name__ == "__main__":
    main()
