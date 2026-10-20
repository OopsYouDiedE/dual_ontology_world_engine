"""Editor-facing diagnostics from the real DV compiler, without executing content."""
import argparse
import json
from pathlib import Path
import re
import sys
import tempfile

from dv_toolchain import LANGUAGE, compiler, run, sources as resolve_sources


def project_sources(focus, project=None):
    if project is None:
        project = next((p / "dv-check.json" for p in focus.parents
                        if (p / "dv-check.json").is_file()), None)
    if project is None:
        return [focus]
    project = project.resolve()
    data = json.loads(project.read_text(encoding="utf-8"))
    for group in data["groups"]:
        if "entry" in group:
            selected = [p for p, _ in resolve_sources([project.parent / group["entry"]])]
            if focus in selected:
                return [project.parent / group["entry"]]
            continue
        selected = [p.resolve() for pattern in group["sources"]
                    for p in sorted(project.parent.glob(pattern))]
        if focus in selected:
            return list(dict.fromkeys(selected))
    return [focus]


def check(focus, project=None, snapshot=None):
    paths = project_sources(focus, project)
    chunks, ranges, source_lines = [], [], []
    offset, line = 0, 1
    resolved = resolve_sources(paths, {focus: snapshot} if snapshot is not None else None)
    for path, source in resolved:
        length = len(source.encode("utf-16-le")) // 2
        chunks.append(source)
        ranges.append([offset, length])
        source_lines.append((line, line + source.count("\n"), path))
        offset += length
        line += source.count("\n")
    executable = compiler()
    temporary_root = LANGUAGE / "obj" / "dv-check"
    temporary_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="request-", dir=temporary_root) as temporary:
        work = Path(temporary)
        bundle = work / "sources.dv"
        bundle.write_text("".join(chunks), encoding="utf-8", newline="\n")
        boundaries = work / "ranges.json"
        boundaries.write_text(json.dumps(ranges), encoding="utf-8")
        result = run(["dotnet", executable, bundle, work / "unused.cs", "--check", boundaries], timeout=180)
        diagnostics = []
        if result.returncode:
            raw = result.stderr or result.stdout
            location = re.search(re.escape(str(bundle)) + r":(\d+):(\d+): ([^\r\n]+)", raw)
            if location:
                row, column = int(location[1]), int(location[2])
                start, _, original = next((entry for entry in source_lines if entry[0] <= row < entry[1]), source_lines[-1])
                diagnostics.append(dict(path=str(original), line=row-start+1, column=column,
                                        severity="error", message=location[3]))
            else:
                diagnostics.append(dict(path=str(focus), line=1, column=1, severity="error",
                                        message=raw.strip().splitlines()[0] if raw.strip() else "Compiler exited without diagnostics"))
        return dict(success=result.returncode == 0, diagnostics=diagnostics,
                    sources=[str(p) for p, _ in resolved], compiler=str(executable),
                    raw=result.stderr, scope="syntax, names, calls and implemented type constraints; content is not executed")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("file", type=Path)
    parser.add_argument("--format", choices=("json", "text"), default="text")
    parser.add_argument("--project", type=Path)
    parser.add_argument("--stdin", action="store_true", help="Check unsaved text for file read from stdin")
    args = parser.parse_args()
    focus = args.file.resolve()
    try:
        result = check(focus, args.project, sys.stdin.read() if args.stdin else None)
    except Exception as error:
        result = dict(success=False, diagnostics=[dict(path=str(focus), line=1, column=1,
                      severity="error", message=str(error))], scope="tool failure")
    if args.format == "json":
        print(json.dumps(result, ensure_ascii=False))
    elif result["success"]:
        print("DV compiler check complete (content not executed)")
    else:
        for item in result["diagnostics"]:
            print(f'{item["path"]}:{item["line"]}:{item["column"]}: {item["severity"]}: {item["message"]}')
    return 0 if result["success"] else 1


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stdin.reconfigure(encoding="utf-8")
    sys.exit(main())
