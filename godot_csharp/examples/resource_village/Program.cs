using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Effects;

static class Program
{
    private static IEnumerable<Intent> OfferFood(
        Entity target, Soul mind, Entity actor, RuleEnvironment _, IReadOnlyDictionary<string, object>? __)
    {
        StateView.Learn(mind, target, "$parent", target.Parent);
        StateView.Learn(mind, target, "doses", target.Get<double>("doses"));
        StateView.Learn(mind, target, "tag:Consumable", target.Tag("Consumable"));
        yield return new Intent(
            actor, mind, AST.F("consume", actor, target),
            [AST.F("spend", target, "doses", 1d), AST.F("change", actor, "tag:Fed", 1d)],
            motionCost: 1,
            label: $"进食：{actor.Name}消耗{target.Name}",
            requirements:
            [
                AST.E("eq", AST.E("value", target, "$parent"), actor.Parent!),
                AST.E("gte", AST.E("value", target, "doses"), 1d),
                AST.E("gte", AST.E("value", target, "tag:Consumable"), 1d)
            ]);
    }

    private static IEnumerable<Intent> OfferRestock(
        Entity target, Soul mind, Entity actor, RuleEnvironment _, IReadOnlyDictionary<string, object>? context)
    {
        if (context?.TryGetValue("food", out var value) != true || value is not Entity food) yield break;
        StateView.Learn(mind, target, "$parent", target.Parent);
        StateView.Learn(mind, target, "doses", target.Get<double>("doses"));
        StateView.Learn(mind, food, "$parent", food.Parent);
        StateView.Learn(mind, food, "doses", food.Get<double>("doses"));
        yield return new Intent(
            actor, mind, AST.F("restock", actor, target),
            [AST.F("spend", target, "doses", 1d), AST.F("set", food, "doses", 1d)],
            motionCost: 1,
            label: $"补给：{actor.Name}从{target.Name}补充口粮",
            requirements:
            [
                AST.E("eq", AST.E("value", target, "$parent"), actor.Parent!),
                AST.E("gte", AST.E("value", target, "doses"), 1d),
                AST.E("eq", AST.E("value", food, "doses"), 0d)
            ]);
    }

    private static IEnumerable<Intent> OfferRest(
        Entity target, Soul mind, Entity actor, RuleEnvironment _, IReadOnlyDictionary<string, object>? __)
    {
        StateView.Learn(mind, target, "$parent", target.Parent);
        StateView.Learn(mind, target, "tag:RestingPlace", target.Tag("RestingPlace"));
        yield return new Intent(
            actor, mind, AST.F("rest", actor, target),
            [AST.F("set", actor, "tag:Panic", (double)Math.Max(0, actor.Tag("Panic") - 2)), AST.F("change", mind, "tag:Stability", 2d)],
            motionCost: 2,
            label: $"休息：{actor.Name}在{target.Name}休憩",
            requirements:
            [
                AST.E("eq", AST.E("value", target, "$parent"), actor.Parent!),
                AST.E("gte", AST.E("value", target, "tag:RestingPlace"), 1d)
            ]);
    }

    private static (string[] Actions, double FoodDoses, double SupplyDoses) RunScenario(int initialSupply)
    {
        var world = new Entity("资源村");
        var square = new Entity("村广场", world, new Dictionary<string, int> { ["Scene"] = 1 });
        var food = new Entity("公共粮箱", square, properties: new Dictionary<string, object> { ["doses"] = 1d });
        food.SetTag("Consumable", 1);
        food.SetTag("Food", 1);
        var supply = new Entity("补给仓", square, properties: new Dictionary<string, object> { ["doses"] = (double)initialSupply });
        supply.SetTag("Supply", 1);
        var bench = new Entity("树荫长椅", square);
        bench.SetTag("RestingPlace", 1);

        food.Offer(OfferFood);
        supply.Offer(OfferRestock);
        bench.Offer(OfferRest);

        using var session = new SimulationSession(world);
        var actions = new List<string>();
        foreach (var (name, slot) in new[] { ("甲", 0), ("乙", 1) })
        {
            var actor = new Character(name, square, position: slot);
            var soul = new Soul($"{name}的心智");
            actor.Bind(soul);
            soul.Discover(a => (a.Parent as Entity)?.Items.OfType<Entity>() ?? []);
            session.Activate(actor, soul, context: new Dictionary<string, object> { ["food"] = food },
                utility: intent => intent.Effect.Op switch
                {
                    "consume" => 100,
                    "restock" => 70,
                    "rest" => 10,
                    _ => 0
                },
                completed: (intent, result) =>
                {
                    if (intent != null && result?.Allowed == true && result.Result?.Succeeded == true)
                    {
                        var verb = intent.Effect.Op switch
                        {
                            "consume" => "进食",
                            "restock" => "补给",
                            "rest" => "休息",
                            _ => intent.Effect.Op
                        };
                        actions.Add($"{intent.Actor.Name}:{verb}");
                    }
                });
        }

        session.Timeline.RunUntil(8, maxSteps: 100);
        if (food.Get<double>("doses") < 0 || supply.Get<double>("doses") < 0)
            throw new InvalidOperationException("资源剂量不得为负数。");
        return (actions.ToArray(), food.Get<double>("doses"), supply.Get<double>("doses"));
    }

    public static void Main()
    {
        var first = RunScenario(1);
        var same = RunScenario(1);
        var changed = RunScenario(2);

        if (!first.Actions.SequenceEqual(same.Actions))
            throw new InvalidOperationException("相同初始世界没有得到相同结果。");
        if (first.Actions.SequenceEqual(changed.Actions))
            throw new InvalidOperationException("修改初始供应没有改变行为序列。");
        if (!first.Actions.Any(action => action.EndsWith(":休息")))
            throw new InvalidOperationException("供应枯竭后未切换到休息。");

        Console.WriteLine("资源村因果演示：");
        Console.WriteLine($"行动序列：{string.Join(" → ", first.Actions)}");
        Console.WriteLine($"最终库存：公共粮箱={first.FoodDoses}，补给仓={first.SupplyDoses}");
        Console.WriteLine("断言通过：资源不为负；相同初始世界可复现；改变供应会改变行为序列。");
    }
}
