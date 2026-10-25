"""Publish explicitly authored RGBA cuts; never infer art, poses or pixel style."""
import argparse
import hashlib
import io
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / 'mcp/asset_pipeline/sources'
OUTPUTS = ROOT / 'godot_csharp/ShareAsset/Internal'


def inside(base, value):
    path = (base / value).resolve()
    if not path.is_relative_to(base.resolve()):
        raise ValueError(f'Path escapes {base}: {value}')
    return path


def sha(data):
    return hashlib.sha256(data).hexdigest()


def publish(manifest_path, replace_owned=False):
    spec = json.loads(manifest_path.read_text(encoding='utf-8'))
    if spec['schema'] != 1 or not spec['target'] or not spec['style_notes']:
        raise ValueError('Require schema=1, target and style_notes')
    # A target is a real local reference, not an assertion that the art is accepted.
    target = inside(ROOT, spec['target'])
    if not target.is_file():
        raise ValueError('Missing visual target')
    destination = inside(OUTPUTS, spec['destination'])
    if destination == OUTPUTS.resolve():
        raise ValueError('Use a dedicated resource pack directory')
    publication = destination / 'approved-publication.json'
    owned = {}
    if publication.exists():
        previous = json.loads(publication.read_text(encoding='utf-8'))
        if previous['manifest'] != str(manifest_path.relative_to(ROOT)):
            raise ValueError('Resource pack belongs to another manifest')
        owned = {x['output']: x['output_sha256'] for x in previous['assets']}
    prepared, records = {}, []
    for asset in spec['assets']:
        name = asset['output']
        if Path(name).name != name or not name.endswith('.png') or name in prepared:
            raise ValueError('Require unique, flat PNG output names')
        source = inside(SOURCES, asset['source'])
        raw = source.read_bytes()
        if sha(raw) != asset['source_sha256']:
            raise ValueError(f'Source changed: {source}')
        with Image.open(io.BytesIO(raw)) as original:
            x, y, right, bottom = asset['crop']
            if not (0 <= x < right <= original.width and 0 <= y < bottom <= original.height):
                raise ValueError(f'Invalid crop: {name}')
            image = original.convert('RGBA').crop((x, y, right, bottom))
        # Transparency must be authored in the source, never guessed from colour.
        if asset['role'] in ('actor', 'prop') and image.getextrema()[3][0] == 255:
            raise ValueError(f'Author transparent source before publishing {name}')
        scale = float(asset.get('scale', 1))
        if scale <= 0:
            raise ValueError('Scale must be positive')
        sampler = asset['sampling']
        if sampler not in ('nearest', 'lanczos'):
            raise ValueError('Explicit sampling must be nearest or lanczos')
        if scale != 1:
            if not asset.get('scale_reason'):
                raise ValueError('Scaling requires an explicit reason')
            image = image.resize((max(1, round(image.width * scale)),
                                  max(1, round(image.height * scale))),
                                 Image.Resampling.NEAREST if sampler == 'nearest' else Image.Resampling.LANCZOS)
        foot = asset.get('foot')
        if asset['role'] in ('actor', 'prop') and (
            not isinstance(foot, list) or len(foot) != 2 or
            not 0 <= foot[0] <= image.width or not 0 <= foot[1] <= image.height):
            raise ValueError(f'Require output-space foot anchor: {name}')
        if asset['role'] == 'actor' and not asset.get('pose'):
            raise ValueError('Actor cuts require explicit pose identity')
        buffer = io.BytesIO()
        image.save(buffer, format='PNG')
        prepared[name] = buffer.getvalue()
        records.append({**asset, 'size': list(image.size), 'output_sha256': sha(prepared[name])})
    if not prepared:
        raise ValueError('No assets')
    # Validate the entire pack before touching any runtime file.
    for name in prepared:
        output = destination / name
        if output.exists() and (not replace_owned or owned.get(name) != sha(output.read_bytes())):
            raise ValueError(f'Refusing unowned or modified output: {output}')
    destination.mkdir(parents=True, exist_ok=True)
    for name, data in prepared.items():
        output = destination / name
        partial = output.with_suffix('.png.partial')
        partial.write_bytes(data)
        partial.replace(output)
    record = {'schema': 1, 'manifest': str(manifest_path.relative_to(ROOT)),
              'manifest_sha256': sha(manifest_path.read_bytes()), 'target': spec['target'],
              'target_sha256': sha(target.read_bytes()), 'style_notes': spec['style_notes'],
              'visual_acceptance': 'pending actual Godot target comparison', 'assets': records}
    partial = publication.with_suffix('.json.partial')
    partial.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    partial.replace(publication)
    print(f'Published {len(records)} authored cuts; visual acceptance remains pending: {destination}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', required=True, help='Repository-relative authored cut manifest')
    parser.add_argument('--replace-owned', action='store_true', help='Replace only unchanged files owned by this manifest')
    args = parser.parse_args()
    publish(inside(ROOT, args.manifest), args.replace_owned)
