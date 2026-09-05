using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Content;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Patterns;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using static DualOntologyWorldEngine.Core.Rules.AST;

/// <summary>
/// Core-only 的小型文学模拟样例。
/// 场景只配置人物欲求、秘密、证据和条款；行动由 LiterarySimulation 自主选择。
/// </summary>
internal static class Program
{
    private sealed record RunResult(string Name, int Steps, bool GoalReached, IReadOnlyList<string> Operations);

    public static void Main()
    {
        var forbidden = RunNightRule(true, print: true);
        var open = RunNightRule(false, print: true);
        PrintCounterfactual(forbidden, open);

        RunTestimonyConflict();
    }

    private static RunResult RunNightRule(bool ruleActive, bool print)
    {
        using var game = new LiterarySimulation(ruleActive ? "夜班守则" : "夜班守则（未张贴守则）");
        var room = game.Scene("旧校舍值班室");
        var door = game.Object("通往档案室的门", room, sealedState: false, evidence: "门锁上有新鲜的刮痕");
        door.Set("statement", "门在午夜后不应被打开");

        var claim = E("eq", E("value", door, "opened"), false);
        var watcher = game.Person("值班人", room, door, "protected", true,
            secret: claim, tone: "克制");
        var student = game.Person("晚归学生", room, door, "opened", true,
            attachment: watcher, secret: E("eq", E("value", door, "opened"), true), tone: "急切");
        var witness = game.Person("记录员", room, door, "opened", true,
            attachment: watcher, tone: "直接");

        var talisman = HauntedItems.Create(HauntedItem.Talisman, "值班符", watcher, door);
        var record = game.Object("门锁刮痕照片", room, false, "照片显示有人在夜里试过这扇门");
        record.SetTag("Evidence", 1);
        record.Reparent(watcher);
        watcher.Mind!.Observe(claim, record);

        if (ruleActive)
            foreach (var actor in game.Characters)
                game.Rules.Add(Patterns.Prohibit(watcher, actor, "open", door, hard: true));

        foreach (var actor in game.Characters)
        {
            game.Perceive(actor);
            InstallPreference(actor.Mind!, ruleActive);
        }

        game.Start();
        var steps = game.Run(45, maxSteps: 180);
        var events = game.Session.Runtime.Events.Where(e => e.Get<bool>("succeeded")).ToList();
        if (print)
        {
            Console.WriteLine();
            Console.WriteLine($"【{game.World.Name}】自主运行 {steps} 步");
            foreach (var ev in events)
                Console.WriteLine($"  {ev.Get<double>("time"),5:0.0}  {LiteraryDialogue.Describe(ev)}");
            Console.WriteLine($"  结局：门.opened={door.Get<bool>("opened")}, 门.protected={door.Get<bool>("protected")}, 符余量={talisman.Get<double>("charges")}");
        }

        return new RunResult(game.World.Name, steps, door.Get<bool>("opened"),
            events.Select(e => e.Get<Intent>("intent")?.Effect.Op ?? "?").ToArray());
    }

    private static void InstallPreference(Soul mind, bool ruleActive)
    {
        mind.InstallPreference((_, intent, _, _) => intent.Effect.Op switch
        {
            // 守则存在时，护符和有来源的交涉比徒劳开门更有吸引力。
            "protect" when ruleActive => 90,
            "state_claim" when ruleActive => 80,
            "show_evidence" when ruleActive => 75,
            "challenge" when ruleActive => 70,
            "ask" when ruleActive => 65,
            "open" when !ruleActive => 100,
            "state_claim" => 35,
            "show_evidence" => 30,
            "ask" => 25,
            _ => 0
        }, source: "literary-story-preference");
    }

    private static void PrintCounterfactual(RunResult withRule, RunResult withoutRule)
    {
        Console.WriteLine();
        Console.WriteLine("【单条件反事实】只改变“守则是否存在”");
        Console.WriteLine($"  有守则：opened={withRule.GoalReached}，行动={string.Join(" → ", withRule.Operations.Distinct())}");
        Console.WriteLine($"  无守则：opened={withoutRule.GoalReached}，行动={string.Join(" → ", withoutRule.Operations.Distinct())}");
        Console.WriteLine(withRule.GoalReached != withoutRule.GoalReached ||
                          !withRule.Operations.SequenceEqual(withoutRule.Operations)
            ? "  反事实成立：同一人物和场景因一条规则产生不同结果。"
            : "  反事实未分化：请提高场景中的规则压力或人物目标。");
    }

    private static void RunTestimonyConflict()
    {
        using var game = new LiterarySimulation("封闭学园质证");
        var room = game.Scene("临时审理室");
        var key = game.Object("失踪的旧钥匙", room, false, "钥匙最后一次被看见是在档案室门口");
        key.Set("sealed", true);
        key.Set("location", "档案室");

        var aliceClaim = E("eq", E("value", key, "location"), "档案室");
        var bobClaim = E("eq", E("value", key, "location"), "钟楼");
        var alice = game.Person("林夕", room, key, "location", "档案室", secret: aliceClaim, tone: "直接");
        var bob = game.Person("周衡", room, key, "location", "钟楼", attachment: alice, secret: bobClaim, tone: "尖锐");
        var mediator = game.Person("顾问", room, key, "location", "档案室", attachment: alice, tone: "克制");

        var photo = game.Object("档案室门口的照片", room, false, "照片中的时间戳早于封锁");
        photo.SetTag("Evidence", 1);
        photo.Set("claim", aliceClaim);
        photo.Reparent(alice);
        alice.Mind!.Observe(aliceClaim, photo);
        bob.Mind!.Observe(bobClaim);

        game.Rules.Add(Patterns.Truth(alice, alice, aliceClaim));
        foreach (var actor in game.Characters)
        {
            game.Perceive(actor);
            actor.Mind!.InstallPreference((_, intent, _, _) => intent.Effect.Op switch
            {
                "state_claim" => 85,
                "show_evidence" => 80,
                "challenge" => 75,
                "ask" => 65,
                "request_help" => 15,
                _ => 0
            }, source: "testimony-preference");
        }

        game.Start();
        var steps = game.Run(55, maxSteps: 220);
        var events = game.Session.Runtime.Events.Where(e => e.Get<bool>("succeeded")).ToList();
        Console.WriteLine();
        Console.WriteLine($"【{game.World.Name}】自主运行 {steps} 步");
        foreach (var ev in events)
            Console.WriteLine($"  {ev.Get<double>("time"),5:0.0}  {LiteraryDialogue.Describe(ev)}");
        Console.WriteLine($"  结局：钥匙位置={key.Get<string>("location")}, 照片被出示={photo.Related("ShownTo").Count() > 0}");
    }
}
