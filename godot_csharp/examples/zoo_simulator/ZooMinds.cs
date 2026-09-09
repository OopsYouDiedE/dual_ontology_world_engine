using System.Collections.Immutable;
using System.Globalization;
using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Examples.ZooSimulator;

/// <summary>此模块只读取脑内投影、记忆与观察历史；不接收 WorldRuntime 或客观场景。</summary>
public static class ZooMinds
{
    public static readonly Dictionary<string, Deed> Actions = new()
    {
        ["游览"]=Deed.Watch,["休息"]=Deed.Rest,["阅读"]=Deed.Read,["撕地图"]=Deed.Tear,["喂虚线纸"]=Deed.FeedPaper,
        ["喂兔子"]=Deed.FeedRabbit,["购买兔子血"]=Deed.BuyBlood,["吃山羊肉"]=Deed.EatMeat,["换黑衣"]=Deed.WearBlack,
        ["求助"]=Deed.Help,["坚定人类"]=Deed.Reaffirm,["无视"]=Deed.Ignore,["混入猿猴"]=Deed.MixApes,
        ["维修"]=Deed.Repair,["关灯"]=Deed.SwitchOff,["修剪"]=Deed.Trim,["引兔至狮区"]=Deed.Lure,
        ["泼兔子血"]=Deed.Splash,["丢弃纸片"]=Deed.Discard,["投纸信号"]=Deed.Signal,["查货"]=Deed.Inspect,
        ["巡逻"]=Deed.Patrol,["辞职"]=Deed.Resign,["隔离十三小时"]=Deed.Isolate,["抢夺纸片"]=Deed.StealPaper,
        ["驱离客房"]=Deed.Evict,["收存食品"]=Deed.Collect,["充电"]=Deed.Charge,["报告"]=Deed.Report,
        ["跟随"]=Deed.Follow,["买兔玩偶"]=Deed.BuyToy,["丢弃玩偶"]=Deed.DiscardToy,["护送"]=Deed.Escort,
        ["疏散"]=Deed.Evacuate,["封猿区"]=Deed.CloseApes,["值守"]=Deed.Patrol,["误认大象"]=Deed.Misidentify,
        ["认同山羊"]=Deed.GoatIdentity,["交出地图"]=Deed.GivePaper,["劝阻"]=Deed.Report,["收缴玩偶"]=Deed.CollectToy,
        ["交谈"]=Deed.Talk,["记录白狮"]=Deed.CountLions,["领取地图"]=Deed.GetMap,["清理"]=Deed.Clean,["开猿区"]=Deed.OpenApes,["认同大象"]=Deed.ElephantIdentity
    };
    public static readonly HashSet<string> FactNames = ["总是","出逃兔子","双路","象异常","独处","兔区","馆外","笑声","有地图","有纸","迷路","危险","获救","多余白狮","成年","玩偶过期","兔耳入象区","反复走散","查货到期","食品可见","修剪到期","黑衣","喂食到期","象区","有求助","有血","持续症状","无人","猿区","馆内","夜间","员工在场","白天","日间停留到期","夜间不足四小时","鲸区","客房","灯亮","疲倦","红衣","上班","下班","巡逻到期","周日","有离馆者","黑衣借宿","有兔玩偶","动摇","查狮到期","狮区","停电","办公室","水母死亡","隔离中","保安在岗","猿区已空","封闭到期"];
    public static string Quote(string name) => System.Text.Json.JsonSerializer.Serialize(name);
    public static string Ref(Projection p) => "@" + Quote(p.Name);
    public static string? Route(ProjectionWorld mind, Projection self, string destination, bool forbid = false)
    {
        var target = mind.Projections.FirstOrDefault(p => p.Kind == ProjectionKind.Space && p.Name == destination);
        var start = self.Parent;
        if (start is null || target is null || ReferenceEquals(start,target)) return null;
        if(self.Value(Z.Role)==(int)Job.Red&&start.Name=="海洋馆"&&destination=="园外"&&mind.Projections.FirstOrDefault(p=>p.Name=="海洋馆员工出口") is {} staffExit)
            return $"wait(3, sequence(require(all(tag_at_least($Actor, 岗位, 2), not(tag_at_least($Actor, 岗位, 3)))), travel({Ref(staffExit)})))";
        var queue = new Queue<(Projection Room, Projection? First)>();
        var seen = new HashSet<Projection> {start};
        queue.Enqueue((start,null));
        while(queue.TryDequeue(out var node))
        foreach(var door in mind.Projections.Where(p => p.Kind == ProjectionKind.Boundary && p.Existence == Truth.True && p.Value(Tags.Locked) == 0 && (p.Value(new Tag("员工专用"))==0 || self.Value(Z.Role)==(int)Job.Red) && p.Relations.Contains((Z.From,node.Room))))
        {
            var next = door.Relations.FirstOrDefault(r => r.Relation == Z.Leads).Target;
            if(next is null || !seen.Add(next)) continue;
            var first = node.First ?? door;
            if(ReferenceEquals(next,target))
            {
                if(forbid && !ReferenceEquals(first,door)) return null;
                var duration = first.Value(Z.Minutes);
                                var gate=first.Name=="入口 → 园外" ? "普通离园资格" : first.Name=="海洋馆 → 园外" ? "海馆离园资格" : null;
                var travel=first.Name=="海洋馆员工出口"?$"sequence(require(all(tag_at_least($Actor, 岗位, 2), not(tag_at_least($Actor, 岗位, 3)))), travel({Ref(first)}))":gate is null?$"travel({Ref(first)})":$"sequence(require(tag_at_least($Actor, {gate}, 1)), travel({Ref(first)}))";
                return $"wait({Math.Max(1,duration)}, {travel})";
            }
            queue.Enqueue((next,first));
        }
        return null;
    }
    private static string? FullRoute(ProjectionWorld mind,Projection self,string destination)
    {
        var target=mind.Projections.FirstOrDefault(p=>p.Name==destination&&p.Kind==ProjectionKind.Space);
        if(target is null||self.Parent is null||target==self.Parent)return null;
        var queue=new Queue<(Projection Room,List<Projection> Doors)>();queue.Enqueue((self.Parent,[]));
        var seen=new HashSet<Projection>{self.Parent};
        while(queue.TryDequeue(out var node))
        foreach(var door in mind.Projections.Where(p=>p.Kind==ProjectionKind.Boundary&&p.Value(Tags.Locked)==0&&p.Relations.Contains((Z.From,node.Room))))
        {
            var next=door.Relations.FirstOrDefault(r=>r.Relation==Z.Leads).Target;
            if(next is null||!seen.Add(next))continue;
            List<Projection> path=[..node.Doors,door];
            if(next==target)return "sequence("+string.Join(", ",path.Select(d=>$"wait({Math.Max(1,d.Value(Z.Minutes))}, travel({Ref(d)}))"))+")";
            queue.Enqueue((next,path));
        }
        return null;
    }
    public static string? ActionText(string action, ProjectionWorld mind, Projection self, bool forbid = false)
    {
        if(action.StartsWith("前往:")) return Route(mind,self,action[3..],forbid);
        var deed = Actions[action];
        string? goal = deed switch
        {
            Deed.BuyBlood or Deed.Inspect => "饮料店", Deed.FeedPaper or Deed.FeedRabbit or Deed.Trim or Deed.MixApes => deed == Deed.MixApes ? "猿猴混处区" : "兔子园区",
            Deed.WearBlack => "海洋馆门口", Deed.SwitchOff => "客房", Deed.Repair => "园长办公室",
            Deed.Splash or Deed.CountLions => "狮子园区", Deed.Signal => "大象园区", Deed.Isolate => "园长办公室", _ => null
        };
        if(goal is not null && self.Parent?.Name != goal) return forbid ? null : Route(mind,self,goal);
        if(deed == Deed.Tear && (self.Value(Z.Map)==0 || self.Value(Z.Paper)>0)) return null;
        if(deed is Deed.FeedPaper or Deed.Discard or Deed.GivePaper or Deed.Signal && self.Value(Z.Paper)==0) return null;
        if(deed == Deed.WearBlack && self.Value(Z.Role)==(int)Job.Black) return null;
        if(deed == Deed.Splash && self.Value(Z.Blood)==0) return null;
        if(deed == Deed.BuyBlood && self.Parent?.Value(Z.Food)==0) return null;
        if(deed == Deed.GetMap && self.Value(Z.Map)>0) return null;
        if(deed == Deed.BuyToy && self.Value(Z.Toy)>0) return null;
        if(deed == Deed.SwitchOff && self.Parent?.Value(Z.Lamp)==0) return null;
        if(deed == Deed.Help && !mind.Projections.Any(p=> p.Kind==ProjectionKind.Body && p!=self && p.Parent==self.Parent && p.Value(Z.Role)!=(int)Job.Visitor)) return null;
        if(deed is Deed.Help or Deed.Escort or Deed.Evacuate or Deed.Talk or Deed.Report or Deed.Evict)
        {
            var peer=mind.Projections.FirstOrDefault(p=>p.Kind==ProjectionKind.Body&&p!=self&&p.Parent==self.Parent&&p.Existence==Truth.True&&
                (deed==Deed.Help?p.Value(Z.Role) is (int)Job.Blue or (int)Job.Guard:deed==Deed.Evict?p.Value(Z.Role)==(int)Job.Black:p.Value(Z.Role)==(int)Job.Visitor));
            if(peer is null)return null;
            var destination=deed==Deed.Evacuate?"主步道":deed==Deed.Evict?"海洋馆":"狮子园区";
            var route=FullRoute(mind,self,destination);
            if(route is null)return null;
            var request=deed==Deed.Help?$"sequence(say({Ref(self)}, \"请跟我前往狮子园区\", {route}), {route})":route;
            return $"wait(2, say({Ref(peer)}, \"请协助前往{destination}\", {request}))";
        }
        var duration = deed switch { Deed.Watch=>18, Deed.Rest=>20, Deed.Read=>4, Deed.Repair=>8, Deed.Trim=>20, Deed.Isolate=>15, _=>2 };
        if(deed == Deed.Reaffirm)
        {
            var human = mind.Projections.FirstOrDefault(p=>p.Name=="人类");
            if(human is null) return null;
            return $"wait(2, sequence(infer(related($Actor, IsA, {Ref(human)})), set($Actor, 动作, {(int)deed+1})))";
        }
        return $"wait({duration}, set($Actor, 动作, {(int)deed+1}))";
    }

