using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public interface IProjectionTransform
{
    /// <summary>输入已使用接收者的投影引用；返回 null 表示此次信息未进入意识。</summary>
    ObservationInput? Transform(ObservationInput input);
}

/// <summary>引擎侧的现实与认知对应。Soul 的决策接口不接收本对象。</summary>
public sealed class WorldRuntime
{
    private readonly HashSet<Essence> _existing = [];
    private readonly Dictionary<Projection, Essence> _binding = [];
    private readonly Dictionary<Essence, List<Projection>> _reality = [];
    private readonly Dictionary<(SoulEssence Soul, Projection Reality), Projection> _mental = [];
    public ProjectionWorld Reality { get; } = new();

    public Projection Project(Essence essence, string name, Projection? parent = null)
    {
        if (parent is not null && !Reality.Contains(parent)) throw new InvalidOperationException("现实表现的父节点必须在现实世界。");
        var projection = new Projection(Reality, name, essence.Kind) { Parent = parent };
        Reality.Projections = Reality.Projections.Add(projection);
        _existing.Add(essence);
        _binding.Add(projection, essence);
        if (!_reality.TryGetValue(essence, out var views)) _reality.Add(essence, views = []);
        views.Add(projection);
        Sync();
        return projection;
    }

    /// <summary>建立初始记忆或多种身份表征的引擎对应，不改变角色认知。</summary>
    public void Associate(SoulEssence soul, Projection mental, Projection reality)
    {
        if (!soul.World.Contains(mental) || !Reality.Contains(reality)) throw new InvalidOperationException("绑定的投影世界不匹配。");
        if (_binding.TryGetValue(mental, out var previous) && !ReferenceEquals(previous, _binding[reality]))
            throw new InvalidOperationException("已有投影不能静默改绑为另一个客观对象。");
        _binding[mental] = _binding[reality];
        _mental[(soul, reality)] = mental;
    }

    public IReadOnlyList<Projection> Representations(Essence essence) => _reality.TryGetValue(essence, out var views) ? views.AsReadOnly() : [];
    /// <summary>Read an existing correspondence without observing or revealing an unknown entity.</summary>
    public Projection? KnownProjection(SoulEssence soul, Essence essence)
        => Representations(essence).Select(view => Mental(soul, view)).FirstOrDefault(mental => mental is not null);
    internal Essence? Resolve(Projection? projection) => projection is not null && _binding.TryGetValue(projection, out var essence) && _existing.Contains(essence) ? essence : null;
    internal bool Controls(SoulEssence soul, Essence body) => _existing.Contains(soul) && soul.Relations.Contains((Relations.Controls, body));
    internal void Destroy(Essence essence) => _existing.Remove(essence);
    public void Remove(Essence essence) { Destroy(essence); Sync(); }
    internal Projection? View(Essence? essence) => essence is null ? null : _reality.GetValueOrDefault(essence)?.FirstOrDefault();
    internal Essence? Space(Essence essence) => essence.Get<SpatialProperty>() is { } spatial
        ? spatial.Holder is { } holder ? holder.Get<SpatialProperty>()?.Space : spatial.Space : null;

    /// <summary>Property 是权威状态；所有现实侧面同时更新，内部世界保持不变。</summary>
    public void Sync()
    {
        foreach (var (essence, views) in _reality)
        foreach (var view in views)
        {
            view.Existence = Ast.From(_existing.Contains(essence));
            var states = essence.Get<StateProperty>();
            var tags = states?.Values.ToImmutableDictionary().SetItems(essence.Get<TemporaryStateProperty>()?.Values ?? []).Where(p => states.Observable.Contains(p.Key)).ToImmutableDictionary() ?? ImmutableDictionary<Tag, int>.Empty;
            if (essence.Get<SurfaceProperty>()?.Surfaces.TryGetValue(view, out var surface) == true) tags = tags.SetItems(surface);
            if (essence.Get<LockProperty>() is { } doorLock) tags = tags.SetItem(Tags.Locked, doorLock.IsLocked ? 1 : 0);
            view.Tags = tags;
            if (essence.Get<SpatialProperty>() is { } spatial) view.Parent = View(spatial.Holder ?? spatial.Space);
            // 本质身份不自动成为可观察事实，只有显式声明的表面关系进入现实表现。
            view.Relations = essence.Get<SurfaceProperty>()?.Relations.GetValueOrDefault(view) ?? [];
            view.Procedure = essence.Get<ProcedureProperty>()?.Effect;
            view.Rules = essence.Get<RuleProperty>()?.Rules ?? [];
        }
    }

