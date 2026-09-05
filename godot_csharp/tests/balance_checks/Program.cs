using System.Diagnostics;
using System.Globalization;
using System.Text;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Content;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Kotodama;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Patterns;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using static DualOntologyWorldEngine.Core.Rules.AST;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var checks = new BalanceChecks();
checks.Run(args.Contains("--long"));
var outputIndex = Array.IndexOf(args, "--output");
if (outputIndex >= 0)
{
    if (outputIndex + 1 == args.Length) throw new ArgumentException("--output 需要文件路径");
    File.WriteAllText(Path.GetFullPath(args[outputIndex + 1]), checks.Report(), Encoding.UTF8);
}
Console.WriteLine(checks.Report());
return checks.Failed > 0 ? 1 : args.Contains("--strict") && checks.Findings.Count > 0 ? 2 : 0;

// 测试内的确定性构造器模拟候选搜索；不假装 Core 已有开放式 AI 创建权限层。
sealed class BalanceChecks
{
    private readonly List<string> _results = [];
    private readonly List<string> _metrics = [];
    public List<string> Findings { get; } = [];
    public int Failed { get; private set; }

    public void Run(bool longRun)
    {
        Check("疲劳三种写入路径、预测一致性与非有限值", FatigueChecks.Run);
        Check("规则候选生成、同义去重、采信与客观约束分离", GeneratedRules);
        Check("48 组言灵：预算/强度/价格组合及失败原子性", GeneratedWords);
        Check("等价嵌套与拆分支付不改变实际结果", EquivalentWords);
        Check("AI 比较双方效用后选择交换，并检验单次结算", NegotiatedExchange);
        Check("生成的期限契约：履约边界、违约预算与重复触发", GeneratedDeadlines);
        Check("AI 自缚折扣存在下限，破约后恢复成本", GeneratedSelfBinding);
        Check("预测未知禁令不泄密，客观执行不能绕过硬禁令", HiddenProhibition);
        foreach (var horizon in longRun ? new[] { 1000d, 10000d, 100000d } : new[] { 1000d })
        {
            var progress = new Dictionary<double, double>();
            foreach (var price in new[] { .75, 1d, 1.25 })
                Check($"自主构造与选择言灵：{horizon:g} AV，价格 {price:g}", () => progress[price] = LongEconomy(horizon, price));
            Check($"{horizon:g} AV 配对价格敏感性", () => Require(progress[.75] > progress[1] && progress[1] > progress[1.25],
                "相同供给下价格变化未改变可支付的有效进展"));
        }
        Check("开放式构造风险探针完整执行（探针发现不等于通过平衡）", ProbeOpenConstruction);
    }

