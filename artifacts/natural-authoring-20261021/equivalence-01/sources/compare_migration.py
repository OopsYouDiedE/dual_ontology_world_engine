#!/usr/bin/env python3
"""Compare migrated data declarations through two independently compiled DLLs.

No formal session, world execution, action completion, or oracle is invoked.
Generated C# is copied byte-for-byte into a new evidence directory and linked
into separate Library projects with the same Runtime.cs. The runtime values are
serialized by each DLL's own R.json_stringify and compared as JSON elements.
"""
from __future__ import annotations

import argparse
import difflib
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import traceback
from xml.sax.saxutils import escape


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
HEADER = re.compile(r"(?m)^[ \t]*static object F_(\w+)\(([^\r\n)]*)\)\s*(=>|\{)")
DV_HEADER = re.compile(r"^(?:fn|axiom) (\w+)\(([^)]*)\)")
CS_STRING = re.compile(r'"(?:\\.|[^"\\])*"', re.DOTALL)
GENERATED_OFFSET = re.compile(r"(?P<prefix>v_\w+__(?:comp_)?|comp_(?:source|result|i)_)(?P<offset>\d+)$")
IDENTIFIER = re.compile(r"(?:[^\W\d]|_)\w*", re.UNICODE)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, data: object) -> None:
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def tokens(source: str, start_index: int = 0):
    """A lexical scanner for compiler-produced C#, not a replacement C# parser."""
    index = start_index
    while index < len(source):
        char = source[index]
        if char.isspace():
            index += 1
            continue
        if source.startswith("//", index):
            end = source.find("\n", index)
            index = len(source) if end == -1 else end + 1
            continue
        if source.startswith("/*", index):
            end = source.find("*/", index + 2)
            if end == -1:
                raise ValueError("Unclosed generated C# comment")
            index = end + 2
            continue
        start = index
        verbatim = source.startswith('@"', index)
        if char in ('"', "'") or verbatim:
            delimiter = '"' if verbatim else char
            index += 2 if verbatim else 1
            while index < len(source):
                if source[index] == delimiter:
                    if verbatim and index + 1 < len(source) and source[index + 1] == delimiter:
                        index += 2
                        continue
                    index += 1
                    yield "literal", source[start:index], start, index
                    break
                if not verbatim and source[index] == "\\":
                    index += 2
                else:
                    index += 1
            else:
                raise ValueError("Unclosed generated C# literal")
            continue
        matched = IDENTIFIER.match(source, index)
        if matched:
            index = matched.end()
            yield "identifier", matched.group(), start, index
            continue
        index += 1
        yield "punctuation", char, start, index


def functions(source: str) -> dict[str, dict]:
    result = {}
    for match in HEADER.finditer(source):
        name, parameters, form = match.groups()
        if name in result:
            raise ValueError(f"Duplicate generated function F_{name}")
        opening = match.end() - 1 if form == "{" else match.end()
        depth = 0
        end = None
        for kind, token, start, stop in tokens(source, opening):
            if kind != "punctuation":
                continue
            if token in "([{":
                depth += 1
            elif token in ")]}":
                depth -= 1
                if form == "{" and depth == 0:
                    end = stop
                    break
            elif token == ";" and form == "=>" and depth == 0:
                end = stop
                break
        if end is None:
            raise ValueError(f"Could not locate the end of generated function F_{name}")
        params = []
        if parameters.strip():
            for parameter in parameters.split(","):
                param = re.fullmatch(r"\s*object\s+v_(\w+)\s*", parameter)
                if param is None:
                    raise ValueError(f"Unexpected generated parameter: {name}: {parameter}")
                params.append(param[1])
        result[name] = {"source": source[match.start():end], "parameters": params}
    if not result:
        raise ValueError("Generated C# contains no F_* declarations")
    return result


def canonical(source: str) -> str:
    # The compiler deliberately suffixes local names with UTF-16 source offsets.
    # Rebase those identities in first-use order within each function, preserving
    # every identifier prefix and every distinct identity/reference relationship.
    # Do not normalize string contents, numeric values, or parameter names.
    offsets, output = {}, []
    for kind, token, _, _ in tokens(source):
        matched = GENERATED_OFFSET.fullmatch(token) if kind == "identifier" else None
        if matched:
            ordinal = offsets.setdefault(matched["offset"], len(offsets))
            token = matched["prefix"] + f"offset{ordinal}"
        output.append(token)
    return "\x1f".join(output)


