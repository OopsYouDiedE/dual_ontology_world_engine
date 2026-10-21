"""Validate original artwork inputs before asset builders publish derivatives."""
from pathlib import Path


def require_originals(paths, family: str) -> None:
    originals = [Path(path) for path in paths]
    if not originals:
        raise FileNotFoundError(f"No {family} original inputs were selected. Restore the original artwork or supply its source directory.")
    missing = [str(path) for path in originals if not path.is_file()]
    if missing:
        raise FileNotFoundError(
            f"Missing {family} original inputs; existing runtime assets have not been changed. "
            "Restore these sources before rebuilding: " + ", ".join(missing)
        )
