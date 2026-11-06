// @mechanism: causal_clock_and_campaign
// @layer: core/simulation
// @contracts: CharacterSessionDef

using DuelView.Autonomy;
using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using DuelView.Spatial;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
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
using System.Text.RegularExpressions;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;
using static DuelView.Core.DVSimulationKernel;

// --- Source: CharacterSessionDefinition.cs ---
namespace DuelView.Runtime
{


/// <summary>A finite authored session uses the production Core and the shared DV parser.</summary>
public sealed class CharacterSessionDefinition
{
    public JsonElement Declaration { get; }
    public string Hash { get; }
    private readonly DvDocument policies;

    public CharacterSessionDefinition(string source,string policySource,Func<string,string>? readSource=null)
    {
        var document=DvDocument.Parse(source);
        if(document.Definition("data","runtime.character_session").GetProperty("value").Get("compact").Flag() && source.Count(c=>c=='\n')>2000)
            throw new InvalidDataException("Compact scenario source exceeds the 2000-line authoring limit.");
        Declaration=CampaignScenario.Expand(document.Definition("data","runtime.character_session").GetProperty("value"),
            readSource??(path=>File.ReadAllText(path.Replace("res://",""))));
        CampaignProgress.Validate(Declaration);
        ValidateReservations(Declaration);
        Hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Declaration.GetRawText()))).ToLowerInvariant();
        policies=DvDocument.Parse(policySource);
    }

    private static void ValidateReservations(JsonElement declaration)
    {
        var actorIds=declaration.Get("actors").Items().Select(actor=>actor.Get("id").Text()).ToHashSet(StringComparer.Ordinal);
        var actorActions=declaration.Get("actors").Items().ToDictionary(
            actor=>actor.Get("id").Text(),
            actor=>actor.Get("actions").Items().Select(action=>action.Get("id").Text()).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var slots=new HashSet<string>(StringComparer.Ordinal);
        foreach(var slot in declaration.Get("slots").Items())
        {
            var id=slot.Get("id").Text();
            var capacity=slot.Get("capacity");
            if(string.IsNullOrWhiteSpace(id)||capacity.ValueKind!=JsonValueKind.Number||!capacity.TryGetInt32(out var count)||count<1||!slots.Add(id))
                throw new InvalidDataException($"Invalid or duplicate reservation slot '{id}'; capacity must be a positive integer.");
        }
        foreach(var station in declaration.Get("yield_stations").Items())
            if(!slots.Add("yield.station."+station.Get("id").Text()))
                throw new InvalidDataException($"Duplicate yield reservation slot '{station.Get("id").Text()}'.");
        foreach(var actor in declaration.Get("actors").Items())
        foreach(var action in actor.Get("actions").Items())
        {
        foreach(var text in new[]{"completion_text","public_completion_text"})
            if(action.Get(text).ValueKind!=JsonValueKind.Undefined&&(action.Get(text).ValueKind!=JsonValueKind.String||action.Get(text).Text().Length>16000))
                throw new InvalidDataException("Action presentation text must be a bounded string: "+action.Get("id").Text());
        var completionLines=action.Get("completion_lines");
        if(completionLines.ValueKind!=JsonValueKind.Undefined&&(completionLines.ValueKind!=JsonValueKind.Array||completionLines.GetArrayLength()>32))
            throw new InvalidDataException("Action completion lines must be a bounded array: "+action.Get("id").Text());
        foreach(var line in completionLines.Items())
        {
            var viewpoint=line.Get("speaker");
            if(line.Get("text").ValueKind!=JsonValueKind.String||line.Get("text").Text().Length>16000||
               viewpoint.ValueKind!=JsonValueKind.Undefined&&(viewpoint.ValueKind!=JsonValueKind.String||!actorIds.Contains(viewpoint.Text())))
                throw new InvalidDataException("Invalid completion line or viewpoint: "+action.Get("id").Text());
            if(line.Get("when").ValueKind!=JsonValueKind.Undefined)_=Condition.Compile(line.Get("when"),actor.Get("id").Text());
        }
        var cooldown=action.Get("cooldown_minutes");
        if(cooldown.ValueKind!=JsonValueKind.Undefined&&(!double.IsFinite(cooldown.Number(double.NaN))||cooldown.Number()<0))
            throw new InvalidDataException("Action cooldown must be a finite nonnegative duration: "+action.Get("id").Text());
        foreach(var member in action.Get("motor").Get("observe_group").Items())
            if(!actorIds.Contains(member.Text()))throw new InvalidDataException("Unknown group observation actor: "+member.Text());
        var observedActions=action.Get("motor").Get("observed_group_actions").Items().ToArray();
        if(observedActions.Length>16 || observedActions.Select(item=>item.Get("actor").Text()).Distinct(StringComparer.Ordinal).Count()!=observedActions.Length)
            throw new InvalidDataException("Invalid observed group action budget: "+action.Get("id").Text());
        foreach(var observed in observedActions)
        {
            var observedActor=observed.Get("actor").Text();
            if(!action.Get("motor").Get("observe_group").Items().Any(member=>member.Text()==observedActor) ||
               !actorActions.TryGetValue(observedActor,out var candidates) || !candidates.Contains(observed.Get("action").Text()))
                throw new InvalidDataException("Invalid observed group action: "+action.Get("id").Text());
        }
        var handoff=action.Get("handoff");
        if(handoff.ValueKind==JsonValueKind.Object)
        {
            var recipient=handoff.Get("actor").Text();
            if(!actorIds.Contains(recipient) || action.Get("motor").Get("observe_actor").Text()!=recipient || handoff.Get("refs").Items().Count()>8 || handoff.Get("result_refs").Items().Count()>8)
                throw new InvalidDataException("Invalid physical handoff: "+action.Get("id").Text());
            foreach(var reference in handoff.Get("refs").Items().Concat(handoff.Get("result_refs").Items()))_=FieldKey.Read(reference,recipient);
        }
        var guards=action.Get("knowledge_guards").Items().ToArray();
        if(guards.Length>16)throw new InvalidDataException("Knowledge guard budget exceeded: "+action.Get("id").Text());
        foreach(var guard in guards)
        {
            var id=guard.Get("actor").Text();
            if(!actorIds.Contains(id) || guard.Get("mode").Text() is not ("known_true" or "not_known_true"))
                throw new InvalidDataException("Invalid knowledge guard: "+action.Get("id").Text());
            _=FieldKey.Read(guard.Get("ref"),id);
        }
        foreach(var resource in action.Get("resource_reservations").Items())
        {
            var id=resource.Text();
            if(!slots.Contains(id))
                throw new InvalidDataException($"Actor '{actor.Get("id").Text()}' action '{action.Get("id").Text()}' references undefined reservation slot '{id}'.");
        }
        }
    }

    private JsonElement Policy(string id)=>policies.Definition("data",id).GetProperty("value");

    public RuntimeSession Create(string? deductionSource=null)
    {
        var runtime=Policy("runtime.session");
        var timing=Policy(runtime.Get("action_timing_definition").Text());
        var policy=JsonNode.Parse(runtime.GetRawText())!.AsObject();
        policy["content_time_unit_seconds"]=timing.Get("content_time_unit_seconds").GetDouble();
        if(Declaration.Get("clock_ref").ValueKind==JsonValueKind.Object)
            policy["clock_ref"]=JsonNode.Parse(Declaration.Get("clock_ref").GetRawText());
        var scheduling=JsonNode.Parse(Policy(runtime.Get("scheduling_definition").Text()).GetRawText())!.AsObject();
        var stagger=Declaration.Get("scheduling").Get("actor_stagger_seconds");
        if(stagger.ValueKind==JsonValueKind.Number)scheduling["actor_stagger_seconds"]=stagger.GetDouble();
        foreach(var key in new[]{"decision_interval_seconds","candidates_per_decision","evaluations_per_slice","milliseconds_per_slice"})
            if(Declaration.Get("scheduling").Get(key).ValueKind==JsonValueKind.Number)
                scheduling[key]=JsonNode.Parse(Declaration.Get("scheduling").Get(key).GetRawText());
        var world=Declaration.Get("physical");
        var session=new RuntimeSession(world,JsonSerializer.SerializeToElement(policy),
            JsonSerializer.SerializeToElement(scheduling),Policy(runtime.Get("clock_definition").Text()),
            Policy(runtime.Get("residency_definition").Text()),world.Get("time").Number()*timing.Get("content_time_unit_seconds").Number());
        if(Declaration.Get("completion").Get("mode").Text()=="observation_window")
            session.ConfigureObservationHorizon(Declaration.Get("completion").Get("end_minutes").Number());
        session.ConfigureBindings(Declaration.Get("bindings"));
        session.ConfigureHostPhysicalBindings(Declaration.Get("host_physical_bindings"));
        session.ConfigureReservations(Declaration.Get("slots"));
        foreach(var process in Declaration.Get("world_processes").Items())
        {
            var id=process.Get("law").Text();
            var adoption=world.Get("adoptions").Get(id);
            var reference=Declaration.Get("clock_ref");
            if(adoption.ValueKind!=JsonValueKind.Object || reference.ValueKind!=JsonValueKind.Object)
                throw new InvalidDataException("World process requires an adopted law and clock: "+id);
            var automatic=JsonNode.Parse(adoption.GetRawText())!.AsObject();
            automatic["trigger"]=process.Get("once").Flag()?"once":"rising_edge";
            session.Rules.Register(id,JsonSerializer.SerializeToElement(automatic),FieldKey.Read(reference,"world"));
        }
        foreach(var station in Declaration.Get("yield_stations").Items())
            session.Reservations.Define("yield.station."+station.Get("id").Text(),1);
        foreach(var actor in Declaration.Get("actors").Items())
        {
            var actorNode=JsonNode.Parse(actor.GetRawText())!.AsObject();
            var actions=actorNode["actions"]!.AsArray();
            var actorId=actor.Get("id").Text();
            foreach(var station in Declaration.Get("yield_stations").Items())
            {
                var id=station.Get("id").Text();
                actions.Add(JsonSerializer.SerializeToNode(new {
                    id=actorId+".yield."+id,label="让开通路",kind="travel",manual_only=true,
                    precondition=new {op="constant",value=true},duration=1,host_execution=true,
                    timing=new {host_elapsed=true},minimum_travel_distance_m=0.4,
                    route_rooms=new[]{station.Get("room").Text()},resource_reservations=new[]{"yield.station."+id},
                    perceived_need_effects=new {},completed_need_effects=new {},
                    motor=new {target=station.Get("target"),posture="standing",animation="walk"}
                }));
            }
            var actorData=JsonSerializer.SerializeToElement(actorNode);
            session.RegisterActor(actorData,Policy(runtime.Get("attention").Text()),
                ActionCatalog.Compile(actorData,world,Declaration.Get("cost_model"),timing),actor.Get("autonomous").Flag());
            if(actor.Get("incapacitated_ref").ValueKind==JsonValueKind.Object)
                session.ConfigureIncapacitation(actorId,actor.Get("incapacitated_ref"));
        }
        session.ConfigurePresenceGroups(Declaration.Get("presence_groups"));
        session.ConfigureAccumulators(Declaration.Get("objective_accumulators"));
        session.ConfigureMechanics(Declaration);
        if(Declaration.Get("deduction").ValueKind==JsonValueKind.Object)
            session.ConfigureDeduction(deductionSource??throw new InvalidDataException("Deduction source is required."),Declaration.Get("deduction"));
        return session;
    }
}
}

