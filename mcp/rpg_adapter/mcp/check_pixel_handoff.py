"""Temporary pixel/binary checks plus an explicitly labelled CLI protocol fixture.

Use --aseprite [executable] to additionally open/export the generated document in
the real Aseprite application. The default protocol fixture does not cover that.
No project assets are written.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import struct
import tempfile
from pathlib import Path
import zlib
from unittest.mock import patch

from PIL import Image, ImageChops

import pixel_handoff as handoff
import workflow


def _run_fixture(aseprite: str | None) -> dict:
    with tempfile.TemporaryDirectory(prefix='pixel-handoff-') as directory:
        root = Path(directory)
        workflow.SOURCES = root
        workflow.SOURCE = root / 'source'
        workflow.OUTPUT = root / 'output'
        workflow.SOURCE.mkdir()
        workflow.OUTPUT.mkdir()
        source = root / 'input.png'
        image = Image.new('RGBA', (3, 2))
        image.putdata([(255, 0, 0, 255), (0, 0, 0, 0), (0, 255, 0, 128),
                       (255, 0, 0, 255), (0, 0, 0, 0), (0, 255, 0, 128)])
        image.save(source)
        audit = handoff.inspect_pixel_style(str(source), ['#FF0000'], ['horizontal', 'vertical'])
        assert audit['visible_color_count'] == 2
        assert audit['alpha'] == {'transparent_pixels': 2, 'partial_pixels': 2, 'opaque_pixels': 2}
        assert audit['palette']['outside_pixel_count'] == 2
        assert 'do not prove' in audit['boundary_note']
        partial_allowed = handoff.inspect_pixel_style(str(source), ['#FF0000', '#00FF00'])
        assert partial_allowed['palette']['outside_pixel_count'] == 0, 'alpha must not alter RGB palette matching'
        same_rgb = root / 'same_rgb.png'
        same_rgb_image = Image.new('RGBA', (2, 1))
        same_rgb_image.putdata([(17, 34, 51, 255), (17, 34, 51, 64)])
        same_rgb_image.save(same_rgb)
        same_rgb_audit = handoff.inspect_pixel_style(str(same_rgb), ['#112233'])
        assert same_rgb_audit['visible_color_count'] == 1
        assert same_rgb_audit['alpha']['partial_pixels'] == 1
        assert same_rgb_audit['palette']['outside_pixel_count'] == 0
        try:
            handoff.quantize_asset('invalid_alpha_palette', str(source), ['#FF000080'])
        except ValueError:
            pass
        else:
            raise AssertionError('RGBA palette entries must be rejected explicitly')

        quantized = handoff.quantize_asset('test_asset', str(source), ['#FF0000', '#00FF00'], [6, 4])
        with Image.open(quantized['output']) as output:
            assert output.size == (6, 4)
            assert list(output.getchannel('A').getdata()).count(0) == 8
            assert list(output.getchannel('A').getdata()).count(128) == 8
        assert json.loads(Path(quantized['metadata']).read_text(encoding='utf-8'))['source_sha256']
        try:
            handoff.quantize_asset('test_asset', str(source), ['#FF0000'])
        except ValueError:
            pass
        else:
            raise AssertionError('same-name overwrite was not rejected')

        dark_source = root / 'dark.png'
        Image.new('RGBA', (1, 1), (0, 0, 0, 255)).save(dark_source)
        dark = handoff.quantize_asset('dark_pixel', str(dark_source), ['#141414'])
        with Image.open(dark['output']) as output:
            assert output.getpixel((0, 0)) == (20, 20, 20, 255), 'palette padding must not introduce black'

        jpeg_source = root / 'original.jpg'
        Image.new('RGB', (2, 2), (20, 30, 40)).save(jpeg_source, quality=91)
        jpeg_bytes = jpeg_source.read_bytes()
        jpeg_result = handoff.quantize_asset('jpeg_asset', str(jpeg_source), ['#141E28'])
        preserved = Path(jpeg_result['source'])
        assert preserved.suffix == '.jpg' and preserved.read_bytes() == jpeg_bytes

        layer_a, layer_b = root / 'layer_a.png', root / 'layer_b.png'
        a = Image.new('RGBA', (2, 2), (0, 0, 0, 0)); a.putpixel((0, 0), (255, 0, 0, 255)); a.save(layer_a)
        b = Image.new('RGBA', (2, 2), (0, 0, 0, 0)); b.putpixel((0, 0), (0, 0, 255, 128)); b.putpixel((1, 1), (0, 0, 255, 255)); b.save(layer_b)
        layered = handoff.layered_sprite('layers', 2, 2,
                    [{'name': 'red', 'path': str(layer_a)}, {'name': 'blue', 'path': str(layer_b)}],
                    {'anchor': [1, 1], 'collision': [0, 1, 2, 1]})
        data = Path(layered['source']).read_bytes()
        assert struct.unpack_from('<IHHHHH', data, 0) == (len(data), 0xA5E0, 1, 2, 2, 32)
        assert struct.unpack_from('<I', data, 14)[0] == 1
        assert struct.unpack_from('<H', data, 18)[0] == 100
        frame_count = struct.unpack_from('<H', data, 128 + 6)[0]
        assert frame_count == 4
        kinds, cursor = [], 144
        for _ in range(frame_count):
            size, kind = struct.unpack_from('<IH', data, cursor)
            kinds.append(kind)
            if kind == 0x2005:
                layer_index, x, y, opacity, cel_type = struct.unpack_from('<HhhBH', data, cursor + 6)
                assert cel_type == 2 and (x, y, opacity) == (0, 0, 255)
                cel_width, cel_height = struct.unpack_from('<HH', data, cursor + 6 + 16)
                pixels_at = cursor + 6 + 16 + 4
                expected = [a, b][layer_index].convert('RGBA').tobytes()
                assert (cel_width, cel_height) == (2, 2)
                assert zlib.decompress(data[pixels_at:cursor + size]) == expected
            cursor += size
        assert kinds == [0x2004, 0x2004, 0x2005, 0x2005]
        with Image.open(layered['composite']) as merged:
            assert merged.getpixel((0, 0)) == (127, 0, 128, 255), 'upper half-alpha cel should composite over lower red cel'
            assert merged.getpixel((1, 1)) == (0, 0, 255, 255)
        metadata = json.loads(Path(layered['metadata']).read_text(encoding='utf-8'))
        assert len(metadata['layers']) == 2 and metadata['anchor'] == [1, 1]
        assert all(len(layer['sha256']) == 64 for layer in metadata['layers'])
        assert 'not tested' in metadata['validation']
        assert layered['aseprite_application_compatibility'] == 'not tested'

        try:
            handoff.layered_sprite('oversized_layers', 2048, 2048,
                [{'name': f'layer_{i}', 'path': str(layer_a)} for i in range(5)])
        except ValueError as exc:
            assert 'pixel budget' in str(exc)
        else:
            raise AssertionError('combined layer budget was not enforced')

        old_sources = workflow.SOURCES
        workflow.SOURCES = root / 'sources'
        ase_source = workflow.SOURCES / 'sprite.aseprite'
        ase_source.parent.mkdir(parents=True)
        ase_source.write_bytes(b'aseprite-source-fixture')
        # This invalid file and fake subprocess exercise command/metadata wiring
        # only. They are never reported as Aseprite application compatibility.
        def fake_run(command, **kwargs):
            assert kwargs['timeout'] == 120 and kwargs['check'] is False
            assert command[:5] == ['aseprite-test', '--batch', '--list-layers', '--list-tags', '--list-slices']
            assert command[5] == str(ase_source.resolve())
            assert '--scale' not in command and '--trim' not in command
            sheet = Path(command[command.index('--sheet') + 1])
            atlas_json = Path(command[command.index('--data') + 1])
            Image.new('RGBA', (1, 1), (1, 2, 3, 255)).save(sheet)
            atlas_json.write_text('{"frames":[],"meta":{"slices":[]}}', encoding='utf-8')
            class Completed:
                returncode = 0
                stderr = ''
            return Completed()
        with patch.dict(os.environ, {'ASEPRITE_BIN': 'aseprite-test'}), \
             patch.object(handoff.subprocess, 'run', side_effect=fake_run) as run_mock:
            export = handoff.export_aseprite('cli_export', str(ase_source))
            assert run_mock.call_count == 1
        export_meta = json.loads(Path(export['metadata']).read_text(encoding='utf-8'))
        assert export_meta['source_sha256'] == __import__('hashlib').sha256(ase_source.read_bytes()).hexdigest()
        assert export_meta['transform']['trim'] is False
        workflow.SOURCES = old_sources
        application = {'status': 'not requested', 'compatibility': 'not tested'}
        if aseprite is not None:
            native_source = Path(layered['source'])
            original_bytes = native_source.read_bytes()
            # Only configure the executable; subprocess.run remains the real CLI.
            with patch.dict(os.environ, {'ASEPRITE_BIN': aseprite}):
                actual = handoff.export_aseprite('real_cli_export', str(native_source))
            assert native_source.read_bytes() == original_bytes
            with Image.open(actual['atlas']) as rendered, Image.open(layered['composite']) as expected:
                assert rendered.size == expected.size == (2, 2)
                difference = ImageChops.difference(rendered.convert('RGBA'), expected.convert('RGBA'))
                assert all(maximum <= 1 for _, maximum in difference.getextrema()), 'Aseprite export differs from the fixture composite'
            atlas_data = json.loads(Path(actual['data']).read_text(encoding='utf-8'))
            assert len(atlas_data['frames']) == 1
            application = {
                'status': 'passed', 'executable': aseprite,
                'scope': 'generated two-layer fixture opened/exported; 2x2 composite within one channel value',
                'source_preserved': True,
            }
        return {
            'pixel_and_binary_layout': 'passed',
            'aseprite_protocol_fixture': 'passed with mocked subprocess; command/metadata only',
            'aseprite_application': application,
            'storage': 'temporary paths only',
        }


def run(aseprite: str | None = None) -> dict:
    original = {key: getattr(workflow, key) for key in ('SOURCE', 'SOURCES', 'OUTPUT')}
    try:
        return _run_fixture(aseprite)
    finally:
        for key, value in original.items():
            setattr(workflow, key, value)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--aseprite', nargs='?', const='auto', metavar='EXECUTABLE',
                        help='Also run the actual application; omit the value to use ASEPRITE_BIN/PATH')
    args = parser.parse_args()
    executable = args.aseprite
    if executable == 'auto':
        executable = os.environ.get('ASEPRITE_BIN') or shutil.which('aseprite')
        if not executable:
            parser.error('Real Aseprite coverage is incomplete: set ASEPRITE_BIN or supply an executable')
    print(json.dumps(run(executable), ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
