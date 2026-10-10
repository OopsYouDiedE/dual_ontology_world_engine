using System.Collections.Immutable;
using System.Diagnostics;
using DualOntologyWorldEngine.Core;

static class CognitionRegressionProgram
{
    static int checks;
    static readonly Tag Mark = new("mark"), Value = new("value");
    static readonly RelationTag Link = new("link"), Denied = new("denied");
    static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        checks++; Console.WriteLine("PASS " + name);
    }
    static T Ok<T>(ContractResult<T> result) => result is ContractResult<T>.Success s ? s.Value :
        throw new Exception("Unexpected failure: " + ((ContractResult<T>.Failure)result).Error);
    static ContractError Error<T>(ContractResult<T> result) => result is ContractResult<T>.Failure f ? f.Error : throw new Exception("Expected failure");
    static SubjectProjectionId Id(Projection p) => p.SubjectId!.Value;
    static CognitionSnapshotV1 Capture(SoulEssence s) => Ok(s.World.CaptureSnapshot());
    static FrozenNounV1 Ref(Projection p) => new FrozenNounV1.Reference(Id(p));
    static ObservationChangeV1 Change(Projection p) => new(Id(p), false, p.Name, p.Kind, ObservationSource.Perception, 0,
        ObservationTimeUnitV1.Unspecified, null, null, false, null, ImmutableDictionary<Tag, int?>.Empty, [], [], [], null, null, false, false, null, "", null, []);
    static ImmutableArray<KnownAuthorTermV1> Term(string id, string text) => [new(new(id), text, text.Normalize())];
    static Projection Add(SoulEssence s, string name, ProjectionKind kind = ProjectionKind.Concept) => Observation.Observe(s, new(name, kind)
        { Source = ObservationSource.Introspection, Existence = Truth.True });
    static void Forget(SoulEssence s, Projection p) => Observation.Observe(s, new(p.Name, p.Kind) { Target = p, Forget = true });

    sealed class Fixture
    {
        internal WorldRuntime Runtime = new();
        internal SoulEssence Soul;
        internal Essence Entity;
        internal Projection Actor, A, B, Room, OtherRoom, Front, Back, Habit, Sender, Request, History;
        internal FacetId FrontId, BackId;
        internal Fixture(int shape)
        {
            var prefix = "w" + shape + ":";
            Soul = new(new EntityId(prefix + "observer"), "observer");
            var space1 = new Essence(new EntityId(prefix + "space1"), "space1", ProjectionKind.Space);
            var space2 = new Essence(new EntityId(prefix + "space2"), "space2", ProjectionKind.Space);
            var r1 = Runtime.Project(space1, new(prefix + "space1"), "space1");
            var r2 = Runtime.Project(space2, new(prefix + "space2"), "space2", shape == 0 ? null : r1);
            var body = new Essence(new EntityId(prefix + "body"), "body", ProjectionKind.Body);
            body.Add(new SpatialProperty { Space = shape == 0 ? space1 : space2 });
            body.Add(new StateProperty()).Observable.Add(Value);
            Runtime.Project(Soul, new(prefix + "soul"), "observer");
            var rb = Runtime.Project(body, new(prefix + "body"), "body"); Soul.Relations.Add((Relations.Controls, body));
            Entity = new(new EntityId(prefix + "entity"), "unit");
            Entity.Add(new SpatialProperty { Space = shape == 0 ? space1 : space2 });
            Entity.Add(new StateProperty()).Observable.UnionWith([Mark, Value]);
            Entity.Get<StateProperty>()!.Values[Mark] = 0;
            FrontId = new(prefix + "front"); BackId = new(prefix + "back");
            Front = Runtime.Project(Entity, FrontId, "front", r1);
            Back = Runtime.Project(Entity, BackId, "back", shape == 0 ? r1 : r2);
            var observed = Runtime.Observe(Soul, [r1, r2, rb, Front, Back]);
            Room = observed[0]; OtherRoom = observed[1]; Actor = observed[2]; A = observed[3]; B = observed[4];
            Ok(Observation.ObserveBatch(Soul, [Change(A) with { KnownTerms = Term("term:unit", "模块") }, Change(B) with { KnownTerms = Term("term:unit", "模块") }]));
            Habit = Add(Soul, "policy"); Sender = Add(Soul, "sender");
            History = Observation.Observe(Soul, new("previous", ProjectionKind.Event) { Existence = Truth.True,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, 2), AddRelations = [(Relations.Subject, Sender)] });
            Request = Observation.Observe(Soul, new("request", ProjectionKind.Event) { Existence = Truth.True,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(SocialSystem.RequestCompatibility, 1),
                AddRelations = [(Relations.Subject, Sender)], Procedure = new Effect.Act(ActionOp.Inspect, A) });
        }
    }

    static Fixture World(int shape)
    {
        // Fixture constructor is kept explicit so the two parent topologies are visible in the test.
        return new Fixture(shape);
    }

    static ImmutableArray<FrozenRuleV1> Rules(Fixture f, int n)
    {
        var condition = new FrozenConditionV1.Constant(Truth.True);
        var effect = new FrozenEffectV1.Act(ActionOp.Inspect, Ref(f.A));
        return [new(condition, RuleMode.Prefer, effect, n, Truth.True), new(condition, RuleMode.Require, effect, n, Truth.True),
            new(condition, RuleMode.Forbid, effect, n, Truth.True), new(condition, RuleMode.Permit, effect, n, Truth.True)];
    }
    static ImmutableArray<ObservationChangeV1> State(Fixture f, int n) =>
        [Change(f.A) with { Name = "view-" + n, Kind = n == 0 ? ProjectionKind.Object : ProjectionKind.Material,
            Existence = Truth.True, Move = true, Parent = Id(n == 0 ? f.Room : f.OtherRoom),
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Mark, n),
            AddRelations = n == 0 ? [] : [new(Link, Id(f.B))], RemoveRelations = n == 0 ? [new(Link, Id(f.B))] : [],
            DenyRelations = [new(Denied, Id(f.B))], Rules = Rules(f, n + 1), Procedure = new FrozenEffectV1.Set(Ref(f.A), Value, n),
            SourceDependencies = [Id(f.B)], Details = "observed-" + n },
         Change(f.B) with { Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Mark, n) }];

    static void Atomic(Fixture f, string label)
    {
        Ok(Observation.ObserveBatch(f.Soul, State(f, 0)));
        var start = f.Soul.World.Revision.Value;
        var mixed = 0; var reads = 0;
        using var ready = new ManualResetEventSlim();
        var reader = Task.Run(() =>
        {
            ready.Set();
            for (var i = 0; i < 100; i++)
            {
                var s = Capture(f.Soul); var a = s.Projections.Single(p => p.Id == Id(f.A)); var b = s.Projections.Single(p => p.Id == Id(f.B));
                var n = a.Tags[Mark];
                if (b.Tags[Mark] != n || a.Name != "view-" + n || a.Parent != Id(n == 0 ? f.Room : f.OtherRoom) ||
                    a.Rules[0].Weight != n + 1 || a.Rules[0].Acceptance != Truth.True || a.Kind != (n == 0 ? ProjectionKind.Object : ProjectionKind.Material) ||
                    a.Relations.Any(r => r.Relation == Link) != (n == 1) || a.DeniedRelations.Length != 1 ||
                    a.Procedure is not FrozenEffectV1.Set { Value: var v } || v != n) Interlocked.Increment(ref mixed);
                if (Ast.Evaluate(new Condition.All([new Condition.Tagged(f.A, Mark, 1), new Condition.Not(new Condition.Tagged(f.B, Mark, 1))]), new(f.Actor)) != Truth.False)
                    Interlocked.Increment(ref mixed);
                Interlocked.Increment(ref reads); Thread.Yield();
            }
        });
        ready.Wait();
        for (var i = 0; i < 30; i++) Ok(Observation.ObserveBatch(f.Soul, State(f, i % 2)));
        reader.GetAwaiter().GetResult();
        Check(mixed == 0 && reads == 100 && f.Soul.World.Revision.Value == start + 30, label + " CB-V01 atomic full graph + AST (100 reads/30 commits)");
        var old = Capture(f.Soul);
        var foreign = Add(new SoulEssence("foreign"), "foreign-node");
        var result = Observation.ObserveBatch(f.Soul, [Change(f.A) with { Name = "should-not-publish" }, Change(f.B) with { Move = true, Parent = Id(foreign) }]);
        Console.WriteLine("EXPECTED CB-V02 " + Error(result).Code);
        Check(Error(result).Code == ContractErrorCode.InvalidReference && ReferenceEquals(old, Capture(f.Soul)), label + " CB-V02 foreign last input rollback");
        result = Observation.ObserveBatch(f.Soul, [Change(f.A) with { Move = true, Parent = Id(f.B) }, Change(f.B) with { Move = true, Parent = Id(f.A) }]);
        Check(Error(result).Code == ContractErrorCode.InvalidBatch && ReferenceEquals(old, Capture(f.Soul)), label + " CB-V02 final parent cycle rollback");
        Check(Error(Observation.ObserveBatch(f.Soul, [Change(f.A), Change(f.A)])).Code == ContractErrorCode.InvalidBatch, label + " duplicate target InvalidBatch");
        var empty = Ok(Observation.ObserveBatch(f.Soul, []));
        Check(empty.Revision == old.Revision && empty.NotificationStatus == NotificationDeliveryV1.Completed, label + " empty batch has no revision");
        var facet = new FacetId("failed:" + label);
        var unseen = f.Runtime.Project(new Essence("unseen"), facet, "unseen");
        try { f.Runtime.Observe(f.Soul, [f.Front, unseen], transform: new BadReferenceTransform(foreign)); throw new Exception("expected invalid reference"); }
        catch (CognitionContractException e)
        {
            Check(e.Error.Code == ContractErrorCode.InvalidReference && ReferenceEquals(old, Capture(f.Soul)) &&
                Error(f.Runtime.KnownFacet(f.Soul, facet)).Code == ContractErrorCode.Unrecognized, label + " CB-V02 failed engine batch publishes no binding");
        }
        var malformed = Change(f.A) with { SourceDependencies = default };
        Check(Error(Observation.ObserveBatch(f.Soul, [malformed])).Code == ContractErrorCode.MalformedData && ReferenceEquals(old, Capture(f.Soul)), label + " hand-built default batch array rejected before publish");
    }

    static void Notifications(Fixture f, string label)
    {
        var start = f.Soul.World.Revision; var notified = new List<long>(); var bindingSeen = false; var full = true;
        CognitionCommitReceiptV1? reentrant = null;
        void First(CognitionChangedV1 change)
        {
            notified.Add(change.PublishedRevision.Value);
            var s = Capture(f.Soul); full &= s.Revision.Value >= change.PublishedRevision.Value;
            bindingSeen |= Ok(f.Runtime.KnownFacet(f.Soul, f.FrontId)) == f.A;
            if (change.PublishedRevision.Value == start.Value + 1)
                reentrant = Ok(Observation.ObserveBatch(f.Soul, [Change(f.Sender) with { Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, 3) }]));
        }
        void Fault(CognitionChangedV1 _) => throw new InvalidOperationException("fixture observer fault");
        f.Soul.World.CognitionChanged += First; f.Soul.World.CognitionChanged += Fault;
        f.Runtime.Observe(f.Soul, [f.Front]);
        f.Soul.World.CognitionChanged -= First; f.Soul.World.CognitionChanged -= Fault;
        var r1 = Ok(f.Soul.World.ReadNotificationReceipt(new(start.Value + 1)));
        var r2 = Ok(f.Soul.World.ReadNotificationReceipt(new(start.Value + 2)));
        Check(full && bindingSeen && notified.SequenceEqual([start.Value + 1, start.Value + 2]) && reentrant is { NotificationStatus: NotificationDeliveryV1.Pending } &&
            r1.Status == NotificationDeliveryV1.Completed && r1.NotificationFaults.Length == 1 && r2.NotificationFaults.Length == 1,
            label + " CB-V03 complete binding, reentry order, Pending/Completed + NotificationFaults");
        var receipt = Ok(Observation.ObserveBatch(f.Soul, [Change(f.A)]));
        Check(receipt.NotificationStatus == NotificationDeliveryV1.Completed && receipt.NotificationFaults.IsEmpty, label + " clean notification receipt");
        Check(Error(f.Soul.World.ReadNotificationReceipt(new(-1))).Code == ContractErrorCode.InvalidRevision &&
            Error(f.Runtime.ReadCognitionAudit(f.Soul, new(f.Soul.World.Revision.Value + 1))).Code == ContractErrorCode.InvalidRevision, label + " invalid receipt/audit revisions rejected");
        // A new reality facet must be bound before its first notification as well.
        var facet = new FacetId("extra:" + label); var real = f.Runtime.Project(new Essence("extra"), facet, "extra");
        var seenNew = false;
        void New(CognitionChangedV1 _) => seenNew = f.Runtime.KnownFacet(f.Soul, facet) is ContractResult<Projection>.Success;
        f.Soul.World.CognitionChanged += New; f.Runtime.Observe(f.Soul, [real]); f.Soul.World.CognitionChanged -= New;
        Check(seenNew, label + " CB-V03 new facet binding visible in callback");
        var before = Capture(f.Soul);
        try { f.Runtime.Observe(f.Soul, [f.Front], transform: new ReentrantTransform(f.Soul, f.Sender)); throw new Exception("expected stale"); }
        catch (CognitionContractException e) { Check(e.Error.Code == ContractErrorCode.StaleRevision && f.Soul.World.Revision.Value == before.Revision.Value + 1, label + " transform reentry rejects stale outer transaction"); }
    }
    sealed class ReentrantTransform(SoulEssence soul, Projection p) : IProjectionTransform
    {
        public ObservationInput Transform(ObservationInput input) { Observation.Observe(soul, new(p.Name, p.Kind) { Target = p }); return input; }
    }
    sealed class BadReferenceTransform(Projection foreign) : IProjectionTransform
    {
        public ObservationInput Transform(ObservationInput input) => input with { Move = true, Parent = foreign };
    }

    static void Consumers(Fixture f, string label)
    {
        var requestEffect = new FrozenEffectV1.Act(ActionOp.Inspect, Ref(f.B));
        ImmutableArray<FrozenRuleV1> Policy(int n) => Rules(f, n + 1).SetItem(0, new(
            new FrozenConditionV1.Related(new FrozenNounV1.Slot(RuleSlot.Target), Link, Ref(f.B)),
            RuleMode.Prefer, new FrozenEffectV1.Act(ActionOp.Inspect, new FrozenNounV1.Slot(RuleSlot.Target)), n + 1, Truth.True));
        ImmutableArray<ObservationChangeV1> Set(int n) => [Change(f.Habit) with { Rules = Policy(n) },
            Change(f.Actor) with { Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Cooperation, n) },
            Change(f.Sender) with { Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, n) },
            Change(f.Request) with { Procedure = requestEffect, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(SocialSystem.RequestCompatibility, n + 1) }];
        // Clear the rules installed by CB-V01 so the full decision round has one coherent policy basis.
        Ok(Observation.ObserveBatch(f.Soul, [Change(f.A) with { Rules = [], AddRelations = [new(Link, Id(f.B))] }]));
        Ok(Observation.ObserveBatch(f.Soul, Set(0))); var start = f.Soul.World.Revision.Value;
        var write = Task.Run(() => { for (var i = 1; i <= 30; i++) { Ok(Observation.ObserveBatch(f.Soul, Set(i % 2))); Thread.Yield(); } });
        var rounds = new List<(long Rev, int Score, bool Forbidden)>();
        var invalidCandidates = 0;
        for (var i = 0; i < 80; i++)
        {
            var assessed = DecisionSystem.Assess(f.Soul.World, f.Actor);
            // Distinct targets prevent candidate deduplication from discarding the request's social score.
            if (assessed.Count != 2 || !assessed.Any(a => a.Decision.Basis == f.Habit && a.Decision.Effect is Effect.Act { Target: Noun.Reference r } && r.Projection == f.A) ||
                !assessed.Any(a => a.Decision.Basis == f.Request && a.Decision.Effect is Effect.Act { Target: Noun.Reference r } && r.Projection == f.B)) invalidCandidates++;
            foreach (var a in assessed) rounds.Add((a.Revision!.Value.Value, a.Decision.Score, a.Forbidden));
        }
        write.GetAwaiter().GetResult();
        Check(invalidCandidates == 0 && rounds.Count == 160 && rounds.All(a => !a.Forbidden && a.Score == 2 * (a.Rev == start ? 1 : (a.Rev - start) % 2 + 1)),
            label + " CB-V05 full Assess fixed revision (80 rounds/30 writes, policy + distinct social request)");
        var s = Capture(f.Soul); var priorRevision = s.Revision;
        var preview = Ok(SocialSystem.EvaluateSender(s, Id(f.Actor), Id(f.Sender), 1, Id(f.Request)));
        var actual = Ok(SocialSystem.Evaluate(s, Id(f.Actor), Id(f.Request), 1));
        Check(preview == actual && actual.Score == 3 && f.Soul.World.Revision == priorRevision, label + " CB-V06 social same snapshot + history, preview read only");
        Forget(f.Soul, f.History);
        var after = SocialSystem.EvaluateSender(f.Soul.World, f.Actor, f.Sender, 1, f.Request);
        Check(after.Score == 1 && Ok(f.Runtime.CaptureCognitionAudit(f.Soul)).Commits.Any(c => c.ForgottenIds.Contains(Id(f.History))), label + " CB-V06 forgotten history never restored from audit");
        var model = new CaseModel("neutral", ["choice"], [["a", "b"]], [new("view-0", Mark, "m")],
            [new([0], ImmutableDictionary<string, int>.Empty.Add("m", 0)), new([1], ImmutableDictionary<string, int>.Empty.Add("m", 1))]);
        var belief = Ok(Reasoner.Assess(s, model));
        Check(belief.Revision == s.Revision && belief.Candidates.Length >= 1, label + " Reasoner snapshot revision");
        var speaker = Add(f.Soul, "source-current");
        Ok(Observation.ObserveBatch(f.Soul, [Change(speaker) with { Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, 3) }]));
        var claim = Observation.Observe(f.Soul, new("neutral-claim") { Existence = Truth.True, Source = ObservationSource.Testimony,
            AddRelations = [(Relations.Subject, speaker)] });
        var claimSnapshot = Capture(f.Soul);
        Ok(Observation.ObserveBatch(f.Soul, [Change(speaker) with { Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, -3) }]));
        Check(Ok(Reasoner.Weight(claimSnapshot, Id(claim))).Factor == 3.5 && Reasoner.Weight(claim).Factor == 1,
            label + " Reasoner keeps current claim relation, LastObservation.Source, same-version sender trust");
        Ok(Observation.ObserveBatch(f.Soul, [Change(f.Sender) with { Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, 3) },
            Change(claim) with { Source = ObservationSource.Testimony, RemoveRelations = [new(Relations.Subject, Id(speaker))], AddRelations = [new(Relations.Subject, Id(f.Sender))] }]));
        Check(Reasoner.Weight(claim).Factor == 3.5, label + " Reasoner uses current sender relation rather than older source relation");
    }

    static void HistoryAndTerms(Fixture f, string label)
    {
        Ok(Observation.ObserveBatch(f.Soul, State(f, 0)));
        var old = Capture(f.Soul); var oldA = old.Projections.Single(p => p.Id == Id(f.A));
        var unknown = Add(f.Soul, "unknown");
        Ok(Observation.ObserveBatch(f.Soul, [Change(unknown) with { Existence = Truth.Unknown, KnownTerms = Term("term:unit", "模块") }]));
        var term = ChineseTermBridge.Interpret(Capture(f.Soul), new("模块"));
        Check(term.Status == TermInterpretationStatus.Ambiguous && term.Candidates.Length == 3 && term.Candidates.Any(c => c.Qualification == Truth.Unknown), label + " A ambiguity retains unknown candidates");
        var onlyUnknown = ChineseTermBridge.Interpret(Capture(f.Soul), new(Reference: Id(unknown)));
        Check(onlyUnknown.Status == TermInterpretationStatus.Unknown && onlyUnknown.ResolvedTarget is null, label + " A Unknown is not resolved");
        var unrecognized = ChineseTermBridge.Interpret(Capture(f.Soul), new("未获知词项"));
        Check(unrecognized.Status == TermInterpretationStatus.Unrecognized, label + " A unrecognized vocabulary");
        var qualifier = new FrozenConditionV1.IsKind(new FrozenNounV1.Slot(RuleSlot.Target), ProjectionKind.Material);
        Ok(Observation.ObserveBatch(f.Soul, State(f, 1)));
        var resolved = ChineseTermBridge.Interpret(Capture(f.Soul), new("模块", Qualifier: qualifier));
        Check(resolved.Status == TermInterpretationStatus.Resolved && Ok(f.Runtime.BindResolvedTerm(f.Soul, resolved)) == f.A, label + " A acquired qualifier resolves existing executor target");
        using (var session = new SimulationSession(f.Runtime))
        {
            var execution = session.Submit(f.Soul, new(f.Actor, new Effect.Act(ActionOp.Inspect, Ok(f.Runtime.BindResolvedTerm(f.Soul, resolved))), 0));
            session.CTB.RunUntil(session.CTB.Now);
            Check(execution.Status == ExecutionStatus.Completed, label + " A resolved target enters existing SimulationSession/Inspect");
        }
        var oldTruth = Ok(Ast.Evaluate(old, new FrozenConditionV1.Tagged(Ref(f.A), Mark, 1), new(Id(f.Actor))));
        Check(oldA.Name == "view-0" && oldA.Kind == ProjectionKind.Object && oldA.Rules[0].Weight == 1 && oldA.Procedure is FrozenEffectV1.Set { Value: 0 } && oldTruth.Value == Truth.False,
            label + " CB-V04 old graph/all fields/AST remain immutable");
        var oldId = Id(f.B); Forget(f.Soul, f.B);
        var current = Capture(f.Soul);
        Check(!current.Projections.Any(p => p.Id == oldId) && !current.VisibleHistory.Any(o => o.Target == oldId || o.Dependencies.Contains(oldId)) &&
            current.Projections.Single(p => p.Id == Id(f.A)).LastObservation is null && old.VisibleHistory.Any(o => o.Target == oldId), label + " CB-V04 forget purges visible dependency/history only");
        Check(ChineseTermBridge.Interpret(current, new(Reference: oldId)).Status == TermInterpretationStatus.InvalidReference &&
            Error(f.Runtime.BindResolvedTerm(f.Soul, resolved)).Code == ContractErrorCode.StaleRevision, label + " A forgotten reference + stale resolved receipt");
        var again = f.Runtime.Observe(f.Soul, [f.Back])[0]; f.B = again;
        Check(Id(again) != oldId, label + " CB-V04 reobserve allocates fresh handle");
        var audit = Ok(f.Runtime.CaptureCognitionAudit(f.Soul));
        var restored = Ok(f.Runtime.ReconstructCognitionAudit(audit, old.Revision));
        Check(restored.Projections.Single(p => p.Id == Id(f.A)).Rules[0].Weight == 1 && restored.Projections.Any(p => p.Id == oldId) &&
            audit.Commits.Any(c => c.ForgottenIds.Contains(oldId) && c.Changes.Any(d => d.Id == oldId && d.Before is not null && d.After is null)), label + " CB-V04 immutable audit + tombstone + old reconstruction");
        Check(Error(f.Runtime.ReconstructCognitionAudit(audit, new(-1))).Code == ContractErrorCode.InvalidRevision &&
            Error(f.Runtime.ReconstructCognitionAudit(audit, new(f.Soul.World.Revision.Value + 1))).Code == ContractErrorCode.InvalidRevision, label + " audit revision boundaries");
        var archive = Ok(f.Runtime.CaptureCognitionGraph(f.Soul));
        Check(archive.AllocatedIds.Contains(oldId) && !archive.SubjectSnapshot.Projections.Any(p => p.Id == oldId), label + " retired ID reserved only in engine archive");
    }

    sealed record ForeignCondition : FrozenConditionV1;
    static void AstRoundtrip(Fixture f, string label)
    {
        var actor = new FrozenNounV1.Slot(RuleSlot.Actor); var target = new FrozenNounV1.Slot(RuleSlot.Target);
        FrozenConditionV1 fact = new FrozenConditionV1.All([new FrozenConditionV1.Exists(Ref(f.A)), new FrozenConditionV1.Any([
            new FrozenConditionV1.At(actor, Ref(f.Room)), new FrozenConditionV1.Related(Ref(f.A), Link, Ref(f.B)),
            new FrozenConditionV1.IsKind(target, ProjectionKind.Object)]), new FrozenConditionV1.Not(new FrozenConditionV1.Constant(Truth.False)),
            new FrozenConditionV1.Tagged(Ref(f.A), Mark, 0)]);
        FrozenEffectV1 effect = new FrozenEffectV1.Sequence([new FrozenEffectV1.Act(ActionOp.Inspect, Ref(f.A), Ref(f.B)),
            new FrozenEffectV1.Set(actor, Value, 4), new FrozenEffectV1.Change(Ref(f.A), Value, -1), new FrozenEffectV1.Spend(Ref(f.A), Value, 1),
            new FrozenEffectV1.TemporarySet(Ref(f.A), Value, 3, 2), new FrozenEffectV1.TemporarySetUntilEnd(Ref(f.A), Value, 8),
            new FrozenEffectV1.Relate(Ref(f.A), Link, Ref(f.B), true), new FrozenEffectV1.When(fact, new FrozenEffectV1.Require(fact)),
            new FrozenEffectV1.Wait(2, new FrozenEffectV1.Think(fact), fact),
            new FrozenEffectV1.Say(Ref(f.Sender), "neutral-message", new FrozenEffectV1.Act(ActionOp.Inspect, target))]);
        Ok(Observation.ObserveBatch(f.Soul, [Change(f.Habit) with { Rules = [new(fact, RuleMode.Require, effect, 11, Truth.Unknown)], Procedure = effect }]));
        var snapshot = Capture(f.Soul); var graph = Ok(CognitionGraph.Rebind(snapshot));
        var thawed = Ok(CognitionAst.RebindEffect(effect, graph.Projections));
        var refrozen = Ok(CognitionAst.FreezeEffect(thawed, graph.Projections));
        var rebound = snapshot.Projections.Single(p => p.Id == Id(f.Habit)).Procedure!;
        Check(Ok(Ast.Equivalent(effect, rebound)) && Ok(Ast.Equivalent(effect, refrozen)) && Ok(Ast.Equivalent(fact, snapshot.Projections.Single(p => p.Id == Id(f.Habit)).Rules[0].Condition)) &&
            Ast.References(thawed).Where(p => Id(p) == Id(f.A)).All(p => ReferenceEquals(p, graph.Projections[Id(f.A)])) &&
            !ReferenceEquals(graph.Projections[Id(f.A)], f.A), label + " CB-V07 closed AST two-phase rebind preserves repeated references/slots");
        var bound = Ast.Bind(thawed, new(graph.Projections[Id(f.Actor)], graph.Projections[Id(f.A)], graph.Projections[Id(f.B)], graph.Projections[Id(f.Room)]));
        var say = ((Effect.Sequence)bound).Items.OfType<Effect.Say>().Single();
        Check(say.Request is Effect.Act { Target: Noun.Slot { Role: RuleSlot.Target } }, label + " CB-V07 Say.Request recipient slot remains deferred");
        Check(Error(Ast.Evaluate(snapshot, new ForeignCondition(), new(Id(f.Actor)))).Code == ContractErrorCode.Unsupported &&
            Error(Ast.Evaluate(snapshot, new FrozenConditionV1.All(default), new(Id(f.Actor)))).Code == ContractErrorCode.MalformedData &&
            Error(Ast.Evaluate(snapshot, new FrozenConditionV1.Constant((Truth)99), new(Id(f.Actor)))).Code == ContractErrorCode.Unsupported,
            label + " CB-V07 unknown AST/default array/enum rejected");
        Check(Error(CognitionAst.RebindEffect(effect, graph.Projections.Remove(Id(f.A)))).Code == ContractErrorCode.InvalidReference &&
            Error(CognitionAst.RebindEffect(effect, graph.Projections.SetItem(Id(f.A), f.A))).Code == ContractErrorCode.InvalidReference,
            label + " CB-V07 missing ID/mixed actual worlds rejected");
        var missingParent = new SubjectProjectionId(snapshot.SubjectWorld, Guid.NewGuid());
        var indirectMissing = snapshot with { Projections = [..snapshot.Projections.Select(p => p.Id == Id(f.Room) ? p with { Parent = Id(f.OtherRoom) } :
            p.Id == Id(f.OtherRoom) ? p with { Parent = missingParent } : p)] };
        Check(Error(CognitionGraph.Rebind(indirectMissing)).Code == ContractErrorCode.InvalidReference,
            label + " CB-V07 indirect missing parent rejected as InvalidReference");
        Check(ChineseTermBridge.Interpret(snapshot, new("模块", Qualifier: new ForeignCondition())).Status == TermInterpretationStatus.Unsupported, label + " A Unsupported distinct from Unknown");
        Console.WriteLine("DEFER CB-V07 wire codec and cross-process roundtrip: next batch");
    }

    static void Hidden(Fixture f, string label)
    {
        var snapshot = Capture(f.Soul);
        var query = new TermQueryV1("模块");
        var before = ChineseTermBridge.Interpret(snapshot, query);
        var decisions = Ok(DecisionSystem.Assess(snapshot, Id(f.Actor)));
        var model = new CaseModel("hidden-neutral", ["choice"], [["low", "high"]], [new(f.A.Name, Mark, "mark")],
            [new([0], ImmutableDictionary<string, int>.Empty.Add("mark", 0)), new([1], ImmutableDictionary<string, int>.Empty.Add("mark", 99))]);
        var priorBelief = Ok(Reasoner.Assess(snapshot, model));
        var state = f.Entity.Get<StateProperty>()!;
        state.Values[Value] = 99; state.Values[Mark] = 99; f.Runtime.Sync();
        var after = ChineseTermBridge.Interpret(Capture(f.Soul), query);
        var second = Ok(DecisionSystem.Assess(Capture(f.Soul), Id(f.Actor)));
        var laterBelief = Ok(Reasoner.Assess(Capture(f.Soul), model));
        Check(before.Status == after.Status && before.Candidates.SequenceEqual(after.Candidates) && decisions.Length == second.Length &&
            decisions.Zip(second).All(p => p.First.Score == p.Second.Score && p.First.Forbidden == p.Second.Forbidden) && ReferenceEquals(snapshot, Capture(f.Soul)),
            label + " CB-V09 hidden objective deformation cannot change interpretation/scoring");
        Check(priorBelief.Candidates.SequenceEqual(laterBelief.Candidates) && priorBelief.Credence.Zip(laterBelief.Credence).All(p => p.First.SequenceEqual(p.Second)),
            label + " CB-V09 hidden deformation cannot change Reasoner result from same model/snapshot");
        // Objective identity and binding never appear in the public snapshot graph.
        Check(typeof(FrozenProjectionV1).GetProperties().All(p => p.PropertyType != typeof(EntityId) && p.PropertyType != typeof(FacetId) &&
            p.PropertyType != typeof(Projection) && p.PropertyType != typeof(Essence) && p.PropertyType != typeof(WorldRuntime)), label + " CB-V09 no objective reverse channel in DTO");
        Console.WriteLine("DEFER CB-V09 isolated subjective simulation/model production: C batch");
    }

    static void Identities(Fixture f, string label)
    {
        var isolated = new SoulEssence("batch-owner");
        var first = Ok(isolated.World.AllocateSubjectProjectionId()); var second = Ok(isolated.World.AllocateSubjectProjectionId());
        var template = Change(f.A) with { Target = first, Create = true, Name = "new-one", Move = true, Parent = second, AddRelations = [new(Link, second)] };
        var created = Ok(Observation.ObserveBatch(isolated, [template, template with { Target = second, Name = "new-two", Move = false, Parent = null, AddRelations = [new(Link, first)] }]));
        Check(created.Revision.Value == 1 && Capture(isolated).Projections.Length == 2 && Capture(isolated).Projections.Single(p => p.Id == first).Parent == second,
            label + " typed reserved batch creates closed mutually referenced nodes in one revision");
        var failed = Ok(isolated.World.AllocateSubjectProjectionId());
        var invalidReservation = template with { Target = failed, Parent = Id(f.A), AddRelations = [] };
        Check(Error(Observation.ObserveBatch(isolated, [invalidReservation])).Code == ContractErrorCode.InvalidReference && isolated.World.Revision.Value == 1 &&
            Error(Observation.ObserveBatch(isolated, [invalidReservation with { Move = false, Parent = null }])).Code == ContractErrorCode.InvalidReference,
            label + " failed submitted reservation is retired without publishing a node");
        Check(Error(Ast.Evaluate(Capture(isolated), new FrozenConditionV1.Exists(new FrozenNounV1.Reference(default)), new(first))).Code == ContractErrorCode.InvalidIdentity &&
            Error(DecisionSystem.Assess(Capture(isolated), default(SubjectProjectionId))).Code == ContractErrorCode.InvalidIdentity, label + " default subject handles rejected as InvalidIdentity");
        Check(Error(f.Runtime.LookupSingleKnown(f.Soul, f.Entity)).Code == ContractErrorCode.Ambiguous && f.Runtime.KnownProjections(f.Soul, f.Entity).Length == 2, label + " CB-V11 distinct known facets, no first-item fallback");
        var unknownFacet = f.Runtime.Project(f.Entity, new("unseen:" + label), "unseen");
        Check(Error(f.Runtime.KnownFacet(f.Soul, new("unseen:" + label))).Code == ContractErrorCode.Unrecognized &&
            Error(f.Runtime.KnownFacet(f.Soul, new("absent:" + label))).Code == ContractErrorCode.InvalidReference, label + " unregistered facet vs unrecognized facet");
        f.Runtime.Remove(f.Entity);
        Check(f.Runtime.KnownProjections(f.Soul, f.Entity).Length == 2 && Ok(f.Runtime.KnownFacet(f.Soul, f.FrontId)) == f.A, label + " CB-V11 hidden removal does not erase subject known set");
        var replacement = new Essence("unit"); var rp = f.Runtime.Project(replacement, "front");
        var mental = f.Runtime.Observe(f.Soul, [rp])[0];
        Check(replacement.Id != f.Entity.Id && Id(mental) != Id(f.A), label + " CB-V11 same-name new entity has new identity");
        try { f.Runtime.Project(new Essence(f.Entity.Id, "other"), "other"); throw new Exception("Expected duplicate"); }
        catch (CognitionContractException e) { Check(e.Error.Code == ContractErrorCode.DuplicateIdentity, label + " retired entity ID cannot be reused"); }
        try { f.Runtime.Project(replacement, f.FrontId, "other"); throw new Exception("Expected duplicate"); }
        catch (CognitionContractException e) { Check(e.Error.Code == ContractErrorCode.DuplicateIdentity, label + " facet ID cannot be rebound"); }
        foreach (var bad in new[] { "", " e", "e ", "e\u0301" })
        {
            try { _ = new Essence(new EntityId(bad), "neutral"); throw new Exception("Expected invalid identity"); }
            catch (CognitionContractException e) { Check(e.Error.Code == ContractErrorCode.InvalidIdentity, label + " NFC/Ordinal identity rejects " + System.Text.Json.JsonSerializer.Serialize(bad)); }
        }
        var runtime = new WorldRuntime(); var subjects = Enumerable.Range(0, 3).Select(n => new SoulEssence("subject-" + n)).ToArray();
        var timer = Stopwatch.StartNew(); var mem = GC.GetTotalMemory(true);
        for (var i = 0; i < 16; i++)
        {
            var entity = new Essence("same-display"); var views = new[] { runtime.Project(entity, "surface"), runtime.Project(entity, "surface") };
            foreach (var subject in subjects)
            {
                var known = runtime.Observe(subject, views); var old = Id(known[0]); Forget(subject, known[0]);
                var fresh = runtime.Observe(subject, [views[0]])[0];
                Check(Id(fresh) != old && Id(fresh).SubjectWorld == subject.World.SubjectWorldId, label + " CB-V11 growth " + i + "/" + subject.TrueName);
            }
        }
        var all = subjects.SelectMany(s => Capture(s).Projections.Select(p => p.Id)).ToArray();
        Check(all.Distinct().Count() == all.Length, label + " CB-V11 multi-entity/facet/subject domains remain distinct");
        Console.WriteLine($"MEASURE {label} seed=0 entities=16 facets=32 subjects=3 operations=144 elapsed_ms={timer.ElapsedMilliseconds} memory_delta_bytes={GC.GetTotalMemory(true) - mem}");
        Console.WriteLine("DEFER CB-V11 cross-process restore/duplicate receipt protection: save batch");
    }

    static void IncrementalPublication(int shape, string label)
    {
        var soul = new SoulEssence("incremental-owner"); var runtime = new WorldRuntime();
        var root = Add(soul, "root", ProjectionKind.Space);
        var middle = Add(soul, "middle", ProjectionKind.Space);
        var leaf = Add(soul, "leaf"); var target = Add(soul, "dependency");
        Ok(Observation.ObserveBatch(soul, [Change(middle) with { Move = true, Parent = Id(root) },
            Change(leaf) with { Move = true, Parent = Id(shape == 0 ? root : middle) }]));
        FrozenEffectV1 effect = new FrozenEffectV1.Sequence([
            new FrozenEffectV1.Wait(1, new FrozenEffectV1.Set(new FrozenNounV1.Slot(RuleSlot.Actor), Value, 1),
                new FrozenConditionV1.Not(new FrozenConditionV1.Exists(Ref(target)))),
            new FrozenEffectV1.Say(new FrozenNounV1.Slot(RuleSlot.Actor), "neutral", new FrozenEffectV1.Think(
                new FrozenConditionV1.Any([new FrozenConditionV1.Tagged(Ref(target), Mark, 1)])))]);
        Ok(Observation.ObserveBatch(soul, [Change(leaf) with { Procedure = effect,
            Rules = [new(new FrozenConditionV1.Constant(Truth.True), RuleMode.Prefer, effect, 1, Truth.True)] }]));
        // Prime the lazy same-version reader, then change only unrelated data repeatedly.
        var prior = Capture(soul);
        var assessed = DecisionSystem.Assess(soul.World, leaf);
        for (var n = 0; n < 64; n++) Ok(Observation.ObserveBatch(soul,
            [Change(root) with { Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Value, n) }]));
        var current = Capture(soul); var rebound = Ok(CognitionGraph.Rebind(current));
        Check(prior.VisibleHistory.Length + 64 == current.VisibleHistory.Length &&
            prior.Projections.Single(p => p.Id == Id(root)).Tags.GetValueOrDefault(Value) == 0 &&
            rebound.Projections[Id(leaf)].Parent == rebound.Projections[Id(shape == 0 ? root : middle)] && assessed.Count == 1,
            label + " incremental history remains closed and old captured evaluation stays unchanged");
        var before = Capture(soul); var auditBefore = Ok(runtime.CaptureCognitionAudit(soul));
        var failed = Observation.ObserveBatch(soul, [Change(root) with { Move = true, Parent = Id(leaf) }]);
        Check(Error(failed).Code == ContractErrorCode.InvalidBatch && ReferenceEquals(before, Capture(soul)) &&
            Ok(runtime.CaptureCognitionAudit(soul)).Commits.Length == auditBefore.Commits.Length && root.Parent is null,
            label + " incremental indirect cycle rolls back warmed reader/state/history/audit");
        Forget(soul, target);
        var forgotten = Capture(soul); var node = forgotten.Projections.Single(p => p.Id == Id(leaf));
        Check(node.Rules.IsEmpty && node.Procedure is null && node.LastObservation is null &&
            !forgotten.VisibleHistory.Any(o => o.Dependencies.Contains(Id(target))) &&
            DecisionSystem.Assess(soul.World, leaf).Count == 0 && Ok(CognitionGraph.Rebind(forgotten)).Projections.Count == 3 &&
            prior.Projections.Single(p => p.Id == Id(leaf)).Procedure is not null,
            label + " incremental nested Wait.Until and Say.Request dependencies forgotten without audit recovery");
        Ok(Observation.ObserveBatch(soul, [Change(root)]));
        Ok(Observation.ObserveBatch(soul, [Change(root)]));
        var audit = Ok(runtime.CaptureCognitionAudit(soul));
        Check(audit.Commits[^1].Changes.IsEmpty && audit.Commits[^1].AppendedVisibleHistory.Length == 1 &&
            auditBefore.Commits.Length + 3 == audit.Commits.Length,
            label + " incremental identical observations append history with empty state diff and stable prior audit");
        var restored = Ok(runtime.ReconstructCognitionAudit(audit, prior.Revision));
        Check(restored.Projections.Any(p => p.Id == Id(target)) && restored.Projections.Single(p => p.Id == Id(leaf)).Procedure is not null,
            label + " incremental persistent audit reconstructs earlier nested AST exactly");
    }

    static int Main()
    {
        try
        {
            Console.WriteLine("Cognition foundation regression; neutral worlds; codec/C/D/host migration excluded");
            for (var shape = 0; shape < 2; shape++)
            {
                var label = shape == 0 ? "flat" : "nested"; var f = World(shape);
                Atomic(f, label); Notifications(f, label); Consumers(f, label); HistoryAndTerms(f, label); AstRoundtrip(f, label); Hidden(f, label); Identities(f, label);
                IncrementalPublication(shape, label);
            }
            Console.WriteLine($"TOTAL {checks} checks"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
