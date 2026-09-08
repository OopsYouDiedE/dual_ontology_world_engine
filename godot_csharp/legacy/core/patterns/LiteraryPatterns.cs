using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Content;
using DualOntologyWorldEngine.Core.Timeline;
using System.Runtime.CompilerServices;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Core.Patterns;

public enum LiteraryPattern
{
    Prohibition, Permission, Deadline, Trigger, Exchange, Exception,
    TrueName, Truth, SelfBinding, Substitute, Seal, Testimony
}

/// <summary>基础条款抽象 record，自身持有用于状态绑定的实体令牌。</summary>
public abstract record Clause(string Name, Entity Issuer, Entity? Subject, Entity? Target, bool Hard = false)
{
    private Entity? _token;

    /// <summary>用于与状态视图、因果事件及底层运行时绑定的实体令牌。</summary>
    public Entity Token => _token ??= new Entity(Name, parent: null, properties: new Dictionary<string, object>
    {
        ["active"] = true,
        ["completed"] = false,
        ["accepted"] = false,
        ["granted"] = false,
        ["hard"] = Hard,
        ["public_fields"] = new[] { "active", "completed", "accepted", "granted", "text" },
        ["text"] = Name,
        ["issuer"] = Issuer,
        ["subject"] = Subject!,
        ["target"] = Target!,
        ["clause"] = this
    }).AddTags(CommonTag.Covenant);

    public TreeMember? Parent => Issuer.Parent;

    public T? Get<T>(string key)
    {
        if (key == "issuer" && typeof(T) == typeof(Entity)) return (T)(object)Issuer;
        if (key == "subject" && typeof(T) == typeof(Entity)) return (T?)(object?)Subject;
        if (key == "target" && typeof(T) == typeof(Entity)) return (T?)(object?)Target;
        if (key == "text" && typeof(T) == typeof(string)) return (T)(object)Name;
        return Token.Get<T>(key);
    }

    public T? Get<T>(string key, T defaultValue) => Token.Get(key, defaultValue);

    public static implicit operator Entity(Clause c) => c.Token;

    public static bool operator ==(Entity? a, Clause? b) => ReferenceEquals(a, b?.Token);
    public static bool operator !=(Entity? a, Clause? b) => !ReferenceEquals(a, b?.Token);
    public static bool operator ==(Clause? a, Entity? b) => ReferenceEquals(a?.Token, b);
    public static bool operator !=(Clause? a, Entity? b) => !ReferenceEquals(a?.Token, b);

