using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using DualOntologyWorldEngine.Awakening;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

// Injected neutral mechanism fixtures; no chapter answers or authored prose.
internal static class LearningHostCases
{
    private static T Value<T>(ContractResult<T> result) => result is ContractResult<T>.Success success
        ? success.Value : throw new Exception(JsonSerializer.Serialize(result));
    private static DialogueExpression Literal(string value) => new() { Value = JsonSerializer.SerializeToElement(value) };
    private static WorldEntityDefinition Entity(string id, string kind, string? room = null) => new()
    {
        Id = id, Name = id, Kind = kind,
        Properties = room is null ? [] : new() { ["space"] = new() { Type = "entity", Value = JsonSerializer.SerializeToElement(room) } }
    };
    private static WorldCandidateDefinition Method(string id, string operation, string target, int weight) => new()
    {
        Id = id, Kind = "reaction", Actor = "neutral.actor", Delay = 1,
        Policy = new() { Mode = "core", Weight = weight },
        Response = [new() { Op = "call", Name = operation, Args = [Literal(target)] }]
    };
    private static SubjectiveDecisionMemory Memory(ScriptCharacter actor) => Memory(actor.Soul, actor.Self);
    private static SubjectiveDecisionMemory Memory(SoulEssence soul, Projection self) =>
        SubjectiveDecisionMemory.Restore(ActionLearning.Capture(soul.World, self));
    private static void Observe(string name, object value) => Console.WriteLine("OBSERVATION " + name + " " + JsonSerializer.Serialize(value));

    public static void WorldFailureAndSave(Action<string, bool> check)
    {
        using var world = new WorldSession();
        world.Load(new() { Id = "neutral.learning", Entities = [
            Entity("neutral.local", "room"), Entity("neutral.remote", "room"),
            Entity("neutral.actor", "character", "neutral.local"), Entity("neutral.other", "character", "neutral.local"),
            Entity("neutral.outside", "object", "neutral.remote"), Entity("neutral.available", "object", "neutral.local")],
            Candidates = [Method("neutral.high", "take", "neutral.outside", 4), Method("neutral.low", "inspect", "neutral.available", 2)] });
        world.Process(new VerifiedPerception("neutral.actor", ["neutral.remote", "neutral.outside", "neutral.available"]));
        var initial = world.AssessReactions("neutral.actor");
        check("neutral_world_initial_preference_is_high_method", initial.First().CandidateId == "neutral.learning:neutral.high"
            && initial.First().Score > initial.Last().Score);
        var started = world.Process(new RequestReaction("neutral.actor")).OfType<CandidateActionStarted>().Single();
        var ended = world.Process(new AdvanceWorld(world.Simulation.CTB.Now + 2)).OfType<CandidateActionFinished>().Single();
        check("neutral_world_records_actual_failed_execution", started.CandidateId == "neutral.learning:neutral.high"
            && !ended.Succeeded && ended.Failure != ActionFailure.None
            && ReferenceEquals(world.Entity("neutral.outside").Get<SpatialProperty>()!.Space, world.Entity("neutral.remote")));
        var actor = world.Character("neutral.actor");
        var memory = Memory(actor).Capture();
        check("neutral_world_failure_has_attempt_and_failure_history", memory.Experiences.Single() is { Attempts: 1, Failures: 1, Successes: 0 });
        var reassessed = world.AssessReactions("neutral.actor");
        Observe("neutral-world-real-failure-reassessment", new { initial, ended, reassessed, memory });
        check("neutral_world_real_failure_changes_selected_method", reassessed.First().CandidateId == "neutral.learning:neutral.low"
            && reassessed.Single(a => a.CandidateId == "neutral.learning:neutral.high").Reasons.Contains("observed_action_failed:1"));
        check("neutral_world_failure_does_not_enter_other_subject_memory", !world.Character("neutral.other").Soul.World.Projections
            .Any(p => p.Tags.GetValueOrDefault(new("core.decision-experience.v1")) == 1));
        var bytes = Value(world.Save());
        var directory = Environment.GetEnvironmentVariable("LEARNING_HOST_EVIDENCE");
        if (directory is not null) File.WriteAllBytes(Path.Combine(directory, "neutral-world-failure-save.bin"), bytes);
        using var restored = Value(WorldSession.Restore(bytes));
        var recovered = Memory(restored.Character("neutral.actor")).Capture();
        var restoredChoice = restored.AssessReactions("neutral.actor");
        check("neutral_world_save_restores_exact_failure_experiences", JsonSerializer.Serialize(recovered) == JsonSerializer.Serialize(memory));
        check("neutral_world_save_restores_same_method_scores", JsonSerializer.Serialize(restoredChoice) == JsonSerializer.Serialize(reassessed));
        check("neutral_world_restored_actor_reacts_using_experience", restored.Process(new RequestReaction("neutral.actor"))
            .OfType<CandidateActionStarted>().Single().CandidateId == "neutral.learning:neutral.low");
        check("neutral_world_live_actor_reacts_using_experience", world.Process(new RequestReaction("neutral.actor"))
            .OfType<CandidateActionStarted>().Single().CandidateId == "neutral.learning:neutral.low");
        check("neutral_world_alternative_actually_completes", world.Process(new AdvanceWorld(world.Simulation.CTB.Now + 2))
            .OfType<CandidateActionFinished>().Single().Succeeded
            && restored.Process(new AdvanceWorld(restored.Simulation.CTB.Now + 2)).OfType<CandidateActionFinished>().Single().Succeeded);
    }

