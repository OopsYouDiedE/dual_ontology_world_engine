using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core.Kotodama;

/// <summary>仪式或言灵中单个槽位的约束规格。</summary>
public readonly record struct SlotSpec(
    bool Optional = false,
    bool Many = false,
    IReadOnlyDictionary<string, int>? Tags = null,
    bool Held = false,
    string Holder = "holder",
    string? Relation = null,
    string Target = "target");

/// <summary>言灵、仪式配方与超自然契约核心装配工厂。</summary>
/// <remarks>
/// 负责构建具有规范结构的实体：
/// 1. 仪式配方（Recipe）：定义槽位要求（slots）、源质门槛（thresholds）、支付代价（costs）与产出效果（effects）；
/// 2. 言灵咏唱（WordSpirit/Incantation）：轻量即时触发的超自然干涉原语；
/// 3. 源质聚合（<see cref="AggregateAspects"/>）：在仪式判定时统一汇总所有投入实体的源质投影总和。
/// </remarks>
public static class KotodamaFactory
{
    /// <summary>装配创建一份通用仪式配方或契约模板实体。</summary>
    /// <param name="name">配方或契约名称。</param>
    /// <param name="slots">槽位规格定义字典（角色、数量、标签与从属要求）。</param>
    /// <param name="thresholds">激活所需的各项源质强度门槛映射。</param>
    /// <param name="requirements">外部前置条件表达式或判定谓词序列。</param>
    /// <param name="costs">执行该配方必须支付的代价效果列表。</param>
    /// <param name="effects">仪式完成时施加的世界效果列表。</param>
    /// <param name="triggers">契约被动触发的条件规则集合。</param>
    /// <param name="constraints">契约施加的不可违抗硬性约束规则集合（HardConstraint）。</param>
    /// <param name="obligations">契约规定的主体遵从义务规则集合（Obligation）。</param>
    /// <param name="breach">触犯义务违约时自动触发的惩戒效果列表。</param>
    /// <param name="exceptions">免责豁免条件表达式列表。</param>
    /// <param name="parent">所属世界树节点。</param>
    /// <param name="tags">附加实体标签字典。</param>
    /// <param name="properties">附加客观属性字典。</param>
    /// <returns>标记为 Recipe=1 的仪式配方实体。</returns>
    public static Entity CreateRecipe(
        string name,
        IReadOnlyDictionary<string, SlotSpec>? slots = null,
        IReadOnlyDictionary<string, int>? thresholds = null,
        IEnumerable<object>? requirements = null,
        IEnumerable<Effect>? costs = null,
        IEnumerable<Effect>? effects = null,
        IEnumerable<Rule>? triggers = null,
        IEnumerable<Rule>? constraints = null,
        IEnumerable<Rule>? obligations = null,
        IEnumerable<Effect>? breach = null,
        IEnumerable<Expr>? exceptions = null,
        TreeMember? parent = null,
        IDictionary<string, int>? tags = null,
        IDictionary<string, object>? properties = null)
    {
        var recipeTags = new Dictionary<string, int> { ["Recipe"] = 1 };
        if (tags != null)
        {
            foreach (var (k, v) in tags) recipeTags[k] = v;
        }

        var props = new Dictionary<string, object>
        {
            ["slots"] = slots != null ? new Dictionary<string, SlotSpec>(slots) : new(),
            ["thresholds"] = thresholds != null ? new Dictionary<string, int>(thresholds) : new()
        };
        if (properties != null)
        {
            foreach (var (k, v) in properties) props[k] = v;
        }

        var recipe = new Entity(name, parent, recipeTags, properties: props);

        if (requirements != null) foreach (var r in requirements) recipe.Attach(FacetType.Requirement, r);
        if (costs != null) foreach (var c in costs) recipe.Attach(FacetType.Cost, c);
        if (effects != null) foreach (var e in effects) recipe.Attach(FacetType.Effect, e);
        if (triggers != null) foreach (var t in triggers) recipe.Attach(FacetType.Trigger, t);
        if (constraints != null) foreach (var hc in constraints) recipe.Attach(FacetType.HardConstraint, hc);
        if (obligations != null) foreach (var o in obligations) recipe.Attach(FacetType.Obligation, o);
        if (breach != null) foreach (var b in breach) recipe.Attach(FacetType.Breach, b);
        if (exceptions != null) foreach (var ex in exceptions) recipe.Attach(FacetType.Exception, ex);

        return recipe;
    }

