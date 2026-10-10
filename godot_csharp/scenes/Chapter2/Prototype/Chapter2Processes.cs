using System.Collections.Immutable;
using System.Text.Json;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;

namespace DualOntologyWorldEngine.Chapter2.Prototype;

public sealed partial class Game
{
    private readonly HashSet<(string Law, int Day, int Slot, Essence? Body)> _appliedLaws = [];
    private readonly WorkOccupancy _workOccupancy = new();
    private readonly HashSet<EventId> _deliveredClaims = [];
    private readonly HashSet<(int Day, string Hook)> _institutionHooks = [];

    /// <summary>Authored external stimuli deliver requests into the recipient's world.
    /// They never submit the recipient's response or record a completed action.</summary>
    private void RuntimeStimuli(string hook)
    {
        foreach(var signal in ExecutionDesign.GetProperty("stimuli").EnumerateObject())
        {
            var row=signal.Value;var key=(Day,"stimulus:"+signal.Name);
            if(row.GetProperty("hook").GetString()!=hook || _institutionHooks.Contains(key))continue;
            var title=AstText(row.GetProperty("title").GetString()!);
            var historyJob=Enum.Parse<Job>(row.GetProperty("history_job").GetString()!);
            var candidates=People.Where(p=>!p.Asleep && !p.World.Projections.Any(x=>x.Kind==ProjectionKind.Event && x.Name==title))
                .Select(p=>(Person:p,Ast:BindAst(FxOf(p),parameters:new Dictionary<string,Condition>
                { ["prior_action"]=new Condition.Constant(Events.Any(e=>e.Who==p.Id && e.Job==historyJob && e.Day<Day) ? Truth.True : Truth.False) })))
                .Where(c=>Ast.Evaluate(c.Ast.Condition(row.GetProperty("condition")),new(c.Person.Self,Scene:c.Person.Self.Parent))==Truth.True)
                .OrderByDescending(c=>c.Ast.ReadInteger(row.GetProperty("rank"))).ThenBy(c=>c.Person.Id).ToArray();
            if(candidates.Length==0)continue;
            var (recipient,ast)=candidates[0];var source=ResolveEntity(row.GetProperty("source").GetString()!);
            var voice=recipient.M[source];var place=SpaceOf(recipient.Body);
            // Recheck physical presence immediately before delivery. A stale cognition cannot
            // deliver a stimulus to somebody who has left its observed room.
            if(recipient.Self.Parent is not { } room || !ReferenceEquals(EssenceOf(recipient,room),place))continue;
            var text=AstText(row.GetProperty("text").GetString()!, ("人",recipient.Name));
            var receipt=new TestimonyEventV1<Essence?>(new()
            {
                Time=Now,TimeUnit=ObservationTimeUnitV1.ActionValue,Subject=source,Object=recipient.Soul,Source=source,Location=place,
                KnowledgeSource=ObservationSource.Perception,Phase=EventPhaseV1.Delivered,Outcome=EventOutcomeV1.Succeeded,Perceivers=[recipient.Soul]
            });
            Rt.RecordEvent(receipt);
            Observation.Observe(recipient.Soul,new(voice.Name,voice.Kind)
            { Target=voice,Source=ObservationSource.Perception,Time=Now,Tags=ImmutableDictionary<Tag,int?>.Empty.Add(Tags.Trust,ast.ReadInteger(row.GetProperty("trust"))) });
            Observation.Observe(recipient.Soul,new(title,ProjectionKind.Event)
            {
                Source=ObservationSource.Perception,Existence=Truth.True,Time=Now,Details=text,
                AddRelations=[(Relations.Subject,voice)],Procedure=ast.Effect(row.GetProperty("request")),Event=Rt.ProjectEvent(recipient.Soul,receipt)
            });
            _institutionHooks.Add(key);
            Log.Add(AstText(row.GetProperty("log").GetString()!, ("人",recipient.Name)));
        }
    }

    private void WorldProcesses(string hook)
    {
        foreach (var law in ExecutionDesign.GetProperty("laws").EnumerateObject())
        {
            var row = law.Value;
            if (row.GetProperty("hook").GetString() != hook) continue;
            var scope = row.GetProperty("once").GetString();
            var subjects = row.TryGetProperty("each", out var each) && each.GetString() == "characters" ? People.Cast<Person?>() : [null];
            foreach (var subject in subjects)
            {
                var key = (law.Name, scope == "world" ? 0 : Day, scope == "slot" ? (int)Slot : 0, subject?.Body);
                if (_appliedLaws.Contains(key)) continue;
                Dictionary<string, Noun>? bindings = subject is null ? null : new()
                { ["$body"] = View[subject.Body], ["$litter"] = View[Litter[subject.Pro.Dorm]] };
                var ast = BindAst(RealFx, bindings);
                var effect = new Effect.Sequence([new Effect.Require(ast.Condition(row.GetProperty("condition"))), ast.Effect(row.GetProperty("effect"))]);
                if (!WorldLawTransaction.Apply(Rt, effect, p => View.FirstOrDefault(v => ReferenceEquals(v.Value, p)).Key, Now, out _)) continue;
                _appliedLaws.Add(key);
                if (row.TryGetProperty("text", out var text)) Log.Add(AstText(text.GetString()!));
            }
        }
    }

