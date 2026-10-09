using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

// DETECTION_ONLY: neutral fixture wiring and assertions only.
// All action settlement uses the unchanged production SimulationSession/ActionRuntime.
// No host Physics(), source design, story data, golden answers or second fact resolver.
var records = new List<Check>();
var wet = new Tag("neutral.wet");
var residue = new Tag("neutral.residue");
var mark = new Tag("neutral.mark");
var covered = new Tag("neutral.covered");
var contact = new RelationTag("neutral.contact");

void CheckFact(string world, string id, bool actual, string expected, object? details = null,
    string category = "SUPPORTED_COMPOSITION")
    => records.Add(new(world, id, category, actual, expected, details));

void CheckAction(Fixture f, string id, Effect effect, ActionFailure expected,
    Func<bool>? postcondition = null, string category = "SUPPORTED_COMPOSITION")
{
    var execution = f.Run(effect);
    var result = execution.Result!;
    CheckFact(f.Name, id, result.Failure == expected && result.Succeeded == (expected == ActionFailure.None)
        && (postcondition?.Invoke() ?? true), $"ActionFailure.{expected}; postcondition true",
        new { status = execution.Status.ToString(), succeeded = result.Succeeded,
            failure = result.Failure.ToString(), time = result.Time,
            postcondition = postcondition?.Invoke() }, category);
}

