using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Content;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Core.Patterns;

public enum LiteraryPattern { Prohibition, Permission, Deadline, Trigger, Exchange, Exception, TrueName, Truth, SelfBinding, Substitute, Seal, Testimony }

/// <summary>文学句式是执行定义的视图；改台词不会改权限或客观事实。</summary>
public static class Patterns
{
    private static Entity Make(LiteraryPattern kind, string name, Entity issuer, Entity? subject, Entity? target = null)
    {
        return new Entity(name, issuer.Parent, properties: new Dictionary<string, object>
        {
            ["pattern"] = kind, ["issuer"] = issuer, ["subject"] = subject!, ["target"] = target!,
            ["active"] = true, ["completed"] = false, ["accepted"] = false, ["hard"] = false,
            ["public_fields"] = new[] { "active", "completed", "accepted", "granted", "text" },
            ["text"] = name, ["granted"] = false
        });
    }
    public static Entity Prohibit(Entity issuer, Entity subject, string operation, Entity? target = null,
        bool hard = false, IEnumerable<Effect>? breach = null)
    {
        var rule = Make(LiteraryPattern.Prohibition, $"不得{operation}《{target?.Name ?? "指定对象"}》", issuer, subject, target);
        rule.Set("operation", operation); rule.Set("hard", hard);
        rule.Set("breach", breach?.ToArray() ?? []); return rule;
    }
    public static Entity Permission(Entity issuer, Entity subject, Entity target, string operation = "open")
    {
        var rule = Make(LiteraryPattern.Permission, $"经{issuer.Name}许可，才可{operation}《{target.Name}》", issuer, subject, target);
        rule.Set("operation", operation); rule.Set("hard", true); return rule;
    }
    public static Entity Deadline(Entity issuer, Entity subject, Entity target, string operation, double due, params Effect[] breach)
    {
        var rule = Make(LiteraryPattern.Deadline, $"在{due:g} AV前{operation}《{target.Name}》", issuer, subject, target);
        rule.Set("operation", operation); rule.Set("due", due); rule.Set("breach", breach); return rule;
    }
    public static Entity Trigger(Entity issuer, string operation, Entity? target, params Effect[] consequences)
    {
        var rule = Make(LiteraryPattern.Trigger, $"一旦{operation}《{target?.Name ?? "对象"}》，约定便应验", issuer, null, target);
        rule.Set("operation", operation); rule.Set("breach", consequences); return rule;
    }
    public static Entity Exchange(Entity issuer, Entity subject, Entity offered, Entity requested)
    {
        var rule = Make(LiteraryPattern.Exchange, $"以《{offered.Name}》交换《{requested.Name}》", issuer, subject, offered);
        rule.Set("requested", requested); return rule;
    }
    public static Entity Except(Entity original, Expr until)
    {
        var rule = Make(LiteraryPattern.Exception, $"满足约定条件，解除《{original.Name}》", original.Get<Entity>("issuer")!, original.Get<Entity>("subject"), original);
        rule.Set("condition", until); return rule;
    }
    public static Entity TrueName(Entity issuer, Entity subject, Entity referent, Entity nameSlip)
    {
        var word = Make(LiteraryPattern.TrueName, $"以《{nameSlip.Name}》指认《{referent.Name}》", issuer, subject, referent);
        word.Set("medium", nameSlip); return word;
    }
    public static Entity Truth(Entity issuer, Entity subject, Expr proposition, bool secrecy = false, Entity? allowedListener = null)
    {
        var rule = Make(LiteraryPattern.Truth, secrecy ? "不得向约定之外的人泄露此事" : "就此命题，不得说谎", issuer, subject, allowedListener);
        rule.Set("proposition", proposition); rule.Set("secrecy", secrecy); rule.Set("hard", true); return rule;
    }
    public static Entity SelfBinding(Entity issuer, Entity subject, string forbiddenOperation)
    {
        var rule = Make(LiteraryPattern.SelfBinding, $"我放弃{forbiddenOperation}，换取有限增幅", issuer, subject);
        rule.Set("operation", forbiddenOperation); return rule;
    }
    public static Entity Substitute(Entity issuer, Entity subject, Entity effigy)
    {
        var word = Make(LiteraryPattern.Substitute, $"以《{effigy.Name}》承担我的负荷", issuer, subject, effigy); return word;
    }
    public static Entity Seal(Entity issuer, Entity subject, Entity target, Entity medium, bool release = false, double strength = 1)
    {
        var word = Make(LiteraryPattern.Seal, $"以《{medium.Name}》{(release ? "解开" : "封存")}《{target.Name}》", issuer, subject, target);
        word.Set("medium", medium); word.Set("release", release); word.Set("strength", strength); return word;
    }
    public static Entity Testimony(Entity issuer, Entity listener, Expr proposition, Entity evidence)
    {
        var rule = Make(LiteraryPattern.Testimony, $"以《{evidence.Name}》支持我的陈述", issuer, listener, evidence);
        rule.Set("proposition", proposition); return rule;
    }
}

