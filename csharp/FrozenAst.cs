using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

/// <summary>Closed AST conversion. No reflection, callbacks or name-based reference recovery.</summary>
public static class CognitionAst
{
    // These walks consume ASTs already checked by Thaw at the input boundary.
    // Dependency discovery and forgetting need handles, not a second object graph.
    internal static IEnumerable<SubjectProjectionId> References(FrozenRuleV1 rule)
    {
        var ids = new List<SubjectProjectionId>();
        Collect(rule.Condition, ids); Collect(rule.Effect, ids);
        return ids;
    }
    internal static IEnumerable<SubjectProjectionId> References(FrozenEffectV1 effect)
    {
        var ids = new List<SubjectProjectionId>(); Collect(effect, ids); return ids;
    }
    private static void Collect(FrozenNounV1 noun, List<SubjectProjectionId> ids)
    {
        if (noun is FrozenNounV1.Reference reference) ids.Add(reference.Target);
    }
    private static void Collect(FrozenConditionV1 condition, List<SubjectProjectionId> ids)
    {
        switch (condition)
        {
            case FrozenConditionV1.Exists n: Collect(n.Target, ids); break;
            case FrozenConditionV1.Tagged n: Collect(n.Target, ids); break;
            case FrozenConditionV1.Related n: Collect(n.Target, ids); Collect(n.Other, ids); break;
            case FrozenConditionV1.At n: Collect(n.Target, ids); Collect(n.Space, ids); break;
            case FrozenConditionV1.IsKind n: Collect(n.Target, ids); break;
            case FrozenConditionV1.Not n: Collect(n.Operand, ids); break;
            case FrozenConditionV1.All n: foreach (var child in n.Items) Collect(child, ids); break;
            case FrozenConditionV1.Any n: foreach (var child in n.Items) Collect(child, ids); break;
        }
    }
    private static void Collect(FrozenEffectV1 effect, List<SubjectProjectionId> ids)
    {
        switch (effect)
        {
            case FrozenEffectV1.Act n: Collect(n.Target, ids); if (n.Tool is { } tool) Collect(tool, ids); break;
            case FrozenEffectV1.Set n: Collect(n.Target, ids); break;
            case FrozenEffectV1.Change n: Collect(n.Target, ids); break;
            case FrozenEffectV1.TemporarySet n: Collect(n.Target, ids); break;
            case FrozenEffectV1.TemporarySetUntilEnd n: Collect(n.Target, ids); break;
            case FrozenEffectV1.Spend n: Collect(n.Target, ids); break;
            case FrozenEffectV1.Relate n: Collect(n.Target, ids); Collect(n.Other, ids); break;
            case FrozenEffectV1.Sequence n: foreach (var child in n.Items) Collect(child, ids); break;
            case FrozenEffectV1.When n: Collect(n.Condition, ids); Collect(n.Body, ids); break;
            case FrozenEffectV1.Require n: Collect(n.Condition, ids); break;
            case FrozenEffectV1.Wait n: Collect(n.Body, ids); if (n.Until is { } until) Collect(until, ids); break;
            case FrozenEffectV1.Say n: Collect(n.Recipient, ids); if (n.Request is { } request) Collect(request, ids); if (n.Claim is { } claim) Collect(claim, ids); break;
            case FrozenEffectV1.Think n: Collect(n.Fact, ids); break;
        }
    }

    internal static T Defined<T>(T value) where T : struct, Enum { Contract.Enum(value, "ast.enum"); return value; }
    private static AuthorTermId? Topic(AuthorTermId? topic) { if (topic is { } t) Contract.Identity(t.Value, "ast.topic"); return topic; }
    private static void Node(object? node, int depth)
    {
        Contract.Require(node is not null, ContractErrorCode.MalformedData, "ast", "AST 节点不能为空。");
        Contract.Require(depth <= 256, ContractErrorCode.LimitExceeded, "ast", "AST 深度超限。");
    }
    private static T Unsupported<T>() => throw new CognitionContractException(new(ContractErrorCode.Unsupported, "ast", "不支持的 AST 节点。", CandidateIds: []));
    private static ImmutableArray<B> Items<A, B>(ImmutableArray<A> items, Func<A, B> map)
    {
        Contract.Array(items, "ast.items");
        return [..items.Select(map)];
    }
    private static Tag Tag(Tag tag) { Contract.Identity(tag.Name, "ast.tag"); return tag; }
    private static RelationTag Relation(RelationTag tag) { Contract.Identity(tag.Name, "ast.relation"); return tag; }
    private static double Delay(double n) { Contract.Number(n, "ast.delay", true); return n; }
    private static string Text(string s) { Contract.Text(s, "ast.text"); return s; }
    private static int Spend(int n) { Contract.Require(n >= 0, ContractErrorCode.MalformedData, "ast.amount", "支付量不能为负。"); return n; }

