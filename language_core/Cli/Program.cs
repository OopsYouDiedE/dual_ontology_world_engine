using System.Collections.Immutable;
using DualOntology.Language;

try
{
    if (args.Length == 0 || args[0] == "demo") Demo.Run();
    else if (args[0] == "eval" && args.Length == 2) Console.WriteLine(Value.Print(Bootstrap.CreateSeed().Evaluate(args[1])));
    else if (args[0] == "run" && args.Length == 2) Console.WriteLine(Value.Print(Bootstrap.CreateSeed().Evaluate(File.ReadAllText(args[1]))));
    else if (args[0] == "self" && args.Length == 2)
    {
        var seed = Bootstrap.CreateSeed();
        var self = Bootstrap.LoadSelf(seed);
        Console.WriteLine(Value.Print(seed.Call("dv-load", new EvaluationLimits(100_000_000), new Value.List(Reader.ReadAll(args[1])), self)));
    }
    else throw new LanguageException("Usage: demo | eval <expression> | self <expression> | run <file>");
    return 0;
}
catch (Exception ex) when (ex is LanguageException or IOException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

internal static class Demo
{
    private static void Expect(bool condition, string message) { if (!condition) throw new LanguageException(message); }
    private static void Show(string name, object actual) => Console.WriteLine($"{name}: {actual}");
    private static void Reject(string name, Action action)
    {
        try { action(); }
        catch (LanguageException ex) { Show(name, ex.Message); return; }
        throw new LanguageException($"{name}: expected rejection.");
    }
    public static void Run()
    {
        Console.WriteLine("Language mechanism evidence; no game session or UI acceptance.");
        var seed = Bootstrap.CreateSeed();
        var self = Bootstrap.LoadSelf(seed);
        var expression = "(let* (x 6 f (fn* (y) (+ x y))) (f 7))";
        var direct = seed.Evaluate(expression);
        var hosted = seed.Call("dv-eval", Reader.Read(expression), self);
        Expect(Value.Same(direct, hosted) && direct == new Value.Number(13), "Self-hosted lexical evaluation differs.");
        Show("seed / self-loaded interpreter lexical closure", $"{Value.Print(direct)} / {Value.Print(hosted)}");
        // Invoke the newly loaded dv-eval closure, rather than merely reusing the seed's evaluator.
        var stage2Evaluator = seed.Call("dv-get", self, new Value.Symbol("dv-eval"));
        var stage2Root = seed.Call("dv-apply", seed.Call("dv-get", self, new Value.Symbol("dv-root")), Value.Seq());
        var nested = seed.Call("dv-apply", new EvaluationLimits(20_000_000), stage2Evaluator, Value.Seq(Reader.Read("(+ 20 22)"), stage2Root));
        Expect(nested == new Value.Number(42), "The self-loaded evaluator cannot evaluate code.");
        Show("evaluator loaded by its own language then invoked", Value.Print(nested));
        foreach (var code in new[] { "(if false 1 2)", "(do (def! n 3) (+ n 4))", "(let* (a 2 b (+ a 3)) (* a b))", "'(a (b 2))", "(do)", "(if false 1)", "(let* (f (fn* (x) x) g (fn* (x) x)) (list (= f g) (list? f)))" })
        {
            var a = seed.Evaluate(code);
            var b = seed.Call("dv-eval", Reader.Read(code), self);
            Expect(Value.Same(a, b), "Seed / self evaluator discrepancy.");
            Show(code, Value.Print(b));
        }
        Reject("duplicate parameters at seed", () => seed.Evaluate("(fn* (x x) x)"));
        Reject("duplicate parameters at self evaluator", () => seed.Call("dv-eval", Reader.Read("(fn* (x x) x)"), self));
        Reject("unbounded tail recursion", () => seed.Evaluate("(do (def! loop (fn* () (loop))) (loop))", new(1000)));
        Reject("unknown special form", () => seed.Evaluate("(unsupported 1)"));
        var occurs = seed.Evaluate("(unify '(var x) '(f (var x)) '())");
        Expect(occurs == Value.Null, "Occurs check allowed a cyclic term.");
        Show("unify x with f(x)", Value.Print(occurs));
        var chain = "(prove-ground '(p c) '((clause (p a) ()) (clause (p b) ((p a))) (clause (p c) ((p b)))) 4 '())";
        var proof = seed.Evaluate(chain);
        var selfProof = seed.Call("dv-eval", new EvaluationLimits(20_000_000), Reader.Read(chain), self);
        Expect(Value.Same(proof, selfProof) && Value.ItemsOf(proof)[0] == new Value.Symbol("proved"), "Horn proof differs across bootstrap stages.");
        Show("ground Horn reasoning at seed / self", Value.Print(proof));
        Show("absent Horn fact", Value.Print(seed.Evaluate("(prove-ground '(p absent) '() 4 '())")));
        Reject("variable Horn clause", () => seed.Evaluate("(prove-ground '(p a) '((clause (p (var x)) ())) 4 '())"));
        CompareProgram(seed, self, "subjective-inference.dv");
        CompareProgram(seed, self, "planning.dv");
        CompareProgram(seed, self, "deduction.dv");
        WorldEvidence();
        AliasEvidence();
        PredictionEvidence();
    }
    private static void CompareProgram(Interpreter seed, Value self, string filename)
    {
        var assembly = typeof(Demo).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(filename, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        var source = reader.ReadToEnd();
        var direct = seed.Evaluate(source);
        var hosted = seed.Call("dv-load", new EvaluationLimits(100_000_000), new Value.List(Reader.ReadAll(source)), self);
        Expect(Value.Same(direct, hosted), $"{filename}: seed / self inference differs.");
        var rows = Value.ItemsOf(hosted);
        if (filename == "subjective-inference.dv")
        {
            Expect(Math.Abs(Value.NumberOf(Value.ItemsOf(rows[0])[1]) - 7.0 / 9) < 1e-12 && Value.NumberOf(Value.ItemsOf(rows[1])[1]) == 0.5, "Trust discount did not change the same held claim.");
            Expect(Value.ItemsOf(Value.ItemsOf(rows[2])[1])[1] == Value.False && Value.ItemsOf(Value.ItemsOf(rows[3])[1])[1] == Value.True, "Modeled belief overwrote own belief.");
        }
        if (filename == "planning.dv")
        {
            var plan = Value.ItemsOf(rows[0]);
            Expect(Value.ItemsOf(plan[1]).Length == 2 && Value.NumberOf(plan[3]) == 12, "Planning did not return successor reward.");
        }
        if (filename == "deduction.dv")
        {
            Expect(Value.ItemsOf(Value.ItemsOf(rows[0])[1])[1] == Value.True && Value.ItemsOf(Value.ItemsOf(rows[1])[1])[3] == Value.True,
                "Derived fact or hypothetical flag missing.");
        }
        Show(filename + " at seed / self", Value.Print(hosted));
    }
    private static void WorldEvidence()
    {
        var world = new WorldCore();
        var room = new EntityId("authority-room");
        var body = new EntityId("authority-body");
        var box = new EntityId("authority-object");
        var hidden = new EntityId("authority-hidden");
        world.Define(room, new("space"));
        world.Define(body, new("body", room));
        world.Define(box, new("object", room, Tags: ImmutableDictionary<string, int>.Empty.Add("stock", 2)));
        world.Define(hidden, new("object", room, Tags: ImmutableDictionary<string, int>.Empty.Add("secret", 9)));
        var subject = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        world.CreateSubject(subject, body);
        world.Observe(subject,
            new(room, "room", new(Truth.True, "space")),
            new(body, "self", new(Truth.True, "body", "room")),
            new(box, "object", new(Truth.True, "object", "room", ImmutableDictionary<string, int>.Empty.Add("stock", 9))));
        var bindings = new ActionBindings("self", "object", Scene: "room");
        var before = world.Capture(subject);
        var unknown = world.Evaluate(before, Condition.Parse("(tagged (slot target) unobserved 1)"), bindings);
        Expect(unknown == Truth.Unknown, "Missing observed tag became false.");
        Show("unobserved tag", unknown);
        Reject("authority identity in subject condition", () => world.Evaluate(before, Condition.Parse("(exists (ref \"authority-hidden\"))"), bindings));
        Expect(!Value.Print(before.Facts).Contains("authority-", StringComparison.Ordinal), "Snapshot exposed objective identity.");
        var action = Effect.Parse("(change (slot target) stock 1)");
        var condition = new Condition.Constant(Truth.True);
        world.AcceptRules(subject,
            new("want", condition, RuleMode.Prefer, action, null, 4, Truth.True),
            new("ban", condition, RuleMode.Forbid, action, null, 5, Truth.True),
            new("allow", condition, RuleMode.Permit, action, null, 5, Truth.True),
            new("ignored", condition, RuleMode.Prefer, Effect.Parse("(set (slot target) stock 99)"), null, 99, Truth.Unknown));
        var assessments = world.Assess(world.Capture(subject), bindings);
        Expect(assessments.Length == 1 && assessments[0].Score == 4 && !assessments[0].Forbidden, "Permit introduced candidate or score.");
        Show("prefer 4 / forbid 5 / permit 5", $"score={assessments[0].Score}, forbidden={assessments[0].Forbidden}");
        var failed = world.Commit(world.Prepare(subject, Effect.Parse("(seq (set (slot target) stock 100) (spend (slot target) stock 101))"), bindings));
        Expect(!failed.Succeeded && world.ReadAuthority(box).Tags!["stock"] == 2, "Failed sequence left a partial write.");
        Show("set 100 then spend 101", $"failure={failed.Failure}, authority stock={world.ReadAuthority(box).Tags!["stock"]}");
        var actualSpend = world.Commit(world.Prepare(subject, Effect.Parse("(spend (slot target) stock 3)"), bindings));
        Expect(!actualSpend.Succeeded, "Subject belief bypassed objective resource validation.");
        Show("subject believes 9 / authority has 2 / spend 3", actualSpend.Failure!);
        var prepared = world.Prepare(subject, action, bindings);
        var concurrent = world.Prepare(subject, action, bindings);
        var commit = world.Commit(prepared);
        var stale = world.Commit(concurrent);
        var duplicate = world.Commit(prepared);
        Expect(commit.Succeeded && !stale.Succeeded && !duplicate.Succeeded && world.ReadAuthority(box).Tags!["stock"] == 3, "Commit isolation failed.");
        Show("prepared competing stages", $"first={commit.Succeeded}, second={stale.Failure}, repeated={duplicate.Failure}");
        Expect(world.Evaluate(before, Condition.Parse("(tagged (slot target) stock 9)"), bindings) == Truth.True, "Execution rewrote old subjective snapshot.");
        Show("old subjective snapshot after commit", world.Evaluate(before, Condition.Parse("(tagged (slot target) stock 9)"), bindings));
        Reject("unsupported wait effect", () => Effect.Parse("(wait 1 (set (slot target) stock 1))"));
        var originalRevision = world.Capture(subject).Revision;
        Reject("unclosed observation graph", () => world.Observe(subject, new Observation(box, "object", new(ParentHandle: "foreign-handle"))));
        Expect(world.Capture(subject).Revision == originalRevision, "Invalid observation changed cognition.");
        Show("rejected observation", "subject revision unchanged");
        var plan = world.Plan(world.Capture(subject), bindings, 1, 32);
        Expect(plan.First is not null && plan.Depth == 1 && world.ReadAuthority(box).Tags!["stock"] == 3, "Planning changed authority or failed to propose the accepted method.");
        Show("formal API self-hosted plan", $"first={plan.First}, depth={plan.Depth}, authority stock=3");
        var conclusion = Condition.Parse("(related (slot target) follows (slot actor))");
        world.AcceptRules(subject, new Rule("deduced", Condition.Parse("(tagged (slot target) stock 9)"), RuleMode.Infer, null, conclusion, Acceptance: Truth.True));
        var proof = world.Prove(world.Capture(subject), conclusion, bindings, 3, 100);
        Expect(proof.Truth == Truth.True && proof.RuleBasis.Contains("deduced"), "Formal API did not use its own inference rule.");
        Show("formal API self-hosted deduction", $"{proof.Truth}, basis={string.Join(',', proof.RuleBasis)}, hypothetical={proof.Hypothetical}");
        var cognitionToken = world.Prepare(subject, action, bindings);
        world.ObserveClaims(subject, new EvidenceClaim("generic_case", "object", 0, 1, EvidenceSource.Testimony, "self"));
        var caseModel = new CaseModel("generic_case", [2], [],
            [new("h0", [0], ImmutableDictionary<string, int>.Empty), new("h1", [1], ImmutableDictionary<string, int>.Empty)], ImmutableDictionary<int, int>.Empty);
        var belief = world.Reason(world.Capture(subject), caseModel);
        Expect(Math.Abs(belief.Credence[0][1] - 2.0 / 3) < 1e-12, "Formal API weighted case differs.");
        Show("formal API self-hosted weighted credence", belief.Credence[0][1]);
        var staleCognition = world.Commit(cognitionToken);
        Expect(staleCognition.Failure == "stale-cognition", "Prepared token ignored changed cognition.");
        Show("uncommitted token after cognition update", staleCognition.Failure!);
        var remoteRoom = new EntityId("authority-remote-room");
        var remote = new EntityId("authority-remote");
        world.Define(remoteRoom, new("space"));
        world.Define(remote, new("object", remoteRoom, Tags: ImmutableDictionary<string, int>.Empty.Add("secret", 3)));
        world.Observe(subject, new(remoteRoom, "remote-room", new(Truth.True, "space")), new(remote, "remote", new(Truth.True, "object", "remote-room")));
        var remoteQuery = world.Commit(world.Prepare(subject, Effect.Parse("(require (tagged (ref \"remote\") secret 2))"), bindings));
        Expect(remoteQuery.Failure == "out-of-reach", "Remote authority guard became a subjective truth oracle.");
        Show("remote authority guard", remoteQuery.Failure!);
        var absentRemote = new EntityId("authority-absent-remote");
        world.Define(absentRemote, new("object", remoteRoom, Exists: false));
        world.Observe(subject, new Observation(absentRemote, "remote-absent", new(Truth.Unknown, "object", "remote-room")));
        var presentQuery = world.Prepare(subject, Effect.Parse("(require (exists (ref \"remote\")))"), bindings);
        var absentQuery = world.Prepare(subject, Effect.Parse("(require (exists (ref \"remote-absent\")))"), bindings);
        Expect(presentQuery.Failure == "out-of-reach" && absentQuery.Failure == "out-of-reach", "Remote existence escaped the access boundary.");
        Show("remote existence present / absent", $"{presentQuery.Failure} / {absentQuery.Failure}");
        var skipped = world.Commit(world.Prepare(subject, Effect.Parse("(when (const false) (spend (ref \"remote\") secret 100))"), bindings));
        Expect(skipped.Succeeded, "Skipped conditional checked its unreachable write.");
        Show("false conditional with unreachable body", skipped.Succeeded);
        world.Observe(subject, new Observation(box, "facet", new(Truth.True, "object", "room", ImmutableDictionary<string, int>.Empty.Add("stock", 9))));
        var alias = world.Commit(world.Prepare(subject, action, bindings with { Target = "facet" }));
        Expect(alias.Succeeded && world.ReadAuthority(box).Tags!["stock"] == 4, "Unchanged alias blocked a real entity write.");
        Show("one observed facet changes a shared entity", $"{alias.Succeeded}, authority stock=4");
        world.AcceptRules(subject, new Rule("strong-ban", condition, RuleMode.Forbid, action, null, 6, Truth.True));
        var forbidden = world.Commit(world.Prepare(subject, action, bindings));
        Expect(!forbidden.Succeeded && forbidden.Failure == "forbidden", "Forbidden action crossed execution boundary.");
        Show("accepted ban stronger than permit at execution", forbidden.Failure!);
    }

    private static (WorldCore World, Guid Subject, EntityId Object, ActionBindings Bindings) GenericWorld(bool alias, bool knowStock)
    {
        var world = new WorldCore();
        var room = new EntityId("host-room");
        var body = new EntityId("host-body");
        var obj = new EntityId("host-object");
        var tags = ImmutableDictionary<string, int>.Empty.Add("stock", 3).Add("flag", 0).Add("progress", 0);
        world.Define(room, new("space"));
        world.Define(body, new("body", room));
        world.Define(obj, new("object", room, Tags: tags));
        var subject = Guid.NewGuid();
        world.CreateSubject(subject, body);
        world.Observe(subject,
            new(room, "room", new(Truth.True, "space")),
            new(body, "self", new(Truth.True, "body", "room")),
            new(obj, "object", new(Truth.True, "object", "room", knowStock ? tags : tags.Remove("stock"))));
        if (alias) world.Observe(subject, new Observation(obj, "facet", new(Truth.True, "object", "room", tags)));
        return (world, subject, obj, new("self", "object", Scene: "room"));
    }
    private static void AliasEvidence()
    {
        var (world, subject, obj, bindings) = GenericWorld(true, true);
        var spend = world.Commit(world.Prepare(subject, Effect.Parse("(seq (spend (ref \"object\") stock 2) (spend (ref \"facet\") stock 2))"), bindings));
        Expect(spend.Failure == "resource" && world.ReadAuthority(obj).Tags!["stock"] == 3, "Alias spends did not share one staged resource.");
        Show("alias cumulative spend 2 + 2 from stock 3", $"{spend.Failure}, authority stock=3");
        var change = world.Commit(world.Prepare(subject, Effect.Parse("(seq (change (ref \"object\") stock 1) (change (ref \"facet\") stock 1))"), bindings));
        Expect(change.Succeeded && world.ReadAuthority(obj).Tags!["stock"] == 5, "Alias changes did not accumulate.");
        Show("alias cumulative change 1 + 1 from stock 3", "authority stock=5");
        world.AcceptRules(subject, new Rule("explicit-ban", new Condition.Constant(Truth.True), RuleMode.Forbid,
            Effect.Parse("(change (ref \"object\") stock 1)"), null, 8, Truth.True));
        var forbidden = world.Commit(world.Prepare(subject, Effect.Parse("(change (ref \"facet\") stock 1)"), bindings));
        Expect(forbidden.Failure == "forbidden" && world.ReadAuthority(obj).Tags!["stock"] == 5, "Alias bypassed an accepted ban.");
        Show("explicit ban through another facet", forbidden.Failure!);
    }
    private static void PredictionEvidence()
    {
        var (world, subject, obj, bindings) = GenericWorld(false, false);
        var yes = new Condition.Constant(Truth.True);
        world.AcceptRules(subject,
            new Rule("guarded", yes, RuleMode.Prefer, Effect.Parse("(seq (when (tagged (slot target) unseen 1) (set (slot target) flag 1)) (change (slot target) progress 1))"), null, 1, Truth.True),
            new Rule("false-successor", Condition.Parse("(all (tagged (slot target) progress 1) (not (tagged (slot target) flag 1)))"), RuleMode.Prefer,
                Effect.Parse("(set (slot target) stock 8)"), null, 10, Truth.True),
            new Rule("unknown-number", yes, RuleMode.Prefer, Effect.Parse("(change (slot target) stock 1)"), null, 5, Truth.True));
        var before = world.Capture(subject);
        var unknown = world.Plan(before, bindings, 2, 10);
        Expect(unknown.First is null && unknown.PredictionFailures.Contains("unknown-condition") && unknown.PredictionFailures.Contains("unknown-state")
            && Value.Same(before.Facts, world.Capture(subject).Facts) && world.ReadAuthority(obj).Tags!["stock"] == 3, "Unknown prediction became a definite successor.");
        Show("unknown guard / unknown numeric prediction", $"stop={unknown.Stop}, rejected={string.Join(',', unknown.PredictionFailures)}, score={unknown.Score}");
        world.AcceptRules(subject,
            new Rule("known-a", yes, RuleMode.Prefer, Effect.Parse("(change (slot target) progress 1)"), null, 2, Truth.True),
            new Rule("known-b", yes, RuleMode.Prefer, Effect.Parse("(change (slot target) flag 1)"), null, 3, Truth.True));
        var bounded = world.Plan(world.Capture(subject), bindings, 3, 1);
        Expect(bounded.First is not null && bounded.Depth == 1 && bounded.ComparedDepth == 1 && bounded.Expansions == 1
            && bounded.Stop == "complete-layer-budget", "Initial state expansion escaped the declared budget.");
        Show("planning budget 1 including initial state", $"depth={bounded.Depth}, compared={bounded.ComparedDepth}, expanded={bounded.Expansions}, stop={bounded.Stop}");
    }
}
