using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Effects;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Simulation;
using DualOntologyWorldEngine.Core.Timeline;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Examples.ZooBaseline;

/// <summary>
/// 动物园异常的最小因果模型。原文纸条只作为不完整观察依据；参数是本示例的模型假设。
/// 异常通过 ActionRuntime 的声明式效果结算，不预先替游客规划安全路线。
/// </summary>
public sealed class ZooAnomalies : IDisposable
{
    public sealed class Settings
    {
        public bool Enable { get; set; } = true;
        public double AttentionPerContact { get; set; } = 1d;
        public double AttentionThreshold { get; set; } = 3d;
        public double ProtectionReduction { get; set; } = 2d;
        public bool ProtectAtLionArea { get; set; } = true;
        public bool ProtectAtRestPoint { get; set; } = true;
        public bool ProtectAtApeArea { get; set; } = true;
    }

    public enum AnomalyPhase { Attention, Erosion, Protection, Missing }
    public enum AnomalyStatus { Normal, Watched, Protected, Missing }

    public sealed class AnomalyEvent
    {
        public AnomalyEvent(double time, Entity actor, Entity scene, AnomalyPhase phase,
            double attention, double erosion, string outcome) =>
            (Time, Actor, Scene, Phase, Attention, Erosion, Outcome) =
            (time, actor, scene, phase, attention, erosion, outcome);
        public double Time { get; }
        public Entity Actor { get; }
        public Entity Scene { get; }
        public AnomalyPhase Phase { get; }
        public double Attention { get; }
        public double Erosion { get; }
        public string Outcome { get; }
        public Entity? TriggerEvent { get; internal set; }
    }

    private sealed record AnomalyState(double Attention, double Erosion, AnomalyStatus Status);

    private readonly ZooBaseline _zoo;
    private readonly Settings _settings;
    private readonly List<Entity> _protectedScenes;
    private readonly List<AnomalyEvent> _trace = [];
    private readonly List<AnomalyEvent> _pending = [];
    private readonly Entity _ledger;
    private readonly Func<Intent, StateView, IReadOnlyList<Effect>, IReadOnlyList<Effect>> _transform;
    private bool _disposed;

    public ZooAnomalies(ZooBaseline zoo, Settings? settings = null, IEnumerable<Entity>? protectedScenes = null)
    {
        _zoo = zoo ?? throw new ArgumentNullException(nameof(zoo));
        _settings = settings ?? new Settings();
        _protectedScenes = protectedScenes?.Distinct().ToList() ?? [];

        _ledger = new Entity("异常观测账簿", _zoo.World,
            properties: new Dictionary<string, object> { ["states"] = new Dictionary<Entity, AnomalyState>() });
        foreach (var actor in Actors) States[actor] = new(0d, 0d, AnomalyStatus.Normal);

        _zoo.Session.Runtime.Effects.Register("zoo_anomaly", PrepareAnomaly);
        _zoo.Session.Runtime.Effects.Register("zoo_protection", PrepareProtection);
        _transform = Transform;
        _zoo.Session.Runtime.EffectTransforms.Add(_transform);
        _zoo.Session.Runtime.Executed += OnExecuted;
    }

    public Settings ModelSettings => _settings;
    public IReadOnlyList<AnomalyEvent> Trace => _trace;
    public IReadOnlyList<Entity> ProtectedScenes => _protectedScenes;
    public IReadOnlyList<Entity> Actors => _zoo.Visitors.Concat(_zoo.Staff).ToArray();
    public double AttentionOf(Entity actor) => States.TryGetValue(actor, out var state) ? state.Attention : 0d;
    public double ErosionOf(Entity actor) => States.TryGetValue(actor, out var state) ? state.Erosion : 0d;
    public AnomalyStatus StatusOf(Entity actor) => States.TryGetValue(actor, out var state) ? state.Status : AnomalyStatus.Normal;

    private Dictionary<Entity, AnomalyState> States =>
        (Dictionary<Entity, AnomalyState>)_ledger.Get<object>("states")!;

    private IReadOnlyList<Effect> Transform(Intent intent, StateView view, IReadOnlyList<Effect> changes)
    {
        if (view.Observer != null) return changes;
        if (!_settings.Enable || !Actors.Contains(intent.Actor)) return changes;

        var result = changes.ToList();
        foreach (var move in changes.Where(e => e.Op == "move" && e.Args.Length >= 2))
        {
            if (move.Args[0] is not Entity visitor || move.Args[1] is not Entity scene || !ReferenceEquals(visitor, intent.Actor))
                continue;
            if (IsExposureScene(scene)) result.Add(F("zoo_anomaly", visitor, scene));
            else if (IsProtectedScene(scene)) result.Add(F("zoo_protection", visitor, scene));
        }
        // 离散接触模型：进入展区的 move 只计一次；随后在展区执行任意非移动行动均继续暴露。
        if (intent.Actor.Parent is Entity current && IsExposureScene(current) && intent.Effect.Op != "move")
            result.Add(F("zoo_anomaly", intent.Actor, current));
        return result;
    }

