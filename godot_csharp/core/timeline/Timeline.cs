using System;
using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Kotodama;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Effects;

namespace DualOntologyWorldEngine.Core.Timeline;

/// <summary>
/// 计划在未来行动值时刻执行的离散任务。登记序号是队列私有排序键，不是世界对象标识。
/// </summary>
public record ScheduledTask(double Time, long Sequence, string Label, Func<double?> Action);

/// <summary>
/// 离散行动值队列。唯一排序键为绝对行动值与登记序号。
/// </summary>
public class Timeline
{
    private readonly PriorityQueue<ScheduledTask, (double Time, long Sequence)> _queue = new();
    private long _nextSequence;

    /// <summary>当前队列的稳定只读快照。</summary>
    public IReadOnlyList<ScheduledTask> Queue => _queue.UnorderedItems
        .OrderBy(item => item.Priority.Time)
        .ThenBy(item => item.Priority.Sequence)
        .Select(item => item.Element)
        .ToArray();
    /// <summary>当前全局行动值时刻。</summary>
    public double Now { get; private set; }

    /// <summary>仅在 AV 实际前进时通知连续过程；同刻的多个行动不会重复消耗需求。</summary>
    public event Action<double>? Advanced;

    private void AdvanceTo(double time)
    {
        var elapsed = time - Now;
        if (elapsed <= 0) return;
        Now = time;
        Advanced?.Invoke(elapsed);
    }

    public int Cancel(Func<ScheduledTask, bool> predicate)
    {
        var remaining = _queue.UnorderedItems.Where(item => !predicate(item.Element)).ToArray();
        var removed = _queue.Count - remaining.Length;
        _queue.Clear();
        _queue.EnqueueRange(remaining);
        return removed;
    }

    /// <summary>
    /// 从当前时刻登记任务。priority 仅为旧调用兼容参数，不参与调度顺序。
    /// </summary>
    public void Schedule(double delay, string label, Func<double?> action, int priority = 0)
    {
        if (!double.IsFinite(delay) || delay < 0)
            throw new ArgumentOutOfRangeException(nameof(delay), "行动值延迟必须是有限非负数。");

        var task = new ScheduledTask(Now + delay, _nextSequence++, label, action);
        _queue.Enqueue(task, (task.Time, task.Sequence));
    }

    /// <summary>登记当前时刻的任务；其顺序仍由登记先后决定。</summary>
    public void ScheduleImmediate(string label, Func<double?> action, int priority = 0) =>
        Schedule(0, label, action, priority);

    /// <summary>执行队首任务，并在回调给出延迟时登记下一次行动槽。</summary>
    public bool Step()
    {
        if (!_queue.TryDequeue(out var task, out _)) return false;
        AdvanceTo(task.Time);
        var nextDelay = task.Action();
        if (nextDelay.HasValue)
            Schedule(nextDelay.Value, task.Label, task.Action);
        return true;
    }

    /// <summary>推进到目标时刻，最大步数由调用方控制以防零成本循环。</summary>
    public int RunUntil(double targetTime, int maxSteps = 1000)
    {
        if (!double.IsFinite(targetTime))
            throw new ArgumentOutOfRangeException(nameof(targetTime), "目标行动值必须是有限数。");

        var steps = 0;
        while (_queue.TryPeek(out _, out var priority) && priority.Time <= targetTime && steps < maxSteps)
        {
            Step();
            steps++;
        }
        // 步数预算耗尽时保留真实进度，不能跳过尚待执行的历史任务。
        if (!_queue.TryPeek(out _, out var nextPriority) || nextPriority.Time > targetTime)
            AdvanceTo(targetTime);
        return steps;
    }
}

/// <summary>行动执行前的客观审查结果。</summary>
public record ReviewResult(bool Allowed, string Reason, Entity? Patch = null, Entity? Contract = null);

/// <summary>一次完整行动结算的审计结果。</summary>
public sealed record ActionResolution(
    bool Allowed,
    string Reason,
    Decision? Decision = null,
    Entity? Event = null,
    EffectResult? Result = null);

