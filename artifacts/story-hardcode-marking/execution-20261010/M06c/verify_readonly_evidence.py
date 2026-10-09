"""Verify M06c evidence without modifying any watched project file."""

import ast
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
evidence = json.loads((HERE / "evidence.json").read_text(encoding="utf-8"))
initial = json.loads((HERE / "evidence-initial.json").read_text(encoding="utf-8"))
dispositions = json.loads((HERE / "dispositions.json").read_text(encoding="utf-8"))

watched = [
    *evidence["source_files"], *evidence["direct_input_files"],
    *evidence["declared_output_files"], *evidence["direct_source_document_metadata"],
]
changed = [
    item["path"] for item in watched
    if not (ROOT / item["path"]).is_file()
    or hashlib.sha256((ROOT / item["path"]).read_bytes()).hexdigest() != item["sha256"]
]
initial_records = {
    item["path"]: item["sha256"] for key in (
        "source_files", "direct_input_files", "declared_output_files", "direct_source_document_metadata"
    ) for item in initial[key]
}
between_snapshots = [item["path"] for item in watched if initial_records[item["path"]] != item["sha256"]]
for path in HERE.glob("*.py"):
    ast.parse(path.read_text(encoding="utf-8"), filename=str(path))

head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip()
diff = subprocess.check_output([
    "git", "diff", "--numstat", "--",
    "godot_csharp/tools/build_breakfast_exchange.gd", "godot_csharp/tools/build_story_tiles.gd",
], cwd=ROOT).decode().strip()
marker_ids = sorted(item["id"] for item in dispositions["markers"])
result = {
    "task": "M06c", "schema": "M06c.final-verification.v1",
    "verified_at_utc": datetime.now(timezone.utc).isoformat(),
    "isolation": "DETECTION_ONLY", "watched_files": len(watched),
    "changed_since_final_snapshot": changed, "changed_between_initial_and_final_snapshots": between_snapshots,
    "head": head, "head_matches_evidence": head == evidence["head"],
    "source_git_diff_numstat": diff, "marker_ids": marker_ids,
    "expected_marker_ids": ["CSH-028", "CSH-030"],
    "report_json_valid": True, "evidence_helpers_parse": True,
    "pass": not changed and not between_snapshots and not diff
    and head == evidence["head"] and marker_ids == ["CSH-028", "CSH-030"],
    "proof_limit": "Only explicit watched paths were byte-compared. No enforced ACL, whole-repository noninterference, asset generation, import, runtime or design acceptance is proved.",
}
(HERE / "final-verification.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, ensure_ascii=False, indent=2))
raise SystemExit(0 if result["pass"] else 1)
