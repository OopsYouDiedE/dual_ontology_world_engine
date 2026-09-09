using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Examples.ZooSimulator;

/// <summary>作品机制只处理物件、身体、感知、时空和行动，不读取文书、编号或采信预设。</summary>
public sealed class ZooAnomalies
{
    private readonly ZooWorld w;
    private readonly Random random;
    private double nextAnomaly;
    private readonly Essence rabbit;
    private readonly Essence[] lions;
    private readonly Essence elephant,jellyfish;
    private readonly SoulEssence observer=new("它的认知");
    private readonly Essence shadowBody;
    private readonly Projection shadowSelf;
    private ActionExecution? shadowAction;
    private Essence? hauntedRoom;
    public string ItsLocation=>shadowBody.Get<SpatialProperty>()!.Space!.TrueName;
    public string ItsIntention { get; private set; }="等待可行动的光影";
    public string ItsReason { get; private set; }="沿有阴影的通道移动，白天寻找树荫，夜间趋光";
    public double NextAnomaly=>nextAnomaly;
    public int BlackBirths { get; private set; }
    public IEnumerable<string> Classifications=>observer.World.Projections.Where(p=>p.Kind==ProjectionKind.Body&&p!=shadowSelf)
        .Select(p=>$"{p.Name}：{(p.Tags.TryGetValue(new Tag("判定物种"),out var species)?((Species)species).ToString():"无法分类")}");
    private readonly Dictionary<Person,Essence> maps=[];
    private readonly Dictionary<Person,Projection> casts=[];

