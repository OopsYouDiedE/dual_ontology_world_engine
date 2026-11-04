using DuelView.Compiled;
using DuelView.Core;

public sealed partial class DvSession
{
    /// <summary>Project an explicitly declared interaction for the current prepared action.
    /// Call on the session owner thread, alongside Prepare. This does not reserve or commit state.</summary>
    public IReadOnlyDictionary<string, object?> PreparedSpatialExecutionValue()
    {
        using var valuesScope = R.UseValues(valueContext);
        if (IsClosed) throw new InvalidOperationException("The session is closed.");
        var pending = Field(state, "pending", false);
        if (pending is false) return HostValues.Empty;
        var action = Field(pending, "action", false);
        var reference = Field(action, "spatial_execution_ref", false);
        if (reference is false) return HostValues.Empty;
        if (reference is not string { Length: > 0 } definitionId)
            throw new InvalidDataException("Prepared spatial action needs a nonempty spatial_execution_ref.");
        var actionEvent = R.Text(Field(pending, "action_event", ""));
        var actor = R.Text(Field(pending, "id", ""));
        var actionId = R.Text(Field(action, "id", ""));
        var definition = Field(content.Get("data", definitionId), "value", false);
        if (definition is false) throw new InvalidDataException($"Missing spatial action definition: {definitionId}");
        if (Field(definition, "kind", false) is not "spatial-action")
            throw new InvalidDataException($"Not a spatial action definition: {definitionId}");
        var result = new Dictionary<string, object?>
        {
            ["action_event"] = actionEvent, ["actor"] = actor, ["action_id"] = actionId,
            ["spatial_execution_ref"] = definitionId, ["state_version"] = Version,
            ["content_hash"] = content.Hash
        };
        var spec = Field(definition, "spatial_execution", false);
        if (Field(definition, "status", false) is not "ready" || spec is false || Field(spec, "status", false) is not "ready")
        {
            result["status"] = "blocked";
            result["reason"] = "spatial-action-unconfigured";
            return HostValues.Freeze(result);
        }
        string Text(object value, string key)
        {
            if (Field(value, key, false) is not string { Length: > 0 } text)
                throw new InvalidDataException($"Spatial action {definitionId}/{key} needs a nonempty identifier.");
            return text;
        }
        double Positive(object value, string key)
        {
            var number = Field(value, key, false);
            if (!R.Bool(R.is_numeric_value(number)) || !double.IsFinite(R.Num(number)) || R.Num(number) <= 0)
                throw new InvalidDataException($"Spatial action {definitionId}/{key} needs a finite positive number.");
            return R.Num(number);
        }
        if (Text(spec, "version") != "spatial-action-v1")
            throw new InvalidDataException($"Unsupported spatial action version: {definitionId}");
        var target = Text(spec, "target_id");
        if (!R.Bool(R.eq(Field(action, "law", false), Field(definition, "method", false))))
            throw new InvalidDataException($"Spatial action method mismatch: {definitionId}");
        var ruleTemplate = Field(content.Get("data", Text(definition, "rule_template")), "value", false);
        var parameters = R.Items(Field(ruleTemplate, "parameters", false));
        var arguments = R.Items(Field(definition, "arguments", false));
        if (parameters.Count != arguments.Count)
            throw new InvalidDataException($"Spatial action argument count mismatch: {definitionId}");
        var actorBound = false;
        var targetBound = false;
        for (var index = 0; index < parameters.Count; index++)
        {
            if (parameters[index] is "actor")
            {
                actorBound = true;
                if (arguments[index] is not string person || person != actor)
                    throw new InvalidDataException($"Spatial action actor mismatch: {definitionId}");
            }
            if (parameters[index] is "seat" or "object")
            {
                targetBound = true;
                if (arguments[index] is not string objectId || objectId != target)
                    throw new InvalidDataException($"Spatial action target mismatch: {definitionId}");
            }
            if (parameters[index] is string angleParameter && angleParameter is "from_angle" or "target_angle")
            {
                var boundMotion = Field(spec, "object_motion", false);
                var expected = Field(boundMotion, angleParameter == "from_angle" ? "from_degrees" : "target_degrees", false);
                if (!R.Bool(R.is_numeric_value(arguments[index])) || !R.Bool(R.is_numeric_value(expected))
                    || !R.Bool(R.eq(arguments[index], expected)))
                    throw new InvalidDataException($"Spatial action law/motion angle mismatch: {definitionId}/{angleParameter}");
            }
        }
        if (!actorBound || !targetBound)
            throw new InvalidDataException($"Spatial action lacks explicit actor and target parameters: {definitionId}");
        // Recheck the original declaration; the published object projection is not player knowledge.
        var pack = Field(content.Get("data", "campaign.pack"), "value", false);
        var selected = Text(pack, "spatial_definition");
        var spatial = Field(content.Get("data", selected), "value", false);
        object? found = null;
        foreach (var floor in R.Items(Field(spatial, "floors", false)))
            foreach (var item in SpatialPublication.InteractionTargets(floor))
                if (Field(item, "id", false) is string id && id == target) found = item;
        if (found is null) throw new InvalidDataException($"Spatial target not published: {target}");
        var interaction = Field(found, "interaction", false);
        if (!R.Bool(R.record_like(interaction)) || Field(interaction, "status", false) is not "ready")
        {
            result["status"] = "blocked";
            result["reason"] = "spatial-target-unconfigured";
            return HostValues.Freeze(result);
        }
        var publication = ProjectSpatialObjects(new[] { target });
        if (!R.Items(Field(interaction, "actions", false)).Any(id => id is string text && text == definitionId))
            throw new InvalidDataException($"Spatial action is not declared by target {target}: {definitionId}");
        var resources = R.Items(Field(spec, "resource_ids", false));
        var declaredResources = R.Items(Field(interaction, "resources", false));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in resources)
            if (resource is not string { Length: > 0 } resourceId || !seen.Add(resourceId)
                || !declaredResources.Any(value => value is string allowed && allowed == resourceId))
                throw new InvalidDataException($"Spatial action resource is undeclared or duplicated: {definitionId}");
        var visual = Field(spec, "visual", false);
        // Godot's unnamed animation library is represented by an explicit empty
        // string. Missing is still invalid; hosts must not guess a library.
        if (Field(visual, "library", false) is not string animationLibrary)
            throw new InvalidDataException($"Spatial action {definitionId}/visual/library needs an explicit string.");
        if (Field(visual, "anchor_root", false) is not bool anchorRoot)
            throw new InvalidDataException($"Spatial action {definitionId}/visual/anchor_root needs an explicit boolean.");
        var checkedVisual = new Dictionary<string, object?>
        {
            ["library"] = animationLibrary, ["clip"] = Text(visual, "clip"),
            ["anchor_root"] = anchorRoot, ["speed"] = Positive(visual, "speed")
        };
        result["status"] = "ready";
        result["version"] = "spatial-action-v1";
        result["target_id"] = target;
        foreach (var key in new[] { "stand_slot", "face_slot", "contact_slot", "actor_contact_slot", "actor_facing_slot" })
            result[key] = Text(spec, key);
        var contactPhase = Text(spec, "contact_phase");
        if (contactPhase is not ("before-and-finish" or "during-visual" or "before-motion"))
            throw new InvalidDataException($"Unknown spatial contact phase: {definitionId}");
        var motion = Field(spec, "object_motion", false);
        if (motion is not false)
        {
            if (contactPhase != "before-motion" || Text(motion, "kind") != "hinged-door"
                || Text(motion, "angle_convention") != "design-clockwise-degrees")
                throw new InvalidDataException($"Unsupported object motion contract: {definitionId}");
            double Angle(string key)
            {
                var value = Field(motion, key, false);
                if (!R.Bool(R.is_numeric_value(value)) || !double.IsFinite(R.Num(value)))
                    throw new InvalidDataException($"Invalid object motion angle: {definitionId}/{key}");
                var angle = R.Num(value);
                var minimum = Field(found, "angle_min", false);
                var maximum = Field(found, "angle_max", false);
                if (!R.Bool(R.is_numeric_value(minimum)) || !R.Bool(R.is_numeric_value(maximum))
                    || angle < R.Num(minimum) || angle > R.Num(maximum))
                    throw new InvalidDataException($"Object motion outside declared door range: {definitionId}/{key}");
                return angle;
            }
            result["object_motion"] = new Dictionary<string, object?>
            {
                ["kind"] = "hinged-door", ["angle_convention"] = "design-clockwise-degrees",
                ["from_degrees"] = Angle("from_degrees"), ["target_degrees"] = Angle("target_degrees"),
                ["speed_degrees_s"] = Positive(motion, "speed_degrees_s"),
                ["timeout_s"] = Positive(motion, "timeout_s"),
                ["tolerance_degrees"] = Positive(motion, "tolerance_degrees")
            };
        }
        else if (contactPhase == "before-motion")
            throw new InvalidDataException($"Before-motion contact needs object motion: {definitionId}");
        var posture = Field(spec, "actor_posture", false);
        if (posture is not false)
        {
            var from = Text(posture, "from"); var to = Text(posture, "to");
            if (!((from == "standing" && to == "sitting") || (from == "sitting" && to == "standing")) || motion is not false)
                throw new InvalidDataException($"Unsupported actor posture transition: {definitionId}");
            var checkedPosture = new Dictionary<string, object?> { ["from"] = from, ["to"] = to };
            foreach (var key in new[] { "seat_slot", "exit_slot", "seat_surface_slot", "left_foot_slot", "right_foot_slot" })
            {
                var slot = Text(posture, key);
                var coordinates = R.Items(Field(interaction, slot, new List<object>()));
                if (coordinates.Count != 3 || coordinates.Any(value => !R.Bool(R.is_numeric_value(value)) || !double.IsFinite(R.Num(value))))
                    throw new InvalidDataException($"Actor posture slot must have three published finite coordinates: {definitionId}/{slot}");
                checkedPosture[key] = slot;
            }
            foreach (var key in new[] { "position_tolerance_m", "foot_tolerance_m", "timeout_s" })
                checkedPosture[key] = Positive(posture, key);
            result["actor_posture"] = checkedPosture;
        }
        result["contact_phase"] = contactPhase;
        foreach (var key in new[] { "arrival_tolerance_m", "contact_tolerance_m", "facing_tolerance_rad", "approach_timeout_s", "turn_timeout_s", "visual_timeout_s" })
            result[key] = Positive(spec, key);
        result["resource_ids"] = resources.Cast<object?>().ToArray();
        result["visual"] = checkedVisual;
        result["spatial_definition"] = publication["spatial_definition"];
        return HostValues.Freeze(result);
    }
}
