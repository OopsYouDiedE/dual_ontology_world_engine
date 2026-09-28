"""Pixel-art inspection and deterministic handoff helpers for the RPG asset workflow."""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import struct
import subprocess
import zlib
from pathlib import Path
from typing import Any

from PIL import Image

import workflow


def _palette(colors: list[str] | None) -> set[tuple[int, int, int]] | None:
    if colors is None:
        return None
    parsed: set[tuple[int, int, int]] = set()
    for color in colors:
        value = color.removeprefix('#')
        if len(value) != 6:
            raise ValueError('Palette colors must be #RRGGBB; alpha is handled separately')
        try:
            channels = tuple(bytes.fromhex(value))
        except ValueError as exc:
            raise ValueError(f'Invalid palette color: {color}') from exc
        parsed.add(channels)
    return parsed


def inspect_pixel_style(path: str, palette: list[str] | None = None,
                        seam_axes: list[str] | None = None) -> dict[str, Any]:
    """Count visible RGB colors, alpha classes and optional palette/seam diagnostics."""
    source = workflow.read_path(path)
    with Image.open(source) as opened:
        image = opened.convert('RGBA')
    counts = image.getcolors(maxcolors=image.width * image.height) or []
    visible = {color[:3] for count, color in counts if color[3] > 0}
    alpha_hist = image.getchannel('A').histogram()
    result: dict[str, Any] = {
        'path': str(source), 'size': list(image.size), 'visible_color_count': len(visible),
        'alpha': {'transparent_pixels': alpha_hist[0],
                  'partial_pixels': sum(alpha_hist[1:255]),
                  'opaque_pixels': alpha_hist[255]},
        'sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
    }
    allowed = _palette(palette)
    if allowed is not None:
        rgb_counts: dict[tuple[int, int, int], int] = {}
        for count, color in counts:
            if color[3] > 0:
                rgb = color[:3]
                rgb_counts[rgb] = rgb_counts.get(rgb, 0) + count
        outside = sorted((color, count) for color, count in rgb_counts.items() if color not in allowed)
        result['palette'] = {'allowed_color_count': len(allowed),
                             'outside_color_count': len(outside),
                             'outside_pixel_count': sum(n for _, n in outside),
                             'outside_colors': [{'rgb': list(c), 'pixels': n} for c, n in outside]}
    axes = seam_axes or []
    if any(axis not in ('horizontal', 'vertical') for axis in axes) or len(set(axes)) != len(axes):
        raise ValueError("seam_axes may contain horizontal and/or vertical")
    seam: dict[str, Any] = {}
    if 'horizontal' in axes:
        left, right = image.crop((0, 0, 1, image.height)), image.crop((image.width - 1, 0, image.width, image.height))
        diffs = sum(a != b for a, b in zip(left.getdata(), right.getdata()))
        seam['horizontal'] = {'edge_pixel_differences': diffs, 'edge_pixels_compared': image.height,
                              'difference_ratio': diffs / image.height}
    if 'vertical' in axes:
        top, bottom = image.crop((0, 0, image.width, 1)), image.crop((0, image.height - 1, image.width, image.height))
        diffs = sum(a != b for a, b in zip(top.getdata(), bottom.getdata()))
        seam['vertical'] = {'edge_pixel_differences': diffs, 'edge_pixels_compared': image.width,
                            'difference_ratio': diffs / image.width}
    if seam:
        result['boundary_comparison'] = seam
        result['boundary_note'] = 'Boundary differences are descriptive diagnostics and do not prove whether an image tiles seamlessly.'
    return result


def _outputs(name: str, source: Path) -> tuple[Path, Path, Path]:
    workflow.name_ok(name)
    suffix = source.suffix.lower()
    if not suffix or not suffix[1:].isalnum():
        raise ValueError('Source must have a simple file extension so its original format can be preserved')
    return (workflow.within(workflow.SOURCE / f'{name}.original{suffix}', workflow.SOURCE),
            workflow.within(workflow.OUTPUT / f'{name}.png', workflow.OUTPUT),
            workflow.within(workflow.SOURCE / f'{name}.handoff.json', workflow.SOURCE))


