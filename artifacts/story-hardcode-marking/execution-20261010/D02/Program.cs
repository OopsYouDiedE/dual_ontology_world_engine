using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

// Neutral diagnostic inputs only. Production Core is referenced unchanged.
static class Probe
{
    static readonly Tag OldWork = new("NeutralOldWork");
    static readonly Tag NewWork = new("NeutralNewWork");

    static void Emit(string scenario, string checkpoint, object data)
        => Console.WriteLine(JsonSerializer.Serialize(new { scenario, checkpoint, data }));

    sealed class Fixture : IDisposable
    {
        public WorldRuntime Runtime = new();
        public SoulEssence Soul = new("NeutralSoul");
        public Essence Body = new("NeutralBody", ProjectionKind.Body);
        public Essence RoomA = new("NeutralRoomA", ProjectionKind.Space);
        public Essence RoomB = new("NeutralRoomB", ProjectionKind.Space);
        public Essence Object = new("NeutralObject");
        public Projection Self;
        public Projection MentalA;
        public Projection MentalB;
        public Projection MentalObject;
        public Projection OldPhase;
        public Projection Settled;
        public Projection OldBasis;
        public Projection NewBasis;
        public SimulationSession Session;
        public Effect OldEffect;
        public Effect NewEffect;

        public Fixture()
        {
            Runtime.Project(RoomA, RoomA.TrueName);
            Runtime.Project(RoomB, RoomB.TrueName);
            Body.Add(new SpatialProperty { Space = RoomA });
            var state = Body.Add(new StateProperty());
            state.Values[OldWork] = 0;
            state.Values[NewWork] = 0;
            state.Observable.UnionWith([OldWork, NewWork, Tags.Cooperation]);
            Object.Add(new SpatialProperty { Space = RoomA });
            Runtime.Project(Body, Body.TrueName);
            Runtime.Project(Object, Object.TrueName);
            Soul.Relations.Add((Relations.Controls, Body));
            Runtime.Project(Soul, Soul.TrueName);
            Runtime.Observe(Soul, Runtime.Representations(RoomA)
                .Concat(Runtime.Representations(RoomB))
                .Concat(Runtime.Representations(Body))
                .Concat(Runtime.Representations(Object)));
            Self = Runtime.KnownProjection(Soul, Body)!;
            MentalA = Runtime.KnownProjection(Soul, RoomA)!;
            MentalB = Runtime.KnownProjection(Soul, RoomB)!;
            MentalObject = Runtime.KnownProjection(Soul, Object)!;
            OldPhase = Concept("NeutralOldPhase", Truth.True);
            Settled = Concept("NeutralSettled", Truth.True);
            OldBasis = Concept("NeutralOldRules", Truth.True);
            NewBasis = Concept("NeutralNewRules", Truth.True);
            OldEffect = new Effect.Set(Self, OldWork, 1);
            NewEffect = new Effect.Set(Self, NewWork, 1);
            Session = new SimulationSession(Runtime);
        }

        public Projection Concept(string name, Truth existence)
            => Observation.Observe(Soul, new(name, ProjectionKind.Concept)
                { Source = ObservationSource.Introspection, Existence = existence });

        public void Rules(Projection basis, params Rule[] rules)
            => Observation.Observe(Soul, new(basis.Name, basis.Kind)
                { Target = basis, Source = ObservationSource.Introspection, Rules = [.. rules] });

        public Rule Prefer(Condition condition, Effect effect, int weight)
            => new(condition, RuleMode.Prefer, effect, weight, Truth.True);

        public void Standard(Condition? scope = null)
        {
            var condition = scope ?? new Condition.Constant(Truth.True);
            Rules(OldBasis, Prefer(condition, OldEffect, 5),
                Prefer(new Condition.All([condition, new Condition.Exists(Settled)]), OldEffect, 20));
            Rules(NewBasis, Prefer(new Condition.Constant(Truth.True), NewEffect, 2));
        }

