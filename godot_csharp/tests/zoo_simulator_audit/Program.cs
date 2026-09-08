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
Console.WriteLine($"审计：{passed}通过，{failed}失败");return failed==0?0:1;