def affected_functions(manifests: list[Path], before: dict[str, dict]) -> dict[str, dict]:
    affected = {}
    for manifest in manifests:
        payload = json.loads(manifest.read_text(encoding="utf-8"))
        for file in payload["files"]:
            for change in file["changes"]:
                name = change["name"]
                if name not in before:
                    raise ValueError(f"Migration manifest function missing in baseline: {name}")
                item = affected.setdefault(name, {"name": name, "kinds": [], "parameters": None,
                                                  "source_file": file["path"], "changes": []})
                kind = change.get("kind", "rule")
                if kind not in item["kinds"]:
                    item["kinds"].append(kind)
                item["changes"].append({"manifest": manifest.name, "line": change["line"]})
                header = DV_HEADER.match(change["before"])
                if header:
                    item["parameters"] = [p.strip() for p in header[2].split(",") if p.strip()]
    for name, item in affected.items():
        original = before[name]["parameters"]
        if item["parameters"] is None:
            item["parameters"] = original
        if item["parameters"] != original:
            raise ValueError(f"Baseline parameter names disagree with original declaration: {name}")
    return affected


def literal(value):
    if isinstance(value, bool):
        return {"kind": "bool", "value": value}
    if isinstance(value, (int, float)):
        return {"kind": "number", "value": value}
    return {"kind": "text", "value": value}


DEFAULTS = {
    "actor": "detective", "subject": "detective", "question": "question", "evidence": "E2",
    "room": "hall", "knowledge": "cognition", "history": "history", "origin": "corridor",
    "target": "huo", "requester": "detective", "speaker": "huo", "listener": "su",
    "ritual": "ritual", "sole": "PoloBoot", "direction": "Bolt", "result_field": "predicted_boots_bolt",
    "words": "BoltKeepShut", "tail": "OpenSaveMe", "su_room": "R08", "huo_room": "hall",
    "source": "Tube", "choice": "accept-huo", "intent": "latch-request", "permitted_intent": "latch-request",
    "atom": "words_bolt", "polarity": True, "variable": "words", "value": "BoltKeepShut",
    "su_report": True, "heard": "Huo-relay-and-Su-report", "preference": "huo", "huo_trust": 4,
    "su_trust": 3, "expected": 4, "result": 4, "priority": 3, "group": "contract",
    "id": "detective", "minutes": 5, "sender": "huo", "accepted": True, "purpose": "patient",
    "method": "check-evidence", "person": "quer", "stage": "prompt", "field": "wedge_set",
}
FALLBACK_BRANCHES = {
    "c1_question_text": "trust", "c1_other_introduction": "margaret",
    "c1_other_handoff_text": "margaret", "c1_other_report_surface": "logistics",
    "c1_op_wake_lines": "su", "c2_work_purpose": "support_active", "c2h_objection": "lucian",
}


def argument(name: str, parameter: str):
    if parameter == "row":
        return {"kind": "data-item", "function": "c1n_rows", "index": 0}
    if parameter == "norm":
        return {"kind": "data-item", "function": "c2_norms", "index": 0}
    if parameter not in DEFAULTS:
        raise ValueError(f"No explicit constructor fixture for {name} parameter {parameter}")
    value = DEFAULTS[parameter]
    if name in {"c1_move", "c1_hist_travel"} and parameter == "target":
        value = "hall"
    if name == "c1_deep_resonance_law" and parameter == "actor":
        value = "huo"
    if name in {"c2_propose_law", "c2_post_law"} and parameter == "actor":
        value = "ruan"
    if name == "c2i_evening_choice_law":
        if parameter == "actor":
            value = "moran"
        elif parameter == "value":
            value = "stay"
    return literal(value)


