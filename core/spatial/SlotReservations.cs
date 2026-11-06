// @mechanism: native_3d_architect_and_prefabs
// @layer: core/spatial
// @contracts: SlotReservations

using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;

// --- Source: SlotReservations.cs ---
namespace DuelView.Runtime
{

public readonly record struct ReservationOwner(string Actor,long Execution);
public sealed record ReservationCheckpoint(ReservationOwner Owner,string[] Resources);

/// <summary>Session-owned standing/resource leases, independent of scene nodes.
/// One actor may retain a posture lease while its next execution acquires a slot.</summary>
public sealed class SlotReservations
{
    private sealed class Slot(int capacity)
    {
        internal readonly int Capacity=capacity;
        internal readonly Dictionary<string,HashSet<long>> Actors=new(StringComparer.Ordinal);
    }
    private readonly Dictionary<string,Slot> slots=new(StringComparer.Ordinal);
    private readonly Dictionary<ReservationOwner,HashSet<string>> owners=[];
    public ReservationCheckpoint[] SaveState()=>owners.Select(pair=>new ReservationCheckpoint(pair.Key,pair.Value.ToArray())).ToArray();
    public void RestoreState(ReservationCheckpoint[] state)
    {
        if(state is null || state.Select(item=>item.Owner).Distinct().Count()!=state.Length)throw new InvalidDataException("Invalid reservation checkpoint.");
        var counts=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        foreach(var item in state)
        {
            if(string.IsNullOrWhiteSpace(item.Owner.Actor)||item.Owner.Execution<0||item.Resources is null||item.Resources.Distinct(StringComparer.Ordinal).Count()!=item.Resources.Length)
                throw new InvalidDataException("Invalid reservation owner checkpoint.");
            foreach(var id in item.Resources)
            {
                if(!slots.ContainsKey(id))throw new InvalidDataException("Checkpoint references missing reservation: "+id);
                if(!counts.TryGetValue(id,out var actors))counts[id]=actors=new(StringComparer.Ordinal);
                actors.Add(item.Owner.Actor);
            }
        }
        if(counts.Any(item=>item.Value.Count>slots[item.Key].Capacity))throw new InvalidDataException("Checkpoint exceeds reservation capacity.");
        foreach(var slot in slots.Values)slot.Actors.Clear();owners.Clear();
        foreach(var item in state)if(!TryAcquire(item.Owner,item.Resources))throw new InvalidDataException("Cannot restore reservation checkpoint.");
    }

    public void Define(string id,int capacity)
    {
        if(string.IsNullOrWhiteSpace(id)||capacity<1)throw new ArgumentException("A standing resource needs an identity and positive capacity.");
        slots.Add(id,new(capacity));
    }

    public bool TryAcquire(ReservationOwner owner,IEnumerable<string> resources)
    {
        if(string.IsNullOrWhiteSpace(owner.Actor)||owner.Execution<0)throw new ArgumentException("A reservation needs an actor and execution identity.");
        var requested=resources.Distinct(StringComparer.Ordinal).ToArray();
        foreach(var id in requested)
        {
            var slot=slots[id];
            if(!slot.Actors.ContainsKey(owner.Actor)&&slot.Actors.Count>=slot.Capacity)return false;
        }
        if(!owners.TryGetValue(owner,out var held))owners.Add(owner,held=new(StringComparer.Ordinal));
        foreach(var id in requested)
        {
            var slot=slots[id];
            if(!slot.Actors.TryGetValue(owner.Actor,out var tickets))slot.Actors.Add(owner.Actor,tickets=[]);
            tickets.Add(owner.Execution);held.Add(id);
        }
        return true;
    }
    public bool CanAcquire(string actor,IEnumerable<string> resources)
    {
        foreach(var id in resources.Distinct(StringComparer.Ordinal))
        {
            if(!slots.TryGetValue(id,out var slot))throw new InvalidDataException("Unknown reservation slot: "+id);
            if(!slot.Actors.ContainsKey(actor)&&slot.Actors.Count>=slot.Capacity)return false;
        }
        return true;
    }
    public string[] Blocked(string actor,IEnumerable<string> resources)=>resources.Distinct(StringComparer.Ordinal)
        .Where(id=>!slots[id].Actors.ContainsKey(actor)&&slots[id].Actors.Count>=slots[id].Capacity).ToArray();

    public void Release(ReservationOwner owner)
    {
        if(!owners.Remove(owner,out var held))return;
        foreach(var id in held)
        {
            var occupants=slots[id].Actors;
            var tickets=occupants[owner.Actor];tickets.Remove(owner.Execution);
            if(tickets.Count==0)occupants.Remove(owner.Actor);
        }
    }

    public void Retain(ReservationOwner execution)
    {
        if(!owners.TryGetValue(execution,out var held))return;
        if(!TryAcquire(new(execution.Actor,0),held))throw new InvalidOperationException("An actor cannot lose its own occupied standing slot.");
        Release(execution);
    }

    public IReadOnlyCollection<string> Held(ReservationOwner owner)=>owners.TryGetValue(owner,out var held)?held.ToArray():[];
    public int Occupants(string id)=>slots[id].Actors.Count;
}
}