    internal static FrozenNounV1 Freeze(Noun noun, Func<Projection, SubjectProjectionId> id, int d = 0)
    {
        Node(noun, d);
        return noun switch
        {
            Noun.Reference n => new FrozenNounV1.Reference(id(n.Projection)),
            Noun.Slot n => new FrozenNounV1.Slot(Defined(n.Role)),
            _ => Unsupported<FrozenNounV1>()
        };
    }
    internal static FrozenConditionV1 Freeze(Condition c, Func<Projection, SubjectProjectionId> id, int d = 0)
    {
        Node(c, d);
        FrozenNounV1 N(Noun n) => Freeze(n, id, d + 1);
        FrozenConditionV1 C(Condition n) => Freeze(n, id, d + 1);
        return c switch
        {
            Condition.Constant n => new FrozenConditionV1.Constant(Defined(n.Value)),
            Condition.Exists n => new FrozenConditionV1.Exists(N(n.Target)),
            Condition.Tagged n => new FrozenConditionV1.Tagged(N(n.Target), Tag(n.Tag), n.Minimum),
            Condition.Related n => new FrozenConditionV1.Related(N(n.Target), Relation(n.Relation), N(n.Other)),
            Condition.At n => new FrozenConditionV1.At(N(n.Target), N(n.Space)),
            Condition.IsKind n => new FrozenConditionV1.IsKind(N(n.Target), Defined(n.Kind)),
            Condition.Not n => new FrozenConditionV1.Not(C(n.Operand)),
            Condition.All n => new FrozenConditionV1.All(Items(n.Items, C)),
            Condition.Any n => new FrozenConditionV1.Any(Items(n.Items, C)),
            _ => Unsupported<FrozenConditionV1>()
        };
    }
    internal static FrozenEffectV1 Freeze(Effect e, Func<Projection, SubjectProjectionId> id, int d = 0)
    {
        Node(e, d);
        FrozenNounV1 N(Noun n) => Freeze(n, id, d + 1);
        FrozenConditionV1 C(Condition n) => Freeze(n, id, d + 1);
        FrozenEffectV1 E(Effect n) => Freeze(n, id, d + 1);
        return e switch
        {
            Effect.Act n => new FrozenEffectV1.Act(Defined(n.Operation), N(n.Target), n.Tool is null ? null : N(n.Tool)),
            Effect.Set n => new FrozenEffectV1.Set(N(n.Target), Tag(n.Tag), n.Value),
            Effect.Change n => new FrozenEffectV1.Change(N(n.Target), Tag(n.Tag), n.Amount),
            Effect.TemporarySet n => new FrozenEffectV1.TemporarySet(N(n.Target), Tag(n.Tag), n.Value, Delay(n.Duration)),
            Effect.TemporarySetUntilEnd n => new FrozenEffectV1.TemporarySetUntilEnd(N(n.Target), Tag(n.Tag), n.Value),
            Effect.Spend n => new FrozenEffectV1.Spend(N(n.Target), Tag(n.Tag), Spend(n.Amount)),
            Effect.Relate n => new FrozenEffectV1.Relate(N(n.Target), Relation(n.Relation), N(n.Other), n.Remove),
            Effect.Sequence n => new FrozenEffectV1.Sequence(Items(n.Items, E)),
            Effect.When n => new FrozenEffectV1.When(C(n.Condition), E(n.Body)),
            Effect.Require n => new FrozenEffectV1.Require(C(n.Condition)),
            Effect.Wait n => new FrozenEffectV1.Wait(Delay(n.Delay), E(n.Body), n.Until is null ? null : C(n.Until)),
            Effect.Say n => new FrozenEffectV1.Say(N(n.Recipient), Text(n.Text), n.Request is null ? null : E(n.Request),
                n.Claim is null ? null : C(n.Claim), Topic(n.Topic)),
            Effect.Think n => new FrozenEffectV1.Think(C(n.Fact)),
            _ => Unsupported<FrozenEffectV1>()
        };
    }
    internal static FrozenRuleV1 Freeze(Rule r, Func<Projection, SubjectProjectionId> id)
    {
        Node(r, 0);
        ValidateLifetime(r.Lifetime, r.LifetimeOwner);
        var effect = Freeze(r.Effect, id);
        ValidateInference(r.Mode, effect);
        return new(Freeze(r.Condition, id), Defined(r.Mode), effect, r.Weight, Defined(r.Acceptance), r.Lifetime, r.LifetimeOwner);
    }
    private static void ValidateInference(RuleMode mode, FrozenEffectV1 effect)
    {
        if (mode != RuleMode.Infer) return;
        while (effect is FrozenEffectV1.When when) effect = when.Body;
        Contract.Require(effect is FrozenEffectV1.Think, ContractErrorCode.MalformedData,
            "infer_effect", "推知规则只接受 Think 或嵌套 When/Think，不是可执行行动。");
    }