def plan_cases(affected: dict[str, dict], before: dict[str, dict]) -> list[dict]:
    cases = []
    for name, item in sorted(affected.items()):
        params = item["parameters"]
        base_args = [argument(name, p) for p in params]
        if any(kind.endswith("-return") for kind in item["kinds"]):
            if len(params) != 1 or name not in FALLBACK_BRANCHES:
                raise ValueError(f"A conditional prose function requires explicit branch fixtures: {name}")
            choices = []
            # The affected prose selectors use ASCII IDs in conditions and
            # Chinese prose in their returns. Read IDs from the baseline body;
            # exclude the prose, numbers, and empty strings, then add the known
            # legal fallback ID explicitly. This never guesses hidden game state.
            for match in CS_STRING.finditer(before[name]["source"]):
                value = json.loads(match.group())
                if re.fullmatch(r"[A-Za-z][A-Za-z0-9_.:-]*", value) and value not in choices:
                    choices.append(value)
            fallback = FALLBACK_BRANCHES[name]
            if fallback not in choices:
                choices.append(fallback)
            for value in choices:
                cases.append({"function": name, "label": "selector:" + value,
                              "arguments": [literal(value)], "kinds": item["kinds"]})
        else:
            cases.append({"function": name, "label": "declaration", "arguments": base_args,
                          "kinds": item["kinds"]})
            for index, parameter in enumerate(params):
                if parameter in {"accepted", "polarity", "su_report"}:
                    alternative = list(base_args)
                    alternative[index] = literal(False)
                    cases.append({"function": name, "label": parameter + ":false",
                                  "arguments": alternative, "kinds": item["kinds"]})
            if name == "c1_deep_resonance_law":
                for actor in ("shen", "su"):
                    cases.append({"function": name, "label": "actor:" + actor,
                                  "arguments": [literal(actor)], "kinds": item["kinds"]})
            if name == "c1_evidence_contact":
                cases.append({"function": name, "label": "evidence:E11",
                              "arguments": [literal("detective"), literal("E11")], "kinds": item["kinds"]})
    for index, case in enumerate(cases):
        case["id"] = f"declaration-{index + 1:04d}"
    return cases


