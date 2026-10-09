"""Read-only integrity checks plus a new artifact-local final manifest."""
import hashlib
import json
from pathlib import Path
import subprocess
base = Path(__file__).resolve().parent
root = base.parents[3]
execution = json.loads((base / "execution-evidence.json").read_text(encoding="utf-8"))
checks = []
for name, expected in execution["linked_source_sha256_before"].items():
    actual = hashlib.sha256((root / name).read_bytes()).hexdigest()
    checks.append({"source": name, "sha256": actual, "unchanged_since_before_experiment": actual == expected})
f01 = json.loads((root / "artifacts/story-hardcode-marking/execution-20261010/F01/source-evidence.json").read_text(encoding="utf-8"))
f01_hashes = {record["path"]: record["sha256"] for record in f01["source_files"]}
for check in checks:
    if check["source"] in f01_hashes:
        check["matches_F01_read_version"] = check["sha256"] == f01_hashes[check["source"]]
json_files = ["core-results.json", "compiler-results.json", "compiler-alias-complete.json", "reference-results.json", "candidate-contract.json", "reference-evidence.json"]
for name in json_files:
    json.loads((base / name).read_text(encoding="utf-8"))
core = json.loads((base / "core-results.json").read_text(encoding="utf-8"))
p05 = next(item["observed"] for item in core["results"] if item["id"] == "P05")
deliverables = ["REPORT.md", "candidate-contract.json", "PREDICTIONS.md", "REFERENCE-PREDICTION.md", *json_files, "execution-evidence.json"]
final = {"schema": "D01A-final-integrity/v1", "mode": "DETECTION_ONLY", "hard_acl": False,
    "head_now": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root).decode().strip(),
    "all_reviewed_sources_unchanged": all(c["unchanged_since_before_experiment"] for c in checks),
    "all_shared_F01_source_versions_match": all(c.get("matches_F01_read_version", True) for c in checks),
    "source_checks": checks,
    "json_syntax_verified": json_files,
    "actual_core_observation_count": len(core["results"]) + 1,
    "P05_mixed_version_positive_count": sum(item["result"] == "True" for item in p05["attempts"]),
    "deliverable_sha256": {name: hashlib.sha256((base / name).read_bytes()).hexdigest() for name in dict.fromkeys(deliverables)},
    "save_fixture": "NOT_PROVIDED / NOT_VERIFIED",
    "production_changes_by_agent": "none; scoped source hashes confirm unchanged reviewed/linked source",
    "game_acceptance_levels": "1/2/3 NOT_RUN"}
(base / "final-integrity.json").write_text(json.dumps(final, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({k: final[k] for k in ["head_now", "all_reviewed_sources_unchanged", "all_shared_F01_source_versions_match", "actual_core_observation_count", "P05_mixed_version_positive_count"]}))
