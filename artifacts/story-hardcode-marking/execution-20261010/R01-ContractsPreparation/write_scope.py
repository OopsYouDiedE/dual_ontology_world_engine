"""Emit static read disclosure and byte hashes for this preparation review only.

Does not execute production tools, tests, Git, or imported project modules.
"""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
BASE = "artifacts/story-hardcode-marking/execution-20261010/"
RELATIVE_INPUTS = [
    "AGENTS.md",
    "artifacts/story-hardcode-marking/solution-review.md",
    BASE + "approval-request.md",
    BASE + "F01/capability-manifest.json",
    BASE + "F01/source-evidence.json",
    BASE + "F01/REPORT.md",
    BASE + "D01A/REPORT.md",
    BASE + "D01A/candidate-contract.json",
    BASE + "D01A/core-results.json",
    BASE + "D01A/reference-results.json",
    BASE + "D01B/README.md",
    BASE + "D01B/candidate-contract.json",
    BASE + "D01B/approval-items.json",
    BASE + "D01B/core-results.json",
    BASE + "D02/diagnosis.md",
    BASE + "D02/raw-output.jsonl",
    BASE + "D03/REPORT.md",
    BASE + "D03/experiment-results-r02.json",
    BASE + "D04/diagnosis.md",
    BASE + "D04/candidate-contracts.md",
    BASE + "D04/probe-results.json",
    BASE + "D05/diagnosis.md",
    BASE + "D05/probe-result.json",
    BASE + "C08/REPORT.md",
    BASE + "C08/contract-draft.json",
    BASE + "C08/file-plan.json",
]

records = []
for relative in RELATIVE_INPUTS:
    payload = (ROOT / relative).read_bytes()
    records.append({
        "path": relative,
        "sha256": hashlib.sha256(payload).hexdigest(),
        "byte_length": len(payload),
    })

