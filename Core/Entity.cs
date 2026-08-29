namespace DualOntologyEngine.Core;

public class Entity(string name, float actionDelay = 0) : IActionUnit, IEntityContainer
{
    public string Name { get; set; } = name;
    public float ActionDelay { get; private set; } = actionDelay;
    public IEntityContainer? Parent { get; set; }
    public Dictionary<string, int> Tags { get; } = [];

    public virtual IEnumerable<IActionUnit> Resolve()
    {
        yield return this;
    }

    public virtual void ScheduleNextAction() { }

    public void SetActionDelay(float value) => ActionDelay = value;
}

public class StackableItem(
    string name,
    int quantity = 1,
    float actionDelay = 0
) : Entity(name, actionDelay)
{
    public int Quantity { get; set; } = quantity;
}
