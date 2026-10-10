using System.Text.Json;
using DualOntologyWorldEngine.Story;
using DualOntologyWorldEngine.Core;

var json = File.ReadAllText(args[0]);
var example = ExampleWorld.Load(args[0]);
var firstChapter = example.Chapters[0];
var secondChapter = example.Chapters[1];
var option = firstChapter.Candidates.Single(c => c.Kind == "option");
var reaction = firstChapter.Candidates.First(c => c.Kind == "reaction");
var player = option.Response.First(line => line.Op == "line" && line.Speaker != "").Speaker;
var responder = reaction.Actor;
var target = option.Target!;
var room = firstChapter.Entities.Single(e => Id(e, firstChapter) == player).Properties["space"].Value.GetString()!;
var targetState = firstChapter.Entities.Single(e => Id(e, firstChapter) == target).Properties.Keys.Single(k => k.StartsWith("state:"))[6..];
var strategy = reaction.Goal!.Args[0].Args[1].Value.GetString()!;
var extensionState = secondChapter.Patches.Single(p => p.Target == player).Properties.Keys.Single()[6..];
var optionId = firstChapter.Id + ":" + option.Id;
int failures = 0;

Console.WriteLine("SCOPE: declaration-driven development examples; not formal chapters or blind play.");
string Id(WorldEntityDefinition entity, ChapterWorldDefinition chapter) =>
    entity.Id.StartsWith("world:") || entity.Id.StartsWith("chapter:") ? entity.Id :
    entity.Scope == "world" ? "world:" + entity.Id : "chapter:" + chapter.Id + ":" + entity.Id;
void Check(string name, Action test)
{
    try { test(); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + " " + error); }
}
void Assert(bool value, string message = "assertion") { if (!value) throw new Exception(message); }
void Reject(Action action)
{
    bool rejected = false;
    try { action(); } catch { rejected = true; }
    Assert(rejected, "expected rejection");
}
WorldSession New() => example.CreateSession();
AvailableCandidate[] Offer(WorldSession session) => session.Process(new RequestCandidates(player, target))
    .OfType<CandidatesAvailable>().Single().Candidates.ToArray();
void See(WorldSession session, string actor) => session.Process(new VerifiedPerception(actor, [target, player, responder, room]));
DialogueExpression Literal(object value) => new() { Value = JsonSerializer.SerializeToElement(value) };
DialogueExpression State(string entity, string tag) => new() { Op = "call", Name = "state", Args = [Literal(entity), Literal(tag)] };

