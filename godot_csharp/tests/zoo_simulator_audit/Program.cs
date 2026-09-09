using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Examples.ZooSimulator;

int passed=0,failed=0;
void Check(bool value,string text){if(!value)throw new Exception(text);}
void Run(string name,Action action){try{action();passed++;Console.WriteLine("通过："+name);}catch(Exception error){failed++;Console.Error.WriteLine("失败："+name+"\n"+error);}}
ZooWorld World(double time=480)=>new(7,0,time,"none");
void Quiet(ZooWorld w){foreach(var scheduled in w.Session.CTB.Pending.ToArray())w.Session.CTB.Cancel(scheduled);}
Person Guest(ZooWorld w,string room="主步道",Job job=Job.Visitor){var p=w.AddPerson(job,"审计人物",room);Quiet(w);w.Observe(p);return p;}
ActionExecution Execute(ZooWorld w,Person p,string action)
{
    var text=ZooMinds.ActionText(action,p.Soul.World,p.Self)??throw new Exception("没有脑内行动："+action);
    return p.Execution=w.Session.Submit(p.Soul,new(p.Self,new AstDecoder(p.Soul.World,Z.All,Z.Relations).DecodeEffect(text),0));
}
Run("九份原文共97条，全部受控文本可解析且没有缺失编号",()=>
{
    var rules=new ZooRules();Check(rules.Clauses.Count==97,"原文条目数错误");
    foreach(var c in rules.Clauses.Values)Check(ZooRules.Parse(c.Script).Length>0,c.Key);
    try{ZooRules.Parse("当 不存在的条件 时 优先 休息 权重 80");throw new Exception("应拒绝未知条件");}catch(FormatException){}
});
Run("决策隔离：未观察的现实变化不进入人物选择",()=>
{
    using var w=World();var p=Guest(w);
    var c=w.Rules.Publish("Custom.1","当 危险 时 优先 前往:狮子园区 权重 800");ZooRules.Read(p,c,w.Now);
    var before=ZooMinds.Decide(p)!.Effect;
    w.Rooms["主步道"].Set(Z.Danger,1);w.Runtime.Sync();
    Check(Ast.Equivalent(before,ZooMinds.Decide(p)!.Effect),"脑内决策偷读了现实");
    w.Observe(p);Check(!Ast.Equivalent(before,ZooMinds.Decide(p)!.Effect),"观察异常后规则没有改变选择");
});
Run("规则动态修改：旧记忆保留、广播更新、规则不改变实际场景",()=>
{
    using var w=World();var p=Guest(w);
    var c=w.Rules.Publish("Custom.1","当 总是 时 优先 前往:狮子园区 权重 800");ZooRules.Read(p,c,w.Now);
    var before=ZooMinds.Decide(p)!.Effect;var location=p.Room;
    w.Rules.Publish("Custom.1","当 总是 时 优先 前往:大象园区 权重 800");
    Check(Ast.Equivalent(before,ZooMinds.Decide(p)!.Effect),"发行直接改变了旧记忆");
    w.Broadcast("Custom.1");Check(!Ast.Equivalent(before,ZooMinds.Decide(p)!.Effect),"更新的文本未影响行为");
    Check(p.Room==location,"文书直接移动了人物");
    w.Rules.Remove("Custom.1");Check(p.Books.ContainsKey("Custom.1"),"撤回远程抹除了认知");
    w.ReadLocal(p);Check(!p.Books.ContainsKey("Custom.1"),"阅读撤回告示后仍未撤销");
});
Run("规则拒绝：同一AST按人物采信产生不同决策",()=>
{
    using var w=World();var p=Guest(w);
    var c=w.Rules.Publish("Custom.1","当 总是 时 优先 前往:狮子园区 权重 800");ZooRules.Read(p,c,w.Now);
    var accepted=ZooMinds.Decide(p)!.Effect;ZooRules.Accept(p,c.Key,Truth.False,w.Now);
    Check(!Ast.Equivalent(accepted,ZooMinds.Decide(p)!.Effect),"拒绝后规则仍驱动人物");
});
Run("移动在耗时结束后才结算，脑内过时通道实际会失败",()=>
{
    using var w=World();var p=Guest(w);
    var execution=Execute(w,p,"前往:狮子园区");w.Run(485);
    Check(p.Room==w.Rooms["主步道"],"移动提前完成");w.Run(486);
    Check(p.Room==w.Rooms["狮子园区"]&&execution.Status==ExecutionStatus.Completed,"六分钟移动失败");
    Quiet(w);w.Observe(p);var back=Execute(w,p,"前往:主步道");
    w.Views.Keys.First(e=>e.TrueName=="狮子园区 → 主步道").Get<LockProperty>()!.IsLocked=true;
    w.Run(492);Check(back.Status==ExecutionStatus.Failed&&p.Room==w.Rooms["狮子园区"],"过时脑内拓扑绕过客观锁门");
});
Run("完整地图不保护，撕纸转移不治疗，喂纸保护实际兔子",()=>
{
    using var w=World();var p=Guest(w,"兔子园区");
    p.Body.Set(Z.Rabbit,30,false);w.Anomalies.Reconsider(p,60);
    Check(w.Anomalies.Paper(p)==null,"完整地图生效");
    w.Anomalies.Perform(p,Deed.Tear);Check(w.Anomalies.Paper(p)!=null,"撕纸未生效");
    w.Anomalies.Perform(p,Deed.FeedPaper);Check(w.Anomalies.Paper(p)==null&&p.Body.Value(Z.Paper)==0,"喂纸者仍有保护");
    Check(w.Views.Keys.Any(e=>e.TrueName=="逃出的兔子"&&e.Value(Z.Paper)>0),"分类干扰未传递给兔子");
    Check(p.Body.Value(Z.Rabbit)==30&&p.Self.Value(Z.Human)==60,"纸片治疗了身体或认知");
});
Run("兔耳饰人类与真实兔子分开，后勤右路不是出口",()=>
{
    using var w=World();var p=Guest(w,"后勤区");w.Anomalies.Arrive(p,"猿猴园区");
    Check(p.Outcome=="在园","右路错误逃离");p.Body.Set(Z.RabbitEars,1);
    p.Body.Get<SpatialProperty>()!.Space=w.Rooms["大象园区"];w.Anomalies.Arrive(p,"主步道");
    Check(p.TrueSpecies==Species.Human&&w.Rooms["大象园区"].Value(Z.Danger)>0,"耳饰者被当作兔子或事件未触发");
});
Run("海洋馆白天可进入且不自动换黑衣，白天出口仍连园内",()=>
{
    using var w=World();var p=Guest(w,"海洋馆门口");var e=Execute(w,p,"前往:海洋馆");w.Run(482);
    Check(e.Status==ExecutionStatus.Completed&&p.Job==Job.Visitor&&p.Room==w.Rooms["海洋馆"],"入馆自动变身份");
    Quiet(w);w.Observe(p);w.Anomalies.Tick();p.Body.Set(new Tag("海馆离园资格"),1);
    Execute(w,p,"前往:园外");w.Run(485);
    Check(p.Room==w.Rooms["海洋馆门口"]&&p.Outcome=="在园","白天出口错误连外界");
});
Run("清晨窗口按行动完成时刻判断，黑衣只有稳定人类认知才通过",()=>
{
    using var w=World(360);var p=Guest(w,"海洋馆",Job.Black);w.Anomalies.Reconsider(p,90);w.Anomalies.Tick();w.Observe(p);
    var e=Execute(w,p,"前往:园外");w.Run(363);Check(e.Status==ExecutionStatus.Completed&&p.Outcome=="成功逃离","稳定黑衣无法逃离");
    using var late=World(388);var q=Guest(late,"海洋馆",Job.Black);late.Anomalies.Reconsider(q,90);late.Anomalies.Tick();late.Observe(q);
    var lateAction=Execute(late,q,"前往:园外");late.Session.CTB.Schedule(2.9,()=>late.Anomalies.Tick());late.Run(391);
    Check(lateAction.Status==ExecutionStatus.Failed&&q.Outcome=="在园","越过6:30仍然逃离");
});
Run("黑衣无法靠普通出口离园",()=>
{
    using var w=World();var p=Guest(w,"入口",Job.Black);w.Anomalies.Reconsider(p,95);
    var e=Execute(w,p,"前往:园外");w.Run(482);Check(e.Status==ExecutionStatus.Failed&&p.Room==w.Rooms["入口"],"黑衣绕过唯一出口");
});
Run("言灵经Core行动槽支付并延迟施放，到期恢复且不远程更新脑内场景",()=>
{
    using var w=World();var p=Guest(w);w.Anomalies.SayTemporary(p,Z.Calm,1,15);
    Check(p.Room!.Value(Z.Calm)==0&&p.Body.Value(Z.Mana)==3,"言灵未行动已生效");
    var decision=ZooMinds.Decide(p)!;p.Execution=w.Session.Submit(p.Soul,new(p.Self,decision.Effect,0));
    w.Run(481);Quiet(w); // 保存到期事件，下面重新构造有到期事件的独立场景。
    Check(p.Room!.Value(Z.Calm)==1&&p.Body.Value(Z.Mana)==2,"言灵未通过Core支付和结算");
    using var timed=World();var q=Guest(timed);timed.Anomalies.SayTemporary(q,Z.Calm,1,15);
    q.Execution=timed.Session.Submit(q.Soul,new(q.Self,ZooMinds.Decide(q)!.Effect,0));timed.Run(481);
    foreach(var scheduled in timed.Session.CTB.Pending.Where(s=>s.Due<496).ToArray())timed.Session.CTB.Cancel(scheduled);
    timed.Run(496);Check(timed.Rooms["主步道"].Value(Z.Calm)==0,"言灵未到期恢复");
});
Run("封猿区十分钟只是员工建议，时间不会自动开门",()=>
{
    using var w=World();var p=Guest(w,"猿猴园区",Job.Blue);w.Anomalies.Perform(p,Deed.CloseApes);w.Run(510);
    var door=w.Views.Keys.First(e=>e.TrueName=="主步道 → 猿猴园区");Check(door.Get<LockProperty>()!.IsLocked,"时间替员工开门");
    w.Anomalies.Perform(p,Deed.OpenApes);Check(!door.Get<LockProperty>()!.IsLocked,"实际开门动作未生效");
});
Run("开闭园边界：夜间不入园，8点恢复客流，16点停止",()=>
{
    using var early=new ZooWorld(7,12,470,"standard");Check(early.InPark==0,"开门前生成游客");early.Run(486);Check(early.InPark>0,"开放后没有游客");
    using var late=new ZooWorld(7,12,955,"standard");var initial=late.People.Count;late.Run(990);Check(late.People.Count==initial,"16点后仍入园");
});
Run("固定行动的实际后果不读取规则文书",()=>
{
    using var a=World();using var b=World();var p=Guest(a,"兔子园区");var q=Guest(b,"兔子园区");
    foreach(var text in a.Rules.Clauses.Values)ZooRules.Read(p,text,a.Now);
    foreach(var deed in new[]{Deed.Tear,Deed.FeedPaper,Deed.FeedRabbit}){a.Anomalies.Perform(p,deed);b.Anomalies.Perform(q,deed);}
    Check(p.Body.Value(Z.Rabbit)==q.Body.Value(Z.Rabbit)&&p.Body.Value(Z.Paper)==q.Body.Value(Z.Paper),"同样行动因规则知识而收到任意惩罚");
});
Run("撤回游客第8条实际解除喂食禁令，选择经Core执行并导致兔化",()=>
{
    using var w=World();var p=Guest(w,"兔子园区");
    ZooRules.Read(p,w.Rules.Clauses["Visitor.8"],w.Now);
    var safe=ZooMinds.Decide(p)!;
    Check(p.Assessments.Any(a=>a.Forbidden&&ZooWorld.Describe(a.Decision.Effect).StartsWith("喂兔子")),"未显示被禁的自然喂食候选");
    w.Withdraw("Visitor.8",true);
    var risky=ZooMinds.Decide(p)!;
    Check(!Ast.Equivalent(safe.Effect,risky.Effect)&&ZooWorld.Describe(risky.Effect).StartsWith("喂兔子"),"撤回后实际选择未改变");
    p.Execution=w.Session.Submit(p.Soul,new(p.Self,risky.Effect,0,Basis:risky.Basis));w.Run(482);Quiet(w);
    Check(p.Body.Value(Z.Rabbit)==30,"喂食行动未结算身体接触");
    // 隔离后只保留生理进展，证明确实有后续机制而非文书删除即惩罚。
    for(var minute=483;minute<=540&&p.Outcome=="在园";minute++){w.Run(minute);w.Anomalies.Expose(p);}
    Check(p.Outcome=="身体兔化",$"未持续发展：{p.Outcome} / {p.Body.Value(Z.Rabbit)}");
});
Run("自由脑内编辑和遗忘可继续决策，幻觉行动由Core判为缺失对象",()=>
{
    using var w=World();var p=Guest(w);
    var original=ZooMinds.Decide(p)!.Effect;
    var room=p.Self.Parent!;
    ZooRules.Read(p,w.Rules.Publish("Custom.2","当 危险 时 优先 前往:狮子园区 权重 800"),w.Now);
    ZooExperiment.Edit(p,room,room.Name,room.Kind,Truth.True,null,"危险征兆=1",w.Now);
    Check(!Ast.Equivalent(original,ZooMinds.Decide(p)!.Effect)&&p.Room!.Value(Z.Danger)==0,"脑内实验未隔离现实");
    w.Replan(p);Check(ZooWorld.Describe(p.Execution!.Intent.Changes).Contains("狮子园区"),"重选先观察抹除了实验认知");
    w.Session.Cancel(p.Execution);Quiet(w);
    foreach(var name in new[]{"手表","当前判断","日常计划","Custom.2"})ZooExperiment.Forget(p,p.Soul.World.Projections.First(v=>v.Name==name),w.Now);
    ZooMinds.Decide(p);w.Observe(p);Check(p.Watch!=null&&p.Soul.World.Contains(p.Watch),"未重新看到手表");
    var ghost=ZooExperiment.Edit(p,null,"幻想出口",ProjectionKind.Boundary,Truth.True,p.Self.Parent,"分钟=2",w.Now);
    ZooExperiment.SetBelief(p,ghost,"RULE true() => PREFER wait(2, travel(@\"幻想出口\"))",w.Now);
    w.Replan(p);var attempt=p.Execution!;w.Run(w.Now+2);
    Check(attempt.Status==ExecutionStatus.Failed&&attempt.Result!.Failure==ActionFailure.MissingObject,"幻觉通道错误进入现实");
});
Run("真实换衣诞生新黑衣，阴影按本地观察分类并可吞噬",()=>
{
    using var w=World();var p=Guest(w,"海洋馆门口");
    w.ReadLocal(p);w.Observe(p);
    for(var step=0;step<4&&p.Job!=Job.Black;step++)
    {
        var chosen=ZooMinds.Decide(p)!;
        p.Execution=w.Session.Submit(p.Soul,new(p.Self,chosen.Effect,0));w.Run(w.Now+2);Quiet(w);w.Observe(p);
    }
    Check(p.Job==Job.Black&&w.Anomalies.BlackBirths==1&&p.TrueSpecies==Species.Human,"没有新增黑衣人类");
    var q=Guest(w,"兔子园区");w.Anomalies.Reconsider(q,30);q.Body.Set(Z.Health,2);
    w.Run(w.Now+1);w.Anomalies.Expose(q);
    Check(q.Outcome=="被阴影吞噬"&&q.ItsClassification==Species.Goat,"近处阴影没有执行分类与伤害");
    Check(p.LastClassified<0,"远处人物被全知分类");
});
Run("连续两天开放入园，红衣本人下班后再通过脑内决策返岗",()=>
{
    using var w=new ZooWorld(7,2,480,"standard");var red=w.People.Single(p=>p.Body.TrueName=="红衣员工");
    w.Run(600);Check(red.Outcome=="正常离岗","红衣未正常下班");
    w.Run(1480);Check(red.Returns==1&&red.Outcome=="在园"&&ZooMinds.IsInside(red.Room?.TrueName),$"红衣未返岗：{red.Outcome} / {red.Room?.TrueName} / {red.CurrentAction}");
    var yesterday=w.People.Count;w.Run(1921);
    Check(w.DailyAdmissions.GetValueOrDefault(1)>=2&&w.People.Count>yesterday,"次日首批游客没有恢复");
    Check(w.People.Count(p=>p.Body.TrueName==red.Body.TrueName)==1,"复制新员工代替本人返岗");
    Check(w.Events.Any(e=>e.Contains("它抵达")),"它没有通过行动产生异常");
});
Run("低认知抵达狮区后恢复身份并回忆出口，不再来回折返",()=>
{
    using var w=World();var p=w.AddPerson(Job.Visitor,"恢复人物","狮子园区");w.Observe(p);
    foreach(var clause in w.Rules.Clauses.Values.Where(c=>c.Section=="Visitor"))ZooRules.Read(p,clause,w.Now);
    ZooExperiment.SetCognitionAndMap(p,30,w.Now);w.Anomalies.Tick();w.Observe(p);
    w.Replan(p);w.Run(540);
    Check(p.Self.Value(Z.Human)>=85,"没有完成身份恢复");
    Check(p.Outcome=="成功逃离","获救后未能离园："+p.Outcome+" / "+p.CurrentAction);
    Check(w.Travels.Count(t=>t.Person==p.Body.TrueName&&t.To=="狮子园区")<2,"仍然反复返回狮区");
});
Run("FORBID 穿透等待、组合动作和目标槽位，权重不能覆盖禁止",()=>
{
    using var w=World();var p=Guest(w);
    var target=p.Soul.World.Projections.First(v=>v.Name=="狮子园区");
    var source=Observation.Observe(p.Soul,new("禁令实验",ProjectionKind.Concept){Existence=Truth.True,Rules=[
        new(new Condition.Constant(Truth.True),RuleMode.Prefer,new Effect.Sequence([new Effect.Wait(9,new Effect.Act(ActionOp.Inspect,target))]),1000,Truth.True),
        new(new Condition.Constant(Truth.True),RuleMode.Forbid,new Effect.Act(ActionOp.Inspect,new Noun.Slot(RuleSlot.Target)),1,Truth.True)]});
    Check(DecisionSystem.Assess(p.Soul.World,p.Self).Single(a=>a.Decision.Basis==source).Forbidden,"组合动作绕过禁止");
});
Run("目的地禁止覆盖多段路线中的最后一扇门",()=>
{
    using var w=World();var p=Guest(w,"主步道");
    ZooRules.Read(p,w.Rules.Publish("Custom.Forbid","当 总是 时 禁止 前往:园外 权重 1"),w.Now);
    var decoder=ZooExperiment.Decoder(p);
    ZooExperiment.SetBelief(p,p.Self,"RULE true() => PREFER sequence(wait(4, travel(@\"主步道 → 入口\")), wait(2, travel(@\"入口 → 园外\")))",w.Now);
    ZooMinds.Decide(p);
    Check(p.Assessments.Any(a=>a.Forbidden&&a.Decision.Basis==p.Self),"多段路线绕过目的地禁止");
});
Run("四种语言的可调用词汇和全部原始规则可无损往返",()=>
{
    var words=ZooMinds.FactNames.Concat(ZooMinds.Actions.Keys).Concat(ZooWorld.RoomNames).Concat(Z.All.Select(t=>t.Name)).Concat(Z.Relations.Select(r=>r.Name));
    foreach(var language in Enum.GetValues<zoo_web.Lang>())
    {
        zoo_web.I18n.Current=language;
        foreach(var word in words)Check(zoo_web.Vocabulary.ToCanonical(zoo_web.Vocabulary.Display(word))==word,"词汇不能往返："+word+" / "+language);
        foreach(var clause in new ZooRules().Clauses.Values)
        {
            var restored=zoo_web.Vocabulary.ToCanonical(zoo_web.Vocabulary.Display(clause.Script));
            Check(restored==clause.Script,"规则不能往返："+clause.Key);ZooRules.Parse(restored);
        }
        var ast="RULE true() => forbid sequence(require(true()), wait(2, set($Actor, 人类信念, 10)))";
        Check(zoo_web.Vocabulary.ToCanonical(zoo_web.Vocabulary.Display(ast))==ast,"AST往返失败");
    }
});
Run("默认客流配置岗位冗余并覆盖主步道",()=>
{
    using var w=new ZooWorld(7,120);
    Check(w.People.Count(p=>p.Job is not (Job.Visitor or Job.Black))==19,"正式员工数量不匹配");
    Check(w.People.Count(p=>p.Room==w.Rooms["主步道"]&&p.Job is Job.Blue or Job.Guard)==4,"步道没有救援覆盖");
});
Run("Lost规则观察限制：精神良好不显示未读也不观察记录，动摇或异常后才可观察",()=>
{
    using var w=World();var p=Guest(w,"猿猴园区");
    Check(p.Self.Value(new Tag("未读文书"))==0,"清醒人物感知到了Lost纸条");
    w.ReadLocal(p);
    Check(!p.Books.Keys.Any(k=>k.StartsWith("Lost")),"清醒人物阅读记录了Lost规则");
    ZooRules.Read(p,w.Rules.Clauses["Lost.1"],w.Now,ObservationSource.Perception);
    Check(!p.Books.ContainsKey("Lost.1"),"直接感知违背精神限制被记录");

    w.Anomalies.Reconsider(p,70);
    w.Observe(p);
    Check(p.Self.Value(new Tag("未读文书"))==1,"精神动摇后未能发现未读纸条");
    w.ReadLocal(p);
    Check(p.Books.ContainsKey("Lost.1"),"精神动摇后未能读入纸条内容");
});
Run("Lost规则采信限制：初次观察为Unknown且不驱动行动，重度侵蚀才采信",()=>
{
    using var w=World();var p=Guest(w,"猿猴园区");
    w.Anomalies.Reconsider(p,70);
    w.ReadLocal(p);
    Check(p.Books["Lost.9"].Acceptance==Truth.Unknown,"初次观察Lost规则被直接采信");
    ZooMinds.Decide(p);
    Check(!p.Assessments.Any(a=>ZooWorld.Describe(a.Decision.Effect).Contains("认同山羊")),"未采信的Lost规则进入了行动候选");

    w.Anomalies.Reconsider(p,20);
    Check(p.Books["Lost.9"].Acceptance==Truth.True,"重度侵蚀未使Lost规则转化为采信");
    ZooMinds.Decide(p);
    Check(p.Assessments.Any(a=>ZooWorld.Describe(a.Decision.Effect).Contains("认同山羊")),"采信Lost规则后未产生认同山羊候选");
    Check(p.DecisionTrace.Any(t=>t.StartsWith("Lost.")),"采信Lost规则后未进入决策考量");

    w.Anomalies.Reconsider(p,90);
    ZooMinds.Decide(p);
    Check(!p.Assessments.Any(a=>ZooWorld.Describe(a.Decision.Effect).Contains("认同山羊")),"理智恢复后Lost规则仍在评估候选中");
    Check(!p.DecisionTrace.Any(t=>t.StartsWith("Lost.")),"理智恢复后Lost规则仍在Scope内");
});
Console.WriteLine($"审计：{passed}通过，{failed}失败");return failed==0?0:1;



