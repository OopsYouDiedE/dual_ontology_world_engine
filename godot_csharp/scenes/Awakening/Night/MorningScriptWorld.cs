using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Awakening;

public sealed partial class MorningWorld
{
    private WorldSession? _persistentSession;
    public WorldSession PersistentSession => _persistentSession ??= CreatePersistentSession();
    private Dictionary<string, Essence> PersistentEntities()
    {
        var entities = new Dictionary<string, Essence>(StringComparer.Ordinal);
        foreach (var (room, space) in _dailySpaces) entities["world:room." + room] = space;
        foreach (var (id, evidence) in _evidence) entities["world:object." + id] = evidence;
        foreach (var daily in _dailyPeople.Values) entities["world:person." + daily.Key] = daily.Body;
        return entities;
    }
    private WorldSession CreatePersistentSession()
    {
        InitializePlayerPerception();
        var entities = PersistentEntities();
        var characters = _dailyPeople.Values.ToDictionary(p => "world:person." + p.Key,
            p => (p.Soul, _runtime.KnownProjection(p.Soul, p.Body)!), StringComparer.Ordinal);
        var definition = new ChapterWorldDefinition { Id = "awakening" };
        foreach (var (id, entity) in entities)
        {
            var declaration = new WorldEntityDefinition { Id = id, Scope = "world", Name = entity.TrueName,
                Kind = entity.Kind == ProjectionKind.Space ? "room" : characters.ContainsKey(id) ? "character" : "object" };
            if (declaration.Kind != "room")
            {
                var space = entity.Get<SpatialProperty>()?.Space
                    ?? throw new InvalidOperationException("A live object is missing its room: " + id);
                var roomId = entities.First(p => ReferenceEquals(p.Value, space)).Key;
                declaration.Properties["space"] = new WorldPropertyDefinition { Type = "entity",
                    Value = System.Text.Json.JsonSerializer.SerializeToElement(roomId) };
            }
            definition.Entities.Add(declaration);
        }
        var session = new WorldSession(_runtime, _session);
        session.BindExisting(definition, entities, characters, []);
        return session;
    }
    public void InitializePlayerPerception()
    {
        var soul=_souls[Person.Detective];var body=_bodies[Person.Detective];
        if(_runtime.KnownProjection(soul,body) is not null)return;
        var room=body.Get<SpatialProperty>()!.Space!;
        _runtime.Observe(soul,_runtime.Representations(room).Concat(_runtime.Representations(body)),_session.CTB.Now);
    }
    private LiveChapterWorld? _continuation;
    public LiveChapterWorld ExportContinuation(ChapterSession progress)
    {
        if (_continuation is not null) return _continuation;
        if (_dailyPeople.Values.Any(p => p.Hosted is { } person && StoryBusy(person) ||
            _runtime.KnownProjection(p.Soul, p.Body) is { } self && _session.IsActing(self)))
            throw new InvalidOperationException("Finish pending chapter actions before handing the live world to the next chapter.");
        StopEcology();
        RuleLifetimes.End(_dailyPeople.Values.Select(p => p.Soul), LifetimeScope.Chapter, "awakening", ++_time);
        var entities = PersistentEntities();
        foreach (var person in Names.People)
        {
            var key = person switch { Person.Detective => "Det", Person.Huo => "Huo", Person.Shen => "Shen", _ => "Su" };
            entities["world:person." + key] = _bodies[person];
        }
        var people = new Dictionary<string, ContinuingCharacter>();
        foreach (var daily in _dailyPeople.Values) entities["world:person." + daily.Key] = daily.Body;
        foreach (var daily in _dailyPeople.Values)
        {
            var key = daily.Key;
            var soul = daily.Soul;
            var body = daily.Body;
            entities["world:person." + key] = body;
            var known = entities.Values.Distinct().Select(e => (Entity: e, Mental: _runtime.KnownProjection(soul, e)))
                .Where(p => p.Mental is not null).ToDictionary(p => p.Entity, p => p.Mental!);
            people[key] = new(soul, body, _runtime.KnownProjection(soul, body), known,
                daily.Hosted is { } person ? Hooks(person) : 0,
                daily.Hosted is { } desirePerson ? DesireStrength(desirePerson) : 0,
                daily.Hosted is { } rulePerson && KnowsRule1(rulePerson));
        }
        if (_scriptWorld is not null) _session.Completed -= _scriptWorld.RefreshAfter;
        return _continuation = new(_runtime, _session, entities, people, progress, PersistentSession);
    }
    private CoreScriptWorld? _scriptWorld;
    public CoreScriptWorld? DefinedScriptWorld => _scriptWorld;
    public CoreScriptWorld ScriptWorld => _scriptWorld ??= CreateScriptWorld();
    private CoreScriptWorld CreateScriptWorld()
    {
        var world = new CoreScriptWorld(_runtime, _session);
        _session.Completed += world.RefreshAfter;
        foreach (var (id, space) in _space) world.Register("room:" + id, space);
        foreach (var (id, evidence) in _evidence) world.Register("object:" + id, evidence);
        foreach (var (id, soul) in _souls)
        {
            var self = _runtime.Observe(soul, _runtime.Representations(_bodies[id].Get<SpatialProperty>()!.Space!)
                .Concat(_runtime.Representations(_bodies[id])), _session.CTB.Now).Single(p => p.Kind == ProjectionKind.Body);
            world.RegisterCharacter("person:" + id, soul, _bodies[id], self,
                () => StoryBusy(id), execution =>
                {
                    DailySuspend(Names.PersonKeys[(int)id]);
                    _storyActions[id] = execution;
                });
        }
        return world;
    }
}