def quantize_asset(name: str, source: str, palette: list[str],
                   target_size: list[int] | None = None, replace: bool = False) -> dict[str, Any]:
    """Nearest-neighbor resize and deterministic palette mapping, retaining alpha and provenance."""
    if not palette:
        raise ValueError('palette must contain at least one color')
    colors = _palette(palette)
    assert colors is not None
    if len(colors) > 256:
        raise ValueError('Palette is limited to 256 colors')
    src = workflow.read_path(source)
    original_path, output_path, manifest_path = _outputs(name, src)
    with Image.open(src) as opened:
        image = opened.convert('RGBA')
    if max(image.size) > 2048:
        raise ValueError('Source canvas is limited to 2048x2048')
    target = tuple(target_size) if target_size is not None else image.size
    if len(target) != 2 or any(type(v) is not int or v < 1 or v > 2048 for v in target):
        raise ValueError('target_size must be [width,height], each 1..2048')
    collisions = [p for p in (original_path, output_path, manifest_path) if p.exists()]
    if collisions and not replace:
        raise ValueError('Destination exists; pass replace=true for an intentional replacement')
    original_bytes = src.read_bytes()
    if src.resolve() != original_path.resolve() and original_path.exists():
        if original_path.read_bytes() != original_bytes:
            raise ValueError('A different original is already preserved under this name')
    if target != image.size:
        image = image.resize(target, Image.Resampling.NEAREST)
    opaque_palette = sorted(colors)
    # Pillow's quantizer is deterministic with dithering disabled. Transparent pixels remain transparent.
    rgb = Image.new('RGB', image.size)
    rgb.putdata([p[:3] for p in image.getdata()])
    pal_image = Image.new('P', (1, 1))
    pal_bytes = []
    for r, g, b in opaque_palette:
        pal_bytes.extend((r, g, b))
    last_color = opaque_palette[-1]
    while len(pal_bytes) < 768:
        pal_bytes.extend(last_color)
    pal_image.putpalette(pal_bytes)
    indexed = rgb.quantize(palette=pal_image, dither=Image.Dither.NONE)
    mapped = indexed.convert('RGB').convert('RGBA')
    mapped.putalpha(image.getchannel('A'))
    original_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    if src.resolve() != original_path.resolve() and not original_path.exists():
        original_path.write_bytes(original_bytes)
    mapped.save(output_path)
    manifest = {
        'name': name, 'original_source': str(src), 'preserved_original': str(original_path),
        'source_sha256': hashlib.sha256(original_bytes).hexdigest(),
        'derived_sha256': hashlib.sha256(output_path.read_bytes()).hexdigest(),
        'palette': [f'#{r:02X}{g:02X}{b:02X}' for r, g, b in opaque_palette],
        'target_size': list(target), 'resampling': 'nearest-neighbor',
        'dithering': False, 'alpha': 'preserved exactly',
        'derivation': 'Palette-quantized derivative; not original or newly authored pixel art.',
    }
    workflow.save_json(manifest_path, manifest)
    return {'source': str(original_path), 'output': str(output_path), 'metadata': str(manifest_path), **manifest}


def _ase_string(value: str) -> bytes:
    encoded = value.encode('utf-8')
    if len(encoded) > 65535:
        raise ValueError('Layer names are limited to 65535 UTF-8 bytes')
    return struct.pack('<H', len(encoded)) + encoded


def _chunk(kind: int, payload: bytes) -> bytes:
    return struct.pack('<IH', len(payload) + 6, kind) + payload


