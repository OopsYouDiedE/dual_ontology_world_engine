using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core;

/// <summary>
/// 表示世界树中的节点基类。
/// </summary>
/// <remarks>
/// 世界树是引擎中管理存在层次与空间拓扑的核心结构。
/// 所有世界中的具体对象（Entity）以及跨场景拓扑连接（Passage）均作为树成员挂载于世界树中。
/// 统一下沉承载 Tags（标签）、Properties（客观属性）与 Facets（语义关系）。
/// </remarks>
public abstract class TreeMember
{
    public string Name { get; set; }
    public TreeMember? Parent { get; private set; }
    public Dictionary<TreeMember, int> Children { get; } = new();

    public IReadOnlyList<TreeMember> Items =>
        Children.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();

    public Dictionary<string, int> Tags { get; }
    public Dictionary<string, object> Properties { get; }
    public List<Facet> Facets { get; }

    protected TreeMember(
        string name,
        TreeMember? parent = null,
        IDictionary<string, int>? tags = null,
        int position = 0,
        IDictionary<string, object>? properties = null,
        IEnumerable<Facet>? facets = null,
        IEnumerable<Enum>? enumTags = null)
    {
        Name = name;
        Tags = tags != null ? new Dictionary<string, int>(tags) : new();
        Properties = properties != null ? new Dictionary<string, object>(properties) : new();
        Facets = facets != null ? new List<Facet>(facets) : new();

        if (enumTags != null)
        {
            foreach (var et in enumTags) SetTag(et, 1);
        }

        if (parent != null)
        {
            Reparent(parent, position);
        }
    }

    protected TreeMember(string name, TreeMember? parent, int position)
        : this(name, parent, null, position)
    {
    }

    public TreeMember Reparent(TreeMember? newParent, int position = 0)
    {
        Parent?.Children.Remove(this);
        Parent = newParent;
        if (Parent == null) return this;

        var occupied = new HashSet<int>(Parent.Children.Values);
        while (occupied.Contains(position))
        {
            position++;
        }

        Parent.Children[this] = position;
        return this;
    }

    public int GetPosition() =>
        Parent != null && Parent.Children.TryGetValue(this, out var pos) ? pos : -1;

    #region 标签与角色操作

    public int Tag(Enum tag) => Tag(tag.ToString());
    public void SetTag(Enum tag, int strength = 1) => SetTag(tag.ToString(), strength);
    public bool HasTag(Enum tag) => Tag(tag) > 0;
    public int ChangeTag(Enum tag, int amount) => ChangeTag(tag.ToString(), amount);

    public TreeMember AddTags(params Enum[] tags)
    {
        foreach (var t in tags) SetTag(t, 1);
        return this;
    }

    public int Tag(string name) => Tags.TryGetValue(name, out var value) ? value : 0;

    public void SetTag(string name, int strength)
    {
        if (strength > 0) Tags[name] = strength;
        else Tags.Remove(name);
    }

    public int ChangeTag(string name, int amount)
    {
        var newValue = Math.Max(0, Tag(name) + amount);
        SetTag(name, newValue);
        return newValue;
    }

    public bool HasTag(string name) => Tag(name) > 0;
    public bool Has(string name) => HasTag(name);
    public int Change(string name, int amount) => ChangeTag(name, amount);

    public bool HasRole(Role role) => HasTag(role);
    public void AddRole(Role role) => SetTag(role, 1);
    public void RemoveRole(Role role) => SetTag(role, 0);

    #endregion

    #region 客观性质（Properties）

    public T? Get<T>(string name, T? defaultValue = default)
    {
        if (Properties.TryGetValue(name, out var value) && value is T typed)
        {
            return typed;
        }
        return defaultValue;
    }

    public object Set(string name, object value)
    {
        Properties[name] = value;
        return value;
    }

    public bool HasProperty(string name) => Properties.ContainsKey(name);

    #endregion

    #region 关系与功能切面（Facet）

    public Facet Attach(
        string key,
        object value,
        int strength = 1,
        Func<Entity, IReadOnlyDictionary<string, object>?, bool>? when = null,
        object? source = null)
    {
        var facet = new Facet(key, value, strength, when, source);
        Facets.Add(facet);
        return facet;
    }

