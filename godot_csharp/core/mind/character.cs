using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Core.Mind;

/// <summary>
/// 承担物理世界行动与感官承载的角色身体实体。
/// </summary>
/// <remarks>
/// Character 是实体体系中扮演物理承载者与行动执行者的具体特化。
/// 
/// 架构定位：
/// 1. 身心二元（Dual Ontology）：Character 表达肉体、位置、背包持有与物理交互，而 Soul 表达认知、信念与主观决策。两者通过双向 Facet 绑定。
/// 2. 空间归属：Character 挂载于场景实体（Scene）的世界树子节点中，通过向上溯源确定当前所在房间。
/// 3. 物品承载：挂载在 Character 下的非 Soul 子实体均视为角色的随身物品（Inventory）。
/// 4. 驱动解耦：单个 Character 可被多个 Soul 驱动（例如附身、双重意识），单个 Soul 也可支配多个身体。
/// </remarks>
public class Character : Entity
{
    /// <summary>与当前角色绑定的主要认知意识主体（首选支配 Soul）。</summary>
    public Soul? Mind => Souls.FirstOrDefault();

    /// <summary>当前与角色绑定的全部 Soul 实体列表。</summary>
    public IReadOnlyList<Soul> Souls =>
        Facets.Where(f => f.Type == FacetType.Soul && f.Value is Soul)
              .Select(f => (Soul)f.Value!)
              .ToList();

    /// <summary>
    /// 获取角色当前随身携带的全部物品（背包）。排除认知主体（Soul）。
    /// </summary>
    public IReadOnlyList<Entity> Inventory =>
        Children.Keys.OfType<Entity>().Where(e => !e.HasTag(CommonTag.Soul) && !e.HasTag("Soul")).ToList();

    /// <summary>
    /// 向上溯源获取角色当前所处的场景房间实体（带有 Scene 标签的祖先实体）。
    /// </summary>
    public Entity? CurrentScene
    {
        get
        {
            TreeMember? current = Parent;
            while (current != null)
            {
                if (current is Entity e && (e.HasTag(CommonTag.Scene) || e.HasTag("Scene")))
                {
                    return e;
                }
                current = current.Parent;
            }
            return null;
        }
    }

    /// <summary>
    /// 创建一个新的物理角色身体实体。
    /// </summary>
    /// <param name="name">角色调试名称或唯一称谓。</param>
    /// <param name="parent">所属场景或父节点。为 null 时表示脱离场景树游离状态。</param>
    /// <param name="tags">附加标签字典。系统会自动合并 Character=1 基础标签。</param>
    /// <param name="position">在父节点中的槽位序号。</param>
    public Character(
        string name,
        TreeMember? parent = null,
        IDictionary<string, int>? tags = null,
        int position = 0)
        : base(name, parent, MergeTags(tags), position: position)
    {
    }

    /// <summary>
    /// 合并基础 Character 标签与外部传入的扩展标签。
    /// </summary>
    /// <param name="tags">外部自定义标签映射。</param>
    /// <returns>合并后的标签字典。</returns>
    private static Dictionary<string, int> MergeTags(IDictionary<string, int>? tags)
    {
        var result = new Dictionary<string, int> { ["Character"] = 1 };
        if (tags != null)
        {
            foreach (var (k, v) in tags) result[k] = v;
        }
        return result;
    }

    /// <summary>
    /// 建立 Character 身体与 Soul 心智之间的双向对象引用关系。
    /// </summary>
    /// <param name="soul">待绑定的灵魂认知主体。</param>
    public void Bind(Soul soul)
    {
        if (!Souls.Contains(soul))
        {
            Relate(FacetType.Soul, soul);
        }
        if (!soul.Characters.Contains(this))
        {
            soul.Relate(FacetType.Character, this);
        }
    }

    /// <summary>
    /// 移除 Character 身体与 Soul 心智之间的双向绑定关系。
    /// </summary>
    /// <param name="soul">待解绑的灵魂认知主体。</param>
    public void Unbind(Soul soul)
    {
        Detach(FacetType.Soul, soul);
        soul.Detach(FacetType.Character, this);
    }
}
