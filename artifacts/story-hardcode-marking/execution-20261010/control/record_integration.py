"""Record this task's reviewed integration without rewriting sealed inputs."""
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / "artifacts/story-hardcode-marking/execution-20261010"
BASE = "7489db56a714724eb7cb735958dd879dc662d238"


def git(*args, cwd=ROOT):
    return subprocess.check_output(["git", *args], cwd=cwd)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(name, value):
    (OUT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


head = git("rev-parse", "HEAD").decode().strip()
baseline = json.loads((OUT / "baseline.json").read_text(encoding="utf-8"))
expected = {
    "csharp/AGENTS.md",
    "csharp/HallCausalityRules.cs",
    "csharp/HallReplayProgram.cs",
    "godot_csharp/scenes/Shared/Story/WorldBehavior.cs",
}
unexpected = []
input_updates = []
for name, original_hash in baseline["tracked_working_file_hashes"].items():
    path = ROOT / name
    actual = sha(path) if path.is_file() else None
    if actual != original_hash and name not in expected:
        entry = {"path": name, "initial": original_hash, "actual": actual}
        if name == "artifacts/story-hardcode-marking/solution-review.md" and actual == "3e53904168303c99b8d9cef5c109bfd971b51c7455ca067a391b0df57c0bce66":
            entry["reviewed_delta"] = "Input adds a virtual detective in the story replacing the player for testing; no production write by this task."
            input_updates.append(entry)
        else:
            unexpected.append(entry)
if unexpected:
    raise RuntimeError(f"Unexpected baseline drift: {unexpected}")

workers = [
    ("M01", "249471e53a8c1208fc80e98835ea0d0ce6e15e00", "c6508540", [
        "csharp/AGENTS.md", "csharp/HallCausalityRules.cs", "csharp/HallReplayProgram.cs",
        "artifacts/story-hardcode-marking/execution-20261010/M01/implementation-report.md"]),
    ("H01a", "dc63af04213ac0d418b1b598bcd9e025d657eafe", "5c678a85", [
        "godot_csharp/scenes/Shared/Story/WorldBehavior.cs",
        "artifacts/story-hardcode-marking/execution-20261010/H01a/implementation-report.md"]),
]
integrated = []
for task, tip, short_integrated, names in workers:
    integrated_sha = git("rev-parse", short_integrated).decode().strip()
    tree_matches = []
    for name in names:
        worker_entry = git("ls-tree", tip, "--", name, cwd=ROOT / f"tmp/solution-review-workers/{task}")
        merged_entry = git("ls-tree", integrated_sha, "--", name)
        if worker_entry != merged_entry:
            raise RuntimeError(f"Integrated object mismatch: {name}")
        tree_matches.append({"path": name, "identical_tree_entry": True})
    integrated.append({"task": task, "worker_tip": tip, "integrated_commit": integrated_sha,
                       "entries": tree_matches})
write("integration-record.json", {
    "schema": "solution-review.integration-record.v1", "base": BASE, "production_head": head,
    "branch": git("branch", "--show-current").decode().strip(), "isolation": "DETECTION_ONLY",
    "integration": "reviewed exact commits cherry-picked with origin trailer",
    "reviewed_tasks": integrated, "initial_tracked_raw_drift_except_reviewed_files": unexpected,
    "initial_translation_bytes_preserved": True,
    "input_document_update_preserved_without_staging": input_updates,
    "scope_review": "R01-ScopeRound02/verification-report.md",
    "semantic_reviews": ["R01-M01/review-report.md", "R01-H01a/independent-review.md"],
    "controller_comparison_only": True, "new_test_execution": False,
})
issues = json.loads((OUT / "issue-map.json").read_text(encoding="utf-8"))
resolutions = []
for item in issues["items"]:
    if item["id"] in {"CSH-001", "CSH-002", "CSH-003", "CSH-004", "CSH-005"}:
        resolutions.append({"id": item["id"], "resolution": "REMOVED", "task": "M01",
                            "code_commit": integrated[0]["integrated_commit"],
                            "evidence": ["R01-M01/review-report.md", "R01-ScopeRound02/verification-report.md"]})
    elif item["id"] in {"CSH-028", "CSH-030"}:
        resolutions.append({"id": item["id"], "resolution": "RETAINED_VALID", "task": "M06c",
                            "code_commit": None, "marker_retained": True,
                            "evidence": ["M06c/report.md", "M06c/dispositions.json"]})
write("issue-resolutions.json", {
    "schema": "solution-review.issue-resolutions.v1", "sealed_inventory": "issue-map.json",
    "inventory_count": len(issues["items"]), "resolved_count": len(resolutions),
    "remaining_count": len(issues["items"]) - len(resolutions), "production_head": head,
    "resolutions": resolutions,
    "additional_fix": {"task": "H01a", "issue": "F01-G05: existing line/source identifiers omitted when copying candidate response",
                       "code_commit": integrated[1]["integrated_commit"], "does_not_clear_an_audit_marker": True},
})
manifest = json.loads((OUT / "provided-pack/subagent-task-manifest.json").read_text(encoding="utf-8"))
states = {
    "F00": ("AUDIT_COMPLETE", "baseline.json; issue-map.json; F00-validation/report.md"),
    "F01": ("AUDIT_COMPLETE", "F01/REPORT.md; F01/capability-manifest.json"),
    "F02": ("DETECTION_FALLBACK_VERIFIED", "F02/bracket-fix-report.md; R01-ScopeRound02/verification-report.md; OS ACL not implemented"),
    "D01-A": ("EXPERIMENT_COMPLETE_PENDING_CONTRACT", "D01A/REPORT.md"),
    "D01-B": ("EXPERIMENT_COMPLETE_PENDING_CONTRACT", "D01B/README.md"),
    "D02": ("DIAGNOSIS_COMPLETE_PENDING_CONTRACT", "D02/diagnosis.md"),
    "D03": ("DIAGNOSIS_COMPLETE_PENDING_CONTRACT", "D03/REPORT.md"),
    "D04": ("DIAGNOSIS_COMPLETE_PENDING_CONTRACT", "D04/diagnosis.md"),
    "D05": ("DIAGNOSIS_COMPLETE_PENDING_CONTRACT", "D05/diagnosis.md"),
    "C08": ("DRAFT_PENDING_ADOPTION", "C08/REPORT.md; actual resource index and consumers not migrated"),
    "M01": ("INTEGRATED", integrated[0]["integrated_commit"]),
    "M06c": ("READ_ONLY_REVIEW_COMPLETE", "M06c/report.md; approved two-file asset recipe scope only"),
    "V00": ("SKELETON_ONLY", "V00/README.md; formal player connection and OS isolation not implemented"),
    "R03": ("INVENTORY_COMPLETE", "R03/REPORT.md; no golden reads or changes; isolation not established"),
    "R01": ("REVIEWED_M01_H01a_ONLY", "R01-M01; R01-H01a; R01-ScopeRound02; each future patch needs independent review"),
    "R02": ("NOT_EXECUTED", "Requires real autonomous results; no motive or replay review completed"),
    "V03": ("FUTURE_SCOPE", "User places 40 game days in future; current chapters are approximately 3–5 game days"),
    "U18": ("LOCKED", "No exact user-approved UI capture/replay writing or golden change"),
}
write("task-state.json", {
    "schema": "solution-review.task-state.v1", "scope": "first two chapters, approximately 3–5 game days",
    "future_scope": "40 game days", "overall": "IN_PROGRESS_AWAITING_AR_CORE_01",
    "public_semantics_approval": "AWAITING_USER", "approval_request": "approval-request.md",
    "production_head": head, "baseline_game_regression_failures": {"flow": 24, "flow2": 3},
    "acceptance_levels": {"level_1": "NOT_EXECUTED", "level_2": "NOT_EXECUTED", "level_3": "NOT_EXECUTED"},
    "tasks": [{"id": t["id"], "status": states.get(t["id"], ("PENDING_APPROVED_CONTRACT_AND_DEPENDENCIES", "No active production grant"))[0],
               "detail": states.get(t["id"], ("PENDING_APPROVED_CONTRACT_AND_DEPENDENCIES", "No active production grant"))[1],
               "depends_on": t["depends_on"]} for t in manifest["tasks"]],
    "subtask": {"H01a": "INTEGRATED; provenance bug only, not H01 completion"},
})
print(json.dumps({"production_head": head, "integrated": [r["task"] for r in integrated],
                  "unexpected_drift": len(unexpected), "reviewed_input_updates": len(input_updates), "resolved_records": len(resolutions),
                  "remaining_records": len(issues["items"]) - len(resolutions)}, ensure_ascii=False))
