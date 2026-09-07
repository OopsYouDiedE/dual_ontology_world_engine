using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;

namespace DualOntologyWorldEngine.Examples.ZooSimulator;

public sealed partial class ZooWorld
{
    // 一个行动值对应一分钟；时间表是游戏层设定，不进入核心引擎。
    public double StartMinute { get; }
    public Random EnvironmentRandom { get; private set; } = null!;
    public Random AnimalRandom { get; private set; } = null!;
    public double Minute => (StartMinute + Session.Timeline.Now) % 1440;
    public int Day => (int)((StartMinute + Session.Timeline.Now) / 1440);
    public IEnumerable<Character> People => Visitors.Concat([BlueStaff, SecurityGuard, RedStaff, Director, BlackInfected]);
    public IEnumerable<Entity> Scenes => World.Children.Keys.OfType<Entity>().Where(e => e.HasTag("Scene"));
    public Entity Outside { get; private set; } = null!;
    public Entity OfficeLight { get; private set; } = null!;
    public Entity Camera { get; private set; } = null!;
    public Entity Computer { get; private set; } = null!;
    public Entity SofaToy { get; private set; } = null!;
    public Entity Bin { get; private set; } = null!;
    public Entity BlackUniform { get; private set; } = null!;
    public Entity Mirror { get; private set; } = null!;
    public Entity Pet { get; private set; } = null!;
    public Entity RabbitEars { get; private set; } = null!;
    public Entity Phone { get; private set; } = null!;
    public List<Entity> Papers { get; } = [];

    private void InitializeProcesses()
    {
        Outside = Scene("馆外员工住处");
        Connect(AquariumGate, Outside, 2);
        Connect(LeftStreet, Walkway, 2);
        Connect(LionStaffPassage, LionRockery, 1);
        Connect(LionArea, RegularExit, 3);
        foreach (var p in Topology.Passages)
            if (p.Left==PrimateArea && p.Right==RightStreet)
                p.Condition = (from,_,_) => from==RightStreet || PrimateArea.Get<bool>("has_double_street");
        foreach (var p in Topology.Passages)
        {
            var original=p.Condition;
            p.Condition=(from,actor,env)=>p.Other(from)?.Get<bool>("closed")!=true && (original?.Invoke(from,actor,env)??true);
        }
        OfficeLight = Item("办公室灯与供电", DirectorOffice, new() { ["lit"] = true, ["powered"] = true, ["is_light"] = true });
        Camera = Item("办公室监控器", DirectorOffice, new() { ["powered"] = true, ["is_device"] = true });
        Computer = Item("记录电脑·它", DirectorOffice, new() { ["is_record"] = true, ["last_record"] = -4320d });
        SofaToy = Item("猿猴玩偶", DirectorOffice, new() { ["is_toy"] = true });
        Bin = Item("垃圾桶", DirectorOffice, new() { ["is_container"] = true });
        Mirror = Item("镜子", DirectorOffice, new() { ["is_mirror"] = true });
        BlackUniform = Item("备用黑色制服", AquariumGate, new() { ["is_uniform"] = true, ["color"] = "黑色工作服" });
        Pet = Item("员工宠物", StaffSupply, new() { ["is_animal"] = true, ["is_pet"] = true });
        RabbitEars = Item("兔耳饰", BlackInfected, new() { ["is_rabbit_toy"] = true, ["is_toy"] = true, ["is_earring"] = true });
        Phone = Item("地图联络电话", Entrance, new() { ["is_phone"] = true });
        StorageBox.Set("containment", true);
        ElephantSign.Set("describes", "elephant");
        RabbitEaredElephant.Set("appearance", "elephant");
        FifthLion.Set("blood_appetite", 1d);
        NightLight.Set("is_light", true);
        NightLight.Set("powered", true);
        NightLight.Set("last_charge", -10080d);
        FoodSupply.Set("is_feed", true);
        FoodSupply.Set("count", 100d);
        Shears.Set("is_shears", true);
        RabbitPlush.Set("count", 1d);
        SpareMaps.Set("is_map_supply", true);
        Rabbits.Set("last_feed", -10080d);
        Rabbits.Set("health", 10d);
        RabbitBushes.Set("growth", 0.2);
        RabbitBushes.Set("shaded", false);
        RabbitBushes.Set("overgrown", false);
        RestPoint.Set("anomaly_resonance", 0d);
        RightStreet.Set("anomaly_resonance", 0d);
        PrimateArea.Set("has_double_street", false);
        // 异常对象是可复用实体，初始存于世界根部，尚未出现在园区。
        foreach (var e in new[] { EscapedRabbit, FifthLion, RabbitEaredElephant, RabbitBlood, GoatMeat, WhaleProjection })
        {
            e.Reparent(World);
            e.Set("active", false);
            e.Set("health", 6d);
        }
        foreach (var lion in Lions)
        {
            lion.Set("health", 12d);
            lion.Set("power", 2d);
        }
        TheShadow.Reparent(RabbitBushes);
        TheShadow.Set("power", 0.5);
        TheShadow.Set("health", 20d);
        BlackInfected.Set("role", "wanderer");
        BlackInfected.Set("offers_tickets", true);
        BlackInfected.Set("health", 10d);
        BlueStaff.Set("role", "staff");
        SecurityGuard.Set("role", "guard");
        RedStaff.Set("role", "red");
        Director.Set("role", "director");
        foreach (var person in People)
        {

            person.Set("calibrated", false);
        }
        foreach (var s in Scenes)
            s.Set("public_fields", new[] { "$parent", "closed_until", "shaded" });
        World.Set("budget", 1000d);
    }