// --- Source: CharacterContentRevision.cs ---
namespace DuelView.Runtime
{


public sealed record CharacterContentRevision(int Version,string EntryPath,string Fingerprint,string DefinitionHash,Dictionary<string,string> Sources);
public sealed record CharacterContentSnapshot(CharacterSessionDefinition Definition,CharacterContentRevision Revision);
public sealed record CharacterContentUpdate(string PreviousFingerprint,string Fingerprint,string[] AddedManualRoutes,string[] TightenedActions,string[] RelabelledActions,int AddedExpectations);

/// <summary>Content-addressed source contracts and a deliberately narrow save-compatible
/// update policy. This never creates facts, seeds knowledge, or edits a checkpoint.</summary>
public static class CharacterContentRevisions
{
    public const string PolicyPath="res://assets/data/98-runtime-session.data.dv";
    const int MaximumArchiveBytes=16*1024*1024;
    static readonly string[] MutableActionFields=["label","precondition","authority_precondition"];

    public static CharacterContentSnapshot Capture(string entryPath,Func<string,string> read)
    {
        var sources=new Dictionary<string,string>(StringComparer.Ordinal);
        string Read(string path)
        {
            if(!path.StartsWith("res://",StringComparison.Ordinal)||path.Split('/').Any(part=>part is ".." or "."))
                throw new InvalidDataException("A content revision requires project resource paths: "+path);
            if(sources.TryGetValue(path,out var found))return found;
            if(sources.Count>=128)throw new InvalidDataException("Content revision source budget exceeded.");
            var text=read(path);
            if(Encoding.UTF8.GetByteCount(text)>MaximumArchiveBytes)throw new InvalidDataException("Content revision source is too large.");
            sources.Add(path,text);return text;
        }
        var definition=new CharacterSessionDefinition(Read(entryPath),Read(PolicyPath),Read);
        foreach(var path in DependencyPaths(definition.Declaration))Read(path);
        // Preserve the rendered room declaration as provenance too. Older checkpoints
        // did not include presentation.source in their fingerprint; do not invent one.
        var presentation=definition.Declaration.Get("presentation").Get("source").Text();
        if(presentation.Length>0)Read(presentation);
        var fingerprint=Fingerprint(definition,Read);
        return new(definition,new(1,entryPath,fingerprint,definition.Hash,sources));
    }

