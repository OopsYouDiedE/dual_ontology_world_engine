using System.Collections.Immutable;
using DualOntologyWorldEngine.Core;

static class EventRegression
{
    static readonly Tag Value = new("value");
    static T Ok<T>(ContractResult<T> r) => r is ContractResult<T>.Success s ? s.Value : throw new Exception("Contract failed: " + r);
    static bool Rejects(Action a) { try { a(); return false; } catch (CognitionContractException) { return true; } }

    public static void Run(Action<bool, string> check)
    {
        foreach (var nested in new[] { false, true })
        {
            var w = new WorldRuntime(); var a = new SoulEssence("observer"); var b = new SoulEssence("receiver");
            var room = new Essence("room", ProjectionKind.Space); var next = new Essence("next", ProjectionKind.Space);
            var body = new Essence("body", ProjectionKind.Body); var other = new Essence("other", ProjectionKind.Body);
            var item = new Essence("unit"); var hidden = new Essence("hidden"); var way = new Essence("way", ProjectionKind.Boundary);
            body.Add(new SpatialProperty { Space = room }); other.Add(new SpatialProperty { Space = room });
            item.Add(new SpatialProperty { Space = room }); body.Add(new StateProperty()).Observable.Add(Value);
            item.Add(new StateProperty()).Observable.Add(Value); item.Get<StateProperty>()!.Values[Value] = 0;
            var passage = way.Add(new PassageProperty()); passage.Connections.Add(room, next); passage.Connections.Add(next, room);
            w.Project(a, "observer"); w.Project(b, "receiver"); var rr = w.Project(room, "room");
            var rn = w.Project(next, "next", nested ? rr : null);
            var rb = w.Project(body, "body"); var ro = w.Project(other, "other"); var ri = w.Project(item, "unit");
            w.Project(hidden, "hidden"); var rw = w.Project(way, "way");
            a.Relations.Add((Relations.Controls, body)); b.Relations.Add((Relations.Controls, other));
            var ap = w.Observe(a, [rr, rn, rb, ro, ri, rw]); var bp = w.Observe(b, [rr, ro, rb, ri]);
            using var session = new SimulationSession(w);
            var label = nested ? "event nested " : "event flat ";

            var cause = new ObservationRecordV1<Essence?>(new() { Subject = item, Source = body, Location = room,
                Time = 3, TimeUnit = ObservationTimeUnitV1.GameMinutes, KnowledgeSource = ObservationSource.Perception,
                Phase = EventPhaseV1.Delivered, Outcome = EventOutcomeV1.Succeeded, Perceivers = [a] });
            w.RecordEvent(cause); w.PublishEvent(a, cause, "rendered observation");
            TypedEventV1<Essence?>[] events = [new ActionEventV1<Essence?>(new()), new ObservationRecordV1<Essence?>(new()),
                new TestimonyEventV1<Essence?>(new()), new PropagationEventV1<Essence?>(new()), new PresentationEventV1<Essence?>(new())];
            foreach (var template in events)
            {
                var f = template.Fields with { Time = 7, TimeUnit = ObservationTimeUnitV1.GameMinutes, Subject = body,
                    Object = hidden, Location = room, Source = body, Origin = new("neutral.story", 1, 1, 1, 4),
                    KnowledgeSource = template is TestimonyEventV1<Essence?> ? ObservationSource.Testimony : ObservationSource.Perception,
                    Phase = EventPhaseV1.Delivered, Outcome = EventOutcomeV1.Succeeded, Topic = new("neutral.topic"),
                    Perceivers = [a], Causes = [cause.Fields.Id], Claims = [new(new("core.state"), item, null, room, Value, 1, Truth.True),
                        new(new("core.state"), hidden, null, room, Value, 9, Truth.True)] };
                var e = TypedEvents.WithFields(template, f); w.RecordEvent(e);
                var p = w.ProjectEvent(a, e)!;
                check(p is not null && p.GetType().GetGenericTypeDefinition() == e.GetType().GetGenericTypeDefinition()
                    && p.Fields.Id == f.Id && p.Fields.Time == 7 && p.Fields.TimeUnit == f.TimeUnit && p.Fields.Origin == f.Origin
                    && p.Fields.Subject == ap[2] && p.Fields.Object is null && p.Fields.Location == ap[0] && p.Fields.Source == ap[2]
                    && p.Fields.Topic == f.Topic && p.Fields.Causes.SequenceEqual(f.Causes) && p.Fields.Claims.Length == 1
                    && p.Fields.Perceivers.IsEmpty, label + template.GetType().Name + " complete authorized projection");
                check(w.ProjectEvent(b, e) is null, label + template.GetType().Name + " absent perception denies access");
                w.PublishEvent(a, e, "changed names; punctuation! altered result.");
            }
            var snapshot = Ok(a.World.CaptureSnapshot()); var rebound = Ok(CognitionGraph.Rebind(snapshot));
            check(TypedEvents.Of(snapshot).Any(e => e.Fields.Id == cause.Fields.Id) && rebound.World.History
                .Where(o => o.Event is not null).SelectMany(o => TypedEvents.References(o.Event!)).All(p => rebound.World.Contains(p!)),
                label + "snapshot preserves typed values and rebinds all references");
            var count = w.EventAudit.Count;
            check(Rejects(() => w.RecordEvent(cause)) && w.EventAudit.Count == count, label + "duplicate ID rejected atomically");
            check(Rejects(() => w.RecordEvent(new PresentationEventV1<Essence?>(cause.Fields with { Id = EventId.New(), Causes = [new("unknown")] })))
                && w.EventAudit.Count == count, label + "unknown cause rejected atomically");
            check(Rejects(() => w.RecordEvent(new ObservationRecordV1<Essence?>(cause.Fields with { Id = EventId.New(), Object = new Essence("foreign") })))
                && w.EventAudit.Count == count, label + "foreign world reference rejected atomically");
            var prior = a.World.Revision;
            check(Rejects(() => Observation.Observe(a, new("bad", ProjectionKind.Event) {
                Event = new TestimonyEventV1<Projection?>(new() { Subject = bp[1] }) })) && a.World.Revision == prior,
                label + "cross subject event reference never publishes");

            ActionExecution Execute(Projection actor, SoulEssence soul, Effect effect, Projection? basis = null)
            { var x = session.Submit(soul, new(actor, effect, 0, Basis: basis)); session.CTB.RunUntil(session.CTB.Now); return x; }
            var say = Execute(ap[2], a, new Effect.Say(ap[3], "unit reached 99",
                new Effect.Set(new Noun.Slot(RuleSlot.Actor), Value, 4), new Condition.Tagged(ap[4], Value, 99), new("neutral.request")));
            var said = (Effect.Say)say.Intent.Changes;
            check(Ast.Equivalent(said, said with { Text = "new rendering?!" }), label + "typed speech identity ignores rendering");
            var speech = b.World.History.Last(o => o.Event is TestimonyEventV1<Projection?>);
            check(say.Status == ExecutionStatus.Completed && item.Get<StateProperty>()!.Values[Value] == 0
                && speech.Event!.Fields.Claims.Single().Value == 99 && bp[3].Tags[Value] == 0, label + "speech is testimony not fact commit");
            var request = speech.Target; var testimony = speech.Event!.Fields;
            var acceptance = new ActionEventV1<Essence?>(new() { Subject = other, Object = item, Source = other,
                Time = session.CTB.Now, TimeUnit = ObservationTimeUnitV1.ActionValue,
                KnowledgeSource = ObservationSource.Introspection, Phase = EventPhaseV1.Accepted,
                Outcome = EventOutcomeV1.Pending, Topic = testimony.Topic, Perceivers = [b], Causes = [testimony.Id] });
            w.RecordEvent(acceptance);
            Observation.Observe(b, new(request.Name, request.Kind) { Target = request,
                Tags = ImmutableDictionary<Tag, int?>.Empty.Add(new("accepted"), 1), Event = w.ProjectEvent(b, acceptance) });
            check(other.Get<StateProperty>() is null && !request.Tags.ContainsKey(Tags.Responded), label + "acceptance does not execute request");
            var response = Execute(bp[1], b, request.Procedure!, request);
            check(response.Status == ExecutionStatus.Completed && other.Get<StateProperty>()!.Values[Value] == 4
                && response.Events.Last().Fields.Topic == new AuthorTermId("neutral.request")
                && response.Events.First().Fields.Causes.Contains(acceptance.Fields.Id), label + "request fulfillment uses typed topic and accepted cause");

            var cancelled = session.Submit(a, new(ap[2], new Effect.Set(ap[4], Value, 17), 10));
            check(session.Cancel(cancelled) && cancelled.Events.Last().Fields.Outcome == EventOutcomeV1.Cancelled
                && !cancelled.Events.Any(e => e.Fields.Phase == EventPhaseV1.Committed) && item.Get<StateProperty>()!.Values[Value] == 0,
                label + "cancel before activation produces no effect event");
            var failed = Execute(ap[2], a, new Effect.Sequence([new Effect.Set(ap[4], Value, 8),
                new Effect.Require(new Condition.Constant(Truth.False))]));
            check(failed.Status == ExecutionStatus.Failed && !failed.Events.Any(e => e.Fields.Phase == EventPhaseV1.Committed)
                && failed.Events.Last().Fields.Claims.IsEmpty && item.Get<StateProperty>()!.Values[Value] == 0, label + "failed atomic stage produces no effect event");
            var move = Execute(ap[2], a, new Effect.Act(ActionOp.Travel, ap[5]));
            check(move.Status == ExecutionStatus.Completed && move.Events.Any(e => e.Fields.Operation == ActionOp.Travel
                && e.Fields.Object == way && e.Fields.Location == next) && body.Get<SpatialProperty>()!.Space == next,
                label + "arrival follows committed travel destination");
            var pending = session.Submit(a, new(ap[2], new Effect.Sequence([new Effect.Set(ap[2], Value, 3),
                new Effect.Wait(20, new Effect.Set(ap[2], Value, 100))]), 0)); session.CTB.RunUntil(session.CTB.Now);
            session.Cancel(pending); session.CTB.RunUntil(session.CTB.Now + 50);
            check(body.Get<StateProperty>()!.Values[Value] == 3 && pending.Events.Last().Fields.Outcome == EventOutcomeV1.Cancelled,
                label + "cancel retains earlier commit and prevents deferred effect");
            var named = ap[4].Name; var structured = testimony.Claims.Single();
            Observation.Observe(a, new("renamed unit", ap[4].Kind) { Target = ap[4] });
            check(structured.Value == testimony.Claims.Single().Value && named != ap[4].Name
                && testimony.Topic == new AuthorTermId("neutral.request"), label + "rendering rename cannot change claim or request topic");
            var destroyed = Execute(ap[2], a, new Effect.Act(ActionOp.Destroy, ap[2]));
            check(destroyed.Status == ExecutionStatus.Completed && destroyed.Events.Last().Fields.Phase == EventPhaseV1.Completed
                && destroyed.Events.Last().Fields.Subject == body, label + "retired actor retains terminal event identity");
            Observation.Observe(a, new(ap[4].Name, ap[4].Kind) { Target = ap[4], Forget = true });
            check(!a.World.History.Any(o => o.Event?.Fields.Id == cause.Fields.Id)
                && !TypedEvents.Of(Ok(a.World.CaptureSnapshot())).Any(e => e.Fields.Id == cause.Fields.Id), label + "forget removes event dependency without authority recovery");
        }
    }
}