// World A: one bidirectional locked boundary between two spaces; cloth and tile share a support.
using (var a = new Fixture("A-two-space-bench"))
{
    var left = a.Entity("A.left", ProjectionKind.Space);
    var right = a.Entity("A.right", ProjectionKind.Space);
    var body = a.Body(left);
    var bench = a.Entity("A.bench", space: left);
    var cloth = a.Entity("A.cloth", space: bench, states: [(wet, 1), (residue, 1)]);
    var tile = a.Entity("A.tile", space: bench, states: [(mark, 0)]);
    var remote = a.Entity("A.remote-tile", space: right, states: [(mark, 0)]);
    var identity = a.Entity("A.key-identity", ProjectionKind.Concept);
    var wrongIdentity = a.Entity("A.wrong-identity", ProjectionKind.Concept);
    var key = a.Entity("A.key", space: bench);
    key.Relations.Add((Relations.HasIdentity, identity));
    var wrong = a.Entity("A.wrong-key", space: bench);
    wrong.Relations.Add((Relations.HasIdentity, wrongIdentity));
    var gate = a.Entity("A.gate", ProjectionKind.Boundary, space: left);
    gate.Add(new LockProperty());
    gate.Relations.Add((Relations.AcceptsKey, identity));
    gate.Add(new PassageProperty()).Connections.Add(left, right);
    gate.Get<PassageProperty>()!.Connections.Add(right, left);
    a.Start();

    CheckAction(a, "A01-locked-travel-rejected", a.Act(ActionOp.Travel, gate), ActionFailure.ConditionFailed,
        () => a.At(body, left), "EXPECTED_FAILURE");
    CheckAction(a, "A02-wrong-key-take", a.Act(ActionOp.Take, wrong), ActionFailure.None);
    CheckAction(a, "A03-wrong-identity-rejected", a.Act(ActionOp.Unlock, gate, wrong), ActionFailure.ConditionFailed,
        () => gate.Get<LockProperty>()!.IsLocked, "EXPECTED_FAILURE");
    CheckAction(a, "A04-unheld-correct-key-rejected", a.Act(ActionOp.Unlock, gate, key), ActionFailure.OutOfReach,
        () => gate.Get<LockProperty>()!.IsLocked, "EXPECTED_FAILURE");
    CheckAction(a, "A05-take-key", a.Act(ActionOp.Take, key), ActionFailure.None);
    CheckAction(a, "A06-unlock", a.Act(ActionOp.Unlock, gate, key), ActionFailure.None,
        () => !gate.Get<LockProperty>()!.IsLocked);
    CheckAction(a, "A07-travel-right", a.Act(ActionOp.Travel, gate), ActionFailure.None, () => a.At(body, right));
    CheckAction(a, "A08-remote-cloth-take-rejected", a.Act(ActionOp.Take, cloth), ActionFailure.OutOfReach,
        () => a.At(cloth, bench), "EXPECTED_FAILURE");
    CheckAction(a, "A09-return-left", a.Act(ActionOp.Travel, gate), ActionFailure.None, () => a.At(body, left));
    CheckAction(a, "A10-cloth-take", a.Act(ActionOp.Take, cloth), ActionFailure.None,
        () => ReferenceEquals(cloth.Get<SpatialProperty>()!.Holder, body));
    CheckAction(a, "A11-cloth-place-on-bench", a.Act(ActionOp.Place, cloth, bench), ActionFailure.None,
        () => a.At(cloth, bench) && a.Value(cloth, wet) == 1);
    CheckFact(a.Name, "A12-placement-does-not-generate-trace", a.Value(tile, mark) == 0,
        "tile mark remains 0 after cloth placement", new { mark = a.Value(tile, mark) }, "AUTOMATIC_PHYSICS_NOT_PROVIDED");

    var explicitContact = new Effect.Sequence([
        new Effect.Require(new Condition.All([
            new Condition.At(a.M(body), a.M(left)),
            new Condition.At(a.M(bench), a.M(left)),
            new Condition.At(a.M(cloth), a.M(bench)),
            new Condition.At(a.M(tile), a.M(bench)),
            new Condition.Tagged(a.M(cloth), wet, 1),
            new Condition.Tagged(a.M(cloth), residue, 1)])),
        new Effect.Change(a.M(tile), mark, 1),
        new Effect.Relate(a.M(tile), contact, a.M(cloth))]);
    CheckAction(a, "A13-declared-wet-contact-transfer", explicitContact, ActionFailure.None,
        () => a.Value(tile, mark) == 1 && tile.Relations.Contains((contact, cloth)));
    CheckFact(a.Name, "A14-memory-not-auto-synced", a.M(tile).Tags.GetValueOrDefault(mark) == 0,
        "actor's earlier perception remains mark=0 until observation",
        new { objective = a.Value(tile, mark), mental = a.M(tile).Tags.GetValueOrDefault(mark) });
    CheckAction(a, "A15-inspect-reveals-observable-mark", a.Act(ActionOp.Inspect, tile), ActionFailure.None,
        () => a.M(tile).Tags.GetValueOrDefault(mark) == 1);
    CheckFact(a.Name, "A16-essential-contact-not-visible-by-default", !a.M(tile).Relations.Contains((contact, a.M(cloth))),
        "objective relation alone is not an observable surface relation", category: "BOUNDARY_LIMITATION");
    CheckAction(a, "A17-dry-state", new Effect.Set(a.M(cloth), wet, 0), ActionFailure.None);
    CheckAction(a, "A18-dry-contact-rejected", explicitContact, ActionFailure.ConditionFailed,
        () => a.Value(tile, mark) == 1, "EXPECTED_FAILURE");
    CheckAction(a, "A19-failed-sequence-rolls-back", new Effect.Sequence([
        new Effect.Set(a.M(tile), mark, 7), a.Act(ActionOp.Take, remote)]), ActionFailure.OutOfReach,
        () => a.Value(tile, mark) == 1, "EXPECTED_FAILURE");
    CheckAction(a, "A20-raw-remote-set-is-not-physical-action", new Effect.Set(a.M(remote), mark, 3), ActionFailure.None,
        () => a.Value(remote, mark) == 3 && a.At(body, left), "BOUNDARY_LIMITATION");
}