    public static Entity? Place(Entity e) => e.HasTag("Scene") ? e : TopologySystem.GetEnclosingScene(e);
    public static bool Anchored(Character c, Entity target) => c.Related("calibrated")
        .Any(f=>f.Value is Entity reference && (reference==target ||
            reference.Get<string>("describes") is { } kind && kind==target.Get<string>("appearance")));
    public static Entity? HeldPaper(Character c) => c.Inventory.Where(e=>e.Get<Entity>("owner") is Character)
        .OrderByDescending(e=>e.Get<bool>("detached")).FirstOrDefault();
    public bool InEarshot(Entity listener, Entity? source) => source!=null &&
        (listener==source || Topology.Adjacency[listener].Any(edge=>edge.Next==source));
    public int VisibleLions(Character c) => Scenes.Where(s=>InEarshot(c.CurrentScene!,s))
        .SelectMany(s=>s.Children.Keys.OfType<Entity>())
        .Count(e=>(e.Get<bool>("is_white_lion") || e.Get<bool>("is_fake_lion")) && e.Get<double>("health")>0 && e.Get<object>("active") is not false);
    public bool IsAquarium(Entity? scene) => scene != null &&
        (scene.Get<bool>("domain_aquarium") || ReferenceEquals(scene, AquariumGate));
    public bool Alive(Character c) => c.CurrentScene != null && c.Get<double>("health") > 0;
    public bool Finished(Character c) => !Alive(c) || ReferenceEquals(c.CurrentScene, RegularExit) || ReferenceEquals(c.CurrentScene, RightStreetExit);
    public string Outcome(Character c) => c.Get<double>("health") <= 0 ? "死亡" : c.CurrentScene == null ? "失踪" :
        ReferenceEquals(c.CurrentScene, RegularExit) ? "离园" : ReferenceEquals(c.CurrentScene, RightStreetExit) ? "去向不明（右街边界）" :
        c.Get<double>("erosion") >= 10 ? "认知同化" : "仍在园内";

    public IEnumerable<Entity> Local(Character c)
    {
        if (c.CurrentScene is not { } scene) return [];
        // 不穿透别人的背包和封存箱。
        return scene.Children.Keys.OfType<Entity>().Where(e => !ReferenceEquals(e, c))
            .SelectMany(e => e is Character || e.Get<bool>("containment") ? new[] { e } :
                new[] { e }.Concat(e.Children.Keys.OfType<Entity>())).Concat(c.Inventory).Distinct();
    }

    public Entity? NextStep(Character c, Entity destination)
    {
        var goal = Place(destination);
        var start = c.CurrentScene;
        if (goal == null || start == null || ReferenceEquals(goal, start)) return null;
        var queue = new PriorityQueue<(Entity Scene, Entity? First), double>();
        var distances = new Dictionary<Entity, double> { [start] = 0 };
        queue.Enqueue((start, null), 0);
        while (queue.TryDequeue(out var node, out var distance))
        {
            if (distance > distances[node.Scene]) continue;
            if (ReferenceEquals(node.Scene, goal)) return node.First;
            foreach (var p in c.Mind!.KnownPassages)
            {
                if (p.Other(node.Scene) is not { } next || !p.Allows(node.Scene,c)) continue;
                var d = distance + p.CostFrom(node.Scene);
                if (distances.TryGetValue(next, out var old) && old <= d) continue;
                distances[next] = d;
                queue.Enqueue((next, node.First ?? next), d);
            }
        }
        return null;
    }
}
