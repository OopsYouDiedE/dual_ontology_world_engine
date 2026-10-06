using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Awakening;

public sealed record DailyPlan(string Who, int Serial, string Room, string Target, string Action, string Note, double Seconds, double Speed);
public sealed record MorningTrajectory(double Time, string Person, string Room, string Action, string Text, int GameMinutes = 0);
public sealed record MorningDailyDecision(double Time, string Person, string Control, int Serial, string Room,
    string Action, int Score, IReadOnlyList<string> Reasons, int GameMinutes = 0, bool Resumed = false);

public sealed partial class MorningWorld
{
    private sealed record DailyStep(string Room, string Target, string Action, string Note, int Weight, double Seconds, HashSet<string> AllowedStages);
    private sealed class DailyPerson
    {
        public required string Key, Actor;
        public required SoulEssence Soul;
        public required Essence Body;
        public required Projection Self;
        public required double Speed;
        public Person? Hosted;
        public DailyStep[] Steps = [];
        public HashSet<string>? AllowedStages;
        public Projection[] Ready = [];
        public Effect[] Effects = [];
        public int Index, Serial;
        public double Remaining, StartAfter;
        public bool Arrived;
        public int? DepartureMinutes;
        public DailyStep[] DepartureSteps = [];
        public bool Departed;
        public bool DepartureComplete;
        public ActionExecution? Execution;
    }

    private readonly Dictionary<string, DailyPerson> _dailyPeople = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Essence> _dailySpaces = new(StringComparer.Ordinal);
    private readonly List<MorningTrajectory> _dailyTrajectory = [];
    private readonly List<MorningDailyDecision> _dailyDecisions = [];
    private readonly Dictionary<string, Queue<string>> _dailyWitnesses = new(StringComparer.Ordinal);
    private readonly List<(string Room, string Text)> _dailyRemoteWitnesses = [];
    private SimulationSession _dailySession = null!;
    private int _dailySerial;
    private bool _dailyClosed;
    private string _dailyStage = "";
    private readonly HashSet<string> _dailyStages = new(StringComparer.Ordinal);
    public IReadOnlyList<MorningTrajectory> DailyTrajectory => _dailyTrajectory.AsReadOnly();
    public IReadOnlyList<MorningDailyDecision> DailyDecisions => _dailyDecisions.AsReadOnly();
    public string DetectiveControl { get; private set; } = "player";
    public int GameMinutes { get; private set; }
    private int _dailySettledMinutes;
    private double _secondsPerGameMinute;

    /// <summary>Publish the shared narrative clock. DailyTick settles only its positive, unconsumed difference.</summary>
    public void SetMorningGameMinutes(int minutes)
    {
        if (minutes < GameMinutes) throw new ArgumentOutOfRangeException(nameof(minutes), "Morning clock cannot move backwards.");
        GameMinutes = minutes;
    }

    private void ApplyDailyDepartures(int minutes)
    {
        foreach (var person in _dailyPeople.Values.Where(p => p.Hosted is null && !p.Departed
            && p.DepartureMinutes is { } departure && minutes >= departure))
        {
            SuspendDaily(person);
            person.Departed = true;
            person.AllowedStages = null;
            InstallDailySteps(person, person.DepartureSteps);
        }
    }

    private static DailyStep[] ReadDailySteps(JsonElement routine) => routine.GetProperty("steps").EnumerateArray()
        .Select(s => new DailyStep(s.GetProperty("room").GetString()!, s.GetProperty("target").GetString()!,
            s.GetProperty("action").GetString()!, s.GetProperty("note").GetString()!, s.GetProperty("weight").GetInt32(),
            s.GetProperty("seconds").GetDouble(), s.TryGetProperty("allowed_stages", out var stages)
                ? stages.EnumerateArray().Select(stage => stage.GetString()!).ToHashSet(StringComparer.Ordinal) : []))
        .ToArray();

