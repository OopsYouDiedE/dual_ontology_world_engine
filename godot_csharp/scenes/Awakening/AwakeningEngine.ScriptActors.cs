using DualOntologyWorldEngine.Core;
using Godot;

public partial class AwakeningEngine
{
    public Godot.Collections.Array ScriptCharacters(string room)
    {
        var result = new Godot.Collections.Array();
        if (_morning.DefinedScriptWorld is not { } world) return result;
        var roomIndex = System.Array.IndexOf(RoomKeys, room);
        if (roomIndex < 0) return result;
        var space = world["room:" + (DualOntologyWorldEngine.Awakening.Room)roomIndex];
        foreach (var character in world.Characters.Values.Where(c => c.SpriteFramesPath is not null &&
                     !c.Key.StartsWith("person:", StringComparison.Ordinal) &&
                     ReferenceEquals(c.Body.Get<SpatialProperty>()?.Space, space)))
            result.Add(new Godot.Collections.Dictionary
            {
                ["id"] = character.Key, ["name"] = character.Body.TrueName,
                ["frames_path"] = character.SpriteFramesPath!,
            });
        return result;
    }
}
