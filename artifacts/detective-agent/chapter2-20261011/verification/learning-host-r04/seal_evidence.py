from __future__ import annotations

import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]

def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

sources = set()
for folder in (ROOT / "csharp", ROOT / "godot_csharp" / "scenes", ROOT / "godot_csharp" / "tools"):
    for pattern in ("*.cs", "*.csproj", "*.gd", "*.py", "*.sln"):
        sources.update(p for p in folder.rglob(pattern) if "obj" not in p.parts and "bin" not in p.parts and "__pycache__" not in p.parts)
sources.update(ROOT / name for name in ("AGENTS.md", "csharp/AGENTS.md", "godot_csharp/AGENTS.md", "tools/verify.py"))
sources.update((ROOT / "godot_csharp").glob("*.csproj"))
source_hashes = {p.relative_to(ROOT).as_posix(): digest(p) for p in sorted(sources)}
(HERE / "final-source-sha256.json").write_text(json.dumps(source_hashes, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

evidence = {p.relative_to(HERE).as_posix(): digest(p) for p in sorted(HERE.rglob("*"))
    if p.is_file() and p.name != "evidence-sha256.json" and "__pycache__" not in p.parts}
(HERE / "evidence-sha256.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Evidence sealed: " + str(HERE))