    static SortedSet<string> DependencyPaths(JsonElement declaration)
    {
        var paths=new SortedSet<string>(StringComparer.Ordinal){PolicyPath};
        foreach(var source in declaration.Get("shared_sources").Items())paths.Add(source.Get("source").Text());
        if(declaration.Get("deduction").ValueKind==JsonValueKind.Object)paths.Add(declaration.Get("deduction").Get("source").Text());
        foreach(var group in new[]{"deductions","strategy_activities"})
            foreach(var item in declaration.Get(group).Items())paths.Add(item.Get("source").Text());
        var manifest=declaration.Get("presentation").Get("room_manifest").Text();
        if(manifest.Length>0)paths.Add(manifest);
        var planning=declaration.Get("planning").Get("source").Text();
        if(planning.Length>0)paths.Add(planning);
        return paths;
    }

    public static string Fingerprint(CharacterSessionDefinition definition,Func<string,string> read)
    {
        var text=new StringBuilder(definition.Hash);
        foreach(var path in DependencyPaths(definition.Declaration))text.Append('\0').Append(path).Append('\0').Append(read(path));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    public static bool ValidFingerprint(string? value)=>value is {Length:64}&&value.All(c=>char.IsAsciiHexDigit(c));

    public static CharacterContentSnapshot Verify(CharacterContentRevision revision)
    {
        if(revision.Version!=1||revision.Sources is null||!ValidFingerprint(revision.Fingerprint)||!ValidFingerprint(revision.DefinitionHash))
            throw new InvalidDataException("Invalid content revision contract.");
        var reconstructed=Capture(revision.EntryPath,path=>revision.Sources.TryGetValue(path,out var text)
            ?text:throw new InvalidDataException("Content revision is missing its source: "+path));
        if(reconstructed.Revision.Fingerprint!=revision.Fingerprint||reconstructed.Definition.Hash!=revision.DefinitionHash||
           reconstructed.Revision.Sources.Count!=revision.Sources.Count||
           reconstructed.Revision.Sources.Any(pair=>!revision.Sources.TryGetValue(pair.Key,out var text)||text!=pair.Value))
            throw new InvalidDataException("Content revision does not reproduce its recorded fingerprint.");
        return reconstructed;
    }

    public static byte[] Compress(CharacterContentRevision revision)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(revision);
        using var output=new MemoryStream();
        using(var gzip=new GZipStream(output,CompressionLevel.SmallestSize,true))gzip.Write(bytes);
        return output.ToArray();
    }

