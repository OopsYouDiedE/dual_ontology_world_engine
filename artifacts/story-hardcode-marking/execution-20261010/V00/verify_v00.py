"""Rebuild and test V00 using only authorized public DTO declarations.

All generated files and command output stay under this V00 directory.
No design, .story, chapter behavior, route, history, or golden output is read.
"""
import base64
import hashlib
import importlib.util
import json
import os
import shutil
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
RESULTS = HERE / "results"
EXPECTED_HEAD = "7489db56a714724eb7cb735958dd879dc662d238"


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def extract_public_declarations():
    wanted = {
        "godot_csharp/scenes/Shared/Story/WorldBehavior.cs": (
            "public abstract record WorldInput;",
            "public sealed record SelectCandidate(string Token) : WorldInput;",
            "public abstract record WorldOutput;",
            "public sealed record AvailableCandidate(string Token, string Id, string Label);",
            "public sealed record CandidatesAvailable(string Actor, IReadOnlyList<AvailableCandidate> Candidates) : WorldOutput;",
            "public sealed record SemanticDialogue(string PresentationId, IReadOnlyList<InteractionLine> Lines) : WorldOutput;",
            "public sealed record NoApplicableCandidate(string Actor) : WorldOutput;",
        ),
        "godot_csharp/scenes/Shared/Story/InteractionPacket.cs": (
            'public sealed record InteractionLine(string Speaker, string Mood, string Text, string VoiceClip, string LineId = "", string SourceId = "");',
            'public sealed record InteractionChoice(string Id, string Label, bool IsAvailable, string LockHint, string LabelId = "");',
            "public sealed record WorldCommand(string Name, IReadOnlyList<object?> Arguments);",
            "public enum PresentationMode { Dialogue, EvidenceCard, Menu, World, Done }",
            "public sealed record EvidenceCard(string Id, string Title, string Facts, string Source, string Picture, IReadOnlyList<string> Notes);",
            "public sealed record InteractionPacket(IReadOnlyList<InteractionLine> Lines, IReadOnlyList<InteractionChoice> Choices,",
            "    PresentationMode PresentationMode, IReadOnlyList<WorldCommand> WorldCommands, bool IsComplete = false, EvidenceCard? Card = null);",
        ),
    }
    output = ["// Generated from EXACT authorized production declarations by verify_v00.py.",
              "// This is a declaration-only compile fixture, NOT the production engine.",
              "#nullable enable",
              "namespace DualOntologyWorldEngine.Story;", ""]
    provenance = []
    for relative, declarations in wanted.items():
        found = []
        lower, upper = (30, 44) if relative.endswith("WorldBehavior.cs") else (6, 13)
        # Stop at the authorized declaration range: no adjacent implementation
        # is imported, printed, compiled, or used as a test expectation.
        with (REPO / relative).open(encoding="utf-8-sig") as source:
            for number, line in enumerate(source, 1):
                if number > upper:
                    break
                if number < lower:
                    continue
                text = line.rstrip("\r\n")
                if text in declarations:
                    found.append(text)
                    provenance.append({"file": relative, "line": number, "declaration": text})
        if tuple(found) != declarations:
            raise RuntimeError("Authorized DTO declaration drift: " + relative)
        output.extend(found)
        output.append("")
    generated = "\n".join(output)
    (HERE / "PublicDtos.g.cs").write_bytes(generated.encode("utf-8"))
    write_json(RESULTS / "dto-provenance.json", {
        "schema": "v00-public-dto-provenance/v1", "baseline": EXPECTED_HEAD,
        "generated_file": "PublicDtos.g.cs", "generated_sha256": hashlib.sha256(generated.encode()).hexdigest(),
        "declarations": provenance, "engine_implementation_imported": False,
        "scope_deviation": "Initial agent read WorldBehavior.cs:26-172 and full InteractionPacket.cs, including adjacent generic implementations; no design/story/solution/route/history source was read. Test tool imports only exact DTO declarations."
    })


def run_logged(name, arguments, timeout=120):
    # Preserve any earlier command output bytes, including failed builds.
    if (RESULTS / (name + ".stdout.log")).exists():
        index = 1
        while (RESULTS / (name + ".previous-" + str(index) + ".stdout.log")).exists():
            index += 1
        for stream in ("stdout", "stderr"):
            previous = RESULTS / (name + "." + stream + ".log")
            if previous.exists():
                (RESULTS / (name + ".previous-" + str(index) + "." + stream + ".log")).write_bytes(previous.read_bytes())
    result = subprocess.run(arguments, cwd=HERE, capture_output=True, timeout=timeout)
    (RESULTS / (name + ".stdout.log")).write_bytes(result.stdout)
    (RESULTS / (name + ".stderr.log")).write_bytes(result.stderr)
    if result.returncode:
        raise RuntimeError(name + " failed with exit " + str(result.returncode))
    return result


