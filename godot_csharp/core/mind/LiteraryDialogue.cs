using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Rules;

namespace DualOntologyWorldEngine.Core.Mind;

/// <summary>
/// 规则怪谈与封闭群像场景的默认人物交流候选。
///
/// 这里只装配语义行动，不直接写入人物、关系或秘密。每个候选的
/// <see cref="Intent.Changes"/> 都是可推演的声明式变化，实际提交仍由统一执行器完成。
/// </summary>
public static class LiteraryDialogue
{
    /// <summary>
    /// 从当前场景、上下文和主体已知事实中装配有内容的交流行动。
    /// </summary>
    public static IEnumerable<Intent> Offers(Soul mind, Entity actor, RuleEnvironment env,
        IReadOnlyDictionary<string, object>? context)
    {
        var people = PeopleInScope(actor, context).ToArray();
        if (people.Length == 0) yield break;
        var (topic, claim) = Topics(mind, actor, env, context).FirstOrDefault();
        var evidence = Evidence(actor, context).FirstOrDefault();
        var goal = actor.Get<Entity>("goal_target") ?? ContextEntity(context, "goal_target");
        var offer = ContextEntity(context, "offer") ?? Inventory(actor).FirstOrDefault(IsOffer);
        var requested = ContextEntity(context, "requested") ?? ContextEntity(context, "request");
        var view = new StateView(mind);

        foreach (var target in people)
        {
            // 每次表达只声明差异；人物绑定、同场条件、记忆关系和重复限制在此统一组装。
            Intent Say(string op, string label, string speech, string memory, IEnumerable<Effect> effects,
                Entity? definition = null, Expr? proposition = null, double cost = 5, bool isPrivate = false,
                Entity? memorySubject = null, Entity? memoryTarget = null, IEnumerable<Expr>? requirements = null,
                params object[] details)
            {
                var owner = memorySubject ?? actor;
                var about = memoryTarget ?? target;
                return new Intent(actor, mind, AST.F(op, new object[] { actor, target }.Concat(details).ToArray()),
                    effects.Append(AST.F("relate", owner, memory, about)), cost, label,
                    new[] { SameParent(actor, target), NotRelated(owner, memory, about) }.Concat(requirements ?? []))
                { Definition = definition ?? target, Speech = speech, Claim = proposition, IsPrivate = isPrivate };
            }
            bool Remembered(string memory, Entity? owner = null, Entity? about = null) =>
                (owner ?? actor).Related(memory, about ?? target).Any();

            if (claim != null && topic != null && !Remembered("Claimed", about: topic) && !Remembered("Retracted", about: topic))
            {
                var statement = Statement(actor, mind, claim);
                var effects = new List<Effect>
                {
                    AST.F("set", actor, "statement", AST.E("quote", claim)),
                    AST.F("set", actor, "statement_text", statement),
                    AST.F("relate", topic, "ClaimedTo", target)
                };
                if (target.Mind is { } listener)
                    effects.Add(AST.F("observe", listener, actor, "statement", AST.E("value", actor, "statement")));
                yield return Say("state_claim", "陈述", statement, "Claimed", effects, topic, claim, 10,
                    memoryTarget: topic, details: [AST.E("quote", claim)]);
            }
            if (!Remembered("Asked") && (topic != null || view.Read(target, "statement") is Expr))
                yield return Say("ask", "追问", $"“{target.Name}，关于{topic?.Name ?? "刚才那句话"}，你究竟知道多少？”", "Asked",
                    [AST.F("set", target, "pending_question", actor), AST.F("relate", target, "QuestionedBy", actor)],
                    details: [claim == null ? target : AST.E("quote", claim)]);

            if (claim != null && !Remembered("Challenged") && view.Read(target, "statement") is Expr other && Contradicts(claim, other))
                yield return Say("challenge", "质疑", $"“等一下。你说{ClaimText(other)}，可我记得{ClaimText(claim)}。这两件事对不上。”", "Challenged",
                    [AST.F("set", target, "pending_question", actor), AST.F("change", target, "strain", 1d), AST.F("relate", target, "DisputedBy", actor)],
                    topic, claim, 10, details: [AST.E("quote", claim)]);

            if (evidence != null && !Remembered("ShownTo", evidence))
            {
                var effects = new List<Effect> { AST.F("relate", actor, "Showed", evidence) };
                if (target.Mind is { } listener)
                    effects.AddRange(EvidenceFields(evidence).Select(field => AST.F("observe", listener, evidence, field, AST.E("value", evidence, field))));
                yield return Say("show_evidence", "出示证据", $"“我把{evidence.Name}拿出来了。上面写着：{EvidenceText(evidence)}。请先看它，再判断我的话。”",
                    "ShownTo", effects, evidence, cost: 10, memorySubject: evidence, details: [evidence]);
            }
            if (offer != null && requested != null && !Remembered("Proposed"))
                yield return Say("propose", "交换提议", $"“我可以把{offer.Name}给你，换回{requested.Name}。你愿意吗？”", "Proposed",
                    [AST.F("relate", target, "ProposalFrom", actor)], offer, cost: 10, details: [offer, requested]);

            if (goal != null && !GoalSatisfied(mind, actor))
            {
                if (actor.Get<Entity>("attachment") == target && !Remembered("Promised"))
                    yield return Say("pledge", "承诺", $"“{target.Name}，我答应你，{GoalText(actor)}。这件事由我负责。”", "Promised",
                        [AST.F("relate", target, "PromiseFrom", actor)], goal, cost: 10,
                        details: [goal, actor.Get<string>("goal_field") ?? "状态", actor.Get<object>("goal_value") ?? goal]);
                if (PreferredRecipient(actor, target) && !Remembered("HelpRequested"))
                    yield return Say("request_help", "求助", $"“我一个人处理不了{goal.Name}，你能帮我吗？”", "HelpRequested",
                        [AST.F("relate", target, "HelpFor", goal)], goal, details: [goal]);
            }
            if (Distressed(mind, target) && !Remembered("Comforted"))
                yield return Say("comfort", "安抚", "“先别急。你可以慢慢说，我在听。”", "Comforted",
                    [AST.F("change", target, "strain", -1d), AST.F("relate", target, "ComfortedBy", actor)],
                    requirements: [AST.E("gte", AST.E("value", target, "strain"), 1d)]);

            if (PendingMatter(actor, target) && !Remembered("Pressured"))
                yield return Say("pressure", "施压", "“别再绕开问题。现在就回答。”", "Pressured",
                    [AST.F("relate", target, "PressuredBy", actor)]);

            var questioned = actor.Get<Entity>("pending_question") == target;
            if (questioned && actor.Get<bool>("guard_secret") && view.Read(actor, "secret") is Expr secret && !Remembered("Concealed"))
                yield return Say("conceal", "隐瞒", "（他把真正想说的话咽了回去。）", "Concealed",
                    [AST.F("set", actor, "pending_question", null!)], isPrivate: true, details: [AST.E("quote", secret)]);

            var retracted = actor.Related("Claimed").Select(f => f.Value).OfType<Entity>().FirstOrDefault();
            if (retracted != null && actor.Related("DisputedBy").Any() && !Remembered("Retracted", about: retracted))
                yield return Say("retract", "撤回", "“刚才那句话，我收回。”", "Retracted",
                    [AST.F("unrelate", actor, "Claimed", retracted), AST.F("set", actor, "statement", null!),
                     AST.F("set", actor, "pending_question", null!), AST.F("unrelate", retracted, "ClaimedTo", target)],
                    retracted, memoryTarget: retracted, requirements: [Contains(actor, "Claimed", retracted)],
                    details: [AST.E("quote", (object?)retracted.Get<Expr>("claim") ?? claim ?? (object)retracted)]);

            if (questioned && !Remembered("Silent"))
                yield return Say("silence", "沉默", "（他没有回答，只是把视线移开了。）", "Silent",
                    [AST.F("set", actor, "pending_question", null!), AST.F("relate", target, "ReceivedSilence", actor)], isPrivate: true);
        }
    }

