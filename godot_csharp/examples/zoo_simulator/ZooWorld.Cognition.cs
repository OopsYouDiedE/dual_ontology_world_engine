using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Social;
using DualOntologyWorldEngine.Core.Timeline;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Examples.ZooSimulator;

public sealed partial class ZooWorld
{
    public NarrativeDynamics Narrative { get; private set; } = null!;
    private Func<double?> _settleEmotions = null!;

    private void InitializeCognition()
    {
        Narrative = new NarrativeDynamics(Session);
        ElephantSign.Set("evidence", ElephantSign.Get<string>("description")!);
        ElephantSign.Set("claim", E("eq", E("value", ElephantSign, "description"), ElephantSign.Get<string>("description")!));
        Camera.Set("evidence", "监控画面需要与现场重新核对。");
        Mirror.Set("evidence", "镜中的眼睛与制服可作为自我辨认的参照。");
        foreach (var source in new[] { ElephantSign, Camera, Mirror, DrinkShelf, WhaleProjection })
            source.Set("public_fields", new[] { "$parent" });

        var runtime = Session.Runtime;
        runtime.WitnessesCollector = (ev, _) =>
        {
            if (ev.GetIntent() is not { } intent || intent.Effect.Op == "evolve") return [];
            var scene = Place(intent.Actor);
            return People.Where(c => Alive(c) && c.CurrentScene == scene
                    && (c == intent.Actor || !intent.IsPrivate))
                .Select(c => c.Mind!);
        };
        runtime.ReactionHandler = (mind, ev, _) =>
        {
            // 被求助者即时回应；反应仍经 Core 结算并支付自己的行动值。
            if (!ev.IsSucceeded() || ev.GetIntent() is not { Effect.Op: "ask", Actor: Character requester } intent
                || intent.Target is not Character helper || helper.Mind != mind
                || helper == requester || Finished(helper) || helper.CurrentScene != requester.CurrentScene
                || helper.Get<string>("role") is not ("staff" or "guard" or "red" or "director")) return null;
            return new Intent(helper, mind, F("acknowledge", helper, requester),
                [F("set", helper, "requester", requester), F("set", helper, "received_request", true),
                 F("change", requester, "fear", -Math.Min(0.2, requester.Get<double>("fear")))],
                1, $"回应{requester.Name}的求助",
                validate: () => Alive(helper) && Alive(requester) && helper.CurrentScene == requester.CurrentScene)
                { Speech = "先说清你看见了什么，我会确认附近的情况。" };
        };
        runtime.Executed += ConfigureSocialOutcome;
        _settleEmotions = () =>
        {
            runtime.MindMechanics.SettleDaily(People.Select(c => c.Mind!));
            return 1440;
        };
        Session.Timeline.Schedule(1440 - Minute, "心智情绪日结", _settleEmotions);
    }

    private void ConfigureSocialOutcome(Intent intent, ActionResolution result)
    {
        // Core 的效果模式按实体引用精确匹配；为当前事件绑定配置。
        var mechanics = Session.Runtime.MindMechanics;
        mechanics.EmotionRules.Clear();
        mechanics.BondRules.Clear();
        if (result.Result?.Succeeded != true) return;
        if (intent.Effect.Op == "attack")
            mechanics.EmotionRules.Add(new(intent.Effect, MindTarget.Witnesses, Emotion.Fear, 1, "目击袭击"));
        if (intent.Effect.Op is not ("acknowledge" or "comfort") || intent.Target is not Character target) return;
        mechanics.EmotionRules.Add(new(intent.Effect, MindTarget.TargetMind, Emotion.Hope, 1, "获得支持"));
        mechanics.BondRules.Add(new(intent.Effect, MindTarget.TargetMind, 1));
        target.Mind!.Get<Entity>("zoo_social_impact")?.Reparent(null);
        var combination = SocialSystem.SelectSocialInfluence(new Dictionary<string, object> { ["support_received"] = true });
        var impact = SocialSystem.CreateSocialImpact("动物园求助回应", target.Mind, intent.Actor, intent.Actor,
            combination, Session.Timeline.Now, durationUnit: 5);
        target.Mind.Set("zoo_social_impact", impact);
        result.Event?.Relate(FacetType.Effect, impact);
    }

    private void InitializeMotives(Character c)
    {
        c.Set("strain", 0d);
        c.Set("public_fields", new[] { "$parent", "uniform", "has_map", "strain", "statement" });
        Narrative.Care(c, "维持人类身份", c, "identity", "human", 8);
        if (c.Get<string>("role") == "visitor")
            Narrative.Care(c, "疲惫后离园", c, "$parent", RegularExit, 20,
                E("gte", E("value", c, "fatigue"), 4d));
    }

    public void Perceive(Character c)
    {
        foreach (var entity in Local(c).Append(c.CurrentScene!).Distinct())
        {
            StateView.Learn(c.Mind!, entity, "$parent", entity.Parent, entity);
            foreach (var field in entity.Get<string[]>("public_fields") ?? [])
                StateView.Learn(c.Mind!, entity, field, StateView.Actual(entity, field), entity);
        }
    }

    public double CognitiveUtility(Intent intent)
    {
        var plan = Session.Runtime.Prepare(intent, new StateView(intent.Mind));
        var value = plan.Succeeded ? Narrative.Value(intent, plan) : 0;
        if (intent.Effect.Op is "ask" or "defend" or "rest")
            value += 6 * UtilityMath.Normalize(intent.Mind.CalculateAggregatedEmotion(Emotion.Fear), 0, 5);
        if (intent.Speech != null) value += 16;
        if (intent.Effect.Op == "reflect") value += 20;
        if (intent.Mind.Get<Entity>("zoo_social_impact") is { } impact
            && impact.Get<double>("expires_at") > Session.Timeline.Now
            && impact.Related("Target", intent.Target).Any())
            value += SocialSystem.GetSocialAttitudePressure(impact.Get<string>("combination")!,
                intent.Effect.Op is "follow" or "accept_help" ? ResponseMode.Cooperate : ResponseMode.Verify);
        return value;
    }
}
