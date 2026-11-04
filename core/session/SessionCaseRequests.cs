using System.Text.Json;
using DuelView.Core;

public sealed partial class DvSession
{
    /// <summary>Shared production request boundary for CLI and Godot authoring clients.</summary>
    public object CaseReasoningRequest(string json)
    {
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        using var document = JsonDocument.Parse(json);
        var request = document.RootElement;
        if (request.ValueKind != JsonValueKind.Object) throw new ArgumentException("Case request must be an object.");
        string Text(string key) => request.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(field.GetString()) ? field.GetString()! : throw new ArgumentException("Missing or invalid " + key);
        bool Is(string key, string value) => request.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String && field.GetString() == value;
        long Integer(string key, long fallback)
        {
            if (!request.TryGetProperty(key, out var field)) return fallback;
            // Godot's JSON bridge emits integral values as floating-point JSON numbers.
            var value = field.GetDouble();
            if (!double.IsFinite(value) || value != Math.Truncate(value) || value < -9007199254740991d || value > 9007199254740991d)
                throw new ArgumentException("Expected an exact integer: " + key);
            return checked((long)value);
        }
        int Setting(string key, int fallback)
        {
            var value = Integer(key, fallback);
            if (value < int.MinValue || value > int.MaxValue) throw new ArgumentException("Setting is outside integer range: " + key);
            return (int)value;
        }
        var operation = Text("operation");
        if (operation == "sim4_plan" || operation == "strategy" && Is("planner", "sim4"))
            return PlanSim4(Setting("depth", 1), Setting("beam_width", 32), Setting("expansion_budget", 4096));
        if (operation == "sim4_execute") return ExecuteSim4(Text("plan_id"), Integer("state_version", -1));
        if (operation == "info") return request.TryGetProperty("audit", out var audit) && audit.ValueKind == JsonValueKind.True
            ? AuditCaseReasoningInfo() : CaseReasoningInfo;
        if (operation == "questions") return CaseQuestions();
        if (operation == "scene") return CaseScene();
        if (operation == "seal_scene") return SealCaseScene(Text("case_id"), Text("query_id"));
        if (operation == "review" && Is("view", "questions")) return CaseQuestions();
        // Raw actor evidence is an authoring operation. Ordinary player input cannot fabricate observations.
        if (operation is "audit" or "design" or "infer_design" or "check_design" or "prove_design" or "check_design_goal" or "travel" or "add_evidence")
            if (!DevMode) throw new InvalidOperationException("Case authoring operations require developer mode.");
        if (operation == "audit") return AuditCaseLibrary();
        var caseId = Text("case_id");
        return operation switch
        {
            "infer" when request.TryGetProperty("goal", out _) => ProveCaseActor(caseId, Viewer, Read<CaseLiteral>(request, "goal")),
            "infer" => InferCaseActor(caseId, Viewer),
            "check" when Is("logic", "natural") => CheckNaturalDeductionActor(caseId, Text("goal"), Text("proof")),
            "check" when request.TryGetProperty("goal", out _) => CheckCaseActorGoal(caseId, Viewer, Read<CaseProof>(request, "proof"), Read<CaseLiteral>(request, "goal")),
            "check" => CheckCaseActor(caseId, Viewer, Read<CaseProof>(request, "proof")),
            "prove" => ProveCaseActor(caseId, Viewer, Read<CaseLiteral>(request, "goal")),
            "check_goal" => CheckCaseActorGoal(caseId, Viewer, Read<CaseProof>(request, "proof"), Read<CaseLiteral>(request, "goal")),
            "check_natural" => CheckNaturalDeductionActor(caseId, Text("goal"), Text("proof")),
            "review" when Is("view", "strategy") => new { choices = ReviewCaseStrategy(caseId, Viewer) },
            "review" when Is("view", "travel") => EstimateActorTravelFromDv(caseId, Text("query_id")),
            "review" => new { candidates = ReviewCaseActor(caseId, Viewer) },
            "strategy" => new { choices = ReviewCaseStrategy(caseId, Viewer) },
            "travel_actor" => EstimateActorTravelFromDv(caseId, Text("query_id")),
            "design" => EvaluateCaseDesign(caseId, Text("scope")),
            "infer_design" => InferCaseDesign(caseId, Text("scope")),
            "check_design" => CheckCaseDesign(caseId, Text("scope"), Read<CaseProof>(request, "proof")),
            "prove_design" => ProveCaseDesign(caseId, Text("scope"), Read<CaseLiteral>(request, "goal")),
            "check_design_goal" => CheckCaseDesignGoal(caseId, Text("scope"), Read<CaseProof>(request, "proof"), Read<CaseLiteral>(request, "goal")),
            "travel" => EstimateCaseTravelFromDv(caseId, Text("query_id")),
            "add_evidence" => AddEvidence(),
            _ => throw new ArgumentException("Unknown case reasoning operation: " + operation)
        };
        object AddEvidence()
        {
            AddCaseActorEvidence(caseId, Viewer, Read<CaseAxiom>(request, "evidence"));
            return new { recorded = true, actor = Viewer, version = Version };
        }
    }

    private static T Read<T>(JsonElement request, string name)
    {
        if (!request.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Missing or invalid " + name);
        return field.Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ArgumentException("Invalid " + name);
    }
}