/// <summary>
/// 行动统一网关。它拥有审查、执行、事件、契约、见证与嵌套反应的完整闭环。
/// </summary>
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
    public Func<Entity, IReadOnlyDictionary<string, object>?, IEnumerable<Soul>>? WitnessesCollector { get; set; }
    public Func<Soul, Entity, IReadOnlyDictionary<string, object>?, Intent?>? ReactionHandler { get; set; }
    /// <summary>效果发生前的拦截反应；与既有的事后见证反应分开。提供器负责反应触发条件。</summary>
    public Func<Intent, IReadOnlyDictionary<string, object>, IEnumerable<Intent>>? BeforeReactions { get; set; }
    public List<Action<Entity, IReadOnlyDictionary<string, object>?>> Listeners { get; } = new();

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
    public ReviewResult Review(Intent intent, IReadOnlyDictionary<string, object>? context = null) => Review(intent, context, out _);

    private ReviewResult Review(Intent intent, IReadOnlyDictionary<string, object>? context, out EffectPlan? plan)
    {
        plan = null;
        if (DeclarativeOnly && intent.Changes == null) return new(false, "文学模拟只接受声明式效果");
        if (!double.IsFinite(intent.MotionCost) || intent.MotionCost <= 0)
            return new ReviewResult(false, "行动值代价必须是有限正数");
        if (intent.Actor is Character character && !character.Souls.Any(soul => ReferenceEquals(soul, intent.Mind)))
            return new ReviewResult(false, "行动身体与心智未绑定");
        if (!intent.Validate()) return new ReviewResult(false, "行动客观前置条件不满足");
        if (intent.Changes != null)
        {
            plan = Prepare(intent);
            if (!plan.Succeeded) return new(false, plan.Reason);
        }
        else
            foreach (var guard in Guards)
                if (guard(intent, new StateView(), Effects.Prepare([])) is { } reason) return new(false, reason);
        if (!Words.Allows(intent.Effect)) return new ReviewResult(false, "契约硬约束阻止");
        if (!Rules.PatchAllows(intent.Effect, Patches, Env, context)) return new ReviewResult(false, "客观规则补丁阻止");
        return new ReviewResult(true, "允许执行");
    }

    /// <summary>返回完整审计结果的标准结算入口。</summary>
    public ActionResolution ResolveDetailed(Intent intent, IReadOnlyDictionary<string, object>? context = null)
    {
        _chainDepth++;
        try { return ResolveDetailed(intent, context ?? new Dictionary<string, object>(), cause: context?.GetValueOrDefault("cause"), phase: "Main", reactionDepth: 0); }
        finally { if (--_chainDepth == 0) _reacted.Clear(); }
    }

    private ActionResolution ResolveDetailed(
        Intent intent,
        IReadOnlyDictionary<string, object> context,
        object? cause,
        string phase,
        int reactionDepth)
    {
        var decision = Rules.Evaluate(intent.Mind, intent, Env, context);
        var review = Review(intent, context, out var plan);
        if (!review.Allowed) return new ActionResolution(false, review.Reason, decision);

        if (BeforeReactions != null && reactionDepth < MaxReactionDepth)
        {
            var reactionContext = new Dictionary<string, object>(context)
            {
                ["parent_intent"] = intent,
                ["phase"] = "BeforeReaction"
            };
            foreach (var reaction in BeforeReactions(intent, reactionContext).ToArray())
                if (_reacted.Add(reaction.Actor)) ResolveDetailed(reaction, reactionContext, intent, "BeforeReaction", reactionDepth + 1);
            review = Review(intent, context, out plan);
            if (!review.Allowed) return new ActionResolution(false, review.Reason, decision);
        }

        var result = plan?.Commit() ?? intent.Execute();
        var ev = RuleEntityFactory.CreateEvent(
            $"{intent.Actor.Name}执行{intent.Label}", intent.Actor, intent.Effect, EventParent,
            outcome: result.Outcome, time: Timeline.Now, cause: cause);
        ev.Set("mind", intent.Mind);
        ev.Set("phase", phase);
        ev.Set("succeeded", result.Succeeded);
        ev.Set("intent", intent);
        if (result.Outcome is StateChange[] changes) ev.Set("changes", changes);
        if (intent.Speech != null) ev.Set("speech", intent.Speech);
        if (!string.IsNullOrEmpty(result.Reason)) ev.Set("reason", result.Reason);
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
            ResolveDetailed(reaction, context, ev, "Reaction", reactionDepth + 1);
        }

        foreach (var listener in Listeners.ToArray()) listener(ev, context);
        return resolution;
    }

    /// <summary>
    /// 旧调用兼容入口。新代码应使用 <see cref="ResolveDetailed"/> 获取拒绝、失败与空产出的完整区分。
    /// </summary>
    public (Entity Event, object? Outcome)? Resolve(Intent intent, IReadOnlyDictionary<string, object>? context = null)
    {
        var resolution = ResolveDetailed(intent, context);
        return resolution.Allowed && resolution.Event != null ? (resolution.Event, resolution.Result?.Outcome) : null;
    }
}