    public static Decision? Decide(Person person)
    {
        var mind = person.Soul.World;
        var self = person.Self;
        var clock = person.Watch is {} watch && mind.Contains(watch) ? watch.Value(Z.Clock) : 0;
        var facts = Concept(person, "当前判断");
        var factsValues = Facts(mind,self,clock);
        Observation.Observe(person.Soul,new(facts.Name,facts.Kind) {Target=facts,Source=ObservationSource.Inference,Time=clock,
            Tags=factsValues.ToImmutableDictionary(p=>new Tag(p.Key),p=>(int?)(p.Value?1:0)),Details="由已观察的身体、场景、手表与记忆形成判断"});
        var decoder = new AstDecoder(mind,[..Z.All,..FactNames.Select(n=>new Tag(n))],Z.Relations);
        person.DecisionTrace.Clear();
        foreach(var book in person.Books.Values.ToArray())
        {
            var compiled = ImmutableArray.CreateBuilder<Rule>();
            foreach(var instruction in ZooRules.Parse(book.Text.Script))
            {
                var action = ActionText(instruction.Action,mind,self,instruction.Mode==RuleMode.Forbid);
                if(action is null) continue;
                var conditions = instruction.Condition.Split('&').Select(term=>term.StartsWith('!')
                    ? $"not(tag_at_least({Ref(facts)}, {term[1..]}, 1))" : $"tag_at_least({Ref(facts)}, {term}, 1)");
                // 空间作用域也是条件树的一部分，馆内不沿用馆外告示。
                var scope = Scope(book.Text.Section,self);
                var text = $"RULE all({(scope?"true()":"false()")}, {string.Join(", ",conditions)}) => {instruction.Mode} {action}";
                var rules = decoder.DecodeRules(text).Select(r=>r with {Acceptance=book.Acceptance,Weight=Math.Max(1,instruction.Weight*book.Understanding/100)}).ToArray();
                compiled.AddRange(rules);
                if(rules.Any(r=>r.Acceptance==Truth.True && Ast.Evaluate(r.Condition,new(self,Scene:self.Parent))==Truth.True))
                    person.DecisionTrace.Add($"{book.Text.Key} → {instruction.Condition} → {instruction.Mode} {instruction.Action} ({rules[0].Weight})");
            }
            Observation.Observe(person.Soul,new(book.Projection.Name,book.Projection.Kind) {Target=book.Projection,Source=ObservationSource.Inference,Time=clock,Rules=compiled.ToImmutable()});
        }
        var habits = Concept(person, "日常计划");
        var everyday = ImmutableArray.CreateBuilder<Rule>();
        void Want(string action,int weight)
        {
            if(ActionText(action,mind,self) is { } text)
                everyday.AddRange(decoder.DecodeRules($"RULE true() => PREFER {text}").Select(r=>r with {Acceptance=Truth.True,Weight=weight}));
        }
        var room = self.Parent?.Name ?? "";
        var role=(Job)self.Value(Z.Role);
        var elapsed = self.Value(new Tag("入园时长"));
        if(role==Job.Visitor)
        {
            var history = mind.History.Where(h=>ReferenceEquals(h.Target,self)&&h.Details.StartsWith("游览完成:")).ToArray();
            var itinerary=new[]{"兔子园区","猿猴园区","大象园区","狮子园区","饮料店"};
            var offset=self.Value(Z.Age)%itinerary.Length;
            var goal=Enumerable.Range(0,itinerary.Length).Select(i=>itinerary[(i+offset)%itinerary.Length])
                .OrderBy(g=>history.Count(h=>h.Details=="游览完成:"+g)).First();
            Want(room==goal ? "游览" : "前往:"+goal,45);
            if(elapsed>140 || clock%1440>=900) Want("前往:园外",elapsed>240?160:75);
            bool Done(Deed deed)=>mind.History.Any(h=>h.Target==self&&h.Details==$"交互完成:{deed}:{room}");
            if(room=="兔子园区"&&!Done(Deed.FeedRabbit))Want("喂兔子",60+self.Value(Z.Age)%12);
            if(room=="饮料店"&&!Done(Deed.BuyBlood)&&self.Parent!.Value(Z.Food)>0)Want("购买兔子血",60);
            if(self.Value(Z.Toy)==0)Want("买兔玩偶",self.Value(Z.Age)<18?50:15);
            if(self.Value(Z.Human)<85)Want("前往:海洋馆门口",65);
        }
        else if(role==Job.Black)
        {
            Want("前往:海洋馆",60);
            if(factsValues["馆内"] && clock%1440>=360 && clock%1440<390) Want("前往:园外",220);
            if(self.Value(Z.Human)<40) Want("抢夺纸片",85);
            Want("休息",factsValues["馆内"]?65:5);
        }
        else
        {
            Want("巡逻",factsValues["巡逻到期"]?26:1);
            if(role==Job.Red){if(room=="园外")Want("前往:海洋馆",250);else if(factsValues["上班"]){Want("前往:"+(room=="客房"?"鲸鱼区":room=="鲸鱼区"?"水母区":"客房"),65);}else Want("前往:海洋馆",60);}
            if(role is Job.Blue or Job.Guard && clock%1440 is >=60 and <360) Want("前往:内部宿舍",35);
            if(role==Job.Director) Want("前往:园长办公室",35);
            if(factsValues["停电"] && role!=Job.Director) Want("维修",170);
        }
        Want("休息",8); Want("阅读",self.Value(new Tag("未读文书"))>0?120:1);
        if(self.Value(Z.Health)<80) Want("求助",40);
        Observation.Observe(person.Soul,new(habits.Name,habits.Kind) {Target=habits,Source=ObservationSource.Inference,Time=clock,Rules=everyday.ToImmutable()});
        person.Assessments=DecisionSystem.Assess(mind,self);
        var selected=person.Assessments.FirstOrDefault(a=>!a.Forbidden)?.Decision;
        var knowinglyDisregarded=selected is not null && mind.Projections.SelectMany(p=>p.Rules).Any(r=>r.Acceptance==Truth.True&&r.Mode==RuleMode.Require&&
            Ast.Evaluate(r.Condition,new(self,Scene:self.Parent))==Truth.True&&!Ast.Equivalent(Ast.Bind(r.Effect,new(self,Scene:self.Parent)),selected.Effect));
        Observation.Observe(person.Soul,new(self.Name,self.Kind){Target=self,Source=ObservationSource.Introspection,Time=clock,
            Tags=ImmutableDictionary<Tag,int?>.Empty.Add(new Tag("明知违背"),knowinglyDisregarded?1:0),Details=knowinglyDisregarded?"意识到自己正在违背已知要求":"已完成行动抉择"});
        return selected;
    }
    private static Projection Concept(Person p,string name)=>p.Soul.World.Projections.FirstOrDefault(v=>v.Name==name)??
        Observation.Observe(p.Soul,new(name,ProjectionKind.Concept){Source=ObservationSource.Inference,Existence=Truth.True,Details="重新形成行动判断"});
    private static bool Scope(string section,Projection self)
    {
        var inside = IsInside(self.Parent?.Name);
        var role=(Job)self.Value(Z.Role);
        return section switch {"Gate"=>!inside, "Inside"=>inside,"Visitor"=>role==Job.Visitor&&!inside,
            "Staff"=>role is Job.Blue or Job.Guard or Job.Black,"Red"=>role==Job.Red,"Director"=>role==Job.Director,_=>true};
    }
    public static bool IsInside(string? name)=>name is "海洋馆" or "水母区" or "鲸鱼区" or "客房";
    private static Dictionary<string,bool> Facts(ProjectionWorld mind,Projection self,int time)
    {
        var room=self.Parent; var name=room?.Name; var hour=time%1440;
        var peers=mind.Projections.Where(p=>p.Kind==ProjectionKind.Body&&p!=self&&p.Parent==room&&p.Existence==Truth.True).ToArray();
        var inside=IsInside(name); var black=self.Value(Z.Role)==(int)Job.Black;
        int V(string n)=>self.Value(new Tag(n));
        bool Local(Tag t)=>room?.Value(t)>0;
        var result=FactNames.ToDictionary(n=>n,_=>false);
        void Set(string n,bool v)=>result[n]=v;
        Set("总是",true); Set("出逃兔子",Local(new Tag("出逃兔子")));Set("双路",Local(Z.Split));Set("象异常",Local(Z.Anomaly));
        Set("独处",peers.Length==0);Set("无人",peers.Length==0);Set("兔区",name=="兔子园区");Set("馆外",name=="海洋馆门口");
        Set("笑声",Local(Z.Laugh));Set("有地图",self.Value(Z.Map)>0);Set("有纸",self.Value(Z.Paper)>0);
        Set("迷路",V("迷路")>0);Set("危险",Local(Z.Danger)||self.Value(Z.Symptoms)>0||self.Value(Z.Human)<65);
        Set("获救",V("获救")>0);Set("多余白狮",room?.Value(Z.LionCount)>4);Set("成年",self.Value(Z.Age)>=18);
        Set("玩偶过期",self.Value(Z.Age)>12&&self.Value(Z.Toy)>0&&V("玩偶持有分钟")>=43200);
        Set("兔耳入象区",name=="大象园区"&&peers.Any(p=>p.Value(Z.RabbitEars)>0));Set("反复走散",V("走散次数")>1);
        Set("查货到期",V("查货到期")>0);Set("食品可见",Local(Z.Food));Set("修剪到期",V("修剪到期")>0);
        Set("黑衣",black);Set("喂食到期",V("喂食到期")>0);Set("象区",name=="大象园区");
        Set("有求助",mind.Projections.Any(p=>p.Kind==ProjectionKind.Event&&p.Procedure!=null&&p.Value(Tags.Responded)==0));
        Set("有血",self.Value(Z.Blood)>0);Set("持续症状",self.Value(Z.Symptoms)>5);Set("猿区",name is "猿猴园区" or "猿猴混处区");
        Set("馆内",inside);Set("夜间",hour<480||hour>=960);Set("白天",hour>=480&&hour<960);
        Set("员工在场",peers.Any(p=>p.Value(Z.Role)!=(int)Job.Visitor));Set("日间停留到期",V("白天入馆")>0&&V("馆内分钟")>=20);
        Set("夜间不足四小时",V("白天入馆")==0&&V("馆内分钟")<240&&hour>=390);
        Set("鲸区",name=="鲸鱼区");Set("客房",name=="客房");Set("灯亮",Local(Z.Lamp));Set("疲倦",V("疲倦")>60);
        Set("红衣",self.Value(Z.Role)==(int)Job.Red);Set("上班",hour>=30&&hour<360);Set("下班",hour>=360);
        Set("巡逻到期",V("巡逻到期")>0);Set("周日",time/1440%7==6);Set("有离馆者",peers.Any(p=>p.Value(Z.Role)==(int)Job.Visitor));
        Set("黑衣借宿",name=="客房"&&peers.Any(p=>p.Value(Z.Role)==(int)Job.Black));Set("有兔玩偶",peers.Any(p=>p.Value(Z.Toy)>0));
        Set("动摇",self.Value(Z.Human)<85);Set("查狮到期",V("查狮到期")>0);Set("狮区",name=="狮子园区");
        Set("停电",mind.Projections.FirstOrDefault(p=>p.Name=="园长办公室")?.Tags.TryGetValue(Z.Power,out var power)==true && power==0);
        Set("办公室",name=="园长办公室");Set("水母死亡",Local(new Tag("水母死亡")));Set("隔离中",V("隔离中")>0);
        Set("保安在岗",hour>=480||hour<60);
        var apes=mind.Projections.FirstOrDefault(p=>p.Name=="猿猴园区");
        Set("猿区已空",apes?.Tags.TryGetValue(new Tag("人数"),out var count)==true&&count==0);
        Set("封闭到期",apes?.Value(new Tag("封闭分钟"))>=10);
        return result;
    }
}






