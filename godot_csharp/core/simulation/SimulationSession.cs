using DualOntologyWorldEngine.Core.Kotodama;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;

namespace DualOntologyWorldEngine.Core.Simulation;

/// <summary>
/// 无表现层的模拟组合根。角色、玩家选择和世界过程共用 Runtime 的唯一时间线。
/// </summary>
public sealed class SimulationSession : IDisposable
{
    private readonly Dictionary<Entity, Func<double?>> _actors = new();
    private readonly Dictionary<Entity, double> _readyAt = new();
    private readonly HashSet<Func<double?>> _commands = new();
    public ActionRuntime Runtime { get; }
    public Timeline.Timeline Timeline => Runtime.Timeline;
    public Entity World { get; }
    public TopologyGraph Topology { get; }

    public SimulationSession(Entity world)
        : this(CreateRuntime(world), world) { }

    public SimulationSession(ActionRuntime runtime, Entity world, TopologyGraph? topology = null)
    {
        Runtime = runtime;
        World = world;
        Topology = topology ?? world.GetTopology() ?? new TopologyGraph();
        World.Set("topology", Topology);
        Runtime.Executed += Charge;
    }

    private static ActionRuntime CreateRuntime(Entity world)
    {
        var env = new RuleEnvironment();
        var rules = new RuleSystem();
        return new(new Timeline.Timeline(), env, rules, new KotodamaSystem(env, rules), world);
    }
    public double ReadyAt(Entity actor) => Math.Max(Timeline.Now, _readyAt.GetValueOrDefault(actor));

    private void Charge(Intent intent, ActionResolution result) =>
        _readyAt[intent.Actor] = ReadyAt(intent.Actor) + intent.MotionCost;

    /// <summary>激活或替换一个身体的行动槽。chooser 可用于玩家接管，原有 Soul.Step 插件继续有效。</summary>
    public void Activate(
        Entity actor, Soul soul, double delay = 0, double idleCost = 1,
        Func<Intent, double>? utility = null,
        Func<IReadOnlyList<Decision>, Intent>? chooser = null,
        Action<Intent?, ActionResolution?>? completed = null,
        ActionContext? context = null)
    {
        if (!double.IsFinite(idleCost) || idleCost <= 0)
            throw new ArgumentOutOfRangeException(nameof(idleCost), "空闲重试必须使时间前进。");
        Deactivate(actor);
        Func<double?>? turn = null;
        turn = () =>
        {
            if (ReadyAt(actor) > Timeline.Now) return ReadyAt(actor) - Timeline.Now;
            var intent = soul.Step(actor, Runtime.Rules, Runtime.Env, context, utility,
                allows: action => ReferenceEquals(action.Actor, actor) && ReferenceEquals(action.Mind, soul)
                    && (action.Changes != null
                        ? Runtime.Prepare(action, new StateView(soul)).Succeeded
                        : Runtime.Review(action, context).Allowed),
                chooser: chooser);
            var result = intent == null ? null : Runtime.ResolveDetailed(intent, context);
            completed?.Invoke(intent, result);
            // 完成回调允许移除或替换自己，不复活旧的行动槽。
            if (!_actors.TryGetValue(actor, out var current) || current != turn) return null;
            return Math.Max(ReadyAt(actor) - Timeline.Now, intent == null || result?.Allowed != true ? idleCost : 0);
        };
        _actors[actor] = turn;
        Timeline.Schedule(Math.Max(delay, ReadyAt(actor) - Timeline.Now), actor.Name, turn);
    }

    public void Activate(
        Entity actor, Soul soul, double delay, double idleCost,
        Func<Intent, double>? utility,
        Func<IReadOnlyList<Decision>, Intent>? chooser,
        Action<Intent?, ActionResolution?>? completed,
        IReadOnlyDictionary<string, object>? context) =>
        Activate(actor, soul, delay, idleCost, utility, chooser, completed, ActionContext.FromDictionary(context));

    public void Deactivate(Entity actor)
    {
        if (_actors.Remove(actor, out var turn)) Timeline.Cancel(task => task.Action == turn);
    }

    /// <summary>
    /// 排入外部玩家命令，返回取消函数。接管前用 Deactivate 停止自主槽；执行时重新检查当前世界。
    /// 同一身体的多条命令仍由唯一 Timeline 按登记顺序等待恢复时间。
    /// </summary>
    public Action Submit(Intent intent, Action<ActionResolution>? completed = null,
        IReadOnlyDictionary<string, object>? context = null)
    {
        Func<double?>? command = null;
        command = () =>
        {
            if (ReadyAt(intent.Actor) > Timeline.Now) return ReadyAt(intent.Actor) - Timeline.Now;
            _commands.Remove(command!);
            var result = Runtime.ResolveDetailed(intent, context);
            completed?.Invoke(result);
            return null;
        };
        _commands.Add(command);
        Timeline.Schedule(ReadyAt(intent.Actor) - Timeline.Now, intent.Label, command);
        return () =>
        {
            _commands.Remove(command);
            Timeline.Cancel(task => task.Action == command);
        };
    }

    public void Dispose()
    {
        foreach (var actor in _actors.Keys.ToArray()) Deactivate(actor);
        Timeline.Cancel(task => _commands.Contains(task.Action));
        _commands.Clear();
        Runtime.Executed -= Charge;
    }
}
