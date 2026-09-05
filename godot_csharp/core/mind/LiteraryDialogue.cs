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
    public static IEnumerable<Intent> Offers(
        Soul mind,
        Entity actor,
        RuleEnvironment env,
        IReadOnlyDictionary<string, object>? context)
    {
        var people = PeopleInScope(actor, context).ToList();
        if (people.Count == 0) yield break;

        var topics = Topics(mind, actor, people, env, context).ToList();
        var claim = topics.FirstOrDefault(t => t.Claim != null)?.Claim;
        var topic = topics.FirstOrDefault(t => t.Claim != null)?.Entity;
        var evidence = Evidence(actor, context).FirstOrDefault();
        var goalTarget = actor.Get<Entity>("goal_target") ?? ContextEntity(context, "goal_target");
        var offer = ContextEntity(context, "offer") ?? Inventory(actor).FirstOrDefault(IsOffer);
        var requested = ContextEntity(context, "requested") ?? ContextEntity(context, "request");

        foreach (var target in people)
        {
            // 陈述：只陈述主体确实拥有来源的命题。重复内容以论题实体去重。
            if (claim != null && topic != null && !actor.Related("Claimed", topic).Any())
            {
                var statement = $"“{ClaimText(claim)}”";
                var claimChanges = new List<Effect>
                {
                    AST.F("set", actor, "statement", statement),
                    AST.F("relate", actor, "Claimed", topic),
                    AST.F("relate", topic, "ClaimedTo", target)
                };
                if (target.Mind is { } listener)
                    claimChanges.Add(AST.F("observe", listener, actor, "statement", AST.E("value", actor, "statement")));
                yield return Make(
                    actor, mind,
                    AST.F("state_claim", actor, target, AST.E("quote", claim)),
                    claimChanges,
                    10, "陈述", topic, claim,
                    speech: statement,
                    requirements: [NotRelated(actor, "Claimed", topic)]);
            }

            // 追问：具体指向一个人；同一问询关系只提出一次。
            if (!actor.Related("Asked", target).Any() && (topic != null || target.HasProperty("statement")))
            {
                var question = claim;
                yield return Make(
                    actor, mind,
                    AST.F("ask", actor, target, question == null ? target : AST.E("quote", question)),
                    [
                        AST.F("relate", actor, "Asked", target),
                        AST.F("relate", target, "QuestionedBy", actor)
                    ],
                    5, "追问", target,
                    speech: $"“{target.Name}，请你把这件事说清楚。”",
                    requirements: [NotRelated(actor, "Asked", target)]);
            }

            // 质疑：只有已经存在的命题或证据才能形成争议。
            if (claim != null && !actor.Related("Challenged", target).Any() && (evidence != null || target.Entries("Claim").Any()))
            {
                yield return Make(
                    actor, mind,
                    AST.F("challenge", actor, target, AST.E("quote", claim)),
                    [
                        AST.F("relate", actor, "Challenged", target),
                        AST.F("relate", target, "DisputedBy", actor)
                    ],
                    10, "质疑", topic, claim,
                    speech: "“这句话的依据在哪里？我认为它还不能成立。”",
                    responseMode: ResponseMode.Verify,
                    requirements: [NotRelated(actor, "Challenged", target)]);
            }

            // 出示证据：证据必须真实存在且有可追溯内容；重复对同一听众不再产生收益。
            if (evidence != null && !evidence.Related("ShownTo", target).Any())
            {
                var evidenceChanges = new List<Effect>
                {
                    AST.F("relate", actor, "Showed", evidence),
                    AST.F("relate", evidence, "ShownTo", target)
                };
                if (target.Mind is { } listener)
                {
                    foreach (var field in EvidenceFields(evidence))
                        evidenceChanges.Add(AST.F("observe", listener, evidence, field, AST.E("value", evidence, field)));
                }
                yield return Make(
                    actor, mind,
                    AST.F("show_evidence", actor, target, evidence),
                    evidenceChanges,
                    10, "出示证据", evidence,
                    speech: $"“看这个。它至少能证明{EvidenceText(evidence)}。”",
                    responseMode: ResponseMode.Verify,
                    requirements: [NotRelated(evidence, "ShownTo", target)]);
            }

            // 交换提议：没有可交付的东西和明确请求时，不生成空泛社交动作。
            if (offer != null && requested != null && !actor.Related("Proposed", target).Any())
            {
                yield return Make(
                    actor, mind,
                    AST.F("propose", actor, target, offer, requested),
                    [
                        AST.F("relate", actor, "Proposed", target),
                        AST.F("relate", target, "ProposalFrom", actor)
                    ],
                    10, "交换提议", offer,
                    speech: $"“我可以把{offer.Name}给你，换回{requested.Name}。你愿意吗？”",
                    responseMode: ResponseMode.Negotiate,
                    requirements: [NotRelated(actor, "Proposed", target)]);
            }

            // 承诺：承诺必须指向一个具体目标或状态字段，不能只增加一条好听的台词。
            if (goalTarget != null && PreferredRecipient(actor, target) && !actor.Related("Promised", target).Any())
            {
                var field = actor.Get<string>("goal_field") ?? "状态";
                var value = actor.Properties.TryGetValue("goal_value", out var goalValue) ? goalValue : goalTarget;
                yield return Make(
                    actor, mind,
                    AST.F("pledge", actor, target, goalTarget, field, value),
                    [
                        AST.F("relate", actor, "Promised", target),
                        AST.F("relate", target, "PromiseFrom", actor)
                    ],
                    10, "承诺", goalTarget,
                    speech: $"“我会处理{goalTarget.Name}的{field}。这件事由我负责。”",
                    responseMode: ResponseMode.Negotiate,
                    requirements: [NotRelated(actor, "Promised", target)]);
            }

            // 求助：需求来自目标、字段或一个已有论题，不凭空请求帮助。
            if (goalTarget != null && PreferredRecipient(actor, target) && !actor.Related("HelpRequested", target).Any())
            {
                yield return Make(
                    actor, mind,
                    AST.F("request_help", actor, target, goalTarget),
                    [
                        AST.F("relate", actor, "HelpRequested", target),
                        AST.F("relate", target, "HelpFor", goalTarget)
                    ],
                    5, "求助", goalTarget,
                    speech: $"“我一个人处理不了{goalTarget.Name}，你能帮我吗？”",
                    responseMode: ResponseMode.Cooperate,
                    requirements: [NotRelated(actor, "HelpRequested", target)]);
            }

            // 安抚只对处于明显不安、恐惧或悲伤状态的人出现。
            if (Distressed(mind, target) && !actor.Related("Comforted", target).Any())
            {
                yield return Make(
                    actor, mind,
                    AST.F("comfort", actor, target),
                    [
                        AST.F("change", target, "strain", -1d),
                        AST.F("relate", actor, "Comforted", target),
                        AST.F("relate", target, "ComfortedBy", actor)
                    ],
                    5, "安抚", target,
                    speech: "“先别急。你可以慢慢说，我在听。”",
                    responseMode: ResponseMode.Protect,
                    requirements: [NotRelated(actor, "Comforted", target),
                        AST.E("gte", AST.E("value", target, "strain"), 1d)]);
            }

            // 施压回应未解决的问询或冲突，避免对无关人物随机施压。
            if (PendingMatter(actor, target) && !actor.Related("Pressured", target).Any())
            {
                yield return Make(
                    actor, mind,
                    AST.F("pressure", actor, target),
                    [
                        AST.F("relate", actor, "Pressured", target),
                        AST.F("relate", target, "PressuredBy", actor)
                    ],
                    5, "施压", target,
                    speech: "“别再绕开问题。现在就回答。”",
                    responseMode: ResponseMode.Restrain,
                    requirements: [NotRelated(actor, "Pressured", target)]);
            }

            // 隐瞒记录的是对特定对象的遮蔽意图，不会改写客观秘密，也不会让听众自动相信。
            var secret = StateValue(mind, actor, "secret") as Expr;
            if (secret != null && !actor.Related("Concealed", target).Any())
            {
                yield return Make(
                    actor, mind,
                    AST.F("conceal", actor, target, AST.E("quote", secret)),
                    [AST.F("relate", actor, "Concealed", target)],
                    5, "隐瞒", target, claim: null, isPrivate: true,
                    speech: "（他把真正想说的话咽了回去。）",
                    requirements: [NotRelated(actor, "Concealed", target)]);
            }

            // 撤回必须有此前已经说出的论题；撤回本身也留下记录，防止反复刷动作。
            var retractTopic = actor.Related("Claimed").Select(f => f.Value as Entity).FirstOrDefault(e => e != null);
            if (retractTopic != null && !actor.Related("Retracted", retractTopic).Any())
            {
                var oldClaim = retractTopic.Get<Expr>("claim") ?? claim;
                var changes = new List<Effect>
                {
                    AST.F("unrelate", actor, "Claimed", retractTopic),
                    AST.F("relate", actor, "Retracted", retractTopic),
                    AST.F("unrelate", retractTopic, "ClaimedTo", target)
                };
                yield return Make(
                    actor, mind,
                    AST.F("retract", actor, target, AST.E("quote", (object?)oldClaim ?? retractTopic)),
                    changes,
                    5, "撤回", retractTopic, oldClaim,
                    speech: "“刚才那句话，我收回。”",
                    responseMode: ResponseMode.Negotiate,
                    requirements: [Contains(actor, "Claimed", retractTopic), NotRelated(actor, "Retracted", retractTopic)]);
            }

            // 沉默是对已提出问题的明确回应，不能在无人询问时无限生成。
            if (target.Related("Asked", actor).Any() && !actor.Related("Silent", target).Any())
            {
                yield return Make(
                    actor, mind,
                    AST.F("silence", actor, target),
                    [
                        AST.F("relate", actor, "Silent", target),
                        AST.F("relate", target, "ReceivedSilence", actor)
                    ],
                    5, "沉默", target, isPrivate: true,
                    speech: "（他没有回答，只是把视线移开了。）",
                    requirements: [NotRelated(actor, "Silent", target)]);
            }
        }
    }

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

        var op = intent?.Effect.Op ?? ev.Get<Effect>("effect")?.Op ?? "interaction";
        var target = intent?.Target;
        var subject = target?.Name ?? "对方";
        return op.ToLowerInvariant() switch
        {
            "ask" => $"有人向{subject}追问了一件事。",
            "challenge" => $"有人当面质疑了{subject}。",
            "show_evidence" => $"有人向{subject}出示了证据。",
            "propose" => $"有人向{subject}提出了交换。",
            "pledge" => $"有人向{subject}作出了郑重承诺。",
            "request_help" => $"有人向{subject}求助。",
            "comfort" => $"有人试着安抚{subject}。",
            "pressure" => $"有人逼{subject}正面回答。",
            "conceal" => $"有人在{subject}面前隐瞒了一些事。",
            "retract" => $"有人撤回了刚才的说法。",
            "silence" => $"{subject}没有回答。",
            "state_claim" => $"有人向{subject}陈述了一个判断。",
            _ => intent?.Label ?? "发生了一次交涉。"
        };
    }

    private sealed record Topic(Entity Entity, Expr? Claim);

    private static Intent Make(
        Entity actor,
        Soul mind,
        Effect effect,
        IEnumerable<Effect> changes,
        double cost,
        string label,
        Entity? definition = null,
        Expr? claim = null,
        bool isPrivate = false,
        ResponseMode? responseMode = null,
        IEnumerable<Expr>? requirements = null,
        string? speech = null)
    {
        var allRequirements = requirements?.ToList() ?? new List<Expr>();
        var conversationTarget = effect.Args.OfType<Entity>().FirstOrDefault(entity => !ReferenceEquals(entity, actor));
        if (conversationTarget != null) allRequirements.Add(SameParent(actor, conversationTarget));
        return new Intent(actor, mind, effect, changes, cost, label: label, requirements: allRequirements)
        {
            Definition = definition,
            Speech = speech,
            Claim = claim,
            IsPrivate = isPrivate
        };
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

    private static IEnumerable<Topic> Topics(
        Soul mind,
        Entity actor,
        IEnumerable<Character> people,
        RuleEnvironment env,
        IReadOnlyDictionary<string, object>? context)
    {
        var expressions = new List<Expr>();
        foreach (var key in new[] { "claim", "proposition", "topic" })
            if (context?.GetValueOrDefault(key) is Expr expression) expressions.Add(expression);
        expressions.AddRange(mind.Observations);
        var ownSecret = StateValue(mind, actor, "secret") as Expr;
        if (ownSecret != null) expressions.Add(ownSecret);
        foreach (var person in people)
            if (StateValue(mind, person, "secret") is Expr knownSecret) expressions.Add(knownSecret);
        foreach (var expression in expressions.Distinct())
        {
            if (Known(mind, env, expression) || ReferenceEquals(expression, ownSecret))
            {
                var entity = expression.Args.OfType<Entity>().FirstOrDefault();
                if (entity != null) yield return new Topic(entity, expression);
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
        foreach (var field in new[] { "evidence", "recording_subject", "recording_text", "statement", "content" })
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

    private static bool Knows(Soul? soul, Expr expression) => soul != null && soul.Observations.Any(o => o.Equals(expression));

    private static object? StateValue(Soul mind, Entity subject, string field) =>
        new StateView(mind).Read(subject, field);

    private static bool Known(Soul mind, RuleEnvironment env, Expr expression)
    {
        // Offers receives the objective environment from the timeline.  Never use its
        // facts as a shortcut for a character's knowledge; construct the subject's view.
        var cognitive = mind.CognitiveEnvironment(env);
        return cognitive.Facts.Any(f => cognitive.Matches(f, expression));
    }

    private static string ClaimText(Expr expression) =>
        expression.Op switch
        {
            "quote" when expression.Args.FirstOrDefault() is Expr inner => ClaimText(inner),
            "eq" when expression.Args.Length >= 2 => $"{ValueText(expression.Args[0])}是{ValueText(expression.Args[1])}",
            "state" when expression.Args.Length >= 2 => $"{ValueText(expression.Args[0])}处于{ValueText(expression.Args[1])}状态",
            _ => expression.Op
        };

    private static string ValueText(object? value) => value switch
    {
        Entity entity => entity.Name,
        Expr expression => ClaimText(expression),
        null => "空",
        _ => value.ToString() ?? "空"
    };

    private static string EvidenceText(Entity evidence) =>
        evidence.Get<string>("description") ?? evidence.Get<string>("content") ?? evidence.Name;

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
