using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Timeline;

namespace DualOntologyWorldEngine.Examples.ZooSimulator;

/// <summary>动物园规则怪谈世界实体、拓扑、场景与初始状态配置。</summary>
public sealed partial class ZooWorld : IDisposable
{
    private readonly Random _random;
    public Random Random => _random;
    private readonly List<Character> _visitors = [];
    private int _nextVisitorIndex;
    public int NextVisitorIndex => ++_nextVisitorIndex;
    private bool _disposed;

    public Entity World { get; }
    public SimulationSession Session { get; }
    public TopologyGraph Topology => Session.Topology;

    // 园区常规场景
    public Entity Entrance { get; }
    public Entity Walkway { get; }
    public Entity RabbitArea { get; }
    public Entity RabbitBushes { get; }
    public Entity PrimateArea { get; }
    public Entity LeftStreet { get; }
    public Entity RightStreet { get; }
    public Entity RightStreetExit { get; }
    public Entity ElephantArea { get; }
    public Entity ElephantDisplay { get; }
    public Entity ElephantLawn { get; }
    public Entity ElephantSign { get; }
    public Entity SecurityOffice { get; }
    public Entity LionArea { get; }
    public Entity LionRockery { get; }
    public Entity LionStaffPassage { get; }
    public Entity DrinkShop { get; }
    public Entity DrinkShelf { get; }
    public Entity RestPoint { get; }
    public Entity StaffSupply { get; }
    public Entity DirectorOffice { get; }
    public Entity RegularExit { get; }

    // 重叠空间：海洋馆场景
    public Entity AquariumGate { get; }
    public Entity AquariumLobby { get; }
    public Entity AquariumFoodShelf { get; }
    public Entity WhaleArea { get; }
    public Entity WhaleProjection { get; }
    public Entity StorageBox { get; }
    public Entity JellyfishArea { get; }
    public Entity JellyfishGuestRoom { get; }
    public Entity NightLight { get; }

    // 异常对象与暗影实体
    public Entity TheShadow { get; }

    // 实体动物
    public Entity Rabbits { get; }
    public Entity EscapedRabbit { get; }
    public Entity Primates { get; }
    public Entity NormalElephant { get; }
    public Entity RabbitEaredElephant { get; }
    public IReadOnlyList<Entity> Lions { get; }
    public Entity FifthLion { get; }
    public Entity Jellyfish { get; }

    // 园区物资、工具与纪念品
    public Entity FoodSupply { get; }
    public Entity Shears { get; }
    public Entity RabbitBlood { get; }
    public Entity NormalDrinks { get; }
    public Entity GoatMeat { get; }
    public Entity AquariumFood { get; }
    public Entity RabbitPlush { get; }
    public Entity Taser { get; }
    public Entity SpareMaps { get; }

    // 工作人员与NPC角色
    public Character BlueStaff { get; }
    public Character SecurityGuard { get; }
    public Character RedStaff { get; }
    public Character Director { get; }
    public Character BlackInfected { get; }
    public IReadOnlyList<Character> Visitors => _visitors;

    public IReadOnlyList<Entity> Exhibits { get; }

