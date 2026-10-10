using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>Stable author IDs resolved once into actual game references; display names are never identifiers.</summary>
public static class Chapter2Bindings
{
    private static string Local(WorldEntityDefinition entity)
    {
        if(entity.Scope!="world")throw new InvalidDataException("Formal existing-world bindings require world scope.");
        return entity.Id.StartsWith("world:",StringComparison.Ordinal)?entity.Id[6..]:entity.Id;
    }
    public static string Id(WorldEntityDefinition entity)=>"world:"+Local(entity);
    public static P PersonId(WorldEntityDefinition entity)
    {
        var local=Local(entity);
        if(entity.Kind!="character"||!local.StartsWith("person.",StringComparison.Ordinal)||!Enum.TryParse<P>(local[7..],out var who)||!Enum.IsDefined(who)||who.ToString()!=local[7..])throw new InvalidDataException("Unknown character binding: "+entity.Id);
        return who;
    }
    public static void Validate(ChapterWorldDefinition chapter)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in chapter.Entities)
            if (!ids.Add(Id(entity))) throw new InvalidDataException("Duplicate entity declaration: " + entity.Id);
        WorldSession.ValidateChapter(chapter);
    }
    public static Essence ResolveId(Game game,string id) => game.ResolveEntity(id);
    public static Essence Resolve(Game game,WorldEntityDefinition entity)
    {
        var id = Id(entity);
        if (game.EntityKind(id) != entity.Kind) throw new InvalidDataException("Entity kind conflict: " + id);
        return game.ResolveEntity(id);
    }
}
