using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Content;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Patterns;
using static DualOntologyWorldEngine.Core.Rules.AST;

MechanismChecks.Run();
var low = RunCase("燃料紧张", 1, 18, 15, true);
var ample = RunCase("燃料充足", 2, 18, 15, true);
var promiseFirst = RunCase("燃料紧张·守诺优先", 1, 10, 20, true);
var hidden = RunCase("燃料紧张·未公开提议", 1, 10, 20, false);

Check(low.Fuel >= 0 && low.Fuel <= 1, "燃料紧张案例越过预算");
Check(ample.Fuel >= 0 && ample.Fuel <= 2, "燃料充足案例越过预算");
Check(promiseFirst.Fuel >= 0 && promiseFirst.Fuel <= 1, "守诺反事实越过预算");
Check(new[] { low, ample, promiseFirst, hidden }.All(r => r.Used.Count == r.Used.Distinct().Count()), "机会被重复使用");
Check(low.ChildSafe != ample.ChildSafe || low.PromiseHonoured != ample.PromiseHonoured || low.PatientSaved != ample.PatientSaved,
    "反事实燃料变化没有造成重要结局分歧");
Check(low.ChildSafe && !low.PatientSaved && !promiseFirst.ChildSafe && promiseFirst.PatientSaved,
    "同燃料权重反事实没有让保护亲情与守诺形成相反结局");
Check(low.PromiseBroken && !promiseFirst.PromiseBroken, "背弃承诺没有留下持久关系损失");
Check(!low.ChildTreated && promiseFirst.ChildTreated, "同样有余药时，既成背弃必须改变后续援助选择");
Check(hidden.Fuel == 0 && !hidden.PatientSaved && !hidden.PromiseHonoured && !hidden.MedicineLoaded,
    "未公开提议时，药师不能凭对方隐藏机会预见合作收益");
Check(low.FuelAfterStop == low.Fuel && ample.FuelAfterStop == ample.Fuel && promiseFirst.FuelAfterStop == promiseFirst.Fuel,
    "终止后仍能刷取资源");
Check(new[] { low, ample, promiseFirst, hidden }.All(r => r.ResourceSpent >= 0 && r.ResourceSpent <= r.InitialFuel + 4), "资源变化总和超过初始预算");

var report = $"# 最后一席渡船：自主案例检查\n\n初始行动槽顺序为阿芙、沈砚；同刻按登记顺序执行。所有组保持这一条件。初始药物费用余额 2 Q、盟约执行源 2 Q，燃料单独计量。合作前瞻只使用公开提议，不保证对方履行。\n\n" +
    Render(low) + "\n" + Render(ample) + "\n" + Render(promiseFirst) + "\n" + Render(hidden) +
    $"\n## 运行分析\n\n燃料反事实：{Outcome(low)} → {Outcome(ample)}。同燃料权重反事实：{Outcome(low)} → {Outcome(promiseFirst)}。药物必须先装载，守诺机会才可救活病人；背弃保护承诺会留下 `broken` 和阿芙的 strain。航行后，阿芙仍有一份药：她是否帮助沈砚的女儿，由一般救助关切与失信后不再援助的条件关切共同决定。即使补足履约，已发生的背弃仍然影响援助。行动由 `Start/Run` 的效用选择产生，程序没有安排固定行动序列。\n\n边界：这些条件关切来自作者设定，并非模型自行生成的人格变化；仍未覆盖跨作品、跨长篇的文学质量。\n";
var output = args.SkipWhile(a => a != "--output").Skip(1).FirstOrDefault();
if (!string.IsNullOrWhiteSpace(output) && output != "markdown") File.WriteAllText(Path.GetFullPath(output), report);
Console.WriteLine(report);

