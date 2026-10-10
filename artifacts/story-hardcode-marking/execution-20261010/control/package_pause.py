"""Freeze and verify this documentation-only pause package; no runtime tests."""
import ast
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[4]
TASK = ROOT / "artifacts/story-hardcode-marking/execution-20261010"
MANIFEST = TASK / "pause-manifest.json"
DIRS = ("C01B-Contract", "C01B-Consumer", "M06a-T1-Plan", "Stress-Plan")
SINGLE = ("approval-record.md", "control/contracts-phase02.md", "PAUSED.md",
          "phase02-state.json", "control/package_pause.py")
PRESERVED = (
    "artifacts/story-hardcode-marking/solution-review.md",
    "artifacts/story-hardcode-marking/solution-review-multiagent-pack.zip",
    "godot_csharp/scenes/Awakening/Script/generated_lines_zh.scene.translation",
    "godot_csharp/scenes/Awakening/Script/generated_lines_zh.speaker.translation",
    "godot_csharp/scenes/Awakening/Script/generated_lines_zh.zh.translation",
)


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def relative(path):
    return path.relative_to(ROOT).as_posix()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def strict_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def parse_json(data):
    return json.loads(data, object_pairs_hook=strict_object)


def package_paths():
    paths = [ROOT / "docs/design-standard.md"]
    paths += [TASK / name for name in SINGLE]
    for name in DIRS:
        paths += [p for p in (TASK / name).rglob("*") if p.is_file()]
    assert all(p.is_file() and not p.is_symlink() for p in paths)
    assert not any(p.name == ".git" or ".git" in p.relative_to(TASK).parts
                   for p in paths if p.is_relative_to(TASK))
    return sorted(paths, key=relative)


def freeze():
    paths = package_paths()
    checks = []
    files = []
    for path in paths:
        data = path.read_bytes()
        files.append({"path": relative(path), "bytes": len(data), "sha256": digest(data)})
        if path.suffix == ".json":
            parse_json(data.decode("utf-8-sig"))
            checks.append({"path": relative(path), "check": "JSON_PARSE", "result": "PASS"})
        elif path.suffix == ".jsonl":
            for line in data.decode("utf-8-sig").splitlines():
                if line.strip():
                    parse_json(line)
            checks.append({"path": relative(path), "check": "JSONL_PARSE", "result": "PASS"})
        elif path.suffix == ".py":
            ast.parse(data.decode("utf-8-sig"), filename=relative(path))
            checks.append({"path": relative(path), "check": "PYTHON_SYNTAX_ONLY", "result": "PASS"})
    assert git("diff", "--cached", "--name-only") == b"", "unexpected pre-existing index changes"
    result = {
        "schema": "solution-review.pause-manifest.v1", "status": "PAUSED_BY_USER",
        "date": "2026-10-10", "base_sha": git("rev-parse", "HEAD").decode().strip(),
        "classification": "DETECTION_ONLY", "files": files,
        "manifest_excludes_its_own_hash": True, "checks": checks,
        "runtime_validation": "NOT_RUN_DOCUMENTATION_ONLY",
        "preserved_outside_commit": [
            {"path": p, "sha256": digest((ROOT / p).read_bytes())} for p in PRESERVED
        ],
    }
    MANIFEST.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Frozen {len(files)} files; {len(checks)} static format checks passed.")


def verify():
    record = parse_json(MANIFEST.read_text(encoding="utf-8"))
    expected = {f["path"]: f["sha256"] for f in record["files"]}
    assert set(expected) == {relative(p) for p in package_paths()}, "package inventory drift"
    expected[relative(MANIFEST)] = digest(MANIFEST.read_bytes())
    staged = {p.decode("utf-8") for p in git("diff", "--cached", "--name-only", "-z").split(b"\0") if p}
    assert staged == set(expected), f"unexpected staged paths: {staged ^ set(expected)}"
    for path, checksum in expected.items():
        assert digest((ROOT / path).read_bytes()) == checksum, f"working bytes drift: {path}"
        assert digest(git("show", f":{path}")) == checksum, f"index bytes drift: {path}"
    for item in record["preserved_outside_commit"]:
        assert digest((ROOT / item["path"]).read_bytes()) == item["sha256"], item["path"]
        assert item["path"] not in staged
    print(f"PASS: {len(staged)} exact staged files; raw bytes match; all 5 existing input changes preserved.")


if __name__ == "__main__":
    {"freeze": freeze, "verify": verify}[sys.argv[1]]()
