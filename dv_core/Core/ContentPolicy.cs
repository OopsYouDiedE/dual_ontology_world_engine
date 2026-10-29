using DuelView.Compiled;

namespace DuelView.Core;

public static partial class DVKernel
{
    public static object F_dv_content_autonomy(object actorId, object definition, object basis)
    {
        var autonomy = R.record_find(definition, "autonomy", false);
        if (autonomy is false) return false;
        var personality = R.record_find(definition, "personality", new List<object>());
        var result = R.record_with(autonomy, "actor", actorId);
        foreach (var key in new[] { "group", "domain" })
            result = R.record_with(result, key, R.record_find(personality, key, ""));
        return R.record_with(result, "basis", basis);
    }

    /// <summary>Merge authored norms without overwriting norms learned during play.</summary>
    public static object F_dv_apply_content_policy(object actor, object definition, object applyPersonality)
    {
        var policy = R.record_find(actor, "policy", new List<object>());
        var original = R.Items(R.record_find(policy, "norm_rules", new List<object>()));
        var rules = original.Where(rule => R.record_find(rule, "content_origin", "") is not string { Length: > 0 }).ToList();
        var ids = rules.Select(rule => R.Text(R.record_find(rule, "id", ""))).ToHashSet(StringComparer.Ordinal);
        var authored = R.Items(R.record_find(definition, "obeys", new List<object>()));
        foreach (var rule in authored)
        {
            var id = R.Text(R.record_find(rule, "id", ""));
            if (!ids.Add(id)) throw new InvalidDataException("Authored norm conflicts with an existing norm: " + id);
            rules.Add(rule);
        }
        if (authored.Count > 0 || rules.Count != original.Count)
            policy = R.record_with(policy, "norm_rules", rules);
        if (R.Bool(applyPersonality))
        {
            var personality = R.record_find(definition, "personality", new List<object>());
            policy = R.record_with(policy, "value_order", R.record_find(personality, "value_order", new List<object>()));
        }
        var priorAutonomy = R.record_find(policy, "sims_autonomy", new List<object>());
        policy = R.record_with(policy, "sims_autonomy", F_dv_content_autonomy(
            R.record_find(actor, "id", ""), definition, priorAutonomy is false ? new List<object>() : R.record_find(priorAutonomy, "basis", new List<object>())));
        return R.record_with(actor, "policy", policy);
    }
}