def controller_contract_tests():
    specification = importlib.util.spec_from_file_location("v00_controller", HERE / "controller.py")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    cases = []
    observation = {"schema": "v00-public-candidate/v1", "turn": 1,
                   "lines": [{"speaker": "neutral", "mood": "neutral", "text": "neutral-visible-text"}],
                   "candidates": [{"token": "runtime-synthetic-token", "label": "neutral-label"}],
                   "card": None, "complete": False}
    evidence = []

    def check(name, payload, expected_error=None):
        raw = json.dumps(payload, ensure_ascii=False) if not isinstance(payload, str) else payload
        try:
            value = module.parse_observation(raw)
            decision = module.decide(value)
            if expected_error:
                raise AssertionError("Expected rejected observation")
            assert decision["token"] == payload["candidates"][0]["token"]
            cases.append({"name": name, "status": "PASS"})
            evidence.append({"name": name, "input": raw, "output": decision})
        except module.BoundaryError as error:
            if expected_error != str(error):
                raise
            cases.append({"name": name, "status": "PASS", "rejection": str(error)})
            evidence.append({"name": name, "input": raw, "rejection": str(error)})

    check("current_visible_candidate", observation)
    for field in ("Runtime", "Soul", "Essence", "IChapterProbe", "state", "observer", "Snapshot", "source_id"):
        check("hidden_top_level_" + field, dict(observation, **{field: "neutral-hidden-marker"}), "unknown_or_missing_field")
    nested = json.loads(json.dumps(observation))
    nested["lines"][0]["SourceId"] = "neutral-hidden-marker"
    check("hidden_line_metadata", nested, "unknown_or_missing_field")
    nested = json.loads(json.dumps(observation))
    nested["candidates"][0]["Id"] = "neutral-hidden-marker"
    check("candidate_id_not_token", nested, "unknown_or_missing_field")
    check("duplicate_field", '{"schema":"v00-public-candidate/v1","schema":"v00-public-candidate/v1"}', "duplicate_field")
    check("unsupported_schema", dict(observation, schema="unknown"), "unsupported_schema")
    check("non_integer_turn", dict(observation, turn=True), "invalid_turn")
    check("duplicate_candidate_token", dict(observation, candidates=observation["candidates"] * 2), "invalid_candidate_token")
    # Tokens are supplied anew by observations; there is no fixed answer inventory.
    for token in ("opaque-A", "opaque-B", "opaque-C"):
        check("runtime_token_" + token, dict(observation, candidates=[{"token": token, "label": "neutral-label"}]))
    with (RESULTS / "controller-contract.jsonl").open("wb") as log:
        for event in evidence:
            log.write((json.dumps(event, ensure_ascii=False) + "\n").encode("utf-8"))
    write_json(RESULTS / "python-results.json", {"passed": len(cases), "failed": 0, "cases": cases})
    return len(cases)


def permission_detection():
    # Deliberately test only a harmless V00-owned file, not repository content.
    sentinel = RESULTS / "permission-sentinel.txt"
    sentinel.write_text("V00 harmless sentinel; not game content\n", encoding="utf-8")
    empty = RESULTS / "empty-cwd"
    empty.mkdir(exist_ok=True)
    code = "import json,pathlib,sys; p=pathlib.Path(sys.argv[1]); print(json.dumps({'outside_cwd_read':p.read_text(encoding='utf-8'),'cwd':str(pathlib.Path.cwd())}))"
    result = subprocess.run([sys.executable, "-I", "-S", "-X", "utf8", "-c", code, str(sentinel)],
                            cwd=empty, env={"SystemRoot": os.environ.get("SystemRoot", "C:\\Windows")},
                            capture_output=True, timeout=10)
    (RESULTS / "permission-probe.stdout.log").write_bytes(result.stdout)
    (RESULTS / "permission-probe.stderr.log").write_bytes(result.stderr)
    assert result.returncode == 0 and json.loads(result.stdout)["outside_cwd_read"].startswith("V00 harmless sentinel")
    write_json(RESULTS / "permission-detection.json", {
        "mode": "DETECTION_ONLY", "api_boundary_tested": True,
        "filesystem_isolation_claimed": False, "outside_child_cwd_file_read_succeeded": True,
        "sentinel": "permission-sentinel.txt", "read_scope": "V00-owned harmless sentinel only",
        "meaning": "Normal same-user subprocess can read outside cwd; cwd/env minimization does not enforce source secrecy.",
        "not_tested": ["restricted user/ACL", "container", "network isolation", "process-memory access restrictions"]
    })