    private void InstallDailySteps(DailyPerson person, DailyStep[] steps)
    {
        person.Steps = steps;
        person.Index = 0;
        person.DepartureComplete = false;
        person.Remaining = 0;
        person.StartAfter = 0;
        person.Ready = steps.Select(s => Observation.Observe(person.Soul, new ObservationInput(s.Note, ProjectionKind.Concept)
            { Source = ObservationSource.Introspection, Existence = Truth.False })).ToArray();
        person.Effects = steps.Select((s, i) => (Effect)new Effect.Set(person.Self, new Tag("MorningStep:" + i), 1)).ToArray();
        Observation.Observe(person.Soul, new ObservationInput(person.Actor, ProjectionKind.Concept)
        { Source = ObservationSource.Introspection, Existence = Truth.True,
            Rules = [..steps.Select((s, i) => new Rule(new Condition.Exists(person.Ready[i]), RuleMode.Prefer, person.Effects[i], s.Weight, Truth.True))] });
    }

    private void CheckDailySteps(DailyPerson person, DailyStep[] steps, HashSet<string> blocked, bool departure)
    {
        foreach (var stage in steps.SelectMany(step => step.AllowedStages))
            if (!Chapter1Data.Root.GetProperty("STAGES").TryGetProperty(stage, out _))
                throw new InvalidOperationException("Unknown authored morning step stage: " + stage);
        foreach (var step in steps)
            if (!_dailySpaces.ContainsKey(step.Room) || person.Hosted is null
                && (blocked.Contains(step.Room) || !departure && _space.Values.Contains(_dailySpaces[step.Room]))
                || !double.IsFinite(step.Seconds) || step.Seconds <= 0 || step.Weight <= 0)
                throw new InvalidOperationException("Invalid or out-of-region morning step.");
    }

    public void SetDetectiveControl(string mode)
    {
        if (mode is not ("player" or "simulation")) throw new ArgumentException("Unknown detective control: " + mode, nameof(mode));
        if (DetectiveControl == mode) return;
        if (mode == "player")
            foreach (var person in _dailyPeople.Values.Where(p => p.Hosted == Person.Detective)) SuspendDaily(person);
        DetectiveControl = mode;
    }

    private bool DailyControlled(DailyPerson person) => person.Hosted != Person.Detective || DetectiveControl == "simulation";
    private bool DailyAllowed(DailyPerson person) => !person.DepartureComplete
        && (person.Hosted is null || _dailyStages.Contains(_dailyStage))
        && (person.AllowedStages is null || person.AllowedStages.Contains(_dailyStage))
        && (person.Steps.Length > 0 && (person.Steps[person.Index].AllowedStages.Count == 0
            || person.Steps[person.Index].AllowedStages.Contains(_dailyStage)));

    public void SetDailyStage(string stage)
    {
        if (!Chapter1Data.Root.GetProperty("STAGES").TryGetProperty(stage, out _))
            throw new ArgumentException("Unknown daily stage: " + stage, nameof(stage));
        _dailyStage = stage;
        foreach (var person in _dailyPeople.Values)
            if (!DailyAllowed(person)) SuspendDaily(person);
    }