def _make_aseprite(layers: list[tuple[str, Image.Image]]) -> bytes:
    width, height = layers[0][1].size
    chunks = []
    for name, _ in layers:
        chunks.append(_chunk(0x2004, struct.pack('<HHHHHHB3s', 1, 0, 0, 0, 0, 0, 255, b'\0\0\0') + _ase_string(name)))
    for index, (_, image) in enumerate(layers):
        payload = struct.pack('<HhhBHh5s', index, 0, 0, 255, 2, 0, b'\0' * 5)
        raw_pixels = image.tobytes()
        compressed_pixels = zlib.compress(raw_pixels)
        payload += struct.pack('<HH', width, height) + compressed_pixels
        chunks.append(_chunk(0x2005, payload))
    frame_payload = b''.join(chunks)
    frame = struct.pack('<IHHH2sI', 16 + len(frame_payload), 0xF1FA, len(chunks), 100, b'\0\0', 0)
    header = bytearray(128)
    total_size = 128 + len(frame) + len(frame_payload)
    struct.pack_into('<IHHHHH', header, 0, total_size, 0xA5E0, 1, width, height, 32)
    struct.pack_into('<I', header, 14, 1)  # DWORD flags, per official 128-byte header
    struct.pack_into('<H', header, 18, 100)  # WORD legacy speed
    header[28] = 0
    struct.pack_into('<H', header, 32, 0)
    header[34:36] = bytes((1, 1))
    return bytes(header) + frame + frame_payload


def layered_sprite(name: str, width: int, height: int, layers: list[dict[str, str]],
                   metadata: dict[str, Any] | None = None, replace: bool = False) -> dict[str, Any]:
    """Build a genuine static Aseprite file from explicit same-canvas RGBA PNG layers."""
    workflow.name_ok(name)
    if not (1 <= width <= 2048 and 1 <= height <= 2048 and 1 <= len(layers) <= 256):
        raise ValueError('Canvas must be 1..2048 and layer count 1..256')
    total_layer_pixels = width * height * len(layers)
    if total_layer_pixels > 16 * 1024 * 1024:
        raise ValueError('Combined layer pixel budget is limited to 16 million RGBA pixels')
    names: set[str] = set()
    loaded: list[tuple[str, Image.Image]] = []
    layer_sources: list[dict[str, str]] = []
    for layer in layers:
        layer_name, path = layer.get('name'), layer.get('path')
        if not layer_name or layer_name in names or not path:
            raise ValueError('Each layer needs a unique name and PNG path')
        names.add(layer_name)
        source = workflow.read_path(path)
        with Image.open(source) as opened:
            image = opened.convert('RGBA')
        if image.size != (width, height):
            raise ValueError('Every layer must match the declared canvas size')
        loaded.append((layer_name, image.copy()))
        layer_sources.append({'name': layer_name, 'source': str(source),
                              'sha256': hashlib.sha256(source.read_bytes()).hexdigest()})
    ase_path = workflow.within(workflow.SOURCE / f'{name}.aseprite', workflow.SOURCE)
    png_path = workflow.within(workflow.OUTPUT / f'{name}.png', workflow.OUTPUT)
    meta_path = workflow.within(workflow.SOURCE / f'{name}.layers.json', workflow.SOURCE)
    if any(p.exists() for p in (ase_path, png_path, meta_path)) and not replace:
        raise ValueError('Destination exists; pass replace=true for an intentional replacement')
    composite = Image.new('RGBA', (width, height))
    for _, image in loaded:
        composite.alpha_composite(image)
    ase_bytes = _make_aseprite(loaded)
    # Validate the emitted container and all layer/cel references before writing it.
    # Verify official fixed offsets: DWORD flags at 14, WORD speed at 18.
    if struct.unpack_from('<IHHHHH', ase_bytes, 0) != (len(ase_bytes), 0xA5E0, 1, width, height, 32):
        raise ValueError('Aseprite header round-trip validation failed')
    if struct.unpack_from('<I', ase_bytes, 14)[0] != 1 or struct.unpack_from('<H', ase_bytes, 18)[0] != 100:
        raise ValueError('Aseprite flags/speed header fields are invalid')
    frame_size, frame_magic, chunks = struct.unpack_from('<IHH', ase_bytes, 128)
    if frame_magic != 0xF1FA or chunks != len(layers) * 2 or frame_size != len(ase_bytes) - 128:
        raise ValueError('Aseprite frame/layer round-trip validation failed')
    ase_path.parent.mkdir(parents=True, exist_ok=True)
    png_path.parent.mkdir(parents=True, exist_ok=True)
    ase_path.write_bytes(ase_bytes)
    composite.save(png_path)
    document = {'canvas': [width, height], 'frames': 1,
                'layers': layer_sources,
                'anchor': (metadata or {}).get('anchor'), 'collision': (metadata or {}).get('collision'),
                'metadata': metadata or {}, 'aseprite_source': str(ase_path), 'composite_png': str(png_path),
                'format': 'Aseprite file with independent RGBA layer cels; static single frame',
                'validation': 'File structure and cel payloads validated against published format specification; Aseprite application compatibility not tested.'}
    workflow.save_json(meta_path, document)
    return {'source': str(ase_path), 'composite': str(png_path), 'metadata': str(meta_path),
            'layer_count': len(layers), 'frame_count': 1, 'structure_validated': True,
            'aseprite_application_compatibility': 'not tested'}


