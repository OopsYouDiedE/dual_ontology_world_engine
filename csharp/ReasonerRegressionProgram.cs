using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

/// <summary>Property checks for the shared reasoner and speech policy (metamorphic: vary one input, check the direction).</summary>
static class ReasonerRegressionProgram
{
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }

    static void Main()
    {
        var mark = new Tag("mark");
        var color = new Tag("color");
        // Two questions: who (A/B/C) and when (early/late); traces: a door mark and a cup colour.
        var hypotheses = new List<CaseHypothesis>();
        for (var who = 0; who < 3; who++)
            for (var when = 0; when < 2; when++)
                hypotheses.Add(new([who, when], ImmutableDictionary<string, int>.Empty
                    .Add("mark", who == 2 ? 1 : 0).Add("color", when)));
        var model = new CaseModel("t", ["who", "when"], [["A", "B", "C"], ["early", "late"]],
            [new("门", mark, "mark"), new("杯", color, "color")], hypotheses);

        SoulEssence Mind() => new("mind");
        Projection Seen(SoulEssence soul, string name, Tag tag, int value) => Observation.Observe(soul,
            new ObservationInput(name, ProjectionKind.Object) { Source = ObservationSource.Perception, Existence = Truth.True,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(tag, value) });
        Projection Person(SoulEssence soul, string name, int trust) => Observation.Observe(soul,
            new ObservationInput(name, ProjectionKind.Body) { Existence = Truth.True, Tags = ImmutableDictionary<Tag, int?>.Empty.Add(Tags.Trust, trust) });
        void Told(SoulEssence soul, Projection speaker, int q, int o, ObservationSource source = ObservationSource.Testimony) =>
            Observation.Observe(soul, new ObservationInput(model.ClaimName(q, o), ProjectionKind.Concept)
            { Source = source, Existence = Truth.True, AddRelations = [(Relations.Subject, speaker)] });

        var blank = Reasoner.Assess(Mind().World, model);
        Check(blank.Candidates.Length == 6 && Math.Abs(blank.Of(0, 0) - 1.0 / 3) < 1e-9, "no observation keeps every hypothesis");

        // Monotone information: every additional perceived trace keeps candidates a subset.
        var soul = Mind();
        Seen(soul, "门", mark, 0);
        var one = Reasoner.Assess(soul.World, model);
        Seen(soul, "杯", color, 1);
        var two = Reasoner.Assess(soul.World, model);
        Check(one.Candidates.Length == 4 && two.Candidates.Length == 2 && two.Candidates.All(one.Candidates.Contains),
            "perceived traces only narrow the candidates");
        Check(two.Of(0, 2) == 0 && Math.Abs(two.Of(1, 1) - 1) < 1e-9, "excluded options get no credence");

        // Testimony is weighed by trust in the speaker; distrust is ignored, never inverted.
        double After(int trust, ObservationSource source = ObservationSource.Testimony)
        {
            var s = Mind(); var who = Person(s, "speaker", trust); Told(s, who, 0, 1, source);
            return Reasoner.Assess(s.World, model).Of(0, 1);
        }
        var trustCurve = new[] { -3, -2, -1, 0, 1, 2, 3 }.Select(t => After(t)).ToArray();
        Check(trustCurve.Zip(trustCurve.Skip(1)).All(p => p.First <= p.Second + 1e-12), "more trust never lowers a claim's credence");
        Check(Math.Abs(trustCurve[0] - 1.0 / 3) < 1e-9, "a distrusted speaker leaves credence unchanged");
        Check(Math.Abs(After(3, ObservationSource.Corruption) - 1.0 / 3) < 1e-9, "words of corruption are not evidence");
        Check(After(0, ObservationSource.Inference) > After(3), "own inference outweighs any testimony");

        // Anti-omniscience: a mind that never perceived the trace is unaffected by other minds.
        var other = Mind(); Seen(other, "门", mark, 1);
        Check(Reasoner.Assess(Mind().World, model).Candidates.Length == 6, "another mind's perception does not leak");

        // Contradiction falls back to least-contradicted candidates and says so.
        var odd = Mind(); Seen(odd, "门", mark, 7);
        var contradicted = Reasoner.Assess(odd.World, model);
        Check(contradicted.Contradicted && contradicted.Candidates.Length == 6, "contradiction is reported, not hidden");

        // Speech policy: monotone in trust, honest speakers never lie, self-implication lowers telling.
        var habits = new SpeechHabits();
        int Forthcoming(SpeechAct act) => act switch { SpeechAct.Tell => 5, SpeechAct.Hedge => 4, SpeechAct.AskBack => 3, SpeechAct.Withhold => 2, SpeechAct.Refuse => 1, _ => 0 };
        foreach (var credence in new[] { 0.1, 0.5, 0.9 })
            foreach (var implicated in new[] { false, true })
                foreach (var liar in new[] { false, true })
                {
                    var h = habits with { NeverLies = !liar };
                    var acts = Enumerable.Range(-3, 7).Select(t => SpeechPolicy.Assess(credence, t, h, implicated)[0].Act).ToArray();
                    Check(acts.Zip(acts.Skip(1)).All(p => Forthcoming(p.First) <= Forthcoming(p.Second)),
                        $"trust never makes a speaker less forthcoming (credence {credence}, implicated {implicated}, may lie {liar})");
                    Check(liar || acts.All(a => a != SpeechAct.Lie), $"an honest speaker never lies (credence {credence}, implicated {implicated})");
                }
        Check(SpeechPolicy.Assess(0.9, 1, habits, true)[0].Score < SpeechPolicy.Assess(0.9, 1, habits, false).First(c => c.Act == SpeechAct.Tell).Score
              || SpeechPolicy.Assess(0.9, 1, habits, true)[0].Act != SpeechAct.Tell, "being implicated makes telling less attractive");
        Check(SpeechPolicy.Assess(0.5, 3, habits)[0].Stance == Stance.Unsure, "an unsure speaker cannot affirm");
        // An unconceived question stays at its default until the mind thinks of it.
        var defaulted = new CaseModel("t", ["who", "when"], [["A", "B", "C"], ["early", "late"]],
            [new("门", mark, "mark"), new("杯", color, "color")], hypotheses, [null, 0]);
        var naive = Mind(); Seen(naive, "杯", color, 1);
        var unconceived = Reasoner.Assess(naive.World, defaulted);
        Check(unconceived.Contradicted && unconceived.Of(1, 0) == 1, "an unconceived question keeps its default even against traces");
        Observation.Observe(naive, new ObservationInput(defaulted.ConceptName(1), ProjectionKind.Concept) { Source = ObservationSource.Inference, Existence = Truth.True });
        var conceived = Reasoner.Assess(naive.World, defaulted);
        Check(!conceived.Contradicted && conceived.Of(1, 1) == 1, "once conceived, traces decide the question");
        var chosen = SpeechPolicy.Choose(0.9, 3, habits, false, (act, _) => act == SpeechAct.Hedge);
        Check(chosen?.Act == SpeechAct.Hedge, "choice falls back to the best act the author wrote words for");
        Console.WriteLine($"TOTAL {checks} checks");
    }
}
