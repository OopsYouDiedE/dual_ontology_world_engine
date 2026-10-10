using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DualOntologyWorldEngine.Core;

static class EventSaveRegression
{
    static T Ok<T>(ContractResult<T> result) => result is ContractResult<T>.Success s ? s.Value
        : throw new Exception("Event save failed: " + ((ContractResult<T>.Failure)result).Error);
    static ContractError Error<T>(ContractResult<T> result) => result is ContractResult<T>.Failure f ? f.Error
        : throw new Exception("Expected event save rejection");
    static string Authority(WorldRuntime runtime) => JsonSerializer.Serialize(runtime.EventAudit.Select(e => new
    { kind = e.GetType().Name, fields = TypedEvents.Map(e, r => r?.Id).Fields }));
    static string Semantic(RestoredRuntimeV1 restored) => JsonSerializer.Serialize(new
    {
        authority = Authority(restored.Runtime),
        subjects = restored.Entities.Values.OfType<SoulEssence>().OrderBy(s => s.Id.Value, StringComparer.Ordinal).Select(s => new
        {
            id = s.Id.Value, graph = Convert.ToBase64String(Ok(CognitionCodec.Encode(Ok(restored.Runtime.CaptureCognitionGraph(s))))),
            audit = Convert.ToBase64String(Ok(CognitionCodec.EncodeAudit(Ok(restored.Runtime.CaptureCognitionAudit(s)))))
        })
    });

    public static int Child(string path)
    {
        var restored = Ok(WorldRuntime.Restore(File.ReadAllBytes(path)));
        var expected = File.ReadAllText(path + ".expected");
        if(Semantic(restored) != expected) throw new Exception("Fresh-process event identity/causality/permission mismatch");
        var second = Ok(WorldRuntime.Restore(Ok(restored.Runtime.Save())));
        if(Semantic(second) != expected || second.Runtime.EventAudit.Count != restored.Runtime.EventAudit.Count
            || second.Runtime.EventAudit.Select(e => e.Fields.Id).Distinct().Count() != second.Runtime.EventAudit.Count)
            throw new Exception("Repeated event restore registered duplicate events");
        if(restored.Runtime.EventAudit.SelectMany(TypedEvents.References).Any(e => !ReferenceEquals(e, restored.Entities[e!.Id])))
            throw new Exception("Authoritative references were not rebound to new-process entities");
        Console.WriteLine("EVENT CHILD RESTORED"); return 0;
    }

    sealed record UnknownEvent<T>(EventFieldsV1<T> Data) : TypedEventV1<T>(Data);