    private void UpdateWorkOccupancy(ActionExecution? committed = null)
    {
        var occupied = new List<(ActionExecution, Essence, Essence)>();
        foreach (var recipe in ExecutionDesign.GetProperty("recipes").EnumerateObject())
        {
            var stations = recipe.Value.GetProperty("stations").EnumerateArray().Select(v => ResolveEntity(v.GetString()!)).ToArray();
            var tags = recipe.Value.GetProperty("actor_tags").EnumerateArray().Select(v => new Tag(v.GetString()!)).ToArray();
            foreach (var person in People)
            {
                var execution = ReferenceEquals(committed?.Intent.Actor, person.Self) ? committed : person.Exec;
                if (execution is not { Status: ExecutionStatus.Pending }) continue;
                for (var i = 0; i < stations.Length; i++)
                    if ((person.Body.Get<TemporaryStateProperty>()?.Override(tags[i]) ?? 0) > 0 && ReferenceEquals(SpaceOf(person.Body), SpaceOf(stations[i])))
                        occupied.Add((execution, person.Body, stations[i]));
            }
        }
        _workOccupancy.Update(Now, occupied);
    }

    private void CancelExecution(ActionExecution execution)
    {
        if (S.Cancel(execution) && _decisionAttempts.Remove(execution, out var experience))
        {
            var actor = People.Single(p => ReferenceEquals(p.Self, execution.Intent.Actor));
            ActionLearning.Observe(actor.Soul, actor.Self, experience, DecisionOutcomeV1.Interrupted, Now);
        }
        UpdateWorkOccupancy();
    }

    private readonly List<(string Id, Essence[] Actors)> _recipeCompletions = [];
    private void CompleteRecipes()
    {
        foreach (var recipe in ExecutionDesign.GetProperty("recipes").EnumerateObject())
        {
            var row = recipe.Value;
            var stations = row.GetProperty("stations").EnumerateArray().Select(v => ResolveEntity(v.GetString()!)).ToArray();
            foreach (var (first, second) in _workOccupancy.CompletedPairs(stations[0], stations[1], row.GetProperty("duration").GetDouble()).ToArray())
            {
                var actors = new[] { first.Actor, second.Actor };
                var bindings = new Dictionary<string, Noun> { ["$first"] = View[first.Actor], ["$second"] = View[second.Actor] };
                var parameters = new Dictionary<string, Condition>();
                if (row.TryGetProperty("knowledge", out var knowledge))
                    for (var i=0;i<actors.Length;i++)
                    {
                        var person=People.Single(p=>ReferenceEquals(p.Body,actors[i]));
                        var condition=BindAst(FxOf(person)).Condition(knowledge);
                        parameters[i==0 ? "first_competent" : "second_competent"] = new Condition.Constant(Ast.Evaluate(condition,new(person.Self,Scene:person.Self.Parent)));
                    }
                var ast = BindAst(RealFx, bindings, parameters:parameters);
                var effect = new Effect.Sequence([new Effect.Require(ast.Condition(row.GetProperty("condition"))), ast.Effect(row.GetProperty("effect"))]);
                if (WorldLawTransaction.Apply(Rt, effect, p => View.FirstOrDefault(v => ReferenceEquals(v.Value, p)).Key, Now, out _, [first.Execution.Events[^1].Fields.Id, second.Execution.Events[^1].Fields.Id]))
                {
                    _recipeCompletions.Add((recipe.Name, actors));
                }
                // This attempt is finished even if its resources or competence were insufficient.
                // Later work cannot retroactively redeem an old pair of action receipts.
                _workOccupancy.Consume(first, second);
            }
        }
    }

    /// <summary>Only delivered typed testimony can update the listener's known claim.
    /// A timer, acceptance receipt, or incomplete action cannot manufacture testimony.</summary>
    private void DeliverAuthoredClaims(ActionExecution execution)
    {
        var causes = execution.Events.Select(e => e.Fields.Id).ToHashSet();
        foreach (var e in Rt.EventAudit.OfType<TestimonyEventV1<Essence?>>().Where(e => e.Fields.Phase == EventPhaseV1.Delivered && e.Fields.Causes.Any(causes.Contains)).ToArray())
        {
            if (!_deliveredClaims.Add(e.Fields.Id)) continue;
            var listener = People.FirstOrDefault(p => ReferenceEquals(p.Soul, e.Fields.Object));
            if (listener is null) continue;
            foreach (var claim in e.Fields.Claims)
            {
                if (claim.Subject is not { } subject || claim.Tag is not { } tag || claim.Value is not { } value || !listener.M.TryGetValue(subject, out var known)) continue;
                Observation.Observe(listener.Soul, new(known.Name, known.Kind)
                {
                    Target = known, Source = ObservationSource.Testimony, Time = Now,
                    Tags = ImmutableDictionary<Tag, int?>.Empty.Add(tag, value), Event = Rt.ProjectEvent(listener.Soul, e)
                });
            }
        }
    }
}
