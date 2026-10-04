using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Awakening;

public sealed partial class MorningWorld
{
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
