from pathlib import Path
import hashlib
import json

OUT = Path(__file__).resolve().parent
run = OUT / "verify-raw" / "20261010-002902-483997-21652"
indices = []
for raw in sorted(run.glob("flow*.log")):
    lines = raw.read_bytes().decode("utf-8", errors="replace").splitlines()
    indices.append({
        "file": raw.relative_to(OUT).as_posix(),
        "failed_check_lines": [number for number, line in enumerate(lines, 1) if line.lstrip().startswith("FAIL ")],
        "engine_error_lines": [number for number, line in enumerate(lines, 1) if line.lstrip().startswith(("ERROR:", "SCRIPT ERROR"))],
    })
(OUT / "failure-line-index.json").write_text(json.dumps(indices, indent=2) + "\n", encoding="utf-8")
provenance = json.loads((OUT / "verify-log-provenance.json").read_text(encoding="utf-8"))
all_copies_valid = all(
    record["identical"] and record["source_sha256"] == record["copy_sha256"]
    and hashlib.sha256((OUT / record["copy"]).read_bytes()).hexdigest() == record["copy_sha256"]
    for record in provenance
)
summary = {
    "baseline": (OUT / "head.stdout.log").read_text().strip(),
    "head_unchanged": (OUT / "head.stdout.log").read_bytes() == (OUT / "head-after-console.stdout.log").read_bytes(),
    "tracked_path_status_unchanged": (OUT / "tracked-diff-before.stdout.log").read_bytes() == (OUT / "tracked-diff-after-console.stdout.log").read_bytes(),
    "copied_channels": len(provenance),
    "copied_channels_byte_identical": all_copies_valid,
    "raw_log_count": len(list(OUT.rglob("*.log"))),
    "results": {"core_build": "PASS", "host_build": "PASS", "story_check": "PASS", "scope_checks": 24,
                "flow_checks": 134, "flow_failed": 24, "flow2_checks": 103, "flow2_failed": 3,
                "flow2_engine_errors": 2, "gallery": "SKIP", "verify": "FAIL"},
    "acceptance_levels": {"level1": "not_executed", "level2": "not_executed", "level3": "not_executed"},
    "timed_out": False,
}
(OUT / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
print(json.dumps(summary, ensure_ascii=False))
