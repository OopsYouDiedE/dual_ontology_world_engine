namespace DualOntologyWorldEngine.TempCore;

/// <summary>
/// Projection 表现层基类。
/// 现实世界与主观世界均采用同构 Projection 模型。
/// Source 仅有 get 访问器，仅 RealityProjection 拥有非空 Source；
/// 主观认知（观察、记忆、推理、幻觉）与感知临时结果的 Source 恒为 null。
/// </summary>
public abstract class Projection
{
    public string Name { get; set; }
    public Essence? Source { get; }
    public Projection? Parent { get; private set; }
    public HashSet<Projection> Children { get; } = [];
    public Dictionary<Tag, double> Tags { get; } = [];
    public HashSet<(RelationTag Relation, Projection Target)> Relations { get; } = [];
    public bool UpdateByObs { get; set; } = true;

    protected Projection(string name, Essence? source = null)
    {
        Name = name;
        Source = source;
    }

    public Projection Reparent(Projection? newParent)
    {
        for (var ancestor = newParent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, this))
                throw new InvalidOperationException("不能将投影节点挂载到自身或后代节点下。");
        }

        Parent?.Children.Remove(this);
        Parent = newParent;
        newParent?.Children.Add(this);
        return this;
    }

    /// <summary>
    /// 创建与任何外部状态解耦、Source 恒为 null 的同构独立投影副本。
    /// 替代任何运行时类型 switch 分发。
    /// </summary>
    public abstract Projection CreateDetached();

    /// <summary>
    /// 双重分派入口：子类经由 PerceptionPipeline 感知后，以强类型 this 转发至 Observation.Observe(...)。
    /// </summary>
    public abstract ObservationEvent ObserveBy(SoulEssence observer, SpaceProjection currentSpace, ProjectionBinding binding);
}

/// <summary>空间节点投影。</summary>
public class SpaceProjection(string name, Essence? source = null) : Projection(name, source)
{
    public override Projection CreateDetached() => new SpaceProjection(Name);

    public override ObservationEvent ObserveBy(SoulEssence observer, SpaceProjection currentSpace, ProjectionBinding binding)
    {
        var perceived = PerceptionPipeline.Perceive(this, currentSpace, observer, binding);
        return Observation.Observe(observer, currentSpace, perceived, binding);
    }
}

/// <summary>边界与门禁通道投影。</summary>
public class BoundaryProjection(string name, Essence? source = null) : Projection(name, source)
{
    public override Projection CreateDetached() => new BoundaryProjection(Name);

    public override ObservationEvent ObserveBy(SoulEssence observer, SpaceProjection currentSpace, ProjectionBinding binding)
    {
        var perceived = PerceptionPipeline.Perceive(this, currentSpace, observer, binding);
        return Observation.Observe(observer, currentSpace, perceived, binding);
    }
}

/// <summary>成形物理实体投影。</summary>
public class PhysicalProjection(string name, Essence? source = null) : Projection(name, source)
{
    public override Projection CreateDetached() => new PhysicalProjection(Name);

    public override ObservationEvent ObserveBy(SoulEssence observer, SpaceProjection currentSpace, ProjectionBinding binding)
    {
        var perceived = PerceptionPipeline.Perceive(this, currentSpace, observer, binding);
        return Observation.Observe(observer, currentSpace, perceived, binding);
    }
}

/// <summary>身体投影。</summary>
public class BodyProjection(string name, Essence? source = null) : Projection(name, source)
{
    public override Projection CreateDetached() => new BodyProjection(Name);

    public override ObservationEvent ObserveBy(SoulEssence observer, SpaceProjection currentSpace, ProjectionBinding binding)
    {
        var perceived = PerceptionPipeline.Perceive(this, currentSpace, observer, binding);
        return Observation.Observe(observer, currentSpace, perceived, binding);
    }
}

/// <summary>心智投影。</summary>
public class SoulProjection(string name, Essence? source = null) : Projection(name, source)
{
    public override Projection CreateDetached() => new SoulProjection(Name);

    public override ObservationEvent ObserveBy(SoulEssence observer, SpaceProjection currentSpace, ProjectionBinding binding)
    {
        var perceived = PerceptionPipeline.Perceive(this, currentSpace, observer, binding);
        return Observation.Observe(observer, currentSpace, perceived, binding);
    }
}