DRIVER = r'''using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static object Invoke(Type type, string name, object[] arguments)
    {
        var method = type.GetMethod(name, Flags) ?? throw new MissingMethodException(type.FullName, name);
        return method.Invoke(null, arguments) ?? throw new InvalidOperationException(name + " returned null");
    }
    private static object Argument(Assembly assembly, JsonElement specification)
    {
        string kind = specification.GetProperty("kind").GetString()!;
        if (kind == "text") return specification.GetProperty("value").GetString()!;
        if (kind == "bool") return specification.GetProperty("value").GetBoolean();
        Type runtime = assembly.GetType("DuelView.Compiled.R", true)!;
        if (kind == "number") return Invoke(runtime, "number", new object[] { specification.GetProperty("value").GetRawText() });
        if (kind == "data-item")
        {
            string name = specification.GetProperty("function").GetString()!;
            if (name != "c1n_rows" && name != "c2_norms") throw new InvalidOperationException("Unapproved fixture provider " + name);
            object list = Invoke(assembly.GetType("DVProgram", true)!, "F_" + name, Array.Empty<object>());
            double index = specification.GetProperty("index").GetDouble();
            return Invoke(runtime, "at", new object[] { list, index });
        }
        throw new InvalidOperationException("Unknown argument fixture " + kind);
    }
    private static string Evaluate(Assembly assembly, JsonElement specification)
    {
        string name = specification.GetProperty("function").GetString()!;
        if (!name.StartsWith("c1", StringComparison.Ordinal) && !name.StartsWith("c2", StringComparison.Ordinal))
            throw new InvalidOperationException("Only listed chapter data declarations may be evaluated");
        object[] arguments = specification.GetProperty("arguments").EnumerateArray().Select(x => Argument(assembly, x)).ToArray();
        object value = Invoke(assembly.GetType("DVProgram", true)!, "F_" + name, arguments);
        return (string)Invoke(assembly.GetType("DuelView.Compiled.R", true)!, "json_stringify", new object[] { value });
    }
    private static string? Difference(JsonElement left, JsonElement right, string path)
    {
        if (left.ValueKind != right.ValueKind) return path + ": JSON kinds differ";
        if (left.ValueKind == JsonValueKind.Array)
        {
            if (left.GetArrayLength() != right.GetArrayLength()) return path + ": array lengths differ";
            for (int i = 0; i < left.GetArrayLength(); i++)
            {
                string? mismatch = Difference(left[i], right[i], path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                if (mismatch != null) return mismatch;
            }
            return null;
        }
        if (left.ValueKind == JsonValueKind.Object)
        {
            var a = left.EnumerateObject().ToArray(); var b = right.EnumerateObject().ToArray();
            if (a.Length != b.Length) return path + ": object lengths differ";
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Name != b[i].Name) return path + ": property names or order differ";
                string? mismatch = Difference(a[i].Value, b[i].Value, path + "." + a[i].Name);
                if (mismatch != null) return mismatch;
            }
            return null;
        }
        bool same = left.ValueKind switch
        {
            JsonValueKind.String => left.GetString() == right.GetString(),
            // Both values use the identical serializer. Compare its exact number
            // representation so large integers cannot collapse through double.
            JsonValueKind.Number => left.GetRawText() == right.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText()
        };
        return same ? null : path + ": values differ";
    }
    private static Dictionary<string, string> ExceptionRecord(Exception error)
    {
        while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
        return new() { ["type"] = error.GetType().FullName ?? "Exception", ["message"] = error.Message,
                       ["details"] = error.ToString() };
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static void WriteNew(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }
    private static int Main(string[] args)
    {
        Assembly before = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        Assembly after = Assembly.LoadFrom(Path.GetFullPath(args[1]));
        using JsonDocument plan = JsonDocument.Parse(File.ReadAllText(args[2]));
        string output = args[3];
        var results = new List<Dictionary<string, object?>>();
        bool failure = false;
        foreach (JsonElement specification in plan.RootElement.GetProperty("cases").EnumerateArray())
        {
            string id = specification.GetProperty("id").GetString()!;
            var result = new Dictionary<string, object?> {
                ["id"] = id, ["function"] = specification.GetProperty("function").GetString(),
                ["label"] = specification.GetProperty("label").GetString() };
            string? left = null, right = null;
            try { left = Evaluate(before, specification); }
            catch (Exception error) { result["before_exception"] = ExceptionRecord(error); }
            try { right = Evaluate(after, specification); }
            catch (Exception error) { result["after_exception"] = ExceptionRecord(error); }
            if (left == null || right == null)
            {
                result["status"] = "exception"; failure = true;
            }
            else
            {
                result["before_sha256"] = Hash(left); result["after_sha256"] = Hash(right);
                using JsonDocument a = JsonDocument.Parse(left, new JsonDocumentOptions { MaxDepth = 256 });
                using JsonDocument b = JsonDocument.Parse(right, new JsonDocumentOptions { MaxDepth = 256 });
                string? mismatch = Difference(a.RootElement, b.RootElement, "$");
                result["status"] = mismatch == null ? "equal" : "different";
                if (mismatch != null)
                {
                    result["difference"] = mismatch;
                    WriteNew(Path.Combine(output, id + ".before.txt"), left);
                    WriteNew(Path.Combine(output, id + ".after.txt"), right);
                    failure = true;
                }
            }
            results.Add(result);
        }
        WriteNew(Path.Combine(output, "declaration-results.json"), JsonSerializer.Serialize(results, Options) + "\n");
        Console.WriteLine(failure ? "Declaration comparison has differences or uncovered exceptions; inspect declaration-results.json."
                                  : "Every planned declaration returned equivalent serialized structure and text.");
        return failure ? 1 : 0;
    }
}
'''


def project(assembly: str, includes: list[Path], output_type: str) -> str:
    entries = "\n".join(f'<Compile Include="{escape(str(p), {chr(34): "&quot;"})}" Link="source{i}.cs" />'
                        for i, p in enumerate(includes))
    return f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net8.0</TargetFramework><OutputType>{output_type}</OutputType>
