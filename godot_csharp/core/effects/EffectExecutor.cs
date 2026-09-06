using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core.Effects;

/// <summary>同一实体字段的真实变化；字段前缀仅供执行器区分已有状态容器。</summary>
public sealed record StateChange(Entity Subject, string Field, object? Before, object? After);

/// <summary>局部覆盖视图。认知读取不回退到隐藏的客观值，分支不写实体。</summary>
public sealed class StateView
{
    public static readonly object Unknown = new();
    private readonly Dictionary<(Entity, string), object?> _values;
    private readonly Dictionary<(Entity, string), object?> _reads;
    public Soul? Observer { get; }
    public StateView(Soul? observer = null)
    {
        Observer = observer;
        _values = new();
        _reads = new();
    }
    private StateView(StateView source)
    {
        Observer = source.Observer;
        _values = new(source._values);
        _reads = new(source._reads);
    }
    public StateView Fork() => new(this);
    internal IReadOnlyDictionary<(Entity, string), object?> Reads => _reads;

    public object? Read(Entity entity, string field)
    {
        var key = (entity, field);
        if (_values.TryGetValue(key, out var value)) return value;
        if (Observer == null || entity == Observer || Observer.Characters.Contains(entity)
            || entity.Parent is Character owner && Observer.Characters.Contains(owner)) value = Actual(entity, field);
        else value = Knowledge(Observer).GetValueOrDefault(key, Unknown);
        _reads.TryAdd(key, value);
        return value;
    }
    public double Number(Entity entity, string field) => Read(entity, field) is { } value && value != Unknown
        ? Convert.ToDouble(value) : double.NaN;
    public void Write(Entity entity, string field, object? value)
    {
        Read(entity, field);
        _values[(entity, field)] = value;
    }
    public static Dictionary<(Entity, string), object?> Knowledge(Soul soul) =>
        soul.Get<Dictionary<(Entity, string), object?>>("KnownState") ?? new();
    public static void Learn(Soul soul, Entity subject, string field, object? value, object? source = null)
    {
        var current = Knowledge(soul);
        if (current.TryGetValue((subject, field), out var previous) && Same(previous, value)) return;
        var known = new Dictionary<(Entity, string), object?>(current) { [(subject, field)] = value };
        soul.Set("KnownState", known);
        soul.Observe(AST.E("learned", subject, field, value ?? "空"), source);
    }
    public static object? Actual(Entity entity, string field) => field switch
    {
        "strain" when entity is Character character => character.Mind?.Fatigue ?? 0d,
        "$parent" => entity.Parent,
        _ when field.StartsWith("tag:") => entity.Tag(field[4..]),
        _ when field.StartsWith("rel:") => entity.Related(field[4..]).Select(f => f.Value).Distinct().ToArray(),
        _ => entity.Properties.GetValueOrDefault(field)
    };
    internal static void Apply(Entity entity, string field, object? value)
    {
        if (field == "KnownState" && entity is Soul soul && value is Dictionary<(Entity, string), object?> known)
        {
            var previous = Knowledge(soul);
            foreach (var ((subject, key), learned) in known)
                if (!previous.TryGetValue((subject, key), out var old) || !Same(old, learned))
                    soul.Observe(AST.E("learned", subject, key, learned ?? "空"), subject);
            soul.Set(field, known);
        }
        else if (field == "strain" && entity is Character character) character.Mind!.SetFatigue(Convert.ToDouble(value));
        else if (field == "$parent") entity.Reparent((TreeMember?)value);
        else if (field.StartsWith("tag:")) entity.SetTag(field[4..], Convert.ToInt32(value));
        else if (field.StartsWith("rel:"))
        {
            var targets = (object[]?)value ?? [];
            foreach (var facet in entity.Related(field[4..]).ToArray())
                if (!targets.Contains(facet.Value)) entity.Detach(facet);
            foreach (var target in targets)
                if (!entity.Related(field[4..], target).Any()) entity.Relate(field[4..], target);
        }
        else if (value == null) entity.Properties.Remove(field);
        else entity.Set(field, value);
    }
    public static bool Same(object? a, object? b) => a is object[] aa && b is object[] bb
        ? aa.SequenceEqual(bb) : IsNumber(a) && IsNumber(b) ? Convert.ToDouble(a) == Convert.ToDouble(b) : Equals(a, b);
    private static bool IsNumber(object? value) => value is int or double or float or long or decimal;

    internal static double FiniteArithmetic(double x, double y, bool subtract)
    {
        var result = subtract ? x - y : x + y;
        return double.IsFinite(result) ? result : throw new InvalidOperationException("数值运算结果必须为有限量");
    }