    /// <summary>快捷装配创建即时施展的言灵咏唱实体（Kotodama/Incantation）。</summary>
    /// <param name="name">言灵称谓或祝词名称。</param>
    /// <param name="slots">施展时所需的素材或介质槽位。</param>
    /// <param name="thresholds">发动言灵所需的源质强度门槛。</param>
    /// <param name="requirements">发动前置环境条件。</param>
    /// <param name="cost">单次施放消耗的代价效果。</param>
    /// <param name="effects">施放成功时的干涉效果列表。</param>
    /// <param name="activation">生效触发规则。</param>
    /// <param name="constraints">施展期间的硬性规约。</param>
    /// <param name="tags">附加标签字典。</param>
    /// <returns>标记为 Incantation=1 与 Kotodama=1 的言灵实体。</returns>
    public static Entity CreateWordSpirit(
        string name = "未命名言灵",
        IReadOnlyDictionary<string, SlotSpec>? slots = null,
        IReadOnlyDictionary<string, int>? thresholds = null,
        IEnumerable<object>? requirements = null,
        Effect? cost = null,
        IEnumerable<Effect>? effects = null,
        IEnumerable<Rule>? activation = null,
        IEnumerable<Rule>? constraints = null,
        IDictionary<string, int>? tags = null)
    {
        var wordTags = new Dictionary<string, int> { ["Incantation"] = 1, ["Kotodama"] = 1 };
        if (tags != null)
        {
            foreach (var (k, v) in tags) wordTags[k] = v;
        }

        var costList = cost != null && !string.Equals(cost.Op, "none", StringComparison.OrdinalIgnoreCase)
            ? new[] { cost }
            : Enumerable.Empty<Effect>();

        return CreateRecipe(
            name,
            slots: slots,
            thresholds: thresholds,
            requirements: requirements,
            costs: costList,
            effects: effects,
            triggers: activation,
            constraints: constraints,
            tags: wordTags);
    }

    /// <summary>汇总折算一组输入材料与相关实体所贡献的有效源质（Aspect）强度总和。</summary>
    /// <param name="inputs">投入仪式或言灵的实体集合。</param>
    /// <param name="target">定向关系投影的目标客体。</param>
    /// <param name="context">附加上下文环境。</param>
    /// <returns>源质名称到累加总强度的映射字典。</returns>
    public static Dictionary<string, int> AggregateAspects(
        IEnumerable<Entity> inputs,
        Entity? target = null,
        IReadOnlyDictionary<string, object>? context = null)
    {
        var contributions = new Dictionary<(Entity Source, string Rule, string Aspect), int>();

        foreach (var entity in inputs.Distinct())
        {
            var aspectDict = entity.GetAspects(target);
            foreach (var (aspect, strength) in aspectDict)
            {
                var token = (entity, "base", aspect.ToString());
                contributions[token] = Math.Max(strength, contributions.GetValueOrDefault(token, 0));
            }
        }

        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ((_, _, aspect), strength) in contributions)
        {
            totals[aspect] = totals.GetValueOrDefault(aspect, 0) + strength;
        }
        return totals;
    }
}

