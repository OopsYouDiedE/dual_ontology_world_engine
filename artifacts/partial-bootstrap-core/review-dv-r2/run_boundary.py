"""Additional bounded snapshot/control counterexamples against the same frozen DLL."""
from run_review import inspect, run, save_json


def main():
    before = inspect()
    save_json("boundary-fingerprint-before.json", before)
    if before["source_differences"] or before["assembly_differences"]:
        raise RuntimeError("r2 freeze mismatch")
    project = "artifacts/partial-bootstrap-core/review-dv-r2/boundary/BoundaryReview.csproj"
    commands = [run("boundary-build", ["dotnet", "build", project, "-c", "Release", "--disable-build-servers"])]
    if not commands[-1]["exit_code"]:
        commands.append(run("boundary", ["dotnet", "run", "--project", project, "-c", "Release", "--no-build"]))
    after = inspect()
    save_json("fingerprint-final.json", after)
    save_json("boundary-execution.json", {"commands": commands,
        "source_unchanged": before["sources"] == after["sources"],
        "production_assemblies_unchanged": before["assemblies"] == after["assemblies"]})
    return commands[-1]["exit_code"]


if __name__ == "__main__":
    raise SystemExit(main())
