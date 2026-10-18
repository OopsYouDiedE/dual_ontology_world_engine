"""Explicit restore entry; never invoked automatically by checkpoint export."""
from pathlib import Path
import argparse
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--checkpoint', type=Path, required=True, help='Committed checkpoint bundle manifest')
    parser.add_argument('--out', type=Path, required=True, help='New independent run/evidence directory')
    parser.add_argument('--timeout', type=int, default=240)
    parser.add_argument('--projection-every', type=int, default=16)
    args = parser.parse_args()
    # run_debug validates the committed bundle and source/config before building.
    command = [sys.executable, str(Path(__file__).with_name('run_debug.py')), '--resume-checkpoint', str(args.checkpoint.resolve()), '--raw-format', 'dag', '--out', str(args.out.resolve()), '--timeout', str(args.timeout), '--projection-every', str(args.projection_every)]
    return subprocess.run(command, check=False).returncode


if __name__ == '__main__':
    raise SystemExit(main())
