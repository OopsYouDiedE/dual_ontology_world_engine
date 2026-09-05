using System;
using System.Collections.Generic;
using System.Linq;

namespace DualOntologyWorldEngine.Core;

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