    internal static Noun Thaw(FrozenNounV1 n, IReadOnlyDictionary<SubjectProjectionId, Projection> map, int d = 0)
    {
        Node(n, d);
        if (n is FrozenNounV1.Reference r)
        {
            Contract.Id(r.Target, r.Target.SubjectWorld, "ast.reference");
            Contract.Require(map.TryGetValue(r.Target, out var p), ContractErrorCode.InvalidReference, "ast.reference", "AST 引用不在当前主体图。");
            return new Noun.Reference(p!);
        }
        return n is FrozenNounV1.Slot s ? new Noun.Slot(Defined(s.Role)) : Unsupported<Noun>();
    }
    internal static Condition Thaw(FrozenConditionV1 c, IReadOnlyDictionary<SubjectProjectionId, Projection> map, int d = 0)
    {
        Node(c, d);
        Noun N(FrozenNounV1 n) => Thaw(n, map, d + 1);
        Condition C(FrozenConditionV1 n) => Thaw(n, map, d + 1);
        return c switch
        {
            FrozenConditionV1.Constant n => new Condition.Constant(Defined(n.Value)),
            FrozenConditionV1.Exists n => new Condition.Exists(N(n.Target)),
            FrozenConditionV1.Tagged n => new Condition.Tagged(N(n.Target), Tag(n.Tag), n.Minimum),
            FrozenConditionV1.Related n => new Condition.Related(N(n.Target), Relation(n.Relation), N(n.Other)),
            FrozenConditionV1.At n => new Condition.At(N(n.Target), N(n.Space)),
            FrozenConditionV1.IsKind n => new Condition.IsKind(N(n.Target), Defined(n.Kind)),
            FrozenConditionV1.Not n => new Condition.Not(C(n.Operand)),
            FrozenConditionV1.All n => new Condition.All(Items(n.Items, C)),
            FrozenConditionV1.Any n => new Condition.Any(Items(n.Items, C)),
            _ => Unsupported<Condition>()
        };
    }
    internal static Effect Thaw(FrozenEffectV1 e, IReadOnlyDictionary<SubjectProjectionId, Projection> map, int d = 0)
    {
        Node(e, d);
        Noun N(FrozenNounV1 n) => Thaw(n, map, d + 1);
        Condition C(FrozenConditionV1 n) => Thaw(n, map, d + 1);
        Effect E(FrozenEffectV1 n) => Thaw(n, map, d + 1);
        return e switch
        {
            FrozenEffectV1.Act n => new Effect.Act(Defined(n.Operation), N(n.Target), n.Tool is null ? null : N(n.Tool)),
            FrozenEffectV1.Set n => new Effect.Set(N(n.Target), Tag(n.Tag), n.Value),
            FrozenEffectV1.Change n => new Effect.Change(N(n.Target), Tag(n.Tag), n.Amount),
            FrozenEffectV1.TemporarySet n => new Effect.TemporarySet(N(n.Target), Tag(n.Tag), n.Value, Delay(n.Duration)),
            FrozenEffectV1.TemporarySetUntilEnd n => new Effect.TemporarySetUntilEnd(N(n.Target), Tag(n.Tag), n.Value),
            FrozenEffectV1.Spend n => new Effect.Spend(N(n.Target), Tag(n.Tag), Spend(n.Amount)),
            FrozenEffectV1.Relate n => new Effect.Relate(N(n.Target), Relation(n.Relation), N(n.Other), n.Remove),
            FrozenEffectV1.Sequence n => new Effect.Sequence(Items(n.Items, E)),
            FrozenEffectV1.When n => new Effect.When(C(n.Condition), E(n.Body)),
            FrozenEffectV1.Require n => new Effect.Require(C(n.Condition)),
            FrozenEffectV1.Wait n => new Effect.Wait(Delay(n.Delay), E(n.Body), n.Until is null ? null : C(n.Until)),
            FrozenEffectV1.Say n => new Effect.Say(N(n.Recipient), Text(n.Text), n.Request is null ? null : E(n.Request),
                n.Claim is null ? null : C(n.Claim), Topic(n.Topic)),
            FrozenEffectV1.Think n => new Effect.Think(C(n.Fact)),
            _ => Unsupported<Effect>()
        };
    }
    internal static Rule Thaw(FrozenRuleV1 r, IReadOnlyDictionary<SubjectProjectionId, Projection> map)
    {
        Node(r, 0);
        ValidateInference(r.Mode, r.Effect);
        ValidateLifetime(r.Lifetime, r.LifetimeOwner);
        return new(Thaw(r.Condition, map), Defined(r.Mode), Thaw(r.Effect, map), r.Weight, Defined(r.Acceptance), r.Lifetime, r.LifetimeOwner);
    }
    private static void ValidateLifetime(LifetimeScope lifetime, string? owner)
    {
        Defined(lifetime);
        if (owner is not null) Contract.Identity(owner, "ast.lifetime_owner");
        Contract.Require(lifetime == LifetimeScope.World || owner is not null, ContractErrorCode.MalformedData,
            "ast.lifetime_owner", "局部规则必须声明有效期所属范围。");
    }
    internal static void ValidateMap(ImmutableDictionary<SubjectProjectionId, Projection> map)
    {
        Contract.Require(map is not null, ContractErrorCode.MalformedData, "map", "重绑定映射不能为空。");
        ProjectionWorld? world = null;
        foreach (var (id, p) in map!)
        {
            Contract.Require(p is not null && p.SubjectId == id, ContractErrorCode.InvalidReference, "map", "映射键与投影句柄不符。");
            world ??= p!.World;
            Contract.Id(id, world.SubjectWorldId ?? Guid.Empty, "map");
            Contract.Require(ReferenceEquals(p!.World, world) && world.Contains(p), ContractErrorCode.InvalidReference, "map", "映射须属于同一个实际主体世界。");
        }
    }
    public static ContractResult<Condition> RebindCondition(FrozenConditionV1 c, ImmutableDictionary<SubjectProjectionId, Projection> map)
        => Contract.Try(() => { ValidateMap(map); return Thaw(c, map); });
    public static ContractResult<Effect> RebindEffect(FrozenEffectV1 e, ImmutableDictionary<SubjectProjectionId, Projection> map)
        => Contract.Try(() => { ValidateMap(map); return Thaw(e, map); });
    public static ContractResult<Rule> RebindRule(FrozenRuleV1 r, ImmutableDictionary<SubjectProjectionId, Projection> map)
        => Contract.Try(() => { ValidateMap(map); return Thaw(r, map); });
    private static SubjectProjectionId Reference(Projection p, ImmutableDictionary<SubjectProjectionId, Projection> map)
    {
        Contract.Require(p is not null && p.SubjectId is { } id && map.GetValueOrDefault(id) == p,
            ContractErrorCode.InvalidReference, "reference", "AST 引用与显式映射不符。");
        return p!.SubjectId!.Value;
    }
    public static ContractResult<FrozenConditionV1> FreezeCondition(Condition c, ImmutableDictionary<SubjectProjectionId, Projection> map)
        => Contract.Try(() => { ValidateMap(map); return Freeze(c, p => Reference(p, map)); });
    public static ContractResult<FrozenEffectV1> FreezeEffect(Effect e, ImmutableDictionary<SubjectProjectionId, Projection> map)
        => Contract.Try(() => { ValidateMap(map); return Freeze(e, p => Reference(p, map)); });
    public static ContractResult<FrozenRuleV1> FreezeRule(Rule r, ImmutableDictionary<SubjectProjectionId, Projection> map)
        => Contract.Try(() => { ValidateMap(map); return Freeze(r, p => Reference(p, map)); });
}

