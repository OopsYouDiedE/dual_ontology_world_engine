using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Examples.ZooSimulator;

public sealed class ZooWorld : IDisposable
{
    public static readonly string[] RoomNames = ["园外","入口","主步道","兔子园区","猿猴园区","猿猴混处区","左街","后勤区","大象园区","狮子园区","饮料店","海洋馆门口","海洋馆","水母区","鲸鱼区","客房","内部宿舍","园长办公室","安保室"];
    public WorldRuntime Runtime { get; } = new();
    public SimulationSession Session { get; }
    public ZooRules Rules { get; } = new();
    public ZooAnomalies Anomalies { get; }
    public Dictionary<string,Essence> Rooms { get; } = [];
    public Dictionary<Essence,Projection> Views { get; } = [];
    public List<Person> People { get; } = [];
    public List<string> Events { get; } = [];
    public List<(double Time,string Person,string From,string To,double Minutes)> Travels { get; } = [];
    private readonly Dictionary<Projection,Person> actors = [];
    private readonly Random random;
    private readonly int population;
    private readonly string preset;
    private int visitorSequence;
    private int lastOpeningDay = -1;
    public Dictionary<int,int> DailyAdmissions { get; } = [];
    private bool disposed;
    public double Now => Session.CTB.Now;
    public int InPark => People.Count(p=>p.Job==Job.Visitor&&p.Outcome=="在园");
    public int PeakVisitors { get; private set; }
    public bool Open => Now%1440>=480&&Now%1440<960;
    public ZooWorld(int seed=7,int visitors=120,double startMinute=480,string preset="standard")
    {
        if(visitors is <0 or >500 || !double.IsFinite(startMinute)||startMinute<0) throw new ArgumentOutOfRangeException(nameof(visitors));
        if(preset is not ("standard" or "none" or "gate" or "all")) throw new ArgumentException("预设应为 standard / none / gate / all。");
        random=new(seed); population=visitors; this.preset=preset;
        Session=new(Runtime);Session.CTB.RunUntil(startMinute);
        foreach(var name in RoomNames)
        {
            var room=new Essence(name,ProjectionKind.Space);Rooms.Add(name,room);Views.Add(room,Runtime.Project(room,name));
            room.Set(Z.Light,1);room.Set(Z.Shadow,name is "兔子园区" or "主步道" or "大象园区" or "猿猴园区"?1:0);room.Set(Z.Calm,0);room.Set(Z.Laugh,0);
            room.Set(Z.Danger,0);room.Set(Z.Food,0);room.Set(Z.Split,0);room.Set(Z.Anomaly,0);
        }
        Rooms["园长办公室"].Set(Z.Power,1);Rooms["客房"].Set(Z.Lamp,1);Rooms["狮子园区"].Set(Z.LionCount,4);
        Connect("园外","入口",120);Connect("入口","主步道",240);Connect("主步道","兔子园区",360);
        Connect("主步道","猿猴园区",420);Connect("猿猴园区","左街",180);Connect("左街","主步道",300);
        Connect("猿猴园区","猿猴混处区",120);Connect("猿猴园区","后勤区",240);
        Connect("主步道","大象园区",480);Connect("主步道","狮子园区",360);Connect("主步道","饮料店",120);
        Connect("饮料店","狮子园区",300);Connect("主步道","海洋馆门口",360);Connect("海洋馆门口","海洋馆",120);
        Connect("海洋馆","水母区",180);Connect("水母区","客房",60);Connect("海洋馆","鲸鱼区",240);
        Connect("海洋馆","园外",180);Connect("后勤区","内部宿舍",240);Connect("后勤区","园长办公室",180);
        Connect("园长办公室","主步道",300);Connect("大象园区","安保室",120);Connect("内部宿舍","狮子园区",240);
        var staffExit=new Essence("海洋馆员工出口",ProjectionKind.Boundary);
        staffExit.Add(new SpatialProperty {Space=Rooms["海洋馆"]});staffExit.Add(new PassageProperty()).Connections.Add(Rooms["海洋馆"],Rooms["园外"]);
        staffExit.Add(new LockProperty {IsLocked=false});staffExit.Set(Z.Minutes,3);staffExit.Set(Z.Distance,180);staffExit.Set(new Tag("员工专用"),1);
        var staffView=Runtime.Project(staffExit,staffExit.TrueName);Views.Add(staffExit,staffView);
        staffExit.Add(new SurfaceProperty()).Relations[staffView]=[(Z.From,Views[Rooms["海洋馆"]]),(Z.Leads,Views[Rooms["园外"]])];
        var staffEntry=new Essence("海洋馆员工入口",ProjectionKind.Boundary);
        staffEntry.Add(new SpatialProperty {Space=Rooms["园外"]});staffEntry.Add(new PassageProperty()).Connections.Add(Rooms["园外"],Rooms["海洋馆"]);
        staffEntry.Add(new LockProperty {IsLocked=false});staffEntry.Set(Z.Minutes,3);staffEntry.Set(Z.Distance,180);staffEntry.Set(new Tag("员工专用"),1);
        var entryView=Runtime.Project(staffEntry,staffEntry.TrueName);Views.Add(staffEntry,entryView);
        staffEntry.Add(new SurfaceProperty()).Relations[entryView]=[(Z.From,Views[Rooms["园外"]]),(Z.Leads,Views[Rooms["海洋馆"]])];
        Anomalies=new(this,random);
        Session.Completed+=Completed;
        AddPerson(Job.Blue,"蓝衣售货员","饮料店");AddPerson(Job.Blue,"夜间值守员","园长办公室");
        AddPerson(Job.Blue,"蓝衣救援员","狮子园区");AddPerson(Job.Blue,"蓝衣饲养员","兔子园区");
        AddPerson(Job.Guard,"大象保安","安保室");AddPerson(Job.Director,"园长","园长办公室");
        AddPerson(Job.Red,"红衣员工","水母区");AddPerson(Job.Black,"黑衣人类","海洋馆门口");
        // 首批客流同时决定岗位冗余；黑衣不计入正式救援编制。
        var teams=(int)Math.Ceiling(visitors/60d);
        for(var team=1;team<=teams;team++)
        {
            AddPerson(Job.Blue,$"入口服务员{team}","入口");
            AddPerson(Job.Blue,$"步道救援员{team}","主步道");
            AddPerson(Job.Blue,$"猿区服务员{team}","猿猴园区");
            AddPerson(Job.Blue,$"象区服务员{team}","大象园区");
            AddPerson(Job.Guard,$"步道保安{team}","主步道");
            AddPerson(Job.Red,$"海馆服务员{team}","海洋馆");
        }
        if(Open){Admit(visitors);lastOpeningDay=(int)(Now/1440);}
        Session.CTB.Schedule(1,Tick);Session.CTB.Schedule(15,ArrivalTick);
    }
    private void Connect(string a,string b,int meters)
    {
        Door(a,b,meters);Door(b,a,meters);
    }
    private void Door(string a,string b,int meters)
    {
        var e=new Essence(a+" → "+b,ProjectionKind.Boundary);
        e.Add(new PassageProperty()).Connections.Add(Rooms[a],Rooms[b]);
        e.Add(new SpatialProperty {Space=Rooms[a]});
        e.Add(new LockProperty {IsLocked=false});
        e.Set(Z.Distance,meters);e.Set(Z.Minutes,(int)Math.Ceiling(meters/60d));
        var view=Runtime.Project(e,e.TrueName,Views[Rooms[a]]);Views.Add(e,view);
        e.Add(new SurfaceProperty()).Relations[view]=[(Z.From,Views[Rooms[a]]),(Z.Leads,Views[Rooms[b]])];
        Runtime.Sync();
    }
    public Person AddPerson(Job job,string name,string room)
    {
        var body=new Essence(name,ProjectionKind.Body);body.Add(new SpatialProperty {Space=Rooms[room]});
        body.Set(Z.Health,100);body.Set(Z.Rabbit,0,false);body.Set(Z.Attention,0,false);body.Set(Z.Role,(int)job);
        body.Set(Z.Map,1);body.Set(Z.Paper,0);body.Set(Z.Blood,0);body.Set(Z.Symptoms,0);body.Set(Z.Mana,3);
        body.Set(Z.Age,job==Job.Visitor?random.Next(6,76):35);body.Set(Z.Confidence,random.Next(55,101));
        body.Set(Z.RabbitEars,job==Job.Visitor&&random.Next(25)==0?1:0);body.Set(Z.Toy,0);
        body.Set(new Tag("普通离园资格"),job!=Job.Black?1:0);body.Set(new Tag("海馆离园资格"),job==Job.Red?1:0);body.Set(Z.Command,0,false);body.Set(new Tag("获救"),0);body.Set(new Tag("迷路"),0);
        var view=Runtime.Project(body,name);Views.Add(body,view);
        var soul=new SoulEssence(name+"的心智");soul.Relations.Add((Relations.Controls,body));Runtime.Project(soul,soul.TrueName);
        // 地图只提供拓扑和标注路长，不透露现场状态。海洋馆地图在入馆后观察。
        var map=Views.Where(p=>p.Key.Kind is ProjectionKind.Space or ProjectionKind.Boundary)
            .Where(p=>!ZooMinds.IsInside(p.Key.TrueName)&& !p.Key.TrueName.Contains("海洋馆")&&!p.Key.TrueName.Contains("水母")&&!p.Key.TrueName.Contains("鲸鱼")&&!p.Key.TrueName.Contains("客房"))
            .Select(p=>p.Value).ToArray();
        Runtime.Observe(soul,map,Now,new MapOnly());
        var local=Runtime.Observe(soul,[Views[Rooms[room]],view],Now);
        var self=local[1];
        Observation.Observe(soul,new(self.Name,self.Kind){Target=self,Source=ObservationSource.Introspection,Time=Now,
            Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Z.Human,job==Job.Black?65:100).Add(Tags.Cooperation,50),
            AddRelations=(job is Job.Blue or Job.Guard) ? [(new RelationTag("岗位地点"),local[0])] : []});
        Observation.Observe(soul,new("人类",ProjectionKind.Concept){Source=ObservationSource.Recall,Existence=Truth.True,Time=Now});
        Observation.Observe(soul,new("手表",ProjectionKind.Object){Source=ObservationSource.Perception,Existence=Truth.True,Time=Now});
        Observation.Observe(soul,new("当前判断",ProjectionKind.Concept){Source=ObservationSource.Inference,Existence=Truth.True,Time=Now});
        Observation.Observe(soul,new("日常计划",ProjectionKind.Concept){Source=ObservationSource.Recall,Existence=Truth.True,Time=Now});
        var p=new Person(body,soul,self,job,Now){Watch=soul.World.Projections.First(v=>v.Name=="手表")};People.Add(p);actors.Add(self,p);Anomalies.GiveMap(p);
        var sections=preset switch {"none"=>Array.Empty<string>(),"gate"=>new[]{"Gate"},"all"=>new[]{"Visitor","Staff","Gate","Lost","Inside","Red","Guard","Director","Survivor"},
            _=>job switch {Job.Visitor=>new[]{"Visitor"},Job.Red=>new[]{"Red","Inside"},Job.Black=>new[]{"Gate","Survivor"},Job.Guard=>new[]{"Staff","Guard"},Job.Director=>new[]{"Director","Guard"},_=>new[]{"Staff"}}};
        foreach(var text in Rules.Clauses.Values.Where(c=>sections.Contains(c.Section)))ZooRules.Read(p,text,Now,ObservationSource.Recall);
        Session.CTB.Schedule(random.NextDouble()*4,()=>Think(p));
        PeakVisitors=Math.Max(PeakVisitors,InPark);
        return p;
    }
    private sealed class MapOnly:IProjectionTransform
    {
        public ObservationInput? Transform(ObservationInput input)=>input with {Source=ObservationSource.Recall,
            Tags=input.Tags.Where(p=>p.Key==Z.Minutes||p.Key==Z.Distance||p.Key==Tags.Locked).ToImmutableDictionary(),Details="入园地图上的位置和步行路长"};
    }
    private void Admit(int count)
    {
        if(!Open)return;
        for(int i=0;i<count;i++)AddPerson(Job.Visitor,$"游客{++visitorSequence:D4}","入口");
        var day=(int)(Now/1440);DailyAdmissions[day]=DailyAdmissions.GetValueOrDefault(day)+count;
        if(count>0)Log($"入园 {count} 人；在园游客 {InPark}");
    }
    private void ArrivalTick()
    {
        if(disposed)return;
        if(Open&&lastOpeningDay==(int)(Now/1440))Admit(Math.Max(population/12,population>0?1:0));
        Session.CTB.Schedule(15,ArrivalTick);
    }
    private void Tick()
    {
        if(disposed)return;
        if(Open&&lastOpeningDay!=(int)(Now/1440)){lastOpeningDay=(int)(Now/1440);Admit(population);}
        Anomalies.Tick();
        foreach(var p in People.Where(p=>p.Outcome=="在园").ToArray())Anomalies.Expose(p);
        Session.CTB.Schedule(1,Tick);
    }
    public void Observe(Person person)
    {
        if(person.Room is not {} room)return;
        var visible=new List<Projection>{Views[room],Views[person.Body]};
        foreach(var pair in Views.Where(v=>v.Key.Kind==ProjectionKind.Boundary&&v.Key.Get<SpatialProperty>()?.Space==room))
        {
            visible.Add(pair.Value);
            foreach(var target in pair.Key.Get<PassageProperty>()!.Connections.Values)visible.Add(Views[target]);
        }
        visible.AddRange(People.Where(p=>p.Room==room&&p.Outcome=="在园").Select(p=>Views[p.Body]));
        // 对同处位置的旧记忆进行缺席观察；只知道没看见，不能知道对方去了哪里。
        foreach(var remembered in person.Soul.World.Projections.Where(p=>p.Kind==ProjectionKind.Body&&p!=person.Self&&p.Parent==person.Self.Parent).ToArray())
            if(!visible.Any(v=>v.Name==remembered.Name))
                Observation.Observe(person.Soul,new(remembered.Name,remembered.Kind){Target=remembered,Source=ObservationSource.Perception,Time=Now,
                    Move=true,Parent=null,Detected=false,Details="巡视此处未见此人；去向未知"});
        Runtime.Sync();Runtime.Observe(person.Soul,visible,Now,new LocalView(Views[room]));
        var watch=person.Watch is {} rememberedWatch && person.Soul.World.Contains(rememberedWatch)?rememberedWatch:
            person.Watch=Observation.Observe(person.Soul,new("手表",ProjectionKind.Object){Source=ObservationSource.Perception,Time=Now,Existence=Truth.True});
        Observation.Observe(person.Soul,new(watch.Name,watch.Kind){Target=watch,Time=Now,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Z.Clock,(int)Now)});
        // 自身时间感、疲劳和到岗记忆也走 Observe，不由决策查询客观世界。
        var section=VisibleSection(room.TrueName);
        var unread=Rules.Clauses.Values.Any(c=>c.Section==section&&person.Books.GetValueOrDefault(c.Key)?.Text!=c)||person.Books.Values.Any(b=>b.Text.Section==section&&!Rules.Clauses.ContainsKey(b.Text.Key));
        var entries=new Dictionary<Tag,int?>
        {
            [new("未读文书")]=unread?1:0,
            [new("入园时长")]=(int)(Now-person.Arrival),[new("馆内分钟")]=person.AquariumEntered<0?0:(int)(Now-person.AquariumEntered),
            [new("白天入馆")]=person.AquariumEntered>=0&&person.AquariumEntered%1440<960&&person.AquariumEntered%1440>=480?1:0,
            [new("查货到期")]=Now>=person.NextShelf&&person.DutyRoom=="饮料店"?1:0,[new("修剪到期")]=Now>=person.NextTrim&&person.DutyRoom=="兔子园区"?1:0,
            [new("喂食到期")]=Now>=person.NextFeed&&person.DutyRoom=="兔子园区"?1:0,[new("查狮到期")]=Now>=person.NextLionCheck?1:0,
            [new("巡逻到期")]=Now>=person.NextShelf?1:0,[new("疲倦")]=(int)Math.Min(100,(Now-person.Arrival)/6),
            [new("隔离中")]=Now<person.IsolationUntil?1:0
        };
        Observation.Observe(person.Soul,new(person.Self.Name,person.Self.Kind){Target=person.Self,Source=ObservationSource.Introspection,Time=Now,Tags=entries.ToImmutableDictionary()});
    }
    private sealed class LocalView(Projection room):IProjectionTransform
    {
        public ObservationInput? Transform(ObservationInput input)
        {
            if(input.Kind==ProjectionKind.Boundary&&input.Target is {} remembered)
                return input with {RemoveRelations=remembered.Relations.Where(r=>r.Relation==Z.Leads&&!input.AddRelations.Contains(r)).ToImmutableHashSet()};
            // 邻接空间只看见入口及名称；不能隔着通道获知房间里的异常或人员。
            if(input.Kind==ProjectionKind.Space&&input.Name!=room.Name)return input with {Tags=ImmutableDictionary<Tag,int?>.Empty};
            return input;
        }
    }
    private void Think(Person p)
    {
        if(disposed||p.Outcome!="在园"||p.Execution?.Status==ExecutionStatus.Pending)return;
        if(!p.SkipNextObservation)Observe(p);
        p.SkipNextObservation=false;
        var decision=ZooMinds.Decide(p);
        if(decision is null){Session.CTB.Schedule(5,()=>Think(p));return;}
        p.CurrentAction=Describe(decision.Effect);
        p.Execution=Session.Submit(p.Soul,new(p.Self,decision.Effect,0,Basis:decision.Basis));
    }
    private void Completed(ActionExecution execution)
    {
        if(!actors.TryGetValue(execution.Intent.Actor,out var p))return;
        var previous=p.Self.Parent?.Name??"";
        p.LastActionResult=$"{Now:F1}：{Describe(execution.Intent.Changes)} → {execution.Status} / {execution.Result?.Failure}";
        if(execution.Status==ExecutionStatus.Failed&&execution.Intent.Basis?.Name=="待执行言灵")Anomalies.UtteranceFailed(p);
        if(execution.Status==ExecutionStatus.Completed&&p.Outcome=="在园")
        {
            var command=p.Body.Value(Z.Command);
            p.Body.Set(Z.Command,0,false);
            if(command>0)Anomalies.Perform(p,(Deed)(command-1));
            if(p.Room?.TrueName is {} current&&current!=previous)
            {
                var duration=TravelDelay(execution.Intent.Changes);
                Travels.Add((Now,p.Body.TrueName,previous,current,duration));
                p.Body.Set(Z.Mixed,0);
                if(ZooMinds.IsInside(current)&&!ZooMinds.IsInside(previous))p.AquariumEntered=Now;
                if(!ZooMinds.IsInside(current))p.AquariumEntered=-1;
                Anomalies.Arrive(p,previous);
                if(previous=="园外"&&p.Job==Job.Red){p.Returns++;Log($"{p.Body.TrueName}第{p.Returns}次实际返岗");}
                Log($"{p.Body.TrueName}：{previous} → {current}，步行 {duration:F0} 分钟");
            }
        }
        else Log($"{p.Body.TrueName}：行动未完成（{execution.Result?.Failure}）");
        if(p.Outcome=="在园")Session.CTB.Schedule(.25,()=>Think(p));
    }
    public static double TravelDelay(Effect effect)=>effect switch {Effect.Wait w=>w.Delay+TravelDelay(w.Body),Effect.Sequence s=>s.Items.Sum(TravelDelay),_=>0};
    public static string Describe(Effect effect)=>effect switch
    {Effect.Wait w=>$"{Describe(w.Body)}（{w.Delay}分钟）",Effect.Act a=>$"{(a.Operation==ActionOp.Travel?"前往":a.Operation.ToString())} {(a.Target is Noun.Reference r?r.Projection.Name:a.Target.ToString())}",Effect.Set s when s.Tag==Z.Command=>ZooMinds.Actions.FirstOrDefault(a=>a.Value==(Deed)(s.Value-1)).Key??((Deed)(s.Value-1)).ToString(),Effect.Sequence s=>string.Join(" → ",s.Items.Select(Describe)),Effect.TemporarySet t=>$"临时改变 {t.Tag.Name}={t.Value}，{t.Duration}分钟",Effect.Say s=>$"对话：{s.Text}",_=>effect.GetType().Name};
    private static string VisibleSection(string? room)=>room switch {"入口"=>"Visitor","海洋馆门口"=>"Gate","海洋馆" or "客房"=>"Inside","水母区"=>"Red","鲸鱼区"=>"Survivor","猿猴园区"=>"Lost","内部宿舍"=>"Staff","安保室"=>"Guard","园长办公室"=>"Director",_=>"Custom"};
    public void ReadLocal(Person p)
    {
        var section=VisibleSection(p.Room?.TrueName);
        foreach(var text in Rules.Clauses.Values.Where(c=>c.Section==section))ZooRules.Read(p,text,Now);
        // 读到撤回告示后才撤销旧建议；别处的人保留旧版本。
        foreach(var book in p.Books.Values.Where(b=>b.Text.Section==section&&!Rules.Clauses.ContainsKey(b.Text.Key)).ToArray())
        {
            Observation.Observe(p.Soul,new(book.Projection.Name,book.Projection.Kind){Target=book.Projection,Source=ObservationSource.Perception,Time=Now,Rules=[],Details="现场撤回告示"});
            p.Books.Remove(book.Text.Key);
        }
    }
    public int Withdraw(string key,bool broadcast)
    {
        if(!Rules.Clauses.TryGetValue(key,out var text))return 0;
        Rules.Remove(key);
        var count=0;
        if(broadcast)foreach(var p in BroadcastAudience())
        {
            if(!p.Books.Remove(key,out var old))continue;
            Observation.Observe(p.Soul,new(old.Projection.Name,old.Projection.Kind){Target=old.Projection,Source=ObservationSource.Testimony,Time=Now,Rules=[],Details=$"听见撤回广播：{text.Key}"});count++;
        }
        Log($"撤回 {key}；已通过广播撤销 {count} 人的记忆，其他人等待现场告示");
        return count;
    }
    private IEnumerable<Person> BroadcastAudience()=>People.Where(p=>p.Outcome=="在园"&&!ZooMinds.IsInside(p.Room?.TrueName)&&p.Room?.TrueName!="内部宿舍");
    public void Replan(Person p,bool observeFirst=false)
    {
        if(p.Outcome!="在园")throw new InvalidOperationException("只能驱动仍在园的人物。");
        if(p.Execution?.Status==ExecutionStatus.Pending)Session.Cancel(p.Execution);
        p.SkipNextObservation=!observeFirst;
        Think(p);
    }
    public void ScheduleReturn(Person p)
    {
        if(p.Job!=Job.Red)return;
        var next=(Math.Floor(Now/1440)+1)*1440+27;
        Log($"{p.Body.TrueName}回家，下次00:27启程，00:30返岗");
        Session.CTB.Schedule(next-Now,()=>
        {
            if(disposed||p.Outcome!="正常离岗"||p.Job!=Job.Red)return;
            p.Outcome="在园";p.LastExposure=Now;p.AquariumEntered=-1;
            Think(p);
            Log($"{p.Body.TrueName}到达员工入口，依据脑内岗位与通道选择返岗行动");
        });
    }
    public void Broadcast(string key)
    {
        var text=Rules.Clauses[key];
        foreach(var p in BroadcastAudience())
            ZooRules.Read(p,text,Now,ObservationSource.Testimony);
        Log($"园内广播修订 {key}；封闭宿舍及海洋馆未收到广播");
    }
    public void Log(string message){Events.Add($"[{(int)(Now/1440)+1}日 {((int)Now%1440)/60:D2}:{(int)Now%60:D2}] {message}");if(Events.Count>600)Events.RemoveRange(0,Events.Count-600);}
    public int Run(double target,int maxSteps=100000)=>Session.CTB.RunUntil(target,maxSteps);
    public void Dispose(){disposed=true;Session.Completed-=Completed;Session.Dispose();}
}








