using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core.Simulation;

/// <summary>对象发布可交互能力；同一工厂函数可挂在多个对象上，无需逐个角色编写分支。</summary>
public delegate IEnumerable<Intent> AffordanceFactory(Entity target, Soul mind, Entity actor,
    RuleEnvironment environment, IReadOnlyDictionary<string, object>? context);

public static class Affordances
{
    public static Facet Offer(this Entity target, AffordanceFactory factory, object? source = null) =>
        target.Attach("Affordance", factory, source: source);

    /// <summary>
    /// 把感知范围内的对象能力接到现有 Soul 候选管线。范围由游戏决定，不默认读取整个世界。
    /// 游戏应在范围查询中排除不可见对象；每个 Intent 在执行前仍须复核资源和访问条件。
    /// </summary>
    public static Facet Discover(this Soul soul, Func<Entity, IEnumerable<Entity>> perceived) =>
        soul.InstallStep((mind, actor, env, context) =>
            perceived(actor).Distinct().ToArray().SelectMany(target =>
                target.Entries("Affordance", context).ToArray()
                    .SelectMany(facet => facet.Value is AffordanceFactory factory
                        ? factory(target, mind, actor, env, context) : [])), source: perceived);
}