/// <summary>统一核验简单言灵、持续仪式（Rite）和因果契约（Covenant）的运行时执行系统。</summary>
/// <remarks>
/// KotodamaSystem 负责世界引擎中所有超自然语言、仪式结算与契约因果的生命周期管理：
/// 1. 效果原语注册（<see cref="Register"/>）：内置物理移动、属性增减、资源消耗与关系建立断开等核心原子效果；
/// 2. 仪式生命周期（Begin -> Advance -> Complete）：管理包含槽位匹配（<see cref="Check"/>）、源质门槛校验与代价支付（Pay）的多阶段仪式；
/// 3. 持久契约（Covenant）：维护具有不可违抗硬性规约（HardConstraint）与违约惩戒（Obligation / Breach）的客观约束；
/// 4. 动作拦截（<see cref="Allows(Effect)"/>）：在动作执行前实时审查是否触犯活跃契约的硬性规约；
/// 5. 因果回响（<see cref="Observe(Effect)"/> 与 <see cref="Trigger"/>）：在世界效果发生后自动审查触发器并施加连带效果。
/// </remarks>
public class KotodamaSystem
{
    private readonly RuleEnvironment _env;
    private readonly RuleSystem _rules;
    public EffectExecutor Effects { get; } = new();
    private readonly List<Entity> _rites = new();
    private readonly List<Entity> _contracts = new();

    /// <summary>当前世界上处于准备或施行阶段的仪式实体列表（Rite）。</summary>
    public IReadOnlyList<Entity> Rites => _rites;

    /// <summary>当前世界上生效的所有持久言灵契约实体列表（Covenant）。</summary>
    public IReadOnlyList<Entity> Contracts => _contracts;

    /// <summary>累计执行的契约硬约束合规审查总次数。</summary>
    public int ConstraintChecks { get; private set; }

    /// <summary>因触犯契约硬约束而遭到拦截阻断的意图总次数。</summary>
    public int BlockedIntents { get; private set; }

    /// <summary>最近一次仪式校验失败或代价支付中断的错误原因诊断文本。</summary>
    public string LastFailure { get; private set; } = "";

    /// <summary>初始化言灵契约系统运行时。</summary>
    /// <param name="env">绑定的规则环境。</param>
    /// <param name="rules">绑定的规则系统。</param>
    public KotodamaSystem(RuleEnvironment env, RuleSystem rules)
    {
        _env = env;
        _rules = rules;
        RegisterCoreEffects();
    }

    /// <summary>向言灵系统注册自定义效果原语的验证与执行处理委托。</summary>
    /// <param name="operation">效果操作符字符串（不区分大小写）。</param>
    /// <param name="prepare">效果计划的验证与执行处理委托。</param>
    public void Register(string operation, Action<EffectPlan, Effect> prepare) => Effects.Register(operation, prepare);

    private void RegisterCoreEffects()
    {
        Register("assert", (plan, effect) =>
        {
            if (effect.Args.Length > 0 && effect.Args[0] is Expr fact)
            {
                plan.OnCommit(() => _env.AssertFact(fact));
            }
        });
        Register("retract", (plan, effect) =>
        {
            if (effect.Args.Length > 0 && effect.Args[0] is Expr fact)
            {
                plan.OnCommit(() => _env.Retract(fact));
            }
        });
    }