Check("actual compiled declaration and inheritance contract", () =>
{
    Assert(example.Chapters.Count == 2 && firstChapter.Requires.Count == 0);
    Assert(secondChapter.Requires.SequenceEqual(new[] { firstChapter.Id }));
    Assert(secondChapter.Entities.Count == 0 && secondChapter.Patches.Count == 2);
    Assert(firstChapter.Entities.Count(e => e.Kind == "character") == 2);
});
Check("missing or empty example document has no fallback", () =>
{
    Reject(() => ExampleWorld.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));
    Reject(() => new ExampleWorld("{\"format\":\"dual-ontology-world-script/v1\",\"chapters\":[]}"));
});
Check("malformed Int32 input rejected before chapter mutation", () =>
{
    using var session = new WorldSession();
    var chapter = WorldScriptDocument.Parse(json)[0];
    chapter.Entities.Single(e => Id(e, chapter) == target).Properties["state:large"] = new()
        { Type = "integer", Value = JsonSerializer.SerializeToElement(2147483648L) };
    Reject(() => session.Load(chapter)); Assert(session.LoadedChapters.Count == 0);
});
Check("only applicable player candidates", () =>
{
    using var session = New(); Assert(Offer(session).Length == 0); See(session, player);
    Assert(Offer(session).Select(c => c.Id).SequenceEqual(new[] { optionId }));
});
Check("Core decision changes with subjective strategy", () =>
{
    using var session = New(); See(session, responder);
    var first = session.Process(new RequestReaction(responder, option.Id)).OfType<CandidateActionStarted>().Single();
    session.Process(new AdvanceWorld(3)); session.RememberState(responder, responder, strategy, 0);
    var second = session.Process(new RequestReaction(responder, option.Id)).OfType<CandidateActionStarted>().Single();
    Assert(first.CandidateId != second.CandidateId);
});
Check("unknown reaction not forced", () =>
{
    using var session = New(); Assert(session.Process(new RequestReaction(responder, option.Id)).Single() is NoApplicableCandidate);
});
Check("identity and state survive chapter extend", () =>
{
    using var session = New(); var entity = session.Entity(target); var actor = session.Character(player);
    var soul = actor.Soul; var self = actor.Self;
    entity.Get<StateProperty>()!.Values[new Tag(targetState)] = 7; session.Load(secondChapter);
    Assert(ReferenceEquals(entity, session.Entity(target)) && ReferenceEquals(soul, session.Character(player).Soul)
        && ReferenceEquals(self, session.Character(player).Self));
    Assert(entity.Get<StateProperty>()!.Values[new Tag(targetState)] == 7);
});
Check("duplicate and missing dependency atomic", () =>
{
    using var session = New(); Reject(() => session.Load(firstChapter)); Assert(session.LoadedChapters.Count == 1);
    using var fresh = new WorldSession(); Reject(() => fresh.Load(secondChapter)); Assert(fresh.LoadedChapters.Count == 0);
});
Check("invalid reference and action atomic", () =>
{
    foreach (var action in new[] { false, true })
    {
        using var session = New(); var chapter = WorldScriptDocument.Parse(json)[1];
        if (action) chapter.Candidates[0].Response.Add(new() { Op = "call", Name = "evil" });
        else chapter.Candidates[0].Target = "world:missing";
        Reject(() => session.Load(chapter));
        Assert(session.CurrentChapter == firstChapter.Id && session.LoadedChapters.Count == 1);
        Assert(!session.Character(player).Body.Get<StateProperty>()!.Values.ContainsKey(new Tag(extensionState)));
    }
});
Check("duplicate and expired selections", () =>
{
    using var session = New(); See(session, player); var old = Offer(session).Single(); var current = Offer(session).Single();
    Reject(() => session.Process(new SelectCandidate(old.Token)));
    Assert(session.Process(new SelectCandidate(current.Token)).OfType<CandidateActionStarted>().Count() == 1);
    Reject(() => session.Process(new SelectCandidate(current.Token)));
    Assert(session.Process(new AdvanceWorld(3)).OfType<CandidateActionFinished>().Count() == 1);
});
Check("dialogue is read from the authored candidate", () =>
{
    using var session = New(); See(session, player); var offered = Offer(session).Single();
    var outputs = session.Process(new SelectCandidate(offered.Token)).Concat(session.Process(new AdvanceWorld(3))).ToArray();
    Assert(outputs.OfType<SemanticDialogue>().Single().Lines.Select(line => (line.Speaker, line.Text))
        .SequenceEqual(option.Response.Where(line => line.Op == "line").Select(line => (line.Speaker, line.Text))));
});
Check("semantic output assembly has no Godot reference", () =>
    Assert(!typeof(WorldOutput).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name!.Contains("Godot"))));
Check("legacy behavior rejected", () => Reject(() => WorldScriptDocument.Parse(json.Replace("\"chapters\":", "\"behavior\":[{}],\"chapters\":"))));
Check("three valued and subjective applicability", () =>
{
    foreach (var mode in new[] { "or", "not", "belief" })
    {
        var chapter = WorldScriptDocument.Parse(json)[0]; var candidate = chapter.Candidates.Single(c => c.Kind == "option");
        candidate.Target = null; candidate.Response = candidate.Response.Where(step => step.Op == "line").ToList();
        var unknown = new DialogueExpression { Op = "call", Name = "known", Args = [Literal(target)] };
        candidate.Applicability = mode == "belief" ? new() { Op = "eq", Args = [State(responder, strategy), Literal(1)] }
            : new() { Op = mode, Args = mode == "or" ? [Literal(true), unknown] : [unknown] };
        using var session = new WorldSession(); session.Load(chapter);
        var offered = session.Process(new RequestCandidates(mode == "belief" ? responder : player)).OfType<CandidatesAvailable>().Single().Candidates;
        Assert(offered.Count == (mode == "not" ? 0 : 1), mode);
        if (mode == "belief")
        {
            session.Process(new SelectCandidate(offered.Single().Token));
            Assert(session.Process(new AdvanceWorld(3)).OfType<CandidateActionFinished>().Single().Succeeded);
            Assert(!session.Entity(responder).Get<StateProperty>()!.Values.ContainsKey(new Tag(strategy)));
        }
    }
});
Check("false belief cannot peek objective", () =>
{
    var chapter = WorldScriptDocument.Parse(json)[0];
    chapter.Candidates.Single(c => c.Kind == "option").Applicability = new() { Op = "eq", Args = [State(target, targetState), Literal(1)] };
    using var session = new WorldSession(); session.Load(chapter); See(session, player);
    session.RememberState(player, target, targetState, 0); Assert(Offer(session).Length == 0);
    Assert(session.Entity(target).Get<StateProperty>()!.Values[new Tag(targetState)] == 1);
});
Check("world candidate survives second chapter load", () =>
{
    using var session = New(); See(session, player); Assert(Offer(session).Any(c => c.Id == optionId));
    session.Load(secondChapter); Assert(Offer(session).Any(c => c.Id == optionId));
});
Console.WriteLine($"RESULT failures={failures}");
return failures == 0 ? 0 : 1;
