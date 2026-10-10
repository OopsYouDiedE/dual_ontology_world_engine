from __future__ import annotations

import argparse
import contextlib
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[5]
HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("repository_verify", ROOT / "tools" / "verify.py")
verify = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verify)

parser = argparse.ArgumentParser()
parser.add_argument("name")
parser.add_argument("--attempt", default="01")
parser.add_argument("--godot")
args = parser.parse_args()
directory = HERE / (args.name + "-attempt-" + args.attempt)
directory.mkdir(parents=True, exist_ok=False)

paths = set()
for folder in (ROOT / "csharp", ROOT / "godot_csharp" / "scenes"):
    for pattern in ("*.cs", "*.csproj", "*.gd", "*.sln"):
        paths.update(p for p in folder.rglob(pattern) if "obj" not in p.parts and "bin" not in p.parts)
paths.update(ROOT / p for p in ("AGENTS.md", "csharp/AGENTS.md", "godot_csharp/AGENTS.md", "tools/verify.py",
    "godot_csharp/tools/story_script.py", "godot_csharp/tools/story_strategy.py", "godot_csharp/tools/check/check_story_strategy.py"))
paths.update((ROOT / "godot_csharp").glob("*.csproj"))
paths.update((ROOT / "godot_csharp" / "tools").rglob("*.py"))
paths.add(Path(__file__).resolve())
manifest = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(paths)}
(directory / "source-sha256.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

steps = {
    "host-build": (["dotnet", "build", "godot_csharp/DualOntologyWorldEngine.sln", "-m:1", "--disable-build-servers", "-nologo", "-v", "q"], 600, False),
    "player-controller": (["dotnet", "run", "--project", "godot_csharp/scenes/Shared/check/PlayerControllerCheck/PlayerControllerCheck.csproj", "-c", "Release"], 600, False),
    "strategy-integration": (["dotnet", "run", "--project", "godot_csharp/scenes/Shared/check/StrategyIntegrationCheck/StrategyIntegrationCheck.csproj", "-c", "Release"], 600, False),
    "strategy-compiler": (["python", "godot_csharp/tools/check/check_story_strategy.py"], 300, False),
    "story-consistency": (["python", "godot_csharp/tools/compile_story.py", "--check"], 300, False),
}
name = args.name
if name in ("save", "flow", "flow2"):
    godot, note = verify.find_godot(args.godot)
    if not godot:
        raise RuntimeError(note)
    script, timeout = {
        "save": ("res://scenes/Shared/check/check_session_save.gd", 300),
        "flow": ("res://scenes/Awakening/check/flow_check.gd", 600),
        "flow2": ("res://scenes/Chapter2/check/check_flow.gd", 900),
    }[name]
    command, scan = [godot, "--headless", "--path", str(ROOT / "godot_csharp"), "--script", script], True
else:
    command, timeout, scan = steps[name]
(directory / "command.json").write_text(json.dumps(command, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
os.chdir(ROOT)
os.environ["LEARNING_HOST_EVIDENCE"] = str(directory)
with (directory / "runner.log").open("w", encoding="utf-8", newline="") as log, contextlib.redirect_stdout(log):
    okay, detail = verify.run(name, command, timeout, scan, directory)
after = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(paths)}
(directory / "source-after-sha256.json").write_text(json.dumps(after, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
changed = [name for name, digest in manifest.items() if after[name] != digest]
(directory / "result.json").write_text(json.dumps({"passed": okay, "detail": detail, "source_changed_during_run": changed}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(("PASS " if okay else "FAIL ") + name)
print("Evidence: " + str(directory))
sys.exit(0 if okay else 1)