    private void InitializeEcology()
    {
        // Narrative minutes and short animation/CTB seconds are separate units.
        // Companion wake time is authored background, never replayed as synthetic investigation.
        var clock = Chapter1Data.Root.GetProperty("DAY_CLOCK");
        GameMinutes = _dailySettledMinutes = clock.GetProperty("start_minutes").GetInt32();
        _secondsPerGameMinute = clock.GetProperty("daily_seconds_per_game_minute").GetDouble();
        if (!double.IsFinite(_secondsPerGameMinute) || _secondsPerGameMinute <= 0)
            throw new InvalidOperationException("Invalid daily seconds per game minute.");
        _dailySession = new SimulationSession(_runtime);
        _dailySession.Completed += DailyCompleted;
        foreach (var stage in Chapter1Data.Root.GetProperty("STAGES").EnumerateObject())
            if (stage.Value.TryGetProperty("daily", out var enabled) && enabled.GetBoolean()) _dailyStages.Add(stage.Name);
        _dailyStage = Chapter1Data.Root.GetProperty("STAGE_EVENTS").GetProperty("初始").GetString()!;
        using var stream = typeof(MorningWorld).Assembly.GetManifestResourceStream("Awakening.World.json")
            ?? throw new InvalidOperationException("Missing compiled world resource.");
        using var json = JsonDocument.Parse(stream);
        var chapter = json.RootElement.GetProperty("chapters")[0];
        var entities = chapter.GetProperty("entities").EnumerateArray().ToArray();
        var design = chapter.GetProperty("design");
        var morning = design.GetProperty("morning");
        var routines = morning.GetProperty("people");
        var blocked = morning.GetProperty("blocked_rooms").EnumerateArray().Select(p => p.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var (room, space) in _space) _dailySpaces[Names.RoomKeys[(int)room]] = space;
        foreach (var entity in entities.Where(e => e.GetProperty("kind").GetString() == "room"))
        {
            var key = entity.GetProperty("id").GetString()!["room.".Length..];
            if (_dailySpaces.ContainsKey(key)) continue;
            var space = new Essence(entity.GetProperty("name").GetString()!, ProjectionKind.Space);
            _dailySpaces[key] = space;
            _runtime.Project(space, space.TrueName);
        }
        foreach (var entity in entities.Where(e => e.GetProperty("kind").GetString() == "character"))
        {
            var key = entity.GetProperty("id").GetString()!["person.".Length..];
            var routine = routines.TryGetProperty(key, out var authored) ? authored : default;
            var actor = routine.ValueKind == JsonValueKind.Object ? routine.GetProperty("actor").GetString()! : key.ToLowerInvariant();
            var hostedIndex = Array.IndexOf(Names.PersonKeys, actor);
            // Both control modes retain the original detective body and soul.
            if (hostedIndex < 0 && entity.GetProperty("name").GetString() == Names.Of(Person.Detective)) hostedIndex = 0;
            var profile = entity.GetProperty("profile");
            SoulEssence soul;
            Essence body;
            if (hostedIndex >= 0) { soul = _souls[(Person)hostedIndex]; body = _bodies[(Person)hostedIndex]; }
            else
            {
                var name = entity.GetProperty("name").GetString()!;
                soul = new SoulEssence(name);
                body = new Essence(name, ProjectionKind.Body);
                var initial = profile.GetProperty("dorm").GetString()!;
                body.Add(new SpatialProperty { Space = _dailySpaces[initial] });
                soul.Relations.Add((Relations.Controls, body));
                _runtime.Project(soul, name);
                _runtime.Project(body, name);
            }
            var space = body.Get<SpatialProperty>()!.Space!;
            var self = _runtime.Observe(soul, _runtime.Representations(space).Concat(_runtime.Representations(body)), 0)
                .Single(p => p.Kind == ProjectionKind.Body);
            var person = new DailyPerson { Key = key, Actor = actor, Soul = soul, Body = body, Self = self,
                Hosted = hostedIndex >= 0 ? (Person)hostedIndex : null, Speed = profile.GetProperty("speed").GetDouble() };
            if (!double.IsFinite(person.Speed) || person.Speed <= 0) throw new InvalidOperationException("Invalid authored character speed.");
            _dailyPeople[key] = person;
            Observation.Observe(soul, new ObservationInput(body.TrueName, ProjectionKind.Concept)
            { Source = ObservationSource.Introspection, Existence = Truth.True, Details = profile.GetRawText() });
            if (morning.TryGetProperty("departures", out var departures) && departures.TryGetProperty(key, out var departure))
            {
                if (person.Hosted is not null || departure.GetProperty("actor").GetString() != actor)
                    throw new InvalidOperationException("Departure must refer to a non-hosted actor.");
                person.DepartureMinutes = departure.GetProperty("minutes").GetInt32();
                person.DepartureSteps = ReadDailySteps(departure);
                if (person.DepartureMinutes < 0 || person.DepartureSteps.Length == 0)
                    throw new InvalidOperationException("Departure requires nonnegative minutes and daily steps.");
                CheckDailySteps(person, person.DepartureSteps, blocked, departure: true);
            }
            if (routine.ValueKind != JsonValueKind.Object) continue;
            if (routine.TryGetProperty("allowed_stages", out var allowed) && allowed.GetArrayLength() > 0)
            {
                person.AllowedStages = allowed.EnumerateArray().Select(s => s.GetString()!).ToHashSet(StringComparer.Ordinal);
                foreach (var stage in person.AllowedStages)
                    if (!Chapter1Data.Root.GetProperty("STAGES").TryGetProperty(stage, out _))
                        throw new InvalidOperationException("Unknown authored morning stage: " + stage);
            }
            var steps = ReadDailySteps(routine);
            CheckDailySteps(person, steps, blocked, departure: false);
            InstallDailySteps(person, steps);
            if (person.Steps.Length > 0)
                person.StartAfter = person.Steps[0].Seconds / person.Speed * _dailyPeople.Count
                    / entities.Count(e => e.GetProperty("kind").GetString() == "character");
        }
        if (morning.TryGetProperty("witnesses", out var witnesses))
            foreach (var witness in witnesses.EnumerateArray())
                _dailyRemoteWitnesses.Add((witness.GetProperty("room").GetString()!, witness.GetProperty("text").GetString()!));
        ObserveDailyRooms();
    }