    private void PrepareAnomaly(EffectPlan plan, Effect effect)
    {
        if (effect.Args is not [Entity visitor, Entity scene])
            throw new InvalidOperationException("zoo_anomaly 需要游客和场景引用");

        var statesBefore = ReadStates(plan.View);
        var state = statesBefore.GetValueOrDefault(visitor) ?? new(0d, 0d, AnomalyStatus.Normal);
        var attention = state.Attention;
        var erosion = state.Erosion;
        var nextAttention = attention + _settings.AttentionPerContact;
        var nextErosion = erosion + (_settings.AttentionPerContact * .5d);
        var states = new Dictionary<Entity, AnomalyState>(statesBefore)
        {
            [visitor] = new(nextAttention, nextErosion, nextAttention >= _settings.AttentionThreshold ? AnomalyStatus.Missing : AnomalyStatus.Watched)
        };
        plan.Write(_ledger, "states", states);

        var consequence = nextAttention >= _settings.AttentionThreshold;
        if (consequence) plan.Write(visitor, "$parent", null);

        plan.OnCommit(() =>
        {
            var phase = consequence ? AnomalyPhase.Missing : nextErosion >= 1d ? AnomalyPhase.Erosion : AnomalyPhase.Attention;
            var outcome = consequence ? "持续暴露达到模型阈值，游客从场景树中消失" : "接触异常场景";
            _pending.Add(new(_zoo.Session.Timeline.Now, visitor, scene, phase, nextAttention, nextErosion, outcome));
        });
    }

    private void PrepareProtection(EffectPlan plan, Effect effect)
    {
        if (effect.Args is not [Entity visitor, Entity scene])
            throw new InvalidOperationException("zoo_protection 需要游客和场景引用");

        var statesBefore = ReadStates(plan.View);
        var state = statesBefore.GetValueOrDefault(visitor) ?? new(0d, 0d, AnomalyStatus.Normal);
        var attention = state.Attention;
        var erosion = state.Erosion;
        var nextAttention = Math.Max(0d, attention - _settings.ProtectionReduction);
        var nextErosion = Math.Max(0d, erosion - _settings.ProtectionReduction * .5d);
        var states = new Dictionary<Entity, AnomalyState>(statesBefore)
        {
            [visitor] = new(nextAttention, nextErosion, AnomalyStatus.Protected)
        };
        plan.Write(_ledger, "states", states);
        plan.OnCommit(() => _pending.Add(new(_zoo.Session.Timeline.Now, visitor, scene,
            AnomalyPhase.Protection, nextAttention, nextErosion, "客观保护条件降低了关注")));
    }

    private bool IsExposureScene(Entity scene) => ReferenceEquals(scene, _zoo.RabbitArea) || ReferenceEquals(scene, _zoo.ElephantArea);

    private bool IsProtectedScene(Entity scene)
    {
        if (_protectedScenes.Contains(scene)) return true;
        if (_settings.ProtectAtLionArea && ReferenceEquals(scene, _zoo.LionArea)) return true;
        if (_settings.ProtectAtRestPoint && ReferenceEquals(scene, _zoo.RestPoint) &&
            scene.Get<bool>("dark") && scene.Get<bool>("enclosed")) return true;
        return _settings.ProtectAtApeArea && ReferenceEquals(scene, _zoo.PrimateArea);
    }

    private Dictionary<Entity, AnomalyState> ReadStates(StateView view) =>
        view.Read(_ledger, "states") is Dictionary<Entity, AnomalyState> states ? new(states) : [];

    private void OnExecuted(Intent intent, ActionResolution resolution)
    {
        if (!resolution.Allowed || !(resolution.Result?.Succeeded == true) || resolution.Event is null) return;
        var pending = _pending.Where(item => ReferenceEquals(item.Actor, intent.Actor) && item.TriggerEvent is null).ToArray();
        foreach (var item in pending)
        {
            item.TriggerEvent = resolution.Event;
            _pending.Remove(item);
            _trace.Add(item);
            if (item.Phase == AnomalyPhase.Missing) _zoo.Session.Deactivate(item.Actor);
        }
    }

    private static Effect F(string op, params object[] args) => AST.F(op, args);

    public void Dispose()
    {
        if (_disposed) return;
        _zoo.Session.Runtime.EffectTransforms.Remove(_transform);
        _zoo.Session.Runtime.Executed -= OnExecuted;
        _disposed = true;
    }
}