        public void ChangeExistence(Projection projection, Truth existence)
            => Observation.Observe(Soul, new(projection.Name, projection.Kind)
                { Target = projection, Source = ObservationSource.Introspection, Existence = existence });

        public void SenseBody(double time = 0)
            => Runtime.Observe(Soul, Runtime.Representations(RoomA)
                .Concat(Runtime.Representations(RoomB)).Concat(Runtime.Representations(Body)), time);

        public object Assess() => DecisionSystem.Assess(Soul.World, Self).Select(a => new
        {
            effect = Name(a.Decision.Effect), score = a.Decision.Score,
            basis = a.Decision.Basis.Name, forbidden = a.Forbidden, reasons = a.Reasons,
        }).ToArray();

        public string Name(Effect effect) => Ast.Equivalent(effect, OldEffect) ? "old"
            : Ast.Equivalent(effect, NewEffect) ? "new" : effect.GetType().Name;

        public object State() => new
        {
            objectiveRoom = Body.Get<SpatialProperty>()!.Space?.TrueName,
            mentalRoom = Self.Parent?.Name,
            objectiveOldWork = Body.Get<StateProperty>()!.Values.GetValueOrDefault(OldWork),
            mentalOldWork = Self.Tags.GetValueOrDefault(OldWork),
            oldPhase = OldPhase.Existence.ToString(), basisExistence = OldBasis.Existence.ToString(),
            basisRules = OldBasis.Rules.Length, mentalObjectExistence = MentalObject.Existence.ToString(),
            choices = Assess(),
        };

        public void Dispose() => Session.Dispose();
    }

    static void BaselineAndBasis()
    {
        using var f = new Fixture();
        f.Standard();
        f.Body.Get<SpatialProperty>()!.Space = f.RoomB;
        f.Runtime.Sync();
        f.SenseBody();
        f.ChangeExistence(f.OldPhase, Truth.False);
        Emit("unscoped", "after_new_phase_and_known_move", f.State());
        f.ChangeExistence(f.OldBasis, Truth.False);
        Emit("basis_false", "same_rules_remain", f.State());
        f.Rules(f.OldBasis, f.OldBasis.Rules.Select(r => r with { Acceptance = Truth.False }).ToArray());
        Emit("rule_acceptance_false", "same_array_count", f.State());
    }

    static void ExplicitScopeAndClear()
    {
        using var f = new Fixture();
        f.Standard(new Condition.Exists(f.OldPhase));
        Emit("phase_scope", "old_phase_true", f.State());
        f.ChangeExistence(f.OldPhase, Truth.False);
        Emit("phase_scope", "old_phase_false", f.State());
        f.ChangeExistence(f.OldPhase, Truth.True);
        var historyBefore = f.Soul.World.History.Count;
        f.Rules(f.OldBasis);
        Emit("rules_empty", "current_rules_replaced", new
        {
            basisRetained = f.Soul.World.Contains(f.OldBasis),
            oldRules = f.OldBasis.Rules.Length,
            phaseRetained = f.Soul.World.Contains(f.OldPhase),
            settledRetained = f.Soul.World.Contains(f.Settled),
            historyBefore, historyAfter = f.Soul.World.History.Count, choices = f.Assess(),
        });
    }

    static void GoalState()
    {
        using var f = new Fixture();
        f.Standard();
        var execution = f.Session.Decide(f.Soul, f.Self, 1)!;
        f.Session.CTB.Step();
        f.SenseBody(1);
        Emit("unconditional_goal", "goal_already_satisfied", new { status = execution.Status.ToString(), state = f.State() });
        f.Standard(new Condition.Not(new Condition.Tagged(f.Self, OldWork, 1)));
        Emit("explicit_unmet_goal", "same_weights_condition_false", f.State());
    }

