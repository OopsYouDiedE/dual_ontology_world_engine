"""Import and capture finite real Godot stage rendering; no Core/AI session."""
import argparse
from pathlib import Path
import subprocess
import sys
from contextlib import redirect_stderr
import hashlib

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from godot_workspace import workspace, import_assets, _link, GAME


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    repository = Path(__file__).resolve().parents[2]
    files = [*repository.joinpath('godot_csharp/scenes/Highlights').glob('*.gd'),
             repository / 'godot_csharp/Contracts/highlight_stages.json',
             *repository.joinpath('godot_csharp/ShareAsset/Internal/Props/ModernStage').glob('*.png'),
             repository / 'mcp/asset_pipeline/publish_astra_stage.py', Path(__file__)]
    (out / 'sources.sha256').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + str(p.relative_to(repository)) + '\n' for p in files), encoding='utf-8')
    with workspace(isolated_user_data=True) as work:
        # Resource URIs are identical in the linked project. Reuse only imported
        # asset blobs, not editor state, user data or session data; new/changed
        # sources still go through Godot's normal source-hash invalidation.
        cache = GAME / '.godot/imported'
        if cache.is_dir(): _link(cache, work / '.godot/imported')
        with (out / 'import-console.log').open('w', encoding='utf-8') as log, redirect_stderr(log):
            import_assets(work, args.godot, out / 'import.log')
        with (out / 'console.log').open('w', encoding='utf-8') as log:
            return subprocess.call([args.godot, '--path', str(work), '--rendering-method', 'gl_compatibility',
                '--script', 'res://tools/capture_modern_stage.gd', '--', '--out=' + str(out)],
                stdout=log, stderr=subprocess.STDOUT, timeout=90)


if __name__ == '__main__':
    raise SystemExit(main())
