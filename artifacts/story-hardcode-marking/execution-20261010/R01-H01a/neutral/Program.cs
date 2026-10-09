using System.Reflection;
using System.Text.Json;
using DualOntologyWorldEngine.Story;

var failures = new List<string>();
var options = new JsonSerializerOptions { WriteIndented = false };
void Assert(bool condition, string message)
{
    Console.WriteLine((condition ? "PASS: " : "FAIL: ") + message);
    if (!condition) failures.Add(message);
}

ChapterWorldDefinition ReadChapter()
{
    var assembly = Assembly.GetExecutingAssembly();
    using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(p => p.EndsWith("neutral-world.json")))!;
    using var reader = new StreamReader(stream);
    return WorldScriptDocument.Parse(reader.ReadToEnd()).Single();
}

InteractionLine Copy(DialogueInstruction instruction) => new(instruction.Speaker, instruction.Mood, instruction.Text, instruction.VoiceClip, instruction.LineId, instruction.SourceId);

IReadOnlyList<WorldOutput> Step(WorldSession session, WorldInput input, string scenario)
{
    Console.WriteLine(JsonSerializer.Serialize(new { scenario, input = JsonSerializer.SerializeToElement(input, input.GetType(), options) }, options));
    var outputs = session.Process(input);
    foreach (var output in outputs)
        Console.WriteLine(JsonSerializer.Serialize(new { scenario, output_type = output.GetType().Name, output = JsonSerializer.SerializeToElement(output, output.GetType(), options) }, options));
    return outputs;
}

void MutateCaller(ChapterWorldDefinition chapter)
{
    foreach (var candidate in chapter.Candidates)
    {
        foreach (var line in candidate.Response)
        {
            line.LineId = "changed.line";
            line.SourceId = "changed.source";
            line.Speaker = "Changed actor";
            line.Mood = "changed";
            line.Text = "Changed neutral text.";
            line.VoiceClip = "changed-voice";
        }
        candidate.Response.Clear();
        candidate.Response.Add(new DialogueInstruction { Op = "line", Text = "Replacement neutral line.", LineId = "replacement", SourceId = "replacement" });
        candidate.ResponseTo.Clear();
        candidate.Policy.Weight = 0;
        candidate.Delay = 500;
    }
    chapter.Candidates.Clear();
}

List<InteractionLine[]> RunOption(bool mutate)
{
    var scenario = mutate ? "option-mutated-caller" : "option-original-caller";
    var chapter = ReadChapter();
    var expected = chapter.Candidates.Take(2).Select(c => c.Response.Select(Copy).ToArray()).ToArray();
    using var session = new WorldSession();
    session.Load(chapter);
    if (mutate) MutateCaller(chapter);
    var offer = Step(session, new RequestCandidates("actor"), scenario).OfType<CandidatesAvailable>().Single();
    Assert(offer.Candidates.Count == 1, scenario + ": exactly one offered option");
    var chosen = offer.Candidates.Single();
    var outputs = Step(session, new SelectCandidate(chosen.Token), scenario).ToList();
    outputs.AddRange(Step(session, new AdvanceWorld(2), scenario));
    var finished = outputs.OfType<CandidateActionFinished>().ToArray();
    Assert(finished.Length == 2 && finished.All(p => p.Succeeded), scenario + ": option and automatic NPC reaction complete through Core");
    var dialogs = outputs.OfType<SemanticDialogue>().Select(d => d.Lines.ToArray()).ToList();
    Assert(dialogs.Count == 2, scenario + ": both semantic dialogue packets emitted");
    for (var index = 0; index < Math.Min(dialogs.Count, expected.Length); index++)
    {
        Assert(dialogs[index].Length == expected[index].Length, scenario + ": line count preserved for packet " + index);
        Assert(dialogs[index].SequenceEqual(expected[index]), scenario + ": exact ordered line content and provenance preserved for packet " + index);
    }
    if (dialogs.Count > 0 && dialogs[0].Length >= 3)
        Assert(dialogs[0][2].LineId == "" && dialogs[0][2].SourceId == "", scenario + ": legacy line with omitted IDs remains empty");
    foreach (var dialogue in outputs.OfType<SemanticDialogue>())
        Assert(Step(session, new PresentationCompleted(dialogue.PresentationId), scenario).Count == 0, scenario + ": presentation acknowledged through existing API");
    return dialogs;
}

void RunExplicitReaction(bool mutate)
{
    var scenario = mutate ? "explicit-reaction-mutated-caller" : "explicit-reaction-original-caller";
    var chapter = ReadChapter();
    var expected = chapter.Candidates[2].Response.Select(Copy).ToArray();
    using var session = new WorldSession();
    session.Load(chapter);
    if (mutate) MutateCaller(chapter);
    var outputs = Step(session, new RequestReaction("observer"), scenario).ToList();
    outputs.AddRange(Step(session, new AdvanceWorld(2), scenario));
    Assert(outputs.OfType<CandidateActionFinished>().Single().Succeeded, scenario + ": explicit NPC reaction completes through Core");
    var dialogue = outputs.OfType<SemanticDialogue>().Single();
    Assert(dialogue.Lines.SequenceEqual(expected), scenario + ": exact ordered line content and provenance preserved");
    Assert(dialogue.Lines.Single().LineId == "" && dialogue.Lines.Single().SourceId == "neutral.source.e", scenario + ": independent SourceId survives an empty LineId");
    Step(session, new PresentationCompleted(dialogue.PresentationId), scenario);
}

var original = RunOption(false);
var mutated = RunOption(true);
Assert(original.Count == mutated.Count && original.Zip(mutated).All(p => p.First.SequenceEqual(p.Second)), "Caller mutation after Load cannot change frozen semantic line data");
RunExplicitReaction(false);
RunExplicitReaction(true);
Console.WriteLine(JsonSerializer.Serialize(new { result = failures.Count == 0 ? "PASS" : "FAIL", failure_count = failures.Count, failures }, options));
return failures.Count == 0 ? 0 : 1;