    private void Check(string name, Action run)
    {
        try { run(); _results.Add($"- PASS：{name}"); }
        catch (Exception ex) { Failed++; _results.Add($"- FAIL：{name}；{ex.Message}"); }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static void Near(double actual, double expected, string reason) =>
        Require(Math.Abs(actual - expected) <= 1e-8 + Math.Abs(expected) * 1e-10,
            $"{reason}: actual={actual:g}, expected={expected:g}");

    private static (Entity World, Character Actor, Soul Mind, SimulationSession Session) Setup(double budget = 12)
    {
        var world = new Entity("试验世界");
        var actor = new Character("构造者", world);
        var mind = new Soul("构造心智");
        actor.Bind(mind);
        actor.Set("charges", budget);
        actor.Set("progress", 0d);
        return (world, actor, mind, new SimulationSession(world));
    }

    // 生成器只允许自己的预算与固定领域产出，不能提交任意字段写入。
    private static IEnumerable<Entity> Words(Character actor, double price)
    {
        foreach (var strength in new[] { 1d, 2d, 4d })
            yield return KotodamaFactory.CreateWordSpirit($"进展{strength:g}",
                cost: F("spend", actor, "charges", strength * price),
                effects: [F("change", actor, "progress", strength)]);
    }

    private static Intent WordIntent(Entity recipe, Character actor, Soul mind) =>
        new(actor, mind, F("invoke_progress", actor, actor),
            recipe.Entries("Cost").Concat(recipe.Entries("Effect")).Select(f => (Effect)f.Value),
            motionCost: 10, label: recipe.Name) { Definition = recipe };

    private static void GeneratedRules()
    {
        var (world, actor, mind, session) = Setup();
        using var cleanup = session;
        actor.SetTag(Aspect.Law, 3);
        var risk = new Entity("风险设施", world);
        risk.SetTag("Danger", 1);
        mind.Observe(E("observed_anomaly", risk));
        var candidates = RuleTemplate.SampleCandidateRules(mind, actor, session.Runtime.Env, []);
        Require(candidates.Count >= 2, "真实规则 AI 没有生成候选");
        var selected = candidates.MaxBy(c => c.BaseAffinity);
        var publication = RuleEntityFactory.CreateRulePublication(selected.Rule, actor);
        publication.Name = "修改显示名称";
        Require(!RuleTemplate.SampleCandidateRules(mind, actor, session.Runtime.Env, [publication])
            .Any(c => new[] { publication }.ContainsEquivalentRule(c.Rule)), "重复发布规避去重");

        var forbid = candidates.Single(c => c.Rule.Directives.Any(d => d.Effect.Op == "accuse_without_evidence")).Rule;
        var rulePublication = RuleEntityFactory.CreateRulePublication(forbid, actor);
        mind.Receive(rulePublication);
        mind.Accept(forbid);
        var intent = new Intent(actor, mind, F("accuse_without_evidence", actor, risk), []);
        var decision = session.Runtime.ResolveDetailed(intent);
        Require(decision.Result?.Succeeded == true && decision.Decision?.Complies == false,
            "采信规则错误变成客观硬约束，或未作用于决策");
        session.Runtime.Patches.Add(RuleEntityFactory.CreateRulePatch("客观禁令", forbid));
        Require(!session.Runtime.Review(intent).Allowed, "生成的槽位规则作为客观补丁未阻断");
    }

    private static void GeneratedWords()
    {
        foreach (var budget in new[] { 0d, 1d, 3d, 8d })
        foreach (var price in new[] { .5, .75, 1d, 1.25 })
        foreach (var strength in new[] { 1d, 2d, 4d })
        {
            var (_, actor, mind, session) = Setup(budget);
            using var cleanup = session;
            var recipe = Words(actor, price).Single(r => r.Name == $"进展{strength:g}");
            var expected = budget >= strength * price;
            var prediction = session.Runtime.Prepare(WordIntent(recipe, actor, mind), new StateView(mind));
            Near(actor.Get<double>("charges"), budget, "预测改写资源");
            Require(prediction.Succeeded == expected, "预算预测错误");
            var result = session.Runtime.Words.Invoke(recipe, mind, new Dictionary<string, object>());
            Require((result != null) == expected, "生成言灵执行与预算不一致");
            Near(actor.Get<double>("charges"), expected ? budget - strength * price : budget, "支付账差");
            Near(actor.Get<double>("progress"), expected ? strength : 0, "失败残留产出或产出错误");
        }
    }

    private static void EquivalentWords()
    {
        foreach (var budget in new[] { 1d, 4d })
        foreach (var nested in new[] { false, true })
        {
            var (_, actor, mind, session) = Setup(budget);
            using var cleanup = session;
            var costs = nested ? F("sequence", F("spend", actor, "charges", 1d), F("spend", actor, "charges", 1d))
                : F("spend", actor, "charges", 2d);
            var recipe = KotodamaFactory.CreateWordSpirit(cost: costs, effects: [F("change", actor, "progress", 2d)]);
            session.Runtime.Words.Invoke(recipe, mind, new Dictionary<string, object>());
            Near(actor.Get<double>("charges"), budget < 2 ? budget : budget - 2, "拆分支付改变余额");
            Near(actor.Get<double>("progress"), budget < 2 ? 0 : 2, "拆分支付改变产出");
        }
    }

    private static void NegotiatedExchange()
    {
        var (world, buyer, mind, session) = Setup();
        using var cleanup = session;
        using var book = new PatternBook(session);
        var seller = new Character("提议者", world);
        seller.Bind(new Soul("提议心智"));
        var payment = new Entity("支付物", buyer);
        // 已公开的偏好；同一买方对各候选估值不同，双方都须获益。
        var proposals = Enumerable.Range(1, 4).Select(value =>
        {
            var item = new Entity($"候选物{value}", seller);
            return (Item: item, BuyerUtility: value - 2d, SellerUtility: 4d - value);
        }).ToArray();
        var selected = proposals.Where(p => p.BuyerUtility > 0 && p.SellerUtility > 0)
            .MaxBy(p => p.BuyerUtility + p.SellerUtility);
        Require(selected.Item != null, "没有互利契约");
        var clause = book.Add(Patterns.Exchange(seller, buyer, selected.Item!, payment));
        StateView.Learn(mind, clause, "text", clause.Name);
        foreach (var (entity, field, value) in new (Entity, string, object?)[]
        { (clause, "active", true), (selected.Item!, "$parent", seller), (payment, "$parent", buyer) })
            StateView.Learn(mind, entity, field, value);
        var intent = book.Offers(mind, buyer).Single();
        session.Submit(intent);
        session.Timeline.RunUntil(0);
        Require(selected.Item!.Parent == buyer && payment.Parent == seller, "选择后的契约未交换双方物品");
        Require(!session.Runtime.ResolveDetailed(intent).Allowed, "契约被重复执行");
        Require(proposals.Where(p => p.Item != selected.Item).All(p => p.Item.Parent == seller), "未选方案改变了世界");
    }

    private static void GeneratedDeadlines()
    {
        foreach (var budget in new[] { 0d, 1d, 3d })
        foreach (var fulfil in new[] { false, true })
        {
            var (world, actor, mind, session) = Setup(budget);
            using var cleanup = session;
            using var book = new PatternBook(session);
            var target = new Entity("工作", world);
            target.Set("loss", 0d);
            var clause = book.Add(Patterns.Deadline(actor, actor, target, "finish", 10,
                F("change", target, "loss", 2d)));
            if (fulfil) session.Timeline.Schedule(10, "AI 选择准时履约", () =>
            {
                session.Runtime.ResolveDetailed(new Intent(actor, mind, F("finish", actor, target), []));
                return null;
            });
            session.Timeline.RunUntil(1000);
            var paid = !fulfil && budget >= 2 ? 2d : 0d;
            Near(actor.Get<double>("charges"), budget - paid, "期限后果预算错误");
            Near(target.Get<double>("loss"), paid, "期限重复生效或免费施加后果");
            Require(!clause.Get<bool>("active"), "期限结束仍保留活动状态");
            Require(clause.Get<bool>("completed") == fulfil, "履约状态错误");
        }
    }

    private static void GeneratedSelfBinding()
    {
        var (_, actor, mind, session) = Setup(20);
        using var cleanup = session;
        using var book = new PatternBook(session);
        var clause = book.Add(Patterns.SelfBinding(actor, actor, "open"));
        StateView.Learn(mind, clause, "text", clause.Name);
        session.Runtime.ResolveDetailed(book.Offers(mind, actor).Single());
        var word = WordIntent(Words(actor, 1).Last(), actor, mind);
        var result = session.Runtime.ResolveDetailed(word);
        Require(result.Result?.Succeeded == true, "自缚后无法施法");
        Near(actor.Get<double>("charges"), 17, "首笔4单位支付应只折扣1");
        session.Runtime.ResolveDetailed(new Intent(actor, mind, F("open", actor), []));
        session.Runtime.ResolveDetailed(word);
        Near(actor.Get<double>("charges"), 13, "破约后仍享有折扣");
    }

    private static void HiddenProhibition()
    {
        var (world, actor, mind, session) = Setup();
        using var cleanup = session;
        using var book = new PatternBook(session);
        var target = new Entity("门", world);
        target.Set("opened", false);
        StateView.Learn(mind, target, "opened", false);
        var open = new Intent(actor, mind, F("invoke_open", actor, target), [F("set", target, "opened", true)]);
        book.Add(Patterns.Prohibit(actor, actor, "open", target, hard: true));
        Require(session.Runtime.Prepare(open, new StateView(mind)).Succeeded, "未知条款提前泄露");
        Require(!session.Runtime.ResolveDetailed(open).Allowed && !target.Get<bool>("opened"), "换行动名称绕过已知效果约束");
    }

    private double LongEconomy(double horizon, double price)
    {
        var (_, actor, mind, session) = Setup(8);
        using var cleanup = session;
        double supplied = 0, spent = 0;
        var casts = 0;
        var wait = Stopwatch.StartNew();
        session.Timeline.Schedule(100, "有限速率供应", () =>
        {
            actor.Set("charges", actor.Get<double>("charges") + 2d);
            supplied += 2;
            return 100;
        });
        mind.InstallStep((soul, body, _, _) => Words(actor, price).Select(r => WordIntent(r, actor, soul)));
        session.Activate(actor, mind, idleCost: 10,
            utility: intent => Convert.ToSingle(((Effect)intent.Definition!.Entries("Effect").Single().Value).Args[2]),
            completed: (intent, result) =>
            {
                if (result?.Result?.Succeeded != true) return;
                casts++;
                spent += ((StateChange[])result.Result.Outcome!).Where(c => c.Field == "charges")
                    .Sum(c => Convert.ToDouble(c.Before) - Convert.ToDouble(c.After));
            });
        var steps = session.Timeline.RunUntil(horizon, maxSteps: (int)horizon + 100);
        Near(session.Timeline.Now, horizon, "长程运行被步骤预算截断");
        Near(actor.Get<double>("charges"), 8 + supplied - spent, "长程资源账不平");
        Near(actor.Get<double>("progress") * price, spent, "收益未支付相应材料");
        Require(casts > 0 && actor.Get<double>("charges") >= 0, "没有施法或资源透支");
        Require(session.Timeline.Queue.Count == 2, "长期调度积累多余任务");
        _metrics.Add($"| {horizon:g} | {price:g} | {casts} | {supplied:g} | {spent:g} | {actor.Get<double>("charges"):g} | {actor.Get<double>("progress"):g} | {steps} | {wait.ElapsedMilliseconds} |");
        return actor.Get<double>("progress");
    }

    private void ProbeOpenConstruction()
    {
        var (world, actor, mind, session) = Setup(1);
        using var cleanup = session;
        // 不把已知漏洞写成必须存在的断言；修复后探针应自然消失。
        var mint = KotodamaFactory.CreateWordSpirit(effects: [F("set", actor, "charges", 1000d)]);
        var result = session.Runtime.Words.Invoke(mint, mind, new Dictionary<string, object>());
        if (result != null && actor.Get<double>("charges") > 1)
            Findings.Add("F01：底层配方接受无成本资源赋值；开放式 AI 需要字段权限与按真实增量计费。");
        var budgetProbe = new Entity("后果预算探针", world);
        budgetProbe.Set("charges", 1d);
        if (Balance.ConsequenceBudget(session.Runtime.Effects.Prepare([F("set", budgetProbe, "charges", 1000d)]).Changes) < 999)
            Findings.Add("F02：条款预算对 set 按单项收费，未按资源增量计价。");

        var risk = new Entity("隐藏风险", world);
        mind.Observe(E("observed_anomaly", risk));
        var before = RuleTemplate.SampleCandidateRules(mind, actor, session.Runtime.Env, []).Count;
        risk.SetTag("Danger", 1);
        var after = RuleTemplate.SampleCandidateRules(mind, actor, session.Runtime.Env, []).Count;
        if (before != after)
            Findings.Add("F03：同一已观察异常的规则候选受隐藏客观 Danger 状态影响。");

        using var book = new PatternBook(session);
        var clause = book.Add(Patterns.Prohibit(actor, actor, "rest", hard: true));
        var rest = new Intent(actor, mind, F("rest", actor), []);
        if (!clause.Get<bool>("accepted") && !session.Runtime.Review(rest).Allowed)
            Findings.Add("F04：PatternBook.Add 可直接激活未采信硬条款；创建权限与双方同意须由上层明确控制。");
    }

    public string Report() => $"""
        # AI 构造平衡检查

        生成于 {DateTimeOffset.UtcNow:O}；.NET {Environment.Version}；{System.Runtime.InteropServices.RuntimeInformation.OSDescription}。
        不变量失败：{Failed}；开放式构造风险：{Findings.Count}。
        测试 AI 为确定性候选生成与效用选择，未调用外部大模型；仅本测试生成的有界候选参与经济批跑。
        风险未清零时，不能认定开放式 AI 创建已经平衡。--strict 将风险作为退出码 2。

        {string.Join("\n", _results)}

        ## 长程预算结果

        | AV | 单位价格 | 施法次数 | 外生供应 | 支付 | 余额 | 进展 | 调度步数 | 毫秒 |
        | --- | --- | --- | --- | --- | --- | --- | --- | --- |
        {string.Join("\n", _metrics)}

        供应每 100 AV 增加 2，初始库存 8，普通行动成本 10 AV。进展是测试产出，不代表人物成长。
        100,000 AV 仅检验单主体经济循环；不代表多人文学质量、内存有界或长期人格稳定。

        ## 风险探针

        {string.Join("\n", Findings.Select(f => "- " + f))}
        """;
}