    public IEnumerable<Facet> Entries(string key, IReadOnlyDictionary<string, object>? context = null) =>
        Facets.Where(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase) &&
                          (f.When == null || (this is Entity e && f.When(e, context))));

    public Facet Relate(FacetType type, Entity target, int strength = 1, object? source = null)
    {
        var facet = new Facet(type, target, strength, source);
        Facets.Add(facet);
        return facet;
    }

    public Facet Relate(string kind, object target, int strength = 1, object? source = null)
    {
        var facet = new Facet(kind, target, strength, null, source);
        Facets.Add(facet);
        return facet;
    }

    public IEnumerable<Facet> Related(FacetType type, Entity? target = null) =>
        Facets.Where(f => f.Type == type && (target == null || ReferenceEquals(f.Value, target)));

    public IEnumerable<Facet> Related(string kind, object? target = null) =>
        Facets.Where(f => string.Equals(f.Key, kind, StringComparison.OrdinalIgnoreCase) &&
                          (target == null || Equals(f.Value, target)));

    public bool Detach(Facet facet) => Facets.Remove(facet);

    public IReadOnlyList<Facet> Detach(FacetType type, Entity? target = null, object? source = null)
    {
        var matched = Facets.Where(f =>
            f.Type == type &&
            (target == null || ReferenceEquals(f.Value, target)) &&
            (source == null || Equals(f.Source, source))).ToList();

        foreach (var facet in matched) Facets.Remove(facet);
        return matched;
    }

    public IReadOnlyList<Facet> Detach(string kind, object? value = null, object? source = null)
    {
        var matched = Facets.Where(f =>
            string.Equals(f.Key, kind, StringComparison.OrdinalIgnoreCase) &&
            (value == null || Equals(f.Value, value)) &&
            (source == null || Equals(f.Source, source))).ToList();

        foreach (var facet in matched) Facets.Remove(facet);
        return matched;
    }

    #endregion
}

/// <summary>
/// 运行时的世界实体。
/// </summary>
/// <remarks>
/// Entity 是世界引擎中唯一的运行时具体对象载体。
/// 人物（Character）、物理道具（Item）、场景（Scene）、知识（Knowledge）等均是 Entity。
/// 继承自 TreeMember，自动获得 Tags、Properties 与 Facets 核心容器与读写能力。
/// 领域系统高级方法（门禁、源质、认知等）统一通过 C# 静态扩展方法提供。
/// </remarks>
public class Entity : TreeMember
{
    public Entity(
        string name,
        TreeMember? parent = null,
        IDictionary<string, int>? tags = null,
        int position = 0,
        IDictionary<string, object>? properties = null,
        IEnumerable<Facet>? facets = null,
        IEnumerable<Enum>? enumTags = null)
        : base(name, parent, tags, position, properties, facets, enumTags)
    {
    }

    public Entity(
        string name,
        TreeMember? parent,
        IEnumerable<Enum> enumTags,
        int position = 0,
        IDictionary<string, object>? properties = null,
        IEnumerable<Facet>? facets = null)
        : base(name, parent, null, position, properties, facets, enumTags)
    {
    }

    public new Entity AddTags(params Enum[] tags)
    {
        base.AddTags(tags);
        return this;
    }

    #region 基础物品与装备生命周期操作

    public bool PickUp(TreeMember actor, int position = 0)
    {
        Reparent(actor, position);
        return true;
    }

    public bool Drop(int position = 0)
    {
        if (Parent?.Parent == null) return false;
        Reparent(Parent.Parent, position);
        SetTag("Held", 0);
        return true;
    }

    public bool PutInto(Entity container, int position = 0, Entity? fromScene = null)
    {
        SetTag("Held", 0);
        return container.Put(this, position, fromScene);
    }

    public bool TakeFrom(Entity container, TreeMember actor, int position = 0, Entity? fromScene = null)
    {
        return container.Take(this, actor, position, fromScene);
    }

    public bool Hold(TreeMember actor)
    {
        if (Parent != actor) Reparent(actor);
        SetTag("Held", 1);
        return true;
    }

    public bool Unhold()
    {
        SetTag("Held", 0);
        return true;
    }

    public Dictionary<string, object?> Examine(Entity? observer = null)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = Name,
            ["tags"] = new Dictionary<string, int>(Tags),
            ["properties"] = new Dictionary<string, object>(Properties),
            ["aspects"] = this.GetAspects(target: null, currentTime: null).ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            ["claims"] = Entries("Claim").Select(f => f.Value).ToList(),
            ["opened"] = Get<bool?>("opened"),
            ["items"] = Items.OfType<Entity>().Select(i => i.Name).ToList()
        };
    }

    #endregion
}
