using System.Reflection;
using System.Text.Json;
using DualOntologyWorldEngine.Story;

namespace V00.PublicPlayer;

// No engine, authored content, solve route, or injected game state is present.
internal sealed class SyntheticPort(bool reject = false) : ICandidatePort
{
    public List<SelectCandidate> Submitted { get; } = [];
    public IReadOnlyList<WorldOutput> Submit(SelectCandidate input)
    {
        Submitted.Add(input);
        if (reject) throw new InvalidOperationException("Synthetic port rejection.");
        return [new NoApplicableCandidate("neutral-actor")];
    }
}

internal sealed record UnexpectedOutput : WorldOutput;
internal sealed record CaseResult(string Name, string Status, string Evidence, string? Error = null);

internal static class Program
{
    private static readonly List<CaseResult> Results = [];
    private static string _directory = "", _python = "", _controller = "", _emptyCwd = "";
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
    private static string Token() => "neutral:" + Guid.NewGuid().ToString("N");
    private static WorldOutput[] Offers(params (string Token, string Label)[] items)
        => [new CandidatesAvailable("neutral-actor", items.Select(item =>
            new AvailableCandidate(item.Token, "official-metadata", item.Label)).ToArray())];
    private static string Select(string token) => JsonSerializer.Serialize(new { type = "SelectCandidate", token });
    private static ObserverLog Log(string name) => new(Path.Combine(_directory, name + ".jsonl"));
    private static void Rejected(Action operation, string code)
    {
        try { operation(); throw new InvalidOperationException("Expected rejection: " + code); }
        catch (BoundaryRejected error) { Check(error.Code == code, $"Expected {code}; got {error.Code}"); }
    }
    private static async Task Case(string name, Func<Task> operation)
    {
        try
        {
            await operation();
            Results.Add(new(name, "PASS", name + ".jsonl"));
        }
        catch (Exception error)
        {
            Results.Add(new(name, "FAIL", name + ".jsonl", error.ToString()));
        }
    }
    private static Task Synchronous(Action action) { action(); return Task.CompletedTask; }

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("results-directory python-executable controller-script required");
        _directory = Path.GetFullPath(args[0]); _python = Path.GetFullPath(args[1]); _controller = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(_directory);
        _emptyCwd = Path.Combine(_directory, "empty-cwd");
        Directory.CreateDirectory(_emptyCwd);

        await Case("01_api_surface", () => Synchronous(() =>
        {
            using var log = Log("01_api_surface");
            var methods = typeof(ICandidatePort).GetMethods();
            Check(methods.Length == 1 && methods[0].Name == "Submit", "Port must have one method");
            Check(methods[0].GetParameters().Length == 1 && methods[0].GetParameters()[0].ParameterType == typeof(SelectCandidate), "Port must accept only SelectCandidate");
            Check(methods[0].ReturnType == typeof(IReadOnlyList<WorldOutput>), "Port output contract drift");
            var wireTypes = new[] { typeof(PublicObservation), typeof(VisibleLine), typeof(VisibleCandidate), typeof(VisibleCard) };
            foreach (var type in wireTypes)
            foreach (var property in type.GetProperties())
                Check(property.PropertyType != typeof(ObserverLog) && property.PropertyType != typeof(ICandidatePort)
                    && !typeof(Delegate).IsAssignableFrom(property.PropertyType), "Handle leaked into wire type");
            log.Record("reflection_assertions", new { api = methods.Select(method => method.ToString()),
                allowed_wire_properties = wireTypes.ToDictionary(type => type.Name, type => type.GetProperties().Select(property => property.Name).ToArray()),
                scope = "API/serialization only; not process/file permissions" });
        }));

        await Case("02_visible_projection_and_process", async () =>
        {
            using var log = Log("02_visible_projection_and_process");
            var port = new SyntheticPort(); var boundary = new CandidateBoundary(port, log);
            var a = Token(); var b = Token();
            var dialogue = new SemanticDialogue("private-presentation-id", [
                new InteractionLine("neutral-speaker", "neutral-mood", "neutral-visible-text", "private-voice-path", "private-line-id", "private-source-id")]);
            var observation = boundary.Observe([.. Offers((b, "B"), (a, "A")), dialogue]);
            var wire = JsonSerializer.Serialize(observation, CandidateBoundary.WireJson);
            foreach (var hidden in new[] { "official-metadata", "private-presentation-id", "private-voice-path", "private-line-id", "private-source-id" })
                Check(!wire.Contains(hidden, StringComparison.Ordinal), "Non-visible metadata leaked: " + hidden);
            Check(wire.Contains("neutral-visible-text", StringComparison.Ordinal), "Visible text missing");
            await using var controller = new ControllerProcess(_python, _controller, _emptyCwd, log);
            // The first projection above checks the wire; start a fresh boundary
            // for the actual driver, so every generation is observed once.
            var driverBoundary = new CandidateBoundary(port, log);
            var driver = new PublicPlayerDriver(driverBoundary, controller);
            var output = await driver.TurnAsync([.. Offers((b, "B"), (a, "A")), dialogue]);
            Check(port.Submitted.Count == 1 && port.Submitted[0].Token == a, "Decision must select CURRENT visible label A");
            await driver.TurnAsync(output);
            Check(port.Submitted.Count == 1, "Abstention must create no WorldInput");
        });

