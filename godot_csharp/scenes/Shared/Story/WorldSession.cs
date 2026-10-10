using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

public sealed class WorldPropertyDefinition
{
    public string Type { get; set; } = "";
    public JsonElement Value { get; set; }
}
public sealed class WorldEntityDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Scope { get; set; } = "world";
    public Dictionary<string, WorldPropertyDefinition> Properties { get; set; } = new();
}
public sealed class WorldEntityExtension
{
    public string Target { get; set; } = "";
    public string Mode { get; set; } = "extend";
    public Dictionary<string, WorldPropertyDefinition> Properties { get; set; } = new();
}
public sealed class ChapterWorldDefinition
{
    public JsonElement Design { get; set; }
    public string Id { get; set; } = "";
    public List<string> Requires { get; set; } = new();
    public List<WorldEntityDefinition> Entities { get; set; } = new();
    public List<WorldEntityExtension> Patches { get; set; } = new();
    public List<WorldCandidateDefinition> Candidates { get; set; } = new();
}

/// <summary>Loads persistent declarations and flat candidates; unsupported legacy behavior is rejected explicitly.</summary>
public static class WorldScriptDocument
{
    public static IReadOnlyList<ChapterWorldDefinition> Parse(string json, bool allowChapterAdapter = false)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("format").GetString() != "dual-ontology-world-script/v1")
            throw new InvalidDataException("Unsupported world script format.");
        var chapters = root.GetProperty("chapters");
        if (root.TryGetProperty("behavior", out var behavior) && behavior.GetArrayLength() > 0)
            throw new NotSupportedException("Legacy top-level behavior is not executable; compile flat chapter candidates.");
        foreach (var chapter in chapters.EnumerateArray())
        {
            foreach (var unsupported in new[] { "interactions", "rules" })
                if (chapter.TryGetProperty(unsupported, out var value) && value.GetArrayLength() > 0)
                    throw new NotSupportedException($"The persistent declaration loader does not execute {unsupported} yet.");
            if (!allowChapterAdapter && new[] { "sequences", "sites", "furniture", "ui_text", "requests" }.Any(key => chapter.TryGetProperty(key,out var value) && (value.ValueKind==JsonValueKind.Array?value.GetArrayLength()>0:value.ValueKind==JsonValueKind.Object?value.EnumerateObject().Any():true)))
                throw new NotSupportedException("This authored chapter requires its chapter adapter; generic loading cannot silently discard narrative or site behavior.");
        }
        return JsonSerializer.Deserialize<List<ChapterWorldDefinition>>(chapters.GetRawText(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
    }
}