    public ZooWorld(int seed = 42, int visitorCount = 4, double startMinute = 540)
    {
        _random = new Random(seed);
        EnvironmentRandom = new Random(seed ^ 0x5a17);
        AnimalRandom = new Random(seed ^ 0x3471);
        StartMinute = startMinute;
        World = new Entity("动物园规则世界");
        Session = new SimulationSession(World);
        Session.Runtime.DeclarativeOnly = true;

        // 1. 初始化常规场景
        Entrance = Scene("大门入口");
        Walkway = Scene("主步道");
        RabbitArea = Scene("兔区");
        RabbitBushes = Scene("兔区树荫灌木", tags: new Dictionary<string, int> { ["vegetation"] = 1, ["overgrown"] = 1 }, properties: new Dictionary<string, object>
        {
            ["shaded"] = true,
            ["overgrown"] = true,
            ["is_shaded_bush"] = true,
            ["anomaly_resonance"] = 1.2
        });
        PrimateArea = Scene("猿类园区", properties: new Dictionary<string, object>
        {
            ["has_double_street"] = true
        });
        LeftStreet = Scene("猿区左街");
        RightStreet = Scene("猿区右街", properties: new Dictionary<string, object>
        {
            ["anomalous"] = true,
            ["anomaly_resonance"] = 2.0
        });
        RightStreetExit = Scene("右街神秘出口");

        ElephantArea = Scene("大象区", properties: new Dictionary<string, object>
        {
            ["is_elephant_area"] = true
        });
        ElephantDisplay = Scene("大象展位");
        ElephantLawn = Scene("大象草坪", properties: new Dictionary<string, object>
        {
            ["has_dotted_paper"] = false
        });
        SecurityOffice = Scene("保安室");

        LionArea = Scene("狮区", properties: new Dictionary<string, object>
        {
        });
        LionRockery = Scene("白狮假山", properties: new Dictionary<string, object>
        {
            ["predator_pressure"] = 0.0
        });
        LionStaffPassage = Scene("狮区工作通道");

        DrinkShop = Scene("饮料店");
        RestPoint = Scene("休息点", tags: new Dictionary<string, int> { ["enclosed"] = 1, ["dark"] = 1 }, properties: new Dictionary<string, object>
        {
            ["enclosed"] = true,
            ["dark"] = true,
            ["door_closed"] = true,
            ["anomaly_resonance"] = 2.0,
        });
        StaffSupply = Scene("员工补给区");
        DirectorOffice = Scene("园长办公室", properties: new Dictionary<string, object>
        {
            ["lights_on"] = true,
            ["is_night_time"] = false
        });
        RegularExit = Scene("正规出口");

        // 2. 初始化重叠空间：海洋馆
        AquariumGate = Scene("海洋馆大门", properties: new Dictionary<string, object>
        {
            ["is_aquarium"] = true
        });
        AquariumLobby = Scene("海洋馆大厅", properties: new Dictionary<string, object>
        {
            ["is_aquarium"] = true
        });
        WhaleArea = Scene("鲸鱼区", properties: new Dictionary<string, object>
        {
            ["is_whale_area"] = true
        });
        JellyfishArea = Scene("水母展区", properties: new Dictionary<string, object>
        {
        });
        JellyfishGuestRoom = Scene("水母客房", properties: new Dictionary<string, object>
        {
            ["is_guest_room"] = true
        });

        // 3. 构建拓扑通道连接（以现实园区步行分钟尺度配置）
        Connect(Entrance, Walkway, 8.0);
        Connect(Walkway, RabbitArea, 12.0);
        Connect(RabbitArea, RabbitBushes, 6.0);
        Connect(Walkway, PrimateArea, 12.0);
        Connect(PrimateArea, LeftStreet, 8.0);
        Connect(PrimateArea, RightStreet, 10.0);
        Connect(RightStreet, RightStreetExit, 8.0);

        Connect(Walkway, ElephantArea, 15.0);
        Connect(ElephantArea, ElephantDisplay, 6.0);
        Connect(ElephantArea, ElephantLawn, 6.0);
        Connect(ElephantArea, SecurityOffice, 6.0);

        Connect(Walkway, LionArea, 15.0);
        Connect(LionArea, LionRockery, 6.0);
        Connect(LionArea, LionStaffPassage, 6.0);

        Connect(Walkway, DrinkShop, 6.0);
        Connect(Walkway, RestPoint, 8.0);
        Connect(Walkway, StaffSupply, 8.0);
        Connect(Walkway, DirectorOffice, 12.0);
        Connect(Walkway, RegularExit, 10.0);

        // 重叠空间通道
        Connect(Walkway, AquariumGate, 15.0);
        Connect(AquariumGate, AquariumLobby, 8.0);
        Connect(AquariumLobby, WhaleArea, 8.0);
        Connect(AquariumLobby, JellyfishArea, 8.0);
        Connect(JellyfishArea, JellyfishGuestRoom, 6.0);

        Exhibits = [RabbitArea, PrimateArea, ElephantArea, LionArea];

        // 场景职能与辖域属性标记（消除心智决策中硬编码的 HashSet 场景白名单）
        foreach (var s in new[] { Walkway, RabbitArea, ElephantArea, DrinkShop, StaffSupply, RestPoint, DirectorOffice })
            s.Set("patrol_blue", true);

        foreach (var s in new[] { ElephantArea, ElephantDisplay, ElephantLawn, SecurityOffice })
        {
            s.Set("patrol_guard", true);
            s.Set("domain_elephant", true);
        }

        foreach (var s in new[] { AquariumLobby, WhaleArea, JellyfishArea, JellyfishGuestRoom })
        {
            s.Set("patrol_red", true);
            s.Set("domain_aquarium", true);
        }

        foreach (var s in new[] { DirectorOffice, Walkway, LionArea, ElephantArea, RabbitArea, RestPoint, StaffSupply })
            s.Set("patrol_director", true);

        foreach (var s in new[] { LionRockery, LionArea, LionStaffPassage })
            s.Set("domain_lion", true);

        // 4. 初始化场景陈设、标示与库存实体
        ElephantSign = Item("大象标示牌", ElephantArea, new()
        {
            ["description"] = "大象是一种体型巨大、有着扇子一般耳朵、身子很长、腿粗得像柱子的生物，非白色。",
        });

        DrinkShelf = Item("冷柜货架", DrinkShop);
        NormalDrinks = Item("普通苏打水", DrinkShelf, new()
        {
            ["is_drink"] = true,
            ["is_consumable"] = true,
            ["count"] = 10d
        });
        RabbitBlood = Item("兔子血", DrinkShelf, properties: new()
        {
            ["is_rabbit_blood"] = true,
            ["is_drink"] = true,
            ["is_consumable"] = true,
            ["is_anomalous_flesh"] = true,
            ["anomaly_resonance"] = 0.05,
            ["count"] = 2d
        }, tags: new Dictionary<string, int> { ["anomaly_carrier"] = 1 });

        AquariumFoodShelf = Item("海洋馆食品货架", AquariumLobby);
        AquariumFood = Item("免费饼干", AquariumFoodShelf, properties: new()
        {
            ["is_food"] = true,
            ["is_consumable"] = true,
            ["count"] = 10d
        });
        GoatMeat = Item("山羊肉", AquariumFoodShelf, properties: new()
        {
            ["is_goat_meat"] = true,
            ["is_food"] = true,
            ["is_consumable"] = true,
            ["is_anomalous_flesh"] = true,
            ["anomaly_resonance"] = 0.05,
            ["count"] = 3d
        }, tags: new Dictionary<string, int> { ["anomaly_carrier"] = 1 });

        StorageBox = Item("收纳箱", WhaleArea, properties: new()
        {
            ["capacity"] = 20
        });
        WhaleProjection = Item("3D溺亡大象投影", WhaleArea, properties: new()
        {
            ["is_projection"] = true,
            ["anomaly_resonance"] = 1.5
        }, tags: new Dictionary<string, int> { ["cognitive_hazard"] = 1 });

        NightLight = Item("水母小夜灯", JellyfishGuestRoom, new()
        {
            ["lit"] = true,
            ["battery"] = 10d,
            ["beacon_strength"] = 2.0
        });
        Taser = Item("电击棒与麻醉枪", JellyfishArea, new()
        {
            ["is_weapon"] = true
        });

        RabbitPlush = Item("兔子玩偶", DrinkShop, new()
        {
            ["is_toy"] = true,
            ["is_rabbit_toy"] = true,
            ["beacon_strength"] = 1.5
        });

        FoodSupply = Item("饲料库存", StaffSupply, new()
        {
            ["food"] = 30d,
            ["public_fields"] = new[] { "food", "$parent" }
        });
        Shears = Item("修枝剪", StaffSupply, new()
        {
            ["is_tool"] = true
        });
        SpareMaps = Item("备用虚线地图", DirectorOffice, new()
        {
            ["count"] = 5d
        });

        TheShadow = Item("“它”", World, new()
        {
            ["is_anomaly_source"] = true,
            ["anomaly_resonance"] = 2.0,
            ["active"] = true
        });

        // 5. 初始化动物实体
        Rabbits = Item("兔子群", RabbitArea, new()
        {
            ["fed_count"] = 0d,
            ["days_since_feed"] = 0d,
            ["is_animal"] = true,
            ["is_rabbit"] = true
        });
        EscapedRabbit = Item("出逃的兔子", RabbitBushes, new()
        {
            ["is_escaped_rabbit"] = true,
            ["is_animal"] = true,
            ["anomaly_resonance"] = 2.0
        });
        Primates = Item("猿类群", PrimateArea, new()
        {
            ["is_animal"] = true,
            ["fed_count"] = 0d
        });
        NormalElephant = Item("真实大象", ElephantDisplay, new()
        {
            ["is_animal"] = true,
            ["is_elephant"] = true,
            ["is_white"] = false,
            ["fed_count"] = 0d
        });
        RabbitEaredElephant = Item("兔耳白象", ElephantDisplay, properties: new()
        {
            ["is_anomaly_elephant"] = true,
            ["is_animal"] = true,
            ["anomaly_resonance"] = 2.0
        }, tags: new Dictionary<string, int> { ["cognitive_hazard"] = 1 });

        var lions = Enumerable.Range(1, 4).Select(i => Item($"白狮{i}", LionRockery, new()
        {
            ["is_white_lion"] = true,
            ["is_animal"] = true,
            ["fed_count"] = 0d
        })).ToArray();
        Lions = lions;
        FifthLion = Item("伪第五白狮", LionRockery, properties: new()
        {
            ["is_fake_lion"] = true,
            ["is_animal"] = true,
            ["anomaly_resonance"] = 2.5
        }, tags: new Dictionary<string, int> { ["cognitive_hazard"] = 1 });

        Jellyfish = Item("发光水母", JellyfishArea, new()
        {
            ["luminous"] = true
        });

        // 6. 初始化工作人员与NPC
        BlueStaff = CreateCharacter("蓝衣员工", StaffSupply, "蓝色工作服", hasMap: true);
        BlueStaff.Set("carried_food", 2d);
        BlueStaff.Set("duty", "巡视投喂与维护");
        Item("异常收缴箱", BlueStaff, new() { ["containment"] = true });

        SecurityGuard = CreateCharacter("大象区保安", SecurityOffice, "保安制服", hasMap: true);
        SecurityGuard.Set("duty", "驻守大象区并执行庇护");
        SecurityGuard.Set("stability", 15.0);

        RedStaff = CreateCharacter("红衣员工", JellyfishArea, "红色工作服", hasMap: false);
        RedStaff.Set("duty", "夜间海洋馆管理与水母灯管制");
        RedStaff.Set("stability", 10.0);

        Director = CreateCharacter("园长", DirectorOffice, "园长便服", hasMap: true);
        Director.Set("duty", "园区统筹与白狮审查");
        Director.Set("stability", 12.0);

        BlackInfected = CreateCharacter("黑衣感染者", RightStreet, "黑色工作服", hasMap: false);
        BlackInfected.Set("erosion", 6.5);
        BlackInfected.Set("anomaly_resonance", 1.0);

        InitializeProcesses();
        InitializeCognition();
        foreach (var person in People) InitializeMotives(person);

        // 8. 初始化游客群体
        AddVisitors(visitorCount);
    }

