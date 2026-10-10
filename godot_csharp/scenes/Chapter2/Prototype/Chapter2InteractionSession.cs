using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Story;
namespace DualOntologyWorldEngine.Chapter2.Prototype;

/// <summary>Semantic chapter events. Presentation acknowledgements advance text, never authorize client state writes.</summary>
public sealed partial class Chapter2InteractionSession : IChapterSession, IPlayerRoleSession
{
    public string ChapterId => "chapter2";
    public string PlayerActor => _actorIds[Enum.Parse<P>(_fieldPresentation.PlayerIdentity)];
    public PlayerCognition CapturePlayerCognition(string actor)
    {
        if (actor != PlayerActor) throw new PlayerBoundaryException("unsupported_player_role");
        var character = _behavior.Character(actor);
        if (character.Soul.World.CaptureSnapshot() is not ContractResult<CognitionSnapshotV1>.Success captured)
            throw new PlayerBoundaryException("actor_cognition_missing");
        return new(captured.Value, character.Self.SubjectId!.Value);
    }
    public PlayerTime PlayerTime => new(ShownDay, _game.CalendarMinutes, ShownSlot.ToString());
    public SimulationSession TraceSimulation => _game.S;
    private readonly Game _game;
    private readonly Chapter2Narrative _narrative;
    private readonly WorldSession _behavior;
    private readonly Dictionary<P,string> _actorIds;
    private readonly IReadOnlyList<Chapter2AuthoredSite> _sites;
    private readonly Chapter2Presentation _fieldPresentation;
    private readonly IReadOnlyDictionary<string,string> _reactionIntents;
    private readonly IReadOnlyList<(string Key,string Text)> _furniture;
    private readonly HashSet<string> _furnitureSpoken = new(StringComparer.Ordinal);
    private HashSet<string>? _nearby;
    private string? _behaviorPresentation;
    private string? _awaitingNarrativeChoice;
    private readonly Queue<Action<List<Dictionary<string, object?>>>> _continuations = new();
    private readonly Dictionary<string, (string Token, Func<bool> Available, Action<List<Dictionary<string, object?>>> Submit)> _choices = new();
    private readonly Dictionary<string, Dictionary<string, object?>> _examined = new();
    private readonly Dictionary<string, string> _talked = new();
    private readonly HashSet<string> _knownPlaces = ["DormLife", "Living"];
    private readonly List<object> _journal = new();
    private string? _presentation;
    private string? _narrativePresentation;
    private string _stage = "intro";
    private int _version, _replayCount;
    private bool _fullMatch, _replayOpen, _ended;
    private Dictionary<string, object?> _replayResult = new();
    private readonly List<(int,int)> _pairs = new();
    /// <summary>对质的议程：每一步问一个话题（取用、门闩、台账），期望的回答是现实里为真、尚未答过的那类主张。</summary>
    private readonly List<string> _agendaDone = new();
    private readonly HashSet<int> _answered = new();
    private readonly HashSet<string> _skippedTopics = new();
    private string _question="",_topic="";
    private HashSet<int> _expected=new();
    private readonly Chapter2InteractionContent _content;
    private readonly string[] _confrontQuestions;
    public Dictionary<string,object?> ReplayMetadata()=>new(){["title"]=_content.Text("replay_title"),["hint"]=_content.Text("replay_hint")};
    public int ShownDay { get; private set; } = 2;
    public Slot ShownSlot { get; private set; } = Slot.Morning;

