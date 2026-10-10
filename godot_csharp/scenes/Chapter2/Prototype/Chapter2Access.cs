using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    private sealed record AccessBoundary(string Kind, Essence State, HashSet<Place> Rooms);
    private sealed record AccessRoute(string Id, string Name, Place To, HashSet<Place> From, HashSet<Place> VisibleFrom);
    private readonly Dictionary<string, AccessBoundary> _boundaries = new(StringComparer.Ordinal);
    private readonly Dictionary<Place, AccessRoute> _routes = [];
    private JsonElement _access;
    private static readonly Tag Passable = new("Passable");
    private readonly SoulEssence _accessSoul = new("access-controller");
    private Projection _accessSelf = null!;
    private readonly Dictionary<Essence, Projection> _accessMental = [];
    public int CalendarMinutes { get; private set; }
    public static int RationFirstDay => FirstDay + 1;
    private static Place AccessPlace(string id) => id.StartsWith("world:room.", StringComparison.Ordinal)
        && Enum.TryParse<Place>(id[11..], out var place) && Enum.IsDefined(place) && place.ToString() == id[11..] ? place
        : throw new InvalidDataException("Unknown access room: " + id);
    private static P AccessPerson(string id) => id.StartsWith("world:person.", StringComparison.Ordinal)
        && Enum.TryParse<P>(id[13..], out var person) && Enum.IsDefined(person) && person.ToString() == id[13..] ? person
        : throw new InvalidDataException("Unknown access character: " + id);
    private static HashSet<Place> AccessPlaces(JsonElement value)
    {
        var items = value.EnumerateArray().Select(v => AccessPlace(v.GetString()!)).ToArray();
        if (items.Length == 0 || items.Distinct().Count() != items.Length) throw new InvalidDataException("Empty or duplicate access rooms.");
        return items.ToHashSet();
    }
    private void LoadAccess()
    {
        _access = (_authored is { Design.ValueKind: JsonValueKind.Object } && _authored.Design.TryGetProperty("access", out var data)
            ? data : Chapter2Design.Section("access")).Clone();
        foreach (var entry in _access.GetProperty("boundaries").EnumerateObject())
        {
            var kind = entry.Value.GetProperty("kind").GetString()!;
            if (kind is not ("corridor" or "dorm")) throw new InvalidDataException("Unknown access boundary kind.");
            var state = new Essence(new EntityId("world:boundary." + entry.Name), entry.Name, ProjectionKind.Boundary);
            var property = state.Add(new StateProperty());
            property.Observable.Add(Passable);
            property.Values[Passable] = entry.Value.GetProperty("initial").GetBoolean() ? 1 : 0;
            _boundaries.Add(entry.Name, new(kind, state, AccessPlaces(entry.Value.GetProperty("rooms"))));
        }
        foreach (var entry in _access.GetProperty("routes").EnumerateObject())
        {
            var to = AccessPlace(entry.Value.GetProperty("to").GetString()!);
            _routes.Add(to, new(entry.Name, entry.Value.GetProperty("name").GetString()!, to, AccessPlaces(entry.Value.GetProperty("from")), AccessPlaces(entry.Value.GetProperty("visible_from"))));
        }
        var routedRooms = _declarations.Keys.Where(id => id.StartsWith("world:room.", StringComparison.Ordinal))
            .Select(id => Enum.TryParse<Place>(id[11..], out var place) && Enum.IsDefined(place) && place.ToString() == id[11..] ? (Place?)place : null)
            .Where(p => p.HasValue).Select(p => p!.Value);
        if (!_routes.Keys.ToHashSet().SetEquals(routedRooms)) throw new InvalidDataException("Missing access route.");
        foreach (var entry in _access.GetProperty("arrivals").EnumerateObject())
        { _ = AccessPerson("world:" + entry.Name); _ = AccessPlace(entry.Value.GetString()!); }
        if (_access.GetProperty("arrivals").EnumerateObject().Count() != People.Length) throw new InvalidDataException("Missing initial character location.");
    }
    private bool BoundaryOpen(AccessBoundary boundary) => Get(boundary.State, Passable) > 0;
    public bool HostDormReleased(Person p) => _boundaries.Values.Any(b => b.Kind == "dorm" && b.Rooms.Contains(p.Pro.Dorm))
        && _boundaries.Values.Where(b => b.Kind == "dorm" && b.Rooms.Contains(p.Pro.Dorm)).All(BoundaryOpen);
    public bool HostCanEnter(Place place) => _boundaries.Values.Where(b => b.Rooms.Contains(place)).All(BoundaryOpen);
    private Place AccessArrival(Person p) => AccessPlace(_access.GetProperty("arrivals").GetProperty("person." + p.Id).GetString()!);
    private void BuildAccess()
    {
        foreach (var route in _routes.Values)
        {
            var way = new Essence(new EntityId("world:" + route.Id), route.Name, ProjectionKind.Boundary);
            way.Add(new PassageProperty());
            way.Add(new StateProperty()).Observable.Add(Passable);
            Way[route.To] = way;
            View[way] = Rt.Project(way, route.Name);
        }
        var body = new Essence("access-controller", ProjectionKind.Body);
        body.Add(new SpatialProperty { Space = Space.Values.First() });
        _accessSoul.Relations.Add((Relations.Controls, body));
        Rt.Project(_accessSoul, _accessSoul.TrueName);
        var self = Rt.Project(body, body.TrueName);
        _accessSelf = Rt.Observe(_accessSoul, [self], Now).Single();
        foreach (var boundary in _boundaries.Values)
        {
            var view = Rt.Project(boundary.State, boundary.State.TrueName);
            _accessMental[boundary.State] = Rt.Observe(_accessSoul, [view], Now).Single();
        }
        RefreshAccess(observe: false);
    }
    private bool CommitAccess(IEnumerable<(AccessBoundary Boundary, bool Open)> changes)
    {
        var pending = changes.Where(c => BoundaryOpen(c.Boundary) != c.Open).ToArray();
        if (pending.Length == 0) return false;
        var effect = new Effect.Sequence([..pending.Select(c => (Effect)new Effect.Set(_accessMental[c.Boundary.State], Passable, c.Open ? 1 : 0))]);
        var execution = S.Submit(_accessSoul, new(_accessSelf, effect, 0));
        S.CTB.RunUntil(Now, 100_000);
        if (execution.Status != ExecutionStatus.Completed) throw new InvalidOperationException("Access update did not commit: " + execution.Result);
        RefreshAccess();
        return true;
    }
    public void HostCorridor(string corridor, bool open)
    {
        if (!_boundaries.TryGetValue(corridor, out var boundary) || boundary.Kind != "corridor") throw new ArgumentException("Unknown corridor.");
        if (CommitAccess([(boundary, open)])) FreeEvents.Add(new FreeEvent.Corridor(SlotIndex, corridor, open));
    }
    public void HostDormOut(string id)
    {
        // Older recorded inputs may contain display names. Resolve them against declared boundaries only.
        var key = _boundaries.ContainsKey(id) ? id : _boundaries.Single(b => b.Value.Kind == "dorm"
            && b.Value.Rooms.Any(r => Names.Of(r) == id || r.ToString() == id)).Key;
        var boundary = _boundaries[key];
        if (boundary.Kind != "dorm") throw new ArgumentException("Unknown dorm boundary.");
        if (CommitAccess([(boundary, true)])) FreeEvents.Add(new FreeEvent.DormOut(SlotIndex, key));
    }
    private void RefreshAccess(bool observe = true)
    {
        foreach (var route in _routes.Values)
        {
            var way = Way[route.To];
            way.Get<StateProperty>()!.Values[Passable] = HostCanEnter(route.To) ? 1 : 0;
            var connections = way.Get<PassageProperty>()!.Connections;
            connections.Clear();
            foreach (var from in route.From.Where(from => HostCanEnter(from) && HostCanEnter(route.To)))
                connections[Space[from]] = Space[route.To];
        }
        Rt.Sync();
        if (observe) foreach (var p in People.Where(p => p.M.Count > 0 && !p.Asleep)) ObserveLocalAccess(p);
    }
    private void ObserveLocalAccess(Person p) => ObserveAccess(p, _routes.Values.Where(r => r.VisibleFrom.Contains(Where(p))
        && Options.Hide?.Invoke(p, View[Way[r.To]]) != true).Select(r => r.To));
    /// <summary>Verified local inspection; merely remembering a route grants no distant perception.</summary>
    public void HostReviewAccess(Person p, Place to)
    {
        if (!_routes[to].VisibleFrom.Contains(Where(p)) || !p.M.ContainsKey(Way[to])) throw new InvalidOperationException("The known route is not visible here.");
        ObserveAccess(p, [to]);
    }
    private void ObserveAccess(Person p, IEnumerable<Place> destinations, ObservationSource source = ObservationSource.Perception)
    {
        var from = Space[Where(p)];
        foreach (var to in destinations.Distinct())
        {
            var way = Way[to];
            var passable = way.Get<PassageProperty>()!.Connections.ContainsKey(from) ? 1 : 0;
            if (!p.M.TryGetValue(way, out var known)) p.M[way] = known = Rt.Observe(p.Soul, [View[way]], Now).Single();
            if (known.Tags.GetValueOrDefault(Passable, -1) == passable) continue;
            Observation.Observe(p.Soul, new ObservationInput(known.Name, known.Kind)
            { Target = known, Source = source, Time = Now, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Passable, passable) });
        }
    }
    private void ObserveFailedAccess(Person p, ActionExecution execution)
    {
        if (execution.Result?.Failure is not (ActionFailure.OutOfReach or ActionFailure.ConditionFailed)) return;
        if (execution.Result.FailedEffect is not Effect.Act { Operation: ActionOp.Travel, Target: Noun.Reference target }) return;
        ObserveAccess(p, Way.Where(w => p.M.TryGetValue(w.Value, out var known) && ReferenceEquals(target.Projection, known)).Select(w => w.Key));
    }
    private void InitializeAccess()
    {
        var clock = Chapter.GetProgress("clock");
        CalendarMinutes = Math.Max(SlotBoundary(FirstDay, Slot.Morning, false),
            Convert.ToInt32(clock.GetValueOrDefault("door_open_minutes", clock.GetValueOrDefault("game_minutes", 0))));
        RefreshAccess();
        foreach (var p in People)
        {
            var rules = Way.Values.Select(way => new Rule(new Condition.Not(new Condition.Tagged(p.M[way], Passable, 1)),
                RuleMode.Forbid, new Effect.Act(ActionOp.Travel, p.M[way]), 100, Truth.True));
            Observation.Observe(p.Soul, new ObservationInput("access", ProjectionKind.Concept)
                { Source = ObservationSource.Perception, Existence = Truth.True,
                  Rules = DualOntologyWorldEngine.Story.RuleLifetimes.Bind(rules,
                      Chapter2Design.Section("rule_lifetimes").GetProperty("access").GetString()!, Chapter2Script.Id) });
        }
    }
    private static int SlotBoundary(int day, Slot slot, bool end)
    {
        var key = day == FirstDay ? "first_day_slot_minutes" : "slot_minutes";
        var row = Chapter2Design.Section("time").GetProperty(key).GetProperty(slot.ToString());
        return (day - FirstDay) * 1440 + row[end ? 1 : 0].GetInt32();
    }
    private void AdvanceCalendar() => CalendarMinutes = Math.Max(CalendarMinutes, SlotBoundary(Day, Slot, true));
}