    public object? Value(object? expression)
    {
        if (expression is not Expr e) return expression;
        var args = e.Args;
        if (e.Op == "quote") return args[0];
        if (e.Op == "value") return Read((Entity)args[0], (string)args[1]);
        if (e.Op == "known") return Value(args[0]) != Unknown;
        if (e.Op == "all")
        {
            var values = args.Select(Value).ToArray();
            return values.Contains(false) ? false : values.Contains(Unknown) ? Unknown : true;
        }
        if (e.Op == "any")
        {
            var values = args.Select(Value).ToArray();
            return values.Contains(true) ? true : values.Contains(Unknown) ? Unknown : false;
        }
        var x = args.Length > 0 ? Value(args[0]) : null;
        var y = args.Length > 1 ? Value(args[1]) : null;
        if (x == Unknown || y == Unknown) return Unknown;
        return e.Op switch
        {
            "true" => true,
            "false" => false,
            "not" => !Equals(x, true),
            "eq" => Same(x, y),
            "ne" => !Same(x, y),
            "gte" => Convert.ToDouble(x) >= Convert.ToDouble(y),
            "gt" => Convert.ToDouble(x) > Convert.ToDouble(y),
            "lte" => Convert.ToDouble(x) <= Convert.ToDouble(y),
            "add" => FiniteArithmetic(Convert.ToDouble(x), Convert.ToDouble(y), subtract: false),
            "subtract" => FiniteArithmetic(Convert.ToDouble(x), Convert.ToDouble(y), subtract: true),
            "contains" => x is object[] items && items.Contains(y),
            _ => throw new InvalidOperationException($"未知条件原语：{e.Op}")
        };
    }
}

public sealed class EffectPlan
{
    public StateView View { get; }
    public List<StateChange> Changes { get; } = new();
    private readonly List<Action> _commitActions = [];
    private readonly List<Action> _abortActions = [];
    public string Reason { get; internal set; } = "";
    public bool Uncertain { get; internal set; }
    public bool IsCommitted { get; private set; }
    public bool IsAborted { get; private set; }
    public bool Succeeded => Reason.Length == 0 && !IsAborted;
    internal EffectPlan(StateView view) => View = view;

    /// <summary>登记只在客观计划成功提交后执行的外部状态动作。</summary>
    public void OnCommit(Action action) => _commitActions.Add(action);

    /// <summary>登记在计划中止（Abort）时触发的资源与锁释放动作。</summary>
    public void OnAbort(Action action) => _abortActions.Add(action);

    public void Write(Entity entity, string field, object? value)
    {
        var before = View.Read(entity, field);
        if (!StateView.Same(before, value)) Changes.Add(new(entity, field, before, value));
        View.Write(entity, field, value);
    }

    /// <summary>中止事务执行，释放暂存锁定与资源预留。</summary>
    public void Abort(string reason = "事务已主动中止")
    {
        if (IsCommitted || IsAborted) return;
        IsAborted = true;
        Reason = reason;
        foreach (var action in _abortActions) action();
    }

    /// <summary>提交事务计划。提交失败时反向恢复已经写入的状态变化。</summary>
    public EffectResult Commit()
    {
        if (IsCommitted)
            return new(false, null, "计划已被提交，禁止重复提交");
        if (IsAborted)
            return new(false, null, Reason.Length > 0 ? Reason : "计划已被中止");
        if (!Succeeded || Uncertain || View.Observer != null)
            return new(false, null, Reason.Length > 0 ? Reason : "认知推演不能提交");
        if (View.Reads.Any(entry => !StateView.Same(StateView.Actual(entry.Key.Item1, entry.Key.Item2), entry.Value)))
            return new(false, null, "状态已变化，需要重新准备");

        var applied = new List<StateChange>(Changes.Count);
        try
        {
            foreach (var change in Changes)
            {
                StateView.Apply(change.Subject, change.Field, change.After);
                applied.Add(change);
            }
            foreach (var action in _commitActions) action();
            IsCommitted = true;
            return new(true, Changes.ToArray());
        }
        catch (Exception ex)
        {
            for (var i = applied.Count - 1; i >= 0; i--)
            {
                var change = applied[i];
                try { StateView.Apply(change.Subject, change.Field, change.Before); }
                catch { /* 保留最初的提交异常；状态写入器应保持可逆。 */ }
            }
            Reason = $"提交失败：{ex.Message}";
            return new(false, null, Reason);
        }
    }
}

