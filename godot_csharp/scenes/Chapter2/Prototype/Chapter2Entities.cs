using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    private readonly Dictionary<string, Essence> _entities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldEntityDefinition> _declarations;
    public IReadOnlyDictionary<string, Essence> Entities => _entities;
    public IEnumerable<Essence> Rooms => _declarations.Where(p => p.Value.Kind == "room").Select(p => _entities[p.Key]);
    public IEnumerable<Essence> Objects => _declarations.Where(p => p.Value.Kind == "object").Select(p => _entities[p.Key]);
    public IEnumerable<string> Groups => _authored!.Design.GetProperty("vocab").GetProperty("groups")
        .EnumerateArray().Select(g => g.GetProperty("id").GetString()!);
    public IEnumerable<Essence> Dorms => _boundaries.Values.Where(b => b.Kind == "dorm")
        .SelectMany(b => b.Rooms).Distinct().Select(p => Space[p]);
    public IEnumerable<Essence> Passages => Way.Values;
    private static string EntityKey(string id) => id.StartsWith("world:", StringComparison.Ordinal) ? id : "world:" + id;
    public Essence ResolveEntity(string id) => _entities.TryGetValue(EntityKey(id), out var entity) ? entity
        : throw new InvalidDataException("Unknown entity ID: " + id);
    internal string EntityKind(string id) => _declarations.TryGetValue(EntityKey(id), out var declaration) ? declaration.Kind
        : throw new InvalidDataException("Unknown entity ID: " + id);
    private void RegisterEntity(string id, Essence entity)
    {
        var key = EntityKey(id);
        if (!_declarations.TryGetValue(key, out var declaration)) throw new InvalidDataException("Missing entity declaration: " + key);
        var expected = declaration.Kind switch
        { "room" => ProjectionKind.Space, "character" => ProjectionKind.Body, "object" => ProjectionKind.Object,
          _ => throw new InvalidDataException("Unsupported entity kind: " + declaration.Kind) };
        if (entity.Kind != expected) throw new InvalidDataException("Entity kind conflict: " + key);
        if (!_entities.TryAdd(key, entity)) throw new InvalidDataException("Duplicate entity binding: " + key);
    }
    private void BuildDeclaredRooms()
    {
        foreach (var (id, declaration) in _declarations.Where(p => p.Value.Kind == "room"))
        {
            var room = Existing(id) ?? new Essence(declaration.Name, ProjectionKind.Space);
            if (room.Get<StateProperty>() is null) room.Add(new StateProperty());
            RegisterEntity(id, room);
            View[room] = Rt.Representations(room).FirstOrDefault() ?? Rt.Project(room, declaration.Name);
            if (id.StartsWith("world:room.", StringComparison.Ordinal)
                && Enum.TryParse<Place>(id[11..], out var legacy) && Enum.IsDefined(legacy) && legacy.ToString() == id[11..])
            { Space.Add(legacy, room); PlaceOf.Add(room, legacy); }
        }
    }
    private void BuildRemainingObjects()
    {
        foreach (var (id, declaration) in _declarations.Where(p => p.Value.Kind == "object" && !_entities.ContainsKey(p.Key)))
        {
            var entity = Existing(id) ?? new Essence(declaration.Name);
            if (entity.Get<StateProperty>() is null) entity.Add(new StateProperty());
            if (declaration.Properties.TryGetValue("space", out var spatial))
            {
                var room = ResolveEntity(spatial.Value.GetString()!);
                if (room.Kind != ProjectionKind.Space) throw new InvalidDataException("Spatial parent must be a room: " + id);
                if (entity.Get<SpatialProperty>() is null) entity.Add(new SpatialProperty { Space = room });
                if (PlaceOf.TryGetValue(room, out var legacy)) PlaceOf[entity] = legacy;
            }
            RegisterEntity(id, entity);
            View[entity] = Rt.Representations(entity).FirstOrDefault() ?? Rt.Project(entity, declaration.Name);
            Things.Add(entity);
        }
    }
    private void BuildObjectSet(string prefix, Dictionary<Place, Essence> target, Tag[] visible, bool rules = false)
    {
        foreach (var (id, declaration) in _declarations.Where(p => p.Value.Kind == "object" && p.Key.StartsWith("world:" + prefix, StringComparison.Ordinal)))
        {
            var room = ResolveEntity(declaration.Properties["space"].Value.GetString()!);
            if (!PlaceOf.TryGetValue(room, out var place)) continue; // Generic objects still bind through the registry.
            var entity = NewThing(place, visible, id: id[6..]);
            if (rules && entity.Get<RuleProperty>() is null) entity.Add(new RuleProperty());
            target.Add(place, entity);
        }
    }
    private Essence BuildStandaloneObject(string id)
    {
        var key = EntityKey(id);
        var declaration = _declarations[key];
        var entity = Existing(key) ?? new Essence(declaration.Name);
        RegisterEntity(key, entity);
        View[entity] = Rt.Representations(entity).FirstOrDefault() ?? Rt.Project(entity, declaration.Name);
        return entity;
    }
}
