using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Awakening;

public sealed partial class MorningWorld
{
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
        var entities = new Dictionary<string, Essence>();
        foreach (var (room, space) in _space) entities["chapter1:room:" + room] = space;
        foreach (var (id, evidence) in _evidence) entities["chapter1:object:" + id] = evidence;
        foreach (var person in Enum.GetValues<Person>())
        {
            var key = person switch { Person.Detective => "Det", Person.Huo => "Huo", Person.Shen => "Shen", _ => "Su" };
            entities["world:person:" + key] = _bodies[person];
        }
        var people = new Dictionary<string, ContinuingCharacter>();
        foreach (var person in Enum.GetValues<Person>())
        {
            var key = person switch { Person.Detective => "Det", Person.Huo => "Huo", Person.Shen => "Shen", _ => "Su" };
            var soul = _souls[person];
            var body = _bodies[person];
            entities["world:person:" + key] = body;
            var known = entities.Values.Distinct().Select(e => (Entity: e, Mental: _runtime.KnownProjection(soul, e)))
                .Where(p => p.Mental is not null).ToDictionary(p => p.Entity, p => p.Mental!);
            people[key] = new(soul, body, _runtime.KnownProjection(soul, body), known,
                Hooks(person), DesireStrength(person), KnowsRule1(person));
        }
        if (_scriptWorld is not null) _session.Completed -= _scriptWorld.RefreshAfter;
        _continuationExported = true;
        return new(_runtime, _session, entities, people, progress);
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
                () => StoryBusy(id), execution => _storyActions[id] = execution);
        }
        return world;
    }
}
