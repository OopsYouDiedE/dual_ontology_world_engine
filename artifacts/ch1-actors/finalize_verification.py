"""Retain verifier byte logs and undo only the three test-induced translation imports."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
raw_runs = [
    ("host-initial-raw", "20261008-202137-417483-22964"),
    ("host-retry-raw", "20261008-202536-998935-23088"),
]
for label, run in raw_runs:
    destination = out / label
    destination.mkdir(exist_ok=True)
    for name in ("host.stdout.log", "host.stderr.log"):
        shutil.copyfile(root / "artifacts/project-audit/verify-logs" / run / name, destination / name)

restored = []
for locale in ("scene", "speaker", "zh"):
    relative = f"godot_csharp/scenes/Awakening/Script/generated_lines_zh.{locale}.translation"
    file = root / relative
    original = subprocess.run(["git", "show", f"HEAD:{relative}"], cwd=root, check=True, capture_output=True).stdout
    prior = file.read_bytes()
    file.write_bytes(original)
    restored.append({"path": relative, "test_import_sha256": hashlib.sha256(prior).hexdigest(), "restored_HEAD_sha256": hashlib.sha256(original).hexdigest(), "matches_HEAD": file.read_bytes() == original})

source = []
for relative in ("godot_csharp/scenes/Shared/Field/WorldView.gd", "godot_csharp/scenes/Story/StoryActor.gd", "godot_csharp/scenes/Awakening/Script/Chapter1SessionFlows.cs"):
    data = (root / relative).read_bytes()
    source.append({"path": relative, "sha256": hashlib.sha256(data).hexdigest(), "CRLF_count": data.count(b"\r\n"), "LF_count": data.count(b"\n")})
(out / "verification-source-manifest.json").write_text(json.dumps({"restored_translation_imports": restored, "verified_sources": source}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"restored": restored, "sources": source}, ensure_ascii=False, indent=2))
