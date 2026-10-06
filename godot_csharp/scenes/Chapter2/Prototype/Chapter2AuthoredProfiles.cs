using System.Collections.Immutable;
using System.Text.Json;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>Reads the complete cast's authored starting traits; the Core decision algorithms remain in C#.</summary>
public static class Chapter2AuthoredProfiles
{
    public static GameOptions Apply(string json, GameOptions? basis = null)
        => (basis ?? new GameOptions()) with { Profiles = ReadAll(json) };

    public static IReadOnlyDictionary<P, Profile> ReadAll(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("format").GetString() != "dual-ontology-world-script/v1")
            throw new InvalidDataException("Unsupported authored profile document.");
        var chapters = root.GetProperty("chapters");
        if (chapters.GetArrayLength() != 1) throw new InvalidDataException("Expected exactly one formal chapter.");
        var cast = new Dictionary<P, Profile>();
        foreach (var entity in chapters[0].GetProperty("entities").EnumerateArray())
        {
            if (entity.GetProperty("kind").GetString() != "character") continue;
            var identity = entity.GetProperty("id").GetString() ?? "";
            var separator = identity.LastIndexOf('.');
            if (separator < 0 || !Enum.TryParse<P>(identity[(separator + 1)..], false, out var who))
                throw new InvalidDataException($"Unknown cast identity {identity}.");
            if (!entity.TryGetProperty("profile", out var source))
                throw new InvalidDataException($"Character {identity} has no authored profile.");
            if (!cast.TryAdd(who, Read(who, source)))
                throw new InvalidDataException($"Duplicate authored profile {who}.");
        }
        var missing = Enum.GetValues<P>().Where(who => !cast.ContainsKey(who)).ToArray();
        if (missing.Length != 0)
            throw new InvalidDataException("Incomplete authored cast: " + string.Join(", ", missing));
        return cast;
    }

    private static Profile Read(P who, JsonElement source)
    {
        static T Member<T>(JsonElement node, string field) where T : struct, Enum
        {
            var name = node.GetProperty(field).GetString();
            if (!Enum.TryParse<T>(name, false, out var value) || !Enum.IsDefined(value))
                throw new InvalidDataException($"Invalid {typeof(T).Name} in profile.{field}: {name}");
            return value;
        }
        static RF Flag(string? name)
        {
            if (!Enum.TryParse<RF>(name, false, out var value) || value == RF.None || !Enum.IsDefined(value))
                throw new InvalidDataException($"Invalid profile flag: {name}");
            return value;
        }
        var flags = RF.None;
        foreach (var item in source.GetProperty("desire_flags").EnumerateArray()) flags |= Flag(item.GetString());
        var values = source.GetProperty("values").EnumerateObject()
            .ToImmutableDictionary(item => Flag(item.Name), item => item.Value.GetInt32());
        var prejudice = source.GetProperty("prejudice").EnumerateObject().ToImmutableDictionary(item =>
        {
            if (!Enum.TryParse<P>(item.Name, false, out var target) || !Enum.IsDefined(target))
                throw new InvalidDataException($"Unknown prejudice target {item.Name}.");
            return target;
        }, item => item.Value.GetInt32());
        var habits = source.GetProperty("habits").EnumerateArray().Select(item =>
        {
            var job = Member<Job>(item, "job");
            var slots = item.GetProperty("slots").EnumerateArray().Select(slot =>
            {
                var name = slot.GetString();
                if (!Enum.TryParse<Slot>(name, false, out var value) || !Enum.IsDefined(value))
                    throw new InvalidDataException($"Unknown habit slot {name}.");
                return value;
            }).ToArray();
            return new HabitSpec(job, slots, item.GetProperty("weight").GetInt32(),
                item.TryGetProperty("desire", out var desire) && desire.GetBoolean(),
                item.TryGetProperty("note", out var note) ? note.GetString() ?? "" : "");
        }).ToImmutableArray();
        var speed = source.GetProperty("speed").GetDouble();
        if (!double.IsFinite(speed) || speed <= 0) throw new InvalidDataException($"Invalid speed for {who}.");
        return new Profile
        {
            Id = who,
            Group = Member<Group>(source, "group"),
            Dorm = Member<Place>(source, "dorm"),
            Speed = speed,
            Cooperation = source.GetProperty("cooperation").GetInt32(),
            Build = Member<Build>(source, "build"),
            Desire = source.GetProperty("desire").GetString() ?? "",
            DemonLine = source.GetProperty("demon_line").GetString() ?? "",
            DesireFlags = flags,
            Strength = source.GetProperty("strength").GetInt32(),
            Lock = source.GetProperty("lock").GetInt32(),
            Signal = source.GetProperty("signal").GetInt32(),
            Structure = source.GetProperty("structure").GetInt32(),
            GreenhouseSkill = source.GetProperty("greenhouse_skill").GetInt32(),
            Cook = source.GetProperty("cook").GetInt32(),
            Craftsman = source.GetProperty("craftsman").GetBoolean(),
            Values = values,
            Prejudice = prejudice,
            DemonPrejudice = source.GetProperty("demon_prejudice").GetInt32(),
            Habits = habits,
            Voice = source.GetProperty("voice").GetString() ?? "",
        };
    }
}