/// <summary>条款、期限、见证及反制共用现有运行时；后果也必须支付并生成因果事件。</summary>
public sealed class PatternBook : IDisposable
{
    private readonly SimulationSession _session;
    private readonly HashSet<Entity> _triggered = new();
    private int _depth;
    private readonly List<Func<double?>> _timers = new();
    public List<Entity> Clauses { get; } = new();
    public PatternBook(SimulationSession session)
    {
        _session = session;
        session.Runtime.Guards.Add(Guard);
        session.Runtime.Listeners.Add(Observe);
        session.Runtime.EffectTransforms.Add(SelfBindingDiscount);
    }
    public Entity Add(Entity clause)
    {
        if (Clauses.Contains(clause)) return clause;
        Clauses.Add(clause);
        if (clause.Get<LiteraryPattern>("pattern") == LiteraryPattern.Deadline)
        {
            Func<double?> timer = () =>
            {
                if (clause.Get<bool>("active") && !clause.Get<bool>("completed")) Consequence(clause, null, "期限届满");
                return null;
            };
            _timers.Add(timer);
            // 截止时刻的普通槽仍有履约机会，下一可表示时刻才判违约。
            _session.Timeline.Schedule(Math.Max(0, Math.BitIncrement(clause.Get<double>("due")) - _session.Timeline.Now), clause.Name, timer);
        }
        return clause;
    }
    private static bool Active(Entity c, StateView view) => !Equals(view.Read(c, "active"), false);
    private static bool Matches(Entity c, Intent i) => (c.Get<Entity>("subject") is not { } s || s == i.Actor)
        && (c.Get<Entity>("target") is not { } t || t == i.Target)
        && (c.Get<string>("operation") is not { } op || op == i.Effect.Op);
    public string? Guard(Intent intent, StateView view)
    {
        foreach (var clause in Clauses)
        {
            if (view.Observer != null && !StateView.Knowledge(view.Observer).ContainsKey((clause, "text"))) continue;
            if (!Active(clause, view)) continue;
            var kind = clause.Get<LiteraryPattern>("pattern");
            if (kind == LiteraryPattern.Truth && clause.Get<Entity>("subject") == intent.Actor && intent.Claim != null)
            {
                var proposition = clause.Get<Expr>("proposition")!;
                var concerns = Equals(proposition, intent.Claim) || Equals(E("not", proposition), intent.Claim);
                if (!concerns) continue;
                if (clause.Get<bool>("secrecy"))
                {
                    if (!intent.IsPrivate || intent.Target != clause.Get<Entity>("target")) return clause.Name;
                }
                else if (!Equals(view.Value(intent.Claim), true)) return clause.Name;
            }
            if (!Matches(clause, intent)) continue;
            if (kind == LiteraryPattern.Prohibition && clause.Get<bool>("hard")) return clause.Name;
            if (kind == LiteraryPattern.Permission && !Equals(view.Read(clause, "granted"), true)) return clause.Name;
        }
        // 约束也检查言灵内部的实际变化，不能以 invoke 名称绕开 open。
        if (intent.Changes != null && intent.Effect.Op != "consequence")
            foreach (var change in _session.Runtime.Prepare(intent, view).Changes)
                if ((change.Field == "sealed" && Equals(change.After, false)) || (change.Field == "opened" && Equals(change.After, true)))
                    foreach (var c in Clauses.Where(c => c.Get<bool>("hard") && Active(c, view)
                        && (view.Observer == null || StateView.Knowledge(view.Observer).ContainsKey((c, "text")))
                        && c.Get<Entity>("subject") == intent.Actor && c.Get<Entity>("target") == change.Subject && c.Get<string>("operation") == "open"))
                        if (c.Get<LiteraryPattern>("pattern") == LiteraryPattern.Prohibition || !Equals(view.Read(c, "granted"), true)) return c.Name;
        return null;
    }
    private IReadOnlyList<Effect> SelfBindingDiscount(Intent intent, StateView view, IReadOnlyList<Effect> effects)
    {
        if (!intent.Effect.Op.StartsWith("invoke_") || view.Read(intent.Actor, "self_binding") is not Entity binding
            || !Equals(view.Read(binding, "active"), true) || !Equals(view.Read(binding, "accepted"), true)
            || binding.Get<string>("operation") == intent.Effect.Op) return effects;
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
            if (!StateView.Knowledge(mind).ContainsKey((c, "text"))) continue;
            var subject = c.Get<Entity>("subject");
            var issuer = c.Get<Entity>("issuer")!;
            var target = c.Get<Entity>("target");
            var active = E("eq", E("value", c, "active"), true);
            if (subject != actor && issuer != actor) continue;
            var kind = c.Get<LiteraryPattern>("pattern");
            Intent Build(string op, IEnumerable<Effect> changes, IEnumerable<Expr>? requirements = null, string? speech = null) =>
                new(actor, mind, F(op, actor, target ?? c), changes, label: c.Name,
                    requirements: new[] { active }.Concat(requirements ?? [])) { Definition = c, Speech = speech };
            if (subject == actor && kind is LiteraryPattern.Prohibition or LiteraryPattern.Deadline or LiteraryPattern.Truth)
                yield return Build("accept_clause", [F("set", c, "accepted", true)],
                    [E("eq", E("value", c, "accepted"), false)], "我听清了。我会遵守这项约定。");
            switch (kind)
            {
                case LiteraryPattern.Permission when issuer == actor:
                    yield return Build("grant", [F("set", c, "granted", true)], [E("eq", E("value", c, "granted"), false)], "可以。但这份许可只对约定的对象有效。"); break;
                case LiteraryPattern.Exchange when subject == actor:
                    var requested = c.Get<Entity>("requested")!;
                    yield return Build("exchange", [F("move", target!, actor), F("move", requested, issuer), F("set", c, "completed", true), F("set", c, "active", false)],
                        [E("eq", E("value", target!, "$parent"), issuer), E("eq", E("value", requested, "$parent"), actor)], "按你提出的条件交换。我会记住这次承诺。"); break;
                case LiteraryPattern.SelfBinding when subject == actor:
                    yield return Build("bind_self", [F("set", actor, "self_binding", c), F("set", c, "accepted", true)],
                        [E("eq", E("value", c, "accepted"), false)]); break;
                case LiteraryPattern.TrueName when subject == actor:
                    var slip = c.Get<Entity>("medium")!;
                    yield return Build("name", [F("relate", actor, "Designates", target!), F("set", c, "completed", true)],
                        [E("eq", E("value", slip, "referent"), target!), E("eq", E("value", slip, "$parent"), actor), E("eq", E("value", c, "completed"), false)]); break;
                case LiteraryPattern.Substitute when subject == actor:
                    yield return Build("prepare_substitute", [F("set", actor, "substitute", target!)],
                        [E("ne", E("value", actor, "substitute"), target!), E("eq", E("value", target!, "$parent"), actor), E("gte", E("value", target!, "charges"), 1d)]); break;
                case LiteraryPattern.Seal when subject == actor:
                    var medium = c.Get<Entity>("medium")!;
                    var release = c.Get<bool>("release");
                    var strength = c.Get<double>("strength", 1);
                    yield return Build(release ? "invoke_release" : "invoke_seal",
                        [F("spend", medium, "charges", strength), F("set", target!, "sealed", !release), F("set", target!, "seal_source", release ? null! : c)],
                        [E("eq", E("value", target!, "sealed"), release), E("eq", E("value", medium, "$parent"), actor),
                         E("any", E("eq", E("value", target!, "$parent"), E("value", actor, "$parent")), E("contains", E("value", actor, "rel:Designates"), target!))]); break;
                case LiteraryPattern.Testimony when issuer == actor:
                    var claim = c.Get<Expr>("proposition")!;
                    yield return new Intent(actor, mind, F("testify", actor, subject!),
                        [F("set", c, "completed", true), F("observe", subject is Character receiver ? receiver.Mind! : mind, c, "proposition", E("quote", claim))],
                        label: c.Name, requirements: [active, E("eq", E("value", c, "completed"), false)])
                        { Definition = c, Claim = claim, Speech = $"我把《{target!.Name}》交出来。它能支持的只有这一点，其余仍需查证。" }; break;
            }
        }
    }

    private void Observe(Entity ev, IReadOnlyDictionary<string, object>? context)
    {
        if (!ev.Get<bool>("succeeded") || ev.Get<Intent>("intent") is not { } intent) return;
        _depth++;
        try
        {
            if (intent.Effect.Op == "invoke_seal" && intent.Definition is { } seal) MaintainSeal(seal);
            foreach (var c in Clauses.ToArray())
            {
                if (!c.Get<bool>("active") || _triggered.Contains(c)) continue;
                var kind = c.Get<LiteraryPattern>("pattern");
                if (kind == LiteraryPattern.Exception && Equals(new StateView().Value(c.Get<Expr>("condition")!), true))
                    Resolve(c, ev, [F("set", c.Get<Entity>("target")!, "active", false), F("set", c, "active", false)], "约定条件成立，条款解除");
                else if (Matches(c, intent))
                {
                    if (kind == LiteraryPattern.Deadline && _session.Timeline.Now <= c.Get<double>("due"))
                        Resolve(c, ev, [F("set", c, "completed", true), F("set", c, "active", false)], "义务已经履行");
                    else if (kind is LiteraryPattern.Trigger or LiteraryPattern.Prohibition)
                        Consequence(c, ev, "约定的后果发生");
                    else if (kind == LiteraryPattern.SelfBinding && c.Get<bool>("accepted"))
                        Resolve(c, ev, [F("set", c, "active", false), F("set", intent.Actor, "self_binding", null!)], "自缚被打破，增幅终止");
                }
            }
        }
        finally { if (--_depth == 0) _triggered.Clear(); }
    }
    private void Consequence(Entity c, Entity? cause, string text)
    {
        var effects = c.Get<Effect[]>("breach") ?? [];
        if (effects.Length == 0)
        {
            if (c.Get<LiteraryPattern>("pattern") == LiteraryPattern.Deadline)
                Resolve(c, cause, [F("set", c, "active", false)], text);
            return;
        }
        var issuer = c.Get<Entity>("issuer")!;
        var changes = new List<Effect> { F("spend", issuer, "charges", 1d) };
        changes.AddRange(effects);
        if (c.Get<LiteraryPattern>("pattern") == LiteraryPattern.Deadline) changes.Add(F("set", c, "active", false));
        Resolve(c, cause, changes, text);
    }
    private void Resolve(Entity c, Entity? cause, IEnumerable<Effect> changes, string text)
    {
        _triggered.Add(c);
        var issuer = c.Get<Entity>("issuer")!;
        var mind = issuer is Character ch ? ch.Mind! : issuer.Get<Soul>("mind")!;
        var intent = new Intent(issuer, mind, F("consequence", issuer, c), changes, 1, text) { Definition = c };
        _session.Runtime.ResolveDetailed(intent, new Dictionary<string, object> { ["cause"] = cause ?? c });
    }
    public float Pressure(Intent intent)
    {
        var view = new StateView(intent.Mind);
        return Clauses.Where(c => Equals(view.Read(c, "accepted"), true) && Equals(view.Read(c, "active"), true) && Matches(c, intent))
            .Sum(c => c.Get<LiteraryPattern>("pattern") == LiteraryPattern.Prohibition ? -3f : 1f);
    }
    public EffectPlan ForecastConsequences(Intent intent, EffectPlan plan)
    {
        foreach (var clause in Clauses.Where(c => StateView.Knowledge(intent.Mind).ContainsKey((c, "text"))
            && Active(c, plan.View) && Matches(c, intent)
            && c.Get<LiteraryPattern>("pattern") is LiteraryPattern.Prohibition or LiteraryPattern.Trigger))
        {
            var issuer = clause.Get<Entity>("issuer")!;
            var effects = new[] { F("spend", issuer, "charges", 1d) }.Concat(clause.Get<Effect[]>("breach") ?? []);
            var consequence = new Intent(issuer, intent.Mind, F("consequence", issuer, clause), effects);
            var next = _session.Runtime.Prepare(consequence, plan.View);
            if (!next.Succeeded) continue;
            plan.Changes.AddRange(next.Changes);
            foreach (var change in next.Changes) plan.View.Write(change.Subject, change.Field, change.After);
            plan.Uncertain |= next.Uncertain;
        }
        return plan;
    }
    private void MaintainSeal(Entity clause)
    {
        var target = clause.Get<Entity>("target")!;
        var medium = clause.Get<Entity>("medium")!;
        Func<double?> timer = () =>
        {
            if (target.Get<Entity>("seal_source") != clause) return null;
            if (medium.Parent != clause.Get<Entity>("subject") || medium.Get<double>("charges") < clause.Get<double>("strength", 1))
            {
                Resolve(clause, null, [F("set", target, "sealed", false), F("set", target, "seal_source", null!)], "封存介质耗尽，封印解除");
                return null;
            }
            Resolve(clause, null, [F("spend", medium, "charges", clause.Get<double>("strength", 1))], "维持封存");
            return Balance.CandleMaintenanceIntervalAv;
        };
        _timers.Add(timer);
        _session.Timeline.Schedule(Balance.CandleMaintenanceIntervalAv, clause.Name, timer);
    }
    public void Dispose()
    {
        _session.Runtime.Guards.Remove(Guard); _session.Runtime.Listeners.Remove(Observe);
        _session.Runtime.EffectTransforms.Remove(SelfBindingDiscount);
        _session.Timeline.Cancel(task => _timers.Contains(task.Action));
    }
}
