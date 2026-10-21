"""Stage explicit compact review records; retain large originals locally."""
from pathlib import Path
import subprocess

task = Path(__file__).resolve().parent
root = task.parents[3]
paths = set()
for pattern in ("*.md", "*.py", "candidate-freeze-*.json", "*preflight*.json", "*postflight*.json", "*postclose*.json"):
    paths.update(task.glob(pattern))
for name in ("integration-stage-plan.json", "parent-evidence-stage-check.json", "candidate-07-build.log", "candidate-07-import.log"):
    paths.add(task / name)
for folder in ("blind-01", "blind-02", "blind-03", "blind-04", "engine-investigation-300s-04", "engine-investigation-300s-07", "perf-ai-90s-x4-01", "perf-integrated-90s-x4-03", "perf-integration-01", "ui-replay-01", "geometry-ui-probe-07"):
    paths.update((task / folder).glob("*.md"))
for folder in ("engine-investigation-300s-04", "engine-investigation-300s-07"):
    paths.update(path for path in (task / folder).glob("*.dvlog") if path.stat().st_size < 1000000)
for name in ("blind-04/run/blind-notes.md", "blind-04/run/frame-000246632.png", "engine-investigation-300s-07/run/frame-000164518.png", "engine-investigation-300s-07/run/frame-000170387.png", "engine-investigation-300s-07/run/frame-000186915.png", "engine-investigation-300s-07/run/frame-000299072.png", "geometry-ui-probe-07/probe.gd", "geometry-ui-probe-07/run-01/probe.dvlog", "geometry-ui-probe-07/run-01/01-open-floor-query.png", "geometry-ui-probe-07/run-01/02-dynamic-body-blocked.png"):
    paths.add(task / name)
existing = sorted(str(path.relative_to(root)).replace("\\", "/") for path in paths if path.is_file())
subprocess.run(["git", "add", "-f", "--pathspec-from-file=-", "--pathspec-file-nul"], cwd=root, input="\0".join(existing).encode() + b"\0", check=True)
print(f"Staged {len(existing)} compact evidence paths; full raw originals and carry packs retained locally.")
