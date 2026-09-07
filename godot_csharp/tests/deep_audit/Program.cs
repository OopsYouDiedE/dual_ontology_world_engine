using System;
using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;
using DualOntologyWorldEngine.Examples.ZooSimulator;

namespace DualOntologyWorldEngine.Tests.DeepAudit;

public static class Program
{
    public static int Main()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine(">>> [独立审查专家 Round 5] 深度穿透验证开始 <<<");
        Console.WriteLine("================================================================================\n");

        AuditValidationA();
        AuditValidationB();
        AuditValidationC();
        AuditValidationD();
        AuditValidationE();
        AuditValidationF();
        AuditCodeMechanisms();

        Console.WriteLine("================================================================================");
        Console.WriteLine(">>> [独立审查专家 Round 5] 深度穿透验证结束 <<<");
        Console.WriteLine("================================================================================");
        return 0;
    }

    private static void AuditValidationA()
    {
        Console.WriteLine("---------------- [审计 A：相同规则，不同角色] ----------------");
        
        // A1：原测试复现（人工注入状态）
        {
            using var world = new ZooWorld(seed: 101, visitorCount: 2);
            var rules = new ZooRules(world);
            rules.ApplyPreset("standard");
            using var anomalies = new ZooAnomalies(world);

            var v1 = world.Visitors[0];
            var v2 = world.Visitors[1];
            v1.Reparent(world.ElephantArea);
            v2.Reparent(world.ElephantArea);

            v1.Attach("calibrated", world.ElephantSign);
            v1.Set("has_dotted_paper", true);
            v1.Set("fear", 2.5);

            v2.Set("has_dotted_paper", false);
            v2.Set("fear", 0.0);
            v2.Set("preferred_exhibit", world.PrimateArea);

            ZooMinds.ActivateAll(world, rules);
            world.Run(until: 10);
            Console.WriteLine($"[A1-原测试] V1终态场景={v1.CurrentScene?.Name}, 庇护={v1.Get<bool>("protected")}; V2终态场景={v2.CurrentScene?.Name}, 庇护={v2.Get<bool>("protected")}");
        }

        // A2：完全自主推演（从Entrance出发，仅凭初始属性与偏好差异）
        {
            using var world = new ZooWorld(seed: 101, visitorCount: 2);
            var rules = new ZooRules(world);
            rules.ApplyPreset("standard");
            using var anomalies = new ZooAnomalies(world);

            var v1 = world.Visitors[0]; // Adult, preferred exhibit
            var v2 = world.Visitors[1]; // Child (age=8)
            v1.Set("preferred_exhibit", world.LionArea);
            v2.Set("preferred_exhibit", world.RabbitArea);

            ZooMinds.ActivateAll(world, rules);
            world.Run(until: 20);

            var trace1 = world.Session.Runtime.Events.Where(e => ReferenceEquals(e.Actor(), v1)).Select(e => e.Effect()?.ToString()).ToList();
            var trace2 = world.Session.Runtime.Events.Where(e => ReferenceEquals(e.Actor(), v2)).Select(e => e.Effect()?.ToString()).ToList();
            bool diff = !trace1.SequenceEqual(trace2) || !ReferenceEquals(v1.CurrentScene, v2.CurrentScene) || v1.Get<double>("fear") != v2.Get<double>("fear") || v1.Get<double>("erosion") != v2.Get<double>("erosion");
            Console.WriteLine($"[A2结论] 无人工中途干预下，两角色是否自主演化出不同路径与后果: {diff}\n");
        }
    }

    private static void AuditValidationB()
    {
        Console.WriteLine("---------------- [审计 B：相同角色，不同信息] ----------------");

        // B1：原测试复现（DrinkShop 场景）
        List<string> actionsStandard = [];
        List<string> actionsNone = [];
        {
            using var w1 = new ZooWorld(seed: 202, visitorCount: 1);
            var r1 = new ZooRules(w1);
            r1.ApplyPreset("standard");
            using var a1 = new ZooAnomalies(w1);
            var v1 = w1.Visitors[0];
            v1.Reparent(w1.DrinkShop);
            v1.Set("thirst", 2.0);
            w1.Session.Runtime.Executed += (i, res) => { if (ReferenceEquals(i.Actor, v1) && res.Allowed) actionsStandard.Add(i.Label); };
            ZooMinds.ActivateAll(w1, r1);
            w1.Run(until: 10);

            using var w2 = new ZooWorld(seed: 202, visitorCount: 1);
            var r2 = new ZooRules(w2);
            r2.ApplyPreset("none");
            using var a2 = new ZooAnomalies(w2);
            var v2 = w2.Visitors[0];
            v2.Reparent(w2.DrinkShop);
            v2.Set("thirst", 2.0);
            w2.Session.Runtime.Executed += (i, res) => { if (ReferenceEquals(i.Actor, v2) && res.Allowed) actionsNone.Add(i.Label); };
            ZooMinds.ActivateAll(w2, r2);
            w2.Run(until: 10);

            Console.WriteLine($"[B1-标准守则组] 首3行动: {string.Join(" -> ", actionsStandard.Take(3))}");
            Console.WriteLine($"[B1-无守则组]   首3行动: {string.Join(" -> ", actionsNone.Take(3))}");
        }

        // B2：大象展位针对性规则测试（守则：在大象区核对标示牌）
        {
            using var w1 = new ZooWorld(seed: 202, visitorCount: 1);
            var r1 = new ZooRules(w1);
            r1.ApplyPreset("standard");
            using var a1 = new ZooAnomalies(w1);
            var v1 = w1.Visitors[0];
            v1.Reparent(w1.ElephantDisplay);
            v1.Set("fear", 2.0);
            string firstActionStandard = "";
            w1.Session.Runtime.Executed += (i, res) => { if (string.IsNullOrEmpty(firstActionStandard) && ReferenceEquals(i.Actor, v1)) firstActionStandard = i.Label; };
            ZooMinds.ActivateAll(w1, r1);
            w1.Run(until: 3);

            using var w2 = new ZooWorld(seed: 202, visitorCount: 1);
            var r2 = new ZooRules(w2);
            r2.ApplyPreset("none");
            using var a2 = new ZooAnomalies(w2);
            var v2 = w2.Visitors[0];
            v2.Reparent(w2.ElephantDisplay);
            v2.Set("fear", 2.0);
            string firstActionNone = "";
            w2.Session.Runtime.Executed += (i, res) => { if (string.IsNullOrEmpty(firstActionNone) && ReferenceEquals(i.Actor, v2)) firstActionNone = i.Label; };
            ZooMinds.ActivateAll(w2, r2);
            w2.Run(until: 3);

            Console.WriteLine($"[B2-大象展区] 知道守则者首选行动: {firstActionStandard}");
            Console.WriteLine($"[B2-大象展区] 无守则者首选行动:   {firstActionNone}");
        }

        // B3：核查海洋馆通道认知隔绝
        {
            using var w = new ZooWorld(seed: 202, visitorCount: 1);
            var v = w.Visitors[0];
            bool knowsAquarium = v.Mind!.KnownPassages.Any(p => ReferenceEquals(p.Left, w.AquariumGate) || ReferenceEquals(p.Right, w.AquariumGate));
            Console.WriteLine($"[B3-拓扑审查] 游客初始主观心智是否已知海洋馆通道: {knowsAquarium} (注: 物理拓扑已将海洋馆设为认知隐蔽通道)");
            Console.WriteLine();
        }
    }

    private static void AuditValidationC()
    {
        Console.WriteLine("---------------- [审计 C：移除规则] ----------------");

        // C1：暗室庇护补丁（RestPointShelterPatch）真实轨迹追踪
        double erosionWithPatch = 0;
        double erosionWithoutPatch = 0;
        List<string> traceWithPatch = [];
        List<string> traceWithoutPatch = [];
        {
            using var w1 = new ZooWorld(seed: 303, visitorCount: 1);
            var r1 = new ZooRules(w1);
            r1.ApplyPreset("standard");
            using var a1 = new ZooAnomalies(w1);
            var v1 = w1.Visitors[0];
            v1.Reparent(w1.RestPoint);
            v1.Set("fatigue", 3.0);
            w1.Session.Runtime.Executed += (i, res) => { if (ReferenceEquals(i.Actor, v1) && res.Allowed) traceWithPatch.Add($"[{w1.Session.Timeline.Now:F1}] {i.Label} (在{v1.CurrentScene?.Name}, 侵蚀={v1.Get<double>("erosion"):F1})"); };
            ZooMinds.ActivateAll(w1, r1);
            w1.Run(until: 5);
            erosionWithPatch = v1.Get<double>("erosion");

            using var w2 = new ZooWorld(seed: 303, visitorCount: 1);
            var r2 = new ZooRules(w2);
            r2.ApplyPreset("standard");
            using var a2 = new ZooAnomalies(w2);
            var v2 = w2.Visitors[0];
            v2.Reparent(w2.RestPoint);
            v2.Set("fatigue", 3.0);
            w2.Session.Runtime.Patches.Remove(w2.RestPointShelterPatch);
            w2.Session.Runtime.Executed += (i, res) => { if (ReferenceEquals(i.Actor, v2) && res.Allowed) traceWithoutPatch.Add($"[{w2.Session.Timeline.Now:F1}] {i.Label} (在{v2.CurrentScene?.Name}, 侵蚀={v2.Get<double>("erosion"):F1})"); };
            ZooMinds.ActivateAll(w2, r2);
            w2.Run(until: 5);
            erosionWithoutPatch = v2.Get<double>("erosion");

            Console.WriteLine($"[C1-暗室保护] 存在补丁最终侵蚀度: {erosionWithPatch:F1} | 轨迹: {string.Join("; ", traceWithPatch)}");
            Console.WriteLine($"[C1-暗室保护] 移除补丁最终侵蚀度: {erosionWithoutPatch:F1} | 轨迹: {string.Join("; ", traceWithoutPatch)}");
        }

        // C2：大象纯洁补丁（ElephantAreaPurityPatch）移除实测
        {
            using var w = new ZooWorld(seed: 303, visitorCount: 1);
            var v = w.Visitors[0];
            v.Set("thirst", 5.0); // 确保渴度充足
            v.Reparent(w.ElephantDisplay);
            w.RabbitBlood.Reparent(v); // 随身携带兔子血

            // 尝试直接在大象展位饮用兔子血
            var consumeIntent = new Intent(v, v.Mind!, AST.F("consume", v, w.RabbitBlood), [AST.F("change", v, "thirst", -2.5)], 1.0, "饮用兔子血");
            
            // 补丁存在时审查
            var reviewWithPatch = w.Session.Runtime.Review(consumeIntent, new ActionContext(Scene: w.ElephantDisplay, Domains: new[] { w.ElephantDisplay }));
            
            // 移除补丁后审查
            w.Session.Runtime.Patches.Remove(w.ElephantAreaPurityPatch);
            var reviewWithoutPatch = w.Session.Runtime.Review(consumeIntent, new ActionContext(Scene: w.ElephantDisplay, Domains: new[] { w.ElephantDisplay }));

            Console.WriteLine($"[C2-纯洁补丁] 补丁存在时 Core Review 结果: Allowed={reviewWithPatch.Allowed}, Reason={reviewWithPatch.Reason}");
            Console.WriteLine($"[C2-纯洁补丁] 补丁移除后 Core Review 结果: Allowed={reviewWithoutPatch.Allowed}, Reason={reviewWithoutPatch.Reason}");
            Console.WriteLine();
        }
    }

    private static void AuditValidationD()
    {
        Console.WriteLine("---------------- [审计 D：错误行为的未知后果] ----------------");

        // D1：多重异常源叠加计算实测
        using (var w = new ZooWorld(seed: 404, visitorCount: 1))
        {
            var rules = new ZooRules(w);
            rules.ApplyPreset("standard");
            using var anomalies = new ZooAnomalies(w);
            var v = w.Visitors[0];
            
            // 将角色置于右街 (resonance=2.0)
            v.Reparent(w.RightStreet);
            // 随身放兔子血 (resonance=2.5)
            w.RabbitBlood.Reparent(v);
            // 随身放山羊肉 (resonance=3.0)
            w.GoatMeat.Reparent(v);

            // 预期共鸣: 2.0 + 2.5 + 3.0 = 7.5
            // 单次行动产生的 attention 增量应为 7.5, erosion 增量应为 7.5 * 0.35 = 2.625
            ZooMinds.ActivateAll(w, rules);
            w.Run(until: 2);

            string sceneName = v.CurrentScene?.Name ?? "已脱离";
            Console.WriteLine($"[D1-三重异常叠加] 终态: 场景={sceneName}, 关注={v.Get<double>("attention"):F2}, 侵蚀={v.Get<double>("erosion"):F2}, 记录数={anomalies.Records.Count}");
        }

        // D2：受污染角色在海洋馆食用正常食物（普通饼干）
        using (var w = new ZooWorld(seed: 404, visitorCount: 1))
        {
            var rules = new ZooRules(w);
            rules.ApplyPreset("standard");
            using var anomalies = new ZooAnomalies(w);
            var v = w.Visitors[0];
            v.Set("erosion", 2.0);
            v.Reparent(w.AquariumLobby);
            w.AquariumFood.Reparent(v); // 拿取免费饼干
            v.Set("thirst", 3.0);

            ZooMinds.ActivateAll(w, rules);
            w.Run(until: 3);

            Console.WriteLine($"[D2-轻度污染食饼干] 终态: 场景={v.CurrentScene?.Name}, 侵蚀={v.Get<double>("erosion"):F1}, 异常记录数={anomalies.Records.Count}");
        }

        // D3：无守则角色直接触摸逃跑兔子
        using (var w = new ZooWorld(seed: 404, visitorCount: 1))
        {
            var rules = new ZooRules(w);
            rules.ApplyPreset("none"); // 无守则
            using var anomalies = new ZooAnomalies(w);
            var v = w.Visitors[0];
            v.Reparent(w.RabbitBushes); // 与出逃兔子同场景

            w.Session.Runtime.Executed += (i, res) =>
            {
                if (ReferenceEquals(i.Actor, v))
                    Console.WriteLine($"[D3执行轨迹] {i.Label} | Allowed={res.Allowed} | 侵蚀={v.Get<double>("erosion"):F1}");
            };

            ZooMinds.ActivateAll(w, rules);
            w.Run(until: 3);

            Console.WriteLine($"[D3-无守则碰兔子] 终态: 侵蚀={v.Get<double>("erosion"):F1}, 恐惧={v.Get<double>("fear"):F1}, 异常事件数={anomalies.Records.Count}");
            Console.WriteLine();
        }
    }

    private static void AuditValidationE()
    {
        Console.WriteLine("---------------- [审计 E：真实因果链顺序改变] ----------------");

        // 彻底废除原测试的 Schedule 匿名委托注入，完全使用真实自主行为序列测试！
        // 场景：两组世界中，角色都在狮区假山，且有兔子血在背包（高辐射源）。
        // 序列 1：白狮先吼叫（lionDelay=0.0），赋予庇护；随后角色在下一跳行动中触发随身辐射，庇护被消耗抵消侵蚀。
        // 序列 2：白狮延迟吼叫（lionDelay=5.0）；角色在 t=1.0 先行动，此时无庇护，随身辐射直接造成侵蚀；随后白狮吼叫净化部分侵蚀。

        double erosionSeq1 = 0;
        double erosionSeq2 = 0;
        bool shelteredConsumedInSeq1 = false;
        {
            using var w1 = new ZooWorld(seed: 505, visitorCount: 0);
            var r1 = new ZooRules(w1);
            r1.ApplyPreset("standard");
            using var a1 = new ZooAnomalies(w1);
            var v1 = w1.SpawnVisitor("访客E1", w1.LionRockery, fear: 2.0);
            w1.RabbitBlood.Reparent(v1); // 随身带兔子血辐射源

            ZooMinds.ActivateAll(w1, r1, lionDelay: 0.0); // 白狮先行动
            w1.Run(until: 6);

            erosionSeq1 = v1.Get<double>("erosion");
            shelteredConsumedInSeq1 = a1.Records.Any(r => r.Mechanism == "大象庇护抵消");
        }

        {
            using var w2 = new ZooWorld(seed: 505, visitorCount: 0);
            var r2 = new ZooRules(w2);
            r2.ApplyPreset("standard");
            using var a2 = new ZooAnomalies(w2);
            var v2 = w2.SpawnVisitor("访客E2", w2.LionRockery, fear: 2.0);
            w2.RabbitBlood.Reparent(v2); // 随身带兔子血辐射源

            ZooMinds.ActivateAll(w2, r2, lionDelay: 4.0); // 白狮延迟行动
            w2.Run(until: 6);

            erosionSeq2 = v2.Get<double>("erosion");
        }

        Console.WriteLine($"[E-真实因果顺序 1 (白狮先吼叫赋予庇护)]: 侵蚀={erosionSeq1:F2}, 庇护抵消触发={shelteredConsumedInSeq1}");
        Console.WriteLine($"[E-真实因果顺序 2 (白狮延迟吼叫)]:         侵蚀={erosionSeq2:F2}");
        Console.WriteLine($"[E结论] 真实时间线行动顺序差异导致终局状态不同: {erosionSeq1 != erosionSeq2}\n");
    }

    private static void AuditValidationF()
    {
        Console.WriteLine("---------------- [审计 F：多随机种子长周期稳定性] ----------------");
        int[] seeds = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        int successCount = 0;

        foreach (var s in seeds)
        {
            try
            {
                using var w = new ZooWorld(seed: s, visitorCount: 4);
                var r = new ZooRules(w);
                r.ApplyPreset("standard");
                using var a = new ZooAnomalies(w);
                ZooMinds.ActivateAll(w, r);

                int steps = w.Run(until: 180); // 运行到 180 时刻
                int activeCount = w.Visitors.Count(v => v.CurrentScene != null);
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"种子 {s} 异常崩溃: {ex.Message}");
            }
        }
        Console.WriteLine($"[F-长周期运行结果] 10 个随机种子全部平稳运行至 180 时刻: {successCount == 10}/10\n");
    }

    private static void AuditCodeMechanisms()
    {
        Console.WriteLine("---------------- [深度静态机制穿透核对] ----------------");

        // 核对 1：Core 原生边际效用衰减在重复行动上的生效情况
        {
            var soul = new Soul("测试心智");
            var actor = new Entity("测试主体");
            var target = new Entity("兔子");
            var intent = new Intent(actor, soul, AST.F("watch", actor, target), [], 1.0);
            
            // 构造 3 次历史事件
            var historyEvents = new List<Entity>();
            for (int i = 0; i < 3; i++)
            {
                var ev = RuleEntityFactory.CreateEvent("观赏", actor, intent.Effect);
                ev.SetChanges([]);
                historyEvents.Add(ev);
            }

            var env = new RuleEnvironment();
            var rules = new RuleSystem();
            var ctx = new ActionContext(Events: historyEvents);
            var decisions = soul.Assess([intent], rules, env, null, ctx);

            Console.WriteLine($"[机制核对 1] 重复3次后 Core 原生边际效用衰减罚分: {decisions[0].RepetitionPenalty} (期望: 90)");
        }

        // 核对 2：Core 原生 PatchAllows 在未定义作用域或世界树祖先上的表现
        {
            var world = new Entity("世界");
            var room = new Entity("房间", world);
            var actor = new Character("小明", room);
            var soul = new Soul("小明心智");
            actor.Bind(soul);

            var patch = RuleEntityFactory.CreateRulePatch("房间禁令",
                new Rule(AST.E("true"), [new(Mode.Forbid, AST.F("shout", RuleSlot.Actor))]),
                parent: room, scope: room);

            var env = new RuleEnvironment();
            var rules = new RuleSystem();

            var shoutIntent = new Intent(actor, soul, AST.F("shout", actor), []);
            bool roomAllows = rules.PatchAllows(shoutIntent, [patch], env, new ActionContext(Scene: room, Domains: new[] { room }));
            bool otherAllows = rules.PatchAllows(shoutIntent, [patch], env, new ActionContext(Scene: world, Domains: new[] { world }));

            Console.WriteLine($"[机制核对 2] Core 原生 PatchAllows 作用域隔离: 房间内允许={roomAllows}, 房间外允许={otherAllows}");
        }
        Console.WriteLine();
    }
}