        await Case("03_packet_visible_card", () => Synchronous(() =>
        {
            using var log = Log("03_packet_visible_card");
            var boundary = new CandidateBoundary(new SyntheticPort(), log);
            var packet = new InteractionPacket([], [], PresentationMode.EvidenceCard, [], Card:
                new EvidenceCard("private-card-id", "neutral-title", "neutral-facts", "neutral-source", "private-picture-path", ["neutral-note"]));
            var wire = JsonSerializer.Serialize(boundary.Observe([], packet), CandidateBoundary.WireJson);
            Check(wire.Contains("neutral-facts", StringComparison.Ordinal) && !wire.Contains("private-card-id", StringComparison.Ordinal)
                && !wire.Contains("private-picture-path", StringComparison.Ordinal), "Card projection mismatch");
        }));

        await Case("04_duplicate_offer", () => Synchronous(() =>
        {
            using var log = Log("04_duplicate_offer"); var boundary = new CandidateBoundary(new SyntheticPort(), log); var token = Token();
            Rejected(() => boundary.Observe(Offers((token, "A"), (token, "B"))), "duplicate_offer_token");
        }));
        await Case("05_consumed_token", () => Synchronous(() =>
        {
            using var log = Log("05_consumed_token"); var port = new SyntheticPort(); var boundary = new CandidateBoundary(port, log); var token = Token();
            boundary.Observe(Offers((token, "A"))); boundary.Accept(Select(token));
            Rejected(() => boundary.Accept(Select(token)), "unknown_consumed_or_expired_token");
            Check(port.Submitted.Count == 1, "Duplicate selection reached port");
        }));
        await Case("06_expired_token", () => Synchronous(() =>
        {
            using var log = Log("06_expired_token"); var port = new SyntheticPort(); var boundary = new CandidateBoundary(port, log); var old = Token();
            boundary.Observe(Offers((old, "A"))); boundary.Observe(Offers((Token(), "B")));
            Rejected(() => boundary.Accept(Select(old)), "unknown_consumed_or_expired_token");
            Check(port.Submitted.Count == 0, "Expired selection reached port");
        }));
        await Case("07_reissued_token", () => Synchronous(() =>
        {
            using var log = Log("07_reissued_token"); var boundary = new CandidateBoundary(new SyntheticPort(), log); var token = Token();
            boundary.Observe(Offers((token, "A")));
            Rejected(() => boundary.Observe(Offers((token, "B"))), "reissued_offer_token");
        }));
        await Case("08_unknown_input_type", () => Synchronous(() =>
        {
            using var log = Log("08_unknown_input_type"); var port = new SyntheticPort(); var boundary = new CandidateBoundary(port, log); var token = Token();
            boundary.Observe(Offers((token, "A")));
            Rejected(() => boundary.Accept(JsonSerializer.Serialize(new { type = "VerifiedPerception", token })), "unsupported_input_type");
            Check(port.Submitted.Count == 0, "Unknown input reached port");
        }));
        await Case("09_unknown_state_field", () => Synchronous(() =>
        {
            using var log = Log("09_unknown_state_field"); var port = new SyntheticPort(); var boundary = new CandidateBoundary(port, log); var token = Token();
            boundary.Observe(Offers((token, "A")));
            Rejected(() => boundary.Accept(JsonSerializer.Serialize(new { type = "SelectCandidate", token, state = new { value = 1 } })), "unknown_or_missing_decision_field");
            Check(port.Submitted.Count == 0, "State field reached port");
        }));
        await Case("10_duplicate_input_field", () => Synchronous(() =>
        {
            using var log = Log("10_duplicate_input_field"); var boundary = new CandidateBoundary(new SyntheticPort(), log); var token = Token();
            boundary.Observe(Offers((token, "A")));
            Rejected(() => boundary.Accept("{\"type\":\"SelectCandidate\",\"token\":" + JsonSerializer.Serialize(token) + ",\"token\":" + JsonSerializer.Serialize(token) + "}"), "duplicate_decision_field");
        }));
        await Case("11_decision_budget", () => Synchronous(() =>
        {
            using var log = Log("11_decision_budget"); var port = new SyntheticPort(); var boundary = new CandidateBoundary(port, log, maxDecisions: 2);
            for (var i = 0; i < 2; i++) { var token = Token(); boundary.Observe(Offers((token, "A"))); boundary.Accept(Select(token)); }
            var final = Token(); boundary.Observe(Offers((final, "A")));
            Rejected(() => boundary.Accept(Select(final)), "decision_budget");
            Check(port.Submitted.Count == 2, "Decision budget exceeded at port");
        }));
        await Case("12_observation_budget", () => Synchronous(() =>
        {
            using var log = Log("12_observation_budget"); var boundary = new CandidateBoundary(new SyntheticPort(), log, maxObservations: 2);
            boundary.Observe([]); boundary.Accept("null"); boundary.Observe([]); boundary.Accept("null");
            Rejected(() => boundary.Observe([]), "observation_budget");
        }));
        await Case("13_port_failure_no_retry", () => Synchronous(() =>
        {
            using var log = Log("13_port_failure_no_retry"); var port = new SyntheticPort(reject: true); var boundary = new CandidateBoundary(port, log); var token = Token();
            boundary.Observe(Offers((token, "A")));
            Rejected(() => boundary.Accept(Select(token)), "official_port_rejected");
            Rejected(() => boundary.Accept(Select(token)), "boundary_closed");
            Check(port.Submitted.Count == 1, "Failed official selection retried");
        }));
        await Case("14_unapproved_output", () => Synchronous(() =>
        {
            using var log = Log("14_unapproved_output"); var boundary = new CandidateBoundary(new SyntheticPort(), log);
            Rejected(() => boundary.Observe([new UnexpectedOutput()]), "unsupported_world_output");
        }));
        await Case("15_menu_id_is_not_token", () => Synchronous(() =>
        {
            using var log = Log("15_menu_id_is_not_token"); var boundary = new CandidateBoundary(new SyntheticPort(), log);
            var packet = new InteractionPacket([], [new InteractionChoice("neutral-id", "neutral-label", true, "")], PresentationMode.Menu, []);
            Rejected(() => boundary.Observe([], packet), "unmapped_interaction_control");
        }));
        await Case("16_world_command_not_player_input", () => Synchronous(() =>
        {
            using var log = Log("16_world_command_not_player_input"); var boundary = new CandidateBoundary(new SyntheticPort(), log);
            var packet = new InteractionPacket([], [], PresentationMode.World, [new WorldCommand("neutral-command", [])]);
            Rejected(() => boundary.Observe([], packet), "unmapped_interaction_control");
        }));
        await Case("17_complete_conflict", () => Synchronous(() =>
        {
            using var log = Log("17_complete_conflict"); var boundary = new CandidateBoundary(new SyntheticPort(), log);
            Rejected(() => boundary.Observe(Offers((Token(), "A")), new InteractionPacket([], [], PresentationMode.Done, [], IsComplete: true)), "complete_with_candidates");
        }));