def verify_observer_separation():
    events = [json.loads(line) for line in (RESULTS / "02_visible_projection_and_process.jsonl").read_text(encoding="utf-8").splitlines()]
    official = "\n".join(event["value"]["json"] for event in events if event["kind"] == "official_world_output")
    wire = b"".join(base64.b64decode(event["value"]["base64"]) for event in events
                    if event["kind"] == "wire_bytes" and event["value"]["direction"] == "host_to_controller").decode()
    hidden = ["official-metadata", "private-presentation-id", "private-voice-path", "private-line-id", "private-source-id"]
    assert all(marker in official and marker not in wire for marker in hidden)
    assert "neutral-visible-text" in wire
    write_json(RESULTS / "observer-separation.json", {"status": "PASS", "scope": "object/API/serialized-wire separation",
        "official_dto_metadata_retained_by_observer": hidden, "all_markers_absent_from_controller_wire": True,
        "observer_path_passed_to_child": False, "controller_root_session_handle": False,
        "filesystem_read_prevention": "NOT_ENFORCED; see permission-detection.json"})
    oversized = [json.loads(line) for line in (RESULTS / "18_process_oversized.jsonl").read_text(encoding="utf-8").splitlines()]
    captured = b"".join(base64.b64decode(event["value"]["base64"]) for event in oversized
                        if event["kind"] == "wire_bytes" and event["value"]["direction"]
                        in ("controller_to_host", "controller_stdout_surplus"))
    emitted = b"x" * 20000 + (b"\r\n" if os.name == "nt" else b"\n")
    assert captured == emitted
    assert not any(event["kind"] == "official_input" for event in oversized)
    write_json(RESULTS / "wire-log-integrity.json", {"status": "PASS", "oversized_reply_bytes_emitted": len(emitted),
        "oversized_reply_bytes_captured": len(captured), "accepted_official_inputs": 0,
        "meaning": "Prefix rejected at limit; terminated-process stdout surplus preserves remaining emitted bytes."})


def main():
    RESULTS.mkdir(exist_ok=True)
    existing = [path for path in RESULTS.iterdir() if path.is_file()]
    if existing:
        index = 1
        while (RESULTS / ("archived-attempt-" + str(index))).exists():
            index += 1
        archive = RESULTS / ("archived-attempt-" + str(index))
        archive.mkdir()
        for path in existing:
            shutil.copyfile(path, archive / path.name)
    write_json(RESULTS / "summary.json", {"status": "RUNNING", "mode": "DETECTION_ONLY"})
    head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=REPO, capture_output=True, check=True).stdout.decode().strip()
    write_json(RESULTS / "run-context.json", {"declared_source_baseline": EXPECTED_HEAD, "observed_head": head,
        "baseline_matches_at_execution": head == EXPECTED_HEAD, "design_and_story_sources_accessed_by_test_tool": False})
    extract_public_declarations()
    python_count = controller_contract_tests()
    permission_detection()
    run_logged("build", ["dotnet", "build", str(HERE / "V00Boundary.csproj"), "--nologo"])
    run_logged("boundary-suite", ["dotnet", "run", "--no-build", "--project", str(HERE / "V00Boundary.csproj"), "--",
                                str(RESULTS), sys.executable, str(HERE / "controller.py")], timeout=60)
    verify_observer_separation()
    csharp = json.loads((RESULTS / "csharp-results.json").read_text(encoding="utf-8"))
    write_json(RESULTS / "summary.json", {"status": "PASS_WITH_LIMITATIONS", "baseline": EXPECTED_HEAD, "mode": "DETECTION_ONLY",
        "python_contract_cases": python_count, "csharp_boundary_cases": csharp["passed"], "failed": csharp["failed"],
        "official_dto_declaration_compile": "PASS", "production_engine_connection": "NOT_VERIFIED",
        "formal_autonomous_playthrough": "NOT_PERFORMED", "acceptance_level_1": "NOT_PERFORMED",
        "acceptance_level_2": "NOT_PERFORMED", "acceptance_level_3": "NOT_PERFORMED",
        "filesystem_isolation": "NOT_ENFORCED; positive harmless external-cwd read recorded",
        "source_scope_deviation": "See dto-provenance.json; initial read included neighboring generic mechanisms"})
    print(json.dumps({"python_cases": python_count, "csharp_cases": csharp["passed"], "failed": csharp["failed"], "mode": "DETECTION_ONLY"}))


if __name__ == "__main__":
    try:
        main()
    except Exception:
        import traceback
        RESULTS.mkdir(exist_ok=True)
        index = 1
        while (RESULTS / ("verification-failure-" + str(index) + ".log")).exists():
            index += 1
        (RESULTS / ("verification-failure-" + str(index) + ".log")).write_bytes(traceback.format_exc().encode("utf-8"))
        write_json(RESULTS / "summary.json", {"status": "FAILED", "mode": "DETECTION_ONLY",
            "failure_evidence": "verification-failure-" + str(index) + ".log"})
        raise
