using System.Text.Json;
using DualOntologyWorldEngine.Story;

namespace V00.PublicPlayer;

// V02 implements this in the TRUSTED host. The player never receives this object.
// No root session, unrestricted WorldInput, probe, snapshot, or state setter.
public interface ICandidatePort
{
    IReadOnlyList<WorldOutput> Submit(SelectCandidate input);
}

public sealed class BoundaryRejected(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed record VisibleLine(string Speaker, string Mood, string Text);
public sealed record VisibleCandidate(string Token, string Label);
public sealed record VisibleCard(string Title, string Facts, string Source, IReadOnlyList<string> Notes);
public sealed record PublicObservation(string Schema, int Turn, IReadOnlyList<VisibleLine> Lines,
    IReadOnlyList<VisibleCandidate> Candidates, VisibleCard? Card, bool Complete);

// Parent-only evidence sink. It is deliberately absent from ICandidatePort/wire DTOs.
public sealed class ObserverLog(string path) : IDisposable
{
    private readonly StreamWriter _writer = new(path, append: false, new System.Text.UTF8Encoding(false));
    private readonly object _gate = new();
    private long _sequence;
    public void Record(string kind, object value)
    {
        lock (_gate)
        {
            _writer.WriteLine(JsonSerializer.Serialize(new { sequence = ++_sequence, kind, value }));
            _writer.Flush();
        }
    }
    public void Bytes(string direction, byte[] bytes, string disposition = "complete")
        => Record("wire_bytes", new { direction, base64 = Convert.ToBase64String(bytes), bytes = bytes.Length, disposition });
    public void Dispose() => _writer.Dispose();
}

public sealed class CandidateBoundary(ICandidatePort port, ObserverLog observer, int maxDecisions = 8, int maxObservations = 32)
{
    public const string Schema = "v00-public-candidate/v1";
    public static readonly JsonSerializerOptions WireJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly HashSet<string> _issued = new(StringComparer.Ordinal);
    private HashSet<string> _current = new(StringComparer.Ordinal);
    private int _turn, _decisions;
    private bool _failed;

    private void RequireOpen()
    {
        if (_failed) throw new BoundaryRejected("boundary_closed");
        if (maxDecisions < 1 || maxObservations < 1) throw new BoundaryRejected("invalid_budget");
    }

    private BoundaryRejected Fail(string code)
    {
        _failed = true;
        _current.Clear();
        observer.Record("rejected", new { code, turn = _turn, decisions = _decisions });
        return new(code);
    }

    public PublicObservation Observe(IReadOnlyList<WorldOutput> outputs, InteractionPacket? presentation = null)
    {
        RequireOpen();
        if (_turn >= maxObservations) throw Fail("observation_budget");
        // Explicit type allowlist before SERIALIZING the official output DTOs.
        // Do not serialize an arbitrary object or IChapterSession.Snapshot dictionary.
        foreach (var output in outputs)
        {
            if (output is not CandidatesAvailable and not SemanticDialogue and not NoApplicableCandidate)
                throw Fail("unsupported_world_output");
            observer.Record("official_world_output", new {
                type = output.GetType().Name,
                json = JsonSerializer.Serialize(output, output.GetType())
            });
        }
        if (presentation is not null)
            observer.Record("official_interaction_packet", new { json = JsonSerializer.Serialize(presentation) });

        if (outputs.OfType<CandidatesAvailable>().Count() > 1)
            throw Fail("multiple_candidate_batches");
        if (outputs.OfType<CandidatesAvailable>().Any() && outputs.OfType<NoApplicableCandidate>().Any())
            throw Fail("conflicting_candidate_batches");
        // A candidate id is not a token. Menus need an explicitly reviewed V02 bridge.
        if (presentation is { Choices.Count: > 0 } or { WorldCommands.Count: > 0 })
            throw Fail("unmapped_interaction_control");
        var candidates = outputs.OfType<CandidatesAvailable>().SelectMany(value => value.Candidates)
            .Select(value => new VisibleCandidate(value.Token, value.Label)).ToArray();
        if (candidates.Length > 256) throw Fail("candidate_limit");
        if (presentation?.IsComplete == true && candidates.Length != 0) throw Fail("complete_with_candidates");
        var fresh = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate.Token) || candidate.Token.Length > 2048 || candidate.Label.Length > 4096)
                throw Fail("invalid_candidate");
            if (!fresh.Add(candidate.Token)) throw Fail("duplicate_offer_token");
            if (_issued.Contains(candidate.Token)) throw Fail("reissued_offer_token");
        }
        var lines = outputs.OfType<SemanticDialogue>().SelectMany(value => value.Lines)
            .Concat(presentation?.Lines ?? []).Select(value => new VisibleLine(value.Speaker, value.Mood, value.Text)).ToArray();
        if (lines.Length > 256 || lines.Any(value => value.Speaker.Length > 4096 || value.Mood.Length > 4096 || value.Text.Length > 4096))
            throw Fail("visible_line_limit");
        var card = presentation?.Card is { } value
            ? new VisibleCard(value.Title, value.Facts, value.Source, value.Notes) : null;
        if (card is not null && (card.Title.Length > 4096 || card.Facts.Length > 4096 || card.Source.Length > 4096
            || card.Notes.Count > 256 || card.Notes.Any(note => note.Length > 4096)))
            throw Fail("visible_card_limit");
        // Every externally scheduled observation is a new generation. A host must
        // supply a current official batch after advancing its own lifecycle.
        _current = fresh;
        _issued.UnionWith(fresh);
        if (_issued.Count > maxDecisions * 256) throw Fail("issued_token_budget");
        var observation = new PublicObservation(Schema, ++_turn, lines, candidates, card, presentation?.IsComplete ?? false);
        var wire = JsonSerializer.Serialize(observation, WireJson);
        if (System.Text.Encoding.UTF8.GetByteCount(wire) > 65536) throw Fail("observation_limit");
        observer.Record("public_observation", observation);
        return observation;
    }

    public IReadOnlyList<WorldOutput> Accept(string json)
    {
        RequireOpen();
        observer.Record("controller_decision", new { json });
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 16384) throw Fail("decision_limit");
        JsonDocument parsed;
        try { parsed = JsonDocument.Parse(json); }
        catch (JsonException) { throw Fail("invalid_json"); }
        using (parsed)
        {
            var root = parsed.RootElement;
            if (root.ValueKind == JsonValueKind.Null)
            {
                if (_current.Count != 0) throw Fail("abstention_with_candidates");
                observer.Record("no_action", new { turn = _turn });
                return [];
            }
            if (root.ValueKind != JsonValueKind.Object) throw Fail("invalid_decision_shape");
            var properties = root.EnumerateObject().ToArray();
            if (properties.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                throw Fail("duplicate_decision_field");
            if (properties.Length != 2 || !root.TryGetProperty("type", out var type) || !root.TryGetProperty("token", out var token))
                throw Fail("unknown_or_missing_decision_field");
            if (type.ValueKind != JsonValueKind.String || type.GetString() != nameof(SelectCandidate))
                throw Fail("unsupported_input_type");
            if (token.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(token.GetString()) || token.GetString()!.Length > 2048)
                throw Fail("invalid_selection_token");
            if (_decisions >= maxDecisions) throw Fail("decision_budget");
            var selected = token.GetString()!;
            if (!_current.Contains(selected)) throw Fail("unknown_consumed_or_expired_token");
            // All current offers expire BEFORE submit; exceptions never retry.
            _current.Clear();
            _decisions++;
            var input = new SelectCandidate(selected);
            observer.Record("official_input", new { type = nameof(SelectCandidate), json = JsonSerializer.Serialize(input) });
            try { return port.Submit(input); }
            catch (Exception error)
            {
                observer.Record("port_error", new { type = error.GetType().Name, error.Message });
                throw Fail("official_port_rejected");
            }
        }
    }

    public void TransportFailure(string code) => throw Fail(code);
}
