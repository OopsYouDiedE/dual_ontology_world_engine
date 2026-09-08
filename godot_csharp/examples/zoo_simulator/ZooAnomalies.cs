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
    private readonly Dictionary<Person,Essence> maps=[];
    private readonly Dictionary<Person,(Tag Property,int Value,double Minutes,Projection Request)> casts=[];
    private readonly Dictionary<(Essence,Tag),(int Baseline,List<(object Token,int Value)> Layers)> temporary=[];
    public ZooAnomalies(ZooWorld world,Random random)
    {
        w=world;this.random=random;nextAnomaly=w.Now+25;
        rabbit=Animal("逃出的兔子","兔子园区");
        lions=Enumerable.Range(1,4).Select(i=>Animal($"白狮{i}","狮子园区")).ToArray();
        elephant=Animal("真正的大象","大象园区");jellyfish=Animal("活水母","水母区");
        w.Runtime.Project(observer,observer.TrueName);
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
            p.Body.Set(new Tag("普通离园资格"),p.Job!=Job.Black&&p.TrueSpecies==Species.Human&&p.Self.Value(Z.Human)>=35?1:0);
            p.Body.Set(new Tag("海馆离园资格"),p.Job==Job.Red||t>=360&&t<390&&p.Self.Value(Z.Human)>=70?1:0);
        }
        if(w.Now>=nextAnomaly)
        {
            var room=w.Rooms[new[]{"兔子园区","猿猴园区","大象园区","饮料店"}[random.Next(4)]];
            room.Set(Z.Danger,1);
            if(room.TrueName=="兔子园区")room.Set(new Tag("出逃兔子"),1);
            if(room.TrueName=="猿猴园区")room.Set(Z.Split,1);
            if(room.TrueName=="大象园区")room.Set(Z.Anomaly,1);
            if(room.TrueName=="饮料店")room.Set(Z.Food,1);
            w.Log($"现场异常：{room.TrueName}出现可观察征兆");
            w.Session.CTB.Schedule(30,()=>{room.Set(Z.Danger,0);room.Set(new Tag("出逃兔子"),0);room.Set(Z.Split,0);room.Set(Z.Anomaly,0);w.Runtime.Sync();});
            if(w.Now%1440<360&&random.Next(4)==0)
            {w.Rooms["园长办公室"].Set(Z.Power,0);w.Rooms["园长办公室"].Set(Z.Light,0);w.Log("办公室供电故障：夜间诱引节点失效");}
            if(random.Next(8)==0)w.Rooms["狮子园区"].Set(new Tag("额外狮"),1);
            nextAnomaly=w.Now+35+random.Next(35);
        }
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
    public void Expose(Person p)
    {
        var dt=w.Now-p.LastExposure;p.LastExposure=w.Now;
        if(dt<=0||p.Room is not {} room)return;
        bool sheltered=room.TrueName is "内部宿舍" or "客房"&&room.Value(Z.Lamp)==0 ||room.TrueName=="水母区"&&jellyfish.Value(Z.Health)>0||room.Value(Z.Calm)>0;
        bool mixed=room.TrueName=="猿猴混处区"&&p.Body.Value(Z.Mixed)>0;
        bool shadow=room.Value(Z.Shadow)>0||(w.Now%1440<480||w.Now%1440>=960)&&room.Value(Z.Light)>0;
        bool seen=room.Value(Z.Danger)>0&&p.Self.Parent?.Value(Z.Danger)>0;
        if(!sheltered&&!mixed&&shadow)
        {
            // 显露察觉才引来定向关注；未读规则者仍会受环境接触伤害。
            if((seen||p.Self.Value(new Tag("明知违背"))>0)&&p.Body.Value(new Tag("掩饰"))==0)p.Body.Set(Z.Attention,Math.Min(100,p.Body.Value(Z.Attention)+(int)Math.Ceiling(dt)),false);
            if(p.Body.Value(Z.Attention)>15&&!(room.TrueName!="园长办公室"&&(w.Now%1440<360)&&w.Rooms["园长办公室"].Value(Z.Power)>0&&w.Rooms["园长办公室"].Value(Z.Light)>0))
            {
                var drop=(int)Math.Ceiling(dt*.7);
                new CorruptionProperty(new IdentityDistortion(p.Self,Math.Max(0,p.Self.Value(Z.Human)-drop))).Apply(p.Soul,w.Now);
            }
            if(room.Value(Z.Danger)>0&&p.Body.Value(Z.Paper)==0)
                p.Body.Set(Z.Rabbit,Math.Min(100,p.Body.Value(Z.Rabbit)+(int)Math.Ceiling(dt*.6)),false);
        }
        if(p.Body.Value(Z.Rabbit)>12)
        {
            if(p.SymptomSince<0)p.SymptomSince=w.Now;
            p.Body.Set(Z.Symptoms,(int)(w.Now-p.SymptomSince));
        }
        if(p.Body.Value(Z.Rabbit)>=85)p.TrueSpecies=Species.Rabbit;
        else if(p.Self.Value(Z.Human)<15)p.TrueSpecies=Species.Goat;
        p.ItsClassification=Paper(p)!=null||mixed?null:p.Self.Value(Z.Human)<35?Species.Goat:p.Body.Value(Z.Rabbit)>60?Species.Rabbit:Species.Human;
        w.Runtime.Observe(observer,[w.Views[room],w.Views[p.Body]],w.Now,new ItsView(p.Body.TrueName,p.ItsClassification));
        if(!sheltered&&p.ItsClassification is Species.Goat or Species.Rabbit&&shadow)
            p.Body.Set(Z.Health,Math.Max(0,p.Body.Value(Z.Health)-(int)Math.Ceiling(dt*2)));
        if(p.Body.Value(Z.Health)==0)Finish(p,"死亡");
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
            {p.Job=Job.Black;p.Body.Set(Z.Role,(int)Job.Black);p.Body.Set(Z.Health,Math.Max(1,p.Body.Value(Z.Health)-20));Reconsider(p,55);}
            // 吼叫驱逐关注，不让纸片或区域自动治愈已经发生的身体转化。
            p.Body.Set(Z.Attention,0,false);p.Body.Set(new Tag("获救"),1);
        }
        if(rabbit.Get<SpatialProperty>()!.Space==room&&rabbit.Value(Z.Paper)==0)rabbit.Set(Z.Health,0);
    }
    public void Arrive(Person p,string from)
    {
        if(p.Room==w.Rooms["园外"])
        {
            bool aquarium=ZooMinds.IsInside(from);
            bool window=w.Now%1440>=360&&w.Now%1440<390;
            if(aquarium?(p.Job==Job.Red||window&&p.Self.Value(Z.Human)>=70):p.Job!=Job.Black&&p.Self.Value(Z.Human)>=35)
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
    }
    public void Perform(Person p,Deed deed)
    {
        var room=p.Room!;
        w.Log($"{p.Body.TrueName}：{deed}");
        switch(deed)
        {
            case Deed.Watch:
                Observation.Observe(p.Soul,new(p.Self.Name,p.Self.Kind){Target=p.Self,Source=ObservationSource.Introspection,Time=w.Now,Details="游览完成:"+room.TrueName});break;
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
                {if(p.Job==Job.Visitor)p.Body.Set(Z.Rabbit,p.Body.Value(Z.Rabbit)+15,false);p.NextFeed=w.Now+10080;}break;
            case Deed.BuyBlood:
                if(room.Value(Z.Food)>0){p.Body.Set(Z.Blood,1);room.Set(Z.Food,0);}break;
            case Deed.EatMeat:
                if(room.Value(Z.Food)>0||p.Body.Value(Z.Blood)>0){p.Body.Set(Z.Blood,0);room.Set(Z.Food,0);p.Body.Set(Z.Rabbit,p.Body.Value(Z.Rabbit)+8,false);}break;
            case Deed.WearBlack:
                if(room.TrueName=="海洋馆门口"){p.Job=Job.Black;p.Body.Set(Z.Role,(int)Job.Black);Reconsider(p,Math.Min(p.Self.Value(Z.Human),65));}break;
            case Deed.Reaffirm:ReconsiderByObservation(p,Math.Min(100,p.Self.Value(Z.Human)+12),"回忆人类的身体和自身经历");break;
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
                if(w.Now>=p.IsolationUntil&&p.TrueSpecies==Species.Human&&p.Self.Value(Z.Human)>=70)p.IsolationUntil=0;break;
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
                    Observation.Observe(p.Soul,new(cast.Request.Name,cast.Request.Kind){Target=cast.Request,Source=ObservationSource.Introspection,Time=w.Now,Rules=[],Details="言灵已经说出"});
                    ApplyTemporary(p,cast.Property,cast.Value,cast.Minutes);
                }
                break;
            case Deed.Rest:break;
        }
        w.Runtime.Sync();
    }
    private void ReconsiderByObservation(Person p,int human,string details)=>Observation.Observe(p.Soul,new(p.Self.Name,p.Self.Kind)
    {Target=p.Self,Source=ObservationSource.Inference,Time=w.Now,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Z.Human,human),Details=details});
    public void SayTemporary(Person speaker,Tag property,int value,double minutes)
    {
        if(property!=Z.Calm&&property!=Z.Light&&property!=Z.Shadow)throw new ArgumentException("言灵只能临时改变安静、灯光或树荫。");
        if(!double.IsFinite(minutes)||minutes<=0||minutes>120)throw new ArgumentOutOfRangeException(nameof(minutes));
        if(value is <0 or >1)throw new ArgumentOutOfRangeException(nameof(value));
        if(speaker.Outcome!="在园"||speaker.Body.Value(Z.Mana)<=0)throw new InvalidOperationException("人物当前无法施放言灵。");
        if(casts.ContainsKey(speaker))throw new InvalidOperationException("人物已有待施放的言灵。");
        var request=Observation.Observe(speaker.Soul,new("待施放言灵",ProjectionKind.Procedure){Source=ObservationSource.Introspection,Time=w.Now,Existence=Truth.True});
        var decoder=new AstDecoder(speaker.Soul.World,Z.All,Z.Relations);
        var rules=decoder.DecodeRules($"RULE true() => PREFER sequence(spend($Actor, 言灵次数, 1), wait(1, set($Actor, 动作, {(int)Deed.Cast+1})))");
        Observation.Observe(speaker.Soul,new(request.Name,request.Kind){Target=request,Source=ObservationSource.Introspection,Time=w.Now,Rules=[..rules.Select(r=>r with {Acceptance=Truth.True,Weight=1000})]});
        casts.Add(speaker,(property,value,minutes,request));
        w.Log($"{speaker.Body.TrueName}准备施放言灵，完成当前行动后执行");
    }
    private void ApplyTemporary(Person speaker,Tag property,int value,double minutes)
    {
        var room=speaker.Room!;var key=(room,property);var token=new object();
        if(!temporary.TryGetValue(key,out var layer))layer=(room.Value(property),[]);
        layer.Layers.Add((token,value));temporary[key]=layer;

        room.Set(property,value);w.Runtime.Sync();
        w.Log($"{speaker.Body.TrueName}的言灵：此处{property.Name}={value}，持续{minutes}分钟");
        w.Session.CTB.Schedule(minutes,()=>
        {
            var current=temporary[key];current.Layers.RemoveAll(p=>ReferenceEquals(p.Token,token));
            room.Set(property,current.Layers.Count==0?current.Baseline:current.Layers[^1].Value);
            if(current.Layers.Count==0)temporary.Remove(key);
            w.Runtime.Sync();w.Log($"{room.TrueName}的{property.Name}言灵到期");
        });
    }
}