    public IReadOnlyList<DailyPlan> DailyTick(double delta, bool active, IEnumerable<string> busy)
    {
        if (_dailyClosed) return [];
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        var occupied = busy.ToHashSet(StringComparer.Ordinal);
        ClearUnattendedWitnesses();
        // Everyone shares each elapsed minute; twelve residents do not multiply elapsed time.
        // Minute-sized slices allow departure boundaries and scheduled work to retain their order.
        while (_dailySettledMinutes < GameMinutes)
        {
            ++_dailySettledMinutes;
            ApplyDailyDepartures(_dailySettledMinutes);
            PrepareDailyWork(_secondsPerGameMinute, active, occupied, includeResidents: true);
            _dailySession.CTB.RunUntil(_dailySession.CTB.Now + _secondsPerGameMinute);
        }
        PrepareDailyWork(delta, active, occupied, includeResidents: false);
        if (active && _dailyStages.Contains(_dailyStage))
        {
            // The host needs real seconds for arrived actors' short work animations.
            // Shift residents' deadlines by the same amount so UI/frame polling never runs their loops.
            foreach (var person in _dailyPeople.Values.Where(p => p.Hosted is null))
                if (person.Execution?.Scheduled is { Pending: true } execution)
                    _dailySession.CTB.Reschedule(execution, Math.Max(0, execution.Due - _dailySession.CTB.Now) + delta);
            _dailySession.CTB.RunUntil(_dailySession.CTB.Now + delta);
        }
        if (!active) return [];
        return _dailyPeople.Values.Where(p => DailyControlled(p) && DailyAllowed(p) && p.Hosted is not null
            && p.Serial != 0 && !occupied.Contains(p.Actor))
            .Select(p => { var step = p.Steps[p.Index]; return new DailyPlan(p.Actor, p.Serial, step.Room, step.Target,
                step.Action, step.Note, p.Remaining * p.Speed, p.Speed); }).ToArray();
    }

