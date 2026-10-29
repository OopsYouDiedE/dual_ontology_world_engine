using DuelView.Compiled;
using DuelView.Core;

/// <summary>Validates the selected spatial data before a host can publish it.</summary>
internal static class SpatialPublication
{
    private static readonly object Missing = new();
    private static object Field(object record, string key) => R.Bool(R.record_like(record)) ? R.record_find(record, key, Missing) : Missing;

    private static List<object> Items(object value, string path)
    {
        if (!R.TryItems(value, out var items)) throw new InvalidDataException($"Spatial {path} must be a list.");
        return items.ToList();
    }

    private static string Id(object record, string path)
    {
        if (Field(record, "id") is not string { Length: > 0 } id)
            throw new InvalidDataException($"Spatial {path} needs a nonempty id.");
        return id;
    }

    private static double Number(object value, string path)
    {
        if (value is not double number || !double.IsFinite(number))
            throw new InvalidDataException($"Spatial {path} must be a finite number.");
        return number;
    }

    private static double[] Vector(object value, int length, string path)
    {
        var items = Items(value, path);
        if (items.Count != length) throw new InvalidDataException($"Spatial {path} needs {length} numbers.");
        return items.Select((item, index) => Number(item, $"{path}/{index}")).ToArray();
    }

    private static void Rect(object value, string path)
    {
        var r = Vector(value, 4, path);
        if (r[2] <= r[0] || r[3] <= r[1]) throw new InvalidDataException($"Spatial {path} has nonpositive extent.");
    }

    private static object ResolveWorldDatum(ContentCatalog catalog, object manifest, object spatial)
    {
        var reference = Field(spatial, "world_datum_ref");
        if (Field(reference, "definition") is not string { Length: > 0 } id || Field(reference, "field") is not string { Length: > 0 } field)
            throw new InvalidDataException("Spatial world_datum_ref must identify a data definition and field.");
        if (!Items(Field(manifest, "definitions"), "manifest/definitions").Any(item => item is string candidate && candidate == id))
            throw new InvalidDataException($"World datum definition absent from manifest: {id}");
        if (Field(reference, "file") is not string { Length: > 0 } file || Path.GetFileName(file) != file)
            throw new InvalidDataException("Spatial world_datum_ref/file must be a published filename.");
        var definition = catalog.Get("data", id);
        if (definition is false) throw new InvalidDataException($"Missing world datum definition: {id}");
        var datum = Field(Field(definition, "value"), field);
        _ = Vector(datum, 3, $"{id}/{field}");
        return datum;
    }