    public static CharacterContentSnapshot ReadCompressed(byte[] archive)
    {
        if(archive.Length>MaximumArchiveBytes)throw new InvalidDataException("Content revision archive is too large.");
        using var input=new MemoryStream(archive);
        using var gzip=new GZipStream(input,CompressionMode.Decompress);
        using var output=new MemoryStream();
        var buffer=new byte[8192];int count;
        while((count=gzip.Read(buffer))>0)
        {
            if(output.Length+count>MaximumArchiveBytes)throw new InvalidDataException("Expanded content revision exceeds its budget.");
            output.Write(buffer,0,count);
        }
        return Verify(JsonSerializer.Deserialize<CharacterContentRevision>(output.ToArray())
            ??throw new InvalidDataException("Empty content revision."));
    }

    public static CharacterContentUpdate Validate(CharacterContentSnapshot previous,CharacterContentSnapshot next,RuntimeSessionState state)
    {
        if(previous.Revision.EntryPath!=next.Revision.EntryPath)throw new InvalidDataException("Content revision entry point changed.");
        var dependencies=DependencyPaths(previous.Definition.Declaration);
        if(!dependencies.SetEquals(DependencyPaths(next.Definition.Declaration)))throw new InvalidDataException("Runtime policy or reasoning dependencies changed.");
        var presentation=previous.Definition.Declaration.Get("presentation").Get("source").Text();
        if(presentation.Length>0)dependencies.Add(presentation);
        foreach(var path in dependencies)
            if(!previous.Revision.Sources.TryGetValue(path,out var oldText)||!next.Revision.Sources.TryGetValue(path,out var newText)||oldText!=newText)
                throw new InvalidDataException("An existing runtime or presentation dependency changed: "+path);
        var changes=ValidateDeclarations(previous.Definition.Declaration,next.Definition.Declaration,state);
        return new(previous.Revision.Fingerprint,next.Revision.Fingerprint,changes.AddedManualRoutes,changes.TightenedActions,changes.RelabelledActions,changes.AddedExpectations);
    }

