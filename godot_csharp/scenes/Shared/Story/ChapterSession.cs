namespace DualOntologyWorldEngine.Story;

/// <summary>两章共同的逻辑位置、玩家观察和剧情进度；不依赖宿主坐标或 Godot。</summary>
public sealed class ChapterSession
{
    public Dictionary<string, string> CharacterRooms { get; } = new();
    public Dictionary<string, string> ObjectRooms { get; } = new();
    private readonly Dictionary<string, Dictionary<string, object>> _progress = new();
    private readonly Dictionary<string, HashSet<string>> _observations = new();

    public Dictionary<string, object> GetProgress(string group)
    {
        if (!_progress.TryGetValue(group, out var state)) _progress[group] = state = new();
        return state;
    }
    public bool Has(string group, string key) => GetProgress(group).ContainsKey(key);
    public void Set(string group, string key, object value) => GetProgress(group)[key] = value;
    public void Remove(string group, string key) => GetProgress(group).Remove(key);
    public void Locate(string actor, string room) => CharacterRooms[actor] = room;
    public void Place(string target, string room) => ObjectRooms[target] = room;
    public bool IsLocalTarget(string actor, string target)
    {
        var room = CharacterRooms.GetValueOrDefault(actor);
        var targetRoom = CharacterRooms.GetValueOrDefault(target) ?? ObjectRooms.GetValueOrDefault(target);
        return room is not null && room == targetRoom;
    }
    public IEnumerable<string> RoomCharacters(string room) => CharacterRooms.Where(p => p.Value == room).Select(p => p.Key);
    public IEnumerable<string> RoomObjects(string room) => ObjectRooms.Where(p => p.Value == room).Select(p => p.Key);
    public void Observe(string actor, string target)
    {
        if (!_observations.TryGetValue(actor, out var known)) _observations[actor] = known = new();
        known.Add(target);
    }
    public bool HasObservation(string actor, string target) => _observations.TryGetValue(actor, out var known) && known.Contains(target);
}