def _aseprite_export_command(executable: str, source: Path, atlas: Path, data: Path) -> list[str]:
    """Fixed argument vector; paths are single argv entries and no user flags are accepted."""
    return [executable, '--batch', '--list-layers', '--list-tags', '--list-slices', str(source),
            '--sheet', str(atlas), '--data', str(data), '--format', 'json-array']


def export_aseprite(name: str, source: str, replace: bool = False) -> dict[str, Any]:
    """Export an AssetsSource Aseprite document with fixed CLI options and preserve its provenance."""
    workflow.name_ok(name)
    source_path = workflow.within(workflow.read_path(source), workflow.GAME / 'AssetsSource')
    if source_path.suffix.lower() not in ('.ase', '.aseprite') or not source_path.is_file():
        raise ValueError('source must be an existing .ase or .aseprite file under AssetsSource')
    executable = os.environ.get('ASEPRITE_BIN') or shutil.which('aseprite')
    if not executable:
        raise ValueError('Aseprite CLI is unavailable; set ASEPRITE_BIN or add aseprite to PATH')
    atlas_path = workflow.within(workflow.OUTPUT / f'{name}.png', workflow.OUTPUT)
    data_path = workflow.within(workflow.OUTPUT / f'{name}.atlas.json', workflow.OUTPUT)
    manifest_path = workflow.within(workflow.SOURCE / f'{name}.export.json', workflow.SOURCE)
    if any(path.exists() for path in (atlas_path, data_path, manifest_path)) and not replace:
        raise ValueError('Export destination exists; pass replace=true for an intentional replacement')
    atlas_path.parent.mkdir(parents=True, exist_ok=True)
    data_path.parent.mkdir(parents=True, exist_ok=True)
    source_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()
    command = _aseprite_export_command(executable, source_path, atlas_path, data_path)
    try:
        completed = subprocess.run(command, capture_output=True, text=True, encoding='utf-8',
                                   errors='replace', timeout=120, check=False)
    except subprocess.TimeoutExpired as exc:
        raise ValueError('Aseprite export timed out after 120 seconds') from exc
    if completed.returncode:
        raise ValueError(f'Aseprite export failed (exit {completed.returncode}): {completed.stderr[-4000:]}')
    if not atlas_path.is_file() or not data_path.is_file():
        raise ValueError('Aseprite CLI returned success but did not create both requested outputs')
    manifest = {
        'name': name, 'source': str(source_path), 'source_sha256': source_hash,
        'atlas': str(atlas_path), 'atlas_sha256': hashlib.sha256(atlas_path.read_bytes()).hexdigest(),
        'atlas_data': str(data_path), 'atlas_data_sha256': hashlib.sha256(data_path.read_bytes()).hexdigest(),
        'cli': str(executable), 'arguments': command[1:],
        'transform': {'scale': None, 'trim': False, 'anchor_data': 'retained in Aseprite JSON metadata'},
        'status': 'exported by Aseprite CLI',
    }
    workflow.save_json(manifest_path, manifest)
    return {'atlas': str(atlas_path), 'data': str(data_path), 'metadata': str(manifest_path), **manifest}