/// <summary>普通行动、言灵与契约共用的唯一声明式变化解释器。</summary>
public sealed class EffectExecutor
{
    private readonly Dictionary<string, Action<EffectPlan, Effect>> _handlers = new();
    public void Register(string op, Action<EffectPlan, Effect> prepare) => _handlers.Add(op, prepare);
    public void Register(EffectOp op, Action<EffectPlan, Effect> prepare) => Register(op.ToOpString(), prepare);
    public EffectPlan Prepare(IEnumerable<Effect> effects, StateView? view = null, IEnumerable<Expr>? requirements = null)
    {
        var plan = new EffectPlan((view ?? new StateView()).Fork());
        try
        {
            foreach (var condition in requirements ?? [])
            {
                var result = plan.View.Value(condition);
                if (result == StateView.Unknown) plan.Uncertain = true;
                else if (!Equals(result, true)) { plan.Reason = "行动条件不成立"; return plan; }
            }
            foreach (var effect in effects) PrepareOne(plan, effect);
        }
        catch (InvalidOperationException ex) { plan.Reason = ex.Message; }
        return plan;
    }
    private void PrepareOne(EffectPlan plan, Effect effect)
    {
        var a = effect.Args;
        if (effect.Op == "sequence") { foreach (var nested in a.Cast<Effect>()) PrepareOne(plan, nested); return; }
        if (effect.Op == "when")
        {
            var condition = plan.View.Value(a[0]);
            if (condition == StateView.Unknown) plan.Uncertain = true;
            else if (Equals(condition, true)) PrepareOne(plan, (Effect)a[1]);
            return;
        }
        if (_handlers.TryGetValue(effect.Op, out var handler)) { handler(plan, effect); return; }
        if (effect.Op == "require")
        {
            var result = plan.View.Value(a[0]);
            if (result == StateView.Unknown) plan.Uncertain = true;
            else if (!Equals(result, true)) throw new InvalidOperationException(a.Length > 1 ? (string)a[1] : "条件不成立");
            return;
        }
        if (a.Length == 0 || a[0] is not Entity entity) throw new InvalidOperationException($"未知效果或缺少对象：{effect.Op}");
        if (effect.Op == "move")
        {
            var parent = a[1] as Entity;
            for (var ancestor = parent; ancestor != null; ancestor = plan.View.Read(ancestor, "$parent") as Entity)
                if (ancestor == entity) throw new InvalidOperationException("不能形成包含循环");
            plan.Write(entity, "$parent", parent); return;
        }
        if (effect.Op == "observe")
        {
            var soul = (Soul)entity;
            var subject = (Entity)a[1];
            var field = (string)a[2];
            var current = plan.View.Read(soul, "KnownState") as Dictionary<(Entity, string), object?> ?? new();
            var known = new Dictionary<(Entity, string), object?>(current);
            var value = a.Length > 3 ? plan.View.Value(a[3]) : plan.View.Read(subject, field);
            if (value == StateView.Unknown) plan.Uncertain = true;
            else known[(subject, field)] = value;
            plan.Write(soul, "KnownState", known); return;
        }
        var key = a.Length > 1 ? a[1] as string : null;
        if (key == null) throw new InvalidOperationException($"未知效果：{effect.Op}");
        var supplied = a.Length > 2 ? plan.View.Value(a[2]) : null;
        if (supplied == StateView.Unknown) { plan.Uncertain = true; return; }
        switch (effect.Op)
        {
            case "set":
                if (key == "strain")
                {
                    var value = Convert.ToDouble(supplied);
                    if (!double.IsFinite(value)) throw new InvalidOperationException("疲劳值必须为有限量");
                    plan.Write(entity, key, Math.Clamp(value, 0d, Core.Content.Balance.FatigueHardLimit));
                }
                else plan.Write(entity, key, supplied);
                break;
            case "change":
            case "spend":
                var before = plan.View.Read(entity, key);
                if (before == StateView.Unknown) { plan.Uncertain = true; break; }
                var current = Convert.ToDouble(before);
                if (!double.IsFinite(current)) throw new InvalidOperationException("变化起始值必须为有限量");
                var amount = Convert.ToDouble(supplied);
                if (!double.IsFinite(amount)) throw new InvalidOperationException("变化必须为有限量");
                if (effect.Op == "spend" && amount <= 0) throw new InvalidOperationException("支付量必须为正");
                var after = StateView.FiniteArithmetic(current,
                    effect.Op == "spend" ? -amount : amount, subtract: false);
                if (after < 0) throw new InvalidOperationException($"{entity.Name}的{key}不足");
                if (key == "strain") after = Math.Min(Core.Content.Balance.FatigueHardLimit, after);
                plan.Write(entity, key, after); break;
            case "relate":
            case "unrelate":
                var existing = plan.View.Read(entity, "rel:" + key);
                if (existing == StateView.Unknown) { plan.Uncertain = true; break; }
                var targets = ((object[]?)existing ?? []).ToList();
                if (effect.Op == "relate" && !targets.Contains(supplied!)) targets.Add(supplied!);
                if (effect.Op == "unrelate") targets.Remove(supplied!);
                plan.Write(entity, "rel:" + key, targets.ToArray()); break;
            default: throw new InvalidOperationException($"未知效果原语：{effect.Op}");
        }
    }
}
