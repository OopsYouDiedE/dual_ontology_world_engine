using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Story;

/// <summary>Closed host-value protocol: no arbitrary CLR type names or object activation.</summary>
public static class SessionPersistence
{
    public static JsonSerializerOptions Json { get; } = new()
    {
        PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
        MaxDepth=256
    };
    public static T Value<T>(ContractResult<T> result) => result switch
    { ContractResult<T>.Success s=>s.Value, ContractResult<T>.Failure f=>throw new CognitionContractException(f.Error), _=>throw new InvalidOperationException() };
    public static void Require([DoesNotReturnIf(false)] bool condition, string path, ContractErrorCode code=ContractErrorCode.MalformedData)
    { if(!condition) throw new CognitionContractException(new(code,path,"会话存档校验失败。",CandidateIds:[])); }
    public static ContractResult<T> Try<T>(Func<T> action)
    {
        try { return new ContractResult<T>.Success(action()); }
        catch(CognitionContractException e) { return new ContractResult<T>.Failure(e.Error); }
        catch(Exception e) when(e is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException or KeyNotFoundException or NullReferenceException)
        { return new ContractResult<T>.Failure(new(e is JsonException j&&j.Message.Contains("could not be mapped",StringComparison.Ordinal) ? ContractErrorCode.Unsupported : ContractErrorCode.MalformedData,"session","无效的会话存档。",CandidateIds:[])); }
    }
    public static JsonNode Pack(object? value) => value switch
    {
        null=>new JsonObject { ["kind"]="null" },
        string s=>new JsonObject { ["kind"]="string",["value"]=s },
        bool b=>new JsonObject { ["kind"]="bool",["value"]=b },
        int n=>new JsonObject { ["kind"]="int",["value"]=n },
        long n=>new JsonObject { ["kind"]="long",["value"]=n },
        double n when double.IsFinite(n)=>new JsonObject { ["kind"]="double",["value"]=n },
        IDictionary<string,object> d=>new JsonObject { ["kind"]="map",["value"]=new JsonObject(d.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new KeyValuePair<string,JsonNode?>(p.Key,Pack(p.Value)))) },
        object[] a=>new JsonObject { ["kind"]="array",["value"]=new JsonArray(a.Select(Pack).ToArray()) },
        _=>throw new CognitionContractException(new(ContractErrorCode.Unsupported,"progress.value","未支持的宿主进度值类型。",CandidateIds:[]))
    };
    public static object? Unpack(JsonElement e)
    {
        var kind=e.GetProperty("kind").GetString();
        Require(e.EnumerateObject().All(p=>p.Name=="kind" || kind!="null"&&p.Name=="value"),"progress.property",ContractErrorCode.Unsupported);
        if(kind=="null") return null;
        var v=e.GetProperty("value");
        return kind switch
        {
            "string"=>v.GetString()!, "bool"=>v.GetBoolean(), "int"=>v.GetInt32(), "long"=>v.GetInt64(),
            "double" when double.IsFinite(v.GetDouble())=>v.GetDouble(),
            "array"=>v.EnumerateArray().Select(Unpack).ToArray(),
            "map"=>v.EnumerateObject().ToDictionary(p=>p.Name,p=>Unpack(p.Value)!,StringComparer.Ordinal),
            _=>throw new CognitionContractException(new(ContractErrorCode.Unsupported,"progress.kind","未知宿主进度值。",CandidateIds:[]))
        };
    }
}

public sealed record ChapterProgressSaveV1([property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,string> CharacterRooms,
    [property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,string> ObjectRooms, [property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,JsonElement> Progress,
    [property:System.Text.Json.Serialization.JsonRequired] Dictionary<string,string[]> Observations);

public sealed partial class ChapterSession
{
    public ChapterProgressSaveV1 CaptureProgress() => new(new(CharacterRooms),new(ObjectRooms),
        _progress.ToDictionary(p=>p.Key,p=>JsonSerializer.SerializeToElement(SessionPersistence.Pack(p.Value))),
        _observations.ToDictionary(p=>p.Key,p=>p.Value.Order(StringComparer.Ordinal).ToArray()));
    public static ChapterSession RestoreProgress(ChapterProgressSaveV1 saved)
    {
        SessionPersistence.Require(saved is not null && saved.CharacterRooms is not null && saved.ObjectRooms is not null && saved.Progress is not null && saved.Observations is not null,"progress");
        var s=new ChapterSession();
        foreach(var p in saved!.CharacterRooms) s.CharacterRooms.Add(p.Key,p.Value);
        foreach(var p in saved.ObjectRooms) s.ObjectRooms.Add(p.Key,p.Value);
        foreach(var p in saved.Progress) s._progress.Add(p.Key,(Dictionary<string,object>)SessionPersistence.Unpack(p.Value)!);
        foreach(var p in saved.Observations)
        { SessionPersistence.Require(p.Value is not null && p.Value.Distinct(StringComparer.Ordinal).Count()==p.Value.Length,"observations"); s._observations.Add(p.Key,p.Value!.ToHashSet(StringComparer.Ordinal)); }
        return s;
    }
}

/// <summary>Publishes a completely validated candidate. Retrying identical bytes returns the current objects.</summary>
public sealed class WorldSessionStore(WorldSession? initial=null)
{
    private readonly object _gate=new();
    private byte[]? _loaded;
    public WorldSession? Current { get; private set; }=initial;
    public Guid RestoreEpoch { get; private set; }=Guid.NewGuid();
    public ContractResult<WorldSession> Restore(byte[] bytes,CodecLimitsV1? limits=null)
    {
        lock(_gate)
        {
            if(_loaded is not null && bytes.AsSpan().SequenceEqual(_loaded)) return new ContractResult<WorldSession>.Success(Current!);
            var result=WorldSession.Restore(bytes,limits);
            if(result is ContractResult<WorldSession>.Success s) { Current=s.Value; _loaded=bytes.ToArray(); RestoreEpoch=Guid.NewGuid(); }
            return result;
        }
    }
}
