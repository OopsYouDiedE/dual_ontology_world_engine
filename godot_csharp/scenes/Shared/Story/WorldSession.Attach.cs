using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

public sealed partial class WorldSession
{
    public bool TryEntity(string id, out Essence entity)
    {
        lock (_gate)
        {
            if (_entities.TryGetValue(id, out var registered)) { entity = registered.Entity; return true; }
            entity = null!;
            return false;
        }
    }

    /// <summary>Registers additional authored behavior against the same live objects. Initial defaults never overwrite running state.</summary>
    public void AttachExistingContent(ChapterWorldDefinition content, IReadOnlyDictionary<string, Essence> entities,
        IReadOnlyDictionary<string, (SoulEssence Soul, Projection Self)> characters)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loaded.Contains(content.Id)) throw new InvalidOperationException("Content is already attached: " + content.Id);
            foreach (var required in content.Requires)
                if (!_loaded.Contains(required)) throw new InvalidOperationException("Missing running content: " + required);
            if (content.Patches.Count != 0) throw new NotSupportedException("Attach behavior without rewriting live defaults.");
            var declared = content.Entities.ToDictionary(e => DeclarationId(e.Id, e.Scope, content.Id), StringComparer.Ordinal);
            var ids = _entities.Keys.Concat(declared.Keys).ToHashSet(StringComparer.Ordinal);
            var plan = declared.Select(p => Read(p.Key, p.Value, content.Id, ids)).ToArray();
            var kinds = plan.ToDictionary(p => p.Id, p => p.Kind, StringComparer.Ordinal);
            foreach (var item in plan)
            {
                if (!entities.TryGetValue(item.Id, out var entity)) throw new InvalidDataException("Missing live binding: " + item.Id);
                if ((item.Kind == "room") != (entity.Kind == ProjectionKind.Space)) throw new InvalidDataException("Binding kind mismatch: " + item.Id);
                if (Runtime.Representations(entity).Count != 1) throw new InvalidDataException("Binding requires one real representation: " + item.Id);
                if (_entities.TryGetValue(item.Id, out var old) && (old.Kind != item.Kind || !ReferenceEquals(old.Entity, entity)))
                    throw new InvalidDataException("Persistent entity identity changed: " + item.Id);
                if (item.Space is not null && (kinds.GetValueOrDefault(item.Space) ?? _entities[item.Space].Kind) != "room")
                    throw new InvalidDataException("Spatial parent must be a room: " + item.Id);
                if (item.Kind == "character" && (!characters.TryGetValue(item.Id, out var actor)
                    || !ReferenceEquals(Runtime.KnownProjection(actor.Soul, entity), actor.Self)))
                    throw new InvalidDataException("Persistent character cognition changed: " + item.Id);
            }
            var behaviors = ValidateCandidates(content, ids, kinds);
            EndChapterLifetime();
            foreach (var item in plan)
            {
                if (_entities.ContainsKey(item.Id)) continue;
                var entity = entities[item.Id];
                ScriptCharacter? actor = null;
                if (item.Kind == "character")
                {
                    var bound = characters[item.Id];
                    actor = World.RegisterCharacter(item.Id, bound.Soul, entity, bound.Self);
                    actor.SpriteFramesPath = item.Sprite;
                }
                else World.Register(item.Id, entity);
                _entities.Add(item.Id, new(item.Id, item.Name, item.Kind, item.Scope, item.Space, item.Sprite,
                    item.Defaults, item.Observable, entity, actor));
                _declarations.Add(item.Id, item);
                if (actor is not null)
                    _known.Add(item.Id, ids.Select(id => (Id: id, Entity: entities.GetValueOrDefault(id) ?? _entities[id].Entity))
                        .Select(p => (p.Id, Projection: Runtime.KnownProjection(actor.Soul, p.Entity)))
                        .Where(p => p.Projection is not null)
                        .ToDictionary(p => p.Id, p => p.Projection!, StringComparer.Ordinal));
            }
            RegisterCandidates(content.Id, behaviors);
            InvalidateChoiceTokens();
            _loaded.Add(content.Id); _order.Add(content.Id); CurrentChapter = content.Id;
        }
    }
}