// World B: directed entry to a hub with a second branch, a locked nested case, and two panel surfaces.
using (var b = new Fixture("B-directed-hub-nested-case"))
{
    var entry = b.Entity("B.entry", ProjectionKind.Space);
    var hub = b.Entity("B.hub", ProjectionKind.Space);
    var annex = b.Entity("B.annex", ProjectionKind.Space);
    var body = b.Body(entry);
    var identity = b.Entity("B.token-identity", ProjectionKind.Concept);
    var token = b.Entity("B.token", holder: body);
    token.Relations.Add((Relations.HasIdentity, identity));
    var entrance = b.Entity("B.entry-gate", ProjectionKind.Boundary, space: entry);
    entrance.Add(new LockProperty());
    entrance.Relations.Add((Relations.AcceptsKey, identity));
    entrance.Add(new PassageProperty()).Connections.Add(entry, hub); // deliberately one-way
    var branch = b.Entity("B.branch", ProjectionKind.Boundary, space: hub);
    branch.Add(new PassageProperty()).Connections.Add(hub, annex);
    branch.Get<PassageProperty>()!.Connections.Add(annex, hub);
    var station = b.Entity("B.station", space: hub);
    var box = b.Entity("B.case", space: station);
    box.Add(new LockProperty());
    box.Relations.Add((Relations.AcceptsKey, identity));
    var wrap = b.Entity("B.wrap", space: box, states: [(wet, 1), (residue, 1)]);
    var packet = b.Entity("B.packet", space: wrap, states: [(mark, 0)]);
    var material = b.Entity("B.fabric-material-label", ProjectionKind.Material, space: station);
    var panel = b.Entity("B.panel", space: station, states: [(mark, 1), (covered, 0)]);
    var front = b.View(panel);
    var back = b.Runtime.Project(panel, "B.panel-back");
    panel.Add(new SurfaceProperty()).Surfaces.Add(front, ImmutableDictionary<Tag, int>.Empty.Add(mark, 0));
    panel.Get<SurfaceProperty>()!.Surfaces.Add(back, ImmutableDictionary<Tag, int>.Empty.Add(mark, 1));
    b.Start();

    CheckAction(b, "B01-keyed-entry-unlock", b.Act(ActionOp.Unlock, entrance, token), ActionFailure.None);
    CheckAction(b, "B02-one-way-entry", b.Act(ActionOp.Travel, entrance), ActionFailure.None, () => b.At(body, hub));
    CheckAction(b, "B03-undeclared-return-rejected", b.Act(ActionOp.Travel, entrance), ActionFailure.OutOfReach,
        () => b.At(body, hub), "EXPECTED_FAILURE");
    CheckAction(b, "B04-locked-ancestor-blocks-packet", b.Act(ActionOp.Take, packet), ActionFailure.OutOfReach,
        () => b.At(packet, wrap), "EXPECTED_FAILURE");
    CheckAction(b, "B05-nested-case-unlock", b.Act(ActionOp.Unlock, box, token), ActionFailure.None);
    CheckAction(b, "B06-wrapper-take", b.Act(ActionOp.Take, wrap), ActionFailure.None);
    CheckAction(b, "B07-wrapper-place-on-station", b.Act(ActionOp.Place, wrap, station), ActionFailure.None,
        () => b.At(wrap, station) && b.At(packet, wrap));
    CheckFact(b.Name, "B08-nesting-does-not-auto-mark", b.Value(packet, mark) == 0,
        "nested object remains unmarked until a declared effect", category: "AUTOMATIC_PHYSICS_NOT_PROVIDED");
    CheckAction(b, "B09-nested-contact-by-existing-effects", new Effect.Sequence([
        new Effect.Require(new Condition.All([
            new Condition.At(b.M(body), b.M(hub)),
            new Condition.At(b.M(station), b.M(hub)),
            new Condition.At(b.M(wrap), b.M(station)),
            new Condition.At(b.M(packet), b.M(wrap)),
            new Condition.Tagged(b.M(wrap), wet, 1),
            new Condition.Tagged(b.M(wrap), residue, 1)])),
        new Effect.Change(b.M(packet), mark, 1)]), ActionFailure.None,
        () => b.Value(packet, mark) == 1);
    CheckAction(b, "B10-material-category-not-takeable", b.Act(ActionOp.Take, material), ActionFailure.UnsupportedOperation,
        category: "EXPECTED_FAILURE");
    CheckAction(b, "B11-cover-place", b.Act(ActionOp.Take, wrap), ActionFailure.None);
    CheckAction(b, "B12-place-cover-on-panel", b.Act(ActionOp.Place, wrap, panel), ActionFailure.None);
    CheckAction(b, "B13-declare-covered-flag", new Effect.Set(b.M(panel), covered, 1), ActionFailure.None);
    b.Runtime.Observe(b.Soul, [back], b.Session.CTB.Now);
    CheckFact(b.Name, "B14-cover-does-not-filter-perception", b.Mental(back).Tags.GetValueOrDefault(mark) == 1,
        "direct supplied back surface still visible after cover placement/flag",
        new { mark = b.Mental(back).Tags.GetValueOrDefault(mark), covered = b.Value(panel, covered) },
        "AUTOMATIC_OCCLUSION_NOT_PROVIDED");
    var beforeFront = b.Mental(front).LastObservation!.Time;
    var beforeBack = b.Mental(back).LastObservation!.Time;
    CheckAction(b, "B15-inspect-back-chooses-first-surface", new Effect.Act(ActionOp.Inspect, b.Mental(back)), ActionFailure.None,
        () => b.Mental(front).LastObservation!.Time > beforeFront && b.Mental(back).LastObservation!.Time == beforeBack,
        "BOUNDARY_LIMITATION");
    CheckFact(b.Name, "B16-identity-does-not-merge-side-recognition", !ReferenceEquals(b.Mental(front), b.Mental(back)),
        "two selected appearances of one Essence remain separate mental projections", category: "BOUNDARY_LIMITATION");
    CheckAction(b, "B17-take-marked-packet", b.Act(ActionOp.Take, packet), ActionFailure.None);
    CheckAction(b, "B18-branch-to-annex", b.Act(ActionOp.Travel, branch), ActionFailure.None, () => b.At(body, annex));
    CheckAction(b, "B19-carry-mark-to-annex", b.Act(ActionOp.Place, packet, annex), ActionFailure.None,
        () => b.At(packet, annex) && b.Value(packet, mark) == 1);
    CheckAction(b, "B20-remote-station-take-rejected", b.Act(ActionOp.Take, wrap), ActionFailure.OutOfReach,
        category: "EXPECTED_FAILURE");
}

