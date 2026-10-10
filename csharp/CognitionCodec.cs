using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DualOntologyWorldEngine.Core;

/// <summary>Strict UTF-8 JSON, closed DTO/AST whitelist. Decode never publishes a live session.</summary>
public static class CognitionCodec
{
    public const string Format = "dual-ontology-cognition/v1";
    public const string AuditFormat = "dual-ontology-cognition-audit/v1";
    public const string ContentVersion = "core.cognition/1";
    public static ImmutableArray<string> Capabilities { get; } = ["core.stable-identity/1", "core.cognition-atomic-batch/1", "core.cognition-snapshot/1", "core.cognition-audit/1", "core.cognition-graph-codec/1", "core.frozen-ast/1"];
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static ContractResult<T> Try<T>(Func<T> action)
    {
        try { return new ContractResult<T>.Success(action()); }
        catch (CognitionContractException e) { return new ContractResult<T>.Failure(e.Error); }
        catch (Exception e) when (e is JsonException or DecoderFallbackException or InvalidOperationException or FormatException or OverflowException or ArgumentException or KeyNotFoundException or NullReferenceException)
        { return new ContractResult<T>.Failure(new(ContractErrorCode.MalformedData, "wire", "无效的 UTF-8/JSON 或字段类型。", CandidateIds: [])); }
    }
    public static ContractResult<byte[]> Encode(CognitionGraphArchiveV1 archive, CodecLimitsV1? limits = null) => Try(() =>
    {
        Contract.Require(archive is not null, ContractErrorCode.MalformedData, "archive", "存档不能为空。");
        Contract.Array(archive!.AllocatedIds, "allocated_ids");
        var body = Object(("format", Write(Format)), ("contract_version", Write(1)), ("content_version", Write(ContentVersion)),
            ("required_capabilities", Items(Capabilities)), ("subject_snapshot", Write(archive.SubjectSnapshot)), ("allocated_ids", Items(archive.AllocatedIds.OrderBy(Observation.IdKey, StringComparer.Ordinal).ToImmutableArray())));
        var bytes = Utf8.GetBytes(body.ToJsonString()); Contract.Value(Decode(bytes, limits)); return bytes;
    });
    public static ContractResult<CognitionGraphArchiveV1> Decode(byte[] bytes, CodecLimitsV1? limits = null) => Try(() =>
    {
        using var doc = Parse(bytes, limits); var e = doc.RootElement;
        Fields(e, "format", "contract_version", "content_version", "required_capabilities", "subject_snapshot", "allocated_ids"); Envelope(e, Format);
        var a = new CognitionGraphArchiveV1(ReadCognitionSnapshotV1(e.GetProperty("subject_snapshot")), Array(e.GetProperty("allocated_ids"), ReadSubjectProjectionId));
        ValidateArchive(a); return a;
    });
    public static ContractResult<byte[]> EncodeAudit(CognitionAuditHistoryV1 audit, CodecLimitsV1? limits = null) => Try(() =>
    {
        Contract.Require(audit is not null, ContractErrorCode.MalformedData, "audit", "审计不能为空。");
        var body = (JsonObject)Write(audit)!; body["format"] = Write(AuditFormat); body["content_version"] = Write(ContentVersion);
        var bytes = Utf8.GetBytes(body.ToJsonString()); Contract.Value(DecodeAudit(bytes, limits)); return bytes;
    });
    public static ContractResult<CognitionAuditHistoryV1> DecodeAudit(byte[] bytes, CodecLimitsV1? limits = null) => Try(() =>
    {
        using var doc = Parse(bytes, limits); var e = doc.RootElement;
        Fields(e, "format", "content_version", "contract_version", "subject_world", "initial_snapshot", "commits", "required_capabilities"); Envelope(e, AuditFormat);
        var audit = ReadCognitionAuditHistoryV1(e, true); ValidateAudit(audit); return audit;
    });
    internal static void ValidateAudit(CognitionAuditHistoryV1 audit) => Contract.Value(new WorldRuntime().ReconstructCognitionAudit(audit, new(audit.Commits.Length)));
    internal static void ValidateArchive(CognitionGraphArchiveV1 a)
    {
        SnapshotGraph.Build(a.SubjectSnapshot); Contract.Array(a.AllocatedIds, "allocated_ids");
        var ids = new HashSet<SubjectProjectionId>();
        foreach (var id in a.AllocatedIds) { Contract.Id(id, a.SubjectSnapshot.SubjectWorld, "allocated_ids"); Contract.Require(ids.Add(id), ContractErrorCode.DuplicateIdentity, "allocated_ids", "句柄登记重复。"); }
        Contract.Require(a.SubjectSnapshot.Projections.All(p => ids.Contains(p.Id)), ContractErrorCode.InvalidReference, "allocated_ids", "活节点必须在已分配登记中。");
    }
    private static void Envelope(JsonElement e, string format)
    {
        Contract.Require(String(e.GetProperty("format")) == format && Integer(e.GetProperty("contract_version")) == 1 && String(e.GetProperty("content_version")) == ContentVersion,
            ContractErrorCode.Unsupported, "version", "不支持的存档或内容版本。");
        if (e.TryGetProperty("required_capabilities", out var c)) foreach (var cap in Array(c, String))
            Contract.Require(Capabilities.Contains(cap), ContractErrorCode.Unsupported, "required_capabilities", "不支持的必要能力。");
    }
    internal static JsonDocument Parse(byte[] bytes, CodecLimitsV1? limits = null)
    {
        var l = limits ?? new();
        Contract.Require(l.MaxBytes > 0 && l.MaxProjections >= 0 && l.MaxAstNodes >= 0 && l.MaxAstDepth >= 0 && l.MaxHistoryEntries >= 0, ContractErrorCode.MalformedData, "limits", "限额不能为负。");
        Contract.Require(bytes is not null && bytes.LongLength <= l.MaxBytes, ContractErrorCode.LimitExceeded, "bytes", "存档字节超限。");
        var text = Utf8.GetString(bytes!); Contract.Require(!text.StartsWith('﻿'), ContractErrorCode.MalformedData, "wire", "不支持 BOM。");
        var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 1024 });
        try
        {
            long ast = 0, history = 0, projections = 0;
            void Walk(JsonElement e, int depth)
            {
                if (e.ValueKind == JsonValueKind.Object)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    var isAst = e.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String && !e.TryGetProperty("name", out _);
                    if (isAst) { ast++; depth++; Contract.Require(ast <= l.MaxAstNodes && depth <= l.MaxAstDepth, ContractErrorCode.LimitExceeded, "ast", "AST 节点或深度超限。"); }
                    foreach (var p in e.EnumerateObject())
                    {
                        Contract.Require(seen.Add(p.Name), ContractErrorCode.MalformedData, p.Name, "JSON 键重复。");
                        if (p.Value.ValueKind == JsonValueKind.Array)
                        {
                            if (p.Name == "projections") projections += p.Value.GetArrayLength();
                            if (p.Name is "visible_history" or "commits") history += p.Value.GetArrayLength();
                        }
                        Contract.Require(projections <= l.MaxProjections && history <= l.MaxHistoryEntries, ContractErrorCode.LimitExceeded, "collections", "图或历史超限。");
                        Walk(p.Value, depth);
                    }
                }
                else if (e.ValueKind == JsonValueKind.Array) foreach (var child in e.EnumerateArray()) Walk(child, depth);
            }
            Walk(doc.RootElement, 0); return doc;
        }
        catch { doc.Dispose(); throw; }
    }
    public static ContractResult<bool> ValidateJson(byte[] bytes, CodecLimitsV1? limits = null) => Try(() => { using var doc=Parse(bytes,limits); return true; });
    internal static FrozenEffectV1 ReadEffect(JsonElement e) => ReadFrozenEffectV1(e);
    public static ContractResult<byte[]> EncodeEffect(Effect effect, IReadOnlyDictionary<SubjectProjectionId,Projection> map) => Try(() =>
    { var frozen=Contract.Value(CognitionAst.FreezeEffect(effect,map.ToImmutableDictionary())); return Utf8.GetBytes(Write(frozen)!.ToJsonString()); });
    public static ContractResult<Effect> RestoreEffect(byte[] bytes,IReadOnlyDictionary<SubjectProjectionId,Projection> map,CodecLimitsV1? limits=null) => Try(() =>
    { using var doc=Parse(bytes,limits); return Contract.Value(CognitionAst.RebindEffect(ReadEffect(doc.RootElement),map.ToImmutableDictionary())); });
    internal static void Fields(JsonElement e, params string[] names)
    {
        Contract.Require(e.ValueKind == JsonValueKind.Object, ContractErrorCode.MalformedData, "object", "必须为对象。");
        foreach (var p in e.EnumerateObject()) Contract.Require(names.Contains(p.Name, StringComparer.Ordinal), ContractErrorCode.Unsupported, p.Name, "未知属性。");
        foreach (var name in names) Contract.Require(e.TryGetProperty(name, out _) || name == "required_capabilities", ContractErrorCode.MalformedData, name, "必要字段缺失。");
    }
    private static string String(JsonElement e) => e.GetString() ?? throw new CognitionContractException(new(ContractErrorCode.MalformedData,"text","文本不能为空。", CandidateIds: []));
    private static int Integer(JsonElement e) { Contract.Require(e.TryGetInt32(out var n), ContractErrorCode.Unsupported,"number","整数超出 Int32 范围。"); return n; }
    private static long Long(JsonElement e) { Contract.Require(e.TryGetInt64(out var n) && n >= 0, ContractErrorCode.Unsupported,"number","版本超出非负 Int64 范围。"); return n; }
    private static double Number(JsonElement e) { var n = e.GetDouble(); Contract.Require(double.IsFinite(n), ContractErrorCode.Unsupported,"number","浮点数必须有限。"); return n; }
    private static double Duration(JsonElement e) { var n=Number(e); Contract.Require(n>=0,ContractErrorCode.Unsupported,"duration","时长不能为负。"); return n; }
    private static int Spend(JsonElement e) { var n=Integer(e); Contract.Require(n>=0,ContractErrorCode.Unsupported,"amount","支付量不能为负。"); return n; }
    private static bool Boolean(JsonElement e) => e.GetBoolean();
    private static Guid GuidValue(JsonElement e) { var s=String(e); Contract.Require(Guid.TryParseExact(s,"D",out var n) && n != Guid.Empty && n.ToString("D") == s, ContractErrorCode.InvalidIdentity,"guid","句柄必须为非空小写 D 格式。"); return n; }
    private static KnowledgeRevision Revision(JsonElement e) => new(Long(e));
    private static VocabularyRevision Vocabulary(JsonElement e) => new(Long(e));
    private static AuthorTermId Term(JsonElement e) { var s=String(e); Contract.Identity(s,"term"); return new(s); }
    private static Tag TagValue(JsonElement e) { var s=String(e); Contract.Identity(s,"tag"); return new(s); }
    private static RelationTag Relation(JsonElement e) { var s=String(e); Contract.Identity(s,"relation"); return new(s); }
    private static ImmutableArray<T> Array<T>(JsonElement e, Func<JsonElement,T> read) => [..e.EnumerateArray().Select(read)];
    private static string EnumName(string name) => System.Text.RegularExpressions.Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    private static T EnumValue<T>(JsonElement e) where T : struct, Enum
    { var name = String(e); foreach (var v in Enum.GetValues<T>()) if (EnumName(v.ToString()) == name) return v; throw new CognitionContractException(new(ContractErrorCode.Unsupported,"enum","未知枚举值。", CandidateIds: [])); }
    private static ImmutableDictionary<Tag,int?> NullableTags(JsonElement e)
    {
        var b=ImmutableDictionary.CreateBuilder<Tag,int?>(); foreach(var item in e.EnumerateArray())
        { Fields(item,"tag","value"); var t=TagValue(item.GetProperty("tag")); Contract.Require(!b.ContainsKey(t), ContractErrorCode.MalformedData,"tags","标签重复。"); b.Add(t,item.GetProperty("value").ValueKind==JsonValueKind.Null ? null : Integer(item.GetProperty("value"))); } return b.ToImmutable();
    }
    private static ImmutableDictionary<Tag,int> TagsValue(JsonElement e)
    { var tags=NullableTags(e); Contract.Require(tags.Values.All(v=>v.HasValue),ContractErrorCode.MalformedData,"tags","标签值不能为 null。"); return tags.ToImmutableDictionary(p=>p.Key,p=>p.Value!.Value); }
    private static JsonObject Object(params (string Key, JsonNode? Value)[] pairs) { var o=new JsonObject(); foreach(var p in pairs) o.Add(p.Key,p.Value); return o; }
    private static JsonArray Items<T>(ImmutableArray<T> items) { Contract.Array(items,"array"); return new(items.Select(x=>Write(x)).ToArray()); }
    internal static JsonNode? Write(object? value) => value switch
    {
        null => null,
        string n => JsonValue.Create(n), bool n => JsonValue.Create(n), int n => JsonValue.Create(n), long n => JsonValue.Create(n),
        double n when double.IsFinite(n) => JsonValue.Create(n), Guid n => JsonValue.Create(n.ToString("D")),
        EntityId n => Write(n.Value), FacetId n => Write(n.Value), AuthorTermId n => Write(n.Value), Tag n => Write(n.Name), RelationTag n => Write(n.Name),
        KnowledgeRevision n => Write(n.Value), VocabularyRevision n => Write(n.Value),
        ImmutableDictionary<Tag,int> n => new JsonArray(n.OrderBy(p=>p.Key.Name,StringComparer.Ordinal).Select(p=> (JsonNode?)Object(("tag",Write(p.Key)),("value",Write(p.Value)))).ToArray()),
        ImmutableDictionary<Tag,int?> n => new JsonArray(n.OrderBy(p=>p.Key.Name,StringComparer.Ordinal).Select(p=> (JsonNode?)Object(("tag",Write(p.Key)),("value",Write(p.Value)))).ToArray()),
        Truth n when Enum.IsDefined(n) => Write(EnumName(n.ToString())),
        ProjectionKind n when Enum.IsDefined(n) => Write(EnumName(n.ToString())),
        RuleSlot n when Enum.IsDefined(n) => Write(EnumName(n.ToString())),
        RuleMode n when Enum.IsDefined(n) => Write(EnumName(n.ToString())),
        ActionOp n when Enum.IsDefined(n) => Write(EnumName(n.ToString())),
        LifetimeScope n when Enum.IsDefined(n) => Write(EnumName(n.ToString())),
        ObservationSource n when Enum.IsDefined(n) => Write(EnumName(n.ToString())),
        ObservationTimeUnitV1 n when Enum.IsDefined(n) => Write(EnumName(n.ToString())),
        SubjectProjectionId n => Object(("subject_world", Write(n.SubjectWorld)), ("local", Write(n.Local))),
        SourceLocationV1 n => Object(("document_id", Write(n.DocumentId)), ("start_line", Write(n.StartLine)), ("start_column", Write(n.StartColumn)), ("end_line", Write(n.EndLine)), ("end_column", Write(n.EndColumn))),
        KnownAuthorTermV1 n => Object(("term_id", Write(n.TermId)), ("raw_lexeme", Write(n.RawLexeme)), ("canonical_lexeme", Write(n.CanonicalLexeme))),
        RelationEdgeV1 n => Object(("relation", Write(n.Relation)), ("target", Write(n.Target))),
        FrozenRuleV1 n => Object(("condition", Write(n.Condition)), ("mode", Write(n.Mode)), ("effect", Write(n.Effect)), ("weight", Write(n.Weight)), ("acceptance", Write(n.Acceptance)), ("lifetime", Write(n.Lifetime)), ("lifetime_owner", Write(n.LifetimeOwner))),
        ObservedStateV1 n => Object(("name", Write(n.Name)), ("kind", Write(n.Kind)), ("existence", Write(n.Existence)), ("parent", Write(n.Parent)), ("tags", Write(n.Tags)), ("relations", Items(n.Relations)), ("denied_relations", Items(n.DeniedRelations)), ("rules", Items(n.Rules)), ("procedure", Write(n.Procedure)), ("known_terms", Items(n.KnownTerms))),
        VisibleObservationV1 n => Object(("target", Write(n.Target)), ("source", Write(n.Source)), ("time", Write(n.Time)), ("time_unit", Write(n.TimeUnit)), ("detected", Write(n.Detected)), ("declared_existence", Write(n.DeclaredExistence)), ("details", Write(n.Details)), ("origin", Write(n.Origin)), ("dependencies", Items(n.Dependencies)), ("observed_state", Write(n.ObservedState))),
        FrozenProjectionV1 n => Object(("id", Write(n.Id)), ("name", Write(n.Name)), ("kind", Write(n.Kind)), ("existence", Write(n.Existence)), ("parent", Write(n.Parent)), ("tags", Write(n.Tags)), ("relations", Items(n.Relations)), ("denied_relations", Items(n.DeniedRelations)), ("rules", Items(n.Rules)), ("procedure", Write(n.Procedure)), ("known_terms", Items(n.KnownTerms)), ("last_observation", Write(n.LastObservation))),
        CognitionSnapshotV1 n => Object(("contract_version", Write(n.ContractVersion)), ("subject_world", Write(n.SubjectWorld)), ("revision", Write(n.Revision)), ("vocabulary_revision", Write(n.VocabularyRevision)), ("projections", Items(n.Projections)), ("visible_history", Items(n.VisibleHistory)), ("required_capabilities", Items(n.RequiredCapabilities))),
        CognitionGraphArchiveV1 n => Object(("subject_snapshot", Write(n.SubjectSnapshot)), ("allocated_ids", Items(n.AllocatedIds))),
        ObservationChangeV1 n => Object(("target", Write(n.Target)), ("create", Write(n.Create)), ("name", Write(n.Name)), ("kind", Write(n.Kind)), ("source", Write(n.Source)), ("time", Write(n.Time)), ("time_unit", Write(n.TimeUnit)), ("detected", Write(n.Detected)), ("existence", Write(n.Existence)), ("move", Write(n.Move)), ("parent", Write(n.Parent)), ("tags", Write(n.Tags)), ("add_relations", Items(n.AddRelations)), ("remove_relations", Items(n.RemoveRelations)), ("deny_relations", Items(n.DenyRelations)), ("rules", (n.Rules is { } aRules ? Items(aRules) : null)), ("procedure", Write(n.Procedure)), ("clear_procedure", Write(n.ClearProcedure)), ("forget", Write(n.Forget)), ("known_terms", (n.KnownTerms is { } aKnownTerms ? Items(aKnownTerms) : null)), ("details", Write(n.Details)), ("origin", Write(n.Origin)), ("source_dependencies", Items(n.SourceDependencies))),
        CognitionAuditChangeV1 n => Object(("id", Write(n.Id)), ("before", Write(n.Before)), ("after", Write(n.After))),
        CognitionAuditCommitV1 n => Object(("previous_revision", Write(n.PreviousRevision)), ("revision", Write(n.Revision)), ("previous_vocabulary_revision", Write(n.PreviousVocabularyRevision)), ("vocabulary_revision", Write(n.VocabularyRevision)), ("inputs", Items(n.Inputs)), ("changes", Items(n.Changes)), ("forgotten_ids", Items(n.ForgottenIds)), ("removed_visible_history_indices", Items(n.RemovedVisibleHistoryIndices)), ("appended_visible_history", Items(n.AppendedVisibleHistory))),
        CognitionAuditHistoryV1 n => Object(("contract_version", Write(n.ContractVersion)), ("subject_world", Write(n.SubjectWorld)), ("initial_snapshot", Write(n.InitialSnapshot)), ("commits", Items(n.Commits)), ("required_capabilities", Items(n.RequiredCapabilities))),
        FrozenNounV1.Reference n => Object(("kind", Write("reference")), ("target", Write(n.Target))),
        FrozenNounV1.Slot n => Object(("kind", Write("slot")), ("role", Write(n.Role))),
        FrozenConditionV1.Constant n => Object(("kind", Write("constant")), ("value", Write(n.Value))),
        FrozenConditionV1.Exists n => Object(("kind", Write("exists")), ("target", Write(n.Target))),
        FrozenConditionV1.Tagged n => Object(("kind", Write("tagged")), ("target", Write(n.Target)), ("tag", Write(n.Tag)), ("minimum", Write(n.Minimum))),
        FrozenConditionV1.Related n => Object(("kind", Write("related")), ("target", Write(n.Target)), ("relation", Write(n.Relation)), ("other", Write(n.Other))),
        FrozenConditionV1.At n => Object(("kind", Write("at")), ("target", Write(n.Target)), ("space", Write(n.Space))),
        FrozenConditionV1.IsKind n => Object(("kind", Write("is_kind")), ("target", Write(n.Target)), ("projection_kind", Write(n.Kind))),
        FrozenConditionV1.Not n => Object(("kind", Write("not")), ("operand", Write(n.Operand))),
        FrozenConditionV1.All n => Object(("kind", Write("all")), ("items", Items(n.Items))),
        FrozenConditionV1.Any n => Object(("kind", Write("any")), ("items", Items(n.Items))),
        FrozenEffectV1.Act n => Object(("kind", Write("act")), ("operation", Write(n.Operation)), ("target", Write(n.Target)), ("tool", Write(n.Tool))),
        FrozenEffectV1.Set n => Object(("kind", Write("set")), ("target", Write(n.Target)), ("tag", Write(n.Tag)), ("value", Write(n.Value))),
        FrozenEffectV1.Change n => Object(("kind", Write("change")), ("target", Write(n.Target)), ("tag", Write(n.Tag)), ("amount", Write(n.Amount))),
        FrozenEffectV1.TemporarySet n => Object(("kind", Write("temporary_set")), ("target", Write(n.Target)), ("tag", Write(n.Tag)), ("value", Write(n.Value)), ("duration", Write(n.Duration))),
        FrozenEffectV1.TemporarySetUntilEnd n => Object(("kind", Write("temporary_set_until_end")), ("target", Write(n.Target)), ("tag", Write(n.Tag)), ("value", Write(n.Value))),
        FrozenEffectV1.Spend n => Object(("kind", Write("spend")), ("target", Write(n.Target)), ("tag", Write(n.Tag)), ("amount", Write(n.Amount))),
        FrozenEffectV1.Relate n => Object(("kind", Write("relate")), ("target", Write(n.Target)), ("relation", Write(n.Relation)), ("other", Write(n.Other)), ("remove", Write(n.Remove))),
        FrozenEffectV1.Sequence n => Object(("kind", Write("sequence")), ("items", Items(n.Items))),
        FrozenEffectV1.When n => Object(("kind", Write("when")), ("condition", Write(n.Condition)), ("body", Write(n.Body))),
        FrozenEffectV1.Require n => Object(("kind", Write("require")), ("condition", Write(n.Condition))),
        FrozenEffectV1.Wait n => Object(("kind", Write("wait")), ("delay", Write(n.Delay)), ("body", Write(n.Body)), ("until", Write(n.Until))),
        FrozenEffectV1.Say n => Object(("kind", Write("say")), ("recipient", Write(n.Recipient)), ("text", Write(n.Text)), ("request", Write(n.Request))),
        FrozenEffectV1.Think n => Object(("kind", Write("think")), ("fact", Write(n.Fact))),
        _ => throw new CognitionContractException(new(ContractErrorCode.Unsupported,"wire","未知 DTO/AST 或无效枚举。", CandidateIds: []))
    };
    internal static SubjectProjectionId ReadSubjectProjectionId(JsonElement e)
    {
        Fields(e, "subject_world", "local");
        return new(GuidValue(e.GetProperty("subject_world")), GuidValue(e.GetProperty("local")));
    }
    internal static SourceLocationV1 ReadSourceLocationV1(JsonElement e)
    {
        Fields(e, "document_id", "start_line", "start_column", "end_line", "end_column");
        return new(String(e.GetProperty("document_id")), Integer(e.GetProperty("start_line")), Integer(e.GetProperty("start_column")), Integer(e.GetProperty("end_line")), Integer(e.GetProperty("end_column")));
    }
    internal static KnownAuthorTermV1 ReadKnownAuthorTermV1(JsonElement e)
    {
        Fields(e, "term_id", "raw_lexeme", "canonical_lexeme");
        return new(Term(e.GetProperty("term_id")), String(e.GetProperty("raw_lexeme")), String(e.GetProperty("canonical_lexeme")));
    }
    internal static RelationEdgeV1 ReadRelationEdgeV1(JsonElement e)
    {
        Fields(e, "relation", "target");
        return new(Relation(e.GetProperty("relation")), ReadSubjectProjectionId(e.GetProperty("target")));
    }
    internal static FrozenRuleV1 ReadFrozenRuleV1(JsonElement e)
    {
        Fields(e, "condition", "mode", "effect", "weight", "acceptance", "lifetime", "lifetime_owner");
        return new(ReadFrozenConditionV1(e.GetProperty("condition"), 0), EnumValue<RuleMode>(e.GetProperty("mode")), ReadFrozenEffectV1(e.GetProperty("effect"), 0), Integer(e.GetProperty("weight")), EnumValue<Truth>(e.GetProperty("acceptance")), EnumValue<LifetimeScope>(e.GetProperty("lifetime")), (e.GetProperty("lifetime_owner").ValueKind == JsonValueKind.Null ? null : (String(e.GetProperty("lifetime_owner")))));
    }
    internal static ObservedStateV1 ReadObservedStateV1(JsonElement e)
    {
        Fields(e, "name", "kind", "existence", "parent", "tags", "relations", "denied_relations", "rules", "procedure", "known_terms");
        return new(String(e.GetProperty("name")), EnumValue<ProjectionKind>(e.GetProperty("kind")), EnumValue<Truth>(e.GetProperty("existence")), (e.GetProperty("parent").ValueKind == JsonValueKind.Null ? null : (ReadSubjectProjectionId(e.GetProperty("parent")))), TagsValue(e.GetProperty("tags")), Array(e.GetProperty("relations"), x => ReadRelationEdgeV1(x)), Array(e.GetProperty("denied_relations"), x => ReadRelationEdgeV1(x)), Array(e.GetProperty("rules"), x => ReadFrozenRuleV1(x)), (e.GetProperty("procedure").ValueKind == JsonValueKind.Null ? null : (ReadFrozenEffectV1(e.GetProperty("procedure"), 0))), Array(e.GetProperty("known_terms"), x => ReadKnownAuthorTermV1(x)));
    }
    internal static VisibleObservationV1 ReadVisibleObservationV1(JsonElement e)
    {
        Fields(e, "target", "source", "time", "time_unit", "detected", "declared_existence", "details", "origin", "dependencies", "observed_state");
        return new(ReadSubjectProjectionId(e.GetProperty("target")), EnumValue<ObservationSource>(e.GetProperty("source")), Number(e.GetProperty("time")), EnumValue<ObservationTimeUnitV1>(e.GetProperty("time_unit")), (e.GetProperty("detected").ValueKind == JsonValueKind.Null ? null : (Boolean(e.GetProperty("detected")))), (e.GetProperty("declared_existence").ValueKind == JsonValueKind.Null ? null : (EnumValue<Truth>(e.GetProperty("declared_existence")))), String(e.GetProperty("details")), (e.GetProperty("origin").ValueKind == JsonValueKind.Null ? null : (ReadSourceLocationV1(e.GetProperty("origin")))), Array(e.GetProperty("dependencies"), x => ReadSubjectProjectionId(x)), ReadObservedStateV1(e.GetProperty("observed_state")));
    }
    internal static FrozenProjectionV1 ReadFrozenProjectionV1(JsonElement e)
    {
        Fields(e, "id", "name", "kind", "existence", "parent", "tags", "relations", "denied_relations", "rules", "procedure", "known_terms", "last_observation");
        return new(ReadSubjectProjectionId(e.GetProperty("id")), String(e.GetProperty("name")), EnumValue<ProjectionKind>(e.GetProperty("kind")), EnumValue<Truth>(e.GetProperty("existence")), (e.GetProperty("parent").ValueKind == JsonValueKind.Null ? null : (ReadSubjectProjectionId(e.GetProperty("parent")))), TagsValue(e.GetProperty("tags")), Array(e.GetProperty("relations"), x => ReadRelationEdgeV1(x)), Array(e.GetProperty("denied_relations"), x => ReadRelationEdgeV1(x)), Array(e.GetProperty("rules"), x => ReadFrozenRuleV1(x)), (e.GetProperty("procedure").ValueKind == JsonValueKind.Null ? null : (ReadFrozenEffectV1(e.GetProperty("procedure"), 0))), Array(e.GetProperty("known_terms"), x => ReadKnownAuthorTermV1(x)), (e.GetProperty("last_observation").ValueKind == JsonValueKind.Null ? null : (ReadVisibleObservationV1(e.GetProperty("last_observation")))));
    }
    internal static CognitionSnapshotV1 ReadCognitionSnapshotV1(JsonElement e)
    {
        Fields(e, "contract_version", "subject_world", "revision", "vocabulary_revision", "projections", "visible_history", "required_capabilities");
        return new(Integer(e.GetProperty("contract_version")), GuidValue(e.GetProperty("subject_world")), Revision(e.GetProperty("revision")), Vocabulary(e.GetProperty("vocabulary_revision")), Array(e.GetProperty("projections"), x => ReadFrozenProjectionV1(x)), Array(e.GetProperty("visible_history"), x => ReadVisibleObservationV1(x)), e.TryGetProperty("required_capabilities", out var caps) ? Array(caps, x => String(x)) : []);
    }
    internal static CognitionGraphArchiveV1 ReadCognitionGraphArchiveV1(JsonElement e)
    {
        Fields(e, "subject_snapshot", "allocated_ids");
        return new(ReadCognitionSnapshotV1(e.GetProperty("subject_snapshot")), Array(e.GetProperty("allocated_ids"), x => ReadSubjectProjectionId(x)));
    }
    internal static ObservationChangeV1 ReadObservationChangeV1(JsonElement e)
    {
        Fields(e, "target", "create", "name", "kind", "source", "time", "time_unit", "detected", "existence", "move", "parent", "tags", "add_relations", "remove_relations", "deny_relations", "rules", "procedure", "clear_procedure", "forget", "known_terms", "details", "origin", "source_dependencies");
        return new(ReadSubjectProjectionId(e.GetProperty("target")), Boolean(e.GetProperty("create")), String(e.GetProperty("name")), EnumValue<ProjectionKind>(e.GetProperty("kind")), EnumValue<ObservationSource>(e.GetProperty("source")), Number(e.GetProperty("time")), EnumValue<ObservationTimeUnitV1>(e.GetProperty("time_unit")), (e.GetProperty("detected").ValueKind == JsonValueKind.Null ? null : (Boolean(e.GetProperty("detected")))), (e.GetProperty("existence").ValueKind == JsonValueKind.Null ? null : (EnumValue<Truth>(e.GetProperty("existence")))), Boolean(e.GetProperty("move")), (e.GetProperty("parent").ValueKind == JsonValueKind.Null ? null : (ReadSubjectProjectionId(e.GetProperty("parent")))), NullableTags(e.GetProperty("tags")), Array(e.GetProperty("add_relations"), x => ReadRelationEdgeV1(x)), Array(e.GetProperty("remove_relations"), x => ReadRelationEdgeV1(x)), Array(e.GetProperty("deny_relations"), x => ReadRelationEdgeV1(x)), (e.GetProperty("rules").ValueKind == JsonValueKind.Null ? null : (Array(e.GetProperty("rules"), x => ReadFrozenRuleV1(x)))), (e.GetProperty("procedure").ValueKind == JsonValueKind.Null ? null : (ReadFrozenEffectV1(e.GetProperty("procedure"), 0))), Boolean(e.GetProperty("clear_procedure")), Boolean(e.GetProperty("forget")), (e.GetProperty("known_terms").ValueKind == JsonValueKind.Null ? null : (Array(e.GetProperty("known_terms"), x => ReadKnownAuthorTermV1(x)))), String(e.GetProperty("details")), (e.GetProperty("origin").ValueKind == JsonValueKind.Null ? null : (ReadSourceLocationV1(e.GetProperty("origin")))), Array(e.GetProperty("source_dependencies"), x => ReadSubjectProjectionId(x)));
    }
    internal static CognitionAuditChangeV1 ReadCognitionAuditChangeV1(JsonElement e)
    {
        Fields(e, "id", "before", "after");
        return new(ReadSubjectProjectionId(e.GetProperty("id")), (e.GetProperty("before").ValueKind == JsonValueKind.Null ? null : (ReadFrozenProjectionV1(e.GetProperty("before")))), (e.GetProperty("after").ValueKind == JsonValueKind.Null ? null : (ReadFrozenProjectionV1(e.GetProperty("after")))));
    }
    internal static CognitionAuditCommitV1 ReadCognitionAuditCommitV1(JsonElement e)
    {
        Fields(e, "previous_revision", "revision", "previous_vocabulary_revision", "vocabulary_revision", "inputs", "changes", "forgotten_ids", "removed_visible_history_indices", "appended_visible_history");
        return new(Revision(e.GetProperty("previous_revision")), Revision(e.GetProperty("revision")), Vocabulary(e.GetProperty("previous_vocabulary_revision")), Vocabulary(e.GetProperty("vocabulary_revision")), Array(e.GetProperty("inputs"), x => ReadObservationChangeV1(x)), Array(e.GetProperty("changes"), x => ReadCognitionAuditChangeV1(x)), Array(e.GetProperty("forgotten_ids"), x => ReadSubjectProjectionId(x)), Array(e.GetProperty("removed_visible_history_indices"), x => Integer(x)), Array(e.GetProperty("appended_visible_history"), x => ReadVisibleObservationV1(x)));
    }
    internal static CognitionAuditHistoryV1 ReadCognitionAuditHistoryV1(JsonElement e, bool envelope = false)
    {
        if (!envelope) Fields(e, "contract_version", "subject_world", "initial_snapshot", "commits", "required_capabilities");
        return new(Integer(e.GetProperty("contract_version")), GuidValue(e.GetProperty("subject_world")), ReadCognitionSnapshotV1(e.GetProperty("initial_snapshot")), Array(e.GetProperty("commits"), x => ReadCognitionAuditCommitV1(x)), e.TryGetProperty("required_capabilities", out var caps) ? Array(caps, x => String(x)) : []);
    }
    private static FrozenNounV1 ReadFrozenNounV1(JsonElement e, int depth = 0)
    {
        Contract.Require(depth <= 256, ContractErrorCode.LimitExceeded,"ast","AST 深度超限。");
        var kind=String(e.GetProperty("kind"));
        switch(kind)
        {
            case "reference": Fields(e, "kind", "target"); return new FrozenNounV1.Reference(ReadSubjectProjectionId(e.GetProperty("target")));
            case "slot": Fields(e, "kind", "role"); return new FrozenNounV1.Slot(EnumValue<RuleSlot>(e.GetProperty("role")));
            default: throw new CognitionContractException(new(ContractErrorCode.Unsupported,"ast.kind","未知 AST 节点。", CandidateIds: []));
        }
    }
    private static FrozenConditionV1 ReadFrozenConditionV1(JsonElement e, int depth = 0)
    {
        Contract.Require(depth <= 256, ContractErrorCode.LimitExceeded,"ast","AST 深度超限。");
        var kind=String(e.GetProperty("kind"));
        switch(kind)
        {
            case "constant": Fields(e, "kind", "value"); return new FrozenConditionV1.Constant(EnumValue<Truth>(e.GetProperty("value")));
            case "exists": Fields(e, "kind", "target"); return new FrozenConditionV1.Exists(ReadFrozenNounV1(e.GetProperty("target"), depth+1));
            case "tagged": Fields(e, "kind", "target", "tag", "minimum"); return new FrozenConditionV1.Tagged(ReadFrozenNounV1(e.GetProperty("target"), depth+1), TagValue(e.GetProperty("tag")), Integer(e.GetProperty("minimum")));
            case "related": Fields(e, "kind", "target", "relation", "other"); return new FrozenConditionV1.Related(ReadFrozenNounV1(e.GetProperty("target"), depth+1), Relation(e.GetProperty("relation")), ReadFrozenNounV1(e.GetProperty("other"), depth+1));
            case "at": Fields(e, "kind", "target", "space"); return new FrozenConditionV1.At(ReadFrozenNounV1(e.GetProperty("target"), depth+1), ReadFrozenNounV1(e.GetProperty("space"), depth+1));
            case "is_kind": Fields(e, "kind", "target", "projection_kind"); return new FrozenConditionV1.IsKind(ReadFrozenNounV1(e.GetProperty("target"), depth+1), EnumValue<ProjectionKind>(e.GetProperty("projection_kind")));
            case "not": Fields(e, "kind", "operand"); return new FrozenConditionV1.Not(ReadFrozenConditionV1(e.GetProperty("operand"), depth+1));
            case "all": Fields(e, "kind", "items"); return new FrozenConditionV1.All(Array(e.GetProperty("items"), x => ReadFrozenConditionV1(x, depth+1)));
            case "any": Fields(e, "kind", "items"); return new FrozenConditionV1.Any(Array(e.GetProperty("items"), x => ReadFrozenConditionV1(x, depth+1)));
            default: throw new CognitionContractException(new(ContractErrorCode.Unsupported,"ast.kind","未知 AST 节点。", CandidateIds: []));
        }
    }
    private static FrozenEffectV1 ReadFrozenEffectV1(JsonElement e, int depth = 0)
    {
        Contract.Require(depth <= 256, ContractErrorCode.LimitExceeded,"ast","AST 深度超限。");
        var kind=String(e.GetProperty("kind"));
        switch(kind)
        {
            case "act": Fields(e, "kind", "operation", "target", "tool"); return new FrozenEffectV1.Act(EnumValue<ActionOp>(e.GetProperty("operation")), ReadFrozenNounV1(e.GetProperty("target"), depth+1), (e.GetProperty("tool").ValueKind == JsonValueKind.Null ? null : (ReadFrozenNounV1(e.GetProperty("tool"), depth+1))));
            case "set": Fields(e, "kind", "target", "tag", "value"); return new FrozenEffectV1.Set(ReadFrozenNounV1(e.GetProperty("target"), depth+1), TagValue(e.GetProperty("tag")), Integer(e.GetProperty("value")));
            case "change": Fields(e, "kind", "target", "tag", "amount"); return new FrozenEffectV1.Change(ReadFrozenNounV1(e.GetProperty("target"), depth+1), TagValue(e.GetProperty("tag")), Integer(e.GetProperty("amount")));
            case "temporary_set": Fields(e, "kind", "target", "tag", "value", "duration"); return new FrozenEffectV1.TemporarySet(ReadFrozenNounV1(e.GetProperty("target"), depth+1), TagValue(e.GetProperty("tag")), Integer(e.GetProperty("value")), Duration(e.GetProperty("duration")));
            case "temporary_set_until_end": Fields(e, "kind", "target", "tag", "value"); return new FrozenEffectV1.TemporarySetUntilEnd(ReadFrozenNounV1(e.GetProperty("target"), depth+1), TagValue(e.GetProperty("tag")), Integer(e.GetProperty("value")));
            case "spend": Fields(e, "kind", "target", "tag", "amount"); return new FrozenEffectV1.Spend(ReadFrozenNounV1(e.GetProperty("target"), depth+1), TagValue(e.GetProperty("tag")), Spend(e.GetProperty("amount")));
            case "relate": Fields(e, "kind", "target", "relation", "other", "remove"); return new FrozenEffectV1.Relate(ReadFrozenNounV1(e.GetProperty("target"), depth+1), Relation(e.GetProperty("relation")), ReadFrozenNounV1(e.GetProperty("other"), depth+1), Boolean(e.GetProperty("remove")));
            case "sequence": Fields(e, "kind", "items"); return new FrozenEffectV1.Sequence(Array(e.GetProperty("items"), x => ReadFrozenEffectV1(x, depth+1)));
            case "when": Fields(e, "kind", "condition", "body"); return new FrozenEffectV1.When(ReadFrozenConditionV1(e.GetProperty("condition"), depth+1), ReadFrozenEffectV1(e.GetProperty("body"), depth+1));
            case "require": Fields(e, "kind", "condition"); return new FrozenEffectV1.Require(ReadFrozenConditionV1(e.GetProperty("condition"), depth+1));
            case "wait": Fields(e, "kind", "delay", "body", "until"); return new FrozenEffectV1.Wait(Duration(e.GetProperty("delay")), ReadFrozenEffectV1(e.GetProperty("body"), depth+1), (e.GetProperty("until").ValueKind == JsonValueKind.Null ? null : (ReadFrozenConditionV1(e.GetProperty("until"), depth+1))));
            case "say": Fields(e, "kind", "recipient", "text", "request"); return new FrozenEffectV1.Say(ReadFrozenNounV1(e.GetProperty("recipient"), depth+1), String(e.GetProperty("text")), (e.GetProperty("request").ValueKind == JsonValueKind.Null ? null : (ReadFrozenEffectV1(e.GetProperty("request"), depth+1))));
            case "think": Fields(e, "kind", "fact"); return new FrozenEffectV1.Think(ReadFrozenConditionV1(e.GetProperty("fact"), depth+1));
            default: throw new CognitionContractException(new(ContractErrorCode.Unsupported,"ast.kind","未知 AST 节点。", CandidateIds: []));
        }
    }
}
