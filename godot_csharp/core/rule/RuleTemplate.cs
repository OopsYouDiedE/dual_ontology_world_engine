using DualOntologyWorldEngine.Core.Mind;

namespace DualOntologyWorldEngine.Core.Rules;

/// <summary>可直接填入场景对象的常见规则套路与候选生成器。</summary>
/// <remarks>
/// 套路只组合 <see cref="Rule"/>、<see cref="Expr"/> 与 <see cref="Effect"/>，不建立平行规则模型。
/// 固定人物、设施和场景直接保存对象引用；随行动变化的位置使用 <see cref="RuleSlot"/>。
/// 候选生成只提出结构正确的规则，不负责发布、说服、日志或 UI。
/// </remarks>
public static class RuleTemplate
{
    /// <summary>建立“条件成立时禁止行动”的基础套路。</summary>
    public static Rule ForbidWhen(Effect action, Expr condition, string source) =>
        condition.Then(source, action.Forbid());

    /// <summary>建立“条件成立时下一步应当执行”的强制套路。</summary>
    public static Rule RequireNext(Effect action, Expr condition, string source) =>
        condition.Then(source, action.Require());

    /// <summary>建立“条件成立时优先行动”的倡导套路。</summary>
    public static Rule PreferWhen(Effect action, Expr condition, string source) =>
        condition.Then(source, action.Prefer());

    /// <summary>建立危险对象的见证套路：当行动者独处且目标危险时，禁止执行指定操作。</summary>
    public static Rule RequireWitness(Entity target, string operation = "operate") =>
        ForbidWhen(
            RuleSlot.Actor.Does(operation, target),
            RuleSlot.Actor.IsState("Alone").And(target.HasAtLeast("Danger")),
            $"操作《{target.Name}》时必须有见证人在场");

    /// <summary>建立证据前置套路：没有针对目标的已确认材料时，禁止指控。</summary>
    public static Rule RequireEvidenceBefore(string operation = "accuse") =>
        ForbidWhen(
            RuleSlot.Actor.Does(operation, RuleSlot.Target),
            AST.E("evidence_for", RuleSlot.Actor, RuleSlot.Target).Not(),
            "公开指控必须以已确认材料为前提");

    /// <summary>建立检查前置套路：没有形成检查事实时，禁止操作固定设施。</summary>
    public static Rule RequireInspectionBefore(Entity facility, string operation = "operate") =>
        ForbidWhen(
            RuleSlot.Actor.Does(operation, facility),
            AST.E("inspected", RuleSlot.Actor, facility).Not(),
            $"操作《{facility.Name}》前必须完成检查");

    /// <summary>建立作用域套路：行动者不在指定场景时，禁止对目标执行操作。</summary>
    public static Rule RestrictToScene(Entity target, Entity scene, string operation) =>
        ForbidWhen(
            RuleSlot.Actor.Does(operation, target),
            RuleSlot.Actor.IsIn(scene).Not(),
            $"只能在《{scene.Name}》内对《{target.Name}》执行 {operation}");

    /// <summary>建立所有权套路：行动者并非物品所有者时，禁止使用该物品。</summary>
    public static Rule RequireOwnership(Entity item, string operation = "use") =>
        ForbidWhen(
            RuleSlot.Actor.Does(operation, item),
            AST.E("related", item, FacetType.Owner.ToString(), RuleSlot.Actor).Not(),
            $"只有《{item.Name}》的所有者可以执行 {operation}");

    /// <summary>建立工具角色套路：工具不具备所需功能角色时，禁止执行操作。</summary>
    public static Rule RequireToolRole(Role role, string operation) =>
        ForbidWhen(
            AST.F(operation, RuleSlot.Actor, RuleSlot.Target, RuleSlot.Tool),
            RuleSlot.Tool.HasAtLeast(role).Not(),
            $"执行 {operation} 必须使用具备 {role} 角色的工具");

    /// <summary>根据人物观察、已接受信念和源质倾向生成适合当前场景的规则候选。</summary>
    /// <remarks>
    /// 返回 ValueTuple 以保持调用轻量。排序固定为亲和度降序、名称序，保证同分结果稳定。
    /// 不适用的套路返回空序列，这是正常场景状态，不抛出异常。
    /// </remarks>
    public static IReadOnlyList<(Rule Rule, string Name, Aspect Guarantee, double BaseAffinity)> SampleCandidateRules(
        Soul soul,
        Entity actor,
        RuleEnvironment env,
        IReadOnlyList<Entity> existingPublications)
    {
        return Candidates()
            .Where(candidate => !existingPublications.ContainsEquivalentRule(candidate.Rule))
            .OrderByDescending(candidate => candidate.BaseAffinity)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToList();

        IEnumerable<(Rule Rule, string Name, Aspect Guarantee, double BaseAffinity)> Candidates()
        {
            foreach (var observation in soul.Observations)
            {
                if (!string.Equals(observation.Op, "observed_anomaly", StringComparison.OrdinalIgnoreCase) ||
                    observation.Args is not [Entity risk, ..])
                {
                    continue;
                }

                var guarantee = actor.Tag(Aspect.Law) >= actor.Tag(Aspect.Preservation)
                    ? Aspect.Law
                    : Aspect.Preservation;
                var rule = RequireWitness(risk, "inspect_facility");
                yield return (
                    rule,
                    $"《{risk.Name}》双人见证检查规程",
                    guarantee,
                    12d + actor.Tag(Aspect.Law) * 3d + actor.Tag(Aspect.Preservation) * 2d);
            }

            if (soul.Observations.Count > 0 || soul.Beliefs.Any(belief => belief.Has("Received")))
            {
                var guarantee = actor.Tag(Aspect.Vestige) >= actor.Tag(Aspect.Mind)
                    ? Aspect.Vestige
                    : Aspect.Mind;
                var rule = PreferWhen(
                    RuleSlot.Actor.Does("verify_demon_words", RuleSlot.Target),
                    AST.E("true"),
                    "外部关键陈述应优先独立核验");
                yield return (
                    rule,
                    "涉外部言论独立查验准则",
                    guarantee,
                    10d + actor.Tag(Aspect.Mind) * 3d + actor.Tag(Aspect.Vestige) * 2d);
            }

            var orderAptitude = actor.Tag(Aspect.Law) * 2 + actor.Tag(Aspect.Mind);
            if (orderAptitude >= 3)
            {
                yield return (
                    RequireEvidenceBefore("accuse_without_evidence"),
                    "公开指控证据前置公约",
                    Aspect.Law,
                    11d + orderAptitude * 2d);
            }
        }
    }
}
