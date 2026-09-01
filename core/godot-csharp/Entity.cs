#nullable enable

using Godot;

namespace DualOntologyEngine.Core;

//这里将存放空间，实体，效果等基础类。目前我准备采用最简化方式放置。目前设计下，主空间具有类似ROOT的结构。
//目前设计中，空间是一个完全空置的容器。

public class TurnSequenceManager
{
    public SortedDictionary<float, IActionUnit> motionQueue { get; private set; } = new();
    public float currentMotionValue { get; private set; }

    public void reset()
    {
        motionQueue.Clear();
        currentMotionValue = 0;
    }

    // 传入的行动值为相对值，队列中保存的是绝对行动值。
    public void add(SortedDictionary<float, Entity> actions)
    {
        foreach (var (relativeMotionValue, actionUnit) in actions)
        {
            var absoluteMotionValue = currentMotionValue + relativeMotionValue;

            while (motionQueue.ContainsKey(absoluteMotionValue))
                absoluteMotionValue += 0.01f;

            motionQueue.Add(absoluteMotionValue, actionUnit);
        }
    }

    // 推进最小行动值的项目，完成动作后添加它的下一轮排期。
    public IActionUnit? next()
    {
        if (motionQueue.Count == 0)
            return null;

        var nextAction = motionQueue.First();
        motionQueue.Remove(nextAction.Key);
        currentMotionValue = nextAction.Key;
        add(nextAction.Value.update_motion_queue());
        return nextAction.Value;
    }
}








public class Entity : IActionUnit
{
    public Entity? parent { get; private set; }
    public IDictionary<Entity, int> children => childPositions;
    public IReadOnlyDictionary<string, int> tagValues => tags;
    public int position => parent is not null && parent.children.TryGetValue(this, out var value) ? value : 0;
    public Vector3 relativePosition { get; private set; }

    // TODO: 后期修改成接口。
    public Node3D? sceneEntity { get; private set; }
    public string name { get; }

    private readonly Dictionary<Entity, int> childPositions = [];
    private readonly Dictionary<string, int> tags;

    public Entity(
        string name,
        Entity? parent,
        Dictionary<string, int> tags,
        int position = 0,
        Vector3 relativePosition = default
    )
    {
        this.name = name;
        this.tags = new(tags);
        this.relativePosition = relativePosition;
        if (parent is not null)
            set_parent(parent, position);
    }

    public void set_relative_position(Vector3 value) => relativePosition = value;

    public void set_parent(Entity? newParent, int position = 0)
    {
        if (newParent is null)
        {
            parent?.children.Remove(this);
            parent = null;
            sceneEntity?.QueueFree();
            sceneEntity = null;
            return;
        }

        var occupiedPositions = newParent.children
            .Where(child => child.Key != this)
            .Select(child => child.Value)
            .ToHashSet();

        while (occupiedPositions.Contains(position))
            position++;

        if (parent == newParent)
        {
            parent.children[this] = position;
            return;
        }

        parent?.children.Remove(this);
        parent = newParent;
        parent.children.Add(this, position);
    }

    public int getTag(string tagName) => tags.GetValueOrDefault(tagName);

    public int changeTag(string tagName, int amount)
    {
        var value = getTag(tagName) + amount;

        if (value <= 0)
            tags.Remove(tagName);
        else
            tags[tagName] = value;

        return value;
    }

    // 返回需要加入新一轮结算的实体；不包含自身即表示将自身移出执行序列。
    public virtual SortedDictionary<float, Entity> update_motion_queue() => new()
    {
        [100f] = this,
    };
}

public interface IActionUnit
{
    public string name { get; }
    public Entity? parent { get; }
    public SortedDictionary<float, Entity> update_motion_queue();

}
