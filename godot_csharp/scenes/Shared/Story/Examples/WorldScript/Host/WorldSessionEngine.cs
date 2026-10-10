using DualOntologyWorldEngine.Story;
using DualOntologyWorldEngine.Core;
using Godot;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

/// <summary>Godot transport only. The persistent WorldSession owns candidates, policy and actions.</summary>
public partial class WorldSessionEngine : Node
{
    private readonly WorldSession _session = new();
    private IReadOnlyList<ChapterWorldDefinition>? _chapters;
    public string CurrentChapter() => _session.CurrentChapter ?? "";
    public bool CanLoadNextChapter() => _chapters is not null && _session.LoadedChapters.Count < _chapters.Count;
    public GDict EntityNames()
    {
        var names = new GDict();
        foreach (var entity in Entities())
        {
            var row = entity.AsGodotDictionary();
            names[row["id"]] = row["name"];
        }
        return names;
    }
    public GArray Entities()
    {
        var entities = new GArray();
        if (_chapters is null) return entities;
        foreach (var chapter in _chapters.Where(c => _session.LoadedChapters.Contains(c.Id)))
        foreach (var definition in chapter.Entities)
        {
            var id = definition.Id.StartsWith("world:", StringComparison.Ordinal) || definition.Id.StartsWith("chapter:", StringComparison.Ordinal)
                ? definition.Id : definition.Scope == "world" ? "world:" + definition.Id : "chapter:" + chapter.Id + ":" + definition.Id;
            if (entities.Any(value => value.AsGodotDictionary()["id"].AsString() == id)) continue;
            var entity = _session.Entity(id);
            var spatial = entity.Get<SpatialProperty>();
            var space = spatial?.Holder?.Get<SpatialProperty>()?.Space ?? spatial?.Space;
            var roomId = space is null ? "" : _session.World.Objects.Single(pair => ReferenceEquals(pair.Value, space)).Key;
            entities.Add(new GDict { ["id"] = id, ["name"] = definition.Name, ["kind"] = definition.Kind, ["room"] = roomId });
        }
        return entities;
    }
    public GArray OpenDocument(string path)
    {
        try
        {
            if (_chapters is not null) throw new InvalidOperationException("The preview document is already open; this session cannot be reset.");
            if (!Godot.FileAccess.FileExists(path))
                throw new FileNotFoundException("Missing compiled world script: " + path + ". Generate it with tools/story_script.py from ordered .story sources.");
            var chapters = WorldScriptDocument.Parse(Godot.FileAccess.GetFileAsString(path));
            if (chapters.Count == 0) throw new InvalidDataException("The world document contains no chapters.");
            _session.Load(chapters[0]);
            _chapters = chapters;
            return new() { new GDict { ["kind"] = "chapter_loaded", ["chapter"] = CurrentChapter() } };
        }
        catch (Exception error) { return Error(error); }
    }
    public GArray LoadNextChapter()
    {
        try
        {
            if (_chapters is null) throw new InvalidOperationException("Open a compiled world document first.");
            if (!CanLoadNextChapter()) throw new InvalidOperationException("All document chapters are already loaded.");
            // Reuse the live session. Loading the next declaration extends it rather than resetting it.
            _session.Load(_chapters[_session.LoadedChapters.Count]);
            return new() { new GDict { ["kind"] = "chapter_loaded", ["chapter"] = CurrentChapter() } };
        }
        catch (Exception error) { return Error(error); }
    }
    public GArray Dispatch(GDict input)
    {
        try
        {
            WorldInput request = input["kind"].AsString() switch
            {
                "candidates" => new RequestCandidates(input["actor"].AsString(),
                    input.ContainsKey("target") ? input["target"].AsString() : null),
                "select" => new SelectCandidate(input["token"].AsString()),
                "perception" => new VerifiedPerception(input["actor"].AsString(),
                    input["entities"].AsGodotArray().Select(value => value.AsString()).ToArray()),
                "presentation_completed" => new PresentationCompleted(input["presentation_id"].AsString()),
                "advance" => new AdvanceWorld(_session.Simulation.CTB.Now + input["elapsed"].AsDouble()),
                _ => throw new ArgumentException("Unsupported preview input: " + input["kind"].AsString()),
            };
            return new(_session.Process(request).Select(output => Variant.From(ConvertOutput(output))));
        }
        catch (Exception error) { return Error(error); }
    }
    private static GDict ConvertOutput(WorldOutput output) => output switch
    {
        CandidatesAvailable choices => new()
        {
            ["kind"] = "candidates", ["actor"] = choices.Actor,
            ["choices"] = new GArray(choices.Candidates.Select(choice => Variant.From(new GDict
                { ["token"] = choice.Token, ["id"] = choice.Id, ["label"] = choice.Label }))),
        },
        CandidateActionStarted action => new()
        { ["kind"] = "action_started", ["execution_id"] = action.ExecutionId, ["actor"] = action.Actor, ["candidate_id"] = action.CandidateId },
        CandidateActionFinished action => new()
        { ["kind"] = "action_finished", ["execution_id"] = action.ExecutionId, ["actor"] = action.Actor,
            ["candidate_id"] = action.CandidateId, ["succeeded"] = action.Succeeded, ["failure"] = action.Failure.ToString() },
        SemanticDialogue dialogue => new()
        {
            ["kind"] = "dialogue", ["presentation_id"] = dialogue.PresentationId,
            ["lines"] = new GArray(dialogue.Lines.Select(line => Variant.From(new GDict
                { ["speaker"] = line.Speaker, ["text"] = line.Text, ["mood"] = line.Mood, ["voice_clip"] = line.VoiceClip }))),
        },
        NoApplicableCandidate none => new() { ["kind"] = "no_candidate", ["actor"] = none.Actor },
        _ => throw new NotSupportedException("Unsupported world output: " + output.GetType().Name),
    };
    private static GArray Error(Exception error)
    {
        GD.PushError(error.Message);
        return new() { new GDict { ["kind"] = "error", ["message"] = error.Message } };
    }
    public override void _ExitTree() => _session.Dispose();
}
