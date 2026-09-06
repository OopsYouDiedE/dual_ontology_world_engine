using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Timeline;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Examples.ZooBaseline;

/// <summary>普通动物园的暂定基线场景，不包含任何规则怪谈内容。</summary>
public sealed class ZooBaseline : IDisposable
{
    private readonly Random _random;
    private readonly List<Character> _visitors = [];
    private readonly List<Character> _staff = [];
    private readonly Entity[] _exhibits;
    private readonly Dictionary<Entity, double> _staffFed = new();
    private int _nextVisitorNumber;
    private bool _started;

    public Entity World { get; }
    public SimulationSession Session { get; }
    public TopologyGraph Topology => Session.Topology;
    public Entity Entrance { get; }
    public Entity Walkway { get; }
    public Entity RabbitArea { get; }
    public Entity PrimateArea { get; }
    public Entity ElephantArea { get; }
    public Entity LionArea { get; }
    public Entity RestPoint { get; }
    public Entity DrinkShop { get; }
    public Entity StaffSupply { get; }
    public Entity Exit { get; }
    public IReadOnlyList<Character> Visitors => _visitors;
    public IReadOnlyList<Character> Staff => _staff;
    public IReadOnlyList<Entity> Animals { get; }
    public IReadOnlyList<Entity> Exhibits => _exhibits;
    public Entity FoodSupply { get; }

    public ZooBaseline(int seed = 1, int visitorCount = 3)
    {
        if (visitorCount < 1) throw new ArgumentOutOfRangeException(nameof(visitorCount));
        _random = new Random(seed);
        World = new Entity("普通动物园");
        Session = new SimulationSession(World);
        Session.Runtime.DeclarativeOnly = true;

        Entrance = Scene("入口");
        Walkway = Scene("游览通路");
        RabbitArea = Scene("兔区");
        PrimateArea = Scene("猿类园区");
        ElephantArea = Scene("大象区");
        LionArea = Scene("狮区");
        RestPoint = Scene("休息点");
        DrinkShop = Scene("饮料店");
        StaffSupply = Scene("员工补给区");
        Exit = Scene("出口");
        Connect(Entrance, Walkway, 1);
        Connect(Walkway, RabbitArea, 2);
        Connect(Walkway, PrimateArea, 2);
        Connect(Walkway, ElephantArea, 3);
        Connect(Walkway, LionArea, 3);
        Connect(Walkway, RestPoint, 1);
        Connect(Walkway, DrinkShop, 1);
        Connect(StaffSupply, Walkway, 1);
        Connect(Walkway, Exit, 1);

        _exhibits = [RabbitArea, PrimateArea, ElephantArea, LionArea];

        var rabbit = Animal("兔子", RabbitArea);
        var primate = Animal("猿类", PrimateArea);
        var elephant = Animal("大象", ElephantArea);
        var lions = Enumerable.Range(1, 4).Select(i => Animal($"白狮{i}", LionArea)).ToArray();
        Animals = [rabbit, primate, elephant, .. lions];
        FoodSupply = new Entity("饲料库存", StaffSupply, properties: new Dictionary<string, object>
        {
            ["food"] = 18d, ["public_fields"] = new[] { "food", "$parent" }
        });

        AddVisitors(visitorCount);
        AddStaff();
    }

    public void Start()
    {
        foreach (var visitor in _visitors)
            ActivateVisitor(visitor);
        foreach (var staff in _staff)
            Session.Activate(staff, staff.Mind!, utility: StaffUtility,
                chooser: decisions => Choose(decisions), completed: OnStaffCompleted);
        _started = true;
    }

    /// <summary>向同一世界加入一批游客；已启动场景会立即注册其行动槽。</summary>
    public void AddVisitors(int count)
    {
        for (var i = 0; i < count; i++)
        {
            AddVisitor(++_nextVisitorNumber);
            if (_started) ActivateVisitor(_visitors[^1]);
        }
    }

    /// <summary>供扩展场景替换或接管游客的自主行动槽。</summary>
    public void ActivateVisitor(Character visitor) => Session.Activate(visitor, visitor.Mind!, utility: VisitorUtility,
        chooser: decisions => Choose(decisions),
        completed: (_, _) => { if (ReferenceEquals(visitor.CurrentScene, Exit)) Session.Deactivate(visitor); });