        foreach (var (mode, code) in new[] { ("unknown", "unknown_or_missing_decision_field"), ("hidden", "unknown_or_missing_decision_field"),
            ("duplicate", "duplicate_decision_field"), ("oversized", "controller_output_limit"), ("hang", "controller_timeout") })
        {
            await Case("18_process_" + mode, async () =>
            {
                using var log = Log("18_process_" + mode); var port = new SyntheticPort(); var boundary = new CandidateBoundary(port, log);
                var offered = Offers((Token(), "A"));
                await using var controller = new ControllerProcess(_python, _controller, _emptyCwd, log, mode, timeoutMs: mode == "hang" ? 800 : 2500);
                var driver = new PublicPlayerDriver(boundary, controller);
                try
                {
                    await driver.TurnAsync(offered);
                    throw new InvalidOperationException("Malicious controller accepted");
                }
                catch (BoundaryRejected error) { Check(error.Code == code, $"Expected {code}; got {error.Code}"); }
                Rejected(() => boundary.Accept(Select(((CandidatesAvailable)offered[0]).Candidates[0].Token)), "boundary_closed");
                Check(port.Submitted.Count == 0, "Malicious child reached official port");
            });
        }
        await Case("19_process_stale", async () =>
        {
            using var log = Log("19_process_stale"); var port = new SyntheticPort(); var boundary = new CandidateBoundary(port, log);
            await using var controller = new ControllerProcess(_python, _controller, _emptyCwd, log, "stale");
            boundary.Accept(await controller.DecideAsync(boundary.Observe(Offers((Token(), "A")))));
            var reply = await controller.DecideAsync(boundary.Observe(Offers((Token(), "B"))));
            Rejected(() => boundary.Accept(reply), "unknown_consumed_or_expired_token");
            Check(port.Submitted.Count == 1, "Stale child request reached port twice");
        });

        var summary = new { schema = "v00-boundary-results/v1", mode = "DETECTION_ONLY", game_played = false,
            production_engine_connected = false, formal_dto_declarations_compiled = true,
            passed = Results.Count(result => result.Status == "PASS"), failed = Results.Count(result => result.Status == "FAIL"), cases = Results };
        File.WriteAllText(Path.Combine(_directory, "csharp-results.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { summary.passed, summary.failed, summary.mode, summary.game_played }));
        return summary.failed == 0 ? 0 : 1;
    }
}
