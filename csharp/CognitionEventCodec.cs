using System.Text.Json;
using System.Text.Json.Nodes;

namespace DualOntologyWorldEngine.Core;

public static partial class CognitionCodec
{
    // Additive V1 fields may be absent in pre-event saves; every other property stays closed.
    internal static void OptionalFields(JsonElement e, string[] required, params string[] optional)
        => Fields(e, [..required, ..optional.Where(name => e.TryGetProperty(name, out _))]);

    private static TypedEventV1<SubjectProjectionId?>? ReadOptionalEvent(JsonElement e)
        => e.TryGetProperty("event", out var value) && value.ValueKind != JsonValueKind.Null
            ? ReadEvent<SubjectProjectionId?>(value, r => r.ValueKind == JsonValueKind.Null ? null : ReadSubjectProjectionId(r)) : null;

    internal static JsonObject WriteEvent<T>(TypedEventV1<T> e, Func<T, JsonNode?> reference)
    {
        TypedEvents.Validate(e, _ => { });
        var kind = e switch
        {
            ActionEventV1<T> => "action", ObservationRecordV1<T> => "observation",
            TestimonyEventV1<T> => "testimony", PropagationEventV1<T> => "propagation",
            PresentationEventV1<T> => "presentation",
            _ => throw new CognitionContractException(new(ContractErrorCode.Unsupported, "event.kind", "未知事件种类。", CandidateIds: []))
        };
        var f = e.Fields;
        return Object(("kind", Write(kind)), ("contract_version", Write(f.ContractVersion)), ("id", Write(f.Id)),
            ("time", Write(f.Time)), ("time_unit", Write(f.TimeUnit)), ("subject", reference(f.Subject)),
            ("object", reference(f.Object)), ("location", reference(f.Location)), ("source", reference(f.Source)),
            ("knowledge_source", Write(f.KnowledgeSource)), ("origin", Write(f.Origin)),
            ("phase", Write(f.Phase)), ("outcome", Write(f.Outcome)), ("operation", Write(f.Operation)),
            ("topic", Write(f.Topic)), ("failure", Write(f.Failure)),
            ("perceivers", new JsonArray(f.Perceivers.Select(reference).ToArray())),
            ("claims", new JsonArray(f.Claims.Select(c => (JsonNode?)Object(("predicate", Write(c.Predicate)),
                ("subject", reference(c.Subject)), ("object", reference(c.Object)), ("location", reference(c.Location)),
                ("tag", Write(c.Tag)), ("value", Write(c.Value)), ("truth", Write(c.Truth)))).ToArray())),
            ("causes", Items(f.Causes)));
    }

    internal static TypedEventV1<T> ReadEvent<T>(JsonElement e, Func<JsonElement, T> reference)
    {
        Fields(e, "kind", "contract_version", "id", "time", "time_unit", "subject", "object", "location", "source",
            "knowledge_source", "origin", "phase", "outcome", "operation", "topic", "failure", "perceivers", "claims", "causes");
        var kind = String(e.GetProperty("kind"));
        Contract.Require(kind is "action" or "observation" or "testimony" or "propagation" or "presentation",
            ContractErrorCode.Unsupported, "event.kind", "未知事件种类。");
        var f = new EventFieldsV1<T>
        {
            ContractVersion = Integer(e.GetProperty("contract_version")), Id = new(String(e.GetProperty("id"))),
            Time = Number(e.GetProperty("time")), TimeUnit = EnumValue<ObservationTimeUnitV1>(e.GetProperty("time_unit")),
            Subject = reference(e.GetProperty("subject")), Object = reference(e.GetProperty("object")),
            Location = reference(e.GetProperty("location")), Source = reference(e.GetProperty("source")),
            KnowledgeSource = EnumValue<ObservationSource>(e.GetProperty("knowledge_source")),
            Origin = e.GetProperty("origin").ValueKind == JsonValueKind.Null ? null : ReadSourceLocationV1(e.GetProperty("origin")),
            Phase = EnumValue<EventPhaseV1>(e.GetProperty("phase")), Outcome = EnumValue<EventOutcomeV1>(e.GetProperty("outcome")),
            Operation = e.GetProperty("operation").ValueKind == JsonValueKind.Null ? null : EnumValue<ActionOp>(e.GetProperty("operation")),
            Topic = e.GetProperty("topic").ValueKind == JsonValueKind.Null ? null : Term(e.GetProperty("topic")),
            Failure = EnumValue<ActionFailure>(e.GetProperty("failure")), Perceivers = Array(e.GetProperty("perceivers"), reference),
            Claims = Array(e.GetProperty("claims"), c =>
            {
                Fields(c, "predicate", "subject", "object", "location", "tag", "value", "truth");
                return new EventClaimV1<T>(Term(c.GetProperty("predicate")), reference(c.GetProperty("subject")),
                    reference(c.GetProperty("object")), reference(c.GetProperty("location")),
                    c.GetProperty("tag").ValueKind == JsonValueKind.Null ? null : TagValue(c.GetProperty("tag")),
                    c.GetProperty("value").ValueKind == JsonValueKind.Null ? null : Integer(c.GetProperty("value")),
                    EnumValue<Truth>(c.GetProperty("truth")));
            }),
            Causes = Array(e.GetProperty("causes"), c => new EventId(String(c)))
        };
        TypedEventV1<T> result = kind switch
        {
            "action" => new ActionEventV1<T>(f), "observation" => new ObservationRecordV1<T>(f),
            "testimony" => new TestimonyEventV1<T>(f), "propagation" => new PropagationEventV1<T>(f),
            "presentation" => new PresentationEventV1<T>(f), _ => throw new InvalidOperationException()
        };
        TypedEvents.Validate(result, _ => { });
        return result;
    }
}
