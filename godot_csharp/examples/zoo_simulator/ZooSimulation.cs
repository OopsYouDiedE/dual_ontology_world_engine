using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;
namespace DualOntologyWorldEngine.Examples.ZooSimulator;

public enum Job { Visitor, Blue, Red, Black, Guard, Director }
public enum Species { Human, Rabbit, Goat }
public enum Deed { Watch, Rest, Read, Tear, FeedPaper, FeedRabbit, BuyBlood, EatMeat, WearBlack, Help, Reaffirm, Ignore, MixApes, Repair, SwitchOff, Trim, Roar, Splash, Discard, Signal, Inspect, Patrol, Resign, Isolate, StealPaper, Evict, Collect, Charge, Report, Follow, BuyToy, Lure, DiscardToy, Escort, Evacuate, CloseApes, Misidentify, GoatIdentity, GivePaper, CollectToy, Talk, CountLions, GetMap, Clean, OpenApes, ElephantIdentity, Cast }

public static class Z
{
    public static readonly Tag Health = new("生命"), Rabbit = new("兔化"), Attention = new("关注"), Human = new("人类信念"),
        Paper = new("虚线纸"), Map = new("完整地图"), Blood = new("异常食品"), Command = new("动作"),
        Light = new("灯光"), Shadow = new("树荫"), Calm = new("言灵安静"), Role = new("岗位"),
        Minutes = new("分钟"), Distance = new("路长"), Open = new("开放"), Mana = new("言灵次数"),
        Symptoms = new("症状分钟"), Escape = new("离园"), RabbitEars = new("兔耳饰"), Toy = new("兔玩偶"),
        Age = new("年龄"), Roaring = new("吼叫"), LionCount = new("白狮数量"), Laugh = new("笑声"), Mixed = new("猿猴混处"),
        StaffPresent = new("员工在场"), Danger = new("危险征兆"), Food = new("食品可见"), Split = new("双路"), Anomaly = new("象异常"),
        Clock = new("时刻"), Lamp = new("客房灯"), Power = new("办公室供电"), Confidence = new("理解程度");
    public static readonly Tag[] All = [new("普通离园资格"),new("海馆离园资格"),Health,Rabbit,Attention,Human,Paper,Map,Blood,Command,Light,Shadow,Calm,Role,Minutes,Distance,Open,Mana,Symptoms,Escape,RabbitEars,Toy,Age,Roaring,LionCount,Laugh,Mixed,StaffPresent,Danger,Food,Split,Anomaly,Clock,Lamp,Power,Confidence];
    public static readonly RelationTag Leads = new("通往"), From = new("起点");
    public static readonly RelationTag[] Relations = [Leads,From];
    public static int Value(this Essence e, Tag t) => e.Get<StateProperty>()?.Values.GetValueOrDefault(t) ?? 0;
    public static int Value(this Projection p, Tag t) => p.Tags.GetValueOrDefault(t);
    public static void Set(this Essence e, Tag t, int n, bool visible = true)
    {
        var state = e.Get<StateProperty>() ?? e.Add(new StateProperty());
        state.Values[t] = n;
        if (visible) state.Observable.Add(t);
    }
}

// 真身、心智、它的判定分别保存；没有由心智反查本质的接口。
public sealed class Person(Essence body, SoulEssence soul, Projection self, Job job, double arrival)
{
    public Essence Body { get; } = body;
    public SoulEssence Soul { get; } = soul;
    public Projection Self { get; } = self;
    public Job Job { get; set; } = job;
    public Species TrueSpecies { get; set; } = Species.Human;
    public Species? ItsClassification { get; set; } = Species.Human;
    public string DutyRoom { get; } = body.Get<SpatialProperty>()!.Space!.TrueName;
    public double Arrival { get; } = arrival;
    public double LastExposure { get; set; } = arrival;
    public double AquariumEntered { get; set; } = -1;
    public double SymptomSince { get; set; } = -1;
    public double IsolationUntil { get; set; }
    public double NextShelf { get; set; }
    public double NextFeed { get; set; }
    public double NextTrim { get; set; }
    public double NextLionCheck { get; set; }
    public bool Resigned { get; set; }
    public bool Discharged { get; set; }
    public string Outcome { get; set; } = "在园";
    public string CurrentAction { get; set; } = "观察";
    public ActionExecution? Execution { get; set; }
    public Dictionary<string, RememberedRule> Books { get; } = [];
    public Dictionary<string, int> Visits { get; } = [];
    public List<string> DecisionTrace { get; } = [];
    public Essence? Room => Body.Get<SpatialProperty>()?.Space;
}

public sealed class ZooSimulation : IDisposable
{
    public ZooSimulation(int seed = 7, int visitors = 120, double startMinute = 480, string preset = "standard")
    { World = new ZooWorld(seed, visitors, startMinute, preset); }
    public ZooWorld World { get; }
    public ZooRules Rules => World.Rules;
    public ZooAnomalies Anomalies => World.Anomalies;
    public void Dispose() => World.Dispose();
}