    public virtual bool Equals(Clause? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

public record ProhibitionClause(
    string Name, Entity Issuer, Entity? Subject, Entity? Target,
    string Operation, IReadOnlyList<Effect> Breach, bool Hard = false)
    : Clause(Name, Issuer, Subject, Target, Hard);

public record PermissionClause(
    string Name, Entity Issuer, Entity? Subject, Entity? Target,
    string Operation = "open", bool Hard = true)
    : Clause(Name, Issuer, Subject, Target, Hard);

public record DeadlineClause(
    string Name, Entity Issuer, Entity? Subject, Entity? Target,
    string Operation, double Due, IReadOnlyList<Effect> Breach, bool Hard = false)
    : Clause(Name, Issuer, Subject, Target, Hard);

public record TriggerClause(
    string Name, Entity Issuer, string Operation, Entity? Target = null,
    IReadOnlyList<Effect>? Consequences = null, bool Hard = false)
    : Clause(Name, Issuer, null, Target, Hard)
{
    public IReadOnlyList<Effect> Breach => Consequences ?? [];
}

public record ExchangeClause(
    string Name, Entity Issuer, Entity? Subject, Entity Offered, Entity Requested, bool Hard = false)
    : Clause(Name, Issuer, Subject, Offered, Hard)
{
    public Entity Offered => Target!;
}

public record ExceptionClause(
    string Name, Entity Issuer, Entity? Subject, Entity? Target,
    Expr Condition, Clause? Original = null, bool Hard = false)
    : Clause(Name, Issuer, Subject, Target, Hard);

public record TrueNameClause(
    string Name, Entity Issuer, Entity? Subject, Entity Referent, Entity Medium, bool Hard = false)
    : Clause(Name, Issuer, Subject, Referent, Hard)
{
    public Entity Referent => Target!;
}

public record TruthClause(
    string Name, Entity Issuer, Entity? Subject, Expr Proposition,
    bool Secrecy = false, Entity? AllowedListener = null, bool Hard = true)
    : Clause(Name, Issuer, Subject, AllowedListener, Hard);

public record SelfBindingClause(
    string Name, Entity Issuer, Entity? Subject, string Operation, bool Hard = false)
    : Clause(Name, Issuer, Subject, null, Hard);

public record SubstituteClause(
    string Name, Entity Issuer, Entity? Subject, Entity Effigy, bool Hard = false)
    : Clause(Name, Issuer, Subject, Effigy, Hard)
{
    public Entity Effigy => Target!;
}

public record SealClause(
    string Name, Entity Issuer, Entity? Subject, Entity Target, Entity Medium,
    bool Release = false, double Strength = 1, bool Hard = false)
    : Clause(Name, Issuer, Subject, Target, Hard);

public record TestimonyClause(
    string Name, Entity Issuer, Entity? Listener, Expr Proposition, Entity Evidence, bool Hard = false)
    : Clause(Name, Issuer, Listener, Evidence, Hard)
{
    public Entity? Listener => Subject;
    public Entity Evidence => Target!;
}

/// <summary>文学句式是执行定义的视图；改台词不会改权限或客观事实。</summary>
public static class Patterns
{
    public static ProhibitionClause Prohibit(Entity issuer, Entity subject, string operation, Entity? target = null,
        bool hard = false, IEnumerable<Effect>? breach = null)
        => new($"不得{operation}《{target?.Name ?? "指定对象"}》", issuer, subject, target, operation, breach?.ToArray() ?? [], hard);

    public static PermissionClause Permission(Entity issuer, Entity subject, Entity target, string operation = "open")
        => new($"经{issuer.Name}许可，才可{operation}《{target.Name}》", issuer, subject, target, operation, true);

    public static DeadlineClause Deadline(Entity issuer, Entity subject, Entity target, string operation, double due, params Effect[] breach)
        => new($"在{due:g} AV前{operation}《{target.Name}》", issuer, subject, target, operation, due, breach, false);

    public static TriggerClause Trigger(Entity issuer, string operation, Entity? target, params Effect[] consequences)
        => new($"一旦{operation}《{target?.Name ?? "对象"}》，约定便应验", issuer, operation, target, consequences, false);

    public static ExchangeClause Exchange(Entity issuer, Entity subject, Entity offered, Entity requested)
        => new($"以《{offered.Name}》交换《{requested.Name}》", issuer, subject, offered, requested, false);

    public static ExceptionClause Except(Clause original, Expr until)
        => new($"满足约定条件，解除《{original.Name}》", original.Issuer, original.Subject, original.Token, until, original, false);

    public static ExceptionClause Except(Entity original, Expr until)
    {
        var clause = original.Get<Clause>("clause");
        var issuer = clause?.Issuer ?? original.Get<Entity>("issuer")!;
        var subject = clause?.Subject ?? original.Get<Entity>("subject");
        return new($"满足约定条件，解除《{original.Name}》", issuer, subject, original, until, clause, false);
    }

    public static TrueNameClause TrueName(Entity issuer, Entity subject, Entity referent, Entity nameSlip)
        => new($"以《{nameSlip.Name}》指认《{referent.Name}》", issuer, subject, referent, nameSlip, false);

    public static TruthClause Truth(Entity issuer, Entity subject, Expr proposition, bool secrecy = false, Entity? allowedListener = null)
        => new(secrecy ? "不得向约定之外的人泄露此事" : "就此命题，不得说谎", issuer, subject, proposition, secrecy, allowedListener, true);

    public static SelfBindingClause SelfBinding(Entity issuer, Entity subject, string forbiddenOperation)
        => new($"我放弃{forbiddenOperation}，换取有限增幅", issuer, subject, forbiddenOperation, false);

    public static SubstituteClause Substitute(Entity issuer, Entity subject, Entity effigy)
        => new($"以《{effigy.Name}》承担我的负荷", issuer, subject, effigy, false);

    public static SealClause Seal(Entity issuer, Entity subject, Entity target, Entity medium, bool release = false, double strength = 1)
        => new($"以《{medium.Name}》{(release ? "解开" : "封存")}《{target.Name}》", issuer, subject, target, medium, release, strength, false);

    public static TestimonyClause Testimony(Entity issuer, Entity listener, Expr proposition, Entity evidence)
        => new($"以《{evidence.Name}》支持我的陈述", issuer, listener, proposition, evidence, false);
}

/// <summary>条款、期限、见证及反制共用现有运行时；后果也必须支付并生成因果事件。</summary>
public sealed class PatternBook : IDisposable
{
    private readonly SimulationSession _session;
    private readonly HashSet<Clause> _triggered = new();
    private int _depth;
    private readonly List<Func<double?>> _timers = new();
    public List<Clause> Clauses { get; } = new();

    public PatternBook(SimulationSession session)
    {
        _session = session;
        session.Runtime.Guards.Add(Guard);
        session.Runtime.Listeners.Add(Observe);
        session.Runtime.EffectTransforms.Add(SelfBindingDiscount);
    }

    public T Add<T>(T clause) where T : Clause
    {
        if (Clauses.Contains(clause)) return clause;
        Clauses.Add(clause);
        if (clause is DeadlineClause deadline)
        {
            Func<double?> timer = () =>
            {
                if (deadline.Token.Get<bool>("active") && !deadline.Token.Get<bool>("completed"))
                    Consequence(deadline, null, "期限届满");
                return null;
            };
            _timers.Add(timer);
            // 截止时刻的普通槽仍有履约机会，下一可表示时刻才判违约。
            _session.Timeline.Schedule(Math.Max(0, Math.BitIncrement(deadline.Due) - _session.Timeline.Now), deadline.Name, timer);
        }
        return clause;
    }

    public Clause Add(Entity entity)
    {
        if (entity.Get<Clause>("clause") is { } clause) return Add(clause);
        throw new ArgumentException("Entity is not associated with any Clause", nameof(entity));
    }

    private static bool Active(Clause c, StateView view) => !Equals(view.Read(c.Token, "active"), false);

    private static bool Matches(Clause c, Intent i) =>
        (c.Subject is null || c.Subject == i.Actor)
        && (c.Target is null || c.Target == i.Target)
        && c switch
        {
            ProhibitionClause p => p.Operation == i.Effect.Op,
            PermissionClause p => p.Operation == i.Effect.Op,
            DeadlineClause d => d.Operation == i.Effect.Op,
            TriggerClause t => t.Operation == i.Effect.Op,
            SelfBindingClause s => s.Operation == i.Effect.Op,
            _ => true
        };

    private string? Guard(Intent intent, StateView view, EffectPlan plan)
    {
        foreach (var clause in Clauses)
        {
            if (view.Observer != null && !StateView.Knowledge(view.Observer).ContainsKey((clause.Token, "text"))) continue;
            if (!Active(clause, view)) continue;

            if (clause is TruthClause truth && truth.Subject == intent.Actor && intent.Claim != null)
            {
                var concerns = Equals(truth.Proposition, intent.Claim) || Equals(E("not", truth.Proposition), intent.Claim);
                if (concerns)
                {
                    if (truth.Secrecy)
                    {
                        if (!intent.IsPrivate || intent.Target != truth.AllowedListener) return truth.Name;
                    }
                    else if (!Equals(view.Value(intent.Claim), true)) return truth.Name;
                }
            }

            if (!Matches(clause, intent)) continue;

            switch (clause)
            {
                case ProhibitionClause { Hard: true } p:
                    return p.Name;
                case PermissionClause perm when !Equals(view.Read(perm.Token, "granted"), true):
                    return perm.Name;
            }
        }

        // 约束也检查言灵内部的实际变化，不能以 invoke 名称绕开 open。
        if (intent.Changes != null && intent.Effect.Op != "consequence")
        {
            foreach (var change in plan.Changes)
            {
                if ((change.Field == "sealed" && Equals(change.After, false)) || (change.Field == "opened" && Equals(change.After, true)))
                {
                    foreach (var c in Clauses)
                    {
                        if (!c.Hard || !Active(c, view)) continue;
                        if (view.Observer != null && !StateView.Knowledge(view.Observer).ContainsKey((c.Token, "text"))) continue;
                        if (c.Subject != intent.Actor || c.Target != change.Subject) continue;

                        switch (c)
                        {
                            case ProhibitionClause { Operation: "open" } p:
                                return p.Name;
                            case PermissionClause { Operation: "open" } perm when !Equals(view.Read(perm.Token, "granted"), true):
                                return perm.Name;
                        }
                    }
                }
            }
        }

        return null;
    }

    private IReadOnlyList<Effect> SelfBindingDiscount(Intent intent, StateView view, IReadOnlyList<Effect> effects)
    {
        if (!intent.Effect.Op.StartsWith("invoke_") || view.Read(intent.Actor, "self_binding") is not Entity bindingEntity) return effects;
        var binding = bindingEntity.Get<Clause>("clause") as SelfBindingClause
            ?? Clauses.OfType<SelfBindingClause>().FirstOrDefault(c => c.Token == bindingEntity);
        if (binding == null || !Equals(view.Read(binding.Token, "active"), true) || !Equals(view.Read(binding.Token, "accepted"), true)
            || binding.Operation == intent.Effect.Op) return effects;

        // 只折扣一次施展的第一笔介质支付；最多省 1 Q，且至少支付原价的一半。
        var discounted = false;
        return effects.Select(effect =>
        {
            if (!discounted && effect.Op == "spend" && effect.Args is [Entity payer, "charges", double amount])
            {
                discounted = true;
                return F("spend", payer, "charges", Math.Max(amount / 2, amount - 1));
            }
            return effect;
        }).ToArray();
    }

    public IEnumerable<Intent> Offers(Soul mind, Entity actor)
    {
        foreach (var c in Clauses)
        {
            if (!StateView.Knowledge(mind).ContainsKey((c.Token, "text"))) continue;
            var subject = c.Subject;
            var issuer = c.Issuer;
            var target = c.Target;
            var active = E("eq", E("value", c.Token, "active"), true);
            if (subject != actor && issuer != actor) continue;

            Intent Build(string op, IEnumerable<Effect> changes, IEnumerable<Expr>? requirements = null, string? speech = null) =>
                new(actor, mind, F(op, actor, target ?? c.Token), changes, label: c.Name,
                    requirements: new[] { active }.Concat(requirements ?? [])) { Definition = c.Token, Speech = speech };

            if (subject == actor && c is ProhibitionClause or DeadlineClause or TruthClause)
                yield return Build("accept_clause", [F("set", c.Token, "accepted", true)],
                    [E("eq", E("value", c.Token, "accepted"), false)], "我听清了。我会遵守这项约定。");

            switch (c)
            {
                case PermissionClause perm when issuer == actor:
                    yield return Build("grant", [F("set", perm.Token, "granted", true)],
                        [E("eq", E("value", perm.Token, "granted"), false)], "可以。但这份许可只对约定的对象有效。");
                    break;

                case ExchangeClause ex when subject == actor:
                    yield return Build("exchange",
                        [F("move", ex.Offered, actor), F("move", ex.Requested, issuer), F("set", ex.Token, "completed", true), F("set", ex.Token, "active", false)],
                        [E("eq", E("value", ex.Offered, "$parent"), issuer), E("eq", E("value", ex.Requested, "$parent"), actor)],
                        "按你提出的条件交换。我会记住这次承诺。");
                    break;

                case SelfBindingClause sb when subject == actor:
                    yield return Build("bind_self",
                        [F("set", actor, "self_binding", sb.Token), F("set", sb.Token, "accepted", true)],
                        [E("eq", E("value", sb.Token, "accepted"), false)]);
                    break;

                case TrueNameClause tn when subject == actor:
                    yield return Build("name",
                        [F("relate", actor, "Designates", tn.Referent), F("set", tn.Token, "completed", true)],
                        [E("eq", E("value", tn.Medium, "referent"), tn.Referent), E("eq", E("value", tn.Medium, "$parent"), actor), E("eq", E("value", tn.Token, "completed"), false)]);
                    break;

                case SubstituteClause sub when subject == actor:
                    yield return Build("prepare_substitute",
                        [F("set", actor, "substitute", sub.Effigy)],
                        [E("ne", E("value", actor, "substitute"), sub.Effigy), E("eq", E("value", sub.Effigy, "$parent"), actor), E("gte", E("value", sub.Effigy, "charges"), 1d)]);
                    break;

                case SealClause seal when subject == actor:
                    yield return Build(seal.Release ? "invoke_release" : "invoke_seal",
                        [F("spend", seal.Medium, "charges", seal.Strength), F("set", seal.Target!, "sealed", !seal.Release), F("set", seal.Target!, "seal_source", seal.Release ? StateView.Unknown : seal.Token)],
                        [E("eq", E("value", seal.Target!, "sealed"), seal.Release), E("eq", E("value", seal.Medium, "$parent"), actor),
                         E("any", E("eq", E("value", seal.Target!, "$parent"), E("value", actor, "$parent")), E("contains", E("value", actor, "rel:Designates"), seal.Target!))]);
                    break;

                case TestimonyClause test when issuer == actor:
                    var claim = test.Proposition;
                    var receiverSoul = test.Listener is Character receiver && receiver.Mind != null ? receiver.Mind : mind;
                    yield return new Intent(actor, mind, F("testify", actor, test.Listener!),
                        [F("set", test.Token, "completed", true), F("observe", receiverSoul, test.Token, "proposition", E("quote", claim))],
                        label: test.Name, requirements: [active, E("eq", E("value", test.Token, "completed"), false)])
                    { Definition = test.Token, Claim = claim, Speech = $"我把《{test.Evidence.Name}》交出来。它能支持的只有这一点，其余仍需查证。" };
                    break;
            }
        }
    }

    private void Observe(Entity ev, ActionContext? context)
    {
        if (!ev.Succeeded() || ev.Intent() is not { } intent) return;
        _depth++;
        try
        {
            if (intent.Effect.Op == "invoke_seal")
            {
                var seal = intent.Definition?.Get<Clause>("clause") as SealClause
                    ?? Clauses.OfType<SealClause>().FirstOrDefault(s => s.Token == intent.Definition);
                if (seal != null) MaintainSeal(seal);
            }

            foreach (var c in Clauses.ToArray())
            {
                if (!c.Token.Get<bool>("active") || _triggered.Contains(c)) continue;

                switch (c)
                {
                    case ExceptionClause exc when Equals(new StateView().Value(exc.Condition), true):
                        Resolve(exc, ev, [F("set", exc.Target!, "active", false), F("set", exc.Token, "active", false)], "约定条件成立，条款解除");
                        continue;
                }

                if (Matches(c, intent))
                {
                    switch (c)
                    {
                        case DeadlineClause d when _session.Timeline.Now <= d.Due:
                            Resolve(d, ev, [F("set", d.Token, "completed", true), F("set", d.Token, "active", false)], "义务已经履行");
                            break;
                        case TriggerClause or ProhibitionClause:
                            Consequence(c, ev, "约定的后果发生");
                            break;
                        case SelfBindingClause sb when sb.Token.Get<bool>("accepted"):
                            Resolve(sb, ev, [F("set", sb.Token, "active", false), F("set", intent.Actor, "self_binding", null!)], "自缚被打破，增幅终止");
                            break;
                    }
                }
            }
        }
        finally { if (--_depth == 0) _triggered.Clear(); }
    }

    private static IReadOnlyList<Effect> GetBreachEffects(Clause c) => c switch
    {
        ProhibitionClause p => p.Breach,
        DeadlineClause d => d.Breach,
        TriggerClause t => t.Consequences ?? [],
        _ => []
    };

    private void Consequence(Clause c, Entity? cause, string text)
    {
        var effects = GetBreachEffects(c);
        if (effects.Count == 0)
        {
            if (c is DeadlineClause)
                Resolve(c, cause, [F("set", c.Token, "active", false)], text);
            return;
        }
        var issuer = c.Issuer;
        var prepared = PrepareConsequence(c, issuer, effects);
        if (!prepared.Plan.Succeeded)
        {
            if (c is DeadlineClause)
                Resolve(c, cause, [F("set", c.Token, "active", false)], "期限结束，后果因预算不足未能生效");
            return;
        }
        var changes = new List<Effect> { F("spend", issuer, "charges", prepared.Cost) };
        changes.AddRange(effects);
        if (c is DeadlineClause) changes.Add(F("set", c.Token, "active", false));
        var result = Resolve(c, cause, changes, text);
        if (result.Result?.Succeeded != true && c is DeadlineClause)
            Resolve(c, cause, [F("set", c.Token, "active", false)], "期限结束，后果因预算不足未能生效");
    }

    private (EffectPlan Plan, double Cost) PrepareConsequence(Clause clause, Entity issuer, IEnumerable<Effect> effects, StateView? view = null)
    {
        var mind = issuer is Character character ? character.Mind! : issuer.Get<Soul>("mind")!;
        var previewIntent = new Intent(issuer, mind, F("consequence", issuer, clause.Token), effects);
        var preview = _session.Runtime.Prepare(previewIntent, view);
        if (!preview.Succeeded) return (preview, 0);
        var cost = Math.Max(1, Balance.ConsequenceBudget(preview.Changes));
        var finalEffects = new[] { F("spend", issuer, "charges", cost) }.Concat(effects);
        return (_session.Runtime.Prepare(new Intent(issuer, mind, F("consequence", issuer, clause.Token), finalEffects), view), cost);
    }

    private ActionResolution Resolve(Clause c, Entity? cause, IEnumerable<Effect> changes, string text)
    {
        _triggered.Add(c);
        var issuer = c.Issuer;
        var mind = issuer is Character ch ? ch.Mind! : issuer.Get<Soul>("mind")!;
        var intent = new Intent(issuer, mind, F("consequence", issuer, c.Token), changes, 1, text) { Definition = c.Token };
        return _session.Runtime.ResolveDetailed(intent, new ActionContext(Cause: cause ?? c.Token));
    }

    public double Pressure(Intent intent)
    {
        var view = new StateView(intent.Mind);
        return Clauses.Where(c => Equals(view.Read(c.Token, "accepted"), true) && Equals(view.Read(c.Token, "active"), true) && Matches(c, intent))
            .Sum(c => c is ProhibitionClause ? -3.0 : 1.0);
    }

    public EffectPlan ForecastConsequences(Intent intent, EffectPlan plan)
    {
        foreach (var clause in Clauses.Where(c => StateView.Knowledge(plan.View.Observer ?? intent.Mind).ContainsKey((c.Token, "text"))
            && Active(c, plan.View) && Matches(c, intent)
            && c is ProhibitionClause or TriggerClause))
        {
            var issuer = clause.Issuer;
            var breach = GetBreachEffects(clause);
            var prepared = PrepareConsequence(clause, issuer, breach, plan.View);
            if (!prepared.Plan.Succeeded) continue;
            plan.Changes.AddRange(prepared.Plan.Changes);
            foreach (var change in prepared.Plan.Changes) plan.View.Write(change.Subject, change.Field, change.After);
            plan.Uncertain |= prepared.Plan.Uncertain;
        }
        return plan;
    }

    private void MaintainSeal(SealClause seal)
    {
        var target = seal.Target;
        var medium = seal.Medium;
        Func<double?> timer = () =>
        {
            if (target == null || target.Get<Entity>("seal_source") != seal.Token) return null;
            if ((seal.Subject != null && medium.Parent != seal.Subject) || medium.Get<double>("charges") < seal.Strength)
            {
                Resolve(seal, null, [F("set", target, "sealed", false), F("set", target, "seal_source", (object?)null!)], "封存介质耗尽，封印解除");
                return null;
            }
            Resolve(seal, null, [F("spend", medium, "charges", seal.Strength)], "维持封存");
            return Balance.CandleMaintenanceIntervalAv;
        };
        _timers.Add(timer);
        _session.Timeline.Schedule(Balance.CandleMaintenanceIntervalAv, seal.Name, timer);
    }

    public void Dispose()
    {
        _session.Runtime.Guards.Remove(Guard);
        _session.Runtime.Listeners.Remove(Observe);
        _session.Runtime.EffectTransforms.Remove(SelfBindingDiscount);
        _session.Timeline.Cancel(task => _timers.Contains(task.Action));
    }
}