    // Public for both production authoring validation and the hosts. Authenticity of
    // archived sources is checked by Verify before this structural policy is used.
    public static CharacterContentUpdate ValidateDeclarations(JsonElement previous,JsonElement next,RuntimeSessionState state)
    {
        SameExcept(previous,next,["actors","physical","expectations"],"session declarations");
        SameExcept(previous.Get("physical"),next.Get("physical"),["adoptions"],"world objects, initial facts and clock");
        var oldActors=previous.Get("actors").Items().ToArray();var newActors=next.Get("actors").Items().ToArray();
        if(oldActors.Length!=newActors.Length)throw new InvalidDataException("Actor identities changed.");
        var oldLaws=previous.Get("physical").Get("adoptions");var newLaws=next.Get("physical").Get("adoptions");
        var oldActions=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
        var newActions=new Dictionary<string,(string Actor,JsonElement Action)>(StringComparer.Ordinal);
        var added=new List<string>();var tightened=new HashSet<string>(StringComparer.Ordinal);var relabelled=new List<string>();
        var routes=new HashSet<(string From,string To)>();
        foreach(var actor in oldActors)
        foreach(var action in actor.Get("actions").Items())
        {
            oldActions.Add(action.Get("id").Text(),action);
            var route=action.Get("route_rooms").Items().Select(item=>item.Text()).ToArray();
            for(var n=1;n<route.Length;n++)routes.Add((route[n-1],route[n]));
        }
        for(var n=0;n<oldActors.Length;n++)
        {
            var oldActor=oldActors[n];var newActor=newActors[n];
            SameExcept(oldActor,newActor,["actions"],"actor policy, cognition, perception or identity");
            var id=oldActor.Get("id").Text();
            var before=oldActor.Get("actions").Items().ToArray();var after=newActor.Get("actions").Items().ToArray();
            var retained=after.Where(a=>oldActions.ContainsKey(a.Get("id").Text())).ToArray();
            if(!before.Select(a=>a.Get("id").Text()).SequenceEqual(retained.Select(a=>a.Get("id").Text())))
                throw new InvalidDataException("Existing actor actions were removed, reassigned or reordered: "+id);
            foreach(var action in after)
            {
                var actionId=action.Get("id").Text();newActions.Add(actionId,(id,action));
                if(!oldActions.TryGetValue(actionId,out var oldAction))
                {
                    ValidateManualRoute(id,action,newLaws.Get(action.Get("law").Text()),routes);
                    added.Add(actionId);continue;
                }
                SameExcept(oldAction,action,MutableActionFields,"existing action effects, duration, resources or mechanisms: "+actionId);
                if(!Values.Same(oldAction.Get("label"),action.Get("label")))relabelled.Add(actionId);
                foreach(var gate in new[]{"precondition","authority_precondition"})
                    if(!Values.Same(oldAction.Get(gate),action.Get(gate)))
                    {RequireTightening(oldAction.Get(gate),action.Get(gate),actionId);tightened.Add(actionId);}
            }
        }
        var oldLawIds=oldLaws.Fields().Select(p=>p.Name).ToHashSet(StringComparer.Ordinal);
        var expectedNewLaws=added.Select(id=>newActions[id].Action.Get("law").Text()).ToHashSet(StringComparer.Ordinal);
        var actualNewLaws=newLaws.Fields().Select(p=>p.Name).Where(id=>!oldLawIds.Contains(id)).ToHashSet(StringComparer.Ordinal);
        if(!expectedNewLaws.SetEquals(actualNewLaws))throw new InvalidDataException("Only a new manual route may introduce an action law.");
        foreach(var law in oldLaws.Fields())
        {
            var replacement=newLaws.Get(law.Name);
            if(!oldActions.TryGetValue(law.Name,out var action))
            {RequireSame(law.Value,replacement,"existing world rule: "+law.Name);continue;}
            SameExcept(law.Value,replacement,["law"],"existing law adoption: "+law.Name);
            SameExcept(law.Value.Get("law"),replacement.Get("law"),["premise"],"existing law effects or postcondition: "+law.Name);
            if(!Values.Same(law.Value.Get("law").Get("premise"),replacement.Get("law").Get("premise")))
            {RequireTightening(law.Value.Get("law").Get("premise"),replacement.Get("law").Get("premise"),action.Get("id").Text());tightened.Add(action.Get("id").Text());}
        }
        foreach(var slot in state.Scheduler.Slots)
            if(slot.Running is { } running&&tightened.Contains(running.Action)||slot.Best is { } best&&tightened.Contains(best)||slot.Selection.Any(tightened.Contains))
                throw new InvalidDataException("Finish the affected decision before updating its admission conditions: "+slot.Actor);
        var oldChecks=previous.Get("expectations").Items().ToArray();var newChecks=next.Get("expectations").Items().ToArray();
        // An objective check and an actor's knowledge check can intentionally share
        // an authored id. Preserve both scopes and their full declarations.
        static (string Id,string Actor) CheckId(JsonElement check)=>(check.Get("id").Text(),check.Get("actor").Text());
        var oldCheckIds=oldChecks.Select(CheckId).ToHashSet();
        var newCheckIds=newChecks.Select(CheckId).ToArray();
        var retainedChecks=newChecks.Where(check=>oldCheckIds.Contains(CheckId(check))).ToArray();
        if(oldCheckIds.Count!=oldChecks.Length||newCheckIds.Distinct().Count()!=newChecks.Length||
           oldCheckIds.Any(id=>id.Id.Length==0)||newCheckIds.Any(id=>id.Id.Length==0)||retainedChecks.Length!=oldChecks.Length||
           oldChecks.Where((check,index)=>!Values.Same(check,retainedChecks[index])).Any())
            throw new InvalidDataException("Existing acceptance conditions were changed or removed.");
        return new("","",added.ToArray(),tightened.ToArray(),relabelled.ToArray(),newChecks.Length-oldChecks.Length);
    }

