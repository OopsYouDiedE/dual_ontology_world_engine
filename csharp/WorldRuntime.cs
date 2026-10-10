using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

public interface IProjectionTransform
{
    /// <summary>输入已使用接收者的投影引用；返回 null 表示此次信息未进入意识。</summary>
    ObservationInput? Transform(ObservationInput input);
}

/// <summary>引擎侧的现实与认知对应。Soul 的决策接口不接收本对象。</summary>
public sealed partial class WorldRuntime
{
    private readonly HashSet<Essence> _existing = [];
    private readonly Dictionary<Projection, Essence> _binding = [];
    private readonly Dictionary<Essence, List<Projection>> _reality = [];
    private readonly Dictionary<(SoulEssence Soul, Projection Reality), Projection> _mental = [];
    private readonly Dictionary<EntityId, Essence> _entities = [];
    private readonly Dictionary<FacetId, Projection> _facets = [];
    public ProjectionWorld Reality { get; } = new(null);

    public Projection Project(Essence essence, string name, Projection? parent = null)
        => Project(essence, new FacetId(Contract.Opaque()), name, parent);

    public Projection Project(Essence essence, FacetId facetId, string name, Projection? parent = null)
    {
        Contract.Identity(facetId.Value, "facet.id");
        Contract.Identity(essence.Id.Value, "entity.id");
        Contract.Text(name, "facet.name");
        Contract.Require(!_facets.ContainsKey(facetId), ContractErrorCode.DuplicateIdentity, "facet.id", "切面身份已登记，不能复用。");
        Contract.Require(!_entities.TryGetValue(essence.Id, out var old) || ReferenceEquals(old, essence) && _existing.Contains(essence),
            ContractErrorCode.DuplicateIdentity, "entity.id", "实体身份已登记或已删除，不能复用。");
        if (parent is not null && !Reality.Contains(parent)) throw new InvalidOperationException("现实表现的父节点必须在现实世界。");
        var projection = new Projection(Reality, name, essence.Kind) { Parent = parent };
        Reality.Projections = Reality.Projections.Add(projection);
        _existing.Add(essence);
        _entities.TryAdd(essence.Id, essence);
        _facets.Add(facetId, projection);
        _binding.Add(projection, essence);
        if (!_reality.TryGetValue(essence, out var views)) _reality.Add(essence, views = []);
        views.Add(projection);
        Sync();
        return projection;
    }

    /// <summary>建立初始记忆或多种身份表征的引擎对应，不改变角色认知。</summary>
    public void Associate(SoulEssence soul, Projection mental, Projection reality)
    {
        lock (soul.World.Gate)
        {
        if (!soul.World.Contains(mental) || !Reality.Contains(reality)) throw new InvalidOperationException("绑定的投影世界不匹配。");
        if (_binding.TryGetValue(mental, out var previous) && !ReferenceEquals(previous, _binding[reality]))
            throw new InvalidOperationException("已有投影不能静默改绑为另一个客观对象。");
        _binding[mental] = _binding[reality];
        _mental[(soul, reality)] = mental;
        }
    }

