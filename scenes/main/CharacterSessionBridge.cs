using Godot;
using DuelView.Runtime;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using GDict=Godot.Collections.Dictionary;

/// <summary>Main's native character port. Physical callbacks retain execution identities.</summary>
public partial class CharacterSessionBridge : Node
{
    private RuntimeSession session=null!;
    private CharacterSessionDefinition definition=null!;
    private readonly System.Collections.Generic.Dictionary<long,Decision> executions=[];
    private readonly System.Collections.Generic.List<object> timeline=[];
    private readonly System.Collections.Generic.Dictionary<string,(long revision,int pending)> cognitiveVersions=[];
    private readonly System.Collections.Generic.Dictionary<string,int> evidenceCursors=new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string,long> auditCounts=new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string,string> interruptTargets=new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.HashSet<string> strategyActions=new(StringComparer.Ordinal);
    private string player="";
    private string contentPath="";
    private long generation=1;
    private CharacterContentSnapshot contentSnapshot=null!;
    private string revisionArchiveHash="";
    private object? contentUpdate;
    private (RuntimeSession Session,CharacterContentSnapshot Content,RuntimeSessionState State,object? Update)? prepared;
    private long Handle(long execution)
    {
        if(execution<1 || execution>uint.MaxValue || generation<1 || generation>int.MaxValue)throw new InvalidDataException("Execution generation exhausted.");
        return (generation<<32)|execution;
    }
    public long RemapExecution(long oldHandle)
    {
        var result=Handle(oldHandle&uint.MaxValue);
        return executions.ContainsKey(result)?result:0;
    }
    public void ReportDoor(string id,double angle,double target,bool moving,double remaining)=>session.ReportDoor(id,new(angle,target,moving,remaining));
    public GDict LogicalDoors()=>Dict(session.LogicalDoors);
    public GDict LogicalPlacement(string actor)
    {
        var value=session.Placement(actor);return value is null?new GDict():Dict(value);
    }
    public Godot.Collections.Array<GDict> RestoredCommands()
    {
        var result=new Godot.Collections.Array<GDict>();
        foreach(var item in executions)
        {
            var action=item.Value;var activity=session.Activity(action.Actor);
            result.Add(Dict(new {kind="execute",actor=action.Actor,execution=item.Key,action=action.Action.Id,duration=action.Action.Duration,
                declaration=action.Action.Declaration,work_started=activity?.WorkStartedAt is not null}));
        }
        return result;
    }
    public string[] RetainedResources(string actor)=>session.Reservations.Held(new(actor,0)).ToArray();
    public GDict ConversationSpec()=>session.ConversationDeclaration.ValueKind==JsonValueKind.Object?Json.ParseString(session.ConversationDeclaration.GetRawText()).AsGodotDictionary():new GDict();
    private StreamWriter? log;
    private static readonly JsonSerializerOptions AuditJson=new(){Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    private string auditPath="";
    private string auditError="";
    private long auditSequence;
    private bool auditClosed;
    private static GDict Dict(object value)=>Json.ParseString(JsonSerializer.Serialize(value)).AsGodotDictionary();
    public GDict Open(string path)
    {
        contentPath=path;
        contentSnapshot=ReadContentSnapshot();definition=contentSnapshot.Definition;
        foreach(var actor in definition.Declaration.GetProperty("actors").EnumerateArray())
        foreach(var action in actor.GetProperty("actions").EnumerateArray())
            if(action.TryGetProperty("interrupt_actor",out var target))interruptTargets.Add(action.GetProperty("id").GetString()!,target.GetString()!);
        session=CreateSession(definition);
        if(definition.Declaration.TryGetProperty("strategy_activities",out var strategies))
            foreach(var item in strategies.EnumerateArray())
                strategyActions.Add(item.GetProperty("action").GetString()!);
        player=definition.Declaration.GetProperty("viewer").GetString()!;
        if(!definition.Declaration.TryGetProperty("presentation",out _)) session.SetAutonomy(player,false);
        var root=definition.Declaration.TryGetProperty("presentation",out var presentation) && presentation.TryGetProperty("receipt_directory",out var receiptDirectory) ? receiptDirectory.GetString()! : "user://character-receipts";
        var directory=ProjectSettings.GlobalizePath(root+"/live-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        auditPath=Path.Combine(directory,"actions.jsonl");
        log=new(new FileStream(auditPath,FileMode.CreateNew,System.IO.FileAccess.Write,FileShare.Read),new UTF8Encoding(false));
        Record("session_open",new {player,content_hash=definition.Hash,audit_schema=2,progress=DuelView.Runtime.CampaignProgress.Evaluate(definition.Declaration,session)});
        return Json.ParseString(definition.Declaration.GetRawText()).AsGodotDictionary();
    }
    private static RuntimeSession CreateSession(CharacterSessionDefinition content)
    {
        var deduction=content.Declaration.TryGetProperty("deduction",out var caseDeclaration)
            ? Godot.FileAccess.GetFileAsString(caseDeclaration.GetProperty("source").GetString()!) : null;
        var candidate=content.Create(deduction);
        if(content.Declaration.TryGetProperty("deductions",out var moreDeductions))
            foreach(var item in moreDeductions.EnumerateArray())candidate.ConfigureDeduction(Godot.FileAccess.GetFileAsString(item.GetProperty("source").GetString()!),item);
        if(content.Declaration.TryGetProperty("strategy_activities",out var strategies))
            foreach(var item in strategies.EnumerateArray())candidate.ConfigureStrategy(Godot.FileAccess.GetFileAsString(item.GetProperty("source").GetString()!),item);
        return candidate;
    }
    public GDict CaptureCheckpoint()
    {
        try
        {
            var archive=EnsureRevisionArchive();
            var state=session.SaveState();
            return new GDict { {"json",JsonSerializer.Serialize(new {version=2,content_path=contentPath,content_hash=contentSnapshot.Revision.Fingerprint,
                content_revision=new {format="dv-sources-gzip-v1",archive_sha256=archive},state})} };
        }
        catch(Exception error){return new GDict {{"error",error.Message}};}
    }
    public string WriteSavedGame(string payload)
    {
        try
        {
            var path=SavedGamePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary=path+".tmp";File.WriteAllText(temporary,payload,Encoding.UTF8);File.Move(temporary,path,true);return "";
        }
        catch(Exception error){return error.Message;}
    }
    public string ReadSavedGame()
    {
        var path=SavedGamePath();
        return File.Exists(path)?File.ReadAllText(path,Encoding.UTF8):"";
    }
    private static string SavedGamePath()
    {
        var slot=OS.GetCmdlineUserArgs().FirstOrDefault(arg=>arg.StartsWith("--dv-save-slot=",StringComparison.Ordinal))?[15..];
        var resource=string.IsNullOrEmpty(slot)?"user://character-session.dvsave":"user://sessions/"+slot+"/character-session.dvsave";
        return ProjectSettings.GlobalizePath(resource);
    }
    private CharacterContentSnapshot ReadContentSnapshot()=>CharacterContentRevisions.Capture(ProjectSettings.LocalizePath(contentPath),path=>
    {
        if(!Godot.FileAccess.FileExists(path))throw new InvalidDataException("Missing checkpoint dependency: "+path);
        return Godot.FileAccess.GetFileAsString(path);
    });
    private string EnsureRevisionArchive()
    {
        if(revisionArchiveHash.Length>0)return revisionArchiveHash;
        var bytes=CharacterContentRevisions.Compress(contentSnapshot.Revision);
        var archiveHash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        // The legacy fingerprint omits presentation.source. Address v2 archives by
        // their full bytes too, so a later visual revision cannot reuse older sources.
        var path=ProjectSettings.GlobalizePath("user://content-revisions/"+contentSnapshot.Revision.Fingerprint+"-"+archiveHash+".json.gz");
        if(File.Exists(path))
        {
            bytes=File.ReadAllBytes(path);
            var prior=CharacterContentRevisions.ReadCompressed(bytes);
            if(prior.Revision.Fingerprint!=contentSnapshot.Revision.Fingerprint||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=archiveHash)
                throw new InvalidDataException("Cached content revision has a different contract.");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllBytes(temporary,bytes);
                try{File.Move(temporary,path,false);}
                catch(IOException) when(File.Exists(path))
                {
                    bytes=File.ReadAllBytes(path);
                    if(CharacterContentRevisions.ReadCompressed(bytes).Revision.Fingerprint!=contentSnapshot.Revision.Fingerprint||
                       Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=archiveHash)
                        throw new InvalidDataException("Conflicting cached content revision.");
                }
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        revisionArchiveHash=archiveHash;return revisionArchiveHash;
    }
    private static CharacterContentSnapshot ReadSavedRevision(JsonElement checkpoint,int version,string fingerprint)
    {
        if(!CharacterContentRevisions.ValidFingerprint(fingerprint))throw new InvalidDataException("Invalid checkpoint content fingerprint.");
        string path;string? archiveHash=null;
        if(version==2)
        {
            var contract=checkpoint.GetProperty("content_revision");
            archiveHash=contract.GetProperty("archive_sha256").GetString();
            if(contract.GetProperty("format").GetString()!="dv-sources-gzip-v1"||!CharacterContentRevisions.ValidFingerprint(archiveHash))
                throw new InvalidDataException("Invalid checkpoint content archive contract.");
            var name=fingerprint+"-"+archiveHash+".json.gz";
            var cached="user://content-revisions/"+name;
            path=Godot.FileAccess.FileExists(cached)?cached:"res://assets/data/content-revisions/"+name;
        }
        else path="res://assets/data/content-revisions/"+fingerprint+".json.gz";
        if(!Godot.FileAccess.FileExists(path))throw new InvalidDataException("Content changed and its verified previous revision is unavailable. A new game is required.");
        var bytes=Godot.FileAccess.GetFileAsBytes(path);
        if(archiveHash is not null&&archiveHash!=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())
            throw new InvalidDataException("Checkpoint content archive differs from its saved contract.");
        var revision=CharacterContentRevisions.ReadCompressed(bytes);
        if(revision.Revision.Fingerprint!=fingerprint)throw new InvalidDataException("Previous sources do not reproduce the checkpoint fingerprint.");
        return revision;
    }
    public string PrepareCheckpoint(string json,bool allowContentChange=false)
    {
        try
        {
            prepared=null;
            using var document=JsonDocument.Parse(json);var root=document.RootElement;
            var version=root.GetProperty("version").GetInt32();
            if(version is not (1 or 2) || root.GetProperty("content_path").GetString()!=contentPath)
                throw new InvalidDataException("Checkpoint format or content path differs.");
            var content=ReadContentSnapshot();
            var state=root.GetProperty("state").Deserialize<RuntimeSessionState>() ?? throw new InvalidDataException("Empty checkpoint state.");
            var previousHash=root.GetProperty("content_hash").GetString()??"";
            CharacterContentUpdate? update=null;
            // Retain the legacy method signature, but never use its flag to bypass
            // validation. Normal load may apply only an authenticated compatible update.
            _=allowContentChange;
            if(previousHash!=content.Revision.Fingerprint)
                update=CharacterContentRevisions.Validate(ReadSavedRevision(root,version,previousHash),content,state);
            var candidate=CreateSession(content.Definition);candidate.RestoreState(state);
            object? evidence=null;
            if(update is not null)
            {
                var stateHash=CharacterContentRevisions.AssertStatePreserved(state,candidate.SaveState());
                evidence=new {policy="manual-routes-and-stricter-admission-v1",update.PreviousFingerprint,update.Fingerprint,update.AddedManualRoutes,
                    update.TightenedActions,update.RelabelledActions,update.AddedExpectations,state_preserved=true,state_hash=stateHash};
            }
            foreach(var execution in state.Executions.Where(item=>item.Dispatched))
                if(candidate.DecisionFor(execution.Execution) is null)throw new InvalidDataException("Missing restored execution.");
            prepared=(candidate,content,state,evidence);
            return "";
        }
        catch(Exception error){return error.Message;}
    }
    public bool CommitPreparedCheckpoint()
    {
        if(prepared is not { } next || generation>=int.MaxValue)return false;
        generation++;session=next.Session;contentSnapshot=next.Content;definition=next.Content.Definition;contentUpdate=next.Update;revisionArchiveHash="";prepared=null;
        if(!definition.Declaration.TryGetProperty("presentation",out _)) session.SetAutonomy(player,false);
        executions.Clear();cognitiveVersions.Clear();evidenceCursors.Clear();interruptTargets.Clear();strategyActions.Clear();
        foreach(var execution in next.State.Executions.Where(item=>item.Dispatched))executions.Add(Handle(execution.Execution),session.DecisionFor(execution.Execution)!);
        foreach(var actor in definition.Declaration.GetProperty("actors").EnumerateArray())
        foreach(var action in actor.GetProperty("actions").EnumerateArray())
            if(action.TryGetProperty("interrupt_actor",out var target))interruptTargets.Add(action.GetProperty("id").GetString()!,target.GetString()!);
        if(definition.Declaration.TryGetProperty("strategy_activities",out var strategies))
            foreach(var item in strategies.EnumerateArray())strategyActions.Add(item.GetProperty("action").GetString()!);
        Record("checkpoint_restored",new {content_hash=contentSnapshot.Revision.Fingerprint,generation,game_seconds=session.Clock.Now,content_update=contentUpdate});return true;
    }
    private void Record(string kind,object data)
    {
        var row=new {seq=auditSequence+1,real_us=Time.GetTicksUsec(),game_seconds=session.Clock.Now,kind,data};
        // The renderer may observe the whole world, but its player snapshot is
        // a separate projection. Private NPC cognition stays in the audit log.
        var payload=JsonSerializer.SerializeToElement(data);
        var actor=payload.TryGetProperty("actor",out var lower)?lower:
            payload.TryGetProperty("Actor",out var upper)?upper:default;
        var own=actor.ValueKind==JsonValueKind.String && actor.GetString()==player;
        var publicToPlayer=kind is "session_open" or "checkpoint_restored" or "player_action" or "player_deduction" ||
            kind.StartsWith("conversation_",StringComparison.Ordinal) || own;
        if(publicToPlayer && kind is not ("actor_cognition" or "passive_perception" or "prop_placement" or "actor_motion" or "frame" or "yield_candidates" or "decision_audit" or "action_effects" or "evidence_received"))
        {
            timeline.Add(row);
            if(timeline.Count>64)timeline.RemoveAt(0);
        }
        if(log is null||auditError.Length>0)return;
        try
        {
            log.WriteLine(JsonSerializer.Serialize(row,AuditJson));log.Flush();
            auditSequence++;auditCounts[kind]=auditCounts.GetValueOrDefault(kind)+1;
            if(kind=="action_receipt")
            {
                var category=payload.GetProperty("Committed").ValueKind==JsonValueKind.True?"successful_receipt":
                    payload.TryGetProperty("Interruption",out var interruption)&&interruption.ValueKind==JsonValueKind.Object?"recoverable_cancellation":
                    payload.GetProperty("Reason").GetString() is "deduction_unproved" or "suggestion_rejected" or "incantation_precondition_failed"?"rejected_receipt":
                    payload.GetProperty("Reason").GetString() is "objective_condition_failed" or "standing_resource_busy"?"blocked_attempt":"unexpected_failure";
                auditCounts[category]=auditCounts.GetValueOrDefault(category)+1;
            }
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException)
        {auditError=error.Message;GD.PushError("Causal audit persistence failed: "+error.Message);}
    }
    public GDict CampaignProgress()=>Dict(DuelView.Runtime.CampaignProgress.Evaluate(definition.Declaration,session));
    private object AuditProjection()=>new {path=auditPath,last_sequence=auditSequence,
        dispatch_count=auditCounts.GetValueOrDefault("action_dispatched"),receipt_count=auditCounts.GetValueOrDefault("action_receipt"),
        effect_count=auditCounts.GetValueOrDefault("action_effects"),evidence_count=auditCounts.GetValueOrDefault("evidence_received"),
        successful_count=auditCounts.GetValueOrDefault("successful_receipt"),recoverable_cancellations=auditCounts.GetValueOrDefault("recoverable_cancellation"),
        rejected_count=auditCounts.GetValueOrDefault("rejected_receipt"),blocked_attempt_count=auditCounts.GetValueOrDefault("blocked_attempt"),unexpected_failure_count=auditCounts.GetValueOrDefault("unexpected_failure"),
        error=auditError,closed=auditClosed};
    public GDict AuditStatus()=>Dict(AuditProjection());
    private void DrainAudit()
    {
        while(session.TryActionOutcome(out var outcome))
        {
            if(outcome!.StartAttempt is { } attempt)Record("decision_audit",attempt.Audit);
            Record("action_effects",outcome);
        }
        foreach(var item in definition.Declaration.GetProperty("actors").EnumerateArray())
        {
            var actor=item.GetProperty("id").GetString()!;var evidence=session.Evidence(actor);
            foreach(var entry in evidence.Skip(evidenceCursors.GetValueOrDefault(actor)))Record("evidence_received",new {actor,evidence=entry});
            evidenceCursors[actor]=evidence.Count;
        }
    }
    public GDict Step(double delta,bool idle,bool paused)
    {
        var before=session.Clock.Now;
        session.Step(delta,idle,paused,player);
        return CollectStep(before,idle,paused);
    }
    public GDict StepPhysics(double actualGameSeconds)
    {
        var before=session.Clock.Now;
        session.StepPhysical(actualGameSeconds,false,player);
        return CollectStep(before,false,false);
    }
    public GDict StepTransit(double gameSeconds)
    {
        var before=session.Clock.Now;session.StepLogical(gameSeconds,player);
        return CollectStep(before,false,false);
    }
    private GDict CollectStep(double before,bool idle,bool paused)
    {
        object? completedDeduction=null;
        var commands=new Godot.Collections.Array<GDict>();
        var completed=new Godot.Collections.Array<GDict>();
        while(session.TryCancellation(out var cancelled))
            commands.Add(Dict(new {kind="cancel",execution=Handle(cancelled!.Execution),actor=cancelled.Actor}));
        while(session.TryAction(out var decision))
        {
            executions.Add(Handle(decision!.Execution),decision);
            var command=Dict(new {kind="execute",actor=decision.Actor,execution=Handle(decision.Execution),
                action=decision.Action.Id,duration=decision.Action.Duration,declaration=decision.Action.Declaration});
            commands.Add(command);Record("action_dispatched",new {decision.Actor,action=decision.Action.Id,decision.Execution,duration=decision.Action.Duration,intent=decision.Intent});
            Record("decision_audit",session.DecisionAudit(decision));
        }
        while(session.TryReceipt(out var receipt))
        {
            Record("action_receipt",receipt!);
            // The private audit keeps the full receipt; the player only receives
            // text actually addressed to them at the action's completion instant.
            var published=receipt! with {Narrative=receipt.Narrative?.Where(line=>line.Recipients.Contains(player,StringComparer.Ordinal))
                .Select(line=>line with {Recipients=[player]}).ToArray()};
            completed.Add(Dict(published));
            if(receipt!.Committed && interruptTargets.TryGetValue(receipt.Action,out var interrupted))session.RequestCancel(interrupted);
            if(strategyActions.Contains(receipt.Action))Record("strategy",new {actor=receipt.Actor,receipt.Committed,result=receipt.Committed?session.LastStrategy:null});
            if((definition.Declaration.TryGetProperty("deduction",out var deduction) && receipt!.Action==deduction.GetProperty("action").GetString()) ||
               (definition.Declaration.TryGetProperty("deductions",out var deductions) && deductions.EnumerateArray().Any(item=>receipt!.Action==item.GetProperty("action").GetString())))
            {var result=session.LastDeduction;Record("deduction",new {actor=receipt!.Actor,result});if(receipt.Actor==player)completedDeduction=result;}
            executions.Remove(Handle(receipt.Execution));
        }
        DrainAudit();
        foreach(var item in definition.Declaration.GetProperty("actors").EnumerateArray())
        {
            var id=item.GetProperty("id").GetString()!;
            var view=session.View(id);
            var version=(view.KnowledgeRevision,session.PendingInformation(id));
            if(cognitiveVersions.TryGetValue(id,out var previous)&&previous==version)continue;
            cognitiveVersions[id]=version;
            Record("actor_cognition",new {actor=id,revision=version.KnowledgeRevision,pending=version.Item2,knowledge_count=view.Knowledge.Count});
        }
        return new GDict {{"commands",commands},{"receipts",completed},{"resting",session.Clock.Resting},{"deduction",completedDeduction is null?new GDict():Dict(completedDeduction)},{"game_seconds",session.Clock.Now},{"world_revision",session.World.Revision},
            {"decision_pending",session.HasPendingDecisionWork},
            {"has_host_navigation",session.HasHostNavigation},
            {"simulation_seconds",session.Clock.Now-before},
            {"world_speed",paused?0:session.Clock.Rate(idle,session.Working(player))}};
    }
    public bool BeginWork(long execution)
    {
        var result=executions.TryGetValue(execution,out var decision)&&session.BeginWork(decision);
        if(result)Record("work_started",new {execution});return result;
    }
    public bool WorkElapsed(long execution)=>executions.TryGetValue(execution,out var decision)&&session.WorkElapsed(decision);
    public bool PhysicalActionPermitted(long execution)=>executions.TryGetValue(execution,out var decision)&&session.PhysicalActionPermitted(decision);
    public void ReportPhysicalBoolean(string id,string facet,string field,bool value)=>session.ReportPhysicalBoolean(new(id,facet,field),value);
    public bool Complete(long execution)=>executions.TryGetValue(execution,out var decision)&&session.HostCompleted(decision);
    public void ConfigureVisibleRoom(string room)
    {
        var previous=session.VisibleRoom;session.ConfigureVisibleRoom(room);
        Record("navigation_authority_handoff",new {previous,visible_room=room,game_seconds=session.Clock.Now});
    }
    public GDict ActorActivity(string actor)
    {
        var activity=session.ActorActivity(actor);if(activity is null)return new GDict();
        var value=Dict(activity);value["execution"]=Handle(value["execution"].AsInt64());return value;
    }
    public bool HasHostNavigation()=>session.HasHostNavigation;
    public bool SetNavigationTarget(long execution,string room,double x,double y,double z,double remainingDistance,double speed)
    {
        if(!executions.TryGetValue(execution,out var decision))return false;
        var result=session.SetNavigationTarget(decision,room,x,y,z,remainingDistance,speed);
        if(result)Record("navigation_target",new {execution,actor=decision.Actor,room,x,y,z,remaining_distance=remainingDistance,speed});
        return result;
    }
    public bool ReportNavigationProgress(long execution,double x,double y,double z,double remainingDistance)
        =>executions.TryGetValue(execution,out var decision)&&session.ReportNavigationProgress(decision,x,y,z,remainingDistance);
    public bool Arrive(long execution,double x,double y,double z)
    {
        if(!executions.TryGetValue(execution,out var decision))return false;
        var result=session.Arrive(decision,x,y,z);
        if(result)Record("navigation_arrived",new {execution,actor=decision.Actor,action=decision.Action.Id,x,y,z,activity=session.ActorActivity(decision.Actor)});
        return result;
    }
    public bool Cancel()=>session.RequestCancel(player);
    public bool CancelActor(string actor)=>session.RequestCancel(actor);
    public bool Available(string action)=>session.Available(player,action);
    public void SetPlayerAutonomy(bool enabled)=>session.SetAutonomy(player,enabled);
    public void ClearPendingTime()=>session.Clock.ClearPendingSeconds();
    public void ClearFastForwardTime()=>session.Clock.ClearUnsimulatedFastForward();
    public double PendingPhysicalSeconds()=>session.Clock.PendingPhysicalSeconds;
    public double PendingSimulationSeconds()=>session.Clock.PendingSeconds;
    public GDict PreviewDeduction(string action)=>Dict(session.DeductionPreview(player,action));
    public string SubmitDeduction(string action,Godot.Collections.Array<string> evidenceIds)
    {
        try
        {
            session.SubmitDeduction(player,action,evidenceIds.ToArray());
            Record("player_deduction",new {action,evidence_ids=evidenceIds.ToArray()});
            return "";
        }
        catch(InvalidOperationException error){return error.Message;}
    }
    public bool KnowsCondition(string json)
    {
        using var document=JsonDocument.Parse(json);
        var knowledge=session.View(player).Knowledge;
        return Condition.Compile(document.RootElement,player).Evaluate(key=>knowledge.TryGetValue(key,out var fact)?fact:null)==Truth.True;
    }
    public GDict WorkProgress(string actor)
    {
        var activity=session.Activity(actor);
        if(activity?.WorkStartedAt is not { } start || activity.WorkEndsAt is not { } end)return new GDict();
        return Dict(new {action=activity.Action,progress=Math.Clamp((session.Clock.Now-start)/Math.Max(.01,end-start),0,1)});
    }
    public string CheckActionOrder(string json)
    {
        var spec=definition.Declaration.GetProperty("action_order_panel");
        if(!KnowsCondition(spec.GetProperty("evidence_gate").GetRawText()))
        {
            if(spec.TryGetProperty("evidence_checklist",out var checklist))
            {
                var missing=checklist.EnumerateArray().Where(item=>!KnowsCondition(item.GetProperty("when").GetRawText())).Select(item=>item.GetProperty("label").GetString());
                return spec.GetProperty("missing_evidence_prefix").GetString()+string.Join("、",missing);
            }
            return spec.GetProperty("missing_evidence_text").GetString()!;
        }
        var submitted=JsonSerializer.Deserialize<string[]>(json)??[];
        var expected=spec.GetProperty("correct_order").EnumerateArray().Select(item=>item.GetString()!).ToArray();
        return submitted.SequenceEqual(expected)?"":spec.GetProperty("wrong_order_text").GetString()!;
    }
    public string CompletionText(string id)
    {
        var action=definition.Declaration.GetProperty("actors").EnumerateArray().SelectMany(actor=>actor.GetProperty("actions").EnumerateArray()).First(item=>item.GetProperty("id").GetString()==id);
        var lines=new List<string>();
        if(action.TryGetProperty("completion_text",out var plain))lines.Add(plain.GetString()!);
        if(action.TryGetProperty("completion_lines",out var variants))foreach(var line in variants.EnumerateArray())
        {
            Func<FieldKey,Fact?> read=session.World.Read;
            if(line.TryGetProperty("speaker",out var speaker))
            {
                var knowledge=session.View(speaker.GetString()!).Knowledge;
                read=key=>knowledge.TryGetValue(key,out var fact)?fact:null;
            }
            if(!line.TryGetProperty("when",out var gate)||Condition.Compile(gate,player).Evaluate(read)==Truth.True)lines.Add(line.GetProperty("text").GetString()!);
        }
        return string.Join("\n",lines);
    }
    public bool ActorIncapacitated(string actor)=>session.Incapacitated(actor);
    public string ActorWorkingAction(string actor)
    {
        var activity=session.Activity(actor);
        return activity is {Phase:"working"} ? activity.Action : "";
    }
    public GDict PhysicalFact(string id,string facet,string field)
    {
        var fact=session.World.Read(new(id,facet,field));
        return fact is null?new GDict {{"known",false}}:Dict(fact);
    }
    public bool Cancelled(long execution)=>executions.TryGetValue(execution,out var decision)&&session.ConfirmCancelled(decision);
    public bool Fail(long execution,string reason)
    {
        if(!executions.TryGetValue(execution,out var decision))return false;
        Record("host_action_failed",new {execution,actor=decision.Actor,action=decision.Action.Id,reported_reason=reason});
        return session.HostFailed(decision,reason);
    }
    public void Vacated()=>session.VacatedRetainedResources(player);
    public void RecordFrame(string phase,double coreMs,double motorMs,double rate)
    {
        log?.WriteLine(JsonSerializer.Serialize(new {kind="actor_frame",real_us=Time.GetTicksUsec(),game_seconds=session.Clock.Now,
            phase,core_ms=coreMs,motor_ms=motorMs,rate,decision_evaluations=session.DecisionEvaluations}));
    }
    public void RecordEncounter(long id,string phase,double playerX,double playerZ,double targetX,double targetZ,long targetExecution)
        =>Record("conversation_motion",new {id,phase,player_x=playerX,player_z=playerZ,target_x=targetX,target_z=targetZ,target_execution=targetExecution});
    public void RecordActorMotion(string actor,long execution,string phase,double x,double z,double targetX,double targetZ,string[] resources)
        =>Record("actor_motion",new {actor,execution,phase,x,z,target_x=targetX,target_z=targetZ,resources});
    public void RecordProp(string id,string location,string holder,double x,double y,double z,string eventName)
        =>Record("prop_placement",new {id,location,holder,x,y,z,event_name=eventName});
    public void ReportPlacement(double x,double y,double z,double yaw,string room)=>session.ReportPlacement(player,x,y,z,yaw,room);
    public void ReportActorPlacement(string actor,double x,double y,double z,double yaw,string room)=>session.ReportPlacement(actor,x,y,z,yaw,room);
    public bool BeginJourney(long execution,Vector3[] path)
    {
        if(!executions.TryGetValue(execution,out var decision))return false;
        var result=session.BeginJourney(decision,path.Select(p=>new PathPoint(p.X,p.Y,p.Z)));
        if(result)Record("journey_started",new {execution,actor=decision.Actor,points=path.Length,
            length_m=path.Zip(path.Skip(1),(a,b)=>a.DistanceTo(b)).Sum(),
            start=new {x=path[0].X,y=path[0].Y,z=path[0].Z},destination=new {x=path[^1].X,y=path[^1].Y,z=path[^1].Z}});
        return result;
    }
    public GDict AdvanceJourney(long execution,double elapsed,double speed)
    {
        if(!executions.TryGetValue(execution,out var decision))return new GDict();
        var view=session.AdvanceJourney(decision,elapsed,speed);
        Record("journey_progress",new {execution,actor=decision.Actor,elapsed,speed,distance=view.Distance,length=view.Length,view.Arrived,view.Position});
        return new GDict {{"position",new Vector3((float)view.Position.X,(float)view.Position.Y,(float)view.Position.Z)},
            {"distance",view.Distance},{"length",view.Length},{"arrived",view.Arrived}};
    }
    public Vector3 PreviewJourney(long execution,double elapsed,double speed)
    {
        var point=session.PreviewJourney(executions[execution],elapsed,speed);
        return new((float)point.X,(float)point.Y,(float)point.Z);
    }
    public bool EndJourney(long execution)
    {
        if(!executions.TryGetValue(execution,out var decision))return false;
        var result=session.EndJourney(decision);
        if(result)Record("journey_ended",new {execution,actor=decision.Actor});
        return result;
    }
    public void Perceive(string actor,string id,string facet,string field,string source)
    {
        var accepted=session.ReceivePhysicalObservation(actor,new(id,facet,field),source);
        Record("passive_perception",new {actor,id,facet,field,source,accepted,pending=session.PendingInformation(actor),knowledge_revision=session.View(actor).KnowledgeRevision});
    }
    public GDict ActorDiagnostics(string actor)=>Dict(new {actor,pending=session.PendingInformation(actor),evidence_count=session.EvidenceCount(actor),
        knowledge=session.View(actor).Knowledge.Select(pair=>new{key=pair.Key,fact=pair.Value}).ToArray()});
    public bool ConversationAvailable(string id)
    {
        var declaration=definition.Declaration.GetProperty("conversations").EnumerateArray().Single(item=>item.GetProperty("id").GetString()==id);
        return session.ConversationAvailable(player,declaration);
    }
    public GDict StartConversation(string id)
    {
        try
        {
            var declaration=definition.Declaration.GetProperty("conversations").EnumerateArray().Single(item=>item.GetProperty("id").GetString()==id);
            var view=session.StartConversation(player,declaration.GetProperty("target").GetString()!,declaration);
            Record("conversation_started",view);return Dict(view);
        }
        catch(InvalidOperationException error){return new GDict { {"error",error.Message} };}
    }
    public bool AlignConversation(long id)
    {
        var result=session.AlignConversation(id);if(result)Record("conversation_aligned",session.Conversation!);return result;
    }
    public bool AdvanceConversation(long id,int line)
    {
        var result=session.AdvanceConversation(id,line);
        if(result)Record("conversation_advanced",new {id,line,next=session.Conversation});return result;
    }
    public bool InterruptConversation(long id)
    {
        var result=session.InterruptConversation(id);if(result)Record("conversation_interrupted",new {id});return result;
    }
    public bool CompleteConversation(long id)
    {
        var result=session.CompleteConversation(id);if(result)Record("conversation_released",new {id});return result;
    }
    public GDict Conversation()=>session.Conversation is { } view?Dict(view):new GDict();
    public string Submit(string action)
    {
        try{session.Submit(player,action);Record("player_action",new {action});return "";}
        catch(InvalidOperationException error){return error.Message;}
    }
    public bool RequestYield(string actor,string action)
    {
        try
        {
            var decision=session.Submit(actor,action);
            Record("actor_yield_requested",new {actor,action,decision.Execution});
            return true;
        }
        catch(InvalidOperationException){return false;}
    }
    public void RecordYieldCandidates(string actor,string room,Godot.Collections.Array<GDict> candidates,string selected)
        =>Record("yield_candidates",new {actor,room,candidates=JsonSerializer.Deserialize<object>(Json.Stringify(Variant.From(candidates))),selected});
    public GDict Snapshot()
    {
        var evidence=session.Evidence(player).ToLookup(item=>item.Key);
        return Dict(new {viewer=player,game_seconds=session.Clock.Now,game_time=session.ContentTime,
        content_hash=definition.Hash,content_update=contentUpdate,campaign_progress=DuelView.Runtime.CampaignProgress.Evaluate(definition.Declaration,session),audit_status=AuditProjection(),activity=session.Activity(player),placement=session.Placement(player),needs=session.Needs(player),world_revision=session.World.Revision,conversation=session.Conversation,
        knowledge=session.View(player).Knowledge.Select(pair=>new{key=pair.Key,fact=pair.Value,
            source_ids=evidence[pair.Key].Where(item=>SameEvidenceValue(item.Value,pair.Value.Value)).Select(item=>item.Id).Distinct().ToArray(),
            sources=evidence[pair.Key].Where(item=>SameEvidenceValue(item.Value,pair.Value.Value)).Select(item=>new{id=item.Id,kind=item.Method,label=EvidenceLabel(item),time=item.Time,
                speaker=EvidenceSourceText(item,"actor",EvidenceSourceText(item,"sender")),recipient=EvidenceSourceText(item,"recipient"),
                method=EvidenceSourceText(item,"method",item.Method),channel=EvidenceSourceText(item,"channel"),speaker_label=EvidenceSourceText(item,"speaker_label")}).ToArray()}).ToArray(),
        decision_evaluations=session.DecisionEvaluations,pending_seconds=session.Clock.PendingSeconds,pending_physical_seconds=session.Clock.PendingPhysicalSeconds,decision_pending=session.HasPendingDecisionWork,
        player_paths=session.PlayerPaths(player),active_intent=session.ActiveIntent(player),has_host_navigation=session.HasHostNavigation,visible_room=session.VisibleRoom,
        deduction=OwnMechanic("deduction"),opinion=OwnMechanic("suggestion"),incantation=OwnMechanic("incantation"),
        mechanic_history=session.MechanicHistoryFor(player).Select(item=>new {time=item.Time,execution=item.Execution,actor=item.Actor,action=item.Action,kind=item.Kind,success=item.Success,details=item.Details}).ToArray(),
        recent=timeline.ToArray()});
    }
    private static bool SameEvidenceValue(JsonElement left,JsonElement right)=>left.ValueKind==JsonValueKind.Number&&right.ValueKind==JsonValueKind.Number
        ?left.GetDouble()==right.GetDouble():left.GetRawText()==right.GetRawText();
    private static string EvidenceLabel(Evidence item)=>item.Source.ValueKind==JsonValueKind.Object&&item.Source.TryGetProperty("label",out var label)&&label.ValueKind==JsonValueKind.String
        ?label.GetString()!:item.Method;
    private static string EvidenceSourceText(Evidence item,string key,string fallback="")=>item.Source.ValueKind==JsonValueKind.Object&&item.Source.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String
        ?value.GetString()!:fallback;
    private object? OwnMechanic(string kind)=>session.MechanicHistoryFor(player).LastOrDefault(item=>item.Actor==player && item.Kind==kind)?.Details;
    public override void _ExitTree()
    {
        if(log is null)return;
        while(session.TryReceipt(out var receipt))Record("action_receipt",receipt!);
        DrainAudit();
        Record("session_closed",new {dispatch_count=auditCounts.GetValueOrDefault("action_dispatched"),receipt_count=auditCounts.GetValueOrDefault("action_receipt"),
            effect_count=auditCounts.GetValueOrDefault("action_effects"),evidence_count=auditCounts.GetValueOrDefault("evidence_received"),last_sequence=auditSequence+1,
            successful_count=auditCounts.GetValueOrDefault("successful_receipt"),recoverable_cancellations=auditCounts.GetValueOrDefault("recoverable_cancellation"),
            rejected_count=auditCounts.GetValueOrDefault("rejected_receipt"),blocked_attempt_count=auditCounts.GetValueOrDefault("blocked_attempt"),unexpected_failure_count=auditCounts.GetValueOrDefault("unexpected_failure"),
            content_hash=definition.Hash,game_seconds=session.Clock.Now,progress=DuelView.Runtime.CampaignProgress.Evaluate(definition.Declaration,session),error=auditError});
        try{log.Flush();if(log.BaseStream is FileStream stream)stream.Flush(true);}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException){auditError=error.Message;GD.PushError("Causal audit close failed: "+error.Message);}
        log.Dispose();log=null;auditClosed=true;
    }
}






