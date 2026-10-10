using System.Text.Json;
using DualOntologyWorldEngine.Story;
namespace DualOntologyWorldEngine.Chapter2.Prototype;
public sealed record Chapter2AuthoredSite(string Id,string Map,string LocalId,Place Room,string Kind,string Title,string? Thing,string Lore);
public static class Chapter2AuthoredSites
{
    public static IReadOnlyList<Chapter2AuthoredSite> ReadAll(string json)
    {
        var chapter=WorldScriptDocument.Parse(json, allowChapterAdapter: true).Single();
        var entities=chapter.Entities.ToDictionary(Chapter2Bindings.Id,StringComparer.Ordinal);
        using var document=JsonDocument.Parse(json);
        var node=document.RootElement.GetProperty("chapters")[0];
        if(!node.TryGetProperty("sites",out var sites)) throw new InvalidDataException("Formal chapter is missing authored sites.");
        var result=new List<Chapter2AuthoredSite>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var site in sites.EnumerateArray())
        {
            string Read(string field,string fallback="")=>site.TryGetProperty(field,out var value)?value.GetString()??fallback:fallback;
            var id=Read("id");var map=Read("map");
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(map)||!ids.Add(map+":"+id))throw new InvalidDataException("Invalid or duplicate authored site: "+id);
            var roomId=Read("room");
            if(!roomId.StartsWith("world:room.",StringComparison.Ordinal)||!Enum.TryParse<Place>(roomId[11..],out var room)||!Enum.IsDefined(room)||room.ToString()!=roomId[11..])throw new InvalidDataException("Unknown site room: "+id);
            // Supported interaction capabilities; each nonempty kind has a session dispatch handler.
            var kind=Read("kind");if(kind is not ("" or "bed" or "watch" or "meal" or "tube" or "school_rules"))throw new InvalidDataException("Unsupported site kind: "+kind);
            var title=Read("title");if(string.IsNullOrWhiteSpace(title))throw new InvalidDataException("Missing site title: "+id);
            var thing=Read("thing");
            if(thing!=""&&(!entities.TryGetValue(thing,out var target)||target.Kind!="object"))throw new InvalidDataException("Site references an undeclared object: "+thing);
            if(thing!="")
            {
                var declaration=entities[thing];
                var anchored = declaration.Properties.TryGetValue("presentation_anchor", out var anchor) && anchor.Value.GetBoolean();
                if(!anchored &&
                   (!declaration.Properties.TryGetValue("space",out var space)||space.Value.GetString()!=roomId))
                    throw new InvalidDataException("Site room differs from its physical object: "+map+":"+id);
            }
            result.Add(new(map+":"+id,map,id,room,kind,title,thing==""?null:thing,Read("lore")));
        }
        return result;
    }
}
