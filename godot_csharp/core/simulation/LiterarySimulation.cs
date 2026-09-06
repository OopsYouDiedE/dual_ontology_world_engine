using DualOntologyWorldEngine.Core.Content;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Patterns;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Core.Simulation;

/// <summary>默认规则怪谈/群像内容组合根。作品只需装配场景、人物、条款与物品。</summary>
public sealed class LiterarySimulation : IDisposable
{
    public Entity World { get; }
    public SimulationSession Session { get; }
    public PatternBook Rules { get; }
    public NarrativeDynamics Narrative { get; }
    public List<Character> Characters { get; } = new();
    private readonly Dictionary<Entity, Intent[]> _candidates = new();
    private readonly List<Action> _cleanup = new();
    public LiterarySimulation(string name)
    {
        World = new Entity(name);
        Session = new SimulationSession(World);
        Session.Runtime.DeclarativeOnly = true;
        Rules = new PatternBook(Session);
        Narrative = new NarrativeDynamics(Session);
        _cleanup.Add(HauntedItems.Install(Session));
        Session.Runtime.WitnessesCollector = (ev, _) =>
        {
            var intent = ev.Intent()!;
            return Characters.Where(ch => ch == intent.Actor ||
                (ch.Parent == intent.Actor.Parent && (!intent.IsPrivate || intent.Target == ch))).Select(ch => ch.Mind!);
        };
        Session.Runtime.Listeners.Add(PerceiveEvent);
        Session.Runtime.EffectTransforms.Add(Deflect);
    }
    public Entity Scene(string name) => new(name, World, new Dictionary<string, int> { ["Scene"] = 1 });
    public Passage Connect(string name, Entity from, Entity to, Entity? gate = null, double cost = Balance.BasicActionAv) =>
        new(name, from, to, cost, parent: World)
        {
            Requirements = gate == null ? [] : [E("eq", E("value", gate, "sealed"), false), E("eq", E("value", gate, "opened"), true)]
        };
    public Entity Anchor(string name, Entity scene)
    {
        var anchor = new Entity(name, scene, properties: new Dictionary<string, object>
        {
            ["charges"] = Balance.OccultAnchorCapacity, ["public_fields"] = new[] { "charges" }
        });
        anchor.Set("mind", new Soul(name + "的执行源"));
        return anchor;
    }
    public Character Person(string name, Entity scene, Entity goal, string field, object value,
        PersonalityProfile personality = PersonalityProfile.Cautious, Entity? attachment = null, Expr? secret = null, string tone = "克制", bool guardSecret = false)
    {
        var actor = new Character(name, scene);
        var mind = new Soul(name + "的心智"); actor.Bind(mind);
        actor.Set("personality", personality); actor.Set("goal_target", goal); actor.Set("goal_field", field); actor.Set("goal_value", value);
        actor.Set("attachment", attachment!); actor.Set("secret", secret!); actor.Set("tone", tone);
        actor.Set("guard_secret", guardSecret);
        actor.Set("marked", false); actor.Set("charges", Balance.OccultAnchorCapacity);
        actor.Set("public_fields", new[] { "marked", "statement", "strain" });
        if (secret?.Op == "eq" && secret.Args is [Expr { Op: "value", Args: [Entity subject, string beliefField] }, var believed])
            StateView.Learn(mind, subject, beliefField, believed, actor);
        Characters.Add(actor);
        mind.InstallStep((soul, body, env, context) =>
        {
            Perceive(actor);
            var actions = BasicOffers(actor).Concat(HauntedItems.Offers(soul, body, env, context))
                .Concat(Rules.Offers(soul, body)).Concat(LiteraryDialogue.Offers(soul, body, env, context))
                .Concat(Narrative.Offers(actor)).ToArray();
            _candidates[body] = actions;
            // 无值得执行的行动时只保留调度槽，不把每次重试写成新的文学事件。
            return actions.Any(action => action.Effect.Op != "wait" && Score(action) > 0) ? actions : [];
        });
        Perceive(actor); return actor;
    }
    public Entity Object(string name, Entity scene, bool sealedState = false, string? evidence = null, bool occultEvidence = false)
    {
        return new Entity(name, scene, properties: new Dictionary<string, object>
        {
            ["sealed"] = sealedState, ["opened"] = false, ["danger"] = 0d, ["marked"] = false,
            ["evidence"] = evidence!, ["occult_evidence"] = occultEvidence,
            ["public_fields"] = new[] { "sealed", "opened", "danger", "marked", "protected", "warded", "illuminated", "revealed", "occult_evidence" }
        });
    }
    public void Start()
    {
        foreach (var actor in Characters)
            Session.Activate(actor, actor.Mind!, idleCost: Balance.BasicActionAv, utility: Score);
    }
    public int Run(double until, int maxSteps = 1000) => Session.Timeline.RunUntil(until, maxSteps);
    public void Perceive(Character actor)
    {
        var mind = actor.Mind!;
        var visible = (actor.Parent?.Items.OfType<Entity>() ?? []).Concat(actor.Items.OfType<Entity>()).Distinct();
        foreach (var entity in visible)
        {
            StateView.Learn(mind, entity, "$parent", entity.Parent, entity);
            foreach (var field in entity.Get<string[]>("public_fields") ?? [])
                StateView.Learn(mind, entity, field, StateView.Actual(entity, field), entity);
        }
        foreach (var clause in Rules.Clauses.Where(c => c.Parent == actor.Parent || c.Get<Entity>("issuer") == actor))
            foreach (var field in clause.Get<string[]>("public_fields") ?? [])
                StateView.Learn(mind, clause, field, StateView.Actual(clause, field), clause);
        foreach (var offer in Narrative.SharedOffers(actor))
            StateView.Learn(mind, offer.Definition!, "used", offer.Definition!.Get<bool>("used"), offer.Actor);
    }
    private IEnumerable<Intent> BasicOffers(Character actor)
    {
        var mind = actor.Mind!;
        yield return new Intent(actor, mind, F("wait", actor), [], label: "暂时观察局势");
        foreach (var target in actor.Parent?.Items.OfType<Entity>().Where(e => e.HasProperty("sealed")) ?? [])
        {
            yield return new Intent(actor, mind, F("open", actor, target), [F("set", target, "opened", true)],
                label: $"打开《{target.Name}》", requirements: [E("eq", E("value", target, "sealed"), false), E("eq", E("value", target, "opened"), false)]);
            yield return new Intent(actor, mind, F("inspect", actor, target),
                [F("observe", mind, target, "evidence"), F("relate", actor, "Examined", target)], 20,
                $"调查《{target.Name}》", [E("not", E("contains", E("value", actor, "rel:Examined"), target)),
                    E("any", E("eq", E("value", target, "occult_evidence"), false), E("eq", E("value", target, "illuminated"), true), E("eq", E("value", target, "revealed"), true))]);
        }
        yield return new Intent(actor, mind, F("rest", actor), [F("spend", actor, "strain", 1d)], 20, "暂且坐下，整理呼吸",
            [E("gte", E("value", actor, "strain"), 1d)]);
        foreach (var passage in TopologySystem.CollectPassages(World).Where(p => p.Condition == null))
            if (actor.CurrentScene is { } current && passage.Other(current) is { } destination)
                yield return new Intent(actor, mind, F("move", actor, destination), [F("move", actor, destination)], passage.CostFrom(current),
                    $"前往{destination.Name}", new[] { E("eq", E("value", actor, "$parent"), current) }.Concat(passage.Requirements));
    }
    public EffectPlan Predict(Intent intent) => Rules.ForecastConsequences(intent, Session.Runtime.Prepare(intent, new StateView(intent.Mind)));
    public double Score(Intent action)
    {
        if (action.Effect.Op == "wait") return 0;
        var view = new StateView(action.Mind);
        var plan = Rules.ForecastConsequences(action, Session.Runtime.Prepare(action, view));
        if (!plan.Succeeded) return -1000;
        var score = ImmediateValue(action, plan);
        // 只查看一步准备后的下一行动。复用相同效果，不在分支调用真实闭包或世界 RNG。
        var future = 0d;
        var available = 0d;
        foreach (var next in _candidates.GetValueOrDefault(action.Actor, []).Concat(Narrative.SharedOffers(action.Actor)).Take(64))
        {
            if (next == action) continue;
            double Benefit(EffectPlan result) => next.Actor == action.Actor ? ImmediateValue(next, result)
                : Value(action.Actor, result) + Narrative.Value(action.Actor, result);
            var current = Rules.ForecastConsequences(next, Session.Runtime.Prepare(next, view));
            if (current.Succeeded) available = Math.Max(available, Benefit(current));
            var following = Rules.ForecastConsequences(next, Session.Runtime.Prepare(next, plan.View));
            if (following.Succeeded)
                future = Math.Max(future, Benefit(following));
        }
        return score + .65 * (future - available);
    }
    private double ImmediateValue(Intent action, EffectPlan plan)
    {
        if (action.Effect.Op == "wait") return 0;
        var score = Value(action.Actor, plan) + Narrative.Value(action, plan) - action.MotionCost / 20d + Rules.Pressure(action);
        var weights = Balance.PersonalityWeights[action.Actor.Get<PersonalityProfile>("personality")];
        if (action.Effect.Op == "state_claim")
            score += action.Actor.Get<bool>("guard_secret") ? -weights["safety"] * 2 : weights["truth"] + weights["image"] * .25;
        if (action.Effect.Op == "challenge") score += weights["truth"] + weights["autonomy"] * .5;
        if (action.Effect.Op == "ask") score += weights["truth"];
        if (action.Effect.Op is "silence" or "conceal") score += action.Actor.Get<bool>("guard_secret") ? weights["safety"] : weights["autonomy"] * .25;
        if (action.Effect.Op == "retract") score += weights["safety"] - weights["image"] * .5;
        if (action.Claim != null && action.Actor.Get<bool>("guard_secret")) score -= weights["safety"] * 3;
        if (action.Effect.Op is "inspect" or "reveal" && plan.Uncertain)
            score += Balance.PersonalityWeights[action.Actor.Get<PersonalityProfile>("personality")]["truth"] * 2;
        return score - (plan.Uncertain ? .5 : 0);
    }
    private static double Value(Entity actor, EffectPlan plan)
    {
        var weights = Balance.PersonalityWeights[actor.Get<PersonalityProfile>("personality")];
        var goal = actor.Get<Entity>("goal_target");
        var field = actor.Get<string>("goal_field");
        var desired = actor.Get<object>("goal_value");
        double value = 0;
        foreach (var c in plan.Changes)
        {
            if (c.Subject == goal && c.Field == field && c.Before != StateView.Unknown && c.After != StateView.Unknown)
                value += weights["goal"] * 3 * ((StateView.Same(c.After, desired) ? 1 : 0) - (StateView.Same(c.Before, desired) ? 1 : 0));
            if (c.Before == StateView.Unknown || c.After == StateView.Unknown) continue;
            if (c.Field == "KnownState" && c.After is Dictionary<(Entity, string), object?> known)
            {
                var previous = c.Before as Dictionary<(Entity, string), object?> ?? new();
                if (known.Any(kv => !previous.TryGetValue(kv.Key, out var old) || !StateView.Same(old, kv.Value))) value += weights["truth"];
            }
            if (c.Field is "charges" or "fuel" or "battery" or "integrity")
                value -= Math.Max(0, Convert.ToDouble(c.Before) - Convert.ToDouble(c.After));
            if (c.Subject == actor && c.Field == "strain") value += (Convert.ToDouble(c.Before) - Convert.ToDouble(c.After)) * weights["safety"];
            if (c.Subject == actor.Get<Entity>("attachment") && c.Field == "strain") value += (Convert.ToDouble(c.Before) - Convert.ToDouble(c.After)) * weights["relationship"];
            if (c.Field == "marked" && c.Subject == actor) value += Equals(c.After, false) ? weights["safety"] : -weights["safety"];
            if (c.Field is "rel:HelpRequested" or "rel:Promised" && c.Subject == actor) value += weights["relationship"] * .5;
        }
        return value;
    }
    private void PerceiveEvent(Entity ev, ActionContext? context)
    {
        foreach (var witness in ev.Related(FacetType.Witness).Select(f => f.Value).OfType<Soul>())
            if (witness.Body is { } body) Perceive(body);
    }
    private IReadOnlyList<Effect> Deflect(Intent intent, StateView view, IReadOnlyList<Effect> changes)
    {
        view = view.Fork();
        var result = new List<Effect>();
        IEnumerable<Effect> Expand(IEnumerable<Effect> input)
        {
            foreach (var effect in input)
            {
                if (effect.Op == "sequence")
                    foreach (var nested in Expand(effect.Args.Cast<Effect>())) yield return nested;
                else if (effect.Op == "when" && Equals(view.Value(effect.Args[0]), true))
                    foreach (var nested in Expand([(Effect)effect.Args[1]])) yield return nested;
                else yield return effect;
            }
        }
        IReadOnlyList<Effect> Rewrite(Effect effect)
        {
            if (effect.Op == "set" && effect.Args is [Entity marked, "marked", true] && Equals(view.Read(marked, "warded"), true))
                return [F("set", marked, "warded", false), F("set", marked, "warded_by", null!)];
            if (effect.Op != "change" || effect.Args is not [Entity victim, "strain", var raw] || Convert.ToDouble(raw) <= 0)
                return [effect];
            var amount = Convert.ToDouble(raw);
            var replacement = new List<Effect>();
            if (Equals(view.Read(victim, "protected"), true))
            {
                replacement.Add(F("set", victim, "protected", false)); replacement.Add(F("set", victim, "protected_by", null!));
                amount = Math.Max(0, amount - 1);
                if (amount == 0) return replacement;
            }
            if (view.Read(victim, "substitute") is Entity effigy && view.Number(effigy, "charges") >= amount && view.Read(effigy, "$parent") == victim)
            {
                replacement.Add(F("spend", effigy, "charges", amount)); replacement.Add(F("set", victim, "substitute", null!));
            }
            else if (view.Read(victim, "mirror") is Entity mirror && view.Number(mirror, "integrity") >= amount
                && view.Read(mirror, "$parent") == victim && view.Read(mirror, "target") == intent.Actor)
            {
                replacement.Add(F("spend", mirror, "integrity", amount)); replacement.Add(F("change", intent.Actor, "strain", amount));
            }
            else
            {
                var thread = victim.Items.OfType<Entity>().FirstOrDefault(item =>
                    item.Get<object>("item_kind") is HauntedItem.RedThread && Equals(view.Read(item, "linked"), true)
                    && view.Read(item, "endpoint_a") == victim && view.Read(item, "endpoint_b") is Character
                    && view.Number(item, "charges") >= 1);
                if (thread != null)
                {
                    var shared = Math.Min(1, amount);
                    replacement.Add(F("spend", thread, "charges", 1d));
                    replacement.Add(F("set", thread, "linked", false));
                    replacement.Add(F("change", (Entity)view.Read(thread, "endpoint_b")!, "strain", shared));
                    amount -= shared;
                }
                if (amount > 0) replacement.Add(F("change", victim, "strain", amount));
            }
            return replacement;
        }
        foreach (var effect in Expand(changes))
        {
            var rewritten = Rewrite(effect);
            result.AddRange(rewritten);
            var shadow = Session.Runtime.Effects.Prepare(rewritten, view);
            if (shadow.Succeeded) view = shadow.View;
        }
        return result;
    }
    public void Dispose()
    {
        foreach (var cleanup in _cleanup) cleanup();
        Narrative.Dispose(); Rules.Dispose(); Session.Dispose();
    }
}