    private static void ValidateRailingCandidate(object spatial, List<object> floors)
    {
        var candidate = Field(spatial, "railing_assembly_candidate");
        if (ReferenceEquals(candidate, Missing)) return;
        const string path = "railing_assembly_candidate";
        if (Field(candidate, "status") is not "full-path-candidate-not-published") throw new InvalidDataException($"Invalid {path}/status");
        foreach (var key in new[] { "max_post_spacing_m", "arc_tolerance_m", "post_radius_m", "rail_radius_m", "guard_thickness_m", "junction_tolerance_m" })
            if (Number(Field(candidate, key), $"{path}/{key}") <= 0) throw new InvalidDataException($"Invalid {path}/{key}");
        var paths = Items(Field(candidate, "paths"), path + "/paths");
        if (paths.Count == 0) throw new InvalidDataException($"Empty {path}/paths");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in paths)
        {
            var id = Id(item, path + "/paths");
            var itemPath = $"{path}/paths/{id}";
            if (!ids.Add(id)) throw new InvalidDataException($"Duplicate {itemPath}");
            var points = Items(Field(item, "points_design"), itemPath + "/points_design");
            var heights = Items(Field(item, "control_heights_m"), itemPath + "/control_heights_m");
            if (points.Count < 2 || heights.Count != points.Count) throw new InvalidDataException($"Invalid {itemPath}/points_design");
            for (var i = 0; i < points.Count; i++)
            {
                _ = Vector(points[i], 3, $"{itemPath}/points_design/{i}");
                if (Number(heights[i], $"{itemPath}/control_heights_m/{i}") <= 0) throw new InvalidDataException($"Invalid {itemPath}/control_heights_m/{i}");
            }
            foreach (var key in new[] { "own_start_post", "own_end_post" })
                if (Field(item, key) is not bool) throw new InvalidDataException($"Invalid {itemPath}/{key}");
            foreach (var key in new[] { "start_guard_cut_m", "end_guard_cut_m" }) _ = Vector(Field(item, key), 2, $"{itemPath}/{key}");
            var support = Field(item, "support");
            if (Field(support, "enabled") is not bool enabled) throw new InvalidDataException($"Invalid {itemPath}/support/enabled");
            if (enabled)
            {
                if (Vector(Field(support, "size_design"), 3, itemPath + "/support/size_design").Any(v => v <= 0)) throw new InvalidDataException($"Invalid {itemPath}/support/size_design");
                _ = Vector(Field(support, "offset_design"), 3, itemPath + "/support/offset_design");
            }
        }
        var usedEnds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var joint in Items(Field(candidate, "junctions"), path + "/junctions"))
        {
            if (Field(joint, "a") is not string a || Field(joint, "b") is not string b || a == b || !ids.Contains(a) || !ids.Contains(b)
                || Field(joint, "a_end") is not string ae || ae is not ("start" or "end") || Field(joint, "b_end") is not string be || be is not ("start" or "end")
                || !usedEnds.Add(a + ":" + ae) || !usedEnds.Add(b + ":" + be))
                throw new InvalidDataException($"Invalid {path}/junctions");
        }
        var replacements = Field(candidate, "replaces");
        foreach (var floor in floors)
        {
            var floorId = Id(floor, "floor");
            var group = Field(replacements, floorId);
            if (ReferenceEquals(group, Missing)) continue;
            foreach (var pair in new[] { ("pieces", "pieces"), ("colliders", "colliders") })
            {
                var allowed = Items(Field(floor, pair.Item2), $"{floorId}/{pair.Item2}").Select(item => Id(item, $"{floorId}/{pair.Item2}")).ToHashSet(StringComparer.Ordinal);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in Items(Field(group, pair.Item1), $"{path}/replaces/{floorId}/{pair.Item1}"))
                    if (entry is not string id || !allowed.Contains(id) || !seen.Add(id)) throw new InvalidDataException($"Invalid {path}/replaces/{floorId}/{pair.Item1}");
            }
        }
    }

    private static void ValidateAnchorCandidates(ContentCatalog catalog, object spatial, List<object> floors, HashSet<string> spatialIds)
    {
        var gates = Items(Field(spatial, "isolation_gate_candidates"), "isolation_gate_candidates");
        var byId = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var gate in gates)
        {
            var id = Id(gate, "isolation_gate_candidates");
            var path = $"isolation_gate_candidates/{id}";
            if (!spatialIds.Add(id) || !byId.TryAdd(id, gate)) throw new InvalidDataException($"Duplicate {path}");
            if (Field(gate, "status") is not "unconfigured") throw new InvalidDataException($"Invalid {path}/status");
            var center = Vector(Field(gate, "center"), 3, path + "/center");
            var floorId = Field(gate, "floor") as string;
            var floor = floors.SingleOrDefault(f => Field(f, "id") is string floorName && floorName == floorId);
            if (floor is null || Math.Abs(center[2] - Number(Field(floor, "z"), path + "/floor/z")) > 0.001) throw new InvalidDataException($"Invalid {path}/floor");
            foreach (var key in new[] { "clear_width_m", "clear_height_m" })
                if (Number(Field(gate, key), $"{path}/{key}") <= 0) throw new InvalidDataException($"Invalid {path}/{key}");
        }
        foreach (var anchor in Items(Field(spatial, "spatial_anchors"), "spatial_anchors"))
        {
            var id = Id(anchor, "spatial_anchors");
            var path = $"spatial_anchors/{id}";
            if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate {path}");
            if (Field(anchor, "status") is not "unconfigured") throw new InvalidDataException($"Invalid {path}/status");
            foreach (var key in new[] { "logical_location", "host_map", "host_room" })
                if (Field(anchor, key) is not string { Length: > 0 }) throw new InvalidDataException($"Invalid {path}/{key}");
            var placesDefinition = catalog.Get("data", "host.runtime.places");
            var places = Field(placesDefinition, "value");
            var place = Field(places, (string)Field(anchor, "logical_location"));
            if (!R.Bool(R.eq(Field(place, "map"), Field(anchor, "host_map"))) || !R.Bool(R.eq(Field(place, "room"), Field(anchor, "host_room"))))
                throw new InvalidDataException($"Host place mismatch: {path}/logical_location");
            if (Field(anchor, "gate_ref") is not string gateId || !byId.TryGetValue(gateId, out var gate)) throw new InvalidDataException($"Invalid {path}/gate_ref");
            if (Field(anchor, "floor") is not string { Length: > 0 } floorId) throw new InvalidDataException($"Invalid {path}/floor");
            var floor = floors.SingleOrDefault(f => Field(f, "id") is string floorName && floorName == floorId);
            if (floor is null || Field(gate, "floor") is not string gateFloor || gateFloor != floorId)
                throw new InvalidDataException($"Invalid {path}/floor");
            if (Field(anchor, "floor_piece_ref") is not string pieceId) throw new InvalidDataException($"Invalid {path}/floor_piece_ref");
            var piece = Items(Field(floor, "pieces"), floorId + "/pieces").SingleOrDefault(item => Field(item, "id") is string pieceName && pieceName == pieceId);
            if (piece is null || Field(piece, "kind") is not "floor") throw new InvalidDataException($"Invalid {path}/floor_piece_ref");
            var rect = Vector(Field(piece, "rect"), 4, path + "/floor_piece_ref/rect");
            var position = Vector(Field(anchor, "position"), 3, path + "/position");
            var center = Vector(Field(gate, "center"), 3, path + "/gate_ref/center");
            if (position[0] <= rect[0] || position[0] >= rect[2] || position[1] <= rect[1] || position[1] >= rect[3]
                || Math.Abs(position[2] - Number(Field(piece, "z"), path + "/floor_piece_ref/z") - Number(Field(piece, "height"), path + "/floor_piece_ref/height")) > 0.001)
                throw new InvalidDataException($"Invalid {path}/position");
            var halfWidth = Number(Field(gate, "clear_width_m"), path + "/gate_ref/clear_width_m") / 2;
            var validSide = Field(anchor, "side") switch
            {
                "south" => position[1] < center[1] && Math.Abs(position[0] - center[0]) <= halfWidth,
                "north" => position[1] > center[1] && Math.Abs(position[0] - center[0]) <= halfWidth,
                "west" => position[0] < center[0] && Math.Abs(position[1] - center[1]) <= halfWidth,
                "east" => position[0] > center[0] && Math.Abs(position[1] - center[1]) <= halfWidth,
                _ => false
            };
            if (!validSide)
                throw new InvalidDataException($"Invalid {path}/side");
        }
    }

    private static void ValidateDeclaredActions(ContentCatalog catalog, object manifest)
    {
        foreach (var entry in Items(Field(manifest, "definitions"), "manifest/definitions"))
        {
            if (entry is not string id) continue;
            var definition = catalog.Get("data", id);
            var value = Field(definition, "value");
            if (Field(value, "kind") is not "spatial-action") continue;
            var path = $"data/{id}";
            if (Field(value, "status") is not ("ready" or "unconfigured")) throw new InvalidDataException($"Invalid action status: {path}/status");
            if (Field(value, "method") is not string { Length: > 0 }) throw new InvalidDataException($"Missing action method: {path}/method");
            if (Field(value, "law_schema") is not string { Length: > 0 }) throw new InvalidDataException($"Missing action law schema: {path}/law_schema");
            if (Field(value, "rule_template") is not string templateId || catalog.Get("data", templateId) is false
                || Field(Field(catalog.Get("data", templateId), "value"), "kind") is not "dv-value-template")
                throw new InvalidDataException($"Invalid action template reference: {path}/rule_template");
            var parameters = Items(Field(Field(catalog.Get("data", templateId), "value"), "parameters"), path + "/rule_template/parameters");
            var arguments = Items(Field(value, "arguments"), path + "/arguments");
            if (parameters.Count != arguments.Count) throw new InvalidDataException($"Action argument count mismatch: {path}/arguments");
        }
    }

    private static void ValidateInteraction(ContentCatalog catalog, object item, string path)
    {
        var interaction = Field(item, "interaction");
        if (!R.Bool(R.record_like(interaction))) throw new InvalidDataException($"Spatial {path}/interaction must be a record.");
        var status = Field(interaction, "status");
        if (status is "unconfigured" or "noninteractive") return;
        if (status is not "ready") throw new InvalidDataException($"Spatial {path}/interaction/status is invalid.");
        if (Field(interaction, "coordinate_space") is not "building-local-meters") throw new InvalidDataException($"Invalid coordinate space: {path}/interaction/coordinate_space");
        if (Field(interaction, "size_space") is not "design-local-meters") throw new InvalidDataException($"Invalid size space: {path}/interaction/size_space");
        Number(Field(item, "model_forward_yaw"), path + "/model_forward_yaw");
        foreach (var field in new[] { "approach", "stand", "face_target", "contact", "area_center", "label_position" })
            _ = Vector(Field(interaction, field), 3, $"{path}/interaction/{field}");
        foreach (var field in new[] { "area_size" })
            if (Vector(Field(interaction, field), 3, $"{path}/interaction/{field}").Any(n => n <= 0))
                throw new InvalidDataException($"Spatial {path}/interaction/{field} must be positive.");
        var shapes = Items(Field(interaction, "collision_shapes"), $"{path}/interaction/collision_shapes");
        var collision = Field(item, "collision");
        if (collision is not bool enabled || enabled != (shapes.Count > 0))
            throw new InvalidDataException($"Spatial {path}/interaction/collision_shapes conflicts with collision.");
        for (var index = 0; index < shapes.Count; index++)
        {
            var shapePath = $"{path}/interaction/collision_shapes/{index}";
            var shape = shapes[index];
            if (Field(shape, "kind") is not "box") throw new InvalidDataException($"Unsupported shape: {shapePath}/kind");
            _ = Vector(Field(shape, "center"), 3, shapePath + "/center");
            if (Vector(Field(shape, "size"), 3, shapePath + "/size").Any(n => n <= 0))
                throw new InvalidDataException($"Spatial {shapePath}/size must be positive.");
        }
        foreach (var field in new[] { "actions", "resources", "state_visuals" })
        {
            var entries = Items(Field(interaction, field), $"{path}/interaction/{field}");
            if (entries.Any(entry => entry is not string { Length: > 0 }))
                throw new InvalidDataException($"Spatial {path}/interaction/{field} must contain nonempty identifiers.");
            if (field == "actions" && entries.Count == 0)
                throw new InvalidDataException($"Spatial {path}/interaction/actions must not be empty.");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var id = (string)entry;
                if (!unique.Add(id)) throw new InvalidDataException($"Duplicate identifier: {path}/interaction/{field}/{id}");
                var definition = catalog.Get("data", id);
                var expectedKind = field == "actions" ? "spatial-action" : field == "resources" ? "spatial-resource" : "spatial-state-visual";
                if (definition is false || Field(Field(definition, "value"), "kind") is not string kind || kind != expectedKind)
                    throw new InvalidDataException($"Invalid typed reference: {path}/interaction/{field}/{id}");
                if (field == "actions")
                {
                    var action = Field(definition, "value");
                    if (Field(action, "status") is not "ready")
                        throw new InvalidDataException($"Action not ready: {path}/interaction/actions/{id}/status");
                    if (Field(action, "method") is not string { Length: > 0 })
                        throw new InvalidDataException($"Action has no method binding: {path}/interaction/actions/{id}/method");
                    if (Field(action, "law_schema") is not string { Length: > 0 })
                        throw new InvalidDataException($"Action has no law schema: {path}/interaction/actions/{id}/law_schema");
                    if (Field(action, "rule_template") is not string templateId || catalog.Get("data", templateId) is false
                        || Field(Field(catalog.Get("data", templateId), "value"), "kind") is not "dv-value-template")
                        throw new InvalidDataException($"Action has no valid rule template: {path}/interaction/actions/{id}/rule_template");
                    var template = Field(catalog.Get("data", templateId), "value");
                    var parameters = Items(Field(template, "parameters"), $"{path}/interaction/actions/{id}/rule_template/parameters");
                    var arguments = Items(Field(action, "arguments"), $"{path}/interaction/actions/{id}/arguments");
                    if (parameters.Count != arguments.Count) throw new InvalidDataException($"Action argument count mismatch: {path}/interaction/actions/{id}/arguments");
                    var targetBound = false;
                    for (var i = 0; i < parameters.Count; i++)
                        if (parameters[i] is string parameter && parameter is "seat" or "object")
                        {
                            targetBound = true;
                            if (arguments[i] is not string target || target != (string)Field(item, "id"))
                                throw new InvalidDataException($"Action target mismatch: {path}/interaction/actions/{id}/arguments/{i}");
                        }
                    if (!targetBound) throw new InvalidDataException($"Action template has no object parameter: {path}/interaction/actions/{id}");
                }
            }
        }
    }

    // Formal assembly must call this gate; a partially declared pack may still
    // be loaded for CLI and content editing, but cannot publish these objects.
    internal static void RequireInteractionReadiness(ContentCatalog catalog, object manifest, object physical, IReadOnlyList<string> objectIds)
    {
        if (objectIds.Count == 0) throw new InvalidDataException("Spatial publication requires an explicit nonempty object manifest.");
        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in objectIds)
            if (string.IsNullOrWhiteSpace(id) || !requested.Add(id)) throw new InvalidDataException("Spatial publication has an invalid or duplicate object id.");
        var found = new HashSet<string>(StringComparer.Ordinal);
        var selected = (string)Field(manifest, "spatial_definition");
        var spatial = Field(catalog.Get("data", selected), "value");
        foreach (var floor in Items(Field(spatial, "floors"), "floors"))
        {
            var floorId = Id(floor, "floor");
            foreach (var item in Items(Field(floor, "furniture"), $"{floorId}/furniture"))
            {
                var id = Id(item, "furniture");
                if (!requested.Contains(id)) continue;
                found.Add(id);
                var path = $"{floorId}/furniture/{id}";
                ValidateInteraction(catalog, item, path);
                if (Field(Field(item, "interaction"), "status") is not ("ready" or "noninteractive"))
                    throw new InvalidDataException($"Spatial {path}/interaction is not ready for publication.");
                if (Field(Field(item, "interaction"), "status") is "ready")
                {
                    var adoptions = Field(physical, "adoptions");
                    foreach (var actionId in Items(Field(Field(item, "interaction"), "actions"), path + "/interaction/actions"))
                    {
                        var action = Field(catalog.Get("data", (string)actionId), "value");
                        var method = (string)Field(action, "method");
                        var template = Field(catalog.Get("data", (string)Field(action, "rule_template")), "value");
                        var parameters = Items(Field(template, "parameters"), path + "/action/parameters");
                        var arguments = Items(Field(action, "arguments"), path + "/action/arguments");
                        var objects = Field(physical, "objects");
                        for (var i = 0; i < parameters.Count; i++)
                            if (parameters[i] is string parameter && parameter is "actor" or "actor_state" or "seat" or "object")
                            {
                                if (arguments[i] is not string objectId || ReferenceEquals(Field(objects, objectId), Missing))
                                    throw new InvalidDataException($"Action object missing from physical world: {path}/interaction/actions/{actionId}/arguments/{i}");
                            }
                        var adoption = Field(adoptions, method);
                        if (ReferenceEquals(adoption, Missing) || Field(adoption, "mode") is not "enforce" || Field(adoption, "active") is not true
                            || !R.Bool(R.eq(Field(Field(adoption, "law"), "schema"), Field(action, "law_schema"))))
                            throw new InvalidDataException($"Action method not adopted for enforcement with declared schema: {path}/interaction/actions/{actionId}");
                    }
                }
            }
        }
        foreach (var id in requested)
            if (!found.Contains(id)) throw new InvalidDataException($"Unknown spatial publication object: {id}");
    }

    internal static IReadOnlyDictionary<string, object?> ProjectObjects(ContentCatalog catalog, object manifest, object physical, IReadOnlyList<string> objectIds)
    {
        RequireInteractionReadiness(catalog, manifest, physical, objectIds);
        var selected = (string)Field(manifest, "spatial_definition");
        var spatial = Field(catalog.Get("data", selected), "value");
        var index = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var floor in Items(Field(spatial, "floors"), "floors"))
            foreach (var item in Items(Field(floor, "furniture"), "furniture"))
                index.Add(Id(item, "furniture"), item);
        var objects = objectIds.Select(id => HostValues.FromDv(index[id])).ToArray();
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["content_hash"] = catalog.Hash,
            ["spatial_definition"] = selected,
            ["world_datum"] = HostValues.FromDv(ResolveWorldDatum(catalog, manifest, spatial)),
            ["world_datum_ref"] = HostValues.FromDv(Field(spatial, "world_datum_ref")),
            ["objects"] = objects
        });
    }

    internal static IReadOnlyDictionary<string, object?> ProjectFloorSurfaces(ContentCatalog catalog, object manifest, IReadOnlyList<string> pieceIds)
    {
        if (pieceIds.Count == 0) throw new InvalidDataException("Floor publication requires an explicit nonempty piece manifest.");
        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in pieceIds)
            if (string.IsNullOrWhiteSpace(id) || !requested.Add(id)) throw new InvalidDataException("Floor publication has an invalid or duplicate piece id.");
        var selected = (string)Field(manifest, "spatial_definition");
        var spatial = Field(catalog.Get("data", selected), "value");
        var index = new Dictionary<string, object>(StringComparer.Ordinal);
        var materials = Items(Field(spatial, "surface_materials"), "surface_materials").ToDictionary(item => Id(item, "surface_materials"), StringComparer.Ordinal);
        foreach (var floor in Items(Field(spatial, "floors"), "floors"))
            foreach (var piece in Items(Field(floor, "pieces"), "pieces"))
                if (Field(piece, "kind") is "floor") index.Add(Id(piece, "floor piece"), piece);
        foreach (var id in requested)
            if (!index.ContainsKey(id)) throw new InvalidDataException($"Unknown floor publication piece: {id}");
        return HostValues.Freeze(new Dictionary<string, object?>
        {
            ["content_hash"] = catalog.Hash,
            ["spatial_definition"] = selected,
            ["world_datum"] = HostValues.FromDv(ResolveWorldDatum(catalog, manifest, spatial)),
            ["world_datum_ref"] = HostValues.FromDv(Field(spatial, "world_datum_ref")),
            ["pieces"] = pieceIds.Select(id => HostValues.FromDv(index[id])).ToArray(),
            ["materials"] = pieceIds.Select(id => (string)Field(index[id], "material_ref")).Distinct(StringComparer.Ordinal)
                .ToDictionary(id => id, id => HostValues.FromDv(materials[id]), StringComparer.Ordinal)
        });
    }

    internal static void Validate(ContentCatalog catalog, object manifest)
    {
        ValidateDeclaredActions(catalog, manifest);
        if (Field(manifest, "spatial_definition") is not string { Length: > 0 } selected)
            throw new InvalidDataException("Campaign pack needs one spatial_definition.");
        if (!Items(Field(manifest, "definitions"), "manifest/definitions").Any(x => x is string id && id == selected))
            throw new InvalidDataException($"Selected spatial definition is absent from manifest: {selected}");
        var definition = catalog.Get("data", selected);
        if (definition is false) throw new InvalidDataException($"Missing selected spatial definition: {selected}");
        var spatial = Field(definition, "value");
        if (Field(spatial, "version") is not "configurable-building-v1")
            throw new InvalidDataException($"Unsupported spatial version: {selected}/version");
        if (Field(spatial, "units") is not "meters; X east, Y north, Z up")
            throw new InvalidDataException($"Unsupported spatial units and axes: {selected}/units");
        if (Field(spatial, "coordinate_space") is not "building-local")
            throw new InvalidDataException($"Unsupported spatial coordinate space: {selected}/coordinate_space");
        var actorProfile = Field(spatial, "host_actor_profile");
        if (Number(Field(actorProfile, "nominal_actor_height_m"), $"{selected}/host_actor_profile/nominal_actor_height_m") <= 0
            || Number(Field(actorProfile, "collision_radius_m"), $"{selected}/host_actor_profile/collision_radius_m") <= 0
            || Field(actorProfile, "source") is not string { Length: > 0 })
            throw new InvalidDataException($"Invalid host actor profile: {selected}/host_actor_profile");
        var datum = Vector(ResolveWorldDatum(catalog, manifest, spatial), 3, $"{selected}/world_datum_ref");
        var probes = Items(Field(spatial, "coordinate_probes"), $"{selected}/coordinate_probes");
        if (probes.Count < 4) throw new InvalidDataException($"Spatial {selected}/coordinate_probes needs at least four references.");
        var probeIds = new HashSet<string>(StringComparer.Ordinal);
        var localPoints = new List<double[]>();
        foreach (var probe in probes)
        {
            var id = Id(probe, "coordinate_probes");
            if (!probeIds.Add(id)) throw new InvalidDataException($"Duplicate coordinate probe: {id}");
            var local = Vector(Field(probe, "local"), 3, $"coordinate_probes/{id}/local");
            var world = Vector(Field(probe, "world"), 3, $"coordinate_probes/{id}/world");
            var godotWorld = Vector(Field(probe, "godot_world"), 3, $"coordinate_probes/{id}/godot_world");
            localPoints.Add(local);
            // Validate the authored reference values, not only their array shape.
            // Design: X east, Y north, Z up. Godot: X east, Y up, Z south.
            for (var axis = 0; axis < 3; axis++)
                if (Math.Abs(world[axis] - (local[axis] + datum[axis])) > 0.001)
                    throw new InvalidDataException($"Coordinate probe world datum mismatch: coordinate_probes/{id}/world/{axis}");
            var expected = new[] { world[0], world[2], -world[1] };
            for (var axis = 0; axis < 3; axis++)
                if (Math.Abs(godotWorld[axis] - expected[axis]) > 0.001)
                    throw new InvalidDataException($"Coordinate probe Godot axes mismatch: coordinate_probes/{id}/godot_world/{axis}");
            if (Field(probe, "source") is not string { Length: > 0 }) throw new InvalidDataException($"Missing coordinate probe source: {id}");
        }
        var noncollinear = false;
        for (var i = 0; i < localPoints.Count && !noncollinear; i++)
            for (var j = i + 1; j < localPoints.Count && !noncollinear; j++)
                for (var k = j + 1; k < localPoints.Count; k++)
                {
                    var a = localPoints[i]; var b = localPoints[j]; var c = localPoints[k];
                    if (Math.Abs((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])) >= 0.000001)
                    {
                        noncollinear = true;
                        break;
                    }
                }
        if (!noncollinear || !localPoints.Any(point => Math.Abs(point[2]-localPoints[0][2]) > 0.000001))
            throw new InvalidDataException($"Spatial {selected}/coordinate_probes must include noncollinear points and a different elevation.");
        var floors = Items(Field(spatial, "floors"), "floors");
        if (floors.Count == 0) throw new InvalidDataException("Spatial definition has no floors.");
        var floorIds = new HashSet<string>(StringComparer.Ordinal);
        var spatialIds = new HashSet<string>(StringComparer.Ordinal);
        var materialIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var material in Items(Field(spatial, "surface_materials"), "surface_materials"))
        {
            var id = Id(material, "surface_materials");
            var path = $"surface_materials/{id}";
            if (!spatialIds.Add(id) || !materialIds.Add(id)) throw new InvalidDataException($"Duplicate {path}");
            if (Field(material, "shader") is not "framework_surface" || Field(material, "floor_surface") is not true || Field(material, "status") is not ("provisional-study-source" or "ready"))
                throw new InvalidDataException($"Unsupported {path}/shader or status");
            if (Field(material, "base_color") is not string color || color.Length != 6 || !color.All(Uri.IsHexDigit)) throw new InvalidDataException($"Invalid {path}/base_color");
            var roughness = Number(Field(material, "roughness"), path + "/roughness");
            if (roughness < 0 || roughness > 1 || Number(Field(material, "texture_scale_m"), path + "/texture_scale_m") <= 0) throw new InvalidDataException($"Invalid {path} parameters");
        }
        var knownFloorIds = floors.Select(floor => Id(floor, "floor")).ToHashSet(StringComparer.Ordinal);
        var stairs = Items(Field(spatial, "effective_stairs"), "effective_stairs");
        var stairsById = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var stair in stairs)
        {
            var id = Id(stair, "effective_stairs");
            if (!stairsById.TryAdd(id, stair)) throw new InvalidDataException($"Duplicate stair id: effective_stairs/{id}");
        }
        var voidValues = Field(spatial, "voids");
        var voids = ReferenceEquals(voidValues, Missing) ? new List<object>() : Items(voidValues, "voids");
        var voidRects = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var voidDestinations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var value in voids)
        {
            var id = Id(value, "voids");
            var path = $"voids/{id}";
            if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {path}");
            if (Field(value, "stair_ref") is not string stairId || !stairsById.TryGetValue(stairId, out var stair))
                throw new InvalidDataException($"Invalid stair reference: {path}/stair_ref");
            var segments = Items(Field(stair, "segments"), $"effective_stairs/{stairId}/segments");
            var indexValue = Number(Field(value, "segment_index"), path + "/segment_index");
            if (indexValue < 0 || indexValue != Math.Truncate(indexValue) || indexValue >= segments.Count)
                throw new InvalidDataException($"Invalid segment index: {path}/segment_index");
            var segment = segments[(int)indexValue];
            if (Field(value, "source_field") is not "floor_opening_bbox") throw new InvalidDataException($"Unsupported void source: {path}/source_field");
            var rect = Vector(Field(segment, "floor_opening_bbox"), 4, $"effective_stairs/{stairId}/segments/{(int)indexValue}/floor_opening_bbox");
            if (rect[2] <= rect[0] || rect[3] <= rect[1]) throw new InvalidDataException($"Invalid void extent: {path}");
            if (Field(value, "from_floor") is not string from || !knownFloorIds.Contains(from) || Field(segment, "from") is not string actualFrom || actualFrom != from
                || Field(value, "to_floor") is not string to || !knownFloorIds.Contains(to) || Field(segment, "to") is not string actualTo || actualTo != to)
                throw new InvalidDataException($"Invalid void floor references: {path}");
            if (Field(value, "diagnostic_control_floor_piece_ref") is not string controlId || string.IsNullOrEmpty(controlId))
                throw new InvalidDataException($"Missing floor control reference: {path}/diagnostic_control_floor_piece_ref");
            var targetFloor = floors.Single(f => Id(f, "floor") == to);
            var control = Items(Field(targetFloor, "pieces"), $"{to}/pieces").SingleOrDefault(piece => Field(piece, "id") is string pieceId && pieceId == controlId);
            if (control is null || Field(control, "kind") is not "floor" || ReferenceEquals(Field(control, "rect"), Missing))
                throw new InvalidDataException($"Invalid floor control reference: {path}/diagnostic_control_floor_piece_ref");
            Rect(Field(control, "rect"), $"{to}/pieces/{controlId}/rect");
            if (Math.Abs(Number(Field(control, "z"), $"{to}/pieces/{controlId}/z") + Number(Field(control, "height"), $"{to}/pieces/{controlId}/height") - Number(Field(targetFloor, "z"), $"{to}/z")) > 0.001)
                throw new InvalidDataException($"Floor control elevation mismatch: {path}/diagnostic_control_floor_piece_ref");
            voidRects.Add(id, rect);
            voidDestinations.Add(id, to);
        }
        foreach (var floor in floors)
        {
            var floorId = Id(floor, "floor");
            if (!floorIds.Add(floorId) || !spatialIds.Add(floorId)) throw new InvalidDataException($"Duplicate floor id: {floorId}");
            var floorElevation = Number(Field(floor, "z"), $"{floorId}/z");
            var rooms = Items(Field(floor, "rooms"), $"{floorId}/rooms");
            var roomIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in rooms)
            {
                var id = Id(room, $"{floorId}/room");
                if (!roomIds.Add(id) || !spatialIds.Add(id)) throw new InvalidDataException($"Duplicate room id: {floorId}/{id}");
                if (Field(room, "name") is not string { Length: > 0 }) throw new InvalidDataException($"Room needs a name: {floorId}/{id}/name");
                Rect(Field(room, "bbox"), $"{floorId}/rooms/{id}/bbox");
            }
            foreach (var category in new[] { "pieces", "colliders", "openings", "furniture" })
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in Items(Field(floor, category), $"{floorId}/{category}"))
                {
                    var id = Id(item, $"{floorId}/{category}");
                    if (!ids.Add(id) || !spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {floorId}/{category}/{id}");
                    var path = $"{floorId}/{category}/{id}";
                    if (category == "pieces" && Field(item, "kind") is "floor" && (Field(item, "material_ref") is not string materialId || !materialIds.Contains(materialId)))
                        throw new InvalidDataException($"Invalid {path}/material_ref");
                    var roomValue = Field(item, "room");
                    if (category == "furniture" && ReferenceEquals(roomValue, Missing))
                        throw new InvalidDataException($"Spatial {path}/room is required.");
                    if (!ReferenceEquals(roomValue, Missing) && (roomValue is not string room || room.Length == 0 || !roomIds.Contains(room)))
                        throw new InvalidDataException($"Invalid room reference: {path}/room");
                    if (category != "furniture" && ReferenceEquals(Field(item, "rect"), Missing) && ReferenceEquals(Field(item, "points"), Missing))
                        throw new InvalidDataException($"Spatial geometry missing: {floorId}/{category}/{id}");
                    if (category == "furniture")
                    {
                        _ = Vector(Field(item, "position"), 3, path + "/position");
                        var size = Vector(Field(item, "size"), 3, path + "/size");
                        if (size.Any(n => n <= 0)) throw new InvalidDataException($"Spatial {path}/size must be positive.");
                        Number(Field(item, "yaw"), path + "/yaw");
                        if (Field(item, "kind") is not string { Length: > 0 }) throw new InvalidDataException($"Spatial {path}/kind is required.");
                        if (Field(item, "name") is not string { Length: > 0 }) throw new InvalidDataException($"Spatial {path}/name is required.");
                        if (Field(item, "collision") is not bool) throw new InvalidDataException($"Spatial {path}/collision must be boolean.");
                        ValidateInteraction(catalog, item, path);
                    }
                    else
                    {
                        var rect = Field(item, "rect");
                        if (!ReferenceEquals(rect, Missing))
                        {
                            Rect(rect, path + "/rect");
                            if (category != "openings")
                            {
                                Number(Field(item, "z"), path + "/z");
                                if (Number(Field(item, "height"), path + "/height") <= 0) throw new InvalidDataException($"Spatial {path}/height must be positive.");
                            }
                        }
                        var points = Field(item, "points");
                        if (!ReferenceEquals(points, Missing))
                        {
                            var vertices = Items(points, path + "/points");
                            if (vertices.Count < 6 || vertices.Count % 2 != 0) throw new InvalidDataException($"Spatial {path}/points needs paired prism vertices.");
                            for (var i = 0; i < vertices.Count; i++) _ = Vector(vertices[i], 3, $"{path}/points/{i}");
                        }
                        if (category == "openings" && Number(Field(item, "top"), path + "/top") <= Number(Field(item, "bottom"), path + "/bottom"))
                            throw new InvalidDataException($"Spatial {path}/top must exceed bottom.");
                    }
                }
            }
            var furniture = Items(Field(floor, "furniture"), $"{floorId}/furniture");
            var colliders = Items(Field(floor, "colliders"), $"{floorId}/colliders");
            var pieces = Items(Field(floor, "pieces"), $"{floorId}/pieces");
            var piecesById = pieces.ToDictionary(item => Id(item, $"{floorId}/pieces"), StringComparer.Ordinal);
            var collidersById = colliders.ToDictionary(item => Id(item, $"{floorId}/colliders"), StringComparer.Ordinal);
            foreach (var piece in pieces)
            {
                if (Field(piece, "kind") is not "floor") continue;
                var id = Id(piece, $"{floorId}/pieces");
                if (Field(piece, "collider_ref") is not string collisionId || !collidersById.TryGetValue(collisionId, out var collision)
                    || Field(collision, "piece_ref") is not string back || back != id || Field(collision, "kind") is not "floor")
                    throw new InvalidDataException($"Invalid floor collision binding: {floorId}/pieces/{id}/collider_ref");
                if (!R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(collision, "rect"))) || !R.Bool(R.eq(Field(piece, "z"), Field(collision, "z")))
                    || !R.Bool(R.eq(Field(piece, "height"), Field(collision, "height"))))
                    throw new InvalidDataException($"Floor geometry differs from collision: {floorId}/pieces/{id}");
                if (Math.Abs(Number(Field(piece, "z"), $"{floorId}/pieces/{id}/z") + Number(Field(piece, "height"), $"{floorId}/pieces/{id}/height") - floorElevation) > 0.000001)
                    throw new InvalidDataException($"Floor top differs from level: {floorId}/pieces/{id}/height");
            }
            foreach (var collision in colliders)
            {
                if (Field(collision, "kind") is not "floor") continue;
                var id = Id(collision, $"{floorId}/colliders");
                if (Field(collision, "piece_ref") is not string pieceId || !piecesById.TryGetValue(pieceId, out var piece)
                    || Field(piece, "collider_ref") is not string back || back != id)
                    throw new InvalidDataException($"Invalid floor piece binding: {floorId}/colliders/{id}/piece_ref");
            }
            var voidRefs = Field(floor, "void_refs");
            if (!ReferenceEquals(voidRefs, Missing))
                foreach (var reference in Items(voidRefs, $"{floorId}/void_refs"))
                {
                    if (reference is not string voidId || !voidRects.TryGetValue(voidId, out var hole) || voidDestinations[voidId] != floorId)
                        throw new InvalidDataException($"Invalid floor void reference: {floorId}/void_refs");
                    foreach (var piece in pieces)
                    {
                        if (Field(piece, "kind") is not "floor") continue;
                        var r = Vector(Field(piece, "rect"), 4, $"{floorId}/pieces/{Id(piece, "piece")}/rect");
                        if (Math.Min(r[2], hole[2]) - Math.Max(r[0], hole[0]) > 0.000001 && Math.Min(r[3], hole[3]) - Math.Max(r[1], hole[1]) > 0.000001)
                            throw new InvalidDataException($"Floor overlaps declared void: {floorId}/pieces/{Id(piece, "piece")}");
                    }
                }
            var openingById = Items(Field(floor, "openings"), $"{floorId}/openings").ToDictionary(item => Id(item, $"{floorId}/openings"), StringComparer.Ordinal);
            var wallsValue = Field(floor, "wall_segments");
            var usedPieces = new HashSet<string>(StringComparer.Ordinal);
            var usedColliders = new HashSet<string>(StringComparer.Ordinal);
            var usedOpenings = new HashSet<string>(StringComparer.Ordinal);
            var wallIds = new HashSet<string>(StringComparer.Ordinal);
            if (!ReferenceEquals(wallsValue, Missing))
                foreach (var wall in Items(wallsValue, $"{floorId}/wall_segments"))
                {
                    var id = Id(wall, $"{floorId}/wall_segments");
                    var path = $"{floorId}/wall_segments/{id}";
                    if (!spatialIds.Add(id) || !wallIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {path}");
                    var start = Vector(Field(wall, "start"), 3, path + "/start");
                    var end = Vector(Field(wall, "end"), 3, path + "/end");
                    var length = Math.Sqrt(Math.Pow(end[0] - start[0], 2) + Math.Pow(end[1] - start[1], 2));
                    if (length <= 0 || Math.Abs(end[2] - start[2]) > 0.000001) throw new InvalidDataException($"Invalid wall line: {path}");
                    if (Number(Field(wall, "height"), path + "/height") <= 0 || Number(Field(wall, "thickness"), path + "/thickness") <= 0)
                        throw new InvalidDataException($"Invalid wall dimensions: {path}");
                    _ = Vector(Field(wall, "start_cut"), 2, path + "/start_cut");
                    _ = Vector(Field(wall, "end_cut"), 2, path + "/end_cut");
                    foreach (var reference in Items(Field(wall, "openings"), path + "/openings"))
                    {
                        if (reference is not string openingId || !usedOpenings.Add(openingId) || !openingById.TryGetValue(openingId, out var opening)
                            || Field(opening, "wall_ref") is not string parent || parent != id)
                            throw new InvalidDataException($"Invalid opening reference: {path}/openings");
                        var from = Number(Field(opening, "from"), $"{floorId}/openings/{openingId}/from");
                        var to = Number(Field(opening, "to"), $"{floorId}/openings/{openingId}/to");
                        if (from < 0 || to <= from || to > length) throw new InvalidDataException($"Opening outside wall: {floorId}/openings/{openingId}");
                        var bottom = Number(Field(opening, "bottom"), $"{floorId}/openings/{openingId}/bottom");
                        var top = Number(Field(opening, "top"), $"{floorId}/openings/{openingId}/top");
                        if (bottom < start[2] || top > start[2] + Number(Field(wall, "height"), path + "/height"))
                            throw new InvalidDataException($"Opening outside wall height: {floorId}/openings/{openingId}");
                        var openingPath = $"{floorId}/openings/{openingId}";
                        var frame = Field(opening, "frame");
                        var door = Field(opening, "door");
                        if (ReferenceEquals(frame, Missing) && !ReferenceEquals(door, Missing))
                            throw new InvalidDataException($"Door requires frame: {openingPath}/door");
                        if (!ReferenceEquals(frame, Missing))
                        {
                            var frameId = Id(frame, openingPath + "/frame");
                            if (!spatialIds.Add(frameId)) throw new InvalidDataException($"Duplicate frame id: {openingPath}/frame");
                            var structuralWidth = Number(Field(opening, "structural_width"), openingPath + "/structural_width");
                            var structuralHeight = Number(Field(opening, "structural_height"), openingPath + "/structural_height");
                            var thickness = Number(Field(frame, "thickness"), openingPath + "/frame/thickness");
                            var netWidth = Number(Field(frame, "net_width"), openingPath + "/frame/net_width");
                            var netHeight = Number(Field(frame, "net_height"), openingPath + "/frame/net_height");
                            if (thickness <= 0 || netWidth <= 0 || netHeight <= 0 || Math.Abs(structuralWidth - (to - from)) > 0.000001
                                || Math.Abs(structuralHeight - (top - bottom)) > 0.000001 || Math.Abs(netWidth - (structuralWidth - 2 * thickness)) > 0.000001
                                || Math.Abs(netHeight - (structuralHeight - thickness)) > 0.000001)
                                throw new InvalidDataException($"Inconsistent frame dimensions: {openingPath}/frame");
                            _ = Vector(Field(frame, "origin_design"), 3, openingPath + "/frame/origin_design");
                            if (Number(Field(frame, "depth"), openingPath + "/frame/depth") <= 0) throw new InvalidDataException($"Invalid frame depth: {openingPath}/frame/depth");
                            Number(Field(frame, "orientation_yaw"), openingPath + "/frame/orientation_yaw");
                            Number(Field(frame, "model_forward_yaw"), openingPath + "/frame/model_forward_yaw");
                            if (!ReferenceEquals(door, Missing))
                            {
                            var doorId = Id(door, openingPath + "/door");
                            if (!spatialIds.Add(doorId)) throw new InvalidDataException($"Duplicate door id: {openingPath}/door");
                            _ = Vector(Field(door, "hinge_design"), 3, openingPath + "/door/hinge_design");
                            var leafWidth = Number(Field(door, "width"), openingPath + "/door/width");
                            var leafHeight = Number(Field(door, "height"), openingPath + "/door/height");
                            if (leafWidth <= 0 || leafWidth > netWidth || leafHeight <= 0 || leafHeight > netHeight || Number(Field(door, "thickness"), openingPath + "/door/thickness") <= 0)
                                throw new InvalidDataException($"Invalid leaf dimensions: {openingPath}/door");
                            Number(Field(door, "orientation_yaw"), openingPath + "/door/orientation_yaw");
                            Number(Field(door, "model_forward_yaw"), openingPath + "/door/model_forward_yaw");
                            if (Field(door, "angle_convention") is not "design-clockwise-degrees") throw new InvalidDataException($"Invalid door angle convention: {openingPath}/door/angle_convention");
                            var minAngle = Number(Field(door, "angle_min"), openingPath + "/door/angle_min");
                            var maxAngle = Number(Field(door, "angle_max"), openingPath + "/door/angle_max");
                            if (minAngle > 0 || maxAngle < 0 || minAngle >= maxAngle) throw new InvalidDataException($"Invalid leaf angle: {openingPath}/door");
                            if (Number(Field(door, "motion_duration_seconds"), openingPath + "/door/motion_duration_seconds") <= 0)
                                throw new InvalidDataException($"Invalid door motion duration: {openingPath}/door/motion_duration_seconds");
                            var openAngle = Number(Field(door, "open_angle"), openingPath + "/door/open_angle");
                            if (openAngle < minAngle || openAngle > maxAngle || Number(Field(door, "review_control_radius_m"), openingPath + "/door/review_control_radius_m") <= 0)
                                throw new InvalidDataException($"Invalid review opening: {openingPath}/door");
                            if (Field(door, "opens_into") is not string roomId || !roomIds.Contains(roomId)) throw new InvalidDataException($"Invalid open-side room: {openingPath}/door/opens_into");
                            var measurement = Field(opening, "clearance_measurement");
                            _ = Vector(Field(measurement, "center_design"), 3, openingPath + "/clearance_measurement/center_design");
                            var lateral = Vector(Field(measurement, "lateral_design"), 3, openingPath + "/clearance_measurement/lateral_design");
                            if (Math.Abs(lateral[2]) > 0.000001 || Math.Sqrt(lateral[0]*lateral[0] + lateral[1]*lateral[1]) <= 0.000001)
                                throw new InvalidDataException($"Invalid measurement direction: {openingPath}/clearance_measurement/lateral_design");
                            var heights = Items(Field(measurement, "heights_m"), openingPath + "/clearance_measurement/heights_m");
                            if (heights.Count == 0 || heights.Any(height => Number(height, openingPath + "/clearance_measurement/heights_m") <= 0 || Number(height, openingPath + "/clearance_measurement/heights_m") >= netHeight))
                                throw new InvalidDataException($"Invalid measurement heights: {openingPath}/clearance_measurement/heights_m");
                            if (Number(Field(measurement, "ray_extent_m"), openingPath + "/clearance_measurement/ray_extent_m") <= netWidth/2
                                || Number(Field(measurement, "tolerance_m"), openingPath + "/clearance_measurement/tolerance_m") <= 0)
                                throw new InvalidDataException($"Invalid measurement bounds: {openingPath}/clearance_measurement");
                            var replacedDoorPieces = new List<object>();
                            foreach (var oldReference in Items(Field(door, "replaces_pieces"), openingPath + "/door/replaces_pieces"))
                            {
                                if (oldReference is not string pieceId || !usedPieces.Add(pieceId) || !piecesById.TryGetValue(pieceId, out var oldPiece)
                                    || Field(oldPiece, "kind") is not ("door_panel" or "door_handle"))
                                    throw new InvalidDataException($"Invalid door replacement: {openingPath}/door/replaces_pieces");
                                replacedDoorPieces.Add(oldPiece);
                            }
                            foreach (var oldReference in Items(Field(door, "replaces_colliders"), openingPath + "/door/replaces_colliders"))
                            {
                                if (oldReference is not string colliderId || !usedColliders.Add(colliderId) || !collidersById.TryGetValue(colliderId, out var oldCollider)
                                    || Field(oldCollider, "kind") is not "door_panel"
                                    || replacedDoorPieces.Count(piece => Field(piece, "kind") is "door_panel"
                                        && R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(oldCollider, "rect")))
                                        && R.Bool(R.eq(Field(piece, "z"), Field(oldCollider, "z")))
                                        && R.Bool(R.eq(Field(piece, "height"), Field(oldCollider, "height")))) != 1)
                                    throw new InvalidDataException($"Invalid door collision replacement: {openingPath}/door/replaces_colliders");
                            }
                            }
                        }
                        var rect = Vector(Field(opening, "rect"), 4, $"{floorId}/openings/{openingId}/rect");
                        if (Math.Abs(end[0] - start[0]) < 0.000001 || Math.Abs(end[1] - start[1]) < 0.000001)
                        {
                            var vertical = Math.Abs(end[0] - start[0]) < 0.000001;
                            var axis = vertical ? 1 : 0;
                            var cross = 1 - axis;
                            var sign = Math.Sign(end[axis] - start[axis]);
                            var x1 = (rect[axis] - start[axis]) * sign;
                            var x2 = (rect[axis + 2] - start[axis]) * sign;
                            if (start[cross] < rect[cross] || start[cross] > rect[cross + 2]
                                || Math.Abs(Math.Min(x1, x2) - from) > 0.000001 || Math.Abs(Math.Max(x1, x2) - to) > 0.000001)
                                throw new InvalidDataException($"Opening geometry disagrees with wall: {floorId}/openings/{openingId}/rect");
                        }
                    }
                    var replaced = new List<object>();
                    foreach (var reference in Items(Field(wall, "replaces_pieces"), path + "/replaces_pieces"))
                    {
                        if (reference is not string pieceId || !usedPieces.Add(pieceId) || !piecesById.TryGetValue(pieceId, out var piece)
                            || Field(piece, "kind") is not string kind || !kind.StartsWith("wall_", StringComparison.Ordinal))
                            throw new InvalidDataException($"Invalid replaced piece reference: {path}/replaces_pieces");
                        replaced.Add(piece);
                    }
                    var replacedBodies = new List<object>();
                    foreach (var reference in Items(Field(wall, "replaces_colliders"), path + "/replaces_colliders"))
                    {
                        if (reference is not string colliderId || !usedColliders.Add(colliderId) || !collidersById.TryGetValue(colliderId, out var body)
                            || Field(body, "kind") is not string kind || !kind.StartsWith("wall_", StringComparison.Ordinal))
                            throw new InvalidDataException($"Invalid replaced collision reference: {path}/replaces_colliders");
                        replacedBodies.Add(body);
                    }
                    if (replaced.Count == 0 || replaced.Count != replacedBodies.Count)
                        throw new InvalidDataException($"Wall replacement count mismatch: {path}");
                    foreach (var piece in replaced)
                        if (replacedBodies.Count(body => R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(body, "rect")))
                            && R.Bool(R.eq(Field(piece, "z"), Field(body, "z"))) && R.Bool(R.eq(Field(piece, "height"), Field(body, "height")))) != 1)
                            throw new InvalidDataException($"Wall replacement geometry mismatch: {path}/replaces_colliders");
                    var splitsValue = Field(wall, "source_splits");
                    if (!ReferenceEquals(splitsValue, Missing))
                    {
                        var splits = Items(splitsValue, path + "/source_splits");
                        if (splits.Count != replaced.Count) throw new InvalidDataException($"Split source count mismatch: {path}/source_splits");
                        var horizontal = Math.Abs(end[1] - start[1]) < 0.000001;
                        var vertical = Math.Abs(end[0] - start[0]) < 0.000001;
                        if (horizontal == vertical) throw new InvalidDataException($"Split wall must be axis aligned: {path}");
                        var cross = horizontal ? 1 : 0;
                        var cutLow = start[cross] - Number(Field(wall, "thickness"), path + "/thickness") / 2;
                        var cutHigh = start[cross] + Number(Field(wall, "thickness"), path + "/thickness") / 2;
                        var seenSources = new HashSet<string>(StringComparer.Ordinal);
                        var seenResiduals = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var split in splits)
                        {
                            if (Field(split, "old_piece_id") is not string oldId || !seenSources.Add(oldId) || !piecesById.TryGetValue(oldId, out var oldPiece)
                                || !replaced.Contains(oldPiece) || Field(split, "old_collider_id") is not string oldBodyId || !collidersById.TryGetValue(oldBodyId, out var oldBody) || !replacedBodies.Contains(oldBody))
                                throw new InvalidDataException($"Invalid split source: {path}/source_splits");
                            var oldRect = Vector(Field(oldPiece, "rect"), 4, path + "/source_splits/old_rect");
                            if (!R.Bool(DVKernel.F_dv_same(Field(oldPiece, "rect"), Field(oldBody, "rect"))) || !R.Bool(R.eq(Field(oldPiece, "z"), Field(oldBody, "z"))) || !R.Bool(R.eq(Field(oldPiece, "height"), Field(oldBody, "height")))
                                || oldRect[cross] >= cutLow || oldRect[cross + 2] <= cutHigh)
                                throw new InvalidDataException($"Invalid split source geometry: {path}/source_splits/{oldId}");
                            var pieceRefs = Items(Field(split, "residual_piece_ids"), path + "/source_splits/residual_piece_ids");
                            var bodyRefs = Items(Field(split, "residual_collider_ids"), path + "/source_splits/residual_collider_ids");
                            if (pieceRefs.Count != 2 || bodyRefs.Count != 2) throw new InvalidDataException($"Invalid split residual count: {path}/source_splits/{oldId}");
                            for (var i = 0; i < 2; i++)
                            {
                                if (pieceRefs[i] is not string newId || !seenResiduals.Add(newId) || !piecesById.TryGetValue(newId, out var piece)
                                    || bodyRefs[i] is not string newBodyId || !seenResiduals.Add(newBodyId) || !collidersById.TryGetValue(newBodyId, out var body)
                                    || Field(piece, "collider_ref") is not string pieceBody || pieceBody != newBodyId || Field(body, "piece_ref") is not string bodyPiece || bodyPiece != newId
                                    || !R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(body, "rect"))) || !R.Bool(R.eq(Field(piece, "z"), Field(oldPiece, "z")))
                                    || !R.Bool(R.eq(Field(piece, "height"), Field(oldPiece, "height"))) || !R.Bool(R.eq(Field(body, "z"), Field(oldPiece, "z"))) || !R.Bool(R.eq(Field(body, "height"), Field(oldPiece, "height"))))
                                    throw new InvalidDataException($"Invalid split residual references: {path}/source_splits/{oldId}");
                                var expected = (double[])oldRect.Clone();
                                if (i == 0) expected[cross + 2] = cutLow; else expected[cross] = cutHigh;
                                var actual = Vector(Field(piece, "rect"), 4, path + "/source_splits/rect");
                                if (actual.Where((v, index) => Math.Abs(v - expected[index]) > 0.000001).Any()) throw new InvalidDataException($"Invalid split residual geometry: {path}/source_splits/{oldId}");
                            }
                        }
                    }
                }
            var straightValues = Field(floor, "straight_stairs");
            if (!ReferenceEquals(straightValues, Missing))
                foreach (var flight in Items(straightValues, $"{floorId}/straight_stairs"))
                {
                    var id = Id(flight, "straight_stairs");
                    var path = $"{floorId}/straight_stairs/{id}";
                    if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {path}");
                    if (Field(flight, "stair_ref") is not string stairId || !stairsById.TryGetValue(stairId, out var stair)) throw new InvalidDataException($"Invalid stair reference: {path}/stair_ref");
                    var segments = Items(Field(stair, "segments"), $"effective_stairs/{stairId}/segments");
                    var segmentIndex = Number(Field(flight, "segment_index"), path + "/segment_index");
                    if (segmentIndex < 0 || segmentIndex != Math.Truncate(segmentIndex) || segmentIndex >= segments.Count) throw new InvalidDataException($"Invalid segment index: {path}/segment_index");
                    var segment = segments[(int)segmentIndex];
                    if (Field(segment, "from") is not string fromFloor || fromFloor != floorId) throw new InvalidDataException($"Flight is not on its source floor: {path}");
                    var points = Items(Field(segment, "points"), path + "/points");
                    var fromIndex = Number(Field(flight, "from_point_index"), path + "/from_point_index");
                    var toIndex = Number(Field(flight, "to_point_index"), path + "/to_point_index");
                    if (fromIndex < 0 || fromIndex != Math.Truncate(fromIndex) || toIndex != fromIndex + 1 || toIndex >= points.Count) throw new InvalidDataException($"Invalid flight points: {path}");
                    var start = Vector(points[(int)fromIndex], 3, path + "/start");
                    var end = Vector(points[(int)toIndex], 3, path + "/end");
                    var run = Math.Sqrt(Math.Pow(end[0]-start[0],2)+Math.Pow(end[1]-start[1],2));
                    var widths = Items(Field(segment, "segment_widths"), path + "/segment_widths");
                    var widthIndex = Number(Field(flight, "width_index"), path + "/width_index");
                    if (widthIndex < 0 || widthIndex != Math.Truncate(widthIndex) || widthIndex >= widths.Count || Number(widths[(int)widthIndex], path + "/width") <= 0) throw new InvalidDataException($"Invalid flight width: {path}");
                    if (Field(flight, "count_field") is not "steps_per_flight") throw new InvalidDataException($"Unsupported step count source: {path}/count_field");
                    var count = Number(Field(segment, "steps_per_flight"), path + "/count");
                    if (count <= 0 || count != Math.Truncate(count) || end[2] <= start[2] || run <= 0
                        || Math.Abs(run - count*Number(Field(segment, "tread"), path + "/tread")) > 0.000001
                        || Math.Abs(end[2]-start[2]-count*Number(Field(segment, "riser"), path + "/riser")) > 0.000001)
                        throw new InvalidDataException($"Flight dimensions disagree with stair source: {path}");
                    if (Number(Field(flight, "clearance_max_spacing"), path + "/clearance_max_spacing") <= 0) throw new InvalidDataException($"Invalid clearance sampling interval: {path}/clearance_max_spacing");
                    if (Number(Field(flight, "tread_thickness"), path + "/tread_thickness") <= 0 || Number(Field(flight, "ramp_thickness"), path + "/ramp_thickness") <= 0)
                        throw new InvalidDataException($"Invalid flight thickness: {path}");
                    var oldPieces = Items(Field(flight, "replaces_pieces"), path + "/replaces_pieces");
                    if (oldPieces.Count != count) throw new InvalidDataException($"Flight step count differs from replaced pieces: {path}");
                    foreach (var reference in oldPieces)
                        if (reference is not string pieceId || !usedPieces.Add(pieceId) || !piecesById.TryGetValue(pieceId, out var piece) || Field(piece, "kind") is not "stairs")
                            throw new InvalidDataException($"Invalid flight piece: {path}/replaces_pieces");
                    var oldBodies = Items(Field(flight, "replaces_colliders"), path + "/replaces_colliders");
                    if (oldBodies.Count != 1) throw new InvalidDataException($"Flight needs one source ramp: {path}");
                    foreach (var reference in oldBodies)
                        if (reference is not string colliderId || !usedColliders.Add(colliderId) || !collidersById.TryGetValue(colliderId, out var collider) || Field(collider, "kind") is not "stair_ramp")
                            throw new InvalidDataException($"Invalid flight ramp: {path}/replaces_colliders");
                }
            var ceilingValues = Field(floor, "ceiling_surfaces");
            if (!ReferenceEquals(ceilingValues, Missing))
                foreach (var ceiling in Items(ceilingValues, $"{floorId}/ceiling_surfaces"))
                {
                    var id = Id(ceiling, "ceiling_surfaces");
                    var path = $"{floorId}/ceiling_surfaces/{id}";
                    if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: {path}");
                    if (Field(ceiling, "room_ref") is not string room || !roomIds.Contains(room)) throw new InvalidDataException($"Invalid room reference: {path}/room_ref");
                    foreach (var reference in Items(Field(ceiling, "hole_refs"), path + "/hole_refs"))
                        if (reference is not string hole || !voidRects.ContainsKey(hole)) throw new InvalidDataException($"Invalid ceiling hole: {path}/hole_refs");
                    var underside = Number(Field(ceiling, "underside"), path + "/underside");
                    var thickness = Number(Field(ceiling, "thickness"), path + "/thickness");
                    if (thickness <= 0 || Field(ceiling, "collision_enabled") is not bool) throw new InvalidDataException($"Invalid ceiling declaration: {path}");
                    var ceilingPieces = new List<object>();
                    foreach (var reference in Items(Field(ceiling, "replaces_pieces"), path + "/replaces_pieces"))
                    {
                        if (reference is not string pieceId || !usedPieces.Add(pieceId) || !piecesById.TryGetValue(pieceId, out var piece)
                            || Field(piece, "kind") is not "ceiling" || Field(piece, "room") is not string pieceRoom || pieceRoom != room
                            || Math.Abs(Number(Field(piece, "z"), path + "/underside") - underside) > 0.000001
                            || Math.Abs(Number(Field(piece, "height"), path + "/thickness") - thickness) > 0.000001)
                            throw new InvalidDataException($"Invalid ceiling piece reference: {path}/replaces_pieces");
                        ceilingPieces.Add(piece);
                    }
                    foreach (var reference in Items(Field(ceiling, "replaces_colliders"), path + "/replaces_colliders"))
                    {
                        if (reference is not string colliderId || !usedColliders.Add(colliderId) || !collidersById.TryGetValue(colliderId, out var collider)
                            || Field(collider, "kind") is not "ceiling" || ceilingPieces.Count(piece => R.Bool(DVKernel.F_dv_same(Field(piece, "rect"), Field(collider, "rect")))
                                && R.Bool(R.eq(Field(piece, "z"), Field(collider, "z"))) && R.Bool(R.eq(Field(piece, "height"), Field(collider, "height")))) != 1)
                            throw new InvalidDataException($"Invalid ceiling collision reference: {path}/replaces_colliders");
                    }
                }
            foreach (var opening in openingById.Values)
            {
                var parent = Field(opening, "wall_ref");
                if (!ReferenceEquals(parent, Missing) && (parent is not string wallId || !wallIds.Contains(wallId) || !usedOpenings.Contains(Id(opening, "opening"))))
                    throw new InvalidDataException($"Invalid reverse wall reference: {floorId}/openings/{Id(opening, "opening")}/wall_ref");
            }
            var furnitureById = furniture.ToDictionary(item => Id(item, $"{floorId}/furniture"), StringComparer.Ordinal);
            var furnitureIds = furnitureById.Keys.ToHashSet(StringComparer.Ordinal);
            foreach (var item in furniture)
            {
                var id = Id(item, $"{floorId}/furniture");
                var references = Field(item, "collision_refs");
                if (ReferenceEquals(references, Missing)) continue;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reference in Items(references, $"{floorId}/furniture/{id}/collision_refs"))
                    if (reference is not string collider || !seen.Add(collider) || !collidersById.TryGetValue(collider, out var body)
                        || Field(body, "object_ref") is not string owner || owner != id)
                        throw new InvalidDataException($"Invalid collision reference: {floorId}/furniture/{id}/collision_refs");
            }
            foreach (var collider in colliders)
            {
                var reference = Field(collider, "object_ref");
                if (ReferenceEquals(reference, Missing)) continue;
                var colliderId = Id(collider, "collider");
                if (reference is not string target || !furnitureIds.Contains(target)
                    || !Items(Field(furnitureById[target], "collision_refs"), $"{floorId}/furniture/{target}/collision_refs").Any(item => item is string refId && refId == colliderId))
                    throw new InvalidDataException($"Invalid object reference: {floorId}/colliders/{colliderId}/object_ref");
            }
        }
        ValidateRailingCandidate(spatial, floors);
        ValidateAnchorCandidates(catalog, spatial, floors, spatialIds);
        foreach (var stair in Items(Field(spatial, "effective_stairs"), "effective_stairs"))
        {
            var id = Id(stair, "effective_stairs");
            if (!spatialIds.Add(id)) throw new InvalidDataException($"Duplicate spatial id: effective_stairs/{id}");
            foreach (var level in Items(Field(stair, "levels"), $"effective_stairs/{id}/levels"))
                if (level is not string floor || !floorIds.Contains(floor)) throw new InvalidDataException($"Unknown level: effective_stairs/{id}/levels");
            foreach (var segment in Items(Field(stair, "segments"), $"effective_stairs/{id}/segments"))
                foreach (var field in new[] { "from", "to" })
                    if (Field(segment, field) is not string floor || !floorIds.Contains(floor)) throw new InvalidDataException($"Unknown level: effective_stairs/{id}/segments/{field}");
        }
    }
}