    public Entity Scene(string name, IDictionary<string, int>? tags = null, Dictionary<string, object>? properties = null)
    {
        var sceneTags = new Dictionary<string, int> { ["Scene"] = 1 };
        if (tags != null) foreach (var (k, v) in tags) sceneTags[k] = v;
        return new Entity(name, World, sceneTags, properties: properties);
    }

    public static Entity Item(string name, TreeMember? parent, Dictionary<string, object>? properties = null, IDictionary<string, int>? tags = null) =>
        new(name, parent, tags: tags, properties: properties);

    public Passage Connect(Entity from, Entity to, double cost, Entity? gate = null, Func<Entity, Entity?, object?, bool>? condition = null) =>
        Topology.Connect($"{from.Name}-{to.Name}", from, to, cost, gate: gate, condition: condition);

    public Character SpawnVisitor(string name, Entity scene, string uniform = "普通便服", bool hasMap = true,
        double thirst = 0.5, double fear = 0.0, double fatigue = 0.0, Entity? preferred = null)
    {
        var visitor = CreateCharacter(name, scene, uniform, hasMap);
        visitor.Set("thirst", thirst);
        visitor.Set("fear", fear);
        visitor.Set("fatigue", fatigue);
        if (preferred != null) visitor.Set("preferred_exhibit", preferred);
        visitor.Set("role", "visitor");
        InitializeMotives(visitor);
        visitor.Set("age", 28d);
        _visitors.Add(visitor);
        return visitor;
    }

