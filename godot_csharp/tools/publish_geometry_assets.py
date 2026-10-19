"""Publish authored vector geometry props without changing their bytes."""
import argparse
import hashlib
import shutil
from pathlib import Path

GAME = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("relative_directory", help="Directory below AssetsSource, mirrored into ShareAsset")
    args = parser.parse_args()
    relative = Path(args.relative_directory)
    if relative.is_absolute() or ".." in relative.parts:
        raise ValueError("Expected a project-relative asset directory")
    source = GAME / "AssetsSource" / relative
    destination = GAME / "ShareAsset" / relative
    files = sorted(source.rglob("*.svg"))
    if not files:
        raise ValueError("No authored SVG assets in directory")
    for path in files:
        target = destination / path.relative_to(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
        if path.read_bytes() != target.read_bytes():
            raise IOError("Published asset differs from source")
        print(target.relative_to(GAME).as_posix(), hashlib.sha256(target.read_bytes()).hexdigest())


if __name__ == "__main__":
    main()