// --- Source: SceneComposerDecoupled.cs ---
namespace DuelView.Runtime
{


/// <summary>
/// Builds a new scene from a finite DV declaration. This is initialization only;
/// live state, evidence updates, scheduling and reloading belong to the session.
/// All identities, fields, rules, modes, initial cells and evidence come from DV.
/// </summary>
public sealed class SceneComposer(Func<string, JsonElement[], JsonElement>? instantiate = null)
{
        public const int MaximumDepth = 128;
    public const int MaximumEvaluationNodes = 1_000_000;
    public static Func<JsonElement, JsonElement>? NormalizeRecordsHook { get; set; }
    public static JsonElement NormalizeRecords(JsonElement value) => NormalizeRecordsHook != null ? NormalizeRecordsHook(value) : Normalize(value, 0);

    private static JsonElement Normalize(JsonElement value, int depth)
    {
        if (depth > MaximumDepth) throw new InvalidDataException("Record normalization depth exceeded.");
        if (value.ValueKind == JsonValueKind.Object)
            return JsonSerializer.SerializeToElement(value.EnumerateObject().ToDictionary(p => p.Name, p => Normalize(p.Value, depth + 1)));
        if (value.ValueKind != JsonValueKind.Array) return value;
        var items = value.EnumerateArray().ToArray();
        if (items.Length > 0 && items.All(p => p.ValueKind == JsonValueKind.Array && p.GetArrayLength() == 2 && p[0].ValueKind == JsonValueKind.String)
            && items.Select(p => p[0].GetString() ?? "").Distinct(StringComparer.Ordinal).Count() == items.Length)
            return JsonSerializer.SerializeToElement(items.ToDictionary(p => p[0].GetString() ?? "", p => Normalize(p[1], depth + 1)));
        return JsonSerializer.SerializeToElement(items.Select(p => Normalize(p, depth + 1)));
    }
    private static readonly JsonSerializerOptions Options = new() { MaxDepth = SceneComposer.MaximumDepth * 2 + 16 };

    public JsonElement Compose(string template, params JsonElement[] arguments) => Materialize(instantiate != null ? instantiate(template, arguments) : throw new InvalidOperationException("No template instantiator"));

    public static JsonElement Materialize(JsonElement declaration)
    {
        var nodes = SceneComposer.MaximumEvaluationNodes;
        return JsonSerializer.SerializeToElement(Visit(NormalizeRecords(declaration), 0, ref nodes), Options);
    }

    private static JsonNode? Visit(JsonElement value, int depth, ref int nodes)
    {
        if (depth > SceneComposer.MaximumDepth || --nodes < 0) throw new InvalidDataException("Scene composition budget exceeded.");
        if (value.ValueKind == JsonValueKind.Array)
        {
            var result = new JsonArray();
            foreach (var item in value.EnumerateArray()) result.Add(Visit(item, depth + 1, ref nodes));
            return result;
        }
        if (value.ValueKind != JsonValueKind.Object) return JsonNode.Parse(value.GetRawText());
        var record = new JsonObject();
        foreach (var property in value.EnumerateObject()) record.Add(property.Name, Visit(property.Value, depth + 1, ref nodes));
        return record["format"]?.GetValueKind() == JsonValueKind.String && record["format"]!.GetValue<string>() is "dv-world-composition" or "dv-world-extension"
            ? World(record) : record;
    }

