using System.Collections.Immutable;

namespace DualOntologyWorldEngine.Core;

internal static class SubjectRead
{
    internal sealed record View(DetachedProjectionGraphV1 Graph,
        ImmutableDictionary<SubjectProjectionId, Projection> Originals, KnowledgeRevision Revision)
    {
        internal Projection Get(Projection original)
        {
            Contract.Require(original is not null && original.SubjectId is { } id && Originals.GetValueOrDefault(id) == original,
                ContractErrorCode.InvalidReference, "reference", "求值引用不在捕获的主体图。");
            return Graph.Projections[original!.SubjectId!.Value];
        }
        internal Projection Original(Projection detached) => Originals[detached.SubjectId!.Value];
        internal Effect Original(Effect effect) => Ast.Map(effect, n => n is Noun.Reference r ? new Noun.Reference(Original(r.Projection)) : n);
    }
    internal static View Capture(ProjectionWorld world)
    {
        lock (world.Gate)
        {
            var snapshot = Contract.Value(world.CaptureSnapshot());
            var graph = world.ReaderGraph ??= SnapshotGraph.Build(snapshot);
            return new(graph, world.Projections.ToImmutableDictionary(p => p.SubjectId!.Value), snapshot.Revision);
        }
    }
    internal static void Context(ProjectionWorld world, Condition c, ActionContext context)
    {
        foreach (var p in new[] { context.Actor, context.Target, context.Tool, context.Scene }.OfType<Projection>().Concat(Ast.References(c)))
            Contract.Require(world.Contains(p), ContractErrorCode.InvalidReference, "context", "AST 上下文与引用必须属于同一世界。");
    }
    internal static Truth Evaluate(Condition condition, ActionContext context)
    {
        Contract.Require(context is not null, ContractErrorCode.MalformedData, "context", "上下文不能为空。");
        Contract.Require(context!.Actor is not null, ContractErrorCode.MalformedData, "actor", "上下文主体不能为空。");
        var world = context!.Actor!.World;
        // Reality remains a legacy read; Sync does not participate in the subjective publication gate.
        if (world.SubjectWorldId is null || world.Detached)
        {
            if (world.Detached)
            {
                var map = world.Projections.ToImmutableDictionary(p => p.SubjectId!.Value);
                condition = CognitionAst.Thaw(Contract.Value(CognitionAst.FreezeCondition(condition, map)), map);
            }
            Context(world, condition, context);
            return Ast.EvaluateLive(condition, context);
        }
        var view = Capture(world);
        var graph = view.Graph;
        SubjectProjectionId Id(Projection p) => view.Get(p).SubjectId!.Value;
        var frozen = CognitionAst.Freeze(condition, Id);
        var c = CognitionAst.Thaw(frozen, graph.Projections);
        return Ast.EvaluateLive(c, new(view.Get(context.Actor), context.Target is null ? null : view.Get(context.Target),
            context.Tool is null ? null : view.Get(context.Tool), context.Scene is null ? null : view.Get(context.Scene)));
    }
}