    public static void Run(Action<bool, string> check)
    {
        foreach(var nested in new[] { false, true })
        {
            var label = nested ? "event-save nested " : "event-save flat ";
            var folder = Path.Combine("tmp", "save-events-wire", nested ? "nested" : "flat"); Directory.CreateDirectory(folder);
            void RetainRejection(string layer, string name, byte[] rejected, ContractError error)
            {
                var path = Path.Combine(folder, layer + "-" + name);
                File.WriteAllBytes(path + ".json", rejected); File.WriteAllText(path + ".error.txt", error.ToString());
            }
            var runtime = new WorldRuntime(); var soul = new SoulEssence(new("observer"), "observer");
            var other = new SoulEssence(new("other"), "other"); var body = new Essence(new("body"), "body", ProjectionKind.Body);
            var root = new Essence(new("root"), "root", ProjectionKind.Space); var room = new Essence(new("room"), "room", ProjectionKind.Space);
            var item = new Essence(new("item"), "unit"); var hidden = new Essence(new("hidden"), "hidden");
            body.Add(new SpatialProperty { Space = room }); item.Add(new SpatialProperty { Space = room });
            soul.Relations.Add((Relations.Controls, body)); runtime.Project(soul, "observer"); runtime.Project(other, "other");
            var rRoot = runtime.Project(root, "root"); var rRoom = runtime.Project(room, "room", nested ? rRoot : null);
            var rBody = runtime.Project(body, "body"); var rItem = runtime.Project(item, "unit"); runtime.Project(hidden, "hidden");
            var known = runtime.Observe(soul, [rRoot, rRoom, rBody, rItem]); runtime.Observe(other, [rRoom]);
            var cause = new ObservationRecordV1<Essence?>(new() { Id = new("cause"), Subject = item, Source = body,
                Location = room, Time = 2, TimeUnit = ObservationTimeUnitV1.GameMinutes, Phase = EventPhaseV1.Delivered,
                Outcome = EventOutcomeV1.Succeeded, Perceivers = [soul] });
            runtime.RecordEvent(cause); var causeProjection = runtime.PublishEvent(soul, cause)!;
            TypedEventV1<Essence?>[] kinds = [new ActionEventV1<Essence?>(new()), new ObservationRecordV1<Essence?>(new()),
                new TestimonyEventV1<Essence?>(new()), new PropagationEventV1<Essence?>(new()), new PresentationEventV1<Essence?>(new())];
            var previous = cause.Fields.Id; var ids = new HashSet<EventId> { previous };
            foreach(var (template, index) in kinds.Select((e, i) => (e, i)))
            {
                var e = TypedEvents.WithFields(template, new EventFieldsV1<Essence?> { Id = new("step:" + index), Subject = body,
                    Object = hidden, Source = body, Location = room, Time = 3 + index, TimeUnit = ObservationTimeUnitV1.GameDays,
                    Origin = new("neutral.story", 2, 1, 2, 9), KnowledgeSource = ObservationSource.Testimony,
                    Phase = EventPhaseV1.Completed, Outcome = EventOutcomeV1.Succeeded, Operation = ActionOp.Inspect,
                    Topic = new("neutral.topic"), Perceivers = [soul], Causes = [previous],
                    Claims = [new(new("core.state"), item, null, room, new("value"), index, Truth.True),
                        new(new("core.state"), hidden, null, room, new("value"), 99, Truth.False)] });
                runtime.RecordEvent(e); runtime.PublishEvent(soul, e); previous = e.Fields.Id; ids.Add(previous);
            }
            var secret = new PresentationEventV1<Essence?>(new() { Id = new("secret"), Subject = hidden, Perceivers = [other] });
            runtime.RecordEvent(secret); runtime.PublishEvent(other, secret);
            var successor = new PropagationEventV1<Essence?>(new() { Id = new("successor"), Subject = body, Perceivers = [soul], Causes = [previous, secret.Fields.Id] });
            runtime.RecordEvent(successor); runtime.PublishEvent(soul, successor); ids.Add(successor.Fields.Id);
            var policy = Observation.Observe(soul, new("policy") { Procedure = new Effect.Say(known[2], "neutral",
                new Effect.Set(known[3], new("value"), 1), new Condition.Tagged(known[3], new("value"), 4), new("neutral.topic")) });
            runtime.Remove(hidden); // The audit must retain valid references to retired entity identities.

            var archive = Ok(runtime.CaptureCognitionGraph(soul)); var bytes = Ok(CognitionCodec.Encode(archive));
            var decoded = Ok(CognitionCodec.Decode(bytes)); var graph = Ok(CognitionGraph.Rebind(decoded));
            check(Ok(CognitionCodec.Encode(decoded)).SequenceEqual(bytes) && graph.World.History.Where(o => o.Event is not null)
                .SelectMany(o => TypedEvents.References(o.Event!)).All(p => graph.World.Contains(p!)), label + "five event kinds wire roundtrip/rebind");
            var visible = TypedEvents.Of(decoded.SubjectSnapshot).Where(e => ids.Contains(e.Fields.Id)).ToArray();
            check(visible.Length == 7 && visible.All(e => e.Fields.Perceivers.IsEmpty && e.Fields.Object is null && e.Fields.Claims.Length <= 1)
                && visible[^1].Fields.Causes.SequenceEqual([previous]), label + "wire preserves authorized claims and hides secret cause");
            var say = graph.Projections[policy.SubjectId!.Value].Procedure as Effect.Say;
            check(say?.Claim is Condition.Tagged { Minimum: 4 } && say.Topic == new AuthorTermId("neutral.topic"), label + "Say claim/topic wire preserved");
            var audit = Ok(runtime.CaptureCognitionAudit(soul)); var auditBytes = Ok(CognitionCodec.EncodeAudit(audit));
            var auditDecoded = Ok(CognitionCodec.DecodeAudit(auditBytes));
            check(Ok(CognitionCodec.EncodeAudit(auditDecoded)).SequenceEqual(auditBytes)
                && Ok(CognitionCodec.Encode(archive with { SubjectSnapshot = Ok(runtime.ReconstructCognitionAudit(auditDecoded, archive.SubjectSnapshot.Revision)) })).SequenceEqual(bytes),
                label + "event audit inputs/changes/history reconstruct exactly");

            void RejectGraph(string name, Action<JsonObject> mutate, ContractErrorCode code)
            {
                var rootJson = JsonNode.Parse(bytes)!.AsObject(); mutate(rootJson);
                var rejected = JsonSerializer.SerializeToUtf8Bytes(rootJson); var error = Error(CognitionCodec.Decode(rejected));
                RetainRejection("graph", name, rejected, error);
                check(error.Code == code, label + name);
            }
            RejectGraph("unknown event kind Unsupported", r => r["subject_snapshot"]!["visible_history"]![0]!["event"]!["kind"] = "custom", ContractErrorCode.Unsupported);
            RejectGraph("unknown event field Unsupported", r => r["subject_snapshot"]!["visible_history"]![0]!["event"]!["extra"] = 1, ContractErrorCode.Unsupported);
            RejectGraph("unknown event phase Unsupported", r => r["subject_snapshot"]!["visible_history"]![0]!["event"]!["phase"] = "custom", ContractErrorCode.Unsupported);
            RejectGraph("event claim numeric overflow Unsupported", r => r["subject_snapshot"]!["visible_history"]!.AsArray().First(o => o!["event"]!["claims"]!.AsArray().Count > 0)!["event"]!["claims"]![0]!["value"] = 2147483648L, ContractErrorCode.Unsupported);
            RejectGraph("unknown subject cause InvalidReference", r => r["subject_snapshot"]!["visible_history"]![0]!["event"]!["causes"]!.AsArray().Add("absent"), ContractErrorCode.InvalidReference);
            RejectGraph("missing event reference InvalidReference", r => r["subject_snapshot"]!["visible_history"]![0]!["event"]!["subject"]!["local"] = Guid.NewGuid().ToString("D"), ContractErrorCode.InvalidReference);
            RejectGraph("missing causal source dependency InvalidReference", r =>
            {
                var o = r["subject_snapshot"]!["visible_history"]!.AsArray().First(o => o!["event"]!["id"]!.GetValue<string>() == "step:0")!;
                var deps = o["dependencies"]!.AsArray(); deps.Remove(deps.Single(p => p!["local"]!.GetValue<string>() == causeProjection.SubjectId!.Value.Local.ToString("D")));
            }, ContractErrorCode.InvalidReference);
            var badEvent = new UnknownEvent<SubjectProjectionId?>(visible[0].Fields);
            var unknown = archive.SubjectSnapshot.VisibleHistory[0] with { Event = badEvent };
            check(Error(CognitionCodec.Encode(archive with { SubjectSnapshot = archive.SubjectSnapshot with { VisibleHistory = archive.SubjectSnapshot.VisibleHistory.SetItem(0, unknown) } })).Code == ContractErrorCode.Unsupported,
                label + "unknown CLR event subclass Unsupported");

            var runtimeBytes = Ok(runtime.Save()); var loaded = Ok(WorldRuntime.Restore(runtimeBytes));
            check(Authority(loaded.Runtime) == Authority(runtime) && loaded.Runtime.EventAudit.SelectMany(TypedEvents.References)
                .All(e => ReferenceEquals(e, loaded.Entities[e!.Id])), label + "authority audit IDs/causes/retired references restored");
            var loadedSoul = (SoulEssence)loaded.Entities[soul.Id]; var loadedOther = (SoulEssence)loaded.Entities[other.Id];
            var last = loaded.Runtime.EventAudit.Single(e => e.Fields.Id == successor.Fields.Id);
            check(loaded.Runtime.ProjectEvent(loadedOther, last) is null && loaded.Runtime.ProjectEvent(loadedSoul, last)!.Fields.Causes.SequenceEqual([previous]),
                label + "loaded authority cannot broaden permission projection");
            var count = loaded.Runtime.EventAudit.Count;
            try { loaded.Runtime.RecordEvent(last); throw new Exception("Expected duplicate event ID"); }
            catch(CognitionContractException e) { check(e.Error.Code == ContractErrorCode.DuplicateIdentity && loaded.Runtime.EventAudit.Count == count, label + "loaded IDs cannot be registered twice"); }
            void RejectRuntime(string name, Action<JsonObject> mutate, ContractErrorCode code)
            {
                var r = JsonNode.Parse(runtimeBytes)!.AsObject(); mutate(r);
                var rejected = JsonSerializer.SerializeToUtf8Bytes(r); var error = Error(WorldRuntime.Restore(rejected));
                RetainRejection("authority", name, rejected, error);
                check(error.Code == code, label + name);
            }
            RejectRuntime("unknown authority kind Unsupported", r => r["events"]![0]!["kind"] = "custom", ContractErrorCode.Unsupported);
            RejectRuntime("declared event capability requires authority array", r => r.Remove("events"), ContractErrorCode.MalformedData);
            RejectRuntime("duplicate authority ID DuplicateIdentity", r => r["events"]!.AsArray().Add(r["events"]![0]!.DeepClone()), ContractErrorCode.DuplicateIdentity);
            RejectRuntime("unknown authority cause InvalidReference", r => r["events"]![0]!["causes"]!.AsArray().Add("absent"), ContractErrorCode.InvalidReference);
            RejectRuntime("forward authority cause InvalidReference", r => r["events"]![0]!["causes"]!.AsArray().Add("step:0"), ContractErrorCode.InvalidReference);
            RejectRuntime("missing authority entity InvalidReference", r => r["events"]![0]!["subject"] = "absent", ContractErrorCode.InvalidReference);
            check(Error(WorldRuntime.Restore(runtimeBytes, new(MaxHistoryEntries: 0))).Code == ContractErrorCode.LimitExceeded, label + "authority history limit enforced");

            var path = Path.GetFullPath(Path.Combine(folder, "runtime.json")); File.WriteAllBytes(path, runtimeBytes); File.WriteAllText(path + ".expected", Semantic(loaded));
            var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            if(Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(typeof(EventSaveRegression).Assembly.Location);
            start.ArgumentList.Add("--event-wire-child"); start.ArgumentList.Add(path);
            using var child = Process.Start(start)!; var stdout = child.StandardOutput.ReadToEnd(); var stderr = child.StandardError.ReadToEnd();
            check(child.WaitForExit(60000) && child.ExitCode == 0 && stdout.Contains("EVENT CHILD RESTORED"), label + "fresh process event semantics/no duplicate registration: " + stderr);
            File.WriteAllText(Path.Combine(folder, "child.log"), stdout + stderr);

            var beforeForget = loadedSoul.World.Revision; var authorityBefore = Authority(loaded.Runtime);
            Observation.Observe(loadedSoul, new("cause", ProjectionKind.Event) { Target = loaded.Projections[causeProjection.SubjectId!.Value], Forget = true });
            var forgotten = Ok(loaded.Runtime.CaptureCognitionGraph(loadedSoul));
            check(!TypedEvents.Of(forgotten.SubjectSnapshot).Any(e => ids.Contains(e.Fields.Id)) && Authority(loaded.Runtime) == authorityBefore,
                label + "forget cause removes transitive visible dependents without deleting authority audit");
            var afterForget = Ok(WorldRuntime.Restore(Ok(loaded.Runtime.Save())));
            var afterSoul = (SoulEssence)afterForget.Entities[soul.Id];
            check(!TypedEvents.Of(Ok(afterSoul.World.CaptureSnapshot())).Any(e => ids.Contains(e.Fields.Id))
                && Ok(afterForget.Runtime.ReconstructCognitionAudit(Ok(afterForget.Runtime.CaptureCognitionAudit(afterSoul)), beforeForget)).VisibleHistory.Any(o => o.Event?.Fields.Id == previous),
                label + "forgotten event stays private after load while old audit remains reconstructable");
        }
    }
}
