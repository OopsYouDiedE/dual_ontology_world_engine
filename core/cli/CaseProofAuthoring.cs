using DuelView.Dv;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DuelView.Core;

/// <summary>Maintained authoring command. Cases remain in DV; generated Lean and receipts stay in .runtime.</summary>
internal static class CaseProofAuthoring
{
    public static int Run(string[] arguments)
    {
        if (arguments.Length is < 3 or > 4)
            throw new ArgumentException("Usage: --verify-case-proofs <project-local-lean-executable> <.runtime-output-directory> [case-id]");
        var root = Path.GetFullPath(Environment.CurrentDirectory);
        var generatedRoot = Path.GetFullPath(Path.Combine(root, ".runtime")) + Path.DirectorySeparatorChar;
        var executable = Path.GetFullPath(arguments[1]);
        var output = Path.GetFullPath(arguments[2]);
        if (!executable.StartsWith(generatedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(executable))
            throw new ArgumentException("Use the verified project-local Lean executable.");
        if (!output.StartsWith(generatedRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Generated proofs must stay inside the project's .runtime directory.");
        Directory.CreateDirectory(output);
        var version = Invoke(executable, new[] { "--version" }, output);
        if (version.Code != 0) throw new InvalidOperationException("Lean did not report its version: " + version.Error);
        var results = new List<object>();
        var failed = 0;
        var leanChecked = 0;
        var rejectedBeforeExport = 0;
        var unexportedGoals = new List<object>();
        var caseFiles = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Content", "Cases"), "*.dv")
            .ToDictionary(x => Path.GetFileNameWithoutExtension(x), StringComparer.Ordinal);
        foreach (var file in caseFiles.Values.Order(StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file, Encoding.UTF8);
            var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
            var caseId = Path.GetFileNameWithoutExtension(file);
            var data = DvScriptReader.Parse(source).GetProperty("data");
            if (arguments.Length == 4 && caseId != arguments[3] &&
                !(data.TryGetProperty("case_history_reference_audit", out var relatedAudit) && relatedAudit.GetProperty("case_id").GetString() == arguments[3])) continue;
            var contexts = new List<(string Id, JsonElement History, JsonElement Request, string? Rejection)>();
            var historySourceHash = sourceHash;
            var sourceCaseId = caseId;
            if (data.TryGetProperty("case_history", out var history))
            {
                contexts.Add(("full", history, default, null));
                if (history.TryGetProperty("branches", out var branches))
                    foreach (var branch in branches.EnumerateArray())
                    {
                        var request = new CaseHistory.CheckRequest(
                            Events: branch.GetProperty("events").EnumerateArray().Select(x => x.GetString()!).ToArray(),
                            Goals: branch.GetProperty("goals").EnumerateArray().Select(x => x.GetString()!).ToArray());
                        contexts.Add((branch.GetProperty("id").GetString()!, CaseHistory.Project(history, request), branch, null));
                    }
            }
            if (data.TryGetProperty("case_history_reference_audit", out var historyAudit))
            {
                sourceCaseId = historyAudit.GetProperty("case_id").GetString()!;
                var referencedSource = File.ReadAllText(caseFiles[sourceCaseId], Encoding.UTF8);
                historySourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(referencedSource)));
                var referencedHistory = DvScriptReader.Parse(referencedSource).GetProperty("data").GetProperty("case_history");
                foreach (var mutation in historyAudit.GetProperty("mutations").EnumerateArray())
                {
                    if (!mutation.TryGetProperty("lean_obligation", out var obligation)) continue;
                    var target = obligation.GetString() switch
                    {
                        "schedule" => "scheduleValid", "transitions" => "runResult.1", "final" => "finalValid",
                        _ => throw new FormatException("Unknown Lean mutation obligation")
                    };
                    var mutatedHistory = referencedHistory;
                    if (mutation.TryGetProperty("lean_events", out var roots))
                        mutatedHistory = CaseHistory.Project(referencedHistory, new CaseHistory.CheckRequest(
                            Events: roots.EnumerateArray().Select(x => x.GetString()!).ToArray(),
                            Goals: mutation.TryGetProperty("lean_goals", out var goals) ? goals.EnumerateArray().Select(x => x.GetString()!).ToArray() : null));
                    var node = JsonNode.Parse(mutatedHistory.GetRawText())!.AsObject();
                    var actionId = mutation.GetProperty("action").GetString();
                    var action = node["actions"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == actionId)!;
                    foreach (var field in mutation.GetProperty("set").EnumerateObject()) action[field.Name] = JsonNode.Parse(field.Value.GetRawText());
                    contexts.Add((mutation.GetProperty("id").GetString()!, JsonSerializer.SerializeToElement(node), default, target));
                }
            }
            foreach (var context in contexts)
            {
                var module = "History_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(caseId + ":" + context.Id)));
                var exported = CaseHistoryLean.Export(context.History, module, context.Request, context.Rejection);
                var path = Path.Combine(output, module + ".lean");
                File.WriteAllText(path, exported.Source, new UTF8Encoding(false));
                var receipt = Invoke(executable, new[] { path }, output, 180000);
                leanChecked++;
                var passed = receipt.Code == 0 && !receipt.Output.Contains("sorryAx", StringComparison.Ordinal)
                    && !receipt.Output.Contains("declaration uses 'sorry'", StringComparison.Ordinal)
                    && !receipt.Error.Contains("declaration uses 'sorry'", StringComparison.Ordinal);
                if (!passed) failed++;
                Console.WriteLine(JsonSerializer.Serialize(new { case_id = caseId, context = context.Id, check = "lean4-computable-history", passed, exit_code = receipt.Code }));
                results.Add(new { case_id = caseId, source_case_id = sourceCaseId, id = "declared-history-transitions", context = context.Id, dv_sha256 = sourceHash, history_dv_sha256 = historySourceHash,
                    check = "lean4-computable-history", expected = context.Rejection is null ? "valid" : "rejected", rejection_target = context.Rejection, passed, generated_file = path,
                    generated_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exported.Source))),
                    exported.Scope, exported.Covered, exported.NotCovered, exported.Actors, exported.Events, exported.SelectedEvents,
                    active_actors = context.History.GetProperty("actions").EnumerateArray().Select(x => x.GetProperty("actor").GetString()).Distinct().Order().ToArray(),
                    start = context.History.GetProperty("start").GetInt32(), end = context.History.GetProperty("end").GetInt32(),
                    dependencies = context.History.TryGetProperty("projection", out var projection) ? (JsonElement?)projection.GetProperty("dependencies").Clone() : null,
                    exit_code = receipt.Code, stdout = receipt.Output, stderr = receipt.Error });
            }
            if (!data.TryGetProperty("natural_deduction_audit", out var audit)) continue;
            var index = 0;
            foreach (var request in audit.GetProperty("checks").EnumerateArray())
            {
                index++;
                var id = request.GetProperty("id").GetString()!;
                var expected = request.GetProperty("expect").GetString();
                var checkedProof = NaturalDeduction.Check(request);
                if (expected == "invalid")
                {
                    rejectedBeforeExport++;
                    if (checkedProof.Valid) failed++;
                    results.Add(new { case_id = caseId, id, dv_sha256 = sourceHash,
                        check = "csharp-rejection-before-export", passed = !checkedProof.Valid, errors = checkedProof.Errors });
                    continue;
                }
                if (expected != "valid" || !checkedProof.Valid)
                {
                    failed++;
                    results.Add(new { case_id = caseId, id, check = "csharp-proof", passed = false, errors = checkedProof.Errors });
                    continue;
                }
                // Encode file/module names independently of case text, which is never executable shell input.
                var stem = "Case_" + Convert.ToHexString(Encoding.UTF8.GetBytes(caseId)) + "_" + index;
                var leanSource = NaturalDeduction.ExportLean(request, stem);
                var path = Path.Combine(output, stem + ".lean");
                File.WriteAllText(path, leanSource, new UTF8Encoding(false));
                var receipt = Invoke(executable, new[] { path }, output);
                leanChecked++;
                var passed = receipt.Code == 0 && !receipt.Output.Contains("sorryAx", StringComparison.Ordinal)
                    && !receipt.Output.Contains("declaration uses 'sorry'", StringComparison.Ordinal)
                    && !receipt.Error.Contains("declaration uses 'sorry'", StringComparison.Ordinal);
                if (!passed) failed++;
                results.Add(new { case_id = caseId, id, dv_sha256 = sourceHash, check = "lean4-kernel",
                    passed, generated_file = path,
                    generated_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(leanSource))),
                    axioms_used_in_dv = checkedProof.AxiomsUsed, exit_code = receipt.Code,
                    stdout = receipt.Output, stderr = receipt.Error });
            }
            if (data.TryGetProperty("case_design", out _))
            {
                var theory = CaseTheory.Parse(source);
                foreach (var scope in theory.Scopes.Values)
                foreach (var theorem in scope.Theorems)
                {
                    var judgment = theory.Prove(scope.Id, theorem.Conclusion);
                    if (judgment.Status == "unknown")
                    {
                        unexportedGoals.Add(new { case_id = caseId, scope = scope.Id, goal = theorem.Id, status = "unknown" });
                        continue;
                    }
                    foreach (var sign in new[] { false, true })
                    {
                        if (sign ? judgment.Refutation is null : judgment.Support is null) continue;
                        var goal = sign ? theorem.Conclusion.Opposite : theorem.Conclusion;
                        var request = CaseLeanBridge.BuildRequest(theory, scope.Id, goal);
                        var stem = "Goal_" + Convert.ToHexString(Encoding.UTF8.GetBytes(caseId + ":" + scope.Id + ":" + theorem.Id)) + (sign ? "_negative" : "_positive");
                        // The module name is a short stable identifier; source identities stay in the receipt.
                        var module = "Goal_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stem)));
                        var leanSource = NaturalDeduction.ExportLean(request, module);
                        var path = Path.Combine(output, module + ".lean");
                        File.WriteAllText(path, leanSource, new UTF8Encoding(false));
                        var receipt = Invoke(executable, new[] { path }, output);
                        leanChecked++;
                        var passed = receipt.Code == 0 && !receipt.Output.Contains("sorryAx", StringComparison.Ordinal)
                            && !receipt.Output.Contains("declaration uses 'sorry'", StringComparison.Ordinal)
                            && !receipt.Error.Contains("declaration uses 'sorry'", StringComparison.Ordinal);
                        if (!passed) failed++;
                        results.Add(new { case_id = caseId, scope = scope.Id, id = theorem.Id, negative = sign,
                            dv_sha256 = sourceHash, check = "horn-certificate-to-lean4-kernel", passed,
                            generated_file = path,
                            generated_sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(leanSource))),
                            exit_code = receipt.Code, stdout = receipt.Output, stderr = receipt.Error });
                    }
                }
            }
        }
        var report = new { kind = "dv-case-authoring-lean-verification", created_utc = DateTimeOffset.UtcNow,
            lean = executable, version = version.Output.Trim(), checks = results.Count, lean_checked = leanChecked,
            rejected_before_export = rejectedBeforeExport, failed,
            coverage = "Explicit DV proof terms and independently computed typed-history scheduling/world transitions checked by generated Lean. Per-result coverage excludes cognition/Sim4/custody/physical motion where not exported. No gameplay completion claim.",
            unexported_goals = unexportedGoals, results };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        var reportPath = Path.Combine(output, "verification.json");
        File.WriteAllText(reportPath, json, new UTF8Encoding(false));
        Console.WriteLine(JsonSerializer.Serialize(new { checks = results.Count, lean_checked = leanChecked,
            rejected_before_export = rejectedBeforeExport, failed, report = reportPath, version = version.Output.Trim() }));
        return failed == 0 && leanChecked > 0 ? 0 : 1;
    }

    private static (int Code, string Output, string Error) Invoke(string executable, IEnumerable<string> arguments, string directory, int timeoutMilliseconds = 30000)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start Lean.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return (-1, stdout.GetAwaiter().GetResult(), "Lean deadline exceeded; no proof accepted. " + stderr.GetAwaiter().GetResult());
        }
        return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