    static void RemovedTarget()
    {
        using var f = new Fixture();
        f.OldEffect = new Effect.Act(ActionOp.Take, f.MentalObject);
        f.Rules(f.OldBasis, f.Prefer(new Condition.Constant(Truth.True), f.OldEffect, 10));
        f.Rules(f.NewBasis, f.Prefer(new Condition.Constant(Truth.True), f.NewEffect, 2));
        f.Runtime.Remove(f.Object);
        Emit("removed_target", "before_sensing_removed_target", f.State());
        var failed = f.Session.Decide(f.Soul, f.Self, 1)!;
        f.Session.CTB.Step();
        Emit("removed_target", "execution_failed", new
        {
            status = failed.Status.ToString(), failure = failed.Result!.Failure.ToString(), state = f.State(),
        });
        f.Runtime.Observe(f.Soul, f.Runtime.Representations(f.Object), 1);
        Emit("removed_target", "false_target_unconditional_rule", f.State());
        f.Rules(f.OldBasis, f.Prefer(new Condition.Exists(f.MentalObject), f.OldEffect, 10));
        Emit("removed_target", "explicit_exists_guard", f.State());
    }

    static void SpatialKnowledge()
    {
        using var f = new Fixture();
        f.Standard(new Condition.At(f.Self, f.MentalA));
        Emit("space_scope", "initial_room", f.State());
        f.Body.Get<SpatialProperty>()!.Space = f.RoomB;
        f.Runtime.Sync();
        Emit("space_scope", "objective_move_before_perception", f.State());
        f.SenseBody();
        Emit("space_scope", "after_perception", f.State());
    }

    static void BoundIntent()
    {
        using var f = new Fixture();
        f.Standard();
        var execution = f.Session.Decide(f.Soul, f.Self, 1)!;
        f.Rules(f.OldBasis);
        Emit("bound_intent", "rules_change_during_pending_action", new
        {
            pendingEffect = f.Name(execution.Intent.Changes), status = execution.Status.ToString(), choices = f.Assess(),
        });
        f.Session.CTB.Step();
        Emit("bound_intent", "already_bound_action_finishes", new { status = execution.Status.ToString(), state = f.State() });
    }

    static void RepeatedAutonomy()
    {
        using var f = new Fixture();
        f.Standard();
        var completed = new List<object>();
        f.Session.Completed += execution => completed.Add(new
        {
            effect = f.Name(execution.Intent.Changes), status = execution.Status.ToString(),
            failure = execution.Result!.Failure.ToString(), time = execution.Result.Time,
        });
        f.Session.Activate(f.Soul, f.Self, 1);
        for (var i = 0; i < 6; i++) f.Session.CTB.Step();
        f.Session.Deactivate(f.Self);
        Emit("autonomy", "six_scheduler_steps_unconditional_goal", new { completed, state = f.State() });
    }

    static void RequestDeduplication()
    {
        using var f = new Fixture();
        Observation.Observe(f.Soul, new(f.Self.Name, f.Self.Kind)
        {
            Target = f.Self, Source = ObservationSource.Introspection,
            Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Cooperation, 4),
        });
        f.Rules(f.OldBasis, f.Prefer(new Condition.Constant(Truth.True), f.OldEffect, 3));
        var request = Observation.Observe(f.Soul, new("NeutralRequest", ProjectionKind.Event)
        {
            Source = ObservationSource.Testimony, Existence = Truth.True, Procedure = f.NewEffect,
        });
        Emit("request_dedup", "accepted_request_no_equivalent_preference", new
        {
            social = SocialSystem.Evaluate(f.Soul.World, f.Self, request), choices = f.Assess(),
        });
        f.Rules(f.NewBasis, f.Prefer(new Condition.Constant(Truth.True), f.NewEffect, 2));
        Emit("request_dedup", "accepted_request_equivalent_preference_added", new
        {
            social = SocialSystem.Evaluate(f.Soul.World, f.Self, request), choices = f.Assess(),
        });
    }

    public static void Main()
    {
        BaselineAndBasis();
        ExplicitScopeAndClear();
        GoalState();
        RemovedTarget();
        SpatialKnowledge();
        BoundIntent();
        RepeatedAutonomy();
        RequestDeduplication();
    }
}
