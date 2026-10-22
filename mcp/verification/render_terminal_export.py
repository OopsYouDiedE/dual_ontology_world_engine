"""Rasterize the CLI's cell-positioned SVG export; does not launch a session.

Supports only the explicit rect/text subset emitted by TerminalView.ExportSvg.
This is a rendering export, not a capture of a physical terminal window.
"""
from pathlib import Path
import argparse
import os
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw, ImageFont


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
            draw.text((float(attrs["x"]), float(attrs["y"])), element.text or "", font=fonts[key],
                      fill=attrs.get("fill", "#dbe6ed"), anchor="ls", stroke_width=synthetic_bold)
        else:
            raise ValueError(f"Unsupported SVG element {tag}; use the CLI's cell export")
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("xb") as stream:
        image.save(stream, format="PNG")
    print(f"Rendered {width}x{height} terminal-cell export to {args.out}")


if __name__ == "__main__":
    main()
