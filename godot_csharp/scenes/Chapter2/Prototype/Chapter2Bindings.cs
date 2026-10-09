using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>Stable author IDs resolved once into actual game references; display names are never identifiers.</summary>
public static class Chapter2Bindings
{
    // STORY-HARDCODE[CH2-084][schema][production]: 将剧本里的具体世界物件身份绑定到本章运行对象。
    private static readonly IReadOnlyDictionary<string,Func<Game,Essence>> Objects = new Dictionary<string,Func<Game,Essence>>
    {
        ["Granary"]=g=>g.Granary,["Desk"]=g=>g.Desk,["Stove"]=g=>g.Stove,["Jar"]=g=>g.Jar,["Ledger"]=g=>g.Ledger,
        ["Bowl"]=g=>g.Bowl,["Bed"]=g=>g.Bed,["MedCab"]=g=>g.MedCab,["OilStore"]=g=>g.OilStore,["Valve"]=g=>g.Valve,
        ["Gauge"]=g=>g.Gauge,["Blackboard"]=g=>g.Blackboard,["Stele"]=g=>g.Stele,["ArchiveDoor"]=g=>g.ArchiveDoor,
        ["WeightRoom"]=g=>g.WeightRoom,["LockCore"]=g=>g.LockCore,["RelayPost"]=g=>g.RelayPost,["Base"]=g=>g.Base,
        ["SketchBook"]=g=>g.SketchBook,["Clock"]=g=>g.Clock,["NameVoice"]=g=>g.NameVoice
    };
    private static string Local(WorldEntityDefinition entity)
    {
        if(entity.Scope!="world")throw new InvalidDataException("Formal existing-world bindings require world scope.");
        return entity.Id.StartsWith("world:",StringComparison.Ordinal)?entity.Id[6..]:entity.Id;
    }
    public static string Id(WorldEntityDefinition entity)=>"world:"+Local(entity);
    public static P PersonId(WorldEntityDefinition entity)
    {
        var local=Local(entity);
        if(entity.Kind!="character"||!local.StartsWith("person.",StringComparison.Ordinal)||!Enum.TryParse<P>(local[7..],out var who)||!Enum.IsDefined(who))throw new InvalidDataException("Unknown character binding: "+entity.Id);
        return who;
    }
    public static void Validate(ChapterWorldDefinition chapter)
    {
        WorldSession.ValidateChapter(chapter);
        foreach(var entity in chapter.Entities)
        {
            var local=Local(entity);
            if(entity.Kind=="character") { _=PersonId(entity);continue; }
            if(entity.Kind=="room"&&local.StartsWith("room.",StringComparison.Ordinal)&&Enum.TryParse<Place>(local[5..],out var place)&&Enum.IsDefined(place))continue;
            if(entity.Kind=="object"&&local.StartsWith("object.",StringComparison.Ordinal)&&Objects.ContainsKey(local[7..]))continue;
            // STORY-HARDCODE[CH2-096][schema][production]: 限定本章公告板实体可绑定的具体地点集合。
            if(entity.Kind=="object"&&local.StartsWith("Board.",StringComparison.Ordinal)&&Enum.TryParse<Place>(local[6..],out var board)&&board is Place.Hall or Place.Logistics or Place.Lecture or Place.DormLife or Place.DormW or Place.DormS or Place.DormL or Place.Living)continue;
            // STORY-HARDCODE[CH2-097][schema][production]: 限定本章废纸篓实体只存在于四个宿舍区。
            if(entity.Kind=="object"&&local.StartsWith("Litter.",StringComparison.Ordinal)&&Enum.TryParse<Place>(local[7..],out var litter)&&litter is Place.DormLife or Place.DormW or Place.DormS or Place.DormL)continue;
            throw new InvalidDataException("Unknown formal binding: "+entity.Id);
        }
    }
    /// <summary>按剧本里的物件编号（object.X / Board.地点 / Litter.地点）取游戏里的实体。</summary>
    public static Essence ResolveId(Game game,string id)=>
        id.StartsWith("Board.",StringComparison.Ordinal)?game.Board[Enum.Parse<Place>(id[6..])]:
        id.StartsWith("Litter.",StringComparison.Ordinal)?game.Litter[Enum.Parse<Place>(id[7..])]:Objects[id[7..]](game);
    public static Essence Resolve(Game game,WorldEntityDefinition entity)
    {
        var local=Local(entity);
        return entity.Kind=="character"?game[PersonId(entity)].Body:
            entity.Kind=="room"?game.Space[Enum.Parse<Place>(local[5..])]:
            local.StartsWith("Board.",StringComparison.Ordinal)?game.Board[Enum.Parse<Place>(local[6..])]:
            local.StartsWith("Litter.",StringComparison.Ordinal)?game.Litter[Enum.Parse<Place>(local[7..])]:Objects[local[7..]](game);
    }
}