    public int Run(double until = 180, int maxSteps = 2000) => Session.Timeline.RunUntil(until, maxSteps);

    public IReadOnlyList<Entity> CausalTrace => Session.Runtime.Events;

    public void Dispose() => Session.Dispose();

    private Entity Scene(string name) => new(name, World, new Dictionary<string, int> { ["Scene"] = 1 });

    private void Connect(Entity from, Entity to, double cost) => Topology.Connect($"{from.Name}-{to.Name}", from, to, cost);

    private static Entity Animal(string name, Entity area) => new(name, area, properties: new Dictionary<string, object>
    {
        ["fed_count"] = 0d,
        ["public_fields"] = new[] { "fed_count", "$parent" }
    });

    private void AddVisitor(int index)
    {
        var visitor = new Character($"游客{index}", Entrance);
        var mind = new Soul($"游客{index}的心智");
        visitor.Bind(mind);
        var preferred = _exhibits[_random.Next(_exhibits.Length)];
        visitor.Set("preferred_exhibit", preferred);
        visitor.Set("public_fields", new[] { "preferred_exhibit", "$parent" });
        mind.InstallStep((_, actor, _, _) => VisitorOffers(visitor));
        _visitors.Add(visitor);
    }

    private void AddStaff()
    {
        var staff = new Character("蓝衣普通员工", StaffSupply);
        var mind = new Soul("蓝衣普通员工的心智");
        staff.Bind(mind);
        staff.Set("uniform", "蓝色工作服");
        staff.Set("carried_food", 0d);
        staff.Set("public_fields", new[] { "$parent", "uniform", "carried_food" });
        foreach (var animal in Animals) _staffFed[animal] = 0d;
        mind.InstallStep((_, actor, _, _) => StaffOffers(staff));
        _staff.Add(staff);
    }

    private IEnumerable<Intent> VisitorOffers(Character visitor)
    {
        var mind = visitor.Mind!;
        var current = visitor.CurrentScene;
        if (current == null || ReferenceEquals(current, Exit)) yield break;
        var visited = visitor.Related("visited").Select(f => f.Value).OfType<Entity>().ToArray();
        foreach (var edge in Topology.Adjacency[current])
        {
            var next = edge.Next;
            if (ReferenceEquals(next, Exit) && visited.Length < 4) continue;
            if (next is not null && (ReferenceEquals(next, Walkway) || ReferenceEquals(next, Exit) || _exhibits.Contains(next) && !visited.Contains(next)))
            {
                var changes = new List<Effect> { F("move", visitor, next) };
                yield return new Intent(visitor, mind, F("move", visitor, next), changes,
                    edge.Passage.CostFrom(current), $"游客前往{next.Name}",
                    []);
            }
        }
        if (_exhibits.Contains(current) && !visited.Contains(current))
            yield return new Intent(visitor, mind, F("watch", visitor, current),
                [F("relate", visitor, "visited", current)], 2, $"游客观赏{current.Name}");
        if (ReferenceEquals(current, Walkway))
            foreach (var edge in Topology.Adjacency[current].Where(e => ReferenceEquals(e.Next, RestPoint)))
                yield return new Intent(visitor, mind, F("move", visitor, RestPoint), [F("move", visitor, RestPoint)],
                    edge.Passage.CostFrom(current), "游客前往休息点");
    }

