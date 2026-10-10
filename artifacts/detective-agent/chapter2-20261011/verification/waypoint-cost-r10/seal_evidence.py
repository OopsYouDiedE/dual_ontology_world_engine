"""Seal this run's source snapshots, raw deltas and evidence byte hashes."""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT = Path(__file__).resolve().parents[5]
OUT = Path(__file__).resolve().parent
snapshots = {
    "check_virtual_navigation.final.gd": "godot_csharp/tools/check/check_virtual_navigation.gd",
    "RouteLearningCases.final.cs": "godot_csharp/scenes/Shared/check/PlayerControllerCheck/RouteLearningCases.cs",
    "PilotNavigation.final.gd": "godot_csharp/scenes/Host/PilotNavigation.gd",
}
for name, source in snapshots.items():
    (OUT / name).write_bytes((ROOT / source).read_bytes())
for stem in ("check_virtual_navigation", "RouteLearningCases"):
    extension = ".gd" if stem == "check_virtual_navigation" else ".cs"
    result = subprocess.run(["git", "diff", "--no-index", "--no-renames", "--ignore-space-at-eol",
        str(OUT / (stem + ".before" + extension)), str(OUT / (stem + ".final" + extension))], capture_output=True)
    assert result.returncode in (0, 1), result.stderr
    (OUT / (stem + ".delta.log")).write_bytes(result.stdout)
records = [json.loads(path.read_text(encoding="utf-8")) for path in OUT.glob("*-01.json")]
records.append(json.loads((OUT / "navigation-overcharge-negative-02.json").read_text(encoding="utf-8")))
for record in records:
    assert record["sources_unchanged_during_run"]
    assert record["source_sha256_before"] == record["source_sha256_after"]
    for source, expected in record["source_sha256_after"].items():
        assert hashlib.sha256((ROOT / source).read_bytes()).hexdigest() == expected
    assert record["output_sha256"] == hashlib.sha256((OUT / (record["stage"] + ".log")).read_bytes()).hexdigest()
manifest = {path.name: {"bytes": path.stat().st_size, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
    for path in sorted(OUT.iterdir()) if path.is_file() and path.name != "evidence-sha256.json"}
(OUT / "evidence-sha256.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Sealed current source snapshots and verified archived raw log hashes")