    public ZooAnomalies(ZooWorld world,Random random)
    {
        w=world;this.random=random;nextAnomaly=w.Now+25;
        rabbit=Animal("逃出的兔子","兔子园区");
        lions=Enumerable.Range(1,4).Select(i=>Animal($"白狮{i}","狮子园区")).ToArray();
        elephant=Animal("真正的大象","大象园区");jellyfish=Animal("活水母","水母区");
        w.Runtime.Project(observer,observer.TrueName);
        shadowBody=Animal("异常影子","兔子园区");observer.Relations.Add((Relations.Controls,shadowBody));
        shadowSelf=w.Runtime.Observe(observer,[w.Views[w.Rooms["兔子园区"]],w.Views[shadowBody]],w.Now)[1];
        w.Session.Completed+=ItCompleted;
    }
    private Essence Animal(string name,string room)
    {
        var animal=new Essence(name,ProjectionKind.Body);animal.Add(new SpatialProperty {Space=w.Rooms[room]});animal.Set(Z.Health,100);
        w.Views.Add(animal,w.Runtime.Project(animal,name));return animal;
    }
    public void GiveMap(Person p)
    {
        var map=new Essence(p.Body.TrueName+"的地图");map.Add(new SpatialProperty {Holder=p.Body});map.Set(Z.Paper,0);
        maps[p]=map;w.Views.Add(map,w.Runtime.Project(map,map.TrueName));
    }
    public Essence? Paper(Person p)=>maps.GetValueOrDefault(p) is {} map&&map.Get<SpatialProperty>()?.Holder==p.Body&&map.Value(Z.Paper)>0?map:null;
    private void TransferPaper(Person p,Essence? holder,Essence? room=null)
    {
        if(Paper(p) is not {} paper)return;
        paper.Get<SpatialProperty>()!.Holder=holder;paper.Get<SpatialProperty>()!.Space=room;
        p.Body.Set(Z.Paper,0);
        if(holder is not null && w.People.FirstOrDefault(q=>q.Body==holder) is {} receiver)
        {maps[receiver]=paper;receiver.Body.Set(Z.Paper,1);}
    }
    public void Tick()
    {
        var t=w.Now%1440;
        foreach(var pair in w.Views.Where(p=>p.Key.Kind==ProjectionKind.Boundary))
        {
            var passage=pair.Key.Get<PassageProperty>()!;
            var from=pair.Key.Get<SpatialProperty>()!.Space!;
            if(pair.Key.TrueName=="园外 → 入口")pair.Key.Get<LockProperty>()!.IsLocked=!w.Open;
            if(pair.Key.TrueName=="海洋馆 → 园外")
            {
                var destination=t>=360&&t<390?w.Rooms["园外"]:w.Rooms["海洋馆门口"];
                passage.Connections[from]=destination;
                pair.Key.Get<SurfaceProperty>()!.Relations[pair.Value]=[(Z.From,w.Views[from]),(Z.Leads,w.Views[destination])];
            }
        }
        foreach(var p in w.People)
        {
            p.Body.Set(new Tag("普通离园资格"),p.Job!=Job.Black&&p.TrueSpecies==Species.Human&&p.HumanBelief>=35?1:0);
            p.Body.Set(new Tag("海馆离园资格"),p.Job==Job.Red||t>=360&&t<390&&p.HumanBelief>=70?1:0);
        }
        if(w.Now>=nextAnomaly&&shadowAction?.Status!=ExecutionStatus.Pending)PlanIt();
        foreach(var room in w.Rooms.Values)
        {
            room.Set(new Tag("人数"),w.People.Count(p=>p.Room==room&&p.Outcome=="在园"));
            if(room.Value(new Tag("封闭开始"))>0)room.Set(new Tag("封闭分钟"),(int)w.Now-room.Value(new Tag("封闭开始")));
        }
        if((int)t==75)w.Rooms["园长办公室"].Set(Z.Light,w.Rooms["园长办公室"].Value(Z.Power));
        // 夜间值守员的监控是独立观察通道，不依赖园长在办公室。
        if(t>=75&&t<360&&w.Rooms["园长办公室"].Value(Z.Power)==0)
        foreach(var p in w.People.Where(p=>p.Job==Job.Blue&&p.Outcome=="在园"))
            w.Runtime.Observe(p.Soul,[w.Views[w.Rooms["园长办公室"]]],w.Now);
        if((int)w.Now%5==0)Roar();
        w.Runtime.Sync();
    }
    private void PlanIt()
    {
        // 它能感知光影网络。这个感知范围属于作品机制，人类不共享它的心智。
        var source=w.Views.Where(p=>p.Key.Kind is ProjectionKind.Space or ProjectionKind.Boundary||p.Key==shadowBody).Select(p=>p.Value);
        w.Runtime.Observe(observer,source,w.Now,new ShadowView());
        var plan=observer.World.Projections.FirstOrDefault(p=>p.Name=="光影行动偏好")??
            Observation.Observe(observer,new("光影行动偏好",ProjectionKind.Concept){Existence=Truth.True});
        var night=w.Now%1440<480||w.Now%1440>=960;
        var rules=new List<Rule>();
        foreach(var door in observer.World.Projections.Where(p=>p.Kind==ProjectionKind.Boundary&&p.Value(Tags.Locked)==0&&p.Relations.Contains((Z.From,shadowSelf.Parent!))))
        {
            var room=door.Relations.FirstOrDefault(r=>r.Relation==Z.Leads).Target;
            if(room is null||room.Name is "园外" or "内部宿舍" or "客房"||room.Value(Z.Calm)>0)continue;
            var medium=night?Z.Light:Z.Shadow;
            if(room.Value(medium)<=0)continue;
            var score=(night?room.Value(Z.Light)*40:room.Value(Z.Shadow)*40)+room.Value(new Tag("人数"));
            if(room.Name=="园长办公室"&&night&&room.Value(Z.Power)>0)score+=120;
            if(room.Name=="狮子园区"&&room.Value(Z.Roaring)>0)score-=150;
            score-=observer.World.History.Count(h=>h.Target==shadowSelf&&h.Details=="已到访:"+room.Name)*3;
            var travel=new Effect.Sequence([new Effect.Require(new Condition.Tagged(room,medium,1)),new Effect.Act(ActionOp.Travel,door)]);
            rules.Add(new(new Condition.Constant(Truth.True),RuleMode.Prefer,new Effect.Wait(Math.Max(1,door.Value(Z.Minutes)),travel),score,Truth.True));
        }
        Observation.Observe(observer,new(plan.Name,plan.Kind){Target=plan,Source=ObservationSource.Inference,Time=w.Now,Rules=[..rules]});
        if(DecisionSystem.Choose(observer.World,shadowSelf) is not {} decision){nextAnomaly=w.Now+5;return;}
        ItsIntention=ZooWorld.Describe(decision.Effect);
        ItsReason=$"脑内光影评分 {decision.Score}；{(night?"夜间趋光，办公室照明强烈诱引":"白天偏好树荫及附近人群")}；正常白狮降低接近意愿";
        shadowAction=w.Session.Submit(observer,new(shadowSelf,decision.Effect,0));
    }
    private sealed class ShadowView:IProjectionTransform
    {
        public ObservationInput? Transform(ObservationInput input)=>input.Kind==ProjectionKind.Boundary&&input.Target is {} old
            ?input with {RemoveRelations=old.Relations.Where(r=>r.Relation==Z.Leads&&!input.AddRelations.Contains(r)).ToImmutableHashSet()}:input;
    }
    private void ItCompleted(ActionExecution execution)
    {
        if(execution!=shadowAction)return;
        nextAnomaly=w.Now+15+random.Next(6);
        if(execution.Status!=ExecutionStatus.Completed)return;
        if(hauntedRoom is {} old){old.Set(Z.Danger,0);old.Set(new Tag("出逃兔子"),0);old.Set(Z.Split,0);old.Set(Z.Anomaly,0);}
        var room=shadowBody.Get<SpatialProperty>()!.Space!;
        w.Runtime.Observe(observer,[w.Views[room],w.Views[shadowBody]],w.Now);
        Observation.Observe(observer,new(shadowSelf.Name,shadowSelf.Kind){Target=shadowSelf,Source=ObservationSource.Introspection,Time=w.Now,Details="已到访:"+room.TrueName});
        hauntedRoom=room;
        if(room.Value(Z.Calm)>0){ItsReason="言灵使该处安静，暂不显现异常";return;}
        room.Set(Z.Danger,1);
        if(room.TrueName=="兔子园区")room.Set(new Tag("出逃兔子"),1);
        if(room.TrueName=="猿猴园区")room.Set(Z.Split,1);
        if(room.TrueName=="大象园区")room.Set(Z.Anomaly,1);
        if(room.TrueName=="饮料店")room.Set(Z.Food,1);
        if(room.TrueName=="园长办公室"&&w.Now%1440<360&&random.Next(4)==0){room.Set(Z.Power,0);room.Set(Z.Light,0);}
        w.Log($"它抵达{room.TrueName}并显现异常；{ItsReason}");w.Runtime.Sync();
    }
    private void BecomeBlack(Person p,string cause)
    {
        if(p.Job==Job.Black)return;
        p.Job=Job.Black;p.Body.Set(Z.Role,(int)Job.Black);BlackBirths++;Reconsider(p,Math.Min(p.HumanBelief ?? 65,65));
        w.Log($"新黑衣诞生：{p.Body.TrueName}，原因：{cause}；仍是真实人类");
    }
    public void Expose(Person p)
    {
        var dt=w.Now-p.LastExposure;p.LastExposure=w.Now;
        if(dt<=0||p.Room is not {} room)return;
        bool sheltered=room.TrueName is "内部宿舍" or "客房"&&room.Value(Z.Lamp)==0 ||room.TrueName=="水母区"&&jellyfish.Value(Z.Health)>0||room.Value(Z.Calm)>0;
        bool mixed=room.TrueName=="猿猴混处区"&&p.Body.Value(Z.Mixed)>0;
        bool shadow=room.Value(Z.Shadow)>0||(w.Now%1440<480||w.Now%1440>=960)&&room.Value(Z.Light)>0;
        bool near=room==shadowBody.Get<SpatialProperty>()!.Space;
        bool seen=p.HasSelf&&room.Value(Z.Danger)>0&&p.Self.Parent?.Value(Z.Danger)>0;
        if(!sheltered&&!mixed&&shadow&&near)
        {
            // 显露察觉才引来定向关注；未读规则者仍会受环境接触伤害。
            if((seen||p.HasSelf&&p.Self.Value(new Tag("明知违背"))>0)&&p.Body.Value(new Tag("掩饰"))==0)p.Body.Set(Z.Attention,Math.Min(100,p.Body.Value(Z.Attention)+(int)Math.Ceiling(dt)),false);
            if(p.HasSelf&&p.Body.Value(Z.Attention)>15&&!(room.TrueName!="园长办公室"&&(w.Now%1440<360)&&w.Rooms["园长办公室"].Value(Z.Power)>0&&w.Rooms["园长办公室"].Value(Z.Light)>0))
            {
                var drop=(int)Math.Ceiling(dt*.7);
                new CorruptionProperty(new IdentityDistortion(p.Self,Math.Max(0,p.Self.Value(Z.Human)-drop))).Apply(p.Soul,w.Now);
            }
            if(room.Value(Z.Danger)>0&&p.Body.Value(Z.Paper)==0)
            {p.Body.Set(new Tag("兔化稳定"),0);p.Body.Set(Z.Rabbit,Math.Min(100,p.Body.Value(Z.Rabbit)+(int)Math.Ceiling(dt*.6)),false);}
        }
        if(p.Body.Value(Z.Rabbit)>=30&&p.Body.Value(new Tag("兔化稳定"))==0)
            p.Body.Set(Z.Rabbit,Math.Min(100,p.Body.Value(Z.Rabbit)+(int)Math.Ceiling(dt*.4)),false);
        if(p.Body.Value(Z.Rabbit)>12)
        {
            if(p.SymptomSince<0)p.SymptomSince=w.Now;
            p.Body.Set(Z.Symptoms,(int)(w.Now-p.SymptomSince));
        }
        if(p.Body.Value(Z.Rabbit)>=85)p.TrueSpecies=Species.Rabbit;
        else if(p.HumanBelief is <15)p.TrueSpecies=Species.Goat;
        if(near&&!sheltered)
        {
            p.ItsClassification=Paper(p)!=null||mixed?null:p.HumanBelief is <35?Species.Goat:p.Body.Value(Z.Rabbit)>60?Species.Rabbit:p.HasSelf?Species.Human:p.ItsClassification;
            p.LastClassified=w.Now;
            w.Runtime.Observe(observer,[w.Views[room],w.Views[p.Body]],w.Now,new ItsView(p.Body.TrueName,p.ItsClassification));
        }
        if(!sheltered&&near&&p.ItsClassification is Species.Goat or Species.Rabbit&&shadow)
            p.Body.Set(Z.Health,Math.Max(0,p.Body.Value(Z.Health)-(int)Math.Ceiling(dt*2)));
        if(p.Body.Value(Z.Health)==0)Finish(p,"被阴影吞噬");
        else if(p.TrueSpecies==Species.Rabbit)Finish(p,"身体兔化");
        else if(p.TrueSpecies==Species.Goat)Finish(p,"身份山羊化");
    }
    private sealed class IdentityDistortion(Projection target,int human):IProjectionTransform
    {
        public ObservationInput? Transform(ObservationInput input)=>ReferenceEquals(input.Target,target)?input with
        {Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Z.Human,human),Details="它干扰了对自身身份的理解"}:null;
    }
    private sealed class ItsView(string name,Species? species):IProjectionTransform
    {
        public ObservationInput? Transform(ObservationInput input)=>input.Name==name?input with
        {Tags=ImmutableDictionary<Tag,int?>.Empty.Add(new Tag("判定物种"),species is {} s?(int)s:null),Details="它的分类；纸片或混处会令分类不确定"}:input;
    }
    public void Reconsider(Person p,int human)
    {
        if(human is <0 or >100)throw new ArgumentOutOfRangeException(nameof(human));
        if(!p.HasSelf)return;
        new CorruptionProperty(new IdentityDistortion(p.Self,human)).Apply(p.Soul,w.Now);
    }
    private void Roar()
    {
        var room=w.Rooms["狮子园区"];
        var count=lions.Count(l=>l.Value(Z.Health)>0&&l.Get<SpatialProperty>()!.Space==room);
        room.Set(Z.LionCount,count+(room.Value(new Tag("额外狮"))>0?1:0));
        room.Set(Z.Roaring,count==4?1:0);
        if(count!=4)return;
        foreach(var p in w.People.Where(p=>p.Room==room&&p.Outcome=="在园"))
        {
            if(p.Body.Value(Z.Rabbit)>=65){p.Body.Set(Z.Health,0);Finish(p,"白狮清除严重兔化者");continue;}
            if(p.Body.Value(Z.Rabbit)>40&&p.Job is Job.Blue or Job.Guard)
            {BecomeBlack(p,"正常白狮咬伤但存活");p.Body.Set(Z.Health,Math.Max(1,p.Body.Value(Z.Health)-20));}
            // 吼叫驱逐关注，不让纸片或区域自动治愈已经发生的身体转化。
            if(p.Body.Value(Z.Rabbit)>0||p.Body.Value(Z.Attention)>0||p.Body.Value(Z.Symptoms)>0||p.HumanBelief is <85)
            {p.Body.Set(Z.Attention,0,false);p.Body.Set(new Tag("获救"),1);p.Body.Set(new Tag("兔化稳定"),1);}
        }
        if(rabbit.Get<SpatialProperty>()!.Space==room&&rabbit.Value(Z.Paper)==0)rabbit.Set(Z.Health,0);
    }
    public void Arrive(Person p,string from)
    {
        if(p.Room==w.Rooms["园外"])
        {
            bool aquarium=ZooMinds.IsInside(from);
            bool window=w.Now%1440>=360&&w.Now%1440<390;
            if(aquarium?(p.Job==Job.Red||window&&p.HumanBelief>=70):p.Job!=Job.Black&&p.HumanBelief>=35)
                Finish(p,p.Job==Job.Visitor||p.Job==Job.Black?"成功逃离":"正常离岗");
        }
        if(p.Room==w.Rooms["大象园区"]&&p.Body.Value(Z.RabbitEars)>0)
        {p.Room.Set(Z.Danger,1);p.Room.Set(Z.Anomaly,1);elephant.Set(Z.Health,Math.Max(0,elephant.Value(Z.Health)-50));
            if(elephant.Value(Z.Health)==0){elephant.Get<SpatialProperty>()!.Space=w.Rooms["鲸鱼区"];w.Rooms["鲸鱼区"].Set(Z.Anomaly,1);}
            w.Log("兔耳饰人类触发大象区事件；真实大象承接异常伤害");}
        if(p.Room==w.Rooms["后勤区"]&&p.Job==Job.Visitor)p.Body.Set(new Tag("迷路"),1);
    }
    private void Finish(Person p,string outcome)
    {
        if(p.Outcome!="在园")return;
        p.Outcome=outcome;
        if(p.Execution?.Status==ExecutionStatus.Pending)w.Session.Cancel(p.Execution);
        w.Log($"{p.Body.TrueName}：{outcome}");
        if(outcome=="正常离岗")w.ScheduleReturn(p);
    }
    public void Perform(Person p,Deed deed)
    {
        var room=p.Room!;
        w.Log($"{p.Body.TrueName}：{deed}");
        if(p.HasSelf)Observation.Observe(p.Soul,new(p.Self.Name,p.Self.Kind){Target=p.Self,Source=ObservationSource.Introspection,Time=w.Now,Details=$"交互完成:{deed}:{room.TrueName}"});
        switch(deed)
        {
            case Deed.Watch:
                if(p.HasSelf)Observation.Observe(p.Soul,new(p.Self.Name,p.Self.Kind){Target=p.Self,Source=ObservationSource.Introspection,Time=w.Now,Details="游览完成:"+room.TrueName});break;
            case Deed.Read:w.ReadLocal(p);break;
            case Deed.Tear:
                if(maps.GetValueOrDefault(p) is {} map&&map.Get<SpatialProperty>()?.Holder==p.Body)
                {map.Set(Z.Paper,1);p.Body.Set(Z.Paper,1);p.Body.Set(Z.Map,0);}break;
            case Deed.FeedPaper:
                if(room.TrueName=="兔子园区"&&Paper(p)!=null)
                {TransferPaper(p,rabbit);rabbit.Set(Z.Paper,1);room.Set(Z.Laugh,1);w.Session.CTB.Schedule(8,()=>room.Set(Z.Laugh,0));}break;
            case Deed.Discard:TransferPaper(p,null,room);break;
            case Deed.Signal:
                if(room.TrueName=="大象园区")TransferPaper(p,null,room);break;
            case Deed.GivePaper:
                if(w.People.FirstOrDefault(q=>q!=p&&q.Room==room&&q.Job==Job.Black) is {} recipient)TransferPaper(p,recipient.Body);break;
            case Deed.StealPaper:
                if(w.People.FirstOrDefault(q=>q!=p&&q.Room==room&&Paper(q)!=null) is {} victim)
                {TransferPaper(victim,p.Body);Reconsider(p,Math.Max(0,p.Self.Value(Z.Human)-25));}break;
            case Deed.FeedRabbit:
                if(room.TrueName=="兔子园区")
                {if(p.Job==Job.Visitor){p.Body.Set(new Tag("兔化稳定"),0);p.Body.Set(Z.Rabbit,p.Body.Value(Z.Rabbit)+30,false);}p.NextFeed=w.Now+10080;}break;
            case Deed.BuyBlood:
                if(room.Value(Z.Food)>0){p.Body.Set(Z.Blood,1);room.Set(Z.Food,0);}break;
            case Deed.EatMeat:
                if(room.Value(Z.Food)>0||p.Body.Value(Z.Blood)>0){p.Body.Set(Z.Blood,0);room.Set(Z.Food,0);p.Body.Set(new Tag("兔化稳定"),0);p.Body.Set(Z.Rabbit,p.Body.Value(Z.Rabbit)+8,false);}break;
            case Deed.WearBlack:
                if(room.TrueName=="海洋馆门口")BecomeBlack(p,"采信门口方案并实际换上黑衣");break;
            case Deed.Reaffirm:
                ReconsiderByObservation(p,Math.Min(100,p.Self.Value(Z.Human)+12),"回忆人类的身体和自身经历");
                if(p.Self.Value(Z.Human)>=50)ZooExperiment.RecallExitMap(p,w.Now);
                break;
            case Deed.ElephantIdentity:
                Observation.Observe(p.Soul,new("我是大象",ProjectionKind.Concept){Source=ObservationSource.Inference,Time=w.Now,Details="误认自身为大象"});ReconsiderByObservation(p,Math.Max(0,p.Self.Value(Z.Human)-10),"错误自我身份");break;
            case Deed.GoatIdentity:ReconsiderByObservation(p,Math.Max(0,p.Self.Value(Z.Human)-12),"采信纸条中的山羊身份");break;
            case Deed.Misidentify:
                Observation.Observe(p.Soul,new("被误认的大象",ProjectionKind.Concept){Source=ObservationSource.Inference,Time=w.Now,Details="兔子/大象的混淆解释，不是客观物种关系"});
                ReconsiderByObservation(p,Math.Max(0,p.Self.Value(Z.Human)-3),"采用混淆解释");break;
            case Deed.Ignore:p.Body.Set(new Tag("掩饰"),1,false);w.Session.CTB.Schedule(10,()=>p.Body.Set(new Tag("掩饰"),0,false));break;
            case Deed.MixApes:if(room.TrueName=="猿猴混处区")p.Body.Set(Z.Mixed,1);break;
            case Deed.Help:case Deed.Report:case Deed.Talk:break;
            case Deed.Escort:case Deed.Evacuate:break;
            case Deed.Repair:
                if(room.TrueName=="园长办公室"){room.Set(Z.Power,1);room.Set(Z.Light,1);}break;
            case Deed.SwitchOff:if(room.TrueName=="客房")room.Set(Z.Lamp,0);break;
            case Deed.Trim:if(room.TrueName=="兔子园区"){room.Set(Z.Shadow,0);p.NextTrim=w.Now+43200;}break;
            case Deed.Lure:
                if(rabbit.Value(Z.Health)>0&&rabbit.Get<SpatialProperty>()!.Space==room)
                {w.Session.CTB.Schedule(8,()=>{rabbit.Get<SpatialProperty>()!.Space=w.Rooms["狮子园区"];w.Runtime.Sync();});}break;
            case Deed.Splash:
                if(room.TrueName=="狮子园区"&&p.Body.Value(Z.Blood)>0){room.Set(new Tag("额外狮"),0);p.Body.Set(Z.Blood,0);}break;
            case Deed.Collect:room.Set(Z.Food,0);p.Body.Set(Z.Blood,1);break;
            case Deed.Inspect:p.NextShelf=w.Now+60;break;
            case Deed.Patrol:p.NextShelf=w.Now+60;break;
            case Deed.CountLions:p.NextLionCheck=w.Now+4320;break;
            case Deed.BuyToy:p.Body.Set(Z.Toy,1);break;
            case Deed.DiscardToy:p.Body.Set(Z.Toy,0);break;
            case Deed.CollectToy:
                foreach(var guest in w.People.Where(q=>q.Room==room&&q!=p))guest.Body.Set(Z.Toy,0);break;
            case Deed.GetMap:GiveMap(p);p.Body.Set(Z.Map,1);break;
            case Deed.Resign:if(p.Job!=Job.Black){p.Resigned=true;p.Job=Job.Visitor;p.Body.Set(Z.Role,(int)Job.Visitor);}break;
            case Deed.Isolate:
                if(p.IsolationUntil==0)p.IsolationUntil=w.Now+780;
                if(w.Now>=p.IsolationUntil&&p.TrueSpecies==Species.Human&&p.HumanBelief>=70)p.IsolationUntil=0;break;
            case Deed.Evict:break;
            case Deed.CloseApes:
                foreach(var door in w.Views.Keys.Where(e=>e.Kind==ProjectionKind.Boundary&&e.TrueName=="主步道 → 猿猴园区"))
                {door.Get<LockProperty>()!.IsLocked=true;w.Rooms["猿猴园区"].Set(new Tag("封闭开始"),(int)w.Now);}break;
            case Deed.OpenApes:
                foreach(var door in w.Views.Keys.Where(e=>e.TrueName=="主步道 → 猿猴园区"))door.Get<LockProperty>()!.IsLocked=false;
                w.Rooms["猿猴园区"].Set(new Tag("封闭开始"),0);w.Rooms["猿猴园区"].Set(new Tag("封闭分钟"),0);break;
            case Deed.Charge:w.Rooms["水母区"].Set(new Tag("电量"),100);break;
            case Deed.Clean:room.Set(new Tag("水母死亡"),0);break;
            case Deed.Cast:
                if(casts.Remove(p,out var cast))
                {
                    Observation.Observe(p.Soul,new(cast.Name,cast.Kind){Target=cast,Source=ObservationSource.Introspection,Time=w.Now,Rules=[],Details="言灵已经说出"});

                }
                break;
            case Deed.Rest:break;
        }
        w.Runtime.Sync();
    }
    private void ReconsiderByObservation(Person p,int human,string details)
    {
        if(p.HasSelf)Observation.Observe(p.Soul,new(p.Self.Name,p.Self.Kind)
        {Target=p.Self,Source=ObservationSource.Inference,Time=w.Now,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Z.Human,human),Details=details});
    }
    public void SayTemporary(Person speaker,Tag property,int value,double minutes)
        => QueueUtterance(speaker,$"temporary_set($Scene, {property.Name}, {value}, {minutes.ToString(System.Globalization.CultureInfo.InvariantCulture)})");

    public void QueueUtterance(Person speaker,string text)
    {
        if(!speaker.HasSelf)throw new InvalidOperationException("已遗忘自身，无法准备言灵行动。");
        if(speaker.Outcome!="在园")throw new InvalidOperationException("人物当前不在园。");
        if(casts.ContainsKey(speaker))throw new InvalidOperationException("人物已有待执行言灵。");
        var decoder=ZooExperiment.Decoder(speaker);
        var effect=decoder.DecodeEffect(text);
        var request=Observation.Observe(speaker.Soul,new("待执行言灵",ProjectionKind.Procedure){Source=ObservationSource.Introspection,Time=w.Now,Existence=Truth.True});
        Effect procedure=new Effect.Sequence([new Effect.Spend(speaker.Self,Z.Mana,1),new Effect.Wait(1,
            new Effect.Sequence([effect,new Effect.Set(speaker.Self,Z.Command,(int)Deed.Cast+1)]))]);
        Observation.Observe(speaker.Soul,new(request.Name,request.Kind){Target=request,Source=ObservationSource.Introspection,Time=w.Now,
            Procedure=procedure,Rules=[new(new Condition.Constant(Truth.True),RuleMode.Prefer,procedure,1000,Truth.True)],Details=text});
        casts.Add(speaker,request);
        w.Log($"{speaker.Body.TrueName}掌握了言灵 AST：{text}");
    }
    public void UtteranceFailed(Person p)
    {
        if(casts.Remove(p,out var request)&&p.Soul.World.Contains(request))Observation.Observe(p.Soul,new(request.Name,request.Kind){Target=request,
            Source=ObservationSource.Introspection,Time=w.Now,Rules=[],ClearProcedure=true,Details="施放失败，检查执行记录后可重试"});
    }
}


