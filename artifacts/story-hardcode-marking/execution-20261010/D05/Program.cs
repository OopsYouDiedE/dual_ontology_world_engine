using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

// DETECTION_ONLY: neutral public-API probes of the existing Core.
// This process neither loads a chapter nor implements a chapter settlement system.
static class Probe
{
    static readonly Tag Occupied = new("NeutralOccupied"), Worked = new("NeutralWorked"), Gate = new("NeutralGate"), Passable = new("NeutralPassable");
    static void Emit(string kind, object value) => Console.WriteLine(JsonSerializer.Serialize(new { kind, value }));
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Emit("assert", new { name, passed = true }); }
    static Effect Seq(params Effect[] items) => new Effect.Sequence([.. items]);
    static Condition All(params Condition[] items) => new Condition.All([.. items]);
    static int Value(Essence e, Tag tag) => e.Get<TemporaryStateProperty>()?.Override(tag) ?? e.Get<StateProperty>()!.Values.GetValueOrDefault(tag);

    sealed class Fixture : IDisposable
    {
        public WorldRuntime World { get; } = new();
        public SimulationSession Session { get; }
        public Essence Room { get; } = new("R", ProjectionKind.Space);
        public List<(SoulEssence Soul, Essence Body)> Actors { get; } = [];
        public List<Essence> Stations { get; } = [];
        public Essence Result { get; }
        public Fixture(int count = 3)
        {
            Session = new(World);
            World.Project(Room, "R");
            Result = MakeObject("Result", Gate);
            for (int i = 0; i < count; i++)
            {
                var body = new Essence(((char)('A' + i)).ToString(), ProjectionKind.Body);
                body.Add(new SpatialProperty { Space = Room });
                body.Add(new StateProperty());
                var soul = new SoulEssence(body.TrueName + "Soul");
                soul.Relations.Add((Relations.Controls, body));
                World.Project(body, body.TrueName);
                World.Project(soul, soul.TrueName);
                Actors.Add((soul, body));
                Stations.Add(MakeObject("Station" + i, Occupied, Worked));
            }
            ObserveAll();
        }
        public Essence MakeObject(string name, params Tag[] tags)
        {
            var e = new Essence(name);
            e.Add(new SpatialProperty { Space = Room });
            var state = e.Add(new StateProperty());
            foreach (var tag in tags) { state.Observable.Add(tag); state.Values[tag] = 0; }
            World.Project(e, name);
            return e;
        }
        public Projection M(int actor, Essence e) => World.KnownProjection(Actors[actor].Soul, e)!;
        public void ObserveAll() { foreach (var (soul, _) in Actors) World.Observe(soul, World.Reality.Projections, Session.CTB.Now); }
        public ActionExecution Submit(int actor, Effect effect, double delay = 0) => Session.Submit(Actors[actor].Soul, new(M(actor, Actors[actor].Body), effect, delay));
        public void Dispose() => Session.Dispose();
    }

    static void HistoricalWorkWithoutOverlap()
    {
        using var f = new Fixture();
        var intervals = new List<(double Start, double End)>();
        f.Session.StageCommitted += e => Emit("work_stage", new { time = f.Session.CTB.Now, actor = e.Intent.Actor.Name, occupied = f.Stations.Select(s => Value(s, Occupied)).ToArray(), worked = f.Stations.Select(s => Value(s, Worked)).ToArray() });
        for (int i = 0; i < 3; i++)
        {
            var st = f.M(i, f.Stations[i]);
            var start = f.Session.CTB.Now + 0.3;
            var e = f.Submit(i, Seq(new Effect.Require(new Condition.Not(new Condition.Tagged(st, Occupied, 1))), new Effect.Set(st, Occupied, 1),
                new Effect.Wait(3, Seq(new Effect.Set(st, Worked, 1), new Effect.Set(st, Occupied, 0)))), 0.3);
            f.Session.CTB.RunUntil(start + 3);
            Check(e.Status == ExecutionStatus.Completed, "single station work completed " + i);
            intervals.Add((start, f.Session.CTB.Now));
            f.Session.CTB.RunUntil(f.Session.CTB.Now + 1);
        }
        var overlap = intervals.Max(x => x.Start) < intervals.Min(x => x.End);
        Emit("historical_work_counterexample", new { intervals = intervals.Select(x => new { x.Start, x.End }), workedAll = f.Stations.All(s => Value(s, Worked) == 1), occupiedAllNow = f.Stations.All(s => Value(s, Occupied) == 1), commonOverlap = overlap, productionHostExecuted = false });
        Check(!overlap && f.Stations.All(s => Value(s, Worked) == 1), "historical completion does not establish common overlap");
    }

    static void CleanupBoundary(bool lease, bool cancel)
    {
        using var f = new Fixture(1);
        var st = f.M(0, f.Stations[0]);
        Effect acquire = lease ? new Effect.TemporarySetUntilEnd(st, Occupied, 1) : new Effect.Set(st, Occupied, 1);
        var e = f.Submit(0, Seq(acquire, new Effect.Wait(1, new Effect.Require(new Condition.Constant(Truth.False)))));
        f.Session.CTB.RunUntil(0);
        var during = Value(f.Stations[0], Occupied);
        if (cancel) f.Session.Cancel(e); else f.Session.CTB.RunUntil(1);
        var after = Value(f.Stations[0], Occupied);
        Emit("cleanup", new { lease, cancel, during, after, status = e.Status.ToString() });
        Check(during == 1 && after == (lease ? 0 : 1), "cleanup boundary lease=" + lease + " cancel=" + cancel);
    }

    static void FalseAccessStatement()
    {
        using var f = new Fixture(2);
        var destination = new Essence("D", ProjectionKind.Space);
        f.World.Project(destination, "D");
        var route = new Essence("Q", ProjectionKind.Boundary);
        route.Add(new SpatialProperty { Space = f.Room });
        var state = route.Add(new StateProperty()); state.Observable.Add(Passable); state.Values[Passable] = 0;
        route.Add(new LockProperty { IsLocked = true });
        route.Add(new PassageProperty()).Connections[f.Room] = destination;
        f.World.Project(route, "Q");
        f.ObserveAll();
        var say = f.Submit(0, new Effect.Say(f.M(0, f.Actors[1].Body), "Q is passable."));
        f.Session.CTB.RunUntil(0);
        var heard = f.Actors[1].Soul.World.Projections.Last(p => p.Kind == ProjectionKind.Event && p.Name == "Q is passable.");
        Emit("plain_false_statement", new { status = say.Status.ToString(), realPassable = Value(route, Passable), believedPassable = f.M(1, route).Tags.GetValueOrDefault(Passable), source = heard.LastObservation!.Source.ToString(), procedurePresent = heard.Procedure is not null, senderPresent = heard.Relations.Any(r => r.Relation == Relations.Subject) });
        Check(say.Status == ExecutionStatus.Completed && route.Get<LockProperty>()!.IsLocked && heard.Procedure is null, "plain speech does not change the real passage");
        var mentalRoute = f.M(1, route);
        Observation.Observe(f.Actors[1].Soul, new("Q") { Target = mentalRoute, Source = ObservationSource.Testimony, Time = 0,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Passable, 1), Details = "Neutral accepted testimony fixture: Q is passable." });
        var travel = new Effect.Act(ActionOp.Travel, mentalRoute);
        Observation.Observe(f.Actors[1].Soul, new("Travel rule", ProjectionKind.Concept) { Source = ObservationSource.Introspection, Existence = Truth.True,
            Rules = [new Rule(new Condition.Tagged(mentalRoute, Passable, 1), RuleMode.Prefer, travel, 1, Truth.True), new Rule(new Condition.Not(new Condition.Tagged(mentalRoute, Passable, 1)), RuleMode.Forbid, travel, 10, Truth.True)] });
        var chosen = DecisionSystem.Choose(f.Actors[1].Soul.World, f.M(1, f.Actors[1].Body));
        Check(chosen is not null, "subjective false passage belief proposes travel");
        var attempt = f.Submit(1, chosen!.Effect);
        f.Session.CTB.RunUntil(0);
        Emit("false_access_attempt", new { believedPassable = mentalRoute.Tags.GetValueOrDefault(Passable), realLocked = route.Get<LockProperty>()!.IsLocked, status = attempt.Status.ToString(), failure = attempt.Result!.Failure.ToString(), location = f.Actors[1].Body.Get<SpatialProperty>()!.Space!.TrueName });
        Check(attempt.Status == ExecutionStatus.Failed && attempt.Result!.Failure == ActionFailure.ConditionFailed, "actual passage rejects false access belief");
        Check(!SpeechPolicy.Assess(0, 0, new SpeechHabits(NeverLies: true)).Any(c => c.Act == SpeechAct.Lie), "speech policy forbids Lie when invoked with NeverLies");
    }

    static void ExistingOverlapGateComposition()
    {
        using var f = new Fixture();
        f.Session.StageCommitted += _ => f.ObserveAll();
        f.Session.Completed += _ => f.ObserveAll();
        var executions = new List<ActionExecution>();
        for (int i = 0; i < 3; i++)
        {
            var own = f.M(i, f.Stations[i]);
            Effect continuation;
            if (i == 0)
            {
                var ready = All(f.Stations.Select(s => (Condition)new Condition.Tagged(f.M(i, s), Occupied, 1)).ToArray());
                continuation = new Effect.Wait(ready, Seq(new Effect.Require(ready), new Effect.Set(f.M(i, f.Result), Gate, 1)));
            }
            else continuation = new Effect.Wait(new Condition.Tagged(f.M(i, f.Result), Gate, 1), new Effect.Require(new Condition.Tagged(f.M(i, f.Result), Gate, 1)));
            executions.Add(f.Submit(i, Seq(new Effect.Require(new Condition.Not(new Condition.Tagged(own, Occupied, 1))), new Effect.TemporarySetUntilEnd(own, Occupied, 1), continuation), i));
        }
        f.Session.CTB.RunUntil(5, 1000);
        Emit("existing_overlap_composition", new { result = Value(f.Result, Gate), statuses = executions.Select(e => e.Status.ToString()).ToArray(), occupied = f.Stations.Select(s => Value(s, Occupied)).ToArray(), actualGroupTransaction = false, fixtureObservationScope = "neutral colocated objects on stage/completion", productionHostExecuted = false });
        Check(Value(f.Result, Gate) == 1 && executions.All(e => e.Status == ExecutionStatus.Completed) && f.Stations.All(s => Value(s, Occupied) == 0), "existing Core can compose one overlap-checked effect with lifecycle leases");
    }

    static void UnboundInvitationTarget()
    {
        using var f = new Fixture(2);
        var promised = new RelationTag("NeutralPromised");
        var receiver = f.M(1, f.Actors[1].Body);
        Observation.Observe(f.Actors[1].Soul, new(receiver.Name, receiver.Kind) { Target = receiver, Source = ObservationSource.Introspection,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Cooperation, 1) });
        var bad = new Effect.Think(new Condition.Related(new Noun.Slot(RuleSlot.Target), promised, f.M(0, f.Result)));
        var sending = f.Submit(0, new Effect.Say(f.M(0, f.Actors[1].Body), "Neutral invitation with target slot", bad));
        f.Session.CTB.RunUntil(0);
        var request = f.Actors[1].Soul.World.Projections.Last(p => p.Name == "Neutral invitation with target slot");
        var attitude = SocialSystem.Evaluate(f.Actors[1].Soul.World, receiver, request);
        var candidate = DecisionSystem.Choose(f.Actors[1].Soul.World, receiver);
        Emit("unbound_invitation", new { sendStatus = sending.Status.ToString(), requestHasProcedure = request.Procedure is not null,
            stance = attitude.Stance.ToString(), score = attitude.Score, candidatePresent = candidate is not null,
            unresolvedSlots = Ast.Nouns(request.Procedure!).OfType<Noun.Slot>().Select(n => n.Role.ToString()).ToArray() });
        Check(attitude.Stance == SocialStance.Accept && candidate is null, "accepted invitation with receiver Target slot never becomes a candidate");
        var good = new Effect.Think(new Condition.Related(new Noun.Slot(RuleSlot.Actor), promised, f.M(0, f.Result)));
        f.Submit(0, new Effect.Say(f.M(0, f.Actors[1].Body), "Neutral invitation with actor slot", good));
        f.Session.CTB.RunUntil(0);
        var response = f.Session.Decide(f.Actors[1].Soul, receiver, 0);
        f.Session.CTB.RunUntil(0);
        Emit("bound_invitation_comparison", new { status = response?.Status.ToString(), promised = receiver.Relations.Contains((promised, f.M(1, f.Result))) });
        Check(response?.Status == ExecutionStatus.Completed && receiver.Relations.Contains((promised, f.M(1, f.Result))), "receiver Actor slot completes the neutral commitment using existing Core");
    }

    public static void Main()
    {
        Emit("scope", new { mode = "DETECTION_ONLY", designFilesRead = false, storyFilesRead = false, productionChanges = false, hardIsolationClaim = false });
        HistoricalWorkWithoutOverlap();
        CleanupBoundary(false, false); CleanupBoundary(false, true); CleanupBoundary(true, false); CleanupBoundary(true, true);
        FalseAccessStatement();
        ExistingOverlapGateComposition();
        UnboundInvitationTarget();
        Emit("complete", new { passed = true, acceptanceLevel = "neutral Core mechanism experiment only" });
    }
}
