namespace DualOntologyEngine.Core;

public interface IEntityContainer;

public sealed class SpatialRegion(string name) : IEntityContainer
{
    public string Name { get; } = name;
    public List<SpatialLink> Links { get; } = [];

    public void ConnectTo(SpatialRegion target, float movementCost = 1, params string[] tags) =>
        Links.Add(new(target, movementCost, tags.ToHashSet()));
}

public sealed record SpatialLink(
    SpatialRegion Target,
    float MovementCost = 1,
    IReadOnlySet<string>? Tags = null
);