    public static void WorldFinaleCancellation(Action<string, bool> check)
    {
        using var world = new WorldSession();
        var candidate = Method("neutral.delayed", "take", "neutral.available", 4);
        candidate.Delay = 10;
        world.Load(new() { Id = "neutral.cancellation", Entities = [Entity("neutral.local", "room"),
            Entity("neutral.actor", "character", "neutral.local"), Entity("neutral.available", "object", "neutral.local")],
            Candidates = [candidate] });
        world.Process(new VerifiedPerception("neutral.actor", ["neutral.available"]));
        var started = world.Process(new RequestReaction("neutral.actor")).OfType<CandidateActionStarted>().Single();
        check("neutral_world_finale_fixture_has_real_pending_action", world.Simulation.Active.Single().Status == ExecutionStatus.Pending);
        world.EndChapterLifetimeAtFinale();
        var ended = world.Process(new AdvanceWorld(world.Simulation.CTB.Now)).OfType<CandidateActionFinished>().Single();
        var actor = world.Character("neutral.actor");
        var memory = Memory(actor).Capture();
        check("neutral_world_finale_reports_interruption_without_success", started.ExecutionId == ended.ExecutionId && !ended.Succeeded
            && memory.Experiences.Single() is { Attempts: 1, Interruptions: 1, Successes: 0, Failures: 0 });
        check("neutral_world_finale_clears_host_executions", ((IDictionary)Get(world, "_executions")).Count == 0
            && world.Simulation.Active.Count == 0);
        check("neutral_world_finale_does_not_commit_cancelled_effect", ReferenceEquals(world.Entity("neutral.available")
            .Get<SpatialProperty>()!.Space, world.Entity("neutral.local")));
        var bytes = Value(world.Save());
        var directory = Environment.GetEnvironmentVariable("LEARNING_HOST_EVIDENCE");
        if (directory is not null) File.WriteAllBytes(Path.Combine(directory, "neutral-world-cancelled-save.bin"), bytes);
        using var restored = Value(WorldSession.Restore(bytes));
        check("neutral_world_cancelled_savepoint_restores_interruption", JsonSerializer.Serialize(Memory(restored.Character("neutral.actor")).Capture())
            == JsonSerializer.Serialize(memory) && restored.CheckSavePoint() is ContractResult<bool>.Success);
        world.EndChapterLifetimeAtFinale();
        check("neutral_world_finale_repeated_end_is_idempotent", world.Process(new AdvanceWorld(world.Simulation.CTB.Now)).Count == 0
            && JsonSerializer.Serialize(Memory(actor).Capture()) == JsonSerializer.Serialize(memory));
        Observe("neutral-world-finale-cancellation", new { started, ended, memory });
    }

