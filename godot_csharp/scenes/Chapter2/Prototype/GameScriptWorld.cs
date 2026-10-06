using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    private CoreScriptWorld? _scriptWorld;
    public CoreScriptWorld? DefinedScriptWorld => _scriptWorld;
    public CoreScriptWorld ScriptWorld => _scriptWorld ??= CreateScriptWorld();
    private CoreScriptWorld CreateScriptWorld()
    {
        var world = new CoreScriptWorld(Rt, S);
        S.Completed += world.RefreshAfter;
        if (Continuation is not null)
            foreach (var (id, entity) in Continuation.Entities) world.Register(id, entity);
        foreach (var (id, space) in Space) world.Register("room:" + id, space);
        foreach (var thing in View.Keys.Where(e => e.Kind != DualOntologyWorldEngine.Core.ProjectionKind.Space &&
                     !People.Any(p => ReferenceEquals(p.Body, e))))
            world.Register("object:" + thing.TrueName, thing);
        foreach (var person in People)
            world.RegisterCharacter("person:" + person.Id, person.Soul, person.Body, person.Self,
                () => person.Exec is not null, execution => person.Exec = execution);
        return world;
    }
}