static CaseResult RunCase(string name, double initialFuel, double childImportance, double promiseImportance, bool announced)
{
    using var sim = new LiterarySimulation(name);
    var scene = sim.Scene("洪水渡口");
    var ferry = new Entity("最后一席渡船", scene, properties: new Dictionary<string, object>
    {
        ["fuel"] = initialFuel, ["public_fields"] = new[] { "fuel" }
    });
    var child = new Entity("沈砚的女儿", scene, properties: new Dictionary<string, object>
    {
        ["safe"] = false, ["treated"] = false, ["public_fields"] = new[] { "safe", "treated" }
    });
    var patient = new Entity("岸上的病人", scene, properties: new Dictionary<string, object>
    {
        ["saved"] = false, ["public_fields"] = new[] { "saved" }
    });
    var promise = new Entity("给阿芙的承诺", scene, properties: new Dictionary<string, object>
    {
        ["honoured"] = false, ["broken"] = false, ["public_fields"] = new[] { "honoured", "broken" }
    });
    var medicine = new Entity("阿芙的药", scene, properties: new Dictionary<string, object>
    {
        ["loaded"] = false, ["public_fields"] = new[] { "loaded" }
    });
    var afu = sim.Person("药师阿芙", scene, patient, "saved", true, PersonalityProfile.TruthSeeking,
        attachment: patient, tone: "直接");
    var shen = sim.Person("船夫沈砚", scene, child, "safe", true, PersonalityProfile.Cautious,
        attachment: child, tone: "直接");
    afu.Set("charges", 2d);

    sim.Narrative.Care(shen, "保护女儿", child, "safe", true, childImportance);
    sim.Narrative.Care(shen, "履行承诺", promise, "honoured", true, promiseImportance);
    sim.Narrative.Care(afu, "救活病人", patient, "saved", true, 5);
    sim.Narrative.Care(afu, "仍想医治孩子", child, "treated", true, 6);
    sim.Narrative.Care(afu, "遭背弃后不再替沈砚付出", child, "treated", false, 12,
        when: E("eq", E("value", promise, "broken"), true));
    sim.Narrative.Opportunity(shen, "把最后一席留给女儿", child,
        [F("spend", ferry, "fuel", 1d), F("set", child, "safe", true)],
        [E("gte", E("value", ferry, "fuel"), 1d)], cost: 4, operation: "reserve_seat");
    sim.Narrative.Opportunity(shen, "履行给阿芙的承诺", promise,
        [F("spend", ferry, "fuel", 1d), F("set", promise, "honoured", true), F("set", patient, "saved", true)],
        [E("gte", E("value", ferry, "fuel"), 1d), E("eq", E("value", medicine, "loaded"), true)], cost: 4, operation: "honour_promise", announced: announced);
    sim.Narrative.Opportunity(afu, "把药送上渡船", patient,
        [F("spend", afu, "charges", 1d), F("set", medicine, "loaded", true)],
        [E("gte", E("value", afu, "charges"), 1d)], cost: 4, operation: "load_medicine", announced: announced);
    sim.Narrative.Opportunity(afu, "航行结束后，把余药留给沈砚的女儿", child,
        [F("spend", afu, "charges", 1d), F("set", child, "treated", true)],
        [E("eq", E("value", ferry, "fuel"), 0d), E("gte", E("value", afu, "charges"), 1d)],
        cost: 4, operation: "offer_aftercare");
    var anchor = sim.Anchor("盟约执行源", scene);
    anchor.Set("charges", 2d);
    sim.Rules.Add(Patterns.Prohibit(anchor, shen, "reserve_seat", child, hard: false,
        breach: [F("set", promise, "broken", true), F("change", afu, "strain", 1d)]));
    sim.Perceive(shen);

    var before = ferry.Get<double>("fuel");
    sim.Start();
    sim.Run(100, 100);
    var events = sim.Session.Runtime.Events.ToArray();
    var usedNames = events.Select(e => e.Get<Intent>("intent"))
        .Where(i => i?.Definition?.Get<bool>("used") == true)
        .Select(i => i!.Definition!.Name).ToList();
    var fuel = ferry.Get<double>("fuel");
    sim.Run(200, 100);
    var afterStop = ferry.Get<double>("fuel");
    var resourceSpent = events.SelectMany(e => e.Get<StateChange[]>("changes") ?? Array.Empty<StateChange>())
        .Where(c => c.Field is "fuel" or "charges" && c.Before is IConvertible && c.After is IConvertible)
        .Sum(c => Math.Max(0, Convert.ToDouble(c.Before) - Convert.ToDouble(c.After)));
    foreach (var (entity, field, initial) in new[] { (ferry, "fuel", initialFuel), (afu, "charges", 2d), (anchor, "charges", 2d) })
    {
        var delta = events.SelectMany(e => e.Get<StateChange[]>("changes") ?? [])
            .Where(c => c.Subject == entity && c.Field == field)
            .Sum(c => Convert.ToDouble(c.After) - Convert.ToDouble(c.Before));
        Check(Math.Abs(entity.Get<double>(field) - initial - delta) < 1e-9, $"{entity.Name}.{field}账目不守恒");
    }
    return new CaseResult(name, before, fuel, afterStop, child.Get<bool>("safe"), promise.Get<bool>("honoured"),
        patient.Get<bool>("saved"), promise.Get<bool>("broken"), usedNames, events, resourceSpent, medicine.Get<bool>("loaded"), child.Get<bool>("treated"));
}

static string Render(CaseResult result)
{
    var lines = $"## {result.Name}\n\n初始 fuel={result.InitialFuel:g}，最终 fuel={result.Fuel:g}，事件中资源实际消耗={result.ResourceSpent:g}；女儿安全={result.ChildSafe}，承诺履行={result.PromiseHonoured}，病人获救={result.PatientSaved}，承诺破裂={result.PromiseBroken}。\n\n";
    var waits = 0;
    foreach (var ev in result.Events)
    {
        var intent = ev.Get<Intent>("intent");
        if (intent == null) continue;
        if (intent.Effect.Op == "wait") { waits++; continue; }
        var changes = ev.Get<StateChange[]>("changes") ?? Array.Empty<StateChange>();
        lines += $"- AV {ev.Get<double>("time"):g}: **{intent.Actor.Name}** / {intent.Label}；" +
            (changes.Length == 0 ? "无状态变化" : string.Join("，", changes.Select(c => $"{c.Subject.Name}.{c.Field}: {Value(c.Before)}→{Value(c.After)}"))) + "\n";
    }
    return lines + $"\n共省略无状态变化的 wait 事件：{waits} 次。\n";
}

static string Outcome(CaseResult r) => $"女儿安全={r.ChildSafe}, 承诺履行={r.PromiseHonoured}, 病人获救={r.PatientSaved}, 承诺破裂={r.PromiseBroken}";
static string Value(object? value) => value switch { null => "null", Entity e => e.Name, _ => value.ToString() ?? "?" };
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

record CaseResult(string Name, double InitialFuel, double Fuel, double FuelAfterStop, bool ChildSafe,
    bool PromiseHonoured, bool PatientSaved, bool PromiseBroken, IReadOnlyList<string> Used, IReadOnlyList<Entity> Events,
    double ResourceSpent, bool MedicineLoaded, bool ChildTreated);
