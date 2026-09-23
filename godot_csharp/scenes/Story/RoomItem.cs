using DualOntologyWorldEngine.Core;
using Godot;

namespace DualOntologyWorldEngine.Game.Story;

/// <summary>An interactable story prop represented by a Godot sprite.</summary>
public partial class RoomItem : Sprite2D
{
    [Export] public string DisplayName { get; set; } = "物件";
    [Export] public string DialogueTitle { get; set; } = "";

    public Essence Essence { get; private set; } = null!;

    public override void _EnterTree() => EnsureEssence();

    public void EnsureEssence()
    {
        Essence ??= new Essence(DisplayName);
        if (Essence.Get<SpatialProperty>() is null)
            Essence.Add(new SpatialProperty());
    }

    public void PlaceIn(Essence room)
    {
        EnsureEssence();
        var spatial = Essence.Get<SpatialProperty>()!;
        spatial.Space = room;
        spatial.Holder = null;
    }
}
