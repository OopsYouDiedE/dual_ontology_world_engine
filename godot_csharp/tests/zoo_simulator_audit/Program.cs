using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;
using DualOntologyWorldEngine.Examples.ZooSimulator;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Tests.ZooSimulatorAudit;

public static class Program
{
    private static int assertions;
    private static void Check(bool condition,string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
    public static int Main()
    {
        try
        {
            Console.WriteLine("文书目录"); Catalog();
            Console.WriteLine("知识与重放"); KnowledgeAndReplay();
            Console.WriteLine("连续暴露"); ContinuousExposure();
            Console.WriteLine("物资竞争"); InventoryCompetition();
            Console.WriteLine("信号与救援"); PaperAndRescue();
            Console.WriteLine("时间与认知范围"); CalendarAndKnowledgeScope();
            Console.WriteLine("不明显规则抽取消融测试"); RuleAblationExperiment();
            Console.WriteLine("开放推演"); Population();
            Console.WriteLine($"通过 {assertions} 项断言：文书完整性、知识/机制隔离、固定行动重放、暴露消融、资源竞争、求助因果及多种子运行。");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static void Catalog()
    {
        using var w=new ZooWorld(1,2);
        var r=new ZooRules(w);
        Check(r.Clauses.Count==97,"原文条目遗漏");
        Check(r.Clauses.Select(c=>c.Key).Distinct().Count()==97,"条目编号重复");
        Check(r.Clauses.Count(c=>c.Advice==null)==1,"声明与行动条款未区分");
        Check(r.Clauses.All(c=>c.Text.Length>5 && !c.Text.Contains("同上文")),"混入解析或空白文本");
        Check(w.Session.Runtime.Patches.Count==0,"文书被注册为客观规则补丁");
        r.ApplyPreset("standard");
        int count=w.Visitors[0].Mind!.Beliefs.Count;
        r.SyncBeliefs();
        Check(w.Visitors[0].Mind!.Beliefs.Count==count,"同步文书重复创建信念");
        r.ApplyPreset("none");
        Check(w.Visitors[0].Mind!.Beliefs.All(b=>!b.HasTag(CommonTag.Accepted)),"切换初始知识未撤销原信念");
    }

    private static Intent Offer(ZooWorld w,ZooRules r,Character c,string op,Entity? target=null) =>
        ZooMinds.Offers(w,c,r).First(i=>i.Effect.Op==op && (target==null || ReferenceEquals(i.Target,target)));
    private static ActionResolution Execute(ZooWorld w,Intent i)
    {
        ActionResolution? result=null;
        double due=w.Session.ReadyAt(i.Actor);
        w.Session.Submit(i,r=>result=r);
        w.Run(due);
        return result ?? throw new InvalidOperationException("命令没有结算");
    }
    private static string Shape(Intent i)=>$"{i.Effect.Op}/{i.Target?.Name}/{i.MotionCost}";
    private static double[] State(Character c)=>[c.Get<double>("health"),c.Get<double>("erosion"),c.Get<double>("attention"),c.Get<double>("fear"),c.Get<double>("fatigue")];

    private static void KnowledgeAndReplay()
    {
        using (var w=new ZooWorld(8,0))
        {
            var c=w.SpawnVisitor("受试者",w.DrinkShop);
            var r=new ZooRules(w);
            w.RabbitBlood.Reparent(w.DrinkShelf);
            w.RabbitBlood.Set("active",true);
            r.ApplyPreset("none");
            var before=ZooMinds.Offers(w,c,r).Select(Shape).ToArray();
            var consume=Offer(w,r,c,"consume",w.RabbitBlood);
            double unadvised=r.Score(c,consume);
            r.Learn(c,"Visitor");
            var after=ZooMinds.Offers(w,c,r).Select(Shape).ToArray();
            Check(before.SequenceEqual(after),"知道守则改变了客观可行动作集合");
            Check(r.Score(c,consume)<unadvised,"文书没有影响判断");
        }
        double[] Replay(string knowledge,string uniform)
        {
            using var w=new ZooWorld(11,0);
            var room=w.Scene("独立实验场景");
            var c=w.SpawnVisitor("受试者",room,uniform);
            var r=new ZooRules(w);
            r.ApplyPreset(knowledge);
            w.RabbitBlood.Reparent(room);
            w.RabbitBlood.Set("active",true);
            using var mechanisms=new ZooAnomalies(w);
            foreach (var op in new[] { "take","consume","drop" })
                Check(Execute(w,Offer(w,r,c,op,w.RabbitBlood)).Result?.Succeeded==true,"固定行动没有成功");
            w.Run(8);
            return State(c);
        }
        var bare=Replay("none","普通便服");
        Check(bare.SequenceEqual(Replay("all","普通便服")),"客观后果读取了守则采信状态");
        Check(bare.SequenceEqual(Replay("none","清洁工制服")),"制服字符串提供了无依据的防护");
    }

    private static void ContinuousExposure()
    {
        double[] Run(double intensity,double actionCost,bool contained=false)
        {
            using var w=new ZooWorld(19,0);
            var room=w.Scene("隔离场景");
            var c=w.SpawnVisitor("受试者",room);
            Entity parent=contained ? ZooWorld.Item("密封箱",c,new() { ["containment"]=true }) : room;
            ZooWorld.Item("未在任何守则中命名的源",parent,new() { ["anomaly_resonance"]=intensity });
            using var mechanisms=new ZooAnomalies(w);
            c.Mind!.InstallStep((_,_,_,_)=>[new Intent(c,c.Mind,F("wait",c,room),[],actionCost)]);
            w.Session.Activate(c,c.Mind);
            w.Run(12);
            return State(c);
        }
        var fast=Run(2,1);
        Check(fast.SequenceEqual(Run(2,4)),"暴露与动作次数耦合");
        var removed=Run(0,1);
        Check(fast[1]>removed[1],"删除危险源没有降低侵蚀");
        Check(Run(2,1,true)[1]==removed[1],"封存容器仍然泄漏源质");
        Check(Run(4,1)[1]>fast[1],"改变机制强度未改变结果");
    }

    private static void InventoryCompetition()
    {
        using var w=new ZooWorld(23,0);
        var r=new ZooRules(w);
        var a=w.SpawnVisitor("甲",w.DrinkShop);
        var b=w.SpawnVisitor("乙",w.DrinkShop);
        w.NormalDrinks.Set("count",1d);
        var first=Offer(w,r,a,"consume",w.NormalDrinks);
        var stale=Offer(w,r,b,"consume",w.NormalDrinks);
        Check(Execute(w,first).Result?.Succeeded==true,"第一份饮料未消费");
        Check(Execute(w,stale).Result?.Succeeded!=true,"过期候选重复消费同一份物资");
        Check(w.NormalDrinks.Get<double>("count")==0,"库存非守恒");
        var take=Offer(w,r,a,"take",w.RabbitPlush);
        var competing=Offer(w,r,b,"take",w.RabbitPlush);
        Check(Execute(w,take).Result?.Succeeded==true,"拿取失败");
        Check(Execute(w,competing).Result?.Succeeded!=true,"过期拿取能跨背包窃取物资");
        Check(w.RabbitPlush.Parent==a,"唯一物件的持有人错误");
    }

    private static void PaperAndRescue()
    {
        using (var w=new ZooWorld(29,0,startMinute:30))
        {
            var r=new ZooRules(w);
            var a=w.SpawnVisitor("投纸者",w.ElephantLawn);
            var b=w.SpawnVisitor("旁观者",w.ElephantArea);
            Check(Execute(w,Offer(w,r,a,"tear")).Result?.Succeeded==true,"撕纸失败");
            Check(Execute(w,Offer(w,r,a,"drop_paper",w.ElephantLawn)).Result?.Succeeded==true,"投纸失败");
            using var mechanisms=new ZooAnomalies(w);
            w.Run(4);
            Check(a.Get<Entity>("paper")!.Parent==w.ElephantLawn,"纸条位置未来自动作");
            Check(w.SecurityGuard.Get<Entity>("requester")==null,"机制替保安远程接收了求助");
            Check(Execute(w,Offer(w,r,w.SecurityGuard,"move",w.ElephantArea)).Result?.Succeeded==true,"保安出门失败");
            Check(Execute(w,Offer(w,r,w.SecurityGuard,"move",w.ElephantLawn)).Result?.Succeeded==true,"保安到场失败");
            Check(Execute(w,Offer(w,r,w.SecurityGuard,"read_signal")).Result?.Succeeded==true,"保安读信失败");
            Check(w.SecurityGuard.Get<Entity>("requester")==a,"求助身份由场景布尔值错误推断");
            Check(!a.Get<bool>("protected") && !b.Get<bool>("protected"),"投纸直接写入庇护结局");
        }
        double Exposure(bool lions)
        {
            using var w=new ZooWorld(31,0);
            var c=w.SpawnVisitor("观众",w.LionRockery);
            ZooWorld.Item("未知源",w.LionRockery,new() { ["anomaly_resonance"]=2d });
            foreach (var l in w.Lions) l.Set("active",lions);
            using var mechanisms=new ZooAnomalies(w);
            w.Run(12);
            return c.Get<double>("erosion");
        }
        Check(Exposure(true)<Exposure(false),"白狮的实际行为没有产生可消融的作用");
    }

    private static void Population()
    {
        var traces=new HashSet<string>();
        foreach (int seed in new[] { 1,7,42,83,191,2026 })
        foreach (string preset in new[] { "none","standard","all","corrupted" })
        {
            using var w=new ZooWorld(seed,4,startMinute:seed%2==0 ? 20 : 950);
            var r=new ZooRules(w);
            r.ApplyPreset(preset);
            using var mechanisms=new ZooAnomalies(w);
            var ledger=w.People.ToDictionary(c=>c,c=>State(c));
            w.Session.Runtime.Executed+=(i,result)=>
            {
                if (result.Result?.Succeeded!=true || result.Event==null) return;
                foreach (var change in result.Event.GetChanges())
                {
                    if (change.Subject is not Character c) continue;
                    if (change.Field=="$parent" && change.Before is Entity from && change.After is Entity to)
                        Check(w.Topology.Adjacency[from].Any(e=>e.Next==to),"角色未经通道瞬移");
                    int field=Array.IndexOf(new[] { "health","erosion","attention","fear","fatigue" },change.Field);
                    if (field<0) continue;
                    Check(Math.Abs(ledger[c][field]-Convert.ToDouble(change.Before))<1e-8,"状态被事件系统外写入");
                    ledger[c][field]=Convert.ToDouble(change.After);
                }
            };
            ZooMinds.ActivateAll(w,r);
            int steps=w.Run(180,100000);
            Check(steps<100000 && !w.Session.Timeline.Queue.Any(t=>t.Time<=180),"推演截断或行动值无法前进");
            foreach (var c in w.People)
            {
                Check(State(c).All(v=>double.IsFinite(v) && v>=0),"状态出现负数或非有限值");
                Check(State(c).SequenceEqual(ledger[c]),"最终状态无法由因果记录还原");
                Check(c.Get<double>("health")>0 || w.Outcome(c)=="死亡","死亡判定不来自生命值");
            }
            Check(w.FoodSupply.Get<double>("count")>=0 && w.World.Get<double>("budget")>=0,"资源透支");
            string trace=string.Join('|',w.Session.Runtime.Events.Where(e=>e.Actor() is Character c && w.Visitors.Contains(c))
                .Select(e=>$"{e.Actor()!.Name}:{e.Effect()?.Op}:{e.GetIntent()?.Target?.Name}"));
            traces.Add(trace);
        }
        Check(traces.Count>4,"多个种子与知识状态只重复同一个预设脚本");
        Console.WriteLine($"24 次开放推演产生 {traces.Count} 条不同轨迹；未要求特定种子必然生还或必然失败。");
    }

    private static void CalendarAndKnowledgeScope()
    {
        using var w=new ZooWorld(37,0,startMinute:59);
        var r=new ZooRules(w);
        r.ApplyPreset("standard");
        var red=w.RedStaff;
        red.Reparent(w.JellyfishGuestRoom); // 定位到待测设备，未布置成功/失败终态。
        var off=Offer(w,r,red,"switch_off",w.NightLight);
        var redLightRule=r.Clauses.Single(c=>c.Key=="Red.2");
        Check(redLightRule.Advice!(red,off)==0,"01:00 前提前触发强制关灯建议");
        using var mechanisms=new ZooAnomalies(w);
        w.Run(1);
        Check(redLightRule.Advice(red,off)>0,"01:00 后没有关灯建议");
        Check(w.NightLight.Get<bool>("lit"),"时钟越过守则期限直接替员工关灯");
        Check(Execute(w,Offer(w,r,red,"switch_off",w.NightLight)).Result?.Succeeded==true,"员工不能实际关灯");
        Check(!w.NightLight.Get<bool>("lit"),"关灯动作无效");
        var c=w.SpawnVisitor("校准者",w.ElephantArea);
        Check(Execute(w,Offer(w,r,c,"inspect",w.ElephantSign)).Result?.Succeeded==true,"标示牌无法阅读");
        Check(ZooWorld.Anchored(c,w.RabbitEaredElephant),"象类参照没有建立");
        Check(!ZooWorld.Anchored(c,w.FifthLion) && !ZooWorld.Anchored(c,w.WhaleProjection),"一块标示牌变成全物种免疫");
        c.Set("age",15d);
        w.RabbitPlush.Reparent(c);
        c.Set("toy_since",w.Session.Timeline.Now-43200);
        var drop=Offer(w,r,c,"drop",w.RabbitPlush);
        var toyRule=r.Clauses.Single(x=>x.Key=="Visitor.12");
        Check(toyRule.Advice!(c,drop)>0,"月期限没有参与建议");
        c.Set("age",12d);
        Check(toyRule.Advice(c,drop)==0,"儿童被错误套用青少年期限");
        var staff=w.BlueStaff;
        staff.Reparent(w.Walkway);
        Check(Execute(w,Offer(w,r,staff,"close",w.PrimateArea)).Result?.Succeeded==true,"封园操作失败");
        Check(ZooMinds.Offers(w,staff,r).Any(i=>i.Effect.Op=="open" && i.Target==w.PrimateArea),"规则期限错误地禁止了物理开门动作");
        w.Run(w.Session.Timeline.Now+11);
        Check(w.PrimateArea.Get<bool>("closed"),"十分钟规则变成自动开门机制");
        Check(Execute(w,Offer(w,r,staff,"open",w.PrimateArea)).Result?.Succeeded==true,"开门没有实际执行");
    }

    private static void RuleAblationExperiment()
    {
        // 测试抽取不明显的规则：
        // 案例 1：Visitor.4（不要饮用货架上的不明红色液体/兔血）
        // 案例 2：Visitor.5（不要独自一人进入兔区树荫灌木丛）
        {
            using var w = new ZooWorld(seed: 55, visitorCount: 0);
            var r = new ZooRules(w);
            r.ApplyPreset("standard");
            w.RabbitBlood.Reparent(w.DrinkShelf);
            w.RabbitBlood.Set("active", true);
            w.NormalDrinks.Set("count", 0d);

            var a = w.SpawnVisitor("完整规则游客", w.DrinkShop);
            var b = w.SpawnVisitor("抽取规则游客", w.DrinkShop);
            r.Learn(a, "Visitor");
            r.Learn(b, "Visitor");
            a.Set("thirst", 3.0);
            b.Set("thirst", 3.0);

            var beforeOffers = ZooMinds.Offers(w, b, r).Select(Shape).ToArray();
            var clause4 = r.Clauses.Single(c => c.Key == "Visitor.4");
            var bBelief4 = b.Mind!.Beliefs.First(bl => ReferenceEquals(bl.Get<Entity>("publication"), clause4.Publication));
            Soul.Stance(bBelief4, CognitiveStage.Rejected);
            var afterOffers = ZooMinds.Offers(w, b, r).Select(Shape).ToArray();
            Check(beforeOffers.SequenceEqual(afterOffers), "抽取规则改变了客观动作集");

            var consumeA = Offer(w, r, a, "consume", w.RabbitBlood);
            var consumeB = Offer(w, r, b, "consume", w.RabbitBlood);
            Check(r.Score(a, consumeA) < 0, "完整规则未能压制危险饮用动作");
            Check(r.Score(b, consumeB) == 0, "抽取规则后仍残留规避评分");

            using var mechanisms = new ZooAnomalies(w);
            ZooMinds.ActivateAll(w, r);
            w.Run(10);

            Check(b.Get<double>("thirst") < a.Get<double>("thirst"), "抽取规则游客未能饮用解渴");
            Check(b.Get<double>("erosion") <= 0.1, "摄入兔血本身产生了过量异常伤害");
            Console.WriteLine($"  [Visitor.4 兔血消融] 完整组守则拒饮(渴={a.Get<double>("thirst"):F1}, 侵蚀={a.Get<double>("erosion"):F2})，抽取组饮用解渴(渴={b.Get<double>("thirst"):F1})且纯物质侵蚀={b.Get<double>("erosion"):F2}");
        }

        {
            using var w = new ZooWorld(seed: 88, visitorCount: 0);
            var r = new ZooRules(w);
            r.ApplyPreset("standard");

            var a = w.SpawnVisitor("完整规则游客", w.RabbitArea);
            var b = w.SpawnVisitor("抽取规则游客", w.RabbitArea);
            r.Learn(a, "Visitor");
            r.Learn(b, "Visitor");

            var clause5 = r.Clauses.Single(c => c.Key == "Visitor.5");
            var bBelief5 = b.Mind!.Beliefs.First(bl => ReferenceEquals(bl.Get<Entity>("publication"), clause5.Publication));
            Soul.Stance(bBelief5, CognitiveStage.Rejected);

            var moveA = Offer(w, r, a, "move", w.RabbitBushes);
            var moveB = Offer(w, r, b, "move", w.RabbitBushes);
            Check(r.Score(a, moveA) < 0, "完整规则未能压制独自进入树荫灌木");
            Check(r.Score(b, moveB) == 0, "抽取规则后仍残留灌木规避评分");
            Console.WriteLine($"  [Visitor.5 灌木消融] 完整组进入灌木评分={r.Score(a, moveA):F1}，抽取组评分={r.Score(b, moveB):F1}");
        }

        // 3. 用户指定规则测试 1：Gate.1（海洋馆门口穿黑色制服）
        {
            using var w = new ZooWorld(seed: 99, visitorCount: 0);
            var r = new ZooRules(w);

            var a = w.SpawnVisitor("采信Gate完整游客", w.AquariumGate, hasMap: false);
            var b = w.SpawnVisitor("抽除Gate1游客", w.AquariumGate, hasMap: false);
            r.Learn(a, "Gate");
            r.Learn(b, "Gate");

            var clause1 = r.Clauses.Single(c => c.Key == "Gate.1");
            var bBelief1 = b.Mind!.Beliefs.First(bl => ReferenceEquals(bl.Get<Entity>("publication"), clause1.Publication));
            Soul.Stance(bBelief1, CognitiveStage.Rejected);

            var wearA = Offer(w, r, a, "wear", w.BlackUniform);
            var wearB = Offer(w, r, b, "wear", w.BlackUniform);
            Check(r.Score(a, wearA) > 0, "Gate.1 未能激励穿上黑色制服");
            Check(r.Score(b, wearB) == 0, "抽除 Gate.1 后仍残留穿衣偏好");

            using var mechanisms = new ZooAnomalies(w);
            ZooMinds.ActivateAll(w, r);
            w.Run(5);

            Check(a.Get<string>("uniform") == "黑色工作服", "完整组未穿上黑色工作服");
            Check(b.Get<string>("uniform") == "普通便服", "抽除组错误更换为黑色工作服");
            Check(a.Get<double>("uncertainty") > b.Get<double>("uncertainty"), "更换制服未导致自我认知动摇");
            Console.WriteLine($"  [Gate.1 换黑衣消融] 完整组制服={a.Get<string>("uniform")}, 认知动摇={a.Get<double>("uncertainty"):F2}；抽除组制服={b.Get<string>("uniform")}, 认知动摇={b.Get<double>("uncertainty"):F2}");
        }

        // 4. 用户指定规则测试 2：Gate.4（参观大象园区不要看标示牌）
        {
            using var w = new ZooWorld(seed: 105, visitorCount: 0);
            var r = new ZooRules(w);

            // 激活兔耳象异常，放置在大象区
            w.RabbitEaredElephant.Reparent(w.ElephantArea);
            w.RabbitEaredElephant.Set("active", true);

            var a = w.SpawnVisitor("采信Gate完整游客", w.ElephantArea);
            var b = w.SpawnVisitor("抽除Gate4游客", w.ElephantArea);
            r.Learn(a, "Gate");
            r.Learn(b, "Gate");

            var clause4 = r.Clauses.Single(c => c.Key == "Gate.4");
            var bBelief4 = b.Mind!.Beliefs.First(bl => ReferenceEquals(bl.Get<Entity>("publication"), clause4.Publication));
            Soul.Stance(bBelief4, CognitiveStage.Rejected);

            var inspectA = Offer(w, r, a, "inspect", w.ElephantSign);
            var inspectB = Offer(w, r, b, "inspect", w.ElephantSign);
            Check(r.Score(a, inspectA) < 0, "Gate.4 未能压制看标示牌动作");
            Check(r.Score(b, inspectB) == 0, "抽除 Gate.4 后仍残留看标示牌惩罚");

            // b 自主执行查看标示牌建立锚定，a 则因 Gate.4 拒绝查看标示牌
            Execute(w, inspectB);
            Check(ZooWorld.Anchored(b, w.RabbitEaredElephant), "查看标示牌后未建立象类锚定");
            Check(!ZooWorld.Anchored(a, w.RabbitEaredElephant), "未看标示牌意外获得锚定");

            using var mechanisms = new ZooAnomalies(w);
            var watchA = Offer(w, r, a, "watch", w.RabbitEaredElephant);
            var watchB = Offer(w, r, b, "watch", w.RabbitEaredElephant);
            Execute(w, watchA);
            Execute(w, watchB);

            Check(a.Get<double>("erosion") > b.Get<double>("erosion"), "缺乏锚定未造成更高认知危害侵蚀");
            Console.WriteLine($"  [Gate.4 禁看标示牌消融] 完整组(听信禁令无锚定)直视兔耳象侵蚀={a.Get<double>("erosion"):F2}；抽除组(正常看牌建立锚定)直视兔耳象侵蚀={b.Get<double>("erosion"):F2}");
        }

        // 5. 用户指定园长规则 1：Director.5（常备三张地图）
        {
            using var w = new ZooWorld(seed: 120, visitorCount: 0);
            var r = new ZooRules(w);
            r.ApplyPreset("standard");
            var d = w.Director;
            w.SpareMaps.Set("count", 1d); // 地图不足3张
            ZooMinds.Observe(w, d);

            var restock = Offer(w, r, d, "restock", w.SpareMaps);
            double scoreFull = r.Score(d, restock);
            Check(scoreFull > 0, "Director.5 未能激励补全三张地图");

            var clause5 = r.Clauses.Single(c => c.Key == "Director.5");
            var bBelief5 = d.Mind!.Beliefs.First(bl => ReferenceEquals(bl.Get<Entity>("publication"), clause5.Publication));
            Soul.Stance(bBelief5, CognitiveStage.Rejected);
            double scoreAblated = r.Score(d, restock);
            Check(scoreAblated == 0, "抽除 Director.5 后仍残留补图偏好");
            Console.WriteLine($"  [Director.5 地图常备消融] 完整组补图评分={scoreFull:F1}；抽除组评分={scoreAblated:F1}");
        }

        // 6. 用户指定园长规则 2：Director.2（门外孩子哭笑声保持沉默无视）
        {
            using var w = new ZooWorld(seed: 121, visitorCount: 0);
            var r = new ZooRules(w);
            r.ApplyPreset("standard");
            var d = w.Director;
            d.Set("heard_laugh_until", w.Session.Timeline.Now + 10); // 听到门外笑声

            var silence = Offer(w, r, d, "silence", w.DirectorOffice);
            var shout = Offer(w, r, d, "shout", w.DirectorOffice);
            double silenceFull = r.Score(d, silence);
            double shoutFull = r.Score(d, shout);
            Check(silenceFull > 0 && shoutFull < 0, "Director.2 未能正确引导沉默与压制大喊");

            var clause2 = r.Clauses.Single(c => c.Key == "Director.2");
            var bBelief2 = d.Mind!.Beliefs.First(bl => ReferenceEquals(bl.Get<Entity>("publication"), clause2.Publication));
            Soul.Stance(bBelief2, CognitiveStage.Rejected);
            double silenceAblated = r.Score(d, silence);
            double shoutAblated = r.Score(d, shout);
            Check(silenceAblated == 0 && shoutAblated == 0, "抽除 Director.2 后仍残留评分");
            Console.WriteLine($"  [Director.2 哭笑声消融] 完整组(沉默={silenceFull:F1}, 大喊={shoutFull:F1})；抽除组(沉默={silenceAblated:F1}, 大喊={shoutAblated:F1})");
        }

        // 7. 用户指定园长规则 3：Director.9（不深究海洋馆/讨论组织）
        {
            using var w = new ZooWorld(seed: 122, visitorCount: 0);
            var r = new ZooRules(w);
            r.ApplyPreset("standard");
            var d = w.Director;
            var staff = w.BlueStaff;
            staff.Reparent(w.DirectorOffice);

            var discuss = Offer(w, r, d, "discuss_organization", staff);
            double discussFull = r.Score(d, discuss);
            Check(discussFull < 0, "Director.9 未能压制深究讨论组织");

            var clause9 = r.Clauses.Single(c => c.Key == "Director.9");
            var bBelief9 = d.Mind!.Beliefs.First(bl => ReferenceEquals(bl.Get<Entity>("publication"), clause9.Publication));
            Soul.Stance(bBelief9, CognitiveStage.Rejected);
            double discussAblated = r.Score(d, discuss);
            Check(discussAblated == 0, "抽除 Director.9 后仍残留讨论抑制");
            Console.WriteLine($"  [Director.9 深究海洋馆消融] 完整组讨论组织评分={discussFull:F1}；抽除组评分={discussAblated:F1}");
        }
    }
}
