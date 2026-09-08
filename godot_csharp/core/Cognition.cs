namespace DualOntologyWorldEngine.Core;

/// <summary>推理只提交明确的内部命题，不根据 Binding 校正角色的结论。</summary>
public static class Cognition
{
    internal static ObservationInput? Input(Condition fact, double time)
    {
        var target = fact switch
        {
            Condition.Exists { Target: Noun.Reference r } => r.Projection,
            Condition.Not { Operand: Condition.Exists { Target: Noun.Reference r } } => r.Projection,
            Condition.Related { Target: Noun.Reference r } => r.Projection,
            Condition.Not { Operand: Condition.Related { Target: Noun.Reference r } } => r.Projection,
            Condition.IsKind { Target: Noun.Reference r } => r.Projection,
            _ => null
        };
        if (target is null) return null;
        var input = new ObservationInput(target.Name, target.Kind) { Target = target, Source = ObservationSource.Inference, Time = time };
        return fact switch
        {
            Condition.Exists => input with { Existence = Truth.True },
            Condition.Not { Operand: Condition.Exists } => input with { Existence = Truth.False },
            Condition.Not { Operand: Condition.Related { Other: Noun.Reference other } relation } => input with { DenyRelations = [(relation.Relation, other.Projection)] },
            Condition.Related { Other: Noun.Reference other } relation => input with { AddRelations = [(relation.Relation, other.Projection)] },
            Condition.IsKind kind => input with { Kind = kind.Kind },
            _ => null
        };
    }

    public static Projection Infer(SoulEssence soul, Condition fact, double time = 0)
        => Observation.Observe(soul, Input(fact, time) ?? throw new InvalidOperationException("推理结果必须是已绑定的存在、身份或关系命题。"));

    public static void Corrupt(SoulEssence soul, IProjectionTransform transform, double time = 0)
    {
        foreach (var target in soul.World.Projections)
        {
            var input = transform.Transform(new ObservationInput(target.Name, target.Kind)
            {
                Target = target, Source = ObservationSource.Corruption, Time = time
            });
            if (input is not null)
            {
                if (!ReferenceEquals(input.Target, target)) throw new InvalidOperationException("侵蚀保留认知对象引用。");
                Observation.Observe(soul, input);
            }
        }
    }
}