    private static JsonObject World(JsonObject declaration)
    {
        var extends = Text(declaration, "format") == "dv-world-extension";
        var original = extends ? declaration["base"] as JsonObject ?? throw new InvalidDataException("World extension needs a base world.") : declaration;
        var id = Text(original, "id"); var domain = Text(original, "domain"); var subject = Text(original, "subject");
        if (id.Length == 0 || domain is not ("physical" or "mind") || (domain == "physical" ? subject.Length != 0 : subject.Length == 0))
            throw new InvalidDataException("Invalid world identity, domain or subject.");
        var objects = extends ? (JsonObject)(original["objects"]?.DeepClone() ?? throw new InvalidDataException("Base world needs objects.")) : new JsonObject();
        var addedObjects = 0;
        foreach (var item in extends && declaration["objects"] is null ? new JsonArray() : List(declaration, "objects"))
        {
            var obj = item as JsonObject ?? throw new InvalidDataException("World objects must be records.");
            var identity = Text(obj, "id");
            if (identity.Length == 0 || objects.ContainsKey(identity)) throw new InvalidDataException("Duplicate or empty object identity: " + identity);
            if (obj["facets"] is JsonArray empty && empty.Count == 0) obj["facets"] = new JsonObject();
            if (obj["facets"] is not JsonObject) throw new InvalidDataException("Object facets must be a record: " + identity);
            objects.Add(identity, obj.DeepClone());
            addedObjects++;
        }
        if (declaration["objects_if_missing"] is JsonArray optional)
            foreach (var item in optional)
            {
                var obj = item as JsonObject ?? throw new InvalidDataException("Optional objects must be records.");
                var identity = Text(obj, "id");
                if (identity.Length == 0) throw new InvalidDataException("Optional object needs an identity.");
                if (objects.ContainsKey(identity)) continue;
                if (obj["facets"] is not JsonObject) throw new InvalidDataException("Optional object facets must be a record.");
                objects.Add(identity, obj.DeepClone()); addedObjects++;
            }
        foreach (var item in List(declaration, "writes"))
        {
            var write = item as JsonObject ?? throw new InvalidDataException("Initial writes must be records.");
            var reference = write["ref"] as JsonObject ?? throw new InvalidDataException("Initial write needs a reference.");
            var scope = reference["subject"]?.GetValue<string>() ?? "";
            if (scope.Length > 0 && scope != subject) throw new InvalidDataException("Cross-subject initial write.");
            if (Text(reference, "type") != "ref") throw new InvalidDataException("Initial write needs a field reference.");
            var identity = Text(reference, "object"); var facet = Text(reference, "facet"); var field = Text(reference, "field");
            if (facet.Length == 0 || field.Length == 0 || objects[identity] is not JsonObject obj) throw new InvalidDataException("Initial write targets an unknown object or field.");
            var facets = (JsonObject)obj["facets"]!;
            if (facets[facet] is JsonArray empty && empty.Count == 0) facets[facet] = new JsonObject();
            if (facets[facet] is null) facets[facet] = new JsonObject();
            var fields = facets[facet] as JsonObject ?? throw new InvalidDataException("Initial facet must be a record.");
            var cell = write["cell"] as JsonObject ?? throw new InvalidDataException("Initial write needs a cell.");
            ValidateCell(cell);
            fields[field] = cell.DeepClone();
        }
        var adoptions = extends ? (JsonObject)(original["adoptions"]?.DeepClone() ?? throw new InvalidDataException("Base world needs adoptions.")) : new JsonObject();
        var addedAdoptions = 0;
        foreach (var item in List(declaration, "adoptions"))
        {
            var adoption = item as JsonObject ?? throw new InvalidDataException("Adoption must be a record.");
            var law = adoption["law"] as JsonObject ?? throw new InvalidDataException("Adoption needs a law.");
            var name = Text(law, "name"); var mode = Text(adoption, "mode");
            var confidence = adoption["confidence"]?.GetValue<double>() ?? throw new InvalidDataException("Adoption needs confidence.");
            if (!double.IsFinite(confidence) || confidence < 0 || confidence > 1 || domain == "physical" && confidence != 1)
                throw new InvalidDataException("Invalid adoption confidence: " + name);
            if (domain == "physical" ? mode != "enforce" : mode is not ("predict" or "infer" or "norm")) throw new InvalidDataException("Adoption mode does not match domain.");
            if (adoption["basis"] is not JsonArray basis || basis.Count == 0) throw new InvalidDataException("Adoption needs evidence: " + name);
            if (name.Length == 0 || adoptions.ContainsKey(name)) throw new InvalidDataException("Duplicate or empty law instance: " + name);
            var copy = (JsonObject)adoption.DeepClone(); copy["active"] = true;
            adoptions.Add(name, copy);
            addedAdoptions++;
        }
        var result = (JsonObject)original.DeepClone();
        result.Remove("format"); result.Remove("writes"); result.Remove("objects_if_missing");
        result["objects"] = objects; result["adoptions"] = adoptions;
        result["revision"] = (extends ? original["revision"]?.GetValue<long>() ?? 0 : 0) + addedObjects + addedAdoptions;
        if (!extends) { result["history"] = new JsonArray(); result["seen"] = new JsonArray(); }
        if (declaration["fields"] is JsonObject extra)
            foreach (var (key, value) in extra)
            {
                if (key is "id" or "domain" or "subject" or "objects" or "adoptions" or "revision" or "format" or "base" or "writes")
                    throw new InvalidDataException("World fields cannot replace composition-owned field: " + key);
                result[key] = value?.DeepClone();
            }
        return result;
    }

    private static void ValidateCell(JsonObject cell)
    {
        _ = cell["known"]?.GetValue<bool>() ?? throw new InvalidDataException("Cell needs known flag.");
        _ = cell["conflicted"]?.GetValue<bool>() ?? throw new InvalidDataException("Cell needs conflict flag.");
        var confidence = cell["confidence"]?.GetValue<double>() ?? throw new InvalidDataException("Cell needs confidence.");
        if (!double.IsFinite(confidence) || confidence < 0 || confidence > 1 || cell["value"] is null)
            throw new InvalidDataException("Invalid initial cell.");
    }
    private static string Text(JsonObject value, string key) => value[key]?.GetValue<string>() ?? throw new InvalidDataException("Missing Text field: " + key);
    private static JsonArray List(JsonObject value, string key) => value[key] as JsonArray ?? throw new InvalidDataException("Missing list field: " + key);
}
}

namespace DuelView.Runtime
{
    public static class SpatialRegistration
    {
        static SpatialRegistration()
        {
            DuelView.Core.DvValueTemplate.SceneComposerHook = SceneComposer.Materialize;
        }
        public static void Initialize() {}
    }
}
