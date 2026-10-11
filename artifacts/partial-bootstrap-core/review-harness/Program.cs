// Independent review tool. Generic API counterexamples only; not production or game evidence.
using System.Collections.Immutable;
using DualOntology.Language;

static class Review
{
    private static readonly Condition Yes = new Condition.Constant(Truth.True);
    private static ImmutableDictionary<string, int> Tags(params (string Key, int Value)[] tags)
        => tags.ToImmutableDictionary(p => p.Key, p => p.Value);
    private static void Show(string name, object? value) => Console.WriteLine($"{name}: {value}");
    private static void Attempt(string name, Func<object?> action)
    {
        try { Show(name, action()); }
        catch (Exception ex) { Show(name, $"{ex.GetType().Name}: {ex.Message}"); }
    }
    private static (WorldCore World, Guid Subject, EntityId Object, ActionBindings Bindings) Setup(int stock = 3, bool alias = false, bool knowStock = true)
    {
        var world = new WorldCore();
        var room = new EntityId("host-room");
        var body = new EntityId("host-body");
        var obj = new EntityId("host-object");
        world.Define(room, new("space"));
        world.Define(body, new("body", room));
        world.Define(obj, new("object", room, Tags: Tags(("stock", stock), ("flag", 0), ("progress", 0))));
        var subject = Guid.NewGuid();
        world.CreateSubject(subject, body);
        world.Observe(subject,
            new(room, "room", new(Truth.True, "space")),
            new(body, "self", new(Truth.True, "body", "room")),
            new(obj, "object", new(Truth.True, "object", "room", knowStock ? Tags(("stock", stock), ("flag", 0), ("progress", 0)) : Tags(("flag", 0), ("progress", 0)))));
        if (alias) world.Observe(subject, new Observation(obj, "facet", new(Truth.True, "object", "room", Tags(("stock", stock), ("flag", 0), ("progress", 0)))));
        return (world, subject, obj, new("self", "object", Scene: "room"));
    }
    private static string Receipt(ActionReceipt result, WorldCore world, EntityId obj)
        => $"Succeeded={result.Succeeded}, Failure={result.Failure ?? "<none>"}, stock={world.ReadAuthority(obj).Tags!["stock"]}, worldRevision={result.WorldRevision}";
    private static string Plan(PlannedAction p)
        => $"first={p.First}, edges={Value.Print(p.Edges)}, score={p.Score}, depth={p.Depth}, expanded={p.Expansions}, stop={p.Stop}";
    public static void Main()
    {
        Console.WriteLine("Independent generic language/host review; no game, story or old Core access.");
        var aliasSpend = Setup(alias: true);
        Attempt("alias cumulative spend INPUT (seq (spend object stock 2) (spend facet stock 2)) authority stock=3", () =>
            Receipt(aliasSpend.World.Commit(aliasSpend.World.Prepare(aliasSpend.Subject,
                Effect.Parse("(seq (spend (ref \"object\") stock 2) (spend (ref \"facet\") stock 2))"), aliasSpend.Bindings)), aliasSpend.World, aliasSpend.Object));
        var aliasChange = Setup(alias: true);
        Attempt("alias cumulative change INPUT (seq (change object stock 1) (change facet stock 1)) authority stock=3", () =>
            Receipt(aliasChange.World.Commit(aliasChange.World.Prepare(aliasChange.Subject,
                Effect.Parse("(seq (change (ref \"object\") stock 1) (change (ref \"facet\") stock 1))"), aliasChange.Bindings)), aliasChange.World, aliasChange.Object));
        var aliasBan = Setup(alias: true);
        aliasBan.World.AcceptRules(aliasBan.Subject, new Rule("ban", Yes, RuleMode.Forbid, Effect.Parse("(change (ref \"object\") stock 1)"), null, 5, Truth.True));
        Attempt("alias ban INPUT ban change object; execute same entity via facet", () =>
            Receipt(aliasBan.World.Commit(aliasBan.World.Prepare(aliasBan.Subject,
                Effect.Parse("(change (ref \"facet\") stock 1)"), aliasBan.Bindings)), aliasBan.World, aliasBan.Object));

        var unknown = Setup(knowStock: false);
        unknown.World.AcceptRules(unknown.Subject, new Rule("try-change", Yes, RuleMode.Prefer, Effect.Parse("(change (slot target) stock 1)"), null, 5, Truth.True));
        Attempt("unknown numeric prediction INPUT missing observed stock; prefer change stock 1", () => Plan(unknown.World.Plan(unknown.World.Capture(unknown.Subject), unknown.Bindings, 2, 1)));
        Show("unknown numeric authority unchanged", unknown.World.ReadAuthority(unknown.Object).Tags!["stock"]);

        var unknownGuard = Setup();
        var guarded = Effect.Parse("(seq (when (tagged (slot target) unseen 1) (set (slot target) flag 1)) (change (slot target) progress 1))");
        unknownGuard.World.AcceptRules(unknownGuard.Subject,
            new Rule("guarded-step", Yes, RuleMode.Prefer, guarded, null, 1, Truth.True),
            new Rule("assumed-old-flag", Condition.Parse("(all (tagged (slot target) progress 1) (not (tagged (slot target) flag 1)))"), RuleMode.Prefer,
                Effect.Parse("(set (slot target) stock 8)"), null, 10, Truth.True));
        Attempt("unknown conditional prediction INPUT when unseen sets known flag, successor needs flag still 0", () => Plan(unknownGuard.World.Plan(unknownGuard.World.Capture(unknownGuard.Subject), unknownGuard.Bindings, 2, 10)));
        Show("unknown conditional snapshot retained", Value.Print(unknownGuard.World.Capture(unknownGuard.Subject).Facts));

        var budget = Setup();
        budget.World.AcceptRules(budget.Subject,
            new Rule("first-a", Yes, RuleMode.Prefer, Effect.Parse("(change (slot target) stock 1)"), null, 1, Truth.True),
            new Rule("first-b", Yes, RuleMode.Prefer, Effect.Parse("(change (slot target) progress 1)"), null, 2, Truth.True));
        foreach (var depth in new[] { 1, 2, 3 })
            Attempt($"planning budget INPUT depth={depth}, expansions=1, two root candidates", () => Plan(budget.World.Plan(budget.World.Capture(budget.Subject), budget.Bindings, depth, 1)));
        Attempt("planning invalid budget INPUT expansions=0", () => Plan(budget.World.Plan(budget.World.Capture(budget.Subject), budget.Bindings, 2, 0)));
        var proofGoal = Condition.Parse("(related (slot target) derived (slot actor))");
        budget.World.AcceptRules(budget.Subject, new Rule("derive", Yes, RuleMode.Infer, null, proofGoal, Acceptance: Truth.True));
        foreach (var amount in new[] { 1, 2 })
            Attempt($"deduction budget INPUT expansions={amount}, goal needs one accepted rule", () => budget.World.Prove(budget.World.Capture(budget.Subject), proofGoal, budget.Bindings, 2, amount));
        budget.World.AcceptRules(budget.Subject, new Rule("deny-derived", Yes, RuleMode.Infer, null, new Condition.Not(proofGoal), Acceptance: Truth.True));
        Attempt("deduction conflict INPUT support and deny same goal", () => budget.World.Prove(budget.World.Capture(budget.Subject), proofGoal, budget.Bindings, 2, 10));
        Attempt("deduction assumption INPUT unknown goal explicitly assumed", () => budget.World.Prove(budget.World.Capture(budget.Subject), Condition.Parse("(tagged (slot target) missing 1)"), budget.Bindings, 1, 10,
            [Condition.Parse("(tagged (slot target) missing 1)")]));

        var tokens = Setup();
        var before = tokens.World.Capture(tokens.Subject);
        var effect = Effect.Parse("(change (slot target) stock 1)");
        var firstToken = tokens.World.Prepare(tokens.Subject, effect, tokens.Bindings);
        var otherToken = tokens.World.Prepare(tokens.Subject, effect, tokens.Bindings);
        Show("token first commit", Receipt(tokens.World.Commit(firstToken), tokens.World, tokens.Object));
        Show("token second competing commit", Receipt(tokens.World.Commit(otherToken), tokens.World, tokens.Object));
        Show("token repeat commit", Receipt(tokens.World.Commit(firstToken), tokens.World, tokens.Object));
        Show("snapshot remains old after commit", tokens.World.Evaluate(before, Condition.Parse("(tagged (slot target) stock 4)"), tokens.Bindings));
        var cognition = tokens.World.Prepare(tokens.Subject, effect, tokens.Bindings);
        tokens.World.Conceive(tokens.Subject, "model", 0);
        Show("token after cognition mutation", Receipt(tokens.World.Commit(cognition), tokens.World, tokens.Object));
        var foreign = Setup();
        Attempt("foreign token", () => foreign.World.Commit(tokens.World.Prepare(tokens.Subject, effect, tokens.Bindings)));
        Attempt("foreign subject view", () => foreign.World.Evaluate(before, Yes, foreign.Bindings));
        Attempt("authority identifier in subject view", () => tokens.World.Evaluate(tokens.World.Capture(tokens.Subject), Condition.Parse("(exists (ref \"host-object\"))"), tokens.Bindings));

        var remote = Setup();
        var remoteRoom = new EntityId("host-remote-room");
        var remoteObj = new EntityId("host-remote-object");
        var remoteAbsent = new EntityId("host-remote-absent");
        remote.World.Define(remoteRoom, new("space"));
        remote.World.Define(remoteObj, new("object", remoteRoom, Tags: Tags(("secret", 1))));
        remote.World.Define(remoteAbsent, new("object", remoteRoom, Exists: false));
        remote.World.Observe(remote.Subject,
            new(remoteRoom, "remote-room", new(Truth.True, "space")),
            new(remoteObj, "remote", new(Truth.Unknown, "object", "remote-room")),
            new(remoteAbsent, "remote-absent", new(Truth.Unknown, "object", "remote-room")));
        foreach (var threshold in new[] { 0, 2 })
            Attempt($"remote require INPUT secret >= {threshold}", () => remote.World.Prepare(remote.Subject,
                new Effect.Require(new Condition.Tagged(new Noun.Reference("remote"), "secret", threshold)), remote.Bindings).Failure);
        Attempt("remote existence INPUT require exists remote (subject Unknown)", () => remote.World.Prepare(remote.Subject,
            new Effect.Require(new Condition.Exists(new Noun.Reference("remote"))), remote.Bindings).Failure);
        Attempt("remote existence INPUT require exists remote-absent (subject Unknown)", () => remote.World.Prepare(remote.Subject,
            new Effect.Require(new Condition.Exists(new Noun.Reference("remote-absent"))), remote.Bindings).Failure);

        var weights = Setup();
        var speaker = new EntityId("host-speaker");
        weights.World.Define(speaker, new("object"));
        weights.World.Observe(weights.Subject, new Observation(speaker, "speaker", new(Truth.True, Tags: Tags(("Trust", 3)))));
        var model = new CaseModel("model", [2], [], [new("h0", [0], Tags()), new("h1", [1], Tags())], ImmutableDictionary<int, int>.Empty);
        weights.World.ObserveClaims(weights.Subject, new EvidenceClaim("model", "object", 0, 1, EvidenceSource.Testimony, "speaker"));
        Show("confidence trusted testimony", string.Join(", ", weights.World.Reason(weights.World.Capture(weights.Subject), model).Distribution.OrderBy(p => p.Key)));
        weights.World.Observe(weights.Subject, new Observation(speaker, "speaker", new(Truth.True, Tags: Tags(("Trust", -3)))));
        Show("confidence distrusted testimony", string.Join(", ", weights.World.Reason(weights.World.Capture(weights.Subject), model).Distribution.OrderBy(p => p.Key)));
        weights.World.ObserveClaims(weights.Subject, new EvidenceClaim("model", "object", 0, 1, EvidenceSource.Perception));
        Show("confidence perception", string.Join(", ", weights.World.Reason(weights.World.Capture(weights.Subject), model).Distribution.OrderBy(p => p.Key)));
    }
}
