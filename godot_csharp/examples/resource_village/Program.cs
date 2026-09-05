using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Rules;

static class Program
{
    private static IEnumerable<Intent> OfferFood(
        Entity target, Soul mind, Entity actor, RuleEnvironment _, IReadOnlyDictionary<string, object>? __)
    {
        yield return new Intent(
            actor, mind, AST.F("consume", actor, target), motionCost: 1,
            resolve: () => { target.Consume(); actor.ChangeTag("Fed", 1); return true; },
            label: $"进食：{actor.Name}消耗{target.Name}",
            validate: () => target.Parent == actor.Parent && target.Has("Consumable") && target.Get<int>("doses") > 0);
    }

    private static IEnumerable<Intent> OfferRestock(
        Entity target, Soul mind, Entity actor, RuleEnvironment _, IReadOnlyDictionary<string, object>? context)
    {
        if (context?.TryGetValue("food", out var value) != true || value is not Entity food) yield break;
        yield return new Intent(
            actor, mind, AST.F("restock", actor, target), motionCost: 1,
            resolve: () => { target.Set("doses", target.Get<int>("doses") - 1); food.Set("doses", 1); return true; },
            label: $"补给：{actor.Name}从{target.Name}补充口粮",
            validate: () => target.Parent == actor.Parent && target.Get<int>("doses") > 0 && food.Get<int>("doses") == 0);
    }

    private static IEnumerable<Intent> OfferRest(
        Entity target, Soul mind, Entity actor, RuleEnvironment _, IReadOnlyDictionary<string, object>? __)
    {
        yield return BaseActions.CreateRestIntent(actor, target, cost: 2, mind: mind);
    }

    private static (string[] Actions, int FoodDoses, int SupplyDoses) RunScenario(int initialSupply)
    {
        var world = new Entity("资源村");
        var square = new Entity("村广场", world, new Dictionary<string, int> { ["Scene"] = 1 });
        var food = new Entity("公共粮箱", square, properties: new Dictionary<string, object> { ["doses"] = 1 });
        food.SetTag("Consumable", 1);
        food.SetTag("Food", 1);
        var supply = new Entity("补给仓", square, properties: new Dictionary<string, object> { ["doses"] = initialSupply });
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
        if (food.Get<int>("doses") < 0 || supply.Get<int>("doses") < 0)
            throw new InvalidOperationException("资源剂量不得为负数。");
        return (actions.ToArray(), food.Get<int>("doses"), supply.Get<int>("doses"));
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
