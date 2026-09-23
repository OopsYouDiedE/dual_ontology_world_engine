using DualOntologyWorldEngine.Core;
using Godot;

namespace DualOntologyWorldEngine.Game.Story;

/// <summary>Connects one Godot room scene to its Core Space and visual decoration.</summary>
public partial class RoomBinding : Node
{
    [Export] public string RoomName { get; set; } = "房间";
    [Export] public NodePath[] DecorationLayerPaths { get; set; } = [];
    [Export] public NodePath ItemsRootPath { get; set; } = "../Items";

    public Essence Room { get; private set; } = null!;

    public override void _Ready()
    {
        Room = new Essence(RoomName, ProjectionKind.Space);
        var decoration = Room.Add(new RoomDecorationProperty());

        foreach (var path in DecorationLayerPaths)
            if (GetNodeOrNull<TileMapLayer>(path) is { } layer)
                decoration.Layers.Add(layer);

        var itemsRoot = GetNode(ItemsRootPath);
        foreach (var node in itemsRoot.FindChildren("*", "", true, false).OfType<RoomItem>())
            node.PlaceIn(Room);
    }
}

/// <summary>Game-layer references to the visual TileMap layers that decorate a room.</summary>
public sealed class RoomDecorationProperty : Property
{
    public List<TileMapLayer> Layers { get; } = [];
}
