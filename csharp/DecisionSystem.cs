namespace DualOntologyWorldEngine.Core;

public sealed record Decision(Effect Effect, int Score, Projection Basis);
public sealed record DecisionAssessment(Decision Decision, bool Forbidden, IReadOnlyList<string> Reasons)
{
    public KnowledgeRevision? Revision { get; init; }
}

/// <summary>
/// 候选、条件和偏好全部来自同一内部世界。客观可行性只在激活时检查。
/// Prefer 与 Require 提出候选并加减分；Forbid 禁止；Permit 只放行——它不提出候选、不加分，
/// 只在条件成立、动作相同且权重不低于禁令时解除那条禁令（“可以做”不等于“想做”）。
/// </summary>
public static partial class DecisionSystem
{
    public static Decision? Choose(ProjectionWorld world, Projection actor) => Assess(world,actor).FirstOrDefault(a=>!a.Forbidden)?.Decision;
    public static IReadOnlyList<DecisionAssessment> Assess(ProjectionWorld world, Projection actor)
    {
        if (world.Detached) return AssessLive(world, actor);
        if (!ReferenceEquals(actor.World, world)) throw new InvalidOperationException("决策主体不属于内部世界。");
        var view = SubjectRead.Capture(world);
        if (actor.SubjectId is not { } id || view.Originals.GetValueOrDefault(id) != actor) return [];
        return AssessLive(view.Graph.World, view.Get(actor)).Select(a => a with
        {
            Decision = a.Decision with { Effect = view.Original(a.Decision.Effect), Basis = view.Original(a.Decision.Basis) },
            Revision = view.Revision
        }).ToArray();
    }
    internal static IReadOnlyList<DecisionAssessment> AssessLive(ProjectionWorld world, Projection actor)
    {
        if (!ReferenceEquals(actor.World,world)) throw new InvalidOperationException("决策主体不属于内部世界。");
        if (!world.Contains(actor)) return [];
        var rules = world.Projections.SelectMany(p => p.Rules.Where(r => r.Acceptance == Truth.True).Select(r => (Basis: p, Rule: r))).ToArray();
        var candidates = new List<(Effect Effect, ActionContext Context, Projection Basis, int Social)>();
        foreach (var (basis, rule) in rules.Where(r => r.Rule.Mode is RuleMode.Prefer or RuleMode.Require))
        foreach (var context in Contexts(world, actor, rule))
            if (Ast.EvaluateLive(rule.Condition, context) == Truth.True)
            {
                var effect = Ast.Bind(rule.Effect, context);
                if (!candidates.Any(c => Ast.Equivalent(c.Effect, effect))) candidates.Add((effect, context, basis, 0));
            }
        foreach (var request in world.Projections.Where(p => p.Kind == ProjectionKind.Event && p.Procedure is not null && p.Tags.GetValueOrDefault(Tags.Responded) == 0))
        {
            var attitude = SocialSystem.Evaluate(world, actor, request);
            if (attitude.Stance != SocialStance.Accept) continue;
            var context = new ActionContext(actor, Scene: actor.Parent);
            // 未填充的目标槽位不能凭空构造；等待更明确的请求或主体自己的规则。
            if (Ast.Nouns(request.Procedure).OfType<Noun.Slot>().Any(n => context.Resolve(n) is null)) continue;
            var effect = Ast.Bind(request.Procedure!, context);
            if (!candidates.Any(c => Ast.Equivalent(c.Effect, effect))) candidates.Add((effect, context, request, attitude.Score));
        }

        var assessments = new List<DecisionAssessment>();
        foreach (var candidate in candidates)
        {
            var score = candidate.Social;
            var forbidden = false;
            var reasons = new List<string>();
            if(candidate.Social!=0)reasons.Add($"社交意愿 {candidate.Social:+0;-0;0}");
            var permit = rules.Where(r => r.Rule.Mode == RuleMode.Permit && Applies(world, actor, r.Rule, candidate.Effect))
                .Select(r => (r.Basis, Weight: r.Rule.Weight)).DefaultIfEmpty((Basis: null!, Weight: -1)).MaxBy(r => r.Weight);
            foreach (var (basis, rule) in rules)
            {
                if (rule.Mode is RuleMode.Permit or RuleMode.Infer) continue;
                if (rule.Mode == RuleMode.Forbid)
                {
                    if (Contexts(world, actor, rule).Any(context =>
                        Ast.EvaluateLive(rule.Condition, context) == Truth.True &&
                        ContainsForbidden(candidate.Effect, Ast.Bind(rule.Effect, context), candidate.Context)))
                    {
                        if (permit.Weight >= rule.Weight) reasons.Add($"{basis.Name}：禁止，但{permit.Basis.Name}放行");
                        else { forbidden = true; reasons.Add($"{basis.Name}：禁止"); }
                    }
                    continue;
                }
                if (Ast.Nouns(rule).OfType<Noun.Slot>().Any(n => candidate.Context.Resolve(n) is null) || Ast.EvaluateLive(rule.Condition, candidate.Context) != Truth.True) continue;
                var matches = Ast.Equivalent(Ast.Bind(rule.Effect, candidate.Context), candidate.Effect);
                if (matches) { score += rule.Weight; reasons.Add($"{basis.Name}：+{rule.Weight}"); }
                else if (rule.Mode == RuleMode.Require) { score -= rule.Weight; reasons.Add($"{basis.Name}：未满足要求 -{rule.Weight}"); }
            }
            assessments.Add(new(new(candidate.Effect,score,candidate.Basis),forbidden,reasons));
        }
        return assessments.OrderBy(a=>a.Forbidden).ThenByDescending(a=>a.Decision.Score).ToArray();
    }

    /// <summary>放行规则在某个上下文里条件成立、且它放行的正是这个候选动作。</summary>
    private static bool Applies(ProjectionWorld world, Projection actor, Rule rule, Effect candidate) =>
        Contexts(world, actor, rule).Any(context => Ast.EvaluateLive(rule.Condition, context) == Truth.True &&
            Ast.Equivalent(Ast.Bind(rule.Effect, context), candidate));

    // 禁令约束行为本身，等待和外层组合不能绕过；传话中的请求不等于已经执行。
    private static bool ContainsForbidden(Effect candidate, Effect forbidden, ActionContext context)
    {
        if (forbidden is Effect.Wait wait) return ContainsForbidden(candidate, wait.Body, context);
        if (Ast.Equivalent(candidate, forbidden)) return true;
        return candidate switch
        {
            Effect.Wait delayed => ContainsForbidden(delayed.Body, forbidden, context),
            Effect.Sequence sequence => sequence.Items.Any(item => ContainsForbidden(item, forbidden, context)),
            Effect.When conditional => Ast.EvaluateLive(conditional.Condition, context) != Truth.False &&
                ContainsForbidden(conditional.Body, forbidden, context),
            _ => false
        };
    }

    private static IEnumerable<ActionContext> Contexts(ProjectionWorld world, Projection actor, Rule rule)
    {
        var slots = Ast.Nouns(rule).OfType<Noun.Slot>().Select(n => n.Role).ToHashSet();
        IEnumerable<Projection?> targets = slots.Contains(RuleSlot.Target) ? world.Projections : new Projection?[] { null };
        IEnumerable<Projection?> tools = slots.Contains(RuleSlot.Tool) ? world.Projections : new Projection?[] { null };
        foreach (var target in targets)
        foreach (var tool in tools)
        {
            var context = new ActionContext(actor, target, tool, actor.Parent);
            if (slots.All(slot => context.Resolve(new Noun.Slot(slot)) is not null)) yield return context;
        }
    }
}
