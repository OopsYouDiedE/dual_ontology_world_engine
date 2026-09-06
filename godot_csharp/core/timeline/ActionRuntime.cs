using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Kotodama;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Effects;

namespace DualOntologyWorldEngine.Core.Timeline;

/// <summary>审查与结算共用结果。Allowed 表示准许执行；Result.Succeeded 表示效果成功；仅结算产生事件。</summary>
public sealed record ActionResolution(
    bool Allowed,
    string Reason,
    Decision? Decision = null,
    Entity? Event = null,
    EffectResult? Result = null);

/// <summary>行动审查、执行与结算的强类型不可变上下文。</summary>
public sealed record ActionContext(
    Entity? Scene = null,
    object? Cause = null,
    Intent? ParentIntent = null,
    string Phase = "Main",
    ActionRuntime? Runtime = null,
    object? Domains = null,
    IEnumerable<Entity>? Events = null,
    object? Source = null,
    Entity? Target = null,
    Expr? Claim = null,
    Entity? Evidence = null,
    Entity? Offer = null,
    Entity? Requested = null,
    IReadOnlyDictionary<string, object>? Extra = null)
{
    public static readonly ActionContext Empty = new();

    public static ActionContext FromDictionary(IReadOnlyDictionary<string, object>? dict)
    {
        if (dict == null) return Empty;
        return new ActionContext(
            Scene: dict.GetValueOrDefault("scene") as Entity,
            Cause: dict.GetValueOrDefault("cause"),
            ParentIntent: dict.GetValueOrDefault("parent_intent") as Intent,
            Phase: dict.GetValueOrDefault("phase") as string ?? "Main",
            Runtime: dict.GetValueOrDefault("runtime") as ActionRuntime,
            Domains: dict.GetValueOrDefault("domains"),
            Events: dict.GetValueOrDefault("events") as IEnumerable<Entity>,
            Source: dict.GetValueOrDefault("source"),
            Target: dict.GetValueOrDefault("target") as Entity,
            Claim: (dict.GetValueOrDefault("claim") ?? dict.GetValueOrDefault("proposition") ?? dict.GetValueOrDefault("topic")) as Expr,
            Evidence: (dict.GetValueOrDefault("evidence") ?? dict.GetValueOrDefault("record") ?? dict.GetValueOrDefault("proof")) as Entity,
            Offer: dict.GetValueOrDefault("offer") as Entity,
            Requested: (dict.GetValueOrDefault("requested") ?? dict.GetValueOrDefault("request")) as Entity,
            Extra: dict);
    }

    public Dictionary<string, object> ToDictionary()
    {
        var dict = Extra != null
            ? new Dictionary<string, object>(Extra, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (Scene != null) dict["scene"] = Scene;
        if (Cause != null) dict["cause"] = Cause;
        if (ParentIntent != null) dict["parent_intent"] = ParentIntent;
        dict["phase"] = Phase;
        if (Runtime != null) dict["runtime"] = Runtime;
        if (Domains != null) dict["domains"] = Domains;
        if (Events != null) dict["events"] = Events;
        if (Source != null) dict["source"] = Source;
        if (Target != null) dict["target"] = Target;
        if (Claim != null) dict["claim"] = Claim;
        if (Evidence != null) dict["evidence"] = Evidence;
        if (Offer != null) dict["offer"] = Offer;
        if (Requested != null) dict["requested"] = Requested;
        return dict;
    }
}

public static class EventExtensions
{
    public static bool IsSucceeded(this Entity ev) => ev.Get<bool>("succeeded");
    public static Intent? GetIntent(this Entity ev) => ev.Get<Intent>("intent");
    public static StateChange[] GetChanges(this Entity ev) => ev.Get<StateChange[]>("changes") ?? [];
}

/// <summary>行动统一网关。它拥有审查、执行、事件、契约、见证与嵌套反应的完整闭环。</summary>
public class ActionRuntime
{
    public bool DeclarativeOnly { get; set; }
    private int _chainDepth;
    private readonly HashSet<Entity> _reacted = new();
    public EffectExecutor Effects { get; }
    public List<Func<Intent, StateView, EffectPlan, string?>> Guards { get; } = new();
    public List<Func<Intent, StateView, IReadOnlyList<Effect>, IReadOnlyList<Effect>>> EffectTransforms { get; } = new();
    public EffectPlan Prepare(Intent intent, StateView? view = null)
    {
        var changes = intent.Changes ?? throw new InvalidOperationException("行动未声明变化");
        view ??= new StateView();
        foreach (var transform in EffectTransforms) changes = transform(intent, view, changes);
        var plan = Effects.Prepare(changes, view, intent.Requirements);
        if (plan.Succeeded)
            foreach (var guard in Guards)
                if (guard(intent, view, plan) is { } reason) { plan.Reason = reason; break; }
        return plan;
    }
    public Timeline Timeline { get; }
    public RuleEnvironment Env { get; }
    public RuleSystem Rules { get; }
    public KotodamaSystem Words { get; }
    /// <summary>情绪、疲劳与关系等通用心智机制的 Core 配置和结算器。</summary>
    public MindMechanics MindMechanics { get; } = new();
    public List<Entity> Patches { get; } = new();
    public List<Entity> Events { get; } = new();
    public TreeMember? EventParent { get; set; }
    public Func<Entity, ActionContext?, IEnumerable<Soul>>? WitnessesCollector { get; set; }
    public Func<Soul, Entity, ActionContext?, Intent?>? ReactionHandler { get; set; }
    /// <summary>效果发生前的拦截反应；与既有的事后见证反应分开。提供器负责反应触发条件。</summary>
    public Func<Intent, ActionContext, IEnumerable<Intent>>? BeforeReactions { get; set; }
    public List<Action<Entity, ActionContext?>> Listeners { get; } = new();

    /// <summary>效果结算后、嵌套反应前通知调度器记账。失败的执行也消耗行动值。</summary>
    public event Action<Intent, ActionResolution>? Executed;

    /// <summary>反应递归最大深度，避免相互反应形成无限行动链。</summary>
    public int MaxReactionDepth { get; set; } = 16;

    public ActionRuntime(Timeline timeline, RuleEnvironment env, RuleSystem rules, KotodamaSystem words, TreeMember? eventParent = null)
    {
        Timeline = timeline;
        Env = env;
        Rules = rules;
        Words = words;
        Effects = words.Effects;
        EventParent = eventParent;
    }

    /// <summary>验证绑定、前置条件、契约和客观规则补丁。</summary>
    public ActionResolution Review(Intent intent, ActionContext? context = null) => Review(intent, context, out _);
    public ActionResolution Review(Intent intent, IReadOnlyDictionary<string, object>? dict) => Review(intent, ActionContext.FromDictionary(dict), out _);

    private ActionResolution Review(Intent intent, ActionContext? context, out EffectPlan? plan)
    {
        plan = null;
        if (DeclarativeOnly && intent.Changes == null) return new(false, "文学模拟只接受声明式效果");
        if (!double.IsFinite(intent.MotionCost) || intent.MotionCost <= 0)
            return new ActionResolution(false, "行动值代价必须是有限正数");
        if (intent.Actor is Character character && !character.Souls.Any(soul => ReferenceEquals(soul, intent.Mind)))
            return new ActionResolution(false, "行动身体与心智未绑定");
        if (!intent.Validate()) return new ActionResolution(false, "行动客观前置条件不满足");
        if (intent.Changes != null)
        {
            plan = Prepare(intent);
            if (!plan.Succeeded) return new(false, plan.Reason);
        }
        else
            foreach (var guard in Guards)
                if (guard(intent, new StateView(), Effects.Prepare([])) is { } reason) return new(false, reason);
        if (!Words.Allows(intent.Effect)) return new ActionResolution(false, "契约硬约束阻止");
        if (!Rules.PatchAllows(intent, Patches, Env, context)) return new ActionResolution(false, "客观规则补丁阻止");
        return new ActionResolution(true, "允许执行");
    }

    /// <summary>返回完整审计结果的标准结算入口。</summary>
    public ActionResolution ResolveDetailed(Intent intent, ActionContext? context = null)
    {
        if (_chainDepth > MaxReactionDepth) return new(false, "反应链达到深度上限");
        _chainDepth++;
        try { return ResolveDetailed(intent, context ?? ActionContext.Empty, reactionDepth: 0); }
        finally { if (--_chainDepth == 0) _reacted.Clear(); }
    }

    public ActionResolution ResolveDetailed(Intent intent, IReadOnlyDictionary<string, object>? dict) =>
        ResolveDetailed(intent, ActionContext.FromDictionary(dict));

    private ActionResolution ResolveDetailed(
        Intent intent,
        ActionContext context,
        int reactionDepth)
    {
        var decision = Rules.Evaluate(intent.Mind, intent, intent.Mind.CognitiveEnvironment(Env), context);
        var review = Review(intent, context, out var plan);
        if (!review.Allowed) return review with { Decision = decision };

        if (BeforeReactions != null && reactionDepth < MaxReactionDepth)
        {
            var reactionContext = context with
            {
                ParentIntent = intent,
                Cause = intent,
                Phase = "BeforeReaction"
            };
            foreach (var reaction in BeforeReactions(intent, reactionContext).ToArray())
                if (_reacted.Add(reaction.Actor)) ResolveDetailed(reaction, reactionContext, reactionDepth + 1);
            review = Review(intent, context, out plan);
            if (!review.Allowed) return review with { Decision = decision };
        }

        var result = plan?.Commit() ?? intent.Execute();
        var ev = RuleEntityFactory.CreateEvent(
            $"{intent.Actor.Name}执行{intent.Label}", intent.Actor, intent.Effect, EventParent,
            outcome: result.Outcome, time: Timeline.Now, cause: context.Cause);
        ev.SetMind(intent.Mind);
        ev.SetPhase(context.Phase);
        ev.SetSucceeded(result.Succeeded);
        ev.SetIntent(intent);
        if (result.Outcome is StateChange[] changes) ev.SetChanges(changes);
        if (intent.Speech != null) ev.SetSpeech(intent.Speech);
        if (!string.IsNullOrEmpty(result.Reason)) ev.SetReason(result.Reason);
        Events.Add(ev);
        var resolution = new ActionResolution(true, result.Succeeded ? "已执行" : result.Reason, decision, ev, result);
        Executed?.Invoke(intent, resolution);

        if (result.Succeeded)
        {
            Words.Observe(intent.Effect);
            Words.Trigger();
        }

        var witnesses = WitnessesCollector?.Invoke(ev, context).Distinct().ToList() ?? new List<Soul>();
        foreach (var witness in witnesses)
        {
            ev.Relate(FacetType.Witness, witness);
            witness.Observe(AST.E("event", ev), source: ev);
        }

        if (result.Succeeded)
        {
            MindMechanics.Apply(intent, ev, Env, witnesses);
        }

        foreach (var witness in witnesses)
        {
            if (ReactionHandler == null || reactionDepth >= MaxReactionDepth) continue;

            var reaction = ReactionHandler(witness, ev, context);
            if (reaction == null || !_reacted.Add(reaction.Actor)) continue;

            // 反应是当前行动的嵌套子回合，绝不插入普通 Timeline 队列。
            var reactionContext = context with
            {
                Cause = ev,
                Phase = "Reaction"
            };
            ResolveDetailed(reaction, reactionContext, reactionDepth + 1);
        }

        foreach (var listener in Listeners.ToArray()) listener(ev, context);
        return resolution;
    }

}