    public IReadOnlyList<Projection> Representations(Essence essence) => _reality.TryGetValue(essence, out var views) ? views.AsReadOnly() : [];
    /// <summary>Read an existing correspondence without observing or revealing an unknown entity.</summary>
    public Projection? KnownProjection(SoulEssence soul, Essence essence)
        // legacy-first: migrating callers must use KnownFacet or LookupSingleKnown.
        => Representations(essence).Select(view => Mental(soul, view)).FirstOrDefault(mental => mental is not null);
    public ImmutableArray<Projection> KnownProjections(SoulEssence soul, Essence entity)
    {
        lock (soul.World.Gate)
        {
            Contract.Require(_entities.TryGetValue(entity.Id, out var registered) && ReferenceEquals(registered, entity),
                ContractErrorCode.InvalidReference, "entity", "实体未在此引擎登记。");
            return [..Representations(entity).Select(p => Mental(soul, p)).OfType<Projection>().Distinct().OrderBy(p => Observation.IdKey(p.SubjectId!.Value), StringComparer.Ordinal)];
        }
    }
    public ContractResult<Projection> KnownFacet(SoulEssence soul, FacetId facet) => Contract.Try(() =>
    {
        lock (soul.World.Gate)
        {
            Contract.Identity(facet.Value, "facet");
            Contract.Require(_facets.TryGetValue(facet, out var view), ContractErrorCode.InvalidReference, "facet", "切面未登记。");
            var mental = Mental(soul, view!);
            Contract.Require(mental is not null, ContractErrorCode.Unrecognized, "facet", "主体尚未认识该切面。");
            return mental!;
        }
    });
    public ContractResult<Projection> LookupSingleKnown(SoulEssence soul, Essence entity) => Contract.Try(() =>
    {
        var known = KnownProjections(soul, entity);
        Contract.Require(!known.IsEmpty, ContractErrorCode.Unrecognized, "entity", "主体尚未认识该实体。");
        if (known.Length > 1) throw new CognitionContractException(new(ContractErrorCode.Ambiguous, "entity", "主体保留多个表现。", CandidateIds: [..known.Select(p => p.SubjectId!.Value)]));
        return known[0];
    });
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
        KnowledgeRevision start;
        Dictionary<Projection, Projection> mapping;
        Dictionary<Projection, Projection> known;
        HashSet<Projection> inputDomain;
        lock (soul.World.Gate)
        {
            start = soul.World.Revision;
            known = _mental.Where(p => ReferenceEquals(p.Key.Soul, soul) && soul.World.Contains(p.Value)).ToDictionary(p => p.Key.Reality, p => p.Value);
            mapping = sources.ToDictionary(p => p, p => known.GetValueOrDefault(p) ?? new Projection(soul.World, p.Name, p.Kind));
            inputDomain = soul.World.Projections.Concat(mapping.Values).ToHashSet();
        }
        try
        {
            Projection? Known(Projection p) => mapping.GetValueOrDefault(p) ?? known.GetValueOrDefault(p);
            void ValidateTransformReferences(ObservationInput input)
            {
                SubjectProjectionId Reference(Projection reference)
                {
                    Contract.Require(reference is not null && ReferenceEquals(reference.World, soul.World) && inputDomain.Contains(reference),
                        ContractErrorCode.InvalidReference, "transform.reference", "感知转换引用须属于起始主体图或本次感知范围。");
                    return reference!.SubjectId!.Value;
                }
                Contract.Require(input.AddRelations is not null && input.RemoveRelations is not null && input.DenyRelations is not null,
                    ContractErrorCode.MalformedData, "transform.relations", "感知转换关系集合不能为空。");
                foreach (var (_, reference) in input.AddRelations!.Concat(input.RemoveRelations!).Concat(input.DenyRelations!)) Reference(reference);
                if (input.Parent is { } parent) Reference(parent);
                if (input.Rules is { } rules) { Contract.Array(rules, "transform.rules"); foreach (var rule in rules) CognitionAst.Freeze(rule, Reference); }
                if (input.Procedure is { } procedure) CognitionAst.Freeze(procedure, Reference);
            }
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
                if (soul.World.Revision != start)
                    throw new CognitionContractException(new(ContractErrorCode.StaleRevision, "expected_revision", "感知转换期间认知版本已变化。",
                        ExpectedRevision: start, ActualRevision: soul.World.Revision, CandidateIds: []));
                if (perceived is not null)
                {
                    if (!ReferenceEquals(perceived.Target, mental)) throw new InvalidOperationException("感知转换保留目标连续性。");
                    ValidateTransformReferences(perceived);
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
            var bindings = new BindingPublication(this, soul, [..inputs.Select(p => (p.Source, p.Input.Target!, _binding[p.Source]))]);
            Observation.ApplyBatch(soul.World, committed, start, bindings);
            return [..inputs.Select(p => p.Input.Target!)];
        }
        finally
        {
            lock (soul.World.Gate)
                foreach (var mental in mapping.Values)
                    if (!soul.World.Contains(mental)) soul.World.Used.Add(mental.SubjectId!.Value);
        }
    }

    internal sealed class BindingPublication(WorldRuntime runtime, SoulEssence soul,
        ImmutableArray<(Projection Source, Projection Mental, Essence Entity)> entries)
    {
        internal void Publish()
        {
            foreach (var (source, mental, entity) in entries)
            {
                runtime._mental[(soul, source)] = mental;
                runtime._binding[mental] = entity;
            }
        }
    }

    internal Projection? Mental(SoulEssence soul, Projection reality) => _mental.GetValueOrDefault((soul, reality)) is { } mental && soul.World.Contains(mental) ? mental : null;
    internal IEnumerable<SoulEssence> Souls => _existing.OfType<SoulEssence>();

    /// <summary>陈述只携带接收者可表征的名词，发送者的认知引用不会进入对方世界。</summary>
    internal void Deliver(SoulEssence receiver, Essence sender, Effect.Say speech, double time, EventId cause)
    {
        var (text, request) = (speech.Text, speech.Request);
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
        var claims = speech.Claim is Condition.Tagged { Target: Noun.Reference r } c && Resolve(r.Projection) is { } on
            ? ImmutableArray.Create(new EventClaimV1<Essence?>(new("core.state"), on, null, null, c.Tag, c.Minimum, Truth.True)) : [];
        var testimony = new TestimonyEventV1<Essence?>(new() {
            Time = time, TimeUnit = ObservationTimeUnitV1.ActionValue, Subject = sender, Object = receiver,
            Location = Space(sender), Source = sender, KnowledgeSource = ObservationSource.Testimony,
            Phase = EventPhaseV1.Delivered, Outcome = EventOutcomeV1.Succeeded, Topic = speech.Topic,
            Claims = claims, Perceivers = [receiver], Causes = [cause] });
        RecordCommittedEvent(testimony);
        Observation.Observe(receiver, new ObservationInput(text, ProjectionKind.Event)
        {
            Source = ObservationSource.Testimony, Time = time, Existence = Truth.True,
            AddRelations = [(Relations.Subject, speaker)], Procedure = translated, Details = text,
            TimeUnit = ObservationTimeUnitV1.ActionValue, Event = ProjectEvent(receiver, testimony)
        });
    }
}
