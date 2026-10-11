"""Independent tooling: preserves raw subprocess bytes; does not build production or run games."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
SEALED = ROOT / "artifacts/partial-bootstrap-core/engineering-20261012-r1"


def verify():
    sealed = json.loads((SEALED / "source-sha256.json").read_text(encoding="utf-8"))
    actual = {
        str(path.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted((ROOT / "language_core").rglob("*"))
        if path.is_file() and not {"bin", "obj", "__pycache__"}.intersection(path.relative_to(ROOT / "language_core").parts)
    }
    assembly = json.loads((SEALED / "execution.json").read_text(encoding="utf-8"))["assemblies"]
    assembly_actual = {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in assembly}
    return {"source_same": actual == sealed, "source_sha256": actual,
            "assembly_same": assembly_actual == assembly, "assembly_sha256": assembly_actual}


def execute(name, arguments):
    result = subprocess.run(arguments, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    (OUT / f"{name}.stdout.txt").write_bytes(result.stdout)
    (OUT / f"{name}.stderr.txt").write_bytes(result.stderr)
    print(json.dumps({"name": name, "command": arguments, "exit_code": result.returncode}, ensure_ascii=False))
    print(result.stdout.decode("utf-8", errors="replace"))
    print(result.stderr.decode("utf-8", errors="replace"))
    return {"name": name, "command": arguments, "exit_code": result.returncode}


before = verify()
(OUT / "fingerprint-before.json").write_text(json.dumps(before, indent=2), encoding="utf-8")
if not before["source_same"] or not before["assembly_same"]:
    raise SystemExit("Frozen sources/assemblies changed before review")
cli = ["dotnet", str(ROOT / "language_core/Cli/bin/Release/net8.0/Language.Cli.dll")]
cases = [
    ("functions", "(let* (f (fn* (x) x) g (fn* (x) x)) (list (fn? f) (native? f) (list? f) (= f g) (= f f) (apply f '(7))))"),
    ("truth", "(list (if nil 1 2) (if false 1 2) (if 'unknown 1 2) (t-not 'unknown) (t-all '(true unknown)) (t-any '(false unknown)))"),
    ("malformed", "(let* (x) x)"),
    ("unsupported", "(quasiquote (a b))"),
    ("unknown-numeric", "(prepare '(change (ref \"x\") missing 1) '((\"x\" ((exists true) (tags ())))) '())"),
    ("unknown-when", "(prepare '(when (tagged (ref \"x\") missing 1) (set (ref \"x\") flag 1)) '((\"x\" ((exists true) (tags ((flag 0)))))) '())"),
]
results = []
for case, expression in cases:
    for mode in ("eval", "self"):
        results.append(execute(f"cli-{case}-{mode}", cli + [mode, expression]))
build = execute("harness-build", ["dotnet", "build", str(OUT / "ReviewHarness.csproj"), "-c", "Release", "--disable-build-servers"])
results.append(build)
if build["exit_code"] == 0:
    results.append(execute("harness", ["dotnet", str(OUT / "bin/Release/net8.0/ReviewHarness.dll")]))
after = verify()
(OUT / "fingerprint-after.json").write_text(json.dumps(after, indent=2), encoding="utf-8")
(OUT / "execution.json").write_text(json.dumps({"results": results, "scope": "generic language/host review; no game acceptance",
    "source_same": after["source_same"], "assembly_same": after["assembly_same"]}, indent=2), encoding="utf-8")
if not after["source_same"] or not after["assembly_same"]:
    raise SystemExit("Frozen sources/assemblies changed during review")