    static void ValidateManualRoute(string actor,JsonElement action,JsonElement adoption,HashSet<(string From,string To)> routes)
    {
        string[] allowed=["id","label","kind","focus","manual_only","priority","precondition","authority_precondition","law","grants","duration_seconds","resource_reservations","handoffs","route_rooms","campaign_day","not_before_game_minutes","not_after_game_minutes"];
        var id=action.Get("id").Text();var route=action.Get("route_rooms").Items().Select(r=>r.Text()).ToArray();
        if(!id.StartsWith(actor+".",StringComparison.Ordinal)||action.Get("kind").Text()!="walk"||!action.Get("manual_only").Flag()||
           action.Fields().Any(p=>!allowed.Contains(p.Name,StringComparer.Ordinal))||action.Get("handoffs").Items().Any()||route.Length<2||route.Any(string.IsNullOrWhiteSpace)||
           action.Get("law").Text()!=id||action.Get("duration_seconds").Number()<=0||
           action.Get("resource_reservations").Items().Any(r=>r.Text()!="actor."+actor))
            throw new InvalidDataException("Added action is not a bounded, repeatable manual route: "+id);
        for(var n=1;n<route.Length;n++)
            if(!routes.Contains((route[n-1],route[n]))&&!routes.Contains((route[n],route[n-1])))
                throw new InvalidDataException("Added route invents a connection absent from existing routes: "+id);
        var room=new FieldKey(actor,"state","room");
        bool IsRoom(JsonElement reference)=>FieldKey.Read(reference,actor)==room;
        var effects=adoption.Get("law").Get("effects").Items().ToArray();
        if(adoption.Get("mode").Text()!="enforce"||adoption.Get("active").Flag(true)!=true||
           adoption.Get("law").Get("postcondition").ValueKind!=JsonValueKind.Undefined||effects.Length!=1||effects[0].Get("op").Text()!="assign"||
           !IsRoom(effects[0].Get("ref"))||effects[0].Get("value").Text()!=route[^1]||
           action.Get("grants").Items().Count()!=1||!IsRoom(action.Get("grants").Items().Single()))
            throw new InvalidDataException("Added route may only move and observe its own actor: "+id);
        bool StartsHere(JsonElement gate)=>Terms(gate).Any(term=>term.Get("op").Text()=="equals"&&
            term.Get("ref").Get("object").Text()==actor&&term.Get("ref").Get("facet").Text()=="state"&&term.Get("ref").Get("field").Text()=="room"&&term.Get("value").Text()==route[0]);
        if(!StartsHere(action.Get("precondition"))||!StartsHere(adoption.Get("law").Get("premise")))
            throw new InvalidDataException("Added route requires its actual origin in both knowledge and world: "+id);
    }