    private static string Statement(Entity actor, Soul mind, Expr claim) =>
        StateView.Knowledge(mind).Any(kv => kv.Key.Item2 == "claim" && Equals(kv.Value, claim))
            ? $"“我收到的材料指向这一点：{ClaimText(claim)}。这不是我亲眼看见的。”"
            : actor.Get<string>("tone") switch
            {
                "尖锐" => $"“别含糊了。{ClaimText(claim)}，这是我的判断。”",
                "直接" => $"“我记得很清楚：{ClaimText(claim)}。”",
                _ => $"“我知道的只有这一点：{ClaimText(claim)}。其余我不能保证。”"
            };

    /// <summary>将已结算事件转成观察者能够理解的短文本。</summary>
    public static string Describe(Entity ev, Soul? observer = null)
    {
        if (observer != null && !ev.Related(FacetType.Witness, observer).Any())
            return "有人进行了交涉，但你没有听清具体内容。";

        var intent = ev.Get<Intent>("intent");
        if (!ev.Get<bool>("succeeded", true))
        {
            var failedTarget = intent?.Target?.Name ?? "对方";
            var reason = ev.Get<string>("reason");
            return string.IsNullOrWhiteSpace(reason)
                ? $"{failedTarget}没有完成这次交涉。"
                : $"{failedTarget}没有完成这次交涉：{reason}。";
        }
        var speech = ev.Get<string>("speech") ?? intent?.Speech;
        if (!string.IsNullOrWhiteSpace(speech)) return speech;

        return intent?.Label ?? "发生了一次交涉。";
    }

