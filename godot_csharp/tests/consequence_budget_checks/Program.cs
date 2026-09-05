using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Content;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Patterns;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using static DualOntologyWorldEngine.Core.Rules.AST;

var checks = new List<(string Name, Action Run)>
{
    ("StateChange 数值差额计费", () =>
    {
        var entity = new Entity("target");
        Assert(Balance.ConsequenceBudget([new StateChange(entity, "loss", 0d, 2d)]) == 2d);
        Assert(Balance.ConsequenceBudget([new StateChange(entity, "loss", null, 1000d)]) == 1000d);
        Assert(Balance.ConsequenceBudget([new StateChange(entity, "flag", false, true)]) == 1d);
        Assert(Balance.ConsequenceBudget([new StateChange(entity, "loss", 2d, 2d)]) == 0d);
    }),
    ("预测与执行只支付实际后果成本", () =>
    {
        var predicted = Setup(10d);
        var predictedTarget = new Entity("target", predicted.World);
        var predictedBook = new PatternBook(predicted.Session);
        var predictedClause = predictedBook.Add(Patterns.Prohibit(predicted.Actor, predicted.Actor, "open", predictedTarget,
            breach: [F("set", predictedTarget, "loss", 2d)]));
        StateView.Learn(predicted.Soul, predictedClause, "text", predictedClause.Name);
        StateView.Learn(predicted.Soul, predictedTarget, "loss", 0d);
        var action = new Intent(predicted.Actor, predicted.Soul, F("open", predicted.Actor, predictedTarget),
            [F("set", predictedTarget, "opened", true)]);
        var forecast = predictedBook.ForecastConsequences(action, predicted.Session.Runtime.Prepare(action, new StateView(predicted.Soul)));
        Assert(forecast.View.Number(predicted.Actor, "charges") == 8d,
            $"forecast charges={forecast.View.Number(predicted.Actor, "charges")}");
        Assert(forecast.View.Number(predictedTarget, "loss") == 2d,
            $"forecast loss={forecast.View.Number(predictedTarget, "loss")}");
        predicted.Session.Dispose();

        var actual = Setup(10d);
        var actualTarget = new Entity("target", actual.World);
        actualTarget.Set("loss", 0d);
        using var book = new PatternBook(actual.Session);
        book.Add(Patterns.Deadline(actual.Actor, actual.Actor, actualTarget, "open", 0,
            F("set", actualTarget, "loss", 2d)));
        actual.Session.Timeline.RunUntil(1);
        Assert(actual.Actor.Get<double>("charges") == 8d && actualTarget.Get<double>("loss") == 2d,
            $"actual charges={actual.Actor.Get<double>("charges")}, loss={actualTarget.Get<double>("loss")}");
    }),
    ("预算不足时 deadline 只关闭一次且不执行后果", () =>
    {
        var setup = Setup(1d);
        var target = new Entity("target", setup.World);
        using var book = new PatternBook(setup.Session);
        var clause = book.Add(Patterns.Deadline(setup.Actor, setup.Actor, target, "open", 0,
            F("set", target, "loss", 1000d)));
        setup.Session.Timeline.RunUntil(1);
        var consequences = setup.Session.Runtime.Events.Count(e => e.Get<Effect>("effect")?.Op == "consequence");
        Assert(!clause.Get<bool>("active") && consequences == 1 && setup.Actor.Get<double>("charges") == 1d
            && target.Get<double?>("loss") == null);
    })
};

var failed = 0;
foreach (var (name, run) in checks)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
return failed;

static (Entity World, Character Actor, Soul Soul, SimulationSession Session) Setup(double charges)
{
    var world = new Entity("world");
    var actor = new Character("actor", world);
    actor.Set("charges", charges);
    var soul = new Soul("soul", world);
    actor.Bind(soul);
    return (world, actor, soul, new SimulationSession(world));
}

static void Assert(bool condition, string message = "assertion failed") { if (!condition) throw new InvalidOperationException(message); }
