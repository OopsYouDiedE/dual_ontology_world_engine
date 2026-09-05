using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;

/// <summary>验证疲劳边界在直接入口、效果准备和真实提交路径上一致。</summary>
public static class FatigueChecks
{
    public static void Run()
    {
        var world = new Entity("疲劳测试世界");
        var actor = new Character("疲劳测试角色", world);
        var soul = new Soul("疲劳测试心智");
        actor.Bind(soul);
        var effects = new EffectExecutor();

        soul.SetFatigue(100);
        Require(soul.Fatigue == 6, "direct setter 未限制上界");
        soul.ChangeFatigue(-100);
        Require(soul.Fatigue == 0, "direct change 未限制下界");
        soul.ChangeFatigue(100);
        Require(soul.Fatigue == 6, "direct change 未限制上界");

        soul.SetFatigue(2);
        var setPlan = effects.Prepare([new Effect("set", actor, "strain", 100d)]);
        Require(setPlan.Succeeded && setPlan.Changes.Count == 1 && Convert.ToDouble(setPlan.Changes[0].After) == 6,
            "set strain 预估未限制到 6");
        Require(setPlan.Commit().Succeeded && soul.Fatigue == 6, "set strain 预估与真实提交不一致");

        soul.SetFatigue(2);
        var changePlan = effects.Prepare([new Effect("change", actor, "strain", 100d)]);
        Require(changePlan.Succeeded && Convert.ToDouble(changePlan.Changes[0].After) == 6,
            "change strain 预估未限制到 6");
        Require(changePlan.Commit().Succeeded && soul.Fatigue == 6, "change strain 预估与真实提交不一致");

        soul.SetFatigue(2);
        foreach (var effect in new[]
        {
            new Effect("set", actor, "strain", double.NaN),
            new Effect("set", actor, "strain", double.PositiveInfinity),
            new Effect("change", actor, "strain", double.NegativeInfinity)
        })
        {
            var plan = effects.Prepare([effect]);
            Require(!plan.Succeeded && soul.Fatigue == 2, "非有限疲劳效果污染了状态");
        }

        RequireThrows(() => soul.SetFatigue(double.NaN), "direct setter 应拒绝 NaN");
        RequireThrows(() => soul.ChangeFatigue(double.PositiveInfinity), "direct change 应拒绝 Infinity");
        Require(soul.Fatigue == 2, "非有限 direct 疲劳操作污染了状态");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }
}