disclosure = {
    "schema": "R01-contracts-preparation-read-scope/v1",
    "task_instance": "/root/r02_contract_review_01 (initial dispatch name retained)",
    "corrected_role": "R01-ContractsPreparation; not R02 behavior/multi-play review",
    "baseline": "7489db56a714724eb7cb735958dd879dc662d238",
    "baseline_verification": "Provided by parent. No Git commands performed.",
    "controller_late_disclosure": {
        "current_head": "5c678a85",
        "current_head_independently_verified": False,
        "review_input_sha256_provided": "3e53904168303c99b8d9cef5c109bfd971b51c7455ca067a391b0df57c0bce66",
        "changed_source_read": False,
        "new_core_semantics_approved": False,
        "virtual_detective_requirement": "Formal story-declared player replacement and visible candidates/decision/real submit for V00/V02; DTO fixture insufficient",
    },
    "permission_mode": "DETECTION_ONLY_SHARED_WORKSPACE",
    "hard_acl": False,
    "initial_write_scope": BASE + "R02/",
    "initial_scope_revoked_before_any_write": True,
    "effective_write_scope": BASE + "R01-ContractsPreparation/",
    "accessed_content_paths": RELATIVE_INPUTS,
    "solution_review": {
        "title_heading_index": "rg displayed all heading lines only; included project title and section titles",
        "displayed_line_ranges": [[185, 280], [301, 377], [397, 414], [437, 453], [492, 537], [610, 675]],
        "raw_io": "Get-Content loaded the whole authorized review file to memory for line slicing. Full file bytes hashed. No section ACL was technically enforced.",
        "scope_purpose": "Contract, task, permissions, approval and delivery requirements only; no design links followed",
    },
    "other_read_methods": {
        "reports_and_candidates": "Get-Content or JSON structured read; some initial aggregate outputs were truncated",
        "F01_capability_manifest": "Initial aggregate full display truncated; subsequently top-level structure and capability name/status/version/gap projections read",
        "F01_source_evidence": "Metadata schema and full source-file hash/range records read; original source files were not opened",
        "D02_raw_output": "Parsed as text lines for record count only; no diagnostic program executed",
        "D03_neutral_result": "Top-level status/count/schema only; report supplies individual results",
        "other_neutral_results": "D01A, D01B, D04, D05 result records read; no rerun",
    },
    "filename_discovery": [
        {
            "root": "artifacts/story-hardcode-marking/",
            "globs": ["solution-review.md", "approval-request.md", "capability-manifest.json", "source-evidence.json", "REPORT.md", "*C08*", "*D01A*", "*D01B*", "*D02*", "*D03*", "*D04*", "*D05*"],
            "effect": "Returned only matching file paths; also exposed names of verification/REPORT.md and R03/REPORT.md. Neither file content read.",
        },
        {
            "roots": [BASE + x for x in ["D01A", "D01B", "D02", "D03", "D04", "D05", "C08"]],
            "effect": "Enumerated evidence file paths to choose specifically authorized reports/candidates/results. Listed code/project/log paths were not opened unless explicitly in accessed_content_paths.",
        },
    ],
    "output_limitations": [
        {
            "sequence": 1,
            "kind": "aggregate_output_truncated",
            "original_token_count": 32928,
            "material": "solution-review selected ranges plus F01/C08 files",
            "followup": "Relevant review ranges, F01 report/schema/projections and C08 contract reread in smaller calls",
        },
        {
            "sequence": 2,
            "kind": "aggregate_output_truncated",
            "original_token_count": 15343,
            "material": "F01 capability projection, review ranges and D01A report",
            "followup": "Selected review task/delivery ranges reread; D01A candidate/results read separately",
        },
        {
            "sequence": 3,
            "kind": "aggregate_output_truncated",
            "original_token_count": 10352,
            "material": "D01B/D02/D03 diagnostics",
            "followup": "Critical D01B conflict checked in candidate and approval JSON; D02 current-mechanism boundary is supported by visible table and F01. No claim of exhaustive line-by-line D02 literary review.",
        },
    ],
    "forbidden_content_not_opened": [
        "docs/ and project design files",
        ".story files",
        "sealed golden/oracle and historical blind-play records",
        "production source files (F01 references only)",
        "design links in AGENTS/review",
        "R03/REPORT.md and verification/REPORT.md discovered names",
    ],
    "content_adjacent_reference_disclosure": "Allowed reports contain source paths, module names and resource/authoring key shapes. They are used only for interfaces and not to infer narrative, character, puzzle or world-design facts.",
    "operations_not_performed": [
        "production/source/AGENTS/design edits",
        "new tests, compiler, build or diagnostic rerun",
        "game, import, image/UI inspection or blind-play",
        "Git operations, commit, push or branch/HEAD verification",
        "approval record edits, capability activation, lease signing",
    ],
    "upstream_failure_preservation": "Existing diagnostic failure files and access disclosures were referenced from allowed reports and left untouched. No claim that they were independently reproduced or resolved.",
    "written_outputs": [
        "REPORT.md", "contract-review.json", "read-scope.json", "input-hashes.json", "delivery-hashes.json", "write_scope.py"
    ],
    "hash_limitation": "Input hashes identify bytes when this script ran, after review reads; no immutable input snapshot or Git-content proof. Shared files may change later.",
}

hash_manifest = {
    "schema": "R01-contracts-preparation-input-hashes/v1",
    "baseline": disclosure["baseline"],
    "baseline_verification": disclosure["baseline_verification"],
    "algorithm": "SHA256",
    "file_count": len(records),
    "files": records,
    "meaning": disclosure["hash_limitation"],
}
for name, value in [("read-scope.json", disclosure), ("input-hashes.json", hash_manifest)]:
    (OUT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

print(json.dumps({"static_metadata_written": True, "input_count": len(records), "output_directory": str(OUT)}, ensure_ascii=False))
