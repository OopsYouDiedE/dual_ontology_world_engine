using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    private readonly Dictionary<string,bool> _corridors = new() { ["north"] = false, ["east"] = false, ["archive"] = false };
    private readonly HashSet<Place> _releasedDorms = [Place.DormLife, Place.DormW];
    private static readonly Tag Passable = new("Passable");
    public int CalendarMinutes { get; private set; }
    public static int RationFirstDay => FirstDay + 1;
    public bool HostDormReleased(Person p) => _releasedDorms.Contains(p.Pro.Dorm);
    public bool HostCanEnter(Place place) => place switch
    {
        Place.Hall or Place.Lecture => _corridors["north"],
        Place.Logistics or Place.Clinic or Place.Greenhouse => _corridors["east"],
        Place.ArchiveFront or Place.Resonance => _corridors["archive"],
        Place.Oath => _corridors["north"],
        Place.DormS or Place.DormL => _releasedDorms.Contains(place),
        _ => true
    };
    public void HostCorridor(string corridor, bool open)
    {
        if (!_corridors.ContainsKey(corridor)) throw new ArgumentException("Unknown corridor.");
        _corridors[corridor] = open; RefreshAccess();
        FreeEvents.Add(new FreeEvent.Corridor(SlotIndex, corridor, open));
    }
    public void HostDormOut(string name)
    {
        var dorm = new[] { Place.DormLife, Place.DormW, Place.DormS, Place.DormL }.Single(p => Names.Of(p) == name || p.ToString() == name);
        if (!_releasedDorms.Add(dorm)) return;
        foreach (var p in Npcs.Where(p => p.Pro.Dorm == dorm)) p.Body.Get<SpatialProperty>()!.Space = Space[Place.Living];
        RefreshAccess(); SyncChapterRooms(); PerceiveAll();
        FreeEvents.Add(new FreeEvent.DormOut(SlotIndex, name));
    }
    private void RefreshAccess()
    {
        foreach (var (place, way) in Way)
        {
            var property = way.Get<StateProperty>() ?? way.Add(new StateProperty());
            property.Observable.Add(Passable); property.Values[Passable] = HostCanEnter(place) ? 1 : 0;
            var connections = way.Get<PassageProperty>()!.Connections;
            connections.Clear();
            foreach (var (from, space) in Space)
                if (HostCanEnter(place) && HostCanEnter(from)) connections[space] = Space[place];
        }
        Rt.Sync();
        foreach (var p in People.Where(p => p.M.Count > 0))
        {
            var routes = Rt.Observe(p.Soul, Way.Values.Select(w => View[w]), Now);
            var i = 0; foreach (var way in Way.Values) p.M[way] = routes[i++];
        }
    }
    private void InitializeAccess()
    {
        CalendarMinutes = Math.Max(SlotBoundary(FirstDay, Slot.Morning, false), Convert.ToInt32(Chapter.GetProgress("clock").GetValueOrDefault("game_minutes", 0)));
        RefreshAccess();
        foreach (var p in People)
        {
            var rules = Way.Values.Select(way => new Rule(new Condition.Not(new Condition.Tagged(p.M[way], Passable, 1)),
                RuleMode.Forbid, new Effect.Act(ActionOp.Travel, p.M[way]), 100, Truth.True)).ToImmutableArray();
            Observation.Observe(p.Soul, new ObservationInput("access", ProjectionKind.Concept)
                { Source = ObservationSource.Perception, Existence = Truth.True, Rules = rules });
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