    private void PrepareDailyWork(double delta, bool active, HashSet<string> occupied, bool includeResidents)
    {
        foreach (var person in _dailyPeople.Values.Where(p => includeResidents || p.Hosted is not null))
        {
            if (!DailyControlled(person) || !DailyAllowed(person) || person.Hosted is not null && !active
                || occupied.Contains(person.Actor) || person.Hosted is { } hosted && StoryBusy(hosted))
            {
                SuspendDaily(person);
                continue;
            }
            if (person.Hosted is not null && _session.IsActing(person.Self)) continue;
            if (person.Execution is not null || person.Serial != 0) continue;
            if (person.StartAfter > 0)
            {
                person.StartAfter = Math.Max(0, person.StartAfter - delta);
                if (person.StartAfter > 0) continue;
            }
            SetDailyReady(person, true);
            var assessment = DecisionSystem.Assess(person.Soul.World, person.Self)
                .FirstOrDefault(a => !a.Forbidden && Ast.Equivalent(a.Decision.Effect, person.Effects[person.Index]));
            if (assessment is null) continue;
            person.Serial = ++_dailySerial;
            var step = person.Steps[person.Index];
            _dailyDecisions.Add(new(_dailySession.CTB.Now, person.Key, "simulation", person.Serial, step.Room,
                step.Action, assessment.Decision.Score, assessment.Reasons.ToImmutableArray(), _dailySettledMinutes, person.Remaining > 0));
            if (person.Remaining <= 0) person.Remaining = step.Seconds / person.Speed;
            if (person.Hosted is null)
            {
                var location = person.Body.Get<SpatialProperty>()!;
                var destination = _dailySpaces[step.Room];
                if (!ReferenceEquals(location.Space, destination))
                {
                    var source = _dailySpaces.First(p => ReferenceEquals(p.Value, location.Space)).Key;
                    location.Space = destination;
                    _dailyTrajectory.Add(new(_dailySession.CTB.Now, person.Key, step.Room, "move", source + " → " + step.Room, _dailySettledMinutes));
                }
                _runtime.Sync();
                ObserveDailyRooms();
                BeginDailyWork(person);
            }
            // Hosted actors only begin after DailyArrive confirms their real host position.
        }
    }