    public void AddVisitors(int count, Entity? startScene = null)
    {
        for (var i = 0; i < count; i++)
        {
            var index = ++_nextVisitorIndex;
            var age = index switch
            {
                1 => 28,
                2 => 8,
                3 => 15,
                _ => _random.Next(18, 55)
            };
            var hasChild = age >= 25 && index == 1;
            var preferred = Exhibits[_random.Next(Exhibits.Count)];
            var visitor = SpawnVisitor($"游客{index}", startScene ?? Entrance, "普通便服", hasMap: true,
                thirst: 0.5, preferred: preferred);
            visitor.Set("age", (double)age);
            visitor.SetTag("age", age);
            if (age >= 18) visitor.SetTag("Adult", 1);
            visitor.Set("has_child", hasChild);
            visitor.Set("carrying_rabbit_toy", false);
            visitor.Set("stability", 10.0);
            if (_visitors.Count > 1) visitor.Set("companion", _visitors[0]);
        }
    }

    private Character CreateCharacter(string name, Entity scene, string uniform, bool hasMap)
    {
        var character = new Character(name, scene);
        var soul = new Soul($"{name}的心智");
        character.Bind(soul);
        character.Set("uniform", uniform);
        character.Set("has_map", hasMap);
        character.Set("thirst", 0d);
        character.Set("fatigue", 0d);
        character.Set("fear", 0d);
        character.Set("erosion", 0d);
        character.Set("attention", 0d);
        character.Set("stability", 10d);
        character.Set("health", 10d);
        character.Set("identity", "human");
        character.Set("eye_count", 2d);
        character.Set("eye_layout", "horizontal");
        character.Set("identity_strength", 1d);
        character.Set("public_fields", new[] { "$parent", "uniform", "has_map" });

        foreach (var passage in Topology.Passages)
        {
            if (!IsAquarium(passage.Left) && !IsAquarium(passage.Right)) soul.LearnPassage(passage);
        }
        character.Set("stamina", 10d);
        character.Set("money", 100d);
        character.Set("recorded_at", -4320d);
        character.Set("counted_at", -4320d);
        character.Set("rabbit_fed_at", -10080d);
        character.Set("pruned_at", 0d);
        character.Set("restocked_at", -1440d);
        character.Set("charged_at", -10080d);
        character.Set("carried_food", 2d);
        if (hasMap)
        {
            var paper = Item("地图虚线页", character, new()
            {
                ["owner"] = character,
                ["detached"] = false,
                ["evidence"] = "地图标出了常规园区的道路与正规出口，纸边留有可撕下的虚线。"
            });
            character.Set("paper", paper);
            Papers.Add(paper);
        }
        return character;
    }

    public int Run(double until = 180, int maxSteps = 2000000) => Session.Timeline.RunUntil(until, maxSteps);

    public void Dispose()
    {
        if (_disposed) return;
        Narrative.Dispose();
        Session.Runtime.Executed -= ConfigureSocialOutcome;
        Session.Timeline.Cancel(task => task.Action == _settleEmotions);
        Session.Dispose();
        _disposed = true;
    }
}
