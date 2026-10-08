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
    private bool _continuationExported;
    public LiveChapterWorld ExportContinuation(ChapterSession progress)
    {
        if (_continuationExported) throw new InvalidOperationException("This live chapter world has already been handed off.");
        if (Enum.GetValues<Person>().Any(p => StoryBusy(p) ||
            _runtime.KnownProjection(_souls[p],_bodies[p]) is { } self && _session.IsActing(self)))
            throw new InvalidOperationException("Finish pending chapter actions before handing the live world to the next chapter.");
        StopEcology();
        // 离开生活翼：拿不拿那件旧物的打算系在本章的物件与场所上，到此失效（本人内省得知，不删记忆、不改钩与欲望）。
        // 否则其中“守着手头的事”等常数偏好会带进下一章，压过那里的一切日常。
        foreach (var who in _minds.Keys)
        {
            var habit = _minds[who].Habits;
            if (habit is not null)
                Observation.Observe(_souls[who], new ObservationInput(habit.Name, habit.Kind) { Target = habit, Source = ObservationSource.Introspection, Time = ++_time, Rules = [] });
        }
        var entities = PersistentEntities();
        foreach (var person in Enum.GetValues<Person>())
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
        _continuationExported = true;
        return new(_runtime, _session, entities, people, progress, PersistentSession);
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