    public Chapter2InteractionSession(Game game, string script)
    {
        ValidateScript(script);
        _game = game; _narrative = new(script, game.InheritedChapter1);
        _content=new(script); _confrontQuestions=_content.Texts("confront_questions",4);
        _sites=Chapter2AuthoredSites.ReadAll(script);
        _fieldPresentation = new(script);
        _map = _fieldPresentation.InitialMap;
        _game.BindInvestigation(script);
        using(var document=System.Text.Json.JsonDocument.Parse(script))
        {
            LoadFlows(document.RootElement.GetProperty("chapters")[0]);
            var furniture=document.RootElement.GetProperty("chapters")[0].GetProperty("furniture");
            _furniture=furniture.EnumerateArray().Select(f=>(f.GetProperty("key").GetString()!,f.GetProperty("text").GetString()!)).OrderByDescending(f=>f.Item1.Length).ToArray();
        }
        var chapter=WorldScriptDocument.Parse(script, allowChapterAdapter: true).Single();
        _reactionIntents=chapter.Candidates.Where(c=>c.Kind=="reaction").ToDictionary(c=>c.Id.StartsWith(chapter.Id+":",StringComparison.Ordinal)?c.Id:chapter.Id+":"+c.Id,c=>c.Intent,StringComparer.Ordinal);
        foreach(var intent in _reactionIntents.Values)if(intent is not ("testimony" or "decline"))throw new InvalidDataException("Formal NPC reactions must declare testimony or decline intent.");
        var entities=new Dictionary<string,Essence>();var characters=new Dictionary<string,(SoulEssence,Projection)>();
        _actorIds=new();
        foreach(var declaration in chapter.Entities)
        {
            var id=Chapter2Bindings.Id(declaration);
            var entity=Chapter2Bindings.Resolve(game,declaration);
            if(declaration.Kind=="character")
            {
                var person=game[Chapter2Bindings.PersonId(declaration)];
                characters[id]=(person.Soul,person.Self);_actorIds[person.Id]=id;
            }
            entities.Add(id,entity);
        }
        if (game.PersistentSession is { } persistent)
        {
            _behavior = persistent;
            _behavior.AttachExistingContent(chapter, entities, characters);
        }
        else
        {
            _behavior = new(game.Rt, game.S);
            _behavior.BindExisting(chapter, entities, characters, game.InheritedChapter1 ? ["awakening"] : []);
        }
    }
    public static void ValidateScript(string script)
    {
        var chapter=WorldScriptDocument.Parse(script,allowChapterAdapter:true).Single();
        if(chapter.Requires.Count!=1||chapter.Requires[0]!="awakening")throw new InvalidDataException("Formal chapter two requires exactly the live awakening chapter.");
        Chapter2Bindings.Validate(chapter);_=Chapter2AuthoredSites.ReadAll(script);
        var content=new Chapter2InteractionContent(script);
        foreach(var key in new[]{"replay_title","replay_hint","hungry","bed_early","watch_early","meal_closed"})_=content.Text(key);
        _=content.Texts("finding_titles",5);_=content.Texts("confront_questions",4);
        foreach(var reaction in chapter.Candidates.Where(c=>c.Kind=="reaction"))if(reaction.Intent is not ("testimony" or "decline"))throw new InvalidDataException("A formal reaction must declare its executable intent.");
        using var document=System.Text.Json.JsonDocument.Parse(script);
        var furniture=document.RootElement.GetProperty("chapters")[0].GetProperty("furniture");
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in furniture.EnumerateArray())
            if(string.IsNullOrWhiteSpace(entry.GetProperty("key").GetString())||string.IsNullOrWhiteSpace(entry.GetProperty("text").GetString())||!keys.Add(entry.GetProperty("key").GetString()!))throw new InvalidDataException("Invalid or duplicate furniture description.");
    }

    private static Dictionary<string, object?> Event(string kind, params (string Key, object? Value)[] args)
    { var result = new Dictionary<string, object?> { ["kind"] = kind }; foreach (var (key,value) in args) result[key] = value; return result; }
    private static string Text(IReadOnlyDictionary<string, object?> input, string key, string fallback = "") => input.GetValueOrDefault(key)?.ToString() ?? fallback;
    private static string Text(IReadOnlyDictionary<string, string> input, string key, string fallback = "") => input.GetValueOrDefault(key) ?? fallback;
    private static int Number(IReadOnlyDictionary<string, object?> input, string key, int fallback = -1) => int.TryParse(Text(input,key),out var n) ? n : fallback;

    public IReadOnlyList<Dictionary<string, object?>> Dispatch(IReadOnlyDictionary<string, object?> input)
    {
        var output = new List<Dictionary<string, object?>>();
        try
        {
            if(input.GetValueOrDefault("nearby_people") is IEnumerable<object?> nearby) _nearby=nearby.Select(p=>p?.ToString()??"").ToHashSet(StringComparer.Ordinal);
            ReadContext(input);
            var kind = Text(input,"kind");
            if (kind is "tick" or "daily") return output;
            if (_presentation is not null && kind != "presented") throw new InvalidOperationException("请先完成当前呈现。");
            switch (kind)
            {
                case "begin":
                    if (_stage != "intro") throw new InvalidOperationException("章节已开始。");
                    _stage = "beginning";
                    EnterSlot();
                    Pump(output); break;
                case "presented":
                    if (_presentation is null || Text(input,"presentation_id") != _presentation) throw new InvalidOperationException("呈现凭证已失效。");
                    _presentation = null;
                    if (_narrativePresentation is { } narrativeId)
                    { _narrativePresentation = null; Consume(_narrative.Presented(narrativeId),output); }
                    if (_behaviorPresentation is { } behaviorId)
                    { _behaviorPresentation=null;BehaviorEvents(_behavior.Process(new PresentationCompleted(behaviorId)),output); }
                    Pump(output); break;
                case "select":
                    var id = Text(input,"choice_id");
                    if (!_choices.TryGetValue(id,out var choice) || Text(input,"token") != choice.Token || !choice.Available()) throw new InvalidOperationException("选择已失效或当前不可用。");
                    _choices.Clear(); ++_version; choice.Submit(output); Pump(output); break;
                case "interact": Interact(input,output); break;
                case "card":
                    ShownDay=_game.Day; ShownSlot=_game.Slot;
                    if (!_examined.TryGetValue(Text(input,"card_id"),out var card)) throw new InvalidOperationException("尚未观察该卡片。");
                    ShowCard(Text(input,"card_id"),card,output); break;
                case "furniture":
                    ShownDay=_game.Day; ShownSlot=_game.Slot;
                    var hitNodes=input.GetValueOrDefault("nodes") is IEnumerable<object?> hits ? hits.Select(n=>n?.ToString()??"") : [Text(input,"node")];
                    foreach(var hitNode in hitNodes)
                    {
                        var furniture=_furniture.FirstOrDefault(f=>hitNode.StartsWith(f.Key,StringComparison.Ordinal));
                        if(furniture.Key is not null&&_furnitureSpoken.Add(Text(input,"map")+":"+hitNode)) { output.Add(Event("aside",("text",furniture.Text)));break; }
                    }
                    break;
                case "blocked_move": Lines([_content.Text("corridor_closed")],output); break;
                case "move":
                    var nextMap = Text(input,"map",_map);
                    if (!Enum.TryParse<Place>(Text(input,"place",MapPlace(nextMap)),out var place)) { Invalidate(); break; }
                    _game.HostMoveDet(place); _map=nextMap; _knownPlaces.Add(place.ToString()); Invalidate(); break;
                case "action":
                    if (_stage != "field" || _choices.Count != 0) throw new InvalidOperationException("当前不能提交时段行动。");
                    if (input.GetValueOrDefault("act") is not IReadOnlyDictionary<string,object?> action) throw new InvalidOperationException("缺少行动。");
                    Commit(Action(action),output); break;
                case "replay": Replay(input,output); break;
                case "replay_vars": _replayVars=(input.GetValueOrDefault("vars") as IEnumerable<object?> ?? []).Select(v=>(object?)Convert.ToInt32(v)).ToArray(); break;
                case "opened": break;
                default: throw new InvalidOperationException("未知章节事件：" + kind);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or KeyNotFoundException or PlayerError)
        { output.Add(Event("error",("message",error.Message))); }
        output.Add(Event("state",("snapshot",Snapshot())));
        return output;
    }

    private void Invalidate() { _choices.Clear(); ++_version; }
    private void Pump(List<Dictionary<string,object?>> output)
    {
        while (_presentation is null && _choices.Count == 0)
        {
            if (_continuations.TryDequeue(out var next)) next(output);
            else if (_flowSteps.TryPop(out var step)) FlowStep(step, output);
            else if (_stage is "beginning" or "field" && EnterSlot()) continue;
            else break;
        }
    }
    private void Lines(IEnumerable<string> lines,List<Dictionary<string,object?>> output,string speaker = "旁白",string mood = "")
    {
        var array = lines.Where(s=>!string.IsNullOrWhiteSpace(s)).Select(s=>(object)new Dictionary<string,object?> { ["speaker"]=speaker,["text"]=s,["mood"]=mood,["voice_clip"]="" }).ToArray();
        if (array.Length == 0) return;
        _presentation = Guid.NewGuid().ToString("N"); output.Add(Event("lines",("presentation_id",_presentation),("lines",array)));
    }
    private void Activate(string name,List<Dictionary<string,object?>> output)
    {
        // A new authored scene occurs in the current slot. Run's feed remains
        // labelled with the slot it just settled until a new scene is activated.
        ShownDay=_game.Day; ShownSlot=_game.Slot;
        Consume(_narrative.Activate(name,_game.Day,_game.Slot,Variables()),output);
    }
    private IReadOnlyDictionary<string,string> Variables() => new Dictionary<string,string>
    {
        ["detective_name"] = DetectiveName(), ["accused_name"] = _game.Accused is { } accused ? Names.Of(accused) : "",
        ["wrong_name"] = _wrongName
    };
    private string _wrongName = "";
    private string DetectiveName() => _game.Continuation?.Progress.GetProgress("state").GetValueOrDefault("detective_name")?.ToString() ?? "侦探";
    private void Consume(IReadOnlyList<Chapter2NarrativeEvent> frames,List<Dictionary<string,object?>> output)
    {
        foreach (var frame in frames)
        {
            switch (frame.Kind)
            {
                case "lines":
                    _presentation = frame.PresentationId; _narrativePresentation = frame.PresentationId;
                    output.Add(Event("lines",("presentation_id",frame.PresentationId),("lines",frame.Lines.Select(l=>(object)new Dictionary<string,object?> { ["speaker"]=l.Speaker,["text"]=l.Text,["mood"]=l.Mood,["voice_clip"]=l.VoiceClip,["line_id"]=l.LineId,["source_id"]=l.SourceId }).ToArray()))); break;
                case "choice":
                    _behavior.RefreshExistingKnowledge();
                    var offered=_behavior.Process(new RequestCandidates(_actorIds[P.Det])).OfType<CandidatesAvailable>().SelectMany(c=>c.Candidates).ToDictionary(c=>c.Id,StringComparer.Ordinal);
                    var available=frame.Choices.Where(c=>offered.ContainsKey(c.CandidateId)).ToArray();
                    if(available.Length==0)throw new InvalidOperationException("当前没有适用的剧情候选。");
                    Menu("选择",available.Select(c=>(c.CandidateId,c.Label,(Func<bool>)(()=>_behavior.IsCandidateAvailable(c.CandidateId,_actorIds[P.Det])),(Action<List<Dictionary<string,object?>> >)(o=>
                    {
                        IReadOnlyList<WorldOutput> submitted;
                        try { submitted=_behavior.Process(new SelectCandidate(offered[c.CandidateId].Token)); }
                        catch
                        {
                            // Reoffer fresh Core tokens; a rejected submission must not leave an awaiting action.
                            Consume(new[] { frame },o);
                            throw;
                        }
                        if(submitted.OfType<CandidateActionStarted>().Any()) _awaitingNarrativeChoice=c.CandidateId;
                        BehaviorEvents(submitted,o);
                        if(_awaitingNarrativeChoice is not null)BehaviorEvents(_behavior.Process(new AdvanceWorld(_game.Now+5)),o);
                    }))),output,offered.ToDictionary(p=>p.Key,p=>p.Value.Token)); break;
                case "action":
                    switch (frame.ActionName)
                    {
                        case "lesson_answer": _game.HostLessonAnswer(bool.Parse(frame.Args[0])); break;
                        case "mediate": Mediate(frame.Args[0]); break;
                        default: throw new InvalidOperationException("未绑定作者行动："+frame.ActionName);
                    }
                    break;
            }
        }
    }
    private void Menu(string title,IEnumerable<(string Id,string Label,Func<bool> Available,Action<List<Dictionary<string,object?>>> Submit)> choices,List<Dictionary<string,object?>> output,IReadOnlyDictionary<string,string>? tokens=null)
    {
        Invalidate(); var options = new List<object>();
        foreach (var c in choices)
        {
            var token = tokens?.GetValueOrDefault(c.Id)??Guid.NewGuid().ToString("N"); _choices[c.Id] = (token,c.Available,c.Submit);
            options.Add(new Dictionary<string,object?> { ["id"]=c.Id,["label"]=c.Label,["token"]=token,["available"]=c.Available(),["lock_hint"]="" });
        }
        output.Add(Event("candidates",("title",title),("options",options)));
    }
    private void Scene(Place place,List<Dictionary<string,object?>> output)
    {
        _game.HostMoveDet(place); _knownPlaces.Add(place.ToString());
        var map = _fieldPresentation.PlaceMap(place.ToString());
        _map = map;
        output.Add(Event("scene",("map",map),("spawn",_fieldPresentation.MapSpawn(map))));
    }
    private void Run(PlayerAction? action,List<Dictionary<string,object?>> output)
    {
        ShownDay=_game.Day; ShownSlot=_game.Slot; _game.RunSlot(action); Invalidate();
        var feed=_game.TakeFeed().ToArray(); _journal.Add(new object[] { Chapter2Content.TimeName(ShownDay,ShownSlot),feed });
        _knownPlaces.Add(_game.Where(_game.Det).ToString());
        Lines(feed,output);
    }
    private void Commit(PlayerAction? action,List<Dictionary<string,object?>> output)
    {
        if (_game.Slot == Slot.Night && action?.Cmd != Cmd.Stakeout)
        { NightAction(output); return; }
        if (_game.Slot==Slot.Afternoon && Game.Get(_game.Det.Body,T.Hungry)>0 && action?.Cmd!=Cmd.Stay)
        { Lines([_content.Text("hungry")],output); return; }
        if (action is not null && !_game.HostPrepareSocialAction(action))
        { Lines(_game.TakeFeed(), output); return; }
        if (action?.Cmd == Cmd.Stay && _game.Slot is Slot.Noon or Slot.Evening && _game.Day > Game.FirstDay)
            action = new(Cmd.Collect);
        Run(action,output);
        Pump(output);
    }
    private PlayerAction? Action(IReadOnlyDictionary<string,object?> a)
    {
        var cmd=Text(a,"cmd","stay");
        P Who() { var who=Enum.Parse<P>(Text(a,"who")); if (who==P.Det || _game.Where(_game[who])!=_game.Where(_game.Det)) throw new InvalidOperationException("人物已离开现场。"); return who; }
        Place WatchPlace() { var place=Enum.Parse<Place>(Text(a,"place"));if(place!=_game.Where(_game.Det))throw new InvalidOperationException("只能在当前空间守夜。");return place; }
        return cmd switch
        {
            "sleep"=>null,"watch"=>new(Cmd.Stakeout){Place=WatchPlace()},"stay"=>new(Cmd.Stay){Place=_game.Where(_game.Det)},
            "collect"=>new(Cmd.Collect),"class"=>new(Cmd.Class),"mediate"=>new(Cmd.Mediate){Wording=Text(a,"wording")},"help"=>new(Cmd.Help){Who=Who()},
            "share" when Game.Get(_game.Det.Body,T.Fed)>0=>new(Cmd.ShareFood){Who=Who()},
            "request"=>new(Cmd.Ask){Who=Who(),Job=Enum.Parse<Job>(Text(a,"job"))},
            "escort" when _examined.Values.Any(c=>Text(c,"thing")==Text(a,"thing"))=>new(Cmd.Escort){Who=Who(),Thing=Text(a,"thing"),Place=_game.Things_[Text(a,"thing")].Place},
            "tell_rule"=>TellRule(Who(),Number(a,"rule")),
            "tell_wording"=>new(Cmd.TellRule){Who=Who(),Card=Game.CaseWordings[Number(a,"wording")].Card},
            "tell_archive_wording"=>new(Cmd.TellRule){Who=Who(),Card=Game.ArchiveWordings[Number(a,"wording")].Card},
            _=>throw new InvalidOperationException("无效或不可用行动："+cmd)
        };
    }
    private PlayerAction TellRule(P who,int id)
    { var spec=_game.HostTellable().FirstOrDefault(s=>s.Id==id)??throw new InvalidOperationException("该规则当前不能转述。"); return new(Cmd.TellRule){Who=who,Spec=spec,Card=spec.Card}; }

    private void Interact(IReadOnlyDictionary<string,object?> input,List<Dictionary<string,object?>> output)
    {
        if (_stage!="field" || _choices.Count>0) throw new InvalidOperationException("请先完成当前选择。");
        ShownDay=_game.Day; ShownSlot=_game.Slot;
        var map=Text(input,"map"); var site=Text(input,"site"); var target=Text(input,"target");
        if (site.StartsWith("person:",StringComparison.Ordinal)) target=site[7..];
        if (Enum.TryParse<P>(target,out var person)) { Talk(person,output); return; }
        var authored=_sites.SingleOrDefault(s=>s.Map==map&&s.LocalId==site)??throw new InvalidOperationException("未知交互地点。");
        var spec=SiteSpec(authored);
        if(_game.Where(_game.Det)!=authored.Room)throw new InvalidOperationException("交互地点已不在当前空间。");
        switch (Text(spec,"kind"))
        {
            case "bed": case "watch":
                if (_game.Slot!=Slot.Night) { Lines([Text(spec,"kind")=="bed"?_content.Text("bed_early"):_content.Text("watch_early")],output); return; }
                var isBed=Text(spec,"kind")=="bed";
                if (isBed) { NightAction(output); return; }
                Menu(isBed?"睡下":"守夜",[("confirm",isBed?"在这里睡到天亮":"在这里守一夜",()=>_game.Slot==Slot.Night,o=>Commit(isBed?null:new(Cmd.Stakeout){Place=_game.Where(_game.Det)},o)),("cancel","返回现场",()=>true,o=>{})],output); return;
            case "meal":
                if (_game.Day >= Game.RationFirstDay && _game.Slot is Slot.Noon or Slot.Evening) Commit(new(Cmd.Stay),output); else Lines([_content.Text("meal_closed")],output); return;
            case "tube": if (_game.Day==Game.CaseDay && _game.Slot==Slot.Evening) { BeginConfront(output); return; } break;
        }
        var thing=Text(spec,"thing");
        if(authored.Thing is not null)
        {
            var physical=_behavior.Entity(authored.Thing).Get<SpatialProperty>();
            if(physical is not null && !ReferenceEquals(physical.Space,_game.Space[_game.Where(_game.Det)]))
                throw new InvalidOperationException("物件不在当前真实空间。");
        }
        var facts=thing!=""?GranaryAspect(site,_game.HostExamine(thing)):authored.Thing is not null?_game.HostExamineEntity(_behavior.Entity(authored.Thing)):"";
        var lore=Text(spec,"lore");var text=lore==""?facts:facts==""?lore:lore+"\n"+facts;
        var id=map+":"+site; var card=new Dictionary<string,object?>{["id"]=id,["title"]=Text(spec,"title"),["text"]=text,["place"]=Names.Of(_game.Where(_game.Det)),["thing"]=thing};
        _examined[id]=card; ShowCard(id,card,output);
        // STORY-HARDCODE[CH2-033][logic][production]: 调查台账在案发日后解锁本案重演的预写逻辑。
        if (thing=="台账" && _game.Day>=Game.CaseDay && !_replayOpen) { _replayOpen=true; _continuations.Enqueue(o=>Activate("card.replay_unlock",o)); }
    }
    // 粮仓的两个交互点各看一面：门闩只说门闩，存粮只说存量。
    private static string GranaryAspect(string site,string facts)
    {
        var parts=facts.Split('；');
        if(parts.Length!=2||!parts[0].StartsWith("存粮"))return facts;
        return site switch { "stock"=>parts[0]+"。","latch"=>parts[1],_=>facts };
    }
    private Dictionary<string,string> SiteSpec(Chapter2AuthoredSite site)
    {
        var thing="";
        if(site.Thing is not null)
        {
            var entity=_behavior.Entity(site.Thing);
            thing=_game.Things_.FirstOrDefault(p=>ReferenceEquals(p.Value.Thing,entity)).Key??"";
        }
        return new(){["title"]=site.Title,["kind"]=site.Kind,["lore"]=site.Lore,["thing"]=thing};
    }
    public IReadOnlyDictionary<string,Dictionary<string,string>> SiteDefinitions(string map)=>_sites.Where(s=>s.Map==map).ToDictionary(s=>s.LocalId,SiteSpec,StringComparer.Ordinal);
    private bool Local(P who)=>who!=P.Det && !_game[who].Asleep && _game.Where(_game[who])==_game.Where(_game.Det) && (_nearby is null || _nearby.Contains(who.ToString()));
    private void ShowCard(string id,Dictionary<string,object?> card,List<Dictionary<string,object?>> output)
    {
        var thing=Text(card,"thing");
        var choices=new List<(string,string,Func<bool>,Action<List<Dictionary<string,object?>>>)>();
        choices.Add(("close",_content.Text("menu_return_field"),()=>true,o=>{}));
        Menu(Text(card,"title"),choices,output);
        var options=output[^1]["options"]; output.RemoveAt(output.Count-1); var result=new Dictionary<string,object?>(card){["kind"]="card",["options"]=options,["facts"]=card["text"],["source"]=card["place"]}; output.Add(result);
    }
    private void ResumeTalk(P who,List<Dictionary<string,object?>> output)
    {
        if(Local(who)) { Talk(who,output); return; }
        // A completed response does not keep a moving or sleeping body in place.
        // Close stale follow-up menus instead of failing the presentation receipt.
        Invalidate();
        output.Add(Event("interaction_closed",("actor",who.ToString()),("reason","actor_unavailable")));
    }
    private void AskReaction(P who,List<Dictionary<string,object?>> output)
    {
        _behavior.RefreshExistingKnowledge();
        var started=_behavior.Process(new RequestReaction(_actorIds[who]));BehaviorEvents(started,output);
        // 问话是“读”：回应不占时间，在当下结算（总纲 §四.2；多日交互审核 M8）。
        if(started.Any(e=>e is CandidateActionStarted))BehaviorEvents(_behavior.Process(new AdvanceWorld(_game.Now)),output);
    }
    private void BehaviorEvents(IReadOnlyList<WorldOutput> events,List<Dictionary<string,object?>> output)
    {
        var reports=new Dictionary<string,(string Actor,string Name,string Text,string Mood)>();
        string? completedNarrative=null;
        foreach(var item in events)
        {
            if(item is CandidateActionFinished narrativeResult&&narrativeResult.CandidateId==_awaitingNarrativeChoice)
            { if(narrativeResult.Succeeded)completedNarrative=_awaitingNarrativeChoice;_awaitingNarrativeChoice=null; }
            if(item is CandidateActionFinished finishedReaction&&finishedReaction.Succeeded&&_reactionIntents.GetValueOrDefault(finishedReaction.CandidateId)=="testimony")
            {
                var who=_actorIds.Single(p=>p.Value==finishedReaction.Actor).Key;
                // Core has already selected and completed this authored reaction. This registry
                // supplies its factual report, not another decision between possible responses.
                var report=_game.HostAskPresentation(who, selectedTestimony: true);_talked[who.ToString()]=report.Text;
                reports[finishedReaction.Actor]=(finishedReaction.Actor,Names.Of(who),report.Text,report.Emotion);
                foreach(var place in _game.Sightings.Where(s=>s.Witness==who&&s.Day>=_game.Day-1).Select(s=>s.At))_knownPlaces.Add(place.ToString());
            }
            if(item is SemanticDialogue dialogue)
            {
                _presentation=dialogue.PresentationId;_behaviorPresentation=dialogue.PresentationId;
                var lines=dialogue.Lines.Select(l=>(object)new Dictionary<string,object?>{["speaker"]=l.Speaker,["speaker_name"]=_actorIds.FirstOrDefault(p=>p.Value==l.Speaker) is var pair&&pair.Value is not null?Names.Of(pair.Key):l.Speaker,["text"]=l.Text,["mood"]=l.Mood,["voice_clip"]=l.VoiceClip,["line_id"]=l.LineId,["source_id"]=l.SourceId}).ToList();
                foreach(var report in reports.Values)lines.Add(new Dictionary<string,object?>{["speaker"]=report.Actor,["speaker_name"]=report.Name,["text"]=report.Text,["mood"]=report.Mood,["voice_clip"]=""});
                reports.Clear(); output.Add(Event("lines",("presentation_id",dialogue.PresentationId),("lines",lines)));
            }
            else if(item is NoApplicableCandidate unavailable) output.Add(Event("error",("message","当前没有适用的回应。"),("actor",unavailable.Actor)));
            else if(item is CandidateActionFinished finished&&!finished.Succeeded) output.Add(Event("error",("message","回应行动未完成："+finished.Failure)));
        }
        if(reports.Count>0)
        {
            _presentation=Guid.NewGuid().ToString("N");
            output.Add(Event("lines",("presentation_id",_presentation),("lines",reports.Values.Select(r=>(object)new Dictionary<string,object?>{["speaker"]=r.Actor,["speaker_name"]=r.Name,["text"]=r.Text,["mood"]=r.Mood,["voice_clip"]=""}).ToArray())));
        }
        if(completedNarrative is { } selected)Consume(_narrative.Select(selected),output);
    }
    // STORY-HARDCODE[CH2-025][logic][production]: 对质议程固定为本案的取用、门闩、台账及特定人物结局。
    private void BeginConfront(List<Dictionary<string,object?>> output)
    {
        _stage="confront"; _pairs.Clear(); _answered.Clear(); _agendaDone.Clear(); _skippedTopics.Clear(); Scene(Place.Hall,output);
        _continuations.Enqueue(o=>Activate("confront.begin",o)); _continuations.Enqueue(ConfrontStep); Pump(output);
    }
    private void ConfrontStep(List<Dictionary<string,object?>> output)
    {
        // 议程由现实里为真的主张决定（多日交互审核 M1）：先问取用（有几份问几遍，第二遍起问“其余的呢？”），再问门闩，最后问台账。
        string? topic=null;HashSet<int> expected=new();
        foreach(var t in new[]{"take","latch","ledger"})
        {
            if(_skippedTopics.Contains(t)) continue;
            expected=Enumerable.Range(0,CaseDesign.Claims.Count).Where(i=>CaseDesign.Claims[i].Topic==t&&!CaseDesign.Claims[i].Frames&&!_answered.Contains(i)&&_game.HostClaimTrue(i)).ToHashSet();
            if(expected.Count>0){topic=t;break;}
        }
        if(topic is null) { Activate("confront.rule",output); _continuations.Enqueue(ConfrontProposal); Pump(output); return; }
        _question=topic switch{"take"=>_agendaDone.Contains("take")?_confrontQuestions[1]:_confrontQuestions[0],"latch"=>_confrontQuestions[2],_=>_confrontQuestions[3]};
        _expected=expected;_topic=topic;
        Menu(_question,ClaimChoices(),output);
    }
    /// <summary>
    /// 能说的话：不带人的主张直接列出；“某人……”的模板先选事，再选人（菜单不一次列出几十句，也不替玩家点名）。
    /// 选中一人的那句才是真正说出口的主张。
    /// </summary>
    private List<(string,string,Func<bool>,Action<List<Dictionary<string,object?>>>)> ClaimChoices(string? template=null)
    {
        var (expected,topic)=(_expected,_topic);
        var choices=new List<(string,string,Func<bool>,Action<List<Dictionary<string,object?>>>)>();
        if(template is not null)
        {
            foreach(var (c,i) in CaseDesign.Claims.Select((c,i)=>(c,i)).Where(x=>x.c.Template==template))
                choices.Add(("claim:"+i,Names.Of(c.Person!.Value),()=>true,o=>ConfrontEvidence(expected,topic,i,o)));
            choices.Add(("back","换一句",()=>true,o=>Menu(_question,ClaimChoices(),o)));
            return choices;
        }
        foreach(var (c,i) in CaseDesign.Claims.Select((c,i)=>(c,i)))
        {
            if(c.Template is null) choices.Add(("claim:"+i,c.Text,()=>true,o=>ConfrontEvidence(expected,topic,i,o)));
            else if(TopId(i)=="template:"+i)
            {
                var t=c.Template;
                choices.Add((TopId(i),t.Replace("{人}","（谁）"),()=>true,o=>Menu("是谁",ClaimChoices(t),o)));
            }
        }
        choices.Add(("pause","暂且不答",()=>true,o=>{_skippedTopics.Add(topic);_agendaDone.Add(topic);Activate("confront.pause",o);_continuations.Enqueue(ConfrontStep);}));
        return choices;
    }
    /// <summary>某句主张在第一层菜单里的编号：模板主张归到它的模板项。</summary>
    public static string TopId(int claim)
    {
        var c=CaseDesign.Claims[claim];
        if(c.Template is null) return "claim:"+claim;
        var first=CaseDesign.Claims.Select((x,j)=>(x,j)).First(x=>x.x.Template==c.Template).j;
        return "template:"+first;
    }
    private void ConfrontEvidence(HashSet<int> expected,string topic,int said,List<Dictionary<string,object?>> output)
    {
        Menu("出示什么",Game.EvidenceNames.Select((name,i)=>(name,i)).Where(p=>_game.DetHasEvidence(p.i)).Select(p=>("evidence:"+p.i,p.name,(Func<bool>)(()=>_game.DetHasEvidence(p.i)),(Action<List<Dictionary<string,object?>> >)(o=>
        {
            var frames=CaseDesign.Claims[said].Frames;
            if((expected.Contains(said)||frames)&&_game.HostPairValid(said,p.i))
            {
                _pairs.Add((said,p.i));_answered.Add(said);if(!_agendaDone.Contains(topic))_agendaDone.Add(topic);
                if(frames) Activate("confront.frame",o); else Lines([_game.HostReaction(said)],o);
            }
            else { _wrongName=p.name; Activate(expected.Contains(said)?"confront.wrong_show":"confront.wrong_say",o); }
            _continuations.Enqueue(ConfrontStep);
        }))),output);
    }
    private void ConfrontProposal(List<Dictionary<string,object?>> output)
    {
        var choices=Game.CaseWordings.Select((w,i)=>("proposal:"+i,w.Text,(Func<bool>)(()=>true),(Action<List<Dictionary<string,object?>> >)(o=>ConfrontCommon(i,o)))).ToList();
        choices.Add(("none","不提新规矩",()=>true,o=>ConfrontCommon(-1,o))); Menu("今后如何",choices,output);
    }
    private void ConfrontCommon(int proposal,List<Dictionary<string,object?>> output)
    {
        void Finish(bool common,List<Dictionary<string,object?>> o) { Run(new(Cmd.Confront){Plan=new Game.ConfrontPlan(_pairs.ToArray(),proposal<0?null:Game.CaseWordings[proposal].Card,common)},o);_continuations.Enqueue(next=>Activate("confront.whisper",next));_stage="field"; }
        Menu("公开指控须附可检查的材料？",[("yes","提议共同规则",()=>true,o=>Finish(true,o)),("no","不提议",()=>true,o=>Finish(false,o))],output);
    }
    private void Council(List<Dictionary<string,object?>> output)
    {
        _stage="council"; _game.HostMoveDet(Place.Oath);_knownPlaces.Add(Place.Oath.ToString()); _map=_fieldPresentation.GatheringMap; output.Add(Event("scene",("map",_map),("spawn",_fieldPresentation.GatheringSpawn),("mode","court"))); Activate("council.begin",output); _continuations.Enqueue(CouncilProposal); Pump(output);
    }
    private void CouncilProposal(List<Dictionary<string,object?>> output)
    {
        var choices=Game.ArchiveWordings.Select((w,i)=>("archive:"+i,w.Text,(Func<bool>)(()=>true),(Action<List<Dictionary<string,object?>> >)(o=>{_game.CardSpec(Game.ArchiveWordings[i].Card,P.Det);CouncilSupport(o);}))).ToList();
        choices.Add(("none","不提新写法",()=>true,CouncilSupport)); Menu("名录库之议",choices,output);
    }
    private void CouncilSupport(List<Dictionary<string,object?>> output)
    {
        var choices=_game.ArchivePlans().Select((s,i)=>("support:"+i,s.Text,(Func<bool>)(()=>true),(Action<List<Dictionary<string,object?>> >)(o=>FinishCouncil(i,o)))).ToList();
        choices.Add(("abstain","不表态",()=>true,o=>FinishCouncil(-1,o)));Menu("支持哪一条",choices,output);
    }
    private void FinishCouncil(int support,List<Dictionary<string,object?>> output)
    {
        var ending=_game.Council(support);_stage="closing";
        Lines(_game.CouncilLog,output);
        _continuations.Enqueue(o=>Activate(ending==Ending.Sealed?"council.sealed":"council.box",o));
        _continuations.Enqueue(o=>
        {
            _ended=true;_stage="ended";
            o.AddRange(EndEvents(new Dictionary<string,object?>
            {
                ["result"]=ending.ToString(),["result_text"]=_game.ResultText,["case_rule_adopted"]=_game.CaseRuleAdopted,["common_rule_proposed"]=_game.CommonRuleProposed,
                ["quer"]=_game.Accused is not { } accused?"没有人被当众指控":_game.QuerCleared?$"{Names.Of(accused)}洗清了冤屈，住回寝区":_game.QuerOstracized?$"{Names.Of(accused)}被当成私取之人，搬出寝区独宿":$"{Names.Of(accused)}的事没有人再提",
                ["det_hooks"]=_game.Det.Hooks,["common_rule"]=_game.CommonRuleEstablished,["case_rule"]=_game.CaseRule?.Text??"",
                ["log"]=_game.CouncilLog.ToArray(),["hooks"]=_game.Npcs.Where(p=>p.Hooks>0).Select(p=>p.Name).ToArray(),["impressions"]=Impressions(),
                ["stands"]=_game.CouncilChoice.ToDictionary(p=>p.Key.ToString(),p=>Game.EndingText(Game.FamilyOf(p.Value)))
            }));
        });
    }
    private void Replay(IReadOnlyDictionary<string,object?> input,List<Dictionary<string,object?>> output)
    {
        if(!_replayOpen) throw new InvalidOperationException("尚未获得重演手稿。");
        var values=(input.GetValueOrDefault("vars") as IEnumerable<object?>)?.Select(v=>Convert.ToInt32(v)).ToArray()??throw new InvalidOperationException("缺少重演变量。");
        if(values.Length!=4||values.Where((v,i)=>v<0||v>=CaseVars.Options[i].Length).Any()) throw new InvalidOperationException("重演变量无效。");
        var (traces,world)=_game.ReplayCase(new(values[0],values[1],values[2],values[3]));var actual=_game.CaseActual??traces;var known=_game.DetEvidence();var looked=_game.DetLookedLitters();var r=traces.Describe(looked).ToArray();var a=actual.Describe(looked).ToArray();
        var findings=r.Select((t,i)=>(object)new Dictionary<string,object?>{["index"]=i,["name"]=_content.Texts("finding_titles",5)[i],["status"]=!known.Contains(i)?"unvisited":t.Text==a[i].Text?"match":"conflict",["text"]=!known.Contains(i)?"还没查过":t.Text==a[i].Text?a[i].Text:$"重演中：{t.Text}；现场：{a[i].Text}"}).ToArray();
        var full=r.Select((t,i)=>known.Contains(i)&&t.Text==a[i].Text).All(v=>v);
        _replayResult=new(){["findings"]=findings,["full_match"]=full,["steps"]=Game.InkSteps(world,Game.CaseNight).Select(s=>(object)new Dictionary<string,object?>{["t"]=s.Time,["who"]=s.Who.ToString(),["name"]=Names.Of(s.Who),["build"]=s.Build switch{Build.Small=>"瘦小",Build.Large=>"高大",_=>"中等"},["from"]=s.From.ToString(),["to"]=s.To.ToString(),["act"]=s.Act}).ToArray()};
        _replayVars=values.Select(v=>(object?)v).ToArray(); _lastRunVars=_replayVars.ToArray();
        if(!ChapterEvents.Flag(input,"skip"))
        {
            _presentation=Guid.NewGuid().ToString("N");
            output.Add(Event("close"));
            output.Add(Event("replay_play",("presentation_id",_presentation),("mode","campus"),("steps",_replayResult["steps"]),("full_match",full)));
        }
        _replayCount++; if(_replayCount==1&&!full)_continuations.Enqueue(o=>Activate("after_replay.first",o));if(full&&!_fullMatch){_fullMatch=true;_continuations.Enqueue(o=>Activate("after_replay.match",o));}
        _continuations.Enqueue(o=>o.Add(Event("manuscript")));
        Pump(output);
    }
    // STORY-HARDCODE[CH2-026][logic][production]: 人物印象候选名单固定为本案关键角色。
    private string[] Impressions()=>_game.Npcs.Where(p=>p.Id is P.Ward or P.Quer or P.Mora or P.Huo or P.Ruan or P.Margaret).Select(p=>
    {
        var (c,i)=_game.ImageOfDet(p);
        var text=c>=2?"说话不避着你":c>=1?"对你还算客气":c>=0?"对你话不多":"明显不想跟你多说";
        if(i>=2)text+="，信你的判断"; return p.Name+"："+text;
    }).ToArray();
    private Dictionary<string,object?> LegacySnapshot()=>new Dictionary<string,object?>
    {
        ["detective_name"]=DetectiveName(),["stage"]=_stage,["ended"]=_ended,["replay_open"]=_replayOpen,["replay_result"]=_replayResult,
        ["known_places"]=_knownPlaces.ToDictionary(k=>k,k=>(object?)true),["examined"]=_examined,["talked"]=_talked,["journal"]=_journal,
        ["people"]=_game.Npcs.Select(p=>(object)new Dictionary<string,object?>{["key"]=p.Id.ToString(),["name"]=p.Name,["place"]=_game.Where(p).ToString(),["doing"]=_game.LastDoing(p.Id,ShownDay,ShownSlot)?.Text??"",["frame"]=_game.LastDoing(p.Id,ShownDay,ShownSlot)?.Frame??"",["asleep"]=p.Asleep,["present"]=!p.Asleep&&_game.HostDormReleased(p),["displayable"]=!p.Asleep&&_game.HostDormReleased(p),["court_present"]=(_stage is "council" or "closing")&&Game.Get(p.Body,T.Sick)==0,["sick"]=Game.Get(p.Body,T.Sick)>0}).ToArray()
    };
}