    private static object Get(object target, string name) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);
    private static object? Invoke(object target, string method, params object?[] arguments) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);
    public static void EnteredRoomBoundary(Action<string, bool> check)
    {
        var session = new Chapter1Session();
        try
        {
            var identities = Names.PersonKeys;
            var rooms = Names.RoomKeys.Take(2).ToArray();
            var bodies = (IDictionary)Get(session.World, "_bodies");
            var souls = (IDictionary)Get(session.World, "_souls");
            var runtime = session.World.PersistentSession.Runtime;
            void PerceiveOwnRoom()
            {
                foreach (var i in Enumerable.Range(0, identities.Length))
                {
                    var body = (Essence)bodies[(Person)i]!;
                    var room = body.Get<SpatialProperty>()!.Space!;
                    runtime.Observe((SoulEssence)souls[(Person)i]!, runtime.Representations(room).Concat(runtime.Representations(body)), 0);
                }
            }
            void Report(string region, string entered, string roomName)
            {
                var actors = identities.ToDictionary(who => who, who => (object?)new Dictionary<string, object?>
                {
                    ["room"] = region, ["entered"] = entered, ["room_name"] = roomName,
                    ["visible"] = true, ["moving"] = true, ["distance"] = 0.0
                });
                Invoke(session, "ReadContext", new Dictionary<string, object?>
                {
                    ["context"] = new Dictionary<string, object?> { ["field_active"] = false, ["actors"] = actors }
                });
            }
            Report(rooms[0], rooms[0], "neutral-entered-a");
            PerceiveOwnRoom();
            var originalBodies = identities.Select((_, i) => ((Essence)bodies[(Person)i]!).Get<SpatialProperty>()!.Space).ToArray();
            var originalParents = identities.Select((_, i) => runtime.KnownProjection((SoulEssence)souls[(Person)i]!, (Essence)bodies[(Person)i]!)!.Parent).ToArray();
            Report(rooms[1], "", "neutral-region-b");
            PerceiveOwnRoom();
            var facts = (IDictionary)Get(session, "_facts");
            check("threshold_region_does_not_move_player_or_party_in_logical_world", identities.All(who => session.ContentSession.Session.CharacterRooms[who] == rooms[0]));
            check("threshold_region_does_not_move_player_or_party_physical_bodies", identities.Select((_, i) => i)
                .All(i => ReferenceEquals(((Essence)bodies[(Person)i]!).Get<SpatialProperty>()!.Space, originalBodies[i])));
            check("threshold_region_does_not_move_subjective_self_projections", identities.Select((_, i) => i)
                .All(i => ReferenceEquals(runtime.KnownProjection((SoulEssence)souls[(Person)i]!, (Essence)bodies[(Person)i]!)!.Parent, originalParents[i])));
            check("threshold_facts_keep_last_entered_room_and_name", identities.All(who => (string)Get(facts[who]!, "Room") == rooms[0]
                && (string)Get(facts[who]!, "RoomName") == "neutral-entered-a" && (string)Get(facts[who]!, "Entered") == ""));
            Report(rooms[1], rooms[1], "neutral-entered-b");
            PerceiveOwnRoom();
            check("actual_entry_moves_player_and_party_logical_rooms", identities.All(who => session.ContentSession.Session.CharacterRooms[who] == rooms[1]));
            check("actual_entry_moves_physical_bodies_and_subjective_self", identities.Select((_, i) => i).All(i =>
                !ReferenceEquals(((Essence)bodies[(Person)i]!).Get<SpatialProperty>()!.Space, originalBodies[i])
                && !ReferenceEquals(runtime.KnownProjection((SoulEssence)souls[(Person)i]!, (Essence)bodies[(Person)i]!)!.Parent, originalParents[i])));
            check("actual_entry_updates_room_facts", identities.All(who => (string)Get(facts[who]!, "Room") == rooms[1]
                && (string)Get(facts[who]!, "RoomName") == "neutral-entered-b" && (string)Get(facts[who]!, "Entered") == rooms[1]));
            Observe("entered-room-boundary", new
            {
                logicalRooms = identities.Select(who => session.ContentSession.Session.CharacterRooms[who]).ToArray(),
                factRooms = identities.Select(who => (string)Get(facts[who]!, "Room")).ToArray(),
                factRoomNames = identities.Select(who => (string)Get(facts[who]!, "RoomName")).ToArray(),
                subjectiveParentsChanged = identities.Select((_, i) => !ReferenceEquals(runtime.KnownProjection(
                    (SoulEssence)souls[(Person)i]!, (Essence)bodies[(Person)i]!)!.Parent, originalParents[i])).ToArray()
            });
        }
        finally { Invoke(session.World, "StopEcology"); }
    }
    private static Array Steps(string room, params (string Target, int Weight)[] steps)
    {
        var type = typeof(MorningWorld).GetNestedType("DailyStep", BindingFlags.NonPublic)!;
        var result = Array.CreateInstance(type, steps.Length);
        for (var i = 0; i < steps.Length; i++) result.SetValue(Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [room, steps[i].Target, "neutral-work", steps[i].Target, steps[i].Weight, 1.0, new HashSet<string>()], null), i);
        return result;
    }
    private static void BindNeutralRoutine(MorningWorld world, object person, string room)
    {
        var soul = (SoulEssence)Get(person, "Soul");
        var self = (Projection)Get(person, "Self");
        var steps = Steps(room, ("neutral-low", 1), ("neutral-high", 2));
        var ready = new[] { "neutral-low", "neutral-high" }.Select(name => Observation.Observe(soul,
            new ObservationInput(name, ProjectionKind.Concept) { Source = ObservationSource.Introspection, Existence = Truth.False })).ToArray();
        Effect[] effects = [new Effect.Set(self, new("neutral.daily.low"), 1), new Effect.Set(self, new("neutral.daily.high"), 1)];
        Observation.Observe(soul, new ObservationInput("neutral-daily-preferences", ProjectionKind.Concept)
        {
            Source = ObservationSource.Introspection, Existence = Truth.True,
            Rules = [new(new Condition.Exists(ready[0]), RuleMode.Prefer, effects[0], 1, Truth.True),
                new(new Condition.Exists(ready[1]), RuleMode.Prefer, effects[1], 2, Truth.True)]
        });
        Set(person, "Steps", steps); Set(person, "Ready", ready); Set(person, "Effects", effects);
        Set(person, "Index", 0); Set(person, "Remaining", 0.0); Set(person, "StartAfter", 0.0);
        Set(person, "AllowedStages", null); Set(person, "DepartureComplete", false);
    }
    public static void MorningCandidateSelection(Action<string, bool> check)
    {
        var world = new MorningWorld();
        try
        {
            var people = (IDictionary)Get(world, "_dailyPeople");
            var person = people.Values.Cast<object>().First(p => Get(p, "Hosted") is Person hosted && hosted != Person.Detective);
            var actor = (string)Get(person, "Actor");
            var spaces = (IDictionary)Get(world, "_dailySpaces");
            var body = (Essence)Get(person, "Body");
            var room = spaces.Keys.Cast<string>().Single(k => ReferenceEquals(spaces[k], body.Get<SpatialProperty>()!.Space));
            Set(world, "_dailyStage", ((HashSet<string>)Get(world, "_dailyStages")).First());
            BindNeutralRoutine(world, person, room);
            var busy = people.Values.Cast<object>().Where(p => !ReferenceEquals(p, person)).Select(p => (string)Get(p, "Actor")).ToArray();
            var first = world.DailyTick(0, true, busy).Single(p => p.Who == actor);
            check("neutral_morning_scores_candidates_instead_of_fixed_index", first.Target == "neutral-high" && (int)Get(person, "Index") == 1);
            world.DailyBlock(actor, first.Serial);
            var next = world.DailyTick(2, true, busy).Single(p => p.Who == actor);
            check("neutral_morning_no_progress_changes_candidate", next.Target == "neutral-low" && (int)Get(person, "Index") == 0);
            var soul = (SoulEssence)Get(person, "Soul");
            var self = (Projection)Get(person, "Self");
            var recorded = Memory(soul, self).Capture();
            check("neutral_morning_keeps_no_progress_history", recorded.Experiences.Any(e => e.Attempts == 1 && e.NoProgress == 1));
            Observe("neutral-morning-candidate-selection", new { first, next, recorded });
            var pendingPlanKey = (DecisionExperienceKeyV1)Get(person, "Experience");
            world.DailySuspend(actor);
            check("neutral_morning_pending_plan_suspension_records_interruption", Memory(soul, self).Get(pendingPlanKey)
                is { Attempts: 1, Interruptions: 1, Successes: 0 });
            // A departure is an ordered process even when a later step has greater preference.
            Set(person, "Departed", true); Set(person, "Index", 0); Set(person, "StartAfter", 0.0);
            var departure = world.DailyTick(0, true, busy).Single(p => p.Who == actor);
            check("neutral_morning_departure_preserves_order_gate", departure.Target == "neutral-low" && (int)Get(person, "Index") == 0);
        }
        finally { Invoke(world, "StopEcology"); }
    }

    public static void MorningStrategyInterruption(Action<string, bool> check)
    {
        var session = new Chapter1Session();
        try
        {
            var people = (IDictionary)Get(session.World, "_dailyPeople");
            var person = people.Values.Cast<object>().First(p => Get(p, "Hosted") is Person hosted && hosted != Person.Detective);
            var actor = (string)Get(person, "Actor");
            var soul = (SoulEssence)Get(person, "Soul");
            var self = (Projection)Get(person, "Self");
            var effect = new Effect.Set(self, new("strategy-method:neutral-interruption"), 1);
            Observation.Observe(soul, new ObservationInput("neutral-method-preference", ProjectionKind.Concept)
            {
                Source = ObservationSource.Introspection, Existence = Truth.True,
                Rules = [new(new Condition.Constant(Truth.True), RuleMode.Prefer, effect, 2, Truth.True)]
            });
            var choice = ActionLearning.Assess(soul.World, self).Single(a => Ast.Equivalent(a.Decision.Effect, effect));
            var key = ActionLearning.Key(soul.World, self, effect);
            var flow = ((IEnumerable)Invoke(session, "StrategyAction", actor, "neutral-interruption", "neutral-unused-scene", null, choice)!).GetEnumerator();
            try
            {
                check("neutral_strategy_starts_with_real_method_attempt", flow.MoveNext()
                    && Memory(soul, self).Get(key) is { Attempts: 1, Interruptions: 0, Successes: 0 });
            }
            finally { (flow as IDisposable)!.Dispose(); }
            check("neutral_strategy_disposed_flow_records_interruption", Memory(soul, self).Get(key) is { Attempts: 1, Interruptions: 1, Successes: 0, Failures: 0 });
            Observe("neutral-strategy-flow-interruption", ActionLearning.Capture(soul.World, self));
        }
        finally { Invoke(session.World, "StopEcology"); }
    }
}