    private IEnumerable<Intent> StaffOffers(Character staff)
    {
        var mind = staff.Mind!;
        var current = staff.CurrentScene;
        if (current == null) yield break;
        var carried = staff.Get<double>("carried_food");
        if (ReferenceEquals(current, StaffSupply) && carried <= 0)
        {
            var food = FoodSupply.Get<double>("food");
            if (food <= 0) yield break;
            yield return new Intent(staff, mind, F("change", staff, "carried_food", 1d),
                [F("spend", FoodSupply, "food", 1d), F("change", staff, "carried_food", 1d)],
                1, "员工领取一份饲料", [E("gte", E("value", FoodSupply, "food"), 1d)]);
            yield break;
        }
        if (carried <= 0 && !ReferenceEquals(current, StaffSupply))
        {
            var supplyPath = Topology.FindPath(current, StaffSupply);
            if (supplyPath is { Count: > 0 })
            {
                var next = supplyPath[0].Other(current)!;
                yield return new Intent(staff, mind, F("move", staff, next), [F("move", staff, next)],
                    supplyPath[0].CostFrom(current), $"饲养员返回{next.Name}");
            }
            yield break;
        }
        var minimumFed = _staffFed.Values.Min();
        foreach (var animal in Animals)
        {
            var area = animal.Parent as Entity;
            if (area == null) continue;
            if (_staffFed[animal] > minimumFed) continue;
            if (ReferenceEquals(current, area) && carried > 0)
            {
                yield return new Intent(staff, mind, F("change", animal, "fed_count", 1d),
                    [F("spend", staff, "carried_food", 1d), F("change", animal, "fed_count", 1d)],
                    2, $"饲养员喂养{animal.Name}", [E("gte", E("value", staff, "carried_food"), 1d)]);
            }
            else
            {
                var path = Topology.FindPath(current, area);
                if (path is { Count: > 0 })
                {
                    var next = path[0].Other(current)!;
                    yield return new Intent(staff, mind, F("move", staff, next), [F("move", staff, next)],
                        path[0].CostFrom(current), $"饲养员前往{next.Name}");
                    break;
                }
            }
        }
    }

    private double VisitorUtility(Intent action)
    {
        var target = action.Target;
        if (ReferenceEquals(target, Exit)) return 100;
        if (target != null && _exhibits.Contains(target))
            return ReferenceEquals(target, action.Actor.Get<Entity>("preferred_exhibit")) ? 35 : 25;
        if (ReferenceEquals(target, RestPoint)) return 4;
        return 1;
    }

    private double StaffUtility(Intent action) => action.Effect.Op == "change" ? 100 : 2;

    private void OnStaffCompleted(Intent? intent, ActionResolution? resolution)
    {
        if (intent?.Effect.Op == "change" && resolution?.Result?.Succeeded == true && intent.Target is Entity animal && _staffFed.ContainsKey(animal))
            _staffFed[animal]++;
        var staff = _staff.FirstOrDefault();
        if (staff != null && staff.Get<double>("carried_food") <= 0 && ReferenceEquals(staff.CurrentScene, StaffSupply) && FoodSupply.Get<double>("food") <= 0)
            Session.Deactivate(staff);
    }

    private Intent Choose(IReadOnlyList<Decision> decisions)
    {
        var best = decisions.Max(d => d.Score);
        var choices = decisions.Where(d => Math.Abs(d.Score - best) < 0.0001 && d.Action != null).Select(d => d.Action!).ToArray();
        return choices[_random.Next(choices.Length)];
    }

}

internal static class Program
{
    public static void Main(string[] args)
    {
        var control = args.Contains("--control");
        var trace = args.Contains("--trace");
        var seed = IntOption(args, "--seed", 1);
        var batches = IntOption(args, "--batches", 1);
        var visitors = IntOption(args, "--visitors", 3);
        if (batches < 1 || visitors < 1) throw new ArgumentOutOfRangeException("batches/visitors");
        using var zoo = new ZooBaseline(seed, visitors);
        using var anomalies = new ZooAnomalies(zoo, new ZooAnomalies.Settings { Enable = !control });
        zoo.Start();
        for (var batch = 0; batch < batches; batch++)
        {
            if (batch > 0) zoo.AddVisitors(visitors);
            zoo.Run(until: zoo.Session.Timeline.Now + 180);
        }
        Console.WriteLine($"mode={(control ? "control" : "anomaly")} seed={seed} batches={batches} visitors={zoo.Visitors.Count} events={zoo.CausalTrace.Count} trace={anomalies.Trace.Count} exited={zoo.Visitors.Count(v => ReferenceEquals(v.CurrentScene, zoo.Exit))} food={zoo.FoodSupply.Get<double>("food")} carry={string.Join(',', zoo.Staff.Select(s => s.Get<double>("carried_food")))} fed={zoo.Animals.Sum(a => a.Get<double>("fed_count"))}");
        if (trace)
            foreach (var item in anomalies.Trace)
            {
                var op = item.TriggerEvent?.Effect()?.Op ?? "unknown";
                Console.WriteLine($"t={item.Time:0.##} phase={item.Phase} actor={item.Actor.Name} scene={item.Scene.Name} trigger={op} outcome={item.Outcome}");
            }
    }

    private static int IntOption(string[] args, string name, int fallback)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value) ? value : fallback;
    }
}