/// <summary>概念与语义身份投影。</summary>
public class ConceptProjection(string name, Essence? source = null) : Projection(name, source)
{
    public override Projection CreateDetached() => new ConceptProjection(Name);

    public override ObservationEvent ObserveBy(SoulEssence observer, SpaceProjection currentSpace, ProjectionBinding binding)
    {
        var perceived = PerceptionPipeline.Perceive(this, currentSpace, observer, binding);
        return Observation.Observe(observer, currentSpace, perceived, binding);
    }
}

/// <summary>场域投影。</summary>
public class AreaProjection(string name, Essence? source = null) : Projection(name, source)
{
    public override Projection CreateDetached() => new AreaProjection(Name);

    public override ObservationEvent ObserveBy(SoulEssence observer, SpaceProjection currentSpace, ProjectionBinding binding)
    {
        var perceived = PerceptionPipeline.Perceive(this, currentSpace, observer, binding);
        return Observation.Observe(observer, currentSpace, perceived, binding);
    }
}

/// <summary>
/// 投影世界容器。现实世界与主观世界各持有一套独立实例。
/// 严格保证：Projection 之间的 Relations 必须属于同一个 ProjectionWorld，严禁跨世界引用。
/// </summary>
public class ProjectionWorld
{
    public HashSet<Projection> Projections { get; } = [];

    public bool Contains(Projection projection) => Projections.Contains(projection);

    public void Add(Projection projection) => Projections.Add(projection);

    public void Remove(Projection projection)
    {
        Projections.Remove(projection);
        projection.Parent?.Children.Remove(projection);
        // 清理指向该投影的内部关系
        foreach (var p in Projections)
        {
            p.Relations.RemoveWhere(r => ReferenceEquals(r.Target, projection));
        }
    }

    public void Replace(Projection oldProj, Projection newProj)
    {
        Remove(oldProj);
        Add(newProj);
    }

    /// <summary>
    /// 在当前世界内安全添加语义关系，强制校验双方必须同属于本世界。
    /// </summary>
    public void AddRelation(Projection source, RelationTag relation, Projection target)
    {
        if (!Projections.Contains(source))
            throw new InvalidOperationException($"源投影 {source.Name} 不在当前 ProjectionWorld 中。");
        if (!Projections.Contains(target))
            throw new InvalidOperationException($"目标投影 {target.Name} 不在当前 ProjectionWorld 中，严禁跨世界建立语义关系。");

        source.Relations.Add((relation, target));
    }

    public Projection? FindByName(string name)
        => Projections.FirstOrDefault(p => p.Name == name);

    public IEnumerable<Projection> FindByRelation(RelationTag relation, Projection target)
        => Projections.Where(p => p.Relations.Any(r => r.Relation == relation && ReferenceEquals(r.Target, target)));

    public IEnumerable<Projection> FindByTag(Tag tag, double minVal = 1.0)
        => Projections.Where(p => p.Tags.TryGetValue(tag, out var val) && val >= minVal);

    public void Apply(ProjectionDelta delta) => delta.Apply(this);
}

/// <summary>
/// 针对主观 ProjectionWorld 的增量更新结构。
/// </summary>
public class ProjectionDelta(Projection target)
{
    public Projection Target { get; } = target;
    public Dictionary<Tag, double>? ChangedTags { get; init; }
    public List<(RelationTag Relation, Projection Target)>? AddedRelations { get; init; }
    public List<(RelationTag Relation, Projection Target)>? RemovedRelations { get; init; }

    public void Apply(ProjectionWorld world)
    {
        if (!world.Contains(Target))
            world.Add(Target);

        if (ChangedTags != null)
        {
            foreach (var (k, v) in ChangedTags)
                Target.Tags[k] = v;
        }

        if (RemovedRelations != null)
        {
            foreach (var rel in RemovedRelations)
                Target.Relations.Remove(rel);
        }

        if (AddedRelations != null)
        {
            foreach (var (rel, target) in AddedRelations)
            {
                if (!world.Contains(target))
                {
                    world.Add(target);
                }
                world.AddRelation(Target, rel, target);
            }
        }
    }
}


