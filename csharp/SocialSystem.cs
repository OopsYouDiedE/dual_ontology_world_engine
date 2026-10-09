namespace DualOntologyWorldEngine.Core;

public enum SocialStance { Accept, Negotiate, Reject }
public sealed record SocialAttitude(SocialStance Stance, int Score);

/// <summary>只评价已经收到的请求；人际状态与历史均来自接收者的内部投影。</summary>
public static class SocialSystem
{
    public static readonly Tag RequestCompatibility = new("RequestCompatibility");

    public static SocialAttitude Evaluate(ProjectionWorld world, Projection self, Projection request, int? compatibility = null)
    {
        if (!world.Contains(self) || !world.Contains(request)) throw new InvalidOperationException("社交评价只能使用内部世界。");
        var sender = request.Relations.FirstOrDefault(r => r.Relation == Relations.Subject).Target;
        return EvaluateSender(world, self, sender, compatibility ?? request.Tags.GetValueOrDefault(RequestCompatibility), request);
    }

    /// <summary>Read-only preview using the same recipient, sender and history as a received request.</summary>
    public static SocialAttitude EvaluateSender(ProjectionWorld world, Projection self, Projection? sender, int compatibility = 0, Projection? request = null)
    {
        if (!world.Contains(self) || sender is not null && !world.Contains(sender)) throw new InvalidOperationException("Social preview requires internal projections.");
        var score = compatibility + self.Tags.GetValueOrDefault(Tags.Cooperation) + (sender?.Tags.GetValueOrDefault(Tags.Trust) ?? 0);
        if (sender is not null)
            score += world.History.Where(h => h.Target.Kind == ProjectionKind.Event &&
                !ReferenceEquals(h.Target, request) && h.Relations.Contains((Relations.Subject, sender)))
                .GroupBy(h => h.Target).Sum(events => events.Last().Tags.GetValueOrDefault(Tags.Trust));
        return new(score > 0 ? SocialStance.Accept : score < 0 ? SocialStance.Reject : SocialStance.Negotiate, score);
    }
}