    private Effect BoundEffect(Effect effect)
    {
        if (effect.Op == "sequence") return AST.F("sequence", effect.Args.OfType<Effect>().Select(BoundEffect).Cast<object>().ToArray());
        if (effect.Op == "when") return AST.F("when", Condition((Expr)effect.Args[0]), BoundEffect((Effect)effect.Args[1]));
        if (effect.Op is "change" or "spend" && effect.Args is [Entity entity, string field, var amount, ..])
        {
            var key = entity.HasProperty(field) || field.StartsWith("tag:") ? field : "tag:" + field;
            var basic = AST.F(effect.Op, entity, key, amount);
            return effect.Op == "spend" && effect.Args.Length == 4
                ? AST.F("sequence", basic, AST.F("require", AST.E("gte", AST.E("value", entity, key), effect.Args[3]), "保留量不足")) : basic;
        }
        if (effect.Op is "remove" or "consume")
            return AST.F("sequence", AST.F("require", AST.E("ne", AST.E("value", effect.Args[0], "$parent"), null!), "投入已经不存在"), AST.F("move", effect.Args[0], null!));
        if (effect.Op == "detach_relation") return AST.F("unrelate", effect.Args);
        return effect;
    }
    private Expr Condition(Expr expr) => expr.Op switch
    {
        "state" => AST.E("gte", AST.E("value", expr.Args[0], "tag:" + expr.Args[1]), 1d),
        "tag_at_least" => AST.E("gte", AST.E("value", expr.Args[0], "tag:" + expr.Args[1]), expr.Args[2]),
        "parent" => AST.E("eq", AST.E("value", expr.Args[0], "$parent"), expr.Args[1]),
        "all" or "any" or "not" => new Expr(expr.Op, expr.Args.OfType<Expr>().Select(Condition).Cast<object>().ToArray()),
        _ => expr
    };

    /// <summary>依据当前局部绑定具象化效果表达式。</summary>
    /// <param name="effect">模板效果 AST。</param>
    /// <param name="bindings">代号替换映射表。</param>
    /// <returns>参数已绑定具体引用的效果实例。</returns>
    public Effect Materialize(Effect effect, IReadOnlyDictionary<string, object> bindings)
    {
        return _env.Materialize(effect, bindings);
    }

    /// <summary>依据局部绑定具象化规则对象。</summary>
    /// <param name="rule">抽象规则定义。</param>
    /// <param name="bindings">代号映射字典。</param>
    /// <returns>具象化后的规则对象。</returns>
    public Rule MaterializeRule(Rule rule, IReadOnlyDictionary<string, object> bindings)
    {
        return _env.Materialize(rule, bindings);
    }

    /// <summary>递归验证指定效果表达式是否在当前世界状态与处理器注册表中合法可行。</summary>
    /// <param name="effect">待验证效果 AST。</param>
    /// <param name="cost">是否作为代价进行额外支付能力检查。</param>
    /// <returns>true 表示效果参数完备且前置检查通过。</returns>
    public bool ValidateEffect(Effect effect, bool cost = false) => Effects.Prepare([BoundEffect(effect)]).Succeeded;

    public IReadOnlyList<Entity>? Apply(params Effect[] effects)
    {
        var plan = Effects.Prepare(effects.Select(BoundEffect));
        var result = plan.Commit();
        return result.Succeeded ? plan.Changes.Select(c => c.Subject).Distinct().ToArray() : null;
    }

    private static IReadOnlyList<Entity> FlattenEntities(IReadOnlyDictionary<string, object>? inputs)
    {
        if (inputs == null) return [];
        var found = new List<Entity>();
        foreach (var val in inputs.Values)
        {
            if (val is Entity e) found.Add(e);
            else if (val is IEnumerable<Entity> es) found.AddRange(es);
        }
        return found.Distinct().ToList();
    }

