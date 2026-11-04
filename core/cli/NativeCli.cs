using System.Text.Json;
using System.Text.Encodings.Web;
using DuelView.Runtime;

// Production text adapter for the same independent Core used by Main.
// Room transitions are timed formal actions; CLI does not claim Godot collision evidence.
internal sealed class NativeCli : IDisposable
{
    const string DefaultContent="assets/data/scenarios/campaign.dv";
    static readonly JsonSerializerOptions Json=new(){Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    readonly CharacterSessionDefinition definition;
    readonly RuntimeSession session;
    readonly JsonElement content;
    readonly string viewer;
    readonly StreamWriter? log;
    readonly StreamWriter? stateLog;
    long stateIndex;
    readonly Dictionary<string,int> completed=[];
    readonly Dictionary<string,double> firstCompletion=[];
    readonly Dictionary<long,Decision> started=[];
    readonly Dictionary<long,DriveEvaluation> decisionDrives=[];
    readonly Dictionary<long,object> decisionReasons=[];
    readonly Dictionary<string,DriveEvaluation> firstDrives=[];
    readonly Dictionary<string,DecisionOption[]> firstOptions=[];
    readonly List<(string Action,string Slot,double Start,double End)> executions=[];
    readonly List<(string Actor,AffectEvent Event)> affectEvents=[];
    readonly Dictionary<string,int> evidenceCursors=[];
    readonly HashSet<string> invariantFailures=[];
    readonly List<object> history=[];
    long version;
    int failures;
    string? selectedObject;
    readonly double end;
    readonly string? receiptPath;
    static JsonElement Get(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)?v:default;
    static string Text(JsonElement e,string fallback="")=>e.ValueKind==JsonValueKind.String?e.GetString()!:fallback;
    static IEnumerable<JsonElement> Items(JsonElement e)=>e.ValueKind==JsonValueKind.Array?e.EnumerateArray():[];
    static double Num(JsonElement e,double fallback=0)=>e.ValueKind==JsonValueKind.Number?e.GetDouble():fallback;
    static string PathOf(string path)=>Path.GetFullPath(path.Replace("res://",""));
    static FieldKey Key(JsonElement r)=>new(Text(Get(r,"object")),Text(Get(r,"facet")),Text(Get(r,"field")));
    static string Encode(object? value)=>JsonSerializer.Serialize(value,Json);
    NativeCli(JsonElement options)
    {
        var path=Text(Get(options,"content_path"),DefaultContent);
        definition=new(File.ReadAllText(PathOf(path)),File.ReadAllText("assets/data/98-runtime-session.data.dv"));
        content=definition.Declaration;
        viewer=Text(Get(options,"viewer"),Text(Get(content,"viewer")));
        if(!Actors.Any(a=>Text(Get(a,"id"))==viewer))throw new InvalidDataException("Unknown viewer.");
        session=definition.Create();
        foreach(var item in Items(Get(content,"deductions")))session.ConfigureDeduction(File.ReadAllText(PathOf(Text(Get(item,"source")))),item);
        foreach(var item in Items(Get(content,"strategy_activities")))session.ConfigureStrategy(File.ReadAllText(PathOf(Text(Get(item,"source")))),item);
        end=Num(Get(Get(content,"run"),"end_minutes"));
        if(end<=session.ContentTime)throw new InvalidDataException("Invalid DV run horizon.");
        if(Text(Get(options,"controller"))!="ai")session.SetAutonomy(viewer,false);
        receiptPath=Text(Get(options,"receipt_path"));
        if(receiptPath.Length>0){Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);log=new(receiptPath);}
        if(receiptPath.Length>0)stateLog=new(Path.ChangeExtension(receiptPath,"states.yaml"));
    }
    IEnumerable<JsonElement> Actors=>Items(Get(content,"actors"));
    string Room(string actor)=>Text(session.World.Read(new(actor,"state","room"))?.Value??default);
    bool Active=>session.ContentTime<end;
    bool Visible(JsonElement fixture)=>Text(Get(fixture,"room"))==Room(viewer)&&Get(fixture,"hidden").ValueKind!=JsonValueKind.True;
    IEnumerable<JsonElement> Actions(string actor)=>Items(Get(Actors.Single(a=>Text(Get(a,"id"))==actor),"actions"));
    object Status()=>new {version,chapter=session.ContentTime<1440?1:2,game_time=session.ContentTime,day=(int)(session.ContentTime/1440)+1,status=Active?"running":"stopped",stop_reason=Active?"":"dv_time_horizon",horizon=end};
    bool Check(JsonElement e)
    {
        var absent=Text(Get(e,"action_absent"));
        if(absent.Length>0)return completed.GetValueOrDefault(absent)==0;
        var spread=Get(e,"start_spread");
        if(spread.ValueKind==JsonValueKind.Object)
        {
            var wanted=Items(Get(spread,"actions")).Select(a=>a.GetString()!).ToArray();
            var starts=wanted.Select(id=>executions.Where(x=>x.Action==id).Select(x=>x.Start).DefaultIfEmpty(double.NaN).Min()).ToArray();
            return starts.All(double.IsFinite)&&(starts.Max()-starts.Min())/60>=Num(Get(spread,"minimum_minutes"));
        }
        var countRule=Get(e,"state_event_count");
        if(countRule.ValueKind==JsonValueKind.Object)
        {
            var count=affectEvents.Count(item=>item.Event.Time/60>=Num(Get(e,"from_minutes"))&&item.Event.Time/60<Num(Get(e,"until_minutes"),double.PositiveInfinity)&&item.Event.Kind==Text(Get(countRule,"kind"))&&
                (Text(Get(countRule,"actor")).Length==0||item.Actor==Text(Get(countRule,"actor")))&&
                (Text(Get(countRule,"source")).Length==0||item.Event.Source==Text(Get(countRule,"source"))));
            return count>=Num(Get(countRule,"minimum"))&&count<=Num(Get(countRule,"maximum"),double.PositiveInfinity);
        }
        var sensory=Get(e,"perception_source");
        if(sensory.ValueKind==JsonValueKind.Object)return session.Evidence(Text(Get(sensory,"actor"))).Any(item=>item.Id.StartsWith("perception/",StringComparison.Ordinal)&&
            item.Key.Object==Text(Get(sensory,"object"))&&(Text(Get(sensory,"facet")).Length==0||item.Key.Facet==Text(Get(sensory,"facet")))&&item.Key.Field==Text(Get(sensory,"field"))&&Text(Get(item.Source,"sensor"))==Text(Get(sensory,"sensor")));
        var inference=Get(e,"inference_evidence");
        if(inference.ValueKind==JsonValueKind.Object)return session.Evidence(Text(Get(inference,"actor"))).Any(item=>item.Method=="inference"&&
            item.Key.Object==Text(Get(inference,"object"))&&(Text(Get(inference,"facet")).Length==0||item.Key.Facet==Text(Get(inference,"facet")))&&item.Key.Field==Text(Get(inference,"field"))&&
            Items(Get(item.Source,"premises")).Any()&&Items(Get(item.Source,"premises")).All(p=>Items(Get(p,"evidence_ids")).Any()));
        var provenance=Get(e,"evidence_source");
        if(provenance.ValueKind==JsonValueKind.Object)return session.Evidence(Text(Get(provenance,"actor"))).Any(item=>
            item.Key.Object==Text(Get(provenance,"object"))&&(Text(Get(provenance,"facet")).Length==0||item.Key.Facet==Text(Get(provenance,"facet")))&&item.Key.Field==Text(Get(provenance,"field"))&&Text(Get(item.Source,"action"))==Text(Get(provenance,"action")));
        var competition=Get(e,"drive_competition");
        if(competition.ValueKind==JsonValueKind.Object)
        {
            var chosen=Text(Get(competition,"action"));
            if(!firstOptions.TryGetValue(chosen,out var options))return false;
            var winner=options.FirstOrDefault(o=>o.Action==chosen);
            var other=options.FirstOrDefault(o=>o.Action==Text(Get(competition,"alternative")));
            return winner is not null&&other is not null&&winner.Score>other.Score&&
                winner.Score-winner.Drive.NeedContributions.GetValueOrDefault(Text(Get(competition,"without")))<other.Score;
        }
        var bonus=Get(e,"drive_bonus");
        if(bonus.ValueKind==JsonValueKind.Object)
        {
            if(!firstDrives.TryGetValue(Text(Get(bonus,"action")),out var evaluation))return false;
            var value=evaluation.NeedContributions.GetValueOrDefault(Text(Get(bonus,"key")));
            if(Get(bonus,"required_for_positive_score").ValueKind==JsonValueKind.True&&(evaluation.Score<=0||evaluation.Score-value>0))return false;
            return value>=Num(Get(bonus,"minimum"),double.NegativeInfinity)&&value<=Num(Get(bonus,"maximum"),double.PositiveInfinity);
        }
        var marker=Text(Get(e,"state_event"));
        if(marker.Length>0)return affectEvents.Any(item=>item.Actor==Text(Get(e,"actor"))&&item.Event.Kind==marker&&item.Event.Source==Text(Get(e,"source_action")));
        var before=Text(Get(e,"before"));var after=Text(Get(e,"after"));
        if(before.Length>0)return firstCompletion.TryGetValue(before,out var b)&&firstCompletion.TryGetValue(after,out var a)&&b<a;
        var actor=Text(Get(e,"actor"));
        if(actor.Length>0)return Condition.Compile(Get(e,"condition"),actor).Evaluate(k=>session.View(actor).Knowledge.GetValueOrDefault(k))==Truth.True;
        return Text(Get(e,"action")).Length>0?completed.GetValueOrDefault(Text(Get(e,"action")))>=Num(Get(e,"minimum"),1):Condition.Compile(Get(e,"condition"),"world").Evaluate(session.World.Read)==Truth.True;
    }
    object Assessment()=>new {content_hash=definition.Hash,game_time=session.ContentTime,failures,invariant_failures=invariantFailures.ToArray(),checks=Items(Get(content,"expectations")).Select(e=>new {id=Text(Get(e,"id")),passed=Check(e)}).ToArray()};
    void CheckKnowledgeBoundaries()
    {
        foreach(var rule in Items(Get(content,"knowledge_boundaries")))
        {
            var actor=Text(Get(rule,"actor"));var key=Key(Get(rule,"ref"));
            if(session.View(actor).Knowledge.GetValueOrDefault(key) is {Known:true} && completed.GetValueOrDefault(Text(Get(rule,"after")))==0)
                invariantFailures.Add(actor+":"+key+" before "+Text(Get(rule,"after")));
        }
    }
    object Observe(int count=0)
    {
        var options=Actions(viewer).Where(a=>session.Available(viewer,Text(Get(a,"id"))));
        if(selectedObject is not null)options=options.Where(a=>Text(Get(a,"target_object"))==selectedObject);
        return new {active=Active,faulted=false,version,completed=count,status=Status(),runtime=Status(),
            content=new {hash=definition.Hash,schema="dv-script-v1"},receipt_path=receiptPath,
            screen=new {viewer,room=Room(viewer),actors=Actors.Where(a=>Room(Text(Get(a,"id")))==Room(viewer)&&Get(a,"hidden").ValueKind!=JsonValueKind.True).Select(a=>new{id=Text(Get(a,"id")),name=Text(Get(a,"name"))}),
                objects=Items(Get(content,"fixtures")).Where(Visible).Select(f=>new{id=Text(Get(f,"id")),label=Text(Get(f,"label"))}),
                knowledge=session.View(viewer).Knowledge.Select(k=>new{key=k.Key,value=k.Value.Value}),needs=session.Needs(viewer),affect=session.Affect(viewer)},
            menu=selectedObject is null?null:new {level=1,shape="radial",target=selectedObject},
            options=options.Select(a=>new{id=Text(Get(a,"id")),label=Text(Get(a,"label"))}).ToArray(),history=history.ToArray(),
            acceptance=Active?null:Assessment()};
    }
    void Receipt(ActionReceipt r)
    {
        version++;
        if(r.Committed){completed[r.Action]=completed.GetValueOrDefault(r.Action)+1;firstCompletion.TryAdd(r.Action,r.Time);}else failures++;
        started.Remove(r.Execution,out var execution);
        decisionDrives.Remove(r.Execution,out var drive);
        decisionReasons.Remove(r.Execution,out var reasons);
        var start=execution?.StartedAt??r.Time;
        var focus=execution is null?"":Text(Get(execution.Action.Declaration,"focus"));
        var interaction=execution is null?"":Text(Get(execution.Action.Declaration,"interaction_slot"));
        if(r.Committed)
        {
            if(interaction.Length>0&&executions.Any(x=>x.Slot==interaction&&Math.Max(start,x.Start)<Math.Min(r.Time,x.End)-0.000001))
                invariantFailures.Add("overlapping interaction slot: "+interaction);
            executions.Add((r.Action,interaction,start,r.Time));
        }
        var row=new Dictionary<string,object?> { ["receipt_index"]=version,["focus"]=focus,["interaction_slot"]=interaction,
            ["version"]=version,["chapter"]=start/60<1440?1:2,["day"]=(int)(start/60/1440)+1,["start_time"]=start/60,["duration"]=(r.Time-start)/60,["actor"]=r.Actor,["action"]=r.Action,
            ["label"]=execution is null?"":Text(Get(execution.Action.Declaration,"label")),
            ["outcome"]=r.Committed?"completed":r.Reason,["execution_event"]=r.Execution,
            ["decision_drive"]=drive,["candidate_scores"]=execution?.Options,
            ["decision_basis"]=reasons,["reason"]=execution is null?"":Text(Get(execution.Action.Declaration,"reason")),
            ["needs"]=session.Needs(r.Actor),["room"]=Room(r.Actor),["knowledge_before_refresh"]=session.View(r.Actor).Knowledge.Select(k=>new{key=k.Key,value=k.Value.Value})};
        if(log is not null){var first=true;foreach(var pair in row){log.WriteLine((first?"- ":"  ")+pair.Key+": "+Encode(pair.Value));first=false;}log.WriteLine();log.Flush();}
        if(Room(r.Actor)==Room(viewer)&&Get(Actors.Single(a=>Text(Get(a,"id"))==r.Actor),"hidden").ValueKind!=JsonValueKind.True)
        {history.Add(new{actor=r.Actor,action=r.Action,completed=r.Committed,start_time=start/60,duration=(r.Time-start)/60});if(history.Count>16)history.RemoveAt(0);}
    }
    object Advance(int steps)
    {
        var before=version;
        // Batching speeds up wall time, never skips simulation boundaries or action costs.
        for(var n=0;n<Math.Clamp(steps,1,100)*300&&Active;n++)
        {
            session.Step(Math.Min(1,(end-session.ContentTime)*60),false,false);
            while(session.TryAction(out var decision))
            {
                started[decision!.Execution]=decision;
                if(decision.Options is { } options)firstOptions.TryAdd(decision.Action.Id,options);
                var drive=session.EvaluateDrive(decision.Actor,decision.Action.Id);
                decisionDrives[decision.Execution]=drive;firstDrives.TryAdd(decision.Action.Id,drive);
                var because=Get(decision.Action.Declaration,"because");
                if(because.ValueKind==JsonValueKind.Object)
                {
                    decisionReasons[decision.Execution]=because.EnumerateObject().Select(p=>{
                        var split=p.Name.LastIndexOf('.');var prefix=p.Name[..split];var layer=prefix.LastIndexOf('.');
                        var explicitFacet=layer>0&&prefix[(layer+1)..] is "phys" or "trace" or "lock" or "doc" or "infer";
                        var key=new FieldKey(explicitFacet?prefix[..layer]:prefix,explicitFacet?prefix[(layer+1)..]:"state",p.Name[(split+1)..]);
                        var fact=session.View(decision.Actor).Knowledge.GetValueOrDefault(key);
                        if(fact is not {Known:true,Conflicted:false}||(fact.Value.ValueKind==JsonValueKind.Number&&p.Value.ValueKind==JsonValueKind.Number?fact.Value.GetDouble()!=p.Value.GetDouble():fact.Value.GetRawText()!=p.Value.GetRawText()))invariantFailures.Add(decision.Action.Id+": missing intent evidence "+p.Name);
                        return new {key,value=fact?.Value,sources=session.Evidence(decision.Actor).Where(e=>e.Key==key).Select(e=>new{e.Id,e.Method,e.Source}).ToArray()};
                    }).ToArray();
                }
                var wake=Num(Get(Actors.Single(a=>Text(Get(a,"id"))==decision.Actor),"wake_at_game_minutes"));
                if(decision.StartedAt/60<wake)invariantFailures.Add(decision.Actor+": acted before wake time");
                if(Get(decision!.Action.Declaration,"requires_host_completion").ValueKind==JsonValueKind.True)
                    throw new InvalidDataException("CLI requires a declared logical execution; cannot forge a physical host completion.");
            }
            while(session.TryReceipt(out var receipt))Receipt(receipt!);
            RecordAffects();
            CheckKnowledgeBoundaries();
        }
        // Commit actions due exactly at the authored horizon without advancing the clock.
        if(!Active){session.Step(0,false,false);while(session.TryReceipt(out var receipt))Receipt(receipt!);if(!string.IsNullOrEmpty(receiptPath))File.WriteAllText(Path.ChangeExtension(receiptPath,"acceptance.json"),Encode(Assessment()));}
        return Observe((int)(version-before));
    }
    void RecordAffects()
    {
        foreach(var actorDefinition in Actors)
        {
            var actor=Text(Get(actorDefinition,"id"));var evidence=session.Evidence(actor);
            var renderedInferences=new HashSet<string>();
            foreach(var item in evidence.Skip(evidenceCursors.GetValueOrDefault(actor)))
            {
                var isCommunication=item.Id.StartsWith("handoff/",StringComparison.Ordinal);
                var isPerception=item.Id.StartsWith("perception/",StringComparison.Ordinal);
                var isInference=item.Method=="inference";
                if(isInference&&!renderedInferences.Add(item.Id+":"+item.Key+":"+item.Source.GetRawText()))continue;
                var isObservation=item.Id.StartsWith("action/",StringComparison.Ordinal)&&!item.Key.Object.StartsWith("event:",StringComparison.Ordinal)&&item.Key.Field!="room";
                if(!isCommunication&&!isInference&&!isObservation&&!isPerception)continue;
                var sourceAction=Text(Get(item.Source,"action"));var label="";
                if(isInference)
                {
                    var basis=Get(item.Source,"basis");
                    label="[推理更新] "+(basis.ValueKind==JsonValueKind.Object?string.Join("；",new[]{("facts","观测事实"),("excluded","排除与保留"),("conclusion","当前结论"),("question","下一问")}.Select(part=>part.Item2+"："+Text(Get(basis,part.Item1)))):Text(basis));
                }
                else
                {
                    var speaker=isCommunication?Text(Get(item.Source,"actor")):actor;
                    var name=Text(Get(Actors.Single(a=>Text(Get(a,"id"))==speaker),"name"),speaker);
                    var fieldLabel=Text(Get(Get(content,"fact_labels"),item.Key.Object+"."+item.Key.Facet+"."+item.Key.Field));
                    var message=fieldLabel.Length>0&&item.Key.Field=="exit"?fieldLabel:item.Value.ValueKind==JsonValueKind.String?item.Value.GetString()!.TrimEnd('。','，'):
                        fieldLabel.Length>0?fieldLabel+(item.Value.ValueKind==JsonValueKind.True?"":item.Value.ValueKind==JsonValueKind.False?"：否":"："+item.Value.GetRawText()):
                        item.Key.Facet=="state"?"状态更新":item.Value.GetRawText();
                    label=isCommunication?"[获知] 来自"+name+"："+message:isPerception?"["+(Text(Get(item.Source,"channel"))=="hearing"?"听觉感知":Text(Get(item.Source,"channel"))=="blocked"?"交互受阻与感知":"视线感知")+"] "+Text(Get(item.Source,"label"))+"；"+message:"[观察所得] "+message;
                }
                var row=new Dictionary<string,object?>{["receipt_index"]=++stateIndex,["kind"]=isInference?"inference":isCommunication?"communication_received":isPerception?"perception":"observation",["start_time"]=session.ContentTime,["duration"]=0,
                    ["actor"]=actor,["action"]=sourceAction,["label"]=label,
                    ["outcome"]="completed",["room"]=Room(actor),["evidence_id"]=item.Id,["source"]=item.Source,["fact_ref"]=item.Key,["fact_value"]=item.Value};
                if(stateLog is not null){var first=true;foreach(var pair in row){stateLog.WriteLine((first?"- ":"  ")+pair.Key+": "+Encode(pair.Value));first=false;}stateLog.WriteLine();stateLog.Flush();}
            }
            evidenceCursors[actor]=evidence.Count;
        }
        foreach(var (actor,item) in session.DrainAffectEvents())
        {
            affectEvents.Add((actor,item));
            var row=new Dictionary<string,object?>{["receipt_index"]=++stateIndex,["kind"]=item.Kind,["start_time"]=item.Time/60,["duration"]=item.Duration/60,
                ["actor"]=actor,["action"]=item.Source,["label"]=item.Label,["outcome"]="completed",["room"]=Room(actor)};
            if(stateLog is not null){var first=true;foreach(var pair in row){stateLog.WriteLine((first?"- ":"  ")+pair.Key+": "+Encode(pair.Value));first=false;}stateLog.WriteLine();stateLog.Flush();}
        }
    }
    object Select(JsonElement request)
    {
        if(!Active)throw new InvalidOperationException("The DV time horizon has been reached.");
        var choice=Text(Get(request,"choice_id"));
        var id=choice.StartsWith("object:",StringComparison.Ordinal)?choice[7..]:Text(Get(request,"object_id"));
        if(id.Length>0)
        {
            if(!Items(Get(content,"fixtures")).Any(f=>Text(Get(f,"id"))==id&&Visible(f)))throw new InvalidOperationException("Object is outside the viewer's visible room.");
            selectedObject=id;return Observe(); // Opening the first-level menu consumes no time and never starts movement.
        }
        var action=Text(Get(request,"choice_id"),Text(Get(request,"action_id")));
        if(!session.Available(viewer,action))throw new InvalidOperationException("Action is not available to this actor.");
        session.Submit(viewer,action);selectedObject=null;return Observe();
    }
    public void Dispose()
    {
        RecordAffects();stateLog?.Dispose();
        log?.Dispose();
        if(!string.IsNullOrEmpty(receiptPath)&&File.Exists(receiptPath))
            ReadableEventLog.Write(receiptPath,content,Path.ChangeExtension(receiptPath,"readable.txt"));
    }
    public static int ValidateContent()
    {
        try {using var doc=JsonDocument.Parse("{\"controller\":\"ai\"}");using var host=new NativeCli(doc.RootElement);Console.WriteLine("PASS independent Core DV new-game initialization");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    public static void Run()
    {
        NativeCli? host=null;
        try {while(Console.ReadLine() is { } line)
        {
            long requestId=0;
            try
            {
                using var doc=JsonDocument.Parse(line);var request=doc.RootElement;requestId=(long)Num(Get(request,"id"));var type=Text(Get(request,"type"));object result;
                if(type=="open"){host?.Dispose();host=new(Get(request,"options"));result=host.Observe();}
                else if(type=="close"){Console.WriteLine(Encode(new{id=requestId,result=new{closed=true}}));break;}
                else if(host is null)throw new InvalidOperationException("Open a session first.");
                else result=type switch {"advance"=>host.Advance((int)Num(Get(request,"steps"),1)),"select"=>host.Select(request),"observe"=>host.Observe(),_=>throw new InvalidOperationException("Unsupported operation.")};
                Console.WriteLine(Encode(new{id=requestId,result}));
            }
            catch(Exception e){Console.WriteLine(Encode(new{id=requestId,error=e.ToString()}));}
        }}finally{host?.Dispose();}
    }
}