public static partial class Ast
{
    public static ContractResult<SnapshotTruthV1> Evaluate(CognitionSnapshotV1 snapshot, FrozenConditionV1 condition, SnapshotActionContextV1 context)
        => Contract.Try(() =>
        {
            var graph = SnapshotGraph.Build(snapshot);
            Projection? Get(SubjectProjectionId? id)
            {
                if (id is null) return null;
                Contract.Id(id.Value, snapshot.SubjectWorld, "context");
                return graph.Projections.TryGetValue(id.Value, out var p) ? p
                    : throw new CognitionContractException(new(ContractErrorCode.InvalidReference, "context", "上下文句柄不在主体图。", CandidateIds: []));
            }
            Contract.Require(context is not null, ContractErrorCode.MalformedData, "context", "上下文不能为空。");
            return new SnapshotTruthV1(snapshot.Revision, EvaluateLive(CognitionAst.Thaw(condition, graph.Projections),
                new(Get(context!.Actor)!, Get(context.Target), Get(context.Tool), Get(context.Scene))));
        });
    public static ContractResult<bool> Equivalent(FrozenConditionV1 left, FrozenConditionV1 right)
        => Contract.Try(() =>
        {
            var map = new ReferenceComparisonMap();
            var a = CognitionAst.Thaw(left, map); map.ResetDomain();
            return Equivalent(a, CognitionAst.Thaw(right, map));
        });
    public static ContractResult<bool> Equivalent(FrozenEffectV1 left, FrozenEffectV1 right)
        => Contract.Try(() =>
        {
            var map = new ReferenceComparisonMap();
            var a = CognitionAst.Thaw(left, map); map.ResetDomain();
            return Equivalent(a, CognitionAst.Thaw(right, map));
        });
    private sealed class ReferenceComparisonMap : IReadOnlyDictionary<SubjectProjectionId, Projection>
    {
        private readonly Dictionary<SubjectProjectionId, Projection> nodes = [];
        private Guid? domain;
        internal void ResetDomain() => domain = null;
        public bool TryGetValue(SubjectProjectionId id, out Projection value)
        {
            Contract.Id(id, id.SubjectWorld, "ast.reference");
            domain ??= id.SubjectWorld;
            Contract.Require(domain == id.SubjectWorld, ContractErrorCode.InvalidReference, "ast.reference", "一棵 AST 不能混用主体域。");
            if (!nodes.TryGetValue(id, out value!)) nodes[id] = value = new Projection(new ProjectionWorld(id.SubjectWorld), "", ProjectionKind.Object, id);
            return true;
        }
        public Projection this[SubjectProjectionId id] { get { TryGetValue(id, out var p); return p; } }
        public IEnumerable<SubjectProjectionId> Keys => nodes.Keys;
        public IEnumerable<Projection> Values => nodes.Values;
        public int Count => nodes.Count;
        public bool ContainsKey(SubjectProjectionId id) => nodes.ContainsKey(id);
        public IEnumerator<KeyValuePair<SubjectProjectionId, Projection>> GetEnumerator() => nodes.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