    private DailyPerson? DailyActor(string actor) => _dailyPeople.Values.FirstOrDefault(p => p.Actor == actor);
    public bool DailyArrive(string actor, int serial, string room)
    {
        var person = DailyActor(actor);
        if (_dailyClosed || person is null || !DailyControlled(person) || !DailyAllowed(person) || person.Serial != serial || serial == 0 || person.Steps[person.Index].Room != room) return false;
        if (!ReferenceEquals(person.Body.Get<SpatialProperty>()!.Space, _dailySpaces[room])) return false;
        if (person.Arrived) return true;
        ObserveDailyRooms();
        BeginDailyWork(person);
        return true;
    }
    private void BeginDailyWork(DailyPerson person)
    {
        person.Arrived = true;
        person.Execution = _dailySession.Submit(person.Soul, new ActionIntent(person.Self, person.Effects[person.Index], person.Remaining,
            new Condition.At(person.Self, _runtime.KnownProjection(person.Soul, _dailySpaces[person.Steps[person.Index].Room])!)));
    }
    public void DailySuspend(string actor) { if (DailyActor(actor) is { } person) SuspendDaily(person); }
    private void SuspendDaily(DailyPerson person)
    {
        if (person.Execution is { Status: ExecutionStatus.Pending } execution)
        {
            person.Remaining = Math.Max(0, (execution.Scheduled?.Due ?? _dailySession.CTB.Now) - _dailySession.CTB.Now);
            _dailySession.Cancel(execution);
        }
        person.Execution = null;
        person.Serial = 0;
        person.Arrived = false;
        if (person.Ready.Length > 0) SetDailyReady(person, false);
    }
    public void DailyBlock(string actor, int serial)
    {
        if (DailyActor(actor) is not { } person || serial == 0 || person.Serial != serial) return;
        SuspendDaily(person);
        person.StartAfter = person.Steps[person.Index].Seconds / person.Speed;
    }
    private void SetDailyReady(DailyPerson person, bool ready)
    {
        var concept = person.Ready[person.Index];
        var existence = ready ? Truth.True : Truth.False;
        if (concept.Existence == existence) return;
        Observation.Observe(person.Soul, new ObservationInput(concept.Name, concept.Kind)
            { Target = concept, Source = ObservationSource.Introspection, Existence = existence, Time = _dailySession.CTB.Now });
    }
    private void DailyCompleted(ActionExecution execution)
    {
        var person = _dailyPeople.Values.FirstOrDefault(p => ReferenceEquals(p.Execution, execution));
        if (person is null) return;
        var step = person.Steps[person.Index];
        if (execution.Status == ExecutionStatus.Completed)
        {
            var record = new MorningTrajectory(_dailySession.CTB.Now, person.Key, step.Room, step.Action, step.Note, _dailySettledMinutes);
            _dailyTrajectory.Add(record);
            ObserveDailyRooms();
            foreach (var witness in _dailyPeople.Values.Where(p => ReferenceEquals(p.Body.Get<SpatialProperty>()!.Space, person.Body.Get<SpatialProperty>()!.Space)))
                Observation.Observe(witness.Soul, new ObservationInput(step.Note, ProjectionKind.Event)
                { Source = ObservationSource.Perception, Existence = Truth.True, Time = record.Time, Details = step.Note,
                    AddRelations = [(Relations.Subject, _runtime.KnownProjection(witness.Soul, person.Body)!)] });
            EnqueueDailyWitness(step.Room, step.Note);
            if (person.Hosted is null)
                foreach (var witness in _dailyRemoteWitnesses) EnqueueDailyWitness(witness.Room, witness.Text);
            SetDailyReady(person, false);
            if (person.Departed && person.Index == person.Steps.Length - 1)
                person.DepartureComplete = true;
            else
                person.Index = (person.Index + 1) % person.Steps.Length;
        }
        else person.StartAfter = step.Seconds / person.Speed;
        person.Execution = null;
        person.Serial = 0;
        person.Arrived = false;
        person.Remaining = 0;
    }
    private void ObserveDailyRooms()
    {
        foreach (var person in _dailyPeople.Values)
        {
            var room = person.Body.Get<SpatialProperty>()!.Space!;
            var visible = _runtime.Representations(room).Concat(_dailyPeople.Values
                .Where(p => ReferenceEquals(p.Body.Get<SpatialProperty>()!.Space, room)).SelectMany(p => _runtime.Representations(p.Body)));
            _runtime.Observe(person.Soul, visible, _dailySession.CTB.Now);
        }
    }
    private void EnqueueDailyWitness(string room, string text)
    {
        if (!_dailySpaces.TryGetValue(room, out var space)
            || !ReferenceEquals(_bodies[Person.Detective].Get<SpatialProperty>()!.Space, space)) return;
        if (!_dailyWitnesses.TryGetValue(room, out var queue)) _dailyWitnesses[room] = queue = new();
        if (queue.Contains(text)) return;
        if (queue.Count >= 8) queue.Dequeue();
        queue.Enqueue(text);
    }
    private void ClearUnattendedWitnesses()
    {
        var here = _bodies[Person.Detective].Get<SpatialProperty>()!.Space;
        foreach (var room in _dailyWitnesses.Keys.ToArray())
            if (!_dailySpaces.TryGetValue(room, out var space) || !ReferenceEquals(here, space)) _dailyWitnesses.Remove(room);
    }
    public string DailyWitness(string room)
    {
        if (_dailyClosed) return "";
        if (!_dailySpaces.TryGetValue(room, out var space) || !ReferenceEquals(_bodies[Person.Detective].Get<SpatialProperty>()!.Space, space)) return "";
        if (!_dailyWitnesses.TryGetValue(room, out var queue) || queue.Count == 0) return "";
        var text = queue.Dequeue();
        Observation.Observe(_souls[Person.Detective], new ObservationInput(text, ProjectionKind.Event)
            { Source = ObservationSource.Perception, Existence = Truth.True, Time = _dailySession.CTB.Now, Details = text });
        return text;
    }
    private void StopEcology()
    {
        if (_dailyClosed) return;
        foreach (var person in _dailyPeople.Values) SuspendDaily(person);
        _dailyClosed = true;
        _dailySession.Completed -= DailyCompleted;
        _dailySession.Dispose();
    }
}