    private static IEnumerable<Character> PeopleInScope(Entity actor, IReadOnlyDictionary<string, object>? context)
    {
        var people = new List<Character>();
        var scene = actor is Character character ? character.CurrentScene : null;
        scene ??= ContextEntity(context, "scene");
        if (scene != null)
            foreach (var entity in scene.Children.Keys.OfType<Character>()) people.Add(entity);
        foreach (var key in new[] { "characters", "audience", "listener", "target", "subject" })
            foreach (var entity in ContextEntities(context, key))
                if (entity is Character c) people.Add(c);
        return people.Where(c => !ReferenceEquals(c, actor) && ReferenceEquals(c.Parent, actor.Parent)).Distinct();
    }

    private static IEnumerable<(Entity Entity, Expr Claim)> Topics(
        Soul mind,
        Entity actor,
        RuleEnvironment env,
        IReadOnlyDictionary<string, object>? context)
    {
        var expressions = new List<Expr>();
        foreach (var key in new[] { "claim", "proposition", "topic" })
            if (context?.GetValueOrDefault(key) is Expr expression) expressions.Add(expression);
        var ownSecret = StateValue(mind, actor, "secret") as Expr;
        if (ownSecret?.Op == "eq" && ownSecret.Args is [Expr { Op: "value", Args: [Entity referent, string field] }, _]
            && StateValue(mind, referent, field) is { } current && current != StateView.Unknown)
            ownSecret = AST.E("eq", ownSecret.Args[0], current);
        if (ownSecret != null) expressions.Add(ownSecret);
        expressions.AddRange(mind.Observations.Where(e => e.Op is "eq" or "not" or "state"));
        var evidenceClaims = StateView.Knowledge(mind).Where(kv => kv.Key.Item2 == "claim").Select(kv => kv.Value).OfType<Expr>().ToArray();
        expressions.AddRange(evidenceClaims);
        var cognitive = mind.CognitiveEnvironment(env);
        foreach (var expression in expressions.Distinct())
        {
            if (ReferenceEquals(expression, ownSecret) || evidenceClaims.Contains(expression)
                || cognitive.Facts.Any(f => cognitive.Matches(f, expression)))
            {
                var entity = Referent(expression);
                if (entity != null) yield return (entity, expression);
            }
        }
    }

    private static IEnumerable<Entity> Evidence(Entity actor, IReadOnlyDictionary<string, object>? context)
    {
        foreach (var key in new[] { "evidence", "record", "proof" })
            foreach (var entity in ContextEntities(context, key))
                if (Accessible(actor, entity) && IsEvidence(entity)) yield return entity;
        foreach (var item in Inventory(actor))
            if (IsEvidence(item)) yield return item;
    }

    private static IEnumerable<string> EvidenceFields(Entity evidence)
    {
        var fields = new HashSet<string>(evidence.Get<string[]>("public_fields") ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var field in new[] { "evidence", "claim", "recording_subject", "recording_text", "statement", "content" })
            if (evidence.HasProperty(field)) fields.Add(field);
        return fields;
    }

    private static bool Accessible(Entity actor, Entity entity) =>
        ReferenceEquals(entity.Parent, actor) || ReferenceEquals(entity.Parent, actor.Parent);

    private static bool IsEvidence(Entity entity) =>
        entity.Has("Evidence") || entity.Has("Record") || entity.Has("Recording") ||
        entity.Entries("Claim").Any() || entity.Get<Expr>("claim") != null ||
        entity.Get<Expr>("proposition") != null || entity.HasProperty("evidence") ||
        entity.HasProperty("recording_text");

    private static bool IsOffer(Entity entity) =>
        entity.Has("Item") || entity.Has("Evidence") || entity.Has("Consumable") || entity.Has("Tool");

    private static IEnumerable<Entity> Inventory(Entity actor) =>
        actor.Children.Keys.OfType<Entity>().Where(entity => !entity.Has("Soul") && !entity.HasTag(CommonTag.Soul));

    private static bool Distressed(Soul observer, Character target)
    {
        var fields = target.Get<string[]>("public_fields") ?? Array.Empty<string>();
        var strain = new StateView(observer).Number(target, "strain");
        return fields.Contains("strain", StringComparer.OrdinalIgnoreCase) && !double.IsNaN(strain) && strain > 0;
    }