/// <summary>
/// One live Core world across chapter loads. Declarations are validated before mutation;
/// subsequent chapters reuse objective entities and each character's existing cognition.
/// This is an in-memory session, not a save-file codec.
/// </summary>
public sealed partial class WorldSession : IDisposable
{
    private sealed record Registered(string Id, string Name, string Kind, string Scope,
        string? Space, string? Sprite, ImmutableDictionary<string, int> Defaults, ImmutableHashSet<string> Observable,
        Essence Entity, ScriptCharacter? Character);
    private sealed record Planned(string Id, string Name, string Kind, string Scope, string? Space, string? Sprite,
        ImmutableDictionary<string, int> Defaults, ImmutableHashSet<string> Observable, ImmutableDictionary<string, int> Beliefs);
    private readonly Dictionary<string, Registered> _entities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Planned> _declarations = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loaded = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();
    private readonly HashSet<string> _endedChapters = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private bool _disposed;
    public WorldRuntime Runtime { get; }
    public SimulationSession Simulation { get; }
    public CoreScriptWorld World { get; }
    public string? CurrentChapter { get; private set; }
    public IReadOnlyList<string> LoadedChapters => _order.AsReadOnly();
    private readonly bool _ownsSimulation;
    public WorldSession() : this(new WorldRuntime(), null) { }
    public WorldSession(WorldRuntime runtime, SimulationSession? simulation)
    {
        Runtime = runtime;
        _ownsSimulation = simulation is null;
        Simulation = simulation ?? new(Runtime);
        World = new(Runtime, Simulation);
        Simulation.Completed += BehaviorCompleted;
    }
    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; Simulation.Completed -= BehaviorCompleted; if (_ownsSimulation) Simulation.Dispose(); _disposed = true; }
    }

    /// <summary>Adopts an already running chapter. All bindings are validated before registry mutation;
    /// no bodies, souls, state values or mental worlds are recreated or initialized.</summary>
    public void BindExisting(ChapterWorldDefinition chapter, IReadOnlyDictionary<string, Essence> entities,
        IReadOnlyDictionary<string, (SoulEssence Soul, Projection Self)> characters, IReadOnlyList<string> completedDependencies)
    {
        lock (_gate)
        {
            if (_entities.Count != 0 || _loaded.Count != 0) throw new InvalidOperationException("Existing-world adoption requires an empty registry.");
            var declared = chapter.Entities.ToDictionary(e => DeclarationId(e.Id,e.Scope,chapter.Id),StringComparer.Ordinal);
            var ids = declared.Keys.ToHashSet(StringComparer.Ordinal);
            if (!chapter.Requires.All(completedDependencies.Contains)) throw new InvalidOperationException("A required live chapter was not completed.");
            var plan = declared.Select(p => Read(p.Key,p.Value,chapter.Id,ids)).ToArray();
            foreach (var item in plan)
            {
                if (!entities.TryGetValue(item.Id,out var entity)) throw new InvalidDataException("Missing live entity binding: "+item.Id);
                if ((item.Kind=="room") != (entity.Kind==ProjectionKind.Space)) throw new InvalidDataException("Live entity kind mismatch: "+item.Id);
                if (item.Kind=="character" && (!characters.TryGetValue(item.Id,out var actor) || !ReferenceEquals(Runtime.KnownProjection(actor.Soul,entity),actor.Self))) throw new InvalidDataException("Missing or invalid live character cognition: "+item.Id);
                if (Runtime.Representations(entity).Count!=1) throw new InvalidDataException("Live binding must have one reality representation: "+item.Id);
                // Declared space is an initial default, not an override of a running entity's location.
                if (item.Space is not null && declared[item.Space].Kind != "room") throw new InvalidDataException("Spatial declaration must reference a room: "+item.Id);
            }
            if (chapter.Patches.Count!=0) throw new NotSupportedException("Existing-world adoption does not apply author patches.");
            var behaviors=ValidateCandidates(chapter,ids,plan.ToDictionary(p=>p.Id,p=>p.Kind));
            foreach(var item in plan)
            {
                var entity=entities[item.Id]; ScriptCharacter? actor=null;
                if(item.Kind=="character") { var bound=characters[item.Id]; actor=World.RegisterCharacter(item.Id,bound.Soul,entity,bound.Self); }
                else World.Register(item.Id,entity);
                _entities.Add(item.Id,new(item.Id,item.Name,item.Kind,item.Scope,item.Space,item.Sprite,item.Defaults,item.Observable,entity,actor));
                _declarations.Add(item.Id,item);
                if(actor is not null) _known[item.Id]=plan.Select(p=>(p.Id,Projection:Runtime.KnownProjection(actor.Soul,entities[p.Id]))).Where(p=>p.Projection is not null).ToDictionary(p=>p.Id,p=>p.Projection!,StringComparer.Ordinal);
            }
            foreach(var dependency in completedDependencies) { _loaded.Add(dependency);_order.Add(dependency); }
            RegisterCandidates(chapter.Id,behaviors);_loaded.Add(chapter.Id);_order.Add(chapter.Id);CurrentChapter=chapter.Id;
        }
    }
    public static void ValidateChapter(ChapterWorldDefinition chapter)
    {
        ValidateName(chapter.Id,"chapter id");
        var definitions=chapter.Entities.ToDictionary(e=>DeclarationId(e.Id,e.Scope,chapter.Id),StringComparer.Ordinal);
        var ids=definitions.Keys.ToHashSet(StringComparer.Ordinal);
        var plan=definitions.Select(p=>Read(p.Key,p.Value,chapter.Id,ids)).ToArray();
        foreach(var item in plan) if(item.Space is not null && definitions[item.Space].Kind!="room") throw new InvalidDataException("Space must reference a room: "+item.Id);
        if(chapter.Patches.Count!=0) throw new NotSupportedException("Live adoption does not support declaration patches.");
        _=ValidateCandidatePlan(chapter,ids,plan.ToDictionary(p=>p.Id,p=>p.Kind),new(StringComparer.Ordinal));
    }
    public Essence Entity(string reference)
    {
        lock (_gate) return _entities[Resolve(reference, CurrentChapter ?? "", _entities.Keys)].Entity;
    }
    public ScriptCharacter Character(string reference)
    {
        lock (_gate) return _entities[Resolve(reference, CurrentChapter ?? "", _entities.Keys)].Character
            ?? throw new ArgumentException("The referenced entity is not a character.");
    }
    public void Load(ChapterWorldDefinition chapter)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ValidateName(chapter.Id, "chapter id");
            if (chapter.Id.Contains(':')) throw new InvalidDataException("Chapter ids cannot contain namespace separators.");
            if (_loaded.Contains(chapter.Id)) throw new InvalidOperationException("Chapter already initialized: " + chapter.Id);
            if (chapter.Requires.Distinct(StringComparer.Ordinal).Count() != chapter.Requires.Count)
                throw new InvalidDataException("Duplicate chapter dependency.");
            foreach (var dependency in chapter.Requires)
                if (!_loaded.Contains(dependency)) throw new InvalidOperationException("Required chapter has not been loaded: " + dependency);
            var declarations = new Dictionary<string, WorldEntityDefinition>(StringComparer.Ordinal);
            foreach (var definition in chapter.Entities)
            {
                var id = DeclarationId(definition.Id, definition.Scope, chapter.Id);
                if (!declarations.TryAdd(id, definition)) throw new InvalidDataException("Duplicate entity declaration: " + id);
            }
            var known = _entities.Keys.Concat(declarations.Keys).ToHashSet(StringComparer.Ordinal);
            var plan = declarations.Select(p => Read(p.Key, p.Value, chapter.Id, known)).ToArray();
            var plannedKinds = plan.ToDictionary(p => p.Id, p => p.Kind, StringComparer.Ordinal);
            foreach (var item in plan)
            {
                if (item.Space is { } space && (plannedKinds.GetValueOrDefault(space) ?? _entities[space].Kind) != "room")
                    throw new InvalidDataException("A spatial parent must be a room: " + space);
                if (_entities.ContainsKey(item.Id) && !Equivalent(_declarations[item.Id], item))
                    throw new InvalidDataException("Conflicting entity declaration: " + item.Id + ". Use an explicit extension for new defaults.");
                if (!_entities.ContainsKey(item.Id) && (World.Objects.ContainsKey(item.Id) || World.Characters.ContainsKey(item.Id)))
                    throw new InvalidDataException("Entity id already registered outside the chapter loader: " + item.Id);
            }
            var extensions = new Dictionary<string, (Dictionary<string, int> Defaults, HashSet<string> Observable)>(StringComparer.Ordinal);
            var behaviors = ValidateCandidates(chapter, known, plannedKinds);
            foreach (var extension in chapter.Patches)
            {
                if (extension.Mode != "extend") throw new NotSupportedException("Runtime mutation must use real actions; only declaration extensions are supported.");
                var id = Resolve(extension.Target, chapter.Id, known);
                var values = ReadProperties(extension.Properties, allowSpatial: false);
                if (!extensions.TryGetValue(id, out var pending)) pending = (new(StringComparer.Ordinal), new(StringComparer.Ordinal));
                var old = _entities.GetValueOrDefault(id);
                var declaration = plan.SingleOrDefault(p => p.Id == id);
                foreach (var (tag, value) in values.Defaults)
                {
                    var previous = pending.Defaults.TryGetValue(tag, out var p) ? p : old?.Defaults.GetValueOrDefault(tag) ?? declaration?.Defaults.GetValueOrDefault(tag);
                    var hasPrevious = pending.Defaults.ContainsKey(tag) || old?.Defaults.ContainsKey(tag) == true || declaration?.Defaults.ContainsKey(tag) == true;
                    if (hasPrevious && previous != value) throw new InvalidDataException("Conflicting default extension: " + id + "/" + tag);
                    pending.Defaults[tag] = value;
                }
                pending.Observable.UnionWith(values.Observable);
                extensions[id] = pending;
            }
            // Build detached objective objects first. No callback or script code runs during commit.
            var staged = plan.Where(p => !_entities.ContainsKey(p.Id)).ToDictionary(p => p.Id,
                p => new Essence(p.Name, p.Kind switch { "room" => ProjectionKind.Space, "character" => ProjectionKind.Body, _ => ProjectionKind.Object }), StringComparer.Ordinal);
            Essence Find(string id) => staged.GetValueOrDefault(id) ?? _entities[id].Entity;
            foreach (var item in plan.Where(p => staged.ContainsKey(p.Id)))
            {
                var entity = staged[item.Id];
                var state = entity.Add(new StateProperty());
                foreach (var (tag, value) in item.Defaults) state.Values[new(tag)] = value;
                foreach (var tag in item.Observable) state.Observable.Add(new(tag));
                if (extensions.TryGetValue(item.Id, out var initialExtension))
                {
                    foreach (var (tag, value) in initialExtension.Defaults) state.Values.TryAdd(new(tag), value);
                    foreach (var tag in initialExtension.Observable) state.Observable.Add(new(tag));
                }
                if (item.Space is not null) entity.Add(new SpatialProperty { Space = Find(item.Space) });
            }
            EndChapterLifetime();
            foreach (var item in plan.Where(p => staged.ContainsKey(p.Id)).OrderBy(p => p.Kind == "room" ? 0 : p.Kind == "object" ? 1 : 2))
            {
                var entity = staged[item.Id];
                ScriptCharacter? character = null;
                if (item.Kind == "character")
                {
                    var soul = new SoulEssence(item.Name + "之魂");
                    soul.Relations.Add((Relations.Controls, entity));
                    Runtime.Project(entity, item.Name);
                    Runtime.Project(soul, soul.TrueName);
                    var seen = Runtime.Observe(soul, Runtime.Representations(Find(item.Space!)).Concat(Runtime.Representations(entity)), Simulation.CTB.Now);
                    character = World.RegisterCharacter(item.Id, soul, entity, seen.Single(p => p.Kind == ProjectionKind.Body));
                    character.SpriteFramesPath = item.Sprite;
                }
                else World.Register(item.Id, entity);
                _entities.Add(item.Id, new(item.Id, item.Name, item.Kind, item.Scope, item.Space, item.Sprite,
                    item.Defaults, item.Observable, entity, character));
                _declarations.Add(item.Id, item);
                if (character is not null) RememberInitialCharacter(item, character);
            }
            foreach (var (id, extension) in extensions)
            {
                var registered = _entities[id];
                var state = registered.Entity.Get<StateProperty>()!;
                foreach (var (tag, value) in extension.Defaults) state.Values.TryAdd(new(tag), value);
                foreach (var tag in extension.Observable) state.Observable.Add(new(tag));
                _entities[id] = registered with { Defaults = registered.Defaults.SetItems(extension.Defaults), Observable = registered.Observable.Union(extension.Observable) };
            }
            Runtime.Sync();
            RegisterCandidates(chapter.Id, behaviors);
            InvalidateChoiceTokens();
            _loaded.Add(chapter.Id); _order.Add(chapter.Id); CurrentChapter = chapter.Id;
        }
    }
    public void EndChapterLifetime()
    {
        lock (_gate)
        {
            if (CurrentChapter is not { } owner || _endedChapters.Contains(owner)) return;
            if (_entities.Values.Any(e => e.Character is { } actor && Simulation.IsActing(actor.Self)))
                throw new InvalidOperationException("Finish pending actions before ending the chapter lifetime.");
            RuleLifetimes.End(_entities.Values.Where(e => e.Character is not null).Select(e => e.Character!.Soul), LifetimeScope.Chapter, owner, Simulation.CTB.Now);
            RuleLifetimes.EndSources(_entities.Values.Select(e => e.Entity), LifetimeScope.Chapter, owner);
            Runtime.Sync();
            _endedChapters.Add(owner);
            InvalidateChoiceTokens();
        }
    }
    private static Planned Read(string id, WorldEntityDefinition definition, string chapter, HashSet<string> known)
    {
        if (string.IsNullOrWhiteSpace(definition.Name)) throw new InvalidDataException("An entity must have a display name: " + id);
        if (definition.Kind is not ("room" or "object" or "character")) throw new InvalidDataException("Unsupported entity kind: " + definition.Kind);
        var properties = ReadProperties(definition.Properties, allowSpatial: true);
        var space = properties.Space is null ? null : Resolve(properties.Space, chapter, known);
        if (definition.Kind == "room" && space is not null || definition.Kind != "room" && space is null)
            throw new InvalidDataException("Rooms cannot have spatial parents; objects and characters require a room.");
        if (definition.Kind != "character" && properties.Sprite is not null) throw new InvalidDataException("Sprite metadata is supported only on characters.");
        if (definition.Kind != "character" && properties.Beliefs.Count > 0) throw new InvalidDataException("Initial beliefs require a character.");
        return new(id, definition.Name, definition.Kind, definition.Scope, space, properties.Sprite, properties.Defaults, properties.Observable, properties.Beliefs);
    }
    private static (string? Space, string? Sprite, ImmutableDictionary<string, int> Defaults, ImmutableHashSet<string> Observable, ImmutableDictionary<string, int> Beliefs)
        ReadProperties(Dictionary<string, WorldPropertyDefinition> properties, bool allowSpatial)
    {
        string? space = null, sprite = null;
        var defaults = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        var observable = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        var beliefs = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var (key, property) in properties)
        {
            if (key == "space" && allowSpatial && property.Type == "entity" && property.Value.ValueKind == JsonValueKind.String) space = property.Value.GetString();
            else if (key == "sprite" && allowSpatial && property.Type == "string" && property.Value.ValueKind == JsonValueKind.String) sprite = property.Value.GetString();
            // Presentation metadata is validated here, but never becomes a Core state or belief.
            else if (key == "presentation_anchor" && allowSpatial && property.Type == "boolean" && property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) { }
            else if (key.StartsWith("state:", StringComparison.Ordinal) && property.Type == "integer" && property.Value.TryGetInt32(out var value))
            { ValidateName(key[6..], "state tag"); defaults.Add(key[6..], value); }
            else if (allowSpatial && key.StartsWith("belief:", StringComparison.Ordinal) && property.Type == "integer" && property.Value.TryGetInt32(out var belief))
            { ValidateName(key[7..], "belief tag"); beliefs.Add(key[7..], belief); }
            else if (key.StartsWith("observable:", StringComparison.Ordinal) && property.Type == "boolean" && property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            { ValidateName(key[11..], "observable tag"); if (property.Value.GetBoolean()) observable.Add(key[11..]); }
            else throw new NotSupportedException("Unsupported property or property type: " + key + " / " + property.Type);
        }
        return (space, sprite, defaults.ToImmutable(), observable.ToImmutable(), beliefs.ToImmutable());
    }
    private static bool Equivalent(Planned a, Planned b) => a.Name == b.Name && a.Kind == b.Kind && a.Scope == b.Scope && a.Space == b.Space && a.Sprite == b.Sprite
        && a.Defaults.Count == b.Defaults.Count && a.Defaults.All(p => b.Defaults.TryGetValue(p.Key, out var value) && value == p.Value) && a.Observable.SetEquals(b.Observable)
        && a.Beliefs.Count == b.Beliefs.Count && a.Beliefs.All(p => b.Beliefs.TryGetValue(p.Key, out var value) && value == p.Value);
    private static string DeclarationId(string id, string scope, string chapter)
    {
        if (scope is not ("world" or "chapter")) throw new InvalidDataException("Unknown entity scope: " + scope);
        ValidateName(id, "entity id");
        var prefix = scope == "world" ? "world:" : "chapter:" + chapter + ":";
        if (id.StartsWith("world:", StringComparison.Ordinal) || id.StartsWith("chapter:", StringComparison.Ordinal))
        { if (!id.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Entity id conflicts with its declared scope: " + id); return id; }
        return prefix + id;
    }
    private static string Resolve(string reference, string chapter, IEnumerable<string> knownIds)
    {
        var known = knownIds as ISet<string> ?? knownIds.ToHashSet(StringComparer.Ordinal);
        if (reference.StartsWith("world:", StringComparison.Ordinal) || reference.StartsWith("chapter:", StringComparison.Ordinal))
        { if (known.Contains(reference)) return reference; throw new InvalidDataException("Unknown entity reference: " + reference); }
        var local = "chapter:" + chapter + ":" + reference;
        if (known.Contains(local)) return local;
        var global = "world:" + reference;
        return known.Contains(global) ? global : throw new InvalidDataException("Unknown entity reference: " + reference);
    }
    private static void ValidateName(string value, string subject)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace) || value.StartsWith(':') || value.EndsWith(':') || value.Contains("::", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid " + subject + ".");
    }
}