    private bool CheckSlot(
        string role,
        SlotSpec spec,
        object? value,
        IReadOnlyDictionary<string, object> bindings)
    {
        var entities = value is Entity e ? [e] : (value as IEnumerable<Entity>)?.ToList() ?? new List<Entity>();
        if (entities.Count == 0)
        {
            return spec.Optional;
        }
        if (!spec.Many && entities.Count != 1)
        {
            return false;
        }

        if (spec.Tags is { Count: > 0 } tagsMap)
        {
            if (entities.Any(ent => tagsMap.Any(kvp => ent.Tag(kvp.Key) < kvp.Value)))
            {
                return false;
            }
        }

        if (spec.Held)
        {
            var holderKey = string.IsNullOrEmpty(spec.Holder) ? "holder" : spec.Holder;
            var holder = bindings.GetValueOrDefault(holderKey) as TreeMember;
            if (holder != null && entities.Any(ent => !ReferenceEquals(ent.Parent, holder)))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(spec.Relation))
        {
            var targetKey = string.IsNullOrEmpty(spec.Target) ? "target" : spec.Target;
            var target = bindings.GetValueOrDefault(targetKey) as Entity;
            if (target == null || entities.Any(ent => !ent.Related(spec.Relation, target).Any()))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>汇总计算施术者与输入素材聚合后的全部有效源质（Aspect）签名强度。</summary>
    /// <param name="recipe">配方实体（读取 include_speaker 等配置）。</param>
    /// <param name="speaker">施术者心智主体。</param>
    /// <param name="inputs">投入仪式的实体映射。</param>
    /// <param name="bindings">当前绑定上下文字典。</param>
    /// <returns>聚合后的源质签名与强度字典。</returns>
    public Dictionary<string, int> Signature(
        Entity recipe,
        Soul speaker,
        IReadOnlyDictionary<string, object>? inputs = null,
        IReadOnlyDictionary<string, object>? bindings = null)
    {
        var entities = new List<Entity>(FlattenEntities(inputs));
        if (recipe.Get<bool>("include_speaker", true))
        {
            entities.Add(speaker);
        }
        return KotodamaFactory.AggregateAspects(entities, bindings?.GetValueOrDefault("target") as Entity, bindings);
    }

    /// <summary>全面校验仪式配方在当前输入实体、目标客体与环境上下文下的前置合法性。</summary>
    /// <param name="recipe">待校验的配方实体。</param>
    /// <param name="speaker">施术者主体。</param>
    /// <param name="inputs">投入的素材与介质字典。</param>
    /// <param name="target">作用目标实体。</param>
    /// <param name="context">附加上下文环境。</param>
    /// <returns>未满足要求的缺失项列表（为空表示校验全部通过）。</returns>
    public IReadOnlyList<string> Check(
        Entity recipe,
        Soul speaker,
        IReadOnlyDictionary<string, object>? inputs = null,
        Entity? target = null,
        IReadOnlyDictionary<string, object>? context = null)
    {
        var ctx = context != null ? new Dictionary<string, object>(context) : new Dictionary<string, object>();
        if (inputs != null)
        {
            foreach (var (k, v) in inputs) ctx[k] = v;
        }
        ctx["speaker"] = speaker;
        if (target != null) ctx["target"] = target;

        var missing = new List<string>();
        var slots = recipe.Get<IReadOnlyDictionary<string, SlotSpec>>("slots") ?? new Dictionary<string, SlotSpec>();

        foreach (var (role, spec) in slots)
        {
            object? slotVal = null;
            inputs?.TryGetValue(role, out slotVal);
            if (!CheckSlot(role, spec, slotVal, ctx))
            {
                missing.Add($"槽位:{role}");
            }
        }

        var values = Signature(recipe, speaker, inputs, ctx);
        var thresholds = recipe.Get<IReadOnlyDictionary<string, int>>("thresholds") ?? new Dictionary<string, int>();
        var required = new Dictionary<string, int>(thresholds, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, amount) in required)
        {
            int actual = values.GetValueOrDefault(key, 0);
            if (actual < amount)
            {
                missing.Add($"门槛:{key} {actual}/{amount}");
            }
        }

        foreach (var facet in recipe.Entries(FacetType.Requirement))
        {
            bool valid;
            if (facet.Value is Func<IReadOnlyDictionary<string, object>, bool> pred)
            {
                valid = pred(ctx);
            }
            else if (facet.Value is Expr expr)
            {
                valid = _env.Evaluate(_env.Materialize(expr, ctx));
            }
            else
            {
                valid = true;
            }
            if (!valid)
            {
                missing.Add("条件");
            }
        }

        return missing;
    }

    /// <summary>基于输入素材中包含的定向语义关系，反向推导可作为仪式施加对象的候选目标实体列表。</summary>
    /// <param name="recipe">配方实体。</param>
    /// <param name="inputs">投入的素材字典。</param>
    /// <returns>符合槽位关联关系约束的公共交集目标实体列表。</returns>
    public IReadOnlyList<Entity> TargetCandidates(Entity recipe, IReadOnlyDictionary<string, object>? inputs = null)
    {
        var groups = new List<HashSet<Entity>>();
        var slots = recipe.Get<IReadOnlyDictionary<string, SlotSpec>>("slots") ?? new Dictionary<string, SlotSpec>();

        foreach (var (role, spec) in slots)
        {
            if (string.IsNullOrEmpty(spec.Relation)) continue;
            if (inputs == null || !inputs.TryGetValue(role, out var slotVal)) continue;

            var entities = slotVal is Entity e ? [e] : (slotVal as IEnumerable<Entity>)?.ToList() ?? new List<Entity>();
            foreach (var ent in entities)
            {
                var targets = ent.Related(spec.Relation).Select(f => f.Value).OfType<Entity>().ToHashSet();
                groups.Add(targets);
            }
        }

        if (groups.Count == 0) return [];
        IEnumerable<Entity> common = groups[0];
        for (int i = 1; i < groups.Count; i++)
        {
            common = common.Intersect(groups[i]);
        }
        return common.ToList();
    }

    /// <summary>发起并启动一项仪式，校验前置条件并创建标记为 Rite=1 的施行状态实体。</summary>
    /// <param name="recipe">仪式配方实体。</param>
    /// <param name="speaker">施术者心智。</param>
    /// <param name="inputs">投入素材字典。</param>
    /// <param name="target">作用目标实体。</param>
    /// <param name="context">附加上下文。</param>
    /// <returns>成功创建的 Rite 实体；若校验未通过则记录 LastFailure 并返回 null。</returns>
    public Entity? Begin(
        Entity recipe,
        Soul speaker,
        IReadOnlyDictionary<string, object>? inputs = null,
        Entity? target = null,
        IReadOnlyDictionary<string, object>? context = null)
    {
        var missing = Check(recipe, speaker, inputs, target, context);
        if (missing.Count > 0)
        {
            LastFailure = string.Join("、", missing);
            return null;
        }

        var ctx = context != null ? new Dictionary<string, object>(context) : new Dictionary<string, object>();
        var parent = (ctx.GetValueOrDefault("world") as TreeMember) ?? speaker.Parent;

        var rite = new Entity(
            $"{recipe.Name}·施行",
            parent,
            new Dictionary<string, int> { ["Rite"] = 1, ["Active"] = 1 },
            properties: new Dictionary<string, object>
            {
                ["recipe"] = recipe,
                ["speaker"] = speaker,
                ["target"] = target!,
                ["inputs"] = inputs != null ? new Dictionary<string, object>(inputs) : new Dictionary<string, object>(),
                ["context"] = ctx,
                ["step"] = 0,
                ["phase"] = "prepared"
            });

        rite.Relate(FacetType.Source, speaker);
        if (target != null) rite.Relate(FacetType.Target, target);

        if (inputs != null)
        {
            foreach (var (role, val) in inputs)
            {
                rite.Attach(role, val);
                if (val is Entity e) rite.Relate(FacetType.Input, e);
            }
        }

        _rites.Add(rite);
        return rite;
    }

    /// <summary>推进仪式完成阶段，扣除代价并结算产出效果或生成持久 Covenant 契约实体。</summary>
    /// <param name="rite">正在施行的 Rite 实体。</param>
    /// <returns>结算完成的 Rite 实体或新生成的 Covenant 契约实体；若代价支付失败则返回 null。</returns>
    public Entity? Advance(Entity rite)
    {
        var recipe = rite.Get<Entity>("recipe")!;
        var inputs = rite.Get<IReadOnlyDictionary<string, object>>("inputs")!;
        var context = rite.Get<IReadOnlyDictionary<string, object>>("context")!;

        var bindings = new Dictionary<string, object>(context);
        foreach (var (k, v) in inputs) bindings[k] = v;
        bindings["speaker"] = rite.Get<Soul>("speaker")!;
        bindings["target"] = rite.Get<Entity>("target")!;
        bindings["rite"] = rite;

        return Complete(rite, bindings);
    }

    private Entity? Complete(Entity rite, IReadOnlyDictionary<string, object> bindings)
    {
        var recipe = rite.Get<Entity>("recipe")!;
        var costs = recipe.Entries(FacetType.Cost).Select(f => f.Value).OfType<Effect>();

        bool persistent = recipe.Has("Covenant") || recipe.Entries(FacetType.Trigger).Any() ||
            recipe.Entries(FacetType.HardConstraint).Any() || recipe.Entries(FacetType.Obligation).Any();
        var outputs = persistent ? Enumerable.Empty<Effect>() : recipe.Entries(FacetType.Effect).Select(f => f.Value).OfType<Effect>();
        var plan = Effects.Prepare(costs.Concat(outputs).Select(e => BoundEffect(Materialize(e, bindings))));
        if (!plan.Commit().Succeeded)
        {
            LastFailure = plan.Reason;
            rite.Set("phase", "interrupted");
            return null;
        }
        foreach (var change in plan.Changes)
            rite.Attach(FacetType.Change, change, source: change.Subject);

        Entity result;
        if (persistent)
        {
            var contract = new Entity(
                recipe.Name,
                rite.Parent,
                new Dictionary<string, int> { ["Covenant"] = 1, ["Active"] = 1 },
                properties: new Dictionary<string, object>
                {
                    ["recipe"] = recipe,
                    ["speaker"] = rite.Get<Soul>("speaker")!,
                    ["target"] = rite.Get<Entity>("target")!,
                    ["bindings"] = new Dictionary<string, object>(bindings),
                    ["active"] = true,
                    ["fired"] = false
                });

            foreach (var facetType in new[] { FacetType.Trigger, FacetType.HardConstraint, FacetType.Obligation, FacetType.Breach, FacetType.Exception, FacetType.Effect })
            {
                foreach (var facet in recipe.Entries(facetType))
                {
                    object val = facet.Value switch
                    {
                        Rule r => MaterializeRule(r, bindings),
                        Effect eff => Materialize(eff, bindings),
                        Expr expr => _env.Materialize(expr, bindings),
                        _ => facet.Value
                    };
                    contract.Attach(facetType, val, facet.Strength, source: recipe);
                }
            }

            _contracts.Add(contract);
            result = contract;
        }
        else
        {
            result = rite;
        }

        rite.Tags.Remove("Active");
        rite.Change("Completed", 1);
        rite.Set("phase", "completed");
        return result;
    }

    /// <summary>原子化一步完成仪式的校验、启动与执行全流程（Begin + Advance）。</summary>
    /// <param name="recipe">仪式配方实体。</param>
    /// <param name="speaker">施术者心智。</param>
    /// <param name="inputs">投入素材字典。</param>
    /// <param name="target">目标实体。</param>
    /// <param name="context">上下文环境。</param>
    /// <returns>完成后的产出实体或契约实体；失败返回 null。</returns>
    public Entity? Invoke(
        Entity recipe,
        Soul speaker,
        IReadOnlyDictionary<string, object>? inputs = null,
        Entity? target = null,
        IReadOnlyDictionary<string, object>? context = null)
    {
        var rite = Begin(recipe, speaker, inputs, target, context);
        return rite != null ? Advance(rite) : null;
    }

    private bool Excepted(Entity contract)
    {
        return contract.Entries(FacetType.Exception)
            .Select(f => f.Value)
            .OfType<Expr>()
            .Any(expr => _env.Evaluate(expr));
    }

    /// <summary>检查指定行动意图是否被活跃契约的硬性约束阻断。</summary>
    /// <param name="intent">待检查行动意图。</param>
    /// <returns>true 表示允许执行；false 表示被契约硬约束拦截。</returns>
    public bool Allows(Intent intent) => Allows(intent.Effect);

    /// <summary>检查特定世界效果是否被生效契约的硬性约束（HardConstraint）阻断。</summary>
    /// <param name="effect">效果 AST。</param>
    /// <returns>true 表示允许通过；false 表示被契约绝对拦截。</returns>
    public bool Allows(Effect effect)
    {
        ConstraintChecks++;
        bool allowed = _contracts.Where(c => c.Get<bool>("active", true) && c.Has("Active") && !Excepted(c))
            .All(contract =>
            {
                var hardConstraints = contract.Entries(FacetType.HardConstraint).Select(f => f.Value).OfType<Rule>();
                return _rules.ObjectiveAllows(effect, hardConstraints, _env);
            });

        if (!allowed) BlockedIntents++;
        return allowed;
    }

    /// <summary>向所有生效契约通报发生的世界效果，审查是否触犯遵从义务（Obligation），触犯时立即施加违约惩戒（Breach）。</summary>
    /// <param name="effect">已发生的世界效果 AST。</param>
    /// <returns>发生违约触碰的契约实体列表。</returns>
    public IReadOnlyList<Entity> Observe(Effect effect)
    {
        var breached = new List<Entity>();
        foreach (var contract in _contracts)
        {
            if (!contract.Has("Active") || Excepted(contract)) continue;
            var obligations = contract.Entries(FacetType.Obligation).Select(f => f.Value).OfType<Rule>().ToList();
            if (obligations.Count > 0 && obligations.All(rule => _rules.ObjectiveComplies(effect, rule, _env)))
            {
                continue;
            }
            if (obligations.Count == 0) continue;

            contract.Change("Breached", 1);
            breached.Add(contract);
            Apply(contract.Entries(FacetType.Breach).Select(f => f.Value).OfType<Effect>().ToArray());
        }
        return breached;
    }

    /// <summary>扫描全部生效契约，若其被动触发规则（Trigger）在环境中完全成立，自动执行预设效果并标记完成。</summary>
    /// <returns>本次扫描触发并成功执行效果的契约实体列表。</returns>
    public IReadOnlyList<Entity> Trigger()
    {
        var fired = new List<Entity>();
        foreach (var contract in _contracts.ToList())
        {
            if (!contract.Has("Active") || contract.Has("Firing") || contract.Get<bool>("fired", false) || Excepted(contract))
            {
                continue;
            }

            var triggers = contract.Entries(FacetType.Trigger).Select(f => f.Value).OfType<Rule>().ToList();
            if (triggers.Count > 0 && !triggers.All(r => _rules.ActivationSatisfied(r, _env)))
            {
                continue;
            }

            contract.Change("Firing", 1);
            bool succeeded;
            try { succeeded = Apply(contract.Entries(FacetType.Effect).Select(f => f.Value).OfType<Effect>().ToArray()) != null; }
            finally { contract.Tags.Remove("Firing"); }

            if (!succeeded) continue;

            contract.Change("Fired", 1);
            contract.Set("fired", true);
            if (!contract.Entries(FacetType.HardConstraint).Any() && !contract.Entries(FacetType.Obligation).Any())
            {
                contract.Tags.Remove("Active");
                contract.Set("active", false);
            }
            fired.Add(contract);
        }
        return fired;
    }

    /// <summary>强制撤销并作废指定的因果契约实体。</summary>
    /// <param name="contract">待废除的契约实体。</param>
    public void Revoke(Entity contract)
    {
        contract.Tags.Remove("Active");
        contract.Change("Revoked", 1);
        contract.Set("active", false);
    }
}