    static IEnumerable<JsonElement> Terms(JsonElement condition)
    {
        if(condition.ValueKind==JsonValueKind.Undefined||condition.Get("op").Text()=="constant"&&condition.Get("value").Flag())yield break;
        if(condition.Get("op").Text()=="all")
        {foreach(var child in condition.Get("items").Items())foreach(var term in Terms(child))yield return term;}
        else yield return condition;
    }
    static void RequireTightening(JsonElement previous,JsonElement next,string action)
    {
        var terms=Terms(next).ToArray();
        if(next.ValueKind!=JsonValueKind.Object||Terms(previous).Any(term=>!terms.Any(candidate=>Values.Same(term,candidate))))
            throw new InvalidDataException("An existing action admission condition was weakened: "+action);
    }
    static void SameExcept(JsonElement previous,JsonElement next,string[] except,string label)
    {
        if(previous.ValueKind!=next.ValueKind)throw new InvalidDataException("Incompatible "+label+".");
        var a=previous.Fields().Where(p=>!except.Contains(p.Name,StringComparer.Ordinal)).ToDictionary(p=>p.Name,p=>p.Value,StringComparer.Ordinal);
        var b=next.Fields().Where(p=>!except.Contains(p.Name,StringComparer.Ordinal)).ToDictionary(p=>p.Name,p=>p.Value,StringComparer.Ordinal);
        if(a.Count!=b.Count||a.Any(p=>!b.TryGetValue(p.Key,out var v)||!Values.Same(p.Value,v)))throw new InvalidDataException("Incompatible "+label+".");
    }
    static void RequireSame(JsonElement previous,JsonElement next,string label)
    {if(!Values.Same(previous,next))throw new InvalidDataException("Incompatible "+label+".");}

    public static string AssertStatePreserved(RuntimeSessionState previous,RuntimeSessionState restored)
    {
        var before=JsonSerializer.SerializeToElement(previous);var after=JsonSerializer.SerializeToElement(restored);
        RequireSame(before,after,"checkpoint state: content updates may not change world, minds, time, pending work or history");
        return CanonicalHash(before);
    }
    public static string CanonicalHash(JsonElement value)
    {
        using var stream=new MemoryStream();
        using(var writer=new Utf8JsonWriter(stream))WriteCanonical(writer,value);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
    static void WriteCanonical(Utf8JsonWriter writer,JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object)
        {
            writer.WriteStartObject();foreach(var property in value.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal))
            {writer.WritePropertyName(property.Name);WriteCanonical(writer,property.Value);}writer.WriteEndObject();
        }
        else if(value.ValueKind==JsonValueKind.Array)
        {writer.WriteStartArray();foreach(var item in value.EnumerateArray())WriteCanonical(writer,item);writer.WriteEndArray();}
        else value.WriteTo(writer);
    }
}
}
