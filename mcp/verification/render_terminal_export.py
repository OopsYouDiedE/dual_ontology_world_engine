"""Rasterize the CLI's cell-positioned SVG export; does not launch a session.

Supports only the explicit rect/text subset emitted by TerminalView.ExportSvg.
This is a rendering export, not a capture of a physical terminal window.
"""
from pathlib import Path
import argparse
import base64
import codecs
import csv
from bisect import bisect_left, bisect_right
from functools import lru_cache
import json
import math
import os
import struct
import time
import unicodedata
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw, ImageFont
from binary_evidence import records


@lru_cache(maxsize=32)
def font_cmaps(font_path):
    """Read real Unicode-to-glyph mappings, including the first face of a TTC.

    Pillow does not expose fallback or glyph coverage. A missing character must
    not silently become the font's .notdef box in recorded puzzle evidence.
    """
    with Path(font_path).open("rb") as source:
        signature = source.read(4)
        face = 0
        if signature == b"ttcf":
            source.seek(12)
            face = struct.unpack(">I", source.read(4))[0]
        source.seek(face + 4)
        count = struct.unpack(">H", source.read(2))[0]
        source.seek(face + 12)
        tables = [struct.unpack(">4sIII", source.read(16)) for _ in range(count)]
        cmap = next((table for table in tables if table[0] == b"cmap"), None)
        if cmap is None:
            raise ValueError(f"Font has no cmap table: {font_path}")
        source.seek(cmap[2])
        data = source.read(cmap[3])
    subtables = []
    seen = set()
    for index in range(struct.unpack_from(">H", data, 2)[0]):
        platform, encoding, offset = struct.unpack_from(">HHI", data, 4 + index * 8)
        if offset in seen or not (platform == 0 or platform == 3 and encoding in (1, 10)):
            continue
        seen.add(offset)
        kind = struct.unpack_from(">H", data, offset)[0]
        if kind == 4:
            length, segments = struct.unpack_from(">H", data, offset + 2)[0], struct.unpack_from(">H", data, offset + 6)[0] // 2
            table = data[offset:offset + length]
            ends = struct.unpack_from(f">{segments}H", table, 14)
            starts = struct.unpack_from(f">{segments}H", table, 16 + segments * 2)
            deltas = struct.unpack_from(f">{segments}h", table, 16 + segments * 4)
            range_start = 16 + segments * 6
            ranges = struct.unpack_from(f">{segments}H", table, range_start)
            subtables.append((kind, starts, ends, deltas, ranges, range_start, table))
        elif kind in (12, 13):
            groups = struct.unpack_from(">I", data, offset + 12)[0]
            values = [struct.unpack_from(">III", data, offset + 16 + index * 12) for index in range(groups)]
            subtables.append((kind, tuple(item[0] for item in values), tuple(item[1] for item in values),
                              tuple(item[2] for item in values)))
    if not subtables:
        raise ValueError(f"Font has no supported Unicode cmap (4/12/13): {font_path}")
    return tuple(subtables)


@lru_cache(maxsize=65536)
def font_has_glyph(font_path, codepoint):
    for table in font_cmaps(font_path):
        kind, starts, ends = table[:3]
        if kind in (12, 13):
            index = bisect_right(starts, codepoint) - 1
            if index >= 0 and codepoint <= ends[index]:
                glyph = table[3][index] + (codepoint - starts[index] if kind == 12 else 0)
                if glyph != 0:
                    return True
        elif codepoint <= 0xffff:
            index = bisect_left(ends, codepoint)
            if index >= len(ends) or codepoint < starts[index]:
                continue
            deltas, ranges, range_start, data = table[3:]
            if ranges[index] == 0:
                glyph = (codepoint + deltas[index]) & 0xffff
            else:
                address = range_start + index * 2 + ranges[index] + (codepoint - starts[index]) * 2
                glyph = struct.unpack_from(">H", data, address)[0] if address + 2 <= len(data) else 0
                if glyph:
                    glyph = (glyph + deltas[index]) & 0xffff
            if glyph != 0:
                return True
    return False


