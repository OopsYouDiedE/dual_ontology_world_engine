"""Copy complete closed test records into an exclusive, verified carry-forward pack."""
from pathlib import Path
import argparse
import hashlib
import shutil
import sys

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / "language_core/tools"))
from binary_evidence import RecordWriter


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("previous", nargs="+", type=Path)
    args = parser.parse_args()
    target = args.out.resolve()
    partial = target.with_name(target.name + ".partial")
    if target.exists() or partial.exists():
        parser.error("Use an exclusive new output path")
    previous = [path.resolve(strict=True) for path in args.previous]
    if len({path.name for path in previous}) != len(previous):
        parser.error("Previous round directory names must be unique")
    if any(target.is_relative_to(path) or partial.is_relative_to(path) for path in previous):
        parser.error("Do not nest the carry-forward pack inside its sources")
    partial.mkdir(parents=True)
    count = total = 0
    with RecordWriter(partial / "carry-forward.dvlog") as writer:
        for source in previous:
            for path in sorted(source.rglob("*")):
                if not path.is_file():
                    continue
                relative = Path(source.name) / path.relative_to(source)
                destination = partial / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                before = digest(path)
                shutil.copy2(path, destination)
                if before != digest(destination) or before != digest(path):
                    raise RuntimeError("Changed or mismatched evidence: " + str(path))
                size = destination.stat().st_size
                writer.append({"source": str(path), "path": str(relative), "sha256": before, "bytes": size})
                count += 1
                total += size
        writer.append({"kind": "complete", "rounds": [str(path) for path in previous],
                       "files": count, "bytes": total, "policy": "full byte copies; originals retained"})
    partial.rename(target)
    print(f"Archived {count} complete files ({total} bytes) to {target}")


if __name__ == "__main__":
    main()