    private static bool PendingMatter(Entity actor, Character target) =>
        target.Related("Asked", actor).Any() || target.Related("Proposed", actor).Any() ||
        actor.Related("Asked", target).Any() || actor.Related("Challenged", target).Any();

    private static bool PreferredRecipient(Entity actor, Character target)
    {
        var attachment = actor.Get<Entity>("attachment");
        return attachment == null || ReferenceEquals(attachment, target);
    }

    private static object? StateValue(Soul mind, Entity subject, string field) =>
        new StateView(mind).Read(subject, field);

    public static bool Contradicts(Expr left, Expr right) =>
        Equals(AST.E("not", left), right) || Equals(AST.E("not", right), left)
        || (left.Op == "eq" && right.Op == "eq" && Equals(left.Args[0], right.Args[0]) && !StateView.Same(left.Args[1], right.Args[1]));
    private static Entity? Referent(Expr expression) => expression.Args.OfType<Entity>().FirstOrDefault()
        ?? expression.Args.OfType<Expr>().Select(Referent).FirstOrDefault(e => e != null);
    private static bool GoalSatisfied(Soul mind, Entity actor) => actor.Get<Entity>("goal_target") is { } target
        && StateView.Same(StateValue(mind, target, actor.Get<string>("goal_field")!), actor.Get<object>("goal_value"));
    public static string GoalText(Entity actor) => actor.Get<Entity>("goal_target") is { } target
        ? ClaimText(AST.E("eq", AST.E("value", target, actor.Get<string>("goal_field")!), actor.Get<object>("goal_value")!)) : "把眼前的事处理好";
    public static string ClaimText(Expr expression) =>
        expression.Op switch
        {
            "quote" when expression.Args.FirstOrDefault() is Expr inner => ClaimText(inner),
            "value" when expression.Args is [Entity subject, string field] => $"{subject.Name}的{FieldText(field)}",
            "not" when expression.Args[0] is Expr inner => $"并非{ClaimText(inner)}",
            "eq" when expression.Args is [Expr { Op: "value", Args: [Entity subject, "opened"] }, bool open] => $"{subject.Name}{(open ? "已经打开" : "还没有打开")}",
            "eq" when expression.Args is [Expr { Op: "value", Args: [Entity subject, "sealed"] }, bool sealedState] => $"{subject.Name}{(sealedState ? "已被封存" : "已解除封存")}",
            "eq" when expression.Args is [Expr { Op: "value", Args: [Entity subject, "location"] }, var location] => $"{subject.Name}在{ValueText(location)}",
            "eq" when expression.Args.Length >= 2 => $"{ValueText(expression.Args[0])}是{ValueText(expression.Args[1])}",
            "state" when expression.Args.Length >= 2 => $"{ValueText(expression.Args[0])}处于{ValueText(expression.Args[1])}状态",
            _ => "这项约定成立"
        };
    private static string FieldText(string field) => field switch
    {
        "opened" => "开启状态", "sealed" => "封存状态", "location" => "所在位置", "protected" => "防护状态", "marked" => "印记", _ => field
    };

    private static string ValueText(object? value) => value switch
    {
        Entity entity => entity.Name,
        Expr expression => ClaimText(expression),
        null => "空",
        true => "肯定的", false => "否定的",
        _ => value.ToString() ?? "空"
    };

    private static string EvidenceText(Entity evidence) =>
        evidence.Get<string>("description") ?? evidence.Get<string>("content") ?? evidence.Get<string>("evidence") ?? evidence.Name;

    private static Entity? ContextEntity(IReadOnlyDictionary<string, object>? context, string key) =>
        context?.GetValueOrDefault(key) as Entity;

    private static IEnumerable<Entity> ContextEntities(IReadOnlyDictionary<string, object>? context, string key)
    {
        if (context == null || !context.TryGetValue(key, out var value)) yield break;
        if (value is Entity entity) { yield return entity; yield break; }
        if (value is IEnumerable enumerable)
            foreach (var item in enumerable)
                if (item is Entity entityItem) yield return entityItem;
    }

    private static Expr Contains(Entity subject, string relation, Entity target) =>
        AST.E("contains", AST.E("value", subject, "rel:" + relation), target);

    private static Expr NotRelated(Entity subject, string relation, Entity target) =>
        AST.E("not", Contains(subject, relation, target));

    private static Expr SameParent(Entity left, Entity right) =>
        AST.E("all",
            AST.E("ne", AST.E("value", left, "$parent"), null!),
            AST.E("eq", AST.E("value", left, "$parent"), AST.E("value", right, "$parent")));
}