var failed = records.Count(r => !r.Passed);
Console.WriteLine(JsonSerializer.Serialize(new
{
    schema = "D03-neutral-core-experiments/v1",
    baseline = "7489db56a714724eb7cb735958dd879dc662d238",
    mode = "DETECTION_ONLY",
    settlement = "unchanged formal Core SimulationSession and ActionRuntime",
    worlds = 2,
    total = records.Count,
    passed = records.Count - failed,
    failed,
    checks = records
}, new JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;

record Check(string World, string Id, string Category, bool Passed, string Expected, object? Actual);

sealed class Fixture : IDisposable
{
    readonly Dictionary<Essence, Projection> views = [];
    public string Name { get; }
    public WorldRuntime Runtime { get; } = new();
    public SoulEssence Soul { get; }
    public SimulationSession Session { get; }
    Essence body = null!;
    public Fixture(string name)
    {
        Name = name;
        Soul = new(name + ".observer");
        Runtime.Project(Soul, Soul.TrueName);
        Session = new(Runtime);
    }
    public Essence Entity(string name, ProjectionKind kind = ProjectionKind.Object, Essence? space = null,
        Essence? holder = null, (Tag Tag, int Value)[]? states = null)
    {
        var essence = new Essence(name, kind);
        if (space is not null || holder is not null) essence.Add(new SpatialProperty { Space = space, Holder = holder });
        if (states is not null)
        {
            var state = essence.Add(new StateProperty());
            foreach (var (tag, value) in states) { state.Values.Add(tag, value); state.Observable.Add(tag); }
        }
        views.Add(essence, Runtime.Project(essence, name));
        return essence;
    }
    public Essence Body(Essence room)
    {
        body = Entity(Name + ".body", ProjectionKind.Body, room);
        Soul.Relations.Add((Relations.Controls, body));
        return body;
    }
    public void Start()
    {
        Runtime.Sync();
        // Authored initial familiarity includes the neutral graph, including remote objects.
        // This is fixture initialization, not a live-game line-of-sight promise.
        Runtime.Observe(Soul, Runtime.Reality.Projections, 0);
    }
    public Projection View(Essence essence) => views[essence];
    public Projection M(Essence essence) => Runtime.KnownProjection(Soul, essence)!;
    public Projection Mental(Projection view) => Soul.World.Projections.Single(p => p.Name == view.Name);
    public bool At(Essence essence, Essence parent) => ReferenceEquals(essence.Get<SpatialProperty>()?.Holder
        ?? essence.Get<SpatialProperty>()?.Space, parent);
    public int Value(Essence essence, Tag tag) => essence.Get<StateProperty>()?.Values.GetValueOrDefault(tag) ?? 0;
    public Effect Act(ActionOp op, Essence target, Essence? tool = null)
        => new Effect.Act(op, M(target), tool is null ? null : new Noun.Reference(M(tool)));
    public ActionExecution Run(Effect effect)
    {
        var execution = Session.Submit(Soul, new ActionIntent(M(body), effect));
        var steps = 0;
        while (execution.Status == ExecutionStatus.Pending && steps++ < 30)
            if (!Session.CTB.Step()) throw new InvalidOperationException("Neutral fixture stalled with no scheduled action.");
        if (execution.Result is null || execution.Status == ExecutionStatus.Pending)
            throw new InvalidOperationException("Neutral fixture did not settle within 30 steps.");
        return execution;
    }
    public void Dispose() => Session.Dispose();
}