@lru_cache(maxsize=64)
def fallback_font_paths(primary, additional=()):
    primary = Path(primary)
    for extra in additional:
        if not Path(extra).is_file():
            raise ValueError(f"Fallback font does not exist: {extra}")
    windows = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    candidates = (primary, *map(Path, additional), primary.parent / "seguisym.ttf", windows / "seguisym.ttf",
                  primary.parent / "segoeui.ttf", windows / "segoeui.ttf",
                  Path("/usr/share/fonts/truetype/noto/NotoSansSymbols2-Regular.ttf"),
                  Path("/usr/share/fonts/truetype/noto/NotoSansSymbols-Regular.ttf"),
                  Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"))
    return tuple(dict.fromkeys(str(path) for path in candidates if path.is_file()))


def cell_width(text):
    """Match TerminalView.RuneWidth, not a font's proportional advance."""
    total = 0
    for character in text:
        if unicodedata.category(character) in ("Mn", "Me"):
            continue
        n = ord(character)
        wide = n == 0x2014 or n >= 0x1100 and (
            n <= 0x115f or n in (0x2329, 0x232a) or 0x2e80 <= n <= 0xa4cf or
            0xac00 <= n <= 0xd7a3 or 0xf900 <= n <= 0xfaff or 0xfe10 <= n <= 0xfe19 or
            0xfe30 <= n <= 0xfe6f or 0xff01 <= n <= 0xff60 or 0xffe0 <= n <= 0xffe6 or
            0x1f300 <= n <= 0x1faff or n >= 0x20000)
        total += 2 if wide else 1
    return total


def draw_fallback_text(image, attrs, text, primary_path, size, fonts, stroke_width=0, fallback_fonts=()):
    """Draw actual supported glyph runs at the original SVG cell positions."""
    paths = fallback_font_paths(str(primary_path), tuple(map(str, fallback_fonts)))
    runs = []
    for character in text:
        # Combining marks keep the preceding base's position and face.
        if character.isspace() or unicodedata.category(character) in ("Mn", "Me"):
            selected = runs[-1][0] if runs else str(primary_path)
        else:
            selected = next((path for path in paths if font_has_glyph(path, ord(character))), None)
            if selected is None:
                raise ValueError(f"No available font supports U+{ord(character):04X}; supply --fallback-font")
        if runs and runs[-1][0] == selected:
            runs[-1] = (selected, runs[-1][1] + character)
        else:
            runs.append((selected, character))
    x = float(attrs["x"])
    cells = cell_width(text)
    cell_pixels = float(attrs["textLength"]) / cells if "textLength" in attrs and cells else None
    for path, run in runs:
        key = (path, size)
        if key not in fonts:
            fonts[key] = ImageFont.truetype(path, size)
        positioned = {**attrs, "x": str(x)}
        advance = cell_width(run) * cell_pixels if cell_pixels is not None else float(fonts[key].getlength(run))
        if cell_pixels is not None:
            positioned["textLength"] = str(advance)
        draw_text_run(image, positioned, run, fonts[key], stroke_width)
        x += advance


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


def render_file(svg_path, output_path, font_path, quality=94, fonts=None, fallback_fonts=()):
    """Render a recorded cell canvas; never create or advance a game session."""
    svg_path, output_path, font_path = Path(svg_path), Path(output_path), Path(font_path)
    if output_path.exists():
        raise FileExistsError(f"Existing evidence is never overwritten: {output_path}")
    if not font_path.is_file():
        raise ValueError("Supply a CJK-capable TrueType/OpenType font")
    root = ET.parse(svg_path).getroot()
    width, height = int(float(root.attrib["width"])), int(float(root.attrib["height"]))
    if not 1 <= width <= 12000 or not 1 <= height <= 12000:
        raise ValueError("Invalid SVG canvas size")
    image = Image.new("RGB", (width, height), "#101824")
    draw = ImageDraw.Draw(image)
    if fonts is None:
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
            synthetic_bold = 1 if bold and actual_font == font_path else 0
            if len(element):
                raise ValueError("Nested SVG text is outside the CLI export subset")
            draw_fallback_text(image, attrs, element.text or "", actual_font, size, fonts, synthetic_bold, fallback_fonts)
        else:
            raise ValueError(f"Unsupported SVG element {tag}; use the CLI's cell export")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    suffix = output_path.suffix.lower()
    if suffix not in (".jpg", ".jpeg", ".png"):
        raise ValueError("Output must be JPG or PNG")
    with output_path.open("xb") as stream:
        if suffix in (".jpg", ".jpeg"):
            image.save(stream, format="JPEG", quality=quality, subsampling=0)
        else:
            image.save(stream, format="PNG")
    image.close()
    return width, height


def render_directory(directory, font_path, quality=94, fallback_fonts=()):
    """Convert every indexed replay page to JPG and preserve every frame mapping."""
    directory = Path(directory)
    source_index = directory / "coverage.csv"
    if not source_index.is_file():
        raise ValueError("The replay coverage.csv is required; unindexed partial capture is not complete")
    output_index = directory / "jpg-coverage.csv"
    partial = output_index.with_suffix(".csv.partial")
    if output_index.exists() or partial.exists():
        raise FileExistsError(f"Use a new capture directory: {directory}")
    rendered = set()
    frames = set()
    fonts = {}
    with source_index.open(newline="", encoding="utf-8-sig") as source, partial.open("x", newline="", encoding="utf-8") as target:
        reader = csv.DictReader(source)
        fields = list(reader.fieldnames or ()) + ["jpg_path", "image_source"]
        writer = csv.DictWriter(target, fieldnames=fields)
        writer.writeheader()
        for row in reader:
            svg_name = row["svg_path"]
            svg_path = directory / svg_name
            if svg_path.parent.resolve() != directory.resolve():
                raise ValueError("Replay index points outside its image directory")
            jpg_path = svg_path.with_suffix(".jpg")
            if svg_name not in rendered:
                render_file(svg_path, jpg_path, font_path, quality, fonts, fallback_fonts)
                rendered.add(svg_name)
            row["jpg_path"] = jpg_path.name
            row["image_source"] = "recorded-cli-cell-rendering"
            writer.writerow(row)
            frames.add(int(row["frame"]))
    partial.rename(output_index)
    return {"images": len(rendered), "frames": len(frames), "index": str(output_index),
            "font_files": sorted({key[0] for key in fonts})}


def render_console_log(console_path, output_directory, font_path, quality=94, fallback_fonts=()):
    """Render actual failed-run console evidence without inventing game frames."""
    console_path, output_directory, font_path = Path(console_path), Path(output_directory), Path(font_path)
    if not font_path.is_file():
        raise ValueError("Supply a CJK-capable TrueType/OpenType font")
    started = time.monotonic()
    output_directory.mkdir(parents=True, exist_ok=False)
    raw = list(records(console_path))
    closed = next((item for item in reversed(raw) if item.get("kind") == "closed"), {})
    window_started = any(item.get("kind") == "window_start" for item in raw)
    trace_available = (console_path.parent / "session.dvdag").is_file()
    startup_failure = not window_started and not trace_available and closed.get("exit_code") not in (None, 0)
    state = "启动失败，无游戏帧" if startup_failure else "会话记录中断，仅展示控制台证据"
    rows = []
    decoders = {name: codecs.getincrementaldecoder("utf-8")("backslashreplace") for name in ("stdout", "stderr", "stdin")}
    pending = {name: "" for name in decoders}
    pending_source = {}

    def append_line(number, real_ns, channel, value):
        text = value.removesuffix("\r")
        # Keep invisible control bytes visible in the evidence image.
        text = "".join(character if character >= " " or character == "\t" else f"\\x{ord(character):02x}" for character in text)
        rows.append({"record": number, "real_ns": real_ns, "channel": channel, "text": text.expandtabs(4)})

    for number, item in enumerate(raw, 1):
        kind = item.get("kind", "")
        stamp = item.get("real_ns", 0)
        if kind in decoders and item.get("encoding") == "base64":
            if not pending[kind] and not decoders[kind].getstate()[0]:
                pending_source[kind] = (number, stamp)
            pending[kind] += decoders[kind].decode(base64.b64decode(item.get("bytes", ""), validate=True))
            while "\n" in pending[kind]:
                line, pending[kind] = pending[kind].split("\n", 1)
                original_number, original_stamp = pending_source[kind]
                append_line(original_number, original_stamp, kind, line)
                pending_source[kind] = (number, stamp)
        elif kind == "launch":
            append_line(number, stamp, "launch", json.dumps({
                key: item.get(key) for key in ("scope", "requested_seconds", "hard_timeout_seconds", "source_revision")
                if key in item
            }, ensure_ascii=False))
        elif kind in ("driver_error", "window_start", "window_end", "closed", "timeout"):
            append_line(number, stamp, kind, json.dumps({key: value for key, value in item.items()
                                                       if key not in ("kind", "real_ns")}, ensure_ascii=False))
    for channel, decoder in decoders.items():
        pending[channel] += decoder.decode(b"", final=True)
        if pending[channel]:
            number, stamp = pending_source.get(channel, (len(raw), 0))
            append_line(number, stamp, channel, pending[channel])
    rows.sort(key=lambda row: (row["record"], row["real_ns"]))
    if not rows:
        raise ValueError("The console record contains no renderable evidence")

    font = ImageFont.truetype(str(font_path), 22)
    heading_font = ImageFont.truetype(str(font_path), 30)
    small_font = ImageFont.truetype(str(font_path), 18)
    fonts = {(str(font_path), 22): font, (str(font_path), 30): heading_font, (str(font_path), 18): small_font}

    def draw_console(image, point, text, selected_font, color):
        if not text:
            return
        # Preserve the same measured line width and top anchor as the raw-log
        # layout while using supported glyphs for each recorded character.
        attrs = {"x": str(point[0]), "y": str(point[1] + selected_font.getmetrics()[0]), "fill": color,
                 "textLength": str(selected_font.getlength(text)), "lengthAdjust": "spacingAndGlyphs"}
        draw_fallback_text(image, attrs, text, font_path, selected_font.size, fonts, fallback_fonts=fallback_fonts)
    width, height, margin, line_height = 1600, 1080, 42, 33
    content_width = width - margin * 2
    visual = []
    for row in rows:
        stamp = row["real_ns"] / 1e9 if isinstance(row["real_ns"], (float, int)) else 0
        prefix = f"#{row['record']}  {stamp:.6f}s  {row['channel']} | "
        current = ""
        for character in prefix + row["text"]:
            if current and font.getlength(current + character) > content_width:
                visual.append((current, row))
                current = "    "
            current += character
        visual.append((current, row))
        visual.append(("", row))
    top, bottom = 170, height - 72
    capacity = max(1, (bottom - top) // line_height)
    pages = (len(visual) + capacity - 1) // capacity
    partial = output_directory / "console-jpg-coverage.csv.partial"
    final_index = output_directory / "console-jpg-coverage.csv"
    with partial.open("x", encoding="utf-8", newline="") as index:
        writer = csv.DictWriter(index, fieldnames=("page", "page_count", "jpg_path", "record", "real_ns", "channel", "image_source"))
        writer.writeheader()
        for page in range(pages):
            segment = visual[page * capacity:(page + 1) * capacity]
            image = Image.new("RGB", (width, height), "#171914")
            draw_console(image, (margin, 28), "原始控制台记录", heading_font, "#e7dec7")
            draw_console(image, (margin, 77), state, font, "#e3b07c")
            draw_console(image, (margin, 118), "来源：" + console_path.name + "  ·  未生成或重放游戏画面", small_font, "#a7a293")
            seen = set()
            filename = f"console-page-{page + 1:03d}.jpg"
            for offset, (line, row) in enumerate(segment):
                color = "#e3b07c" if row["channel"] in ("stderr", "driver_error", "timeout") else "#e7dec7"
                draw_console(image, (margin, top + offset * line_height), line, font, color)
                identity = (row["record"], row["channel"])
                if identity not in seen:
                    seen.add(identity)
                    writer.writerow({"page": page + 1, "page_count": pages, "jpg_path": filename,
                                     "record": row["record"], "real_ns": row["real_ns"], "channel": row["channel"],
                                     "image_source": "original-console-record-no-game-frame"})
            draw_console(image, (margin, height - 48), f"原始日志仍为唯一来源  ·  {page + 1}/{pages}", small_font, "#a7a293")
            with (output_directory / filename).open("xb") as destination:
                image.save(destination, format="JPEG", quality=quality, subsampling=0)
            image.close()
    partial.rename(final_index)
    elapsed = time.monotonic() - started
    with (output_directory / "export.md").open("x", encoding="utf-8") as report:
        report.write(f"# 原始控制台 JPG 记录\n\n{state}。这些图片只排版已有原始控制台内容，没有游戏帧。\n\n"
                     f"来源：`{console_path}`；记录数：{len(raw)}；图片数：{pages}。\n\n"
                     f"退出状态：`{json.dumps(closed, ensure_ascii=False)}`。\n\n"
                     f"离线导出耗时：{elapsed:.3f} 秒；未启动 AI 会话，不计入此前运行窗口。\n")
    return {"images": pages, "records": len(raw), "index": str(final_index), "elapsed_seconds": elapsed}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--svg", type=Path)
    source.add_argument("--directory", type=Path, help="Convert the full replay coverage index to JPG")
    source.add_argument("--console-log", type=Path, help="Render original failed-run console evidence without a game session")
    parser.add_argument("--out", type=Path)
    parser.add_argument("--quality", type=int, default=94)
    parser.add_argument("--font", type=Path, help="CJK-capable font; defaults to Microsoft YaHei on Windows")
    parser.add_argument("--fallback-font", type=Path, action="append", default=[],
                        help="Additional actual glyph fonts; repeatable. Segoe UI Symbol is discovered on Windows.")
    args = parser.parse_args()
    if not 80 <= args.quality <= 100:
        parser.error("quality must be between 80 and 100")
    if args.svg and not args.out:
        parser.error("--svg requires --out")
    if args.console_log and not args.out:
        parser.error("--console-log requires --out NEW_DIRECTORY")
    if args.directory and args.out:
        parser.error("--directory writes JPGs and its index beside the captured SVGs")
    font_path = args.font or Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts/msyh.ttc"
    if args.console_log:
        result = render_console_log(args.console_log, args.out, font_path, args.quality, args.fallback_font)
        print(f"Rendered {result['images']} original-console JPG pages; no game frame; index {result['index']}")
    elif args.directory:
        result = render_directory(args.directory, font_path, args.quality, args.fallback_font)
        print(f"Rendered {result['images']} JPG pages covering {result['frames']} recorded frames; index {result['index']}")
    else:
        width, height = render_file(args.svg, args.out, font_path, args.quality, fallback_fonts=args.fallback_font)
        print(f"Rendered {width}x{height} CLI cell export to {args.out}; not a physical terminal screenshot")


if __name__ == "__main__":
    main()