    /// <summary>仅摄入调用方已经筛选的感知范围。映射不推导角色是否识别同一对象。</summary>
    public ImmutableArray<Projection> Observe(SoulEssence soul, IEnumerable<Projection> visible, double time = 0, IProjectionTransform? transform = null)
    {
        var sources = visible.Distinct().ToArray();
        if (sources.Any(p => !Reality.Contains(p))) throw new InvalidOperationException("感知源必须属于现实世界。");
        var mapping = sources.ToDictionary(p => p, p => Mental(soul, p) ?? new Projection(soul.World, p.Name, p.Kind));
        Projection? Known(Projection p) => mapping.GetValueOrDefault(p) ?? Mental(soul, p);
        Noun MapNoun(Noun noun) => noun is Noun.Reference r
            ? new Noun.Reference(Known(r.Projection) ?? throw new InvalidOperationException("未观察到 AST 中的名词。")) : noun;
        Rule ReadRule(Rule rule, Projection mental)
        {
            var mapped = Ast.Map(rule, MapNoun);
            var remembered = mental.Rules.FirstOrDefault(r => r.Mode == mapped.Mode && Ast.Equivalent(r.Condition, mapped.Condition) && Ast.Equivalent(r.Effect, mapped.Effect));
            return mapped with { Acceptance = remembered?.Acceptance ?? Truth.Unknown };
        }
        var inputs = new List<(Projection Source, ObservationInput Input)>();
        foreach (var source in sources)
        {
            var mental = mapping[source];
            var input = new ObservationInput(source.Name, source.Kind)
            {
                Target = mental, Time = time, Detected = source.Existence == Truth.True, Existence = source.Existence,
                Move = source.Parent is null || Known(source.Parent) is not null,
                Parent = source.Parent is null ? null : Known(source.Parent),
                Tags = source.Tags.ToImmutableDictionary(p => p.Key, p => (int?)p.Value),
                AddRelations = source.Relations.Where(r => Known(r.Target) is not null).Select(r => (r.Relation, Known(r.Target)!)).ToImmutableHashSet(),
                // 不完整的规则保持未获知；不能通过解析规则顺带获知隐藏名词。
                Rules = source.Rules.IsEmpty ? null : [..source.Rules.Where(r => Ast.References(r).All(p => Known(p) is not null)).Select(r => ReadRule(r, mental))],
                Procedure = source.Procedure is { } procedure && Ast.References(procedure).All(p => Known(p) is not null) ? Ast.Map(procedure, MapNoun) : null
            };
            var perceived = transform is null ? input : transform.Transform(input);
            if (perceived is not null)
            {
                if (!ReferenceEquals(perceived.Target, mental)) throw new InvalidOperationException("感知转换保留目标连续性。");
                inputs.Add((source, perceived));
            }
        }
        // 被抑制的新投影不能由另一个对象的关系或规则间接进入认知。
        var accepted = inputs.Select(p => p.Input.Target!).ToHashSet();
        bool Available(Projection p) => accepted.Contains(p) || soul.World.Contains(p);
        var committed = inputs.Select(pair => pair.Input with
        {
            Move = pair.Input.Move && (pair.Input.Parent is null || Available(pair.Input.Parent)),
            Parent = pair.Input.Parent is { } parent && Available(parent) ? parent : null,
            AddRelations = pair.Input.AddRelations.Where(r => Available(r.Target)).ToImmutableHashSet(),
            Rules = pair.Input.Rules is { } rules ? [..rules.Where(r => Ast.References(r).All(Available))] : null,
            Procedure = pair.Input.Procedure is { } procedure && Ast.References(procedure).All(Available) ? procedure : null
        }).ToArray();
        Observation.ApplyBatch(soul.World, committed);
        foreach (var (source, input) in inputs)
        {
            _mental[(soul, source)] = input.Target!;
            _binding[input.Target!] = _binding[source];
        }
        return [..inputs.Select(p => p.Input.Target!)];
    }

    internal Projection? Mental(SoulEssence soul, Projection reality) => _mental.GetValueOrDefault((soul, reality)) is { } mental && soul.World.Contains(mental) ? mental : null;
    internal IEnumerable<SoulEssence> Souls => _existing.OfType<SoulEssence>();

    /// <summary>陈述只携带接收者可表征的名词，发送者的认知引用不会进入对方世界。</summary>
    internal void Deliver(SoulEssence receiver, Essence sender, string text, Effect? request, double time)
    {
        var senderView = View(sender);
        if (senderView is null) return;
        Observe(receiver, [senderView], time);
        var speaker = Mental(receiver, senderView)!;
        Projection? Translate(Projection source)
        {
            if (receiver.World.Contains(source)) return source;
            return Resolve(source) is { } essence && View(essence) is { } view ? Mental(receiver, view) : null;
        }
        // 无法指称的请求保留为文本，不自动建立客观名词。
        var translated = request is not null && Ast.References(request).All(p => Translate(p) is not null)
            ? Ast.Map(request, n => n is Noun.Reference r ? new Noun.Reference(Translate(r.Projection)!) : n) : null;
        Observation.Observe(receiver, new ObservationInput(text, ProjectionKind.Event)
        {
            Source = ObservationSource.Testimony, Time = time, Existence = Truth.True,
            AddRelations = [(Relations.Subject, speaker)], Procedure = translated, Details = text
        });
    }
}

