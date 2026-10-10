using System.Text.Json;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>Author identity to portrait, map and presentation anchor bindings. No world state is inferred here.</summary>
public sealed class Chapter2Presentation
{
    public Dictionary<string, string> Sprites { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> MapPlaces { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> PlaceMaps { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Cast { get; } = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _mapSpawns = new(StringComparer.Ordinal);
    public string PlayerKey { get; }
    public string PlayerIdentity { get; }
    public string GatheringAnchor { get; }
    public string GatheringMap { get; }
    public string GatheringSpawn { get; }
    public string InitialMap { get; }

    public Chapter2Presentation(string json)
    {
        var chapter = WorldScriptDocument.Parse(json, allowChapterAdapter: true).Single();
        var entities = chapter.Entities.ToDictionary(Chapter2Bindings.Id, StringComparer.Ordinal);
        using var document = JsonDocument.Parse(json);
        var source = document.RootElement.GetProperty("chapters")[0];
        var data = source.GetProperty("design").GetProperty("presentation");
        PlayerKey = Required(data, "player_actor");
        PlayerIdentity = Required(data, "player_identity");
        GatheringAnchor = Required(data, "gathering_anchor");
        GatheringSpawn = Required(data, "gathering_spawn");
        InitialMap = Required(data, "initial_map");
        foreach (var actor in chapter.Entities.Where(e => e.Kind == "character"))
        {
            if (!actor.Properties.TryGetValue("sprite", out var sprite) || string.IsNullOrWhiteSpace(sprite.Value.GetString()))
                throw new InvalidDataException("Missing portrait binding: " + Chapter2Bindings.Id(actor));
            Sprites.Add(Chapter2Bindings.PersonId(actor).ToString(), sprite.Value.GetString()!);
        }
        if (!Sprites.ContainsKey(PlayerIdentity)) throw new InvalidDataException("Missing player portrait binding: " + PlayerIdentity);
        foreach (var actor in source.GetProperty("entities").EnumerateArray())
        {
            if (actor.GetProperty("kind").GetString() != "character") continue;
            var sprite = SpriteOf(actor.GetProperty("id").GetString()!.Split('.')[^1]);
            Cast[actor.GetProperty("name").GetString()!] = sprite;
            if (actor.TryGetProperty("aliases", out var aliases))
                foreach (var alias in aliases.EnumerateArray()) Cast[alias.GetString()!] = sprite;
        }
        foreach (var map in data.GetProperty("maps").EnumerateObject())
        {
            var roomId = Required(map.Value, "room");
            if (!entities.TryGetValue(roomId, out var room) || room.Kind != "room")
                throw new InvalidDataException("Missing map room binding: " + map.Name + " -> " + roomId);
            var place = roomId["world:room.".Length..];
            MapPlaces.Add(map.Name, place);
            _mapSpawns.Add(map.Name, Required(map.Value, "spawn"));
            if (map.Value.GetProperty("primary").GetBoolean() && !PlaceMaps.TryAdd(place, map.Name))
                throw new InvalidDataException("Duplicate primary map binding: " + place);
        }
        foreach (var place in MapPlaces.Values.Distinct())
            if (!PlaceMaps.ContainsKey(place)) throw new InvalidDataException("Missing primary map binding: " + place);
        _ = MapPlace(InitialMap);
        var site = GatheringAnchor.StartsWith("site:", StringComparison.Ordinal) ? GatheringAnchor[5..] : "";
        var anchor = source.GetProperty("sites").EnumerateArray().FirstOrDefault(s => s.GetProperty("id").GetString() == site);
        if (anchor.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException("Missing gathering anchor binding: " + GatheringAnchor);
        GatheringMap = Required(anchor, "map");
        _ = MapPlace(GatheringMap);
    }

    public string MapPlace(string key) => MapPlaces.TryGetValue(key, out var place) ? place
        : throw new InvalidDataException("Missing map binding: " + key);
    public string PlaceMap(string place) => PlaceMaps.TryGetValue(place, out var key) ? key
        : throw new InvalidDataException("Missing place map binding: " + place);
    public string MapSpawn(string key) => _mapSpawns.TryGetValue(key, out var spawn) ? spawn
        : throw new InvalidDataException("Missing map spawn binding: " + key);
    public string SpriteOf(string identity) => Sprites.TryGetValue(identity, out var sprite) ? sprite
        : throw new InvalidDataException("Missing portrait binding: " + identity);
    private static string Required(JsonElement data, string key) => data.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!
        : throw new InvalidDataException("Missing presentation binding: " + key);
}