<AssemblyName>{assembly}</AssemblyName><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<UseSharedCompilation>false</UseSharedCompilation><NuGetAudit>false</NuGetAudit></PropertyGroup>
<ItemGroup>{entries}</ItemGroup></Project>\n'''


class Recorder:
    def __init__(self, output: Path, timeout: float):
        self.output, self.timeout, self.index = output, timeout, 0
        self.directory = output / "commands"
        self.directory.mkdir()
        self.environment = os.environ.copy()
        self.environment.setdefault("DOTNET_CLI_HOME", "/tmp/dv-cli" if os.name != "nt" else str(output / "dotnet-home"))
        self.environment.setdefault("NUGET_PACKAGES", "/tmp/dv-nuget" if os.name != "nt" else str(output / "nuget-packages"))
        self.environment.update(DOTNET_NOLOGO="1", DOTNET_CLI_UI_LANGUAGE="en",
                                DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", NuGetAudit="false")

    def run(self, command: list[str | Path]) -> subprocess.CompletedProcess:
        self.index += 1
        stem = self.directory / f"{self.index:03d}"
        argv = [str(arg) for arg in command]
        write_json(stem.with_suffix(".command.json"), {"argv": argv, "cwd": str(self.output), "timeout_seconds": self.timeout})
        flags = {"start_new_session": True} if os.name != "nt" else {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP}
        process = subprocess.Popen(argv, cwd=self.output, env=self.environment,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, **flags)
        timed_out = False
        try:
            stdout, stderr = process.communicate(timeout=self.timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            if os.name == "nt":
                subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True, check=False)
            else:
                os.killpg(process.pid, signal.SIGKILL)
            stdout, stderr = process.communicate()
        stem.with_suffix(".stdout.log").write_bytes(stdout)
        stem.with_suffix(".stderr.log").write_bytes(stderr)
        write_json(stem.with_suffix(".result.json"), {"returncode": process.returncode, "timed_out": timed_out,
                                                     "stdout_sha256": digest(stdout), "stderr_sha256": digest(stderr)})
        if timed_out:
            raise TimeoutError(f"Command {self.index} timed out; original output is retained")
        return subprocess.CompletedProcess(argv, process.returncode, stdout, stderr)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", type=Path, required=True)
    parser.add_argument("--after", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True, help="A new evidence directory; existing directories are rejected")
    parser.add_argument("--runtime", type=Path, default=ROOT / "language_core/compiler/Runtime.cs")
    parser.add_argument("--dotnet", default=shutil.which("dotnet") or "/tmp/dv-dotnet/dotnet")
    parser.add_argument("--timeout", type=float, default=180)
    args = parser.parse_args()
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)
    summary = {"kind": "engineering-declaration-equivalence", "status": "incomplete",
               "scope": "Data constructors and authored text only; no world execution or formal session",
               "limitations": ["Finite constructor fixtures do not prove arbitrary-input semantic equivalence",
                               "JSON comparison uses each independent DLL's identical Runtime serializer",
                               "Game behavior, timing, rendering, and player experience are not exercised"]}
    try:
        frozen = output / "sources"
        frozen.mkdir()
        sources = {"Before.cs": args.before.resolve(), "After.cs": args.after.resolve(), "Runtime.cs": args.runtime.resolve()}
        manifest_files = [HERE / "rule-migration.json", HERE / "prose-migration.json"]
        source_manifest = []
        for filename, source in sources.items():
            data = source.read_bytes()
            (frozen / filename).write_bytes(data)
            source_manifest.append({"path": str(source), "copy": "sources/" + filename, "sha256": digest(data)})
        for manifest in manifest_files:
            data = manifest.read_bytes()
            (frozen / manifest.name).write_bytes(data)
            source_manifest.append({"path": str(manifest), "copy": "sources/" + manifest.name, "sha256": digest(data)})
        write_json(output / "source-manifest.json", source_manifest)
        before = functions((frozen / "Before.cs").read_text(encoding="utf-8-sig"))
        after = functions((frozen / "After.cs").read_text(encoding="utf-8-sig"))
        affected = affected_functions([frozen / p.name for p in manifest_files], before)
        added, removed = sorted(after.keys() - before.keys()), sorted(before.keys() - after.keys())
        raw_changed, substantive = [], []
        for name in sorted(before.keys() & after.keys()):
            old, new = before[name]["source"], after[name]["source"]
            if old != new:
                raw_changed.append(name)
            if canonical(old) != canonical(new):
                substantive.append(name)
        unexpected = sorted(set(substantive) - affected.keys())
        with (output / "function-differences.diff").open("x", encoding="utf-8", newline="\n") as stream:
            for name in substantive:
                stream.writelines(difflib.unified_diff(
                    before[name]["source"].splitlines(keepends=True),
                    after[name]["source"].splitlines(keepends=True),
                    fromfile="before/F_" + name, tofile="after/F_" + name))
                stream.write("\n")
        comparison = {"raw_changed_functions": raw_changed, "substantively_changed_functions": substantive,
                      "added_functions": added, "removed_functions": removed,
                      "unexpected_changed_functions": unexpected, "allowed_migrated_functions": sorted(affected),
                      "normalization": "C# tokens, excluding comments/whitespace; only compiler source-offset suffixes rebased by identity",
                      "substantive_function_diff": "function-differences.diff"}
        write_json(output / "function-differences.json", comparison)
        summary["generated_function_scope"] = "unchanged-outside-migration" if not (added or removed or unexpected) else "unexpected-difference"
        if added or removed or unexpected:
            summary["status"] = "failed"
            summary["unexpected_functions"] = {"added": added, "removed": removed, "changed": unexpected}
            return 1
        cases = plan_cases(affected, before)
        write_json(output / "declaration-cases.json", {"cases": cases, "functions": affected})
        (output / "NuGet.Config").write_text('<configuration><packageSources><clear /></packageSources></configuration>\n', encoding="utf-8")
        recorder = Recorder(output, args.timeout)
        dlls = []
        for label in ("Before", "After"):
            directory = output / label.lower()
            directory.mkdir()
            csproj = directory / "Declarations.csproj"
            assembly = label + "Declarations"
            csproj.write_text(project(assembly, [frozen / (label + ".cs"), frozen / "Runtime.cs"], "Library"), encoding="utf-8")
            built = recorder.run([args.dotnet, "build", csproj, "-c", "Release", "--disable-build-servers", "-m:1", "-p:NuGetAudit=false"])
            if built.returncode:
                raise RuntimeError(f"{label} independent data library did not build; inspect commands/{recorder.index:03d}.*")
            dlls.append(directory / "bin/Release/net8.0" / (assembly + ".dll"))
        driver = output / "driver"
        driver.mkdir()
        (driver / "Program.cs").write_text(DRIVER, encoding="utf-8")
        csproj = driver / "Comparison.csproj"
        csproj.write_text(project("DeclarationComparison", [driver / "Program.cs"], "Exe"), encoding="utf-8")
        built = recorder.run([args.dotnet, "build", csproj, "-c", "Release", "--disable-build-servers", "-m:1", "-p:NuGetAudit=false"])
        if built.returncode:
            raise RuntimeError(f"Reflection comparison driver did not build; inspect commands/{recorder.index:03d}.*")
        result = recorder.run([args.dotnet, driver / "bin/Release/net8.0/DeclarationComparison.dll", *dlls,
                               output / "declaration-cases.json", output])
        result_path = output / "declaration-results.json"
        if not result_path.is_file():
            raise RuntimeError(f"Reflection comparison did not finish; inspect commands/{recorder.index:03d}.*")
        results = json.loads(result_path.read_text(encoding="utf-8"))
        covered = {item["function"] for item in results if item["status"] == "equal"}
        incomplete = sorted(affected.keys() - covered)
        problems = [{key: value for key, value in item.items() if not key.endswith("sha256")}
                    for item in results if item["status"] != "equal"]
        summary.update(status="equivalent-for-listed-fixtures" if result.returncode == 0 and not incomplete else "failed",
                       returned_equivalent_functions=sorted(covered), unverified_functions=incomplete,
                       differences_or_exceptions=problems, generated_function_scope="unchanged-outside-migration")
        if result.returncode == 0 and not incomplete:
            print("Generated functions outside the migration are unchanged after source-offset normalization.")
            print("All listed migrated data declarations and prose selectors returned equivalent structures and exact text.")
            print("No formal session, world execution, rendering, or performance measurement was run.")
            return 0
        print("Declaration equivalence is incomplete; inspect summary.json and declaration-results.json.", file=sys.stderr)
        return 1
    except Exception as error:
        summary.update(status="failed", error={"type": type(error).__name__, "message": str(error),
                                               "details": traceback.format_exc()})
        print(str(error), file=sys.stderr)
        return 1
    finally:
        write_json(output / "summary.json", summary)


if __name__ == "__main__":
    raise SystemExit(main())
